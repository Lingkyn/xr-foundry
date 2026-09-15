from __future__ import annotations

import contextlib
import importlib.util
import io
import json
import subprocess
import sys
import tempfile
import unittest
from datetime import datetime, timezone
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "scripts" / "merge_readiness.py"
SPEC = importlib.util.spec_from_file_location("merge_readiness", SCRIPT)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE  # dataclasses resolve string annotations through sys.modules
SPEC.loader.exec_module(MODULE)

PASSING_CONTRACT = [
    sys.executable,
    "-c",
    "import json; print(json.dumps({'status': 'pass', 'contract_tests': {'status': 'pass'}}))",
]
FAILING_CONTRACT = [
    sys.executable,
    "-c",
    "import json; print(json.dumps({'status': 'fail', 'errors': ['boom'], 'contract_tests': {'status': 'skipped'}}))",
]
NOW = datetime(2026, 9, 30, tzinfo=timezone.utc)
PACKAGE = "packages/unity/demo"


def git(repo: Path, *args: str) -> str:
    result = subprocess.run(
        ["git", "-C", str(repo), "-c", "user.name=t", "-c", "user.email=t@example.com", *args],
        capture_output=True,
        text=True,
        check=True,
    )
    return result.stdout.strip()


def write(repo: Path, path: str, content: str) -> None:
    target = repo / path
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(content, encoding="utf-8")


def write_json(repo: Path, path: str, payload: dict) -> None:
    write(repo, path, json.dumps(payload, indent=2) + "\n")


def commit_all(repo: Path, message: str) -> str:
    git(repo, "add", "-A")
    git(repo, "commit", "-q", "-m", message)
    return git(repo, "rev-parse", "HEAD")


def make_repo(directory: str) -> Path:
    repo = Path(directory)
    git(repo, "init", "-q", "-b", "main")
    write_json(
        repo,
        "package-catalog.json",
        {"packages": [{"id": "com.lingkyn.demo", "path": PACKAGE, "version": "0.1.0", "maturity": "incubating"}]},
    )
    write_json(
        repo,
        "compatibility-profiles.json",
        {
            "profiles": [
                {
                    "id": "demo-profile",
                    "state": "verified",
                    "install_artifact": "com.lingkyn.demo",
                    "package_versions": {"com.lingkyn.demo": "0.1.0"},
                }
            ]
        },
    )
    write_json(repo, f"{PACKAGE}/package.json", {"name": "com.lingkyn.demo", "version": "0.1.0"})
    write_json(repo, f"{PACKAGE}/foundry.component.json", {"id": "com.lingkyn.demo", "version": "0.1.0", "maturity": "incubating"})
    write(repo, f"{PACKAGE}/CHANGELOG.md", "# Changelog\n\n## Unreleased\n")
    write(repo, f"{PACKAGE}/Runtime/Demo.cs", "namespace Lingkyn.Demo { public static class Demo {} }\n")
    write(repo, "CHANGELOG.md", "# Changelog\n\n## Unreleased\n")
    write(repo, "GOVERNANCE.md", "# Governance\n")
    write(repo, "docs/readme.md", "# Docs\n")
    write(repo, "scripts/tool.py", "print('tool')\n")
    commit_all(repo, "base")
    return repo


def branch(repo: Path, name: str) -> None:
    git(repo, "checkout", "-q", "-b", name, "main")


def evaluate(repo: Path, head: str = "HEAD", **overrides):
    options = {
        "skip_contract": True,
        "now": NOW,
        "pr_metadata": {"author_login": "author", "draft": False, "reviews": [{"login": "reviewer", "state": "APPROVED"}]},
    }
    options.update(overrides)
    return MODULE.evaluate(repo, "main", head, **options)


def statuses(report: dict) -> dict[str, str]:
    return {check["id"]: check["status"] for check in report["checks"]}


class MergeReadinessTests(unittest.TestCase):
    def test_docs_change_with_passing_contract_and_review_is_ready(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            repo = make_repo(directory)
            branch(repo, "docs")
            write(repo, "docs/readme.md", "# Docs\n\nMore.\n")
            commit_all(repo, "docs")
            report = evaluate(repo, skip_contract=False, contract_command=PASSING_CONTRACT)
            self.assertEqual("ready", report["verdict"], json.dumps(report, indent=2))
            self.assertEqual("pass", statuses(report)["repository_contract"])
            self.assertEqual("pass", statuses(report)["unity_evidence"])
            self.assertEqual([], git(repo, "worktree", "list", "--porcelain").split("\n\n")[1:], "temporary worktree must be removed")

    def test_failing_contract_blocks(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            repo = make_repo(directory)
            branch(repo, "docs")
            write(repo, "docs/readme.md", "changed\n")
            commit_all(repo, "docs")
            report = evaluate(repo, skip_contract=False, contract_command=FAILING_CONTRACT)
            self.assertEqual("blocked", report["verdict"])
            self.assertIn("repository_contract", report["blocking"])

    def test_skipped_contract_is_unknown_and_blocks(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            repo = make_repo(directory)
            branch(repo, "docs")
            write(repo, "docs/readme.md", "changed\n")
            commit_all(repo, "docs")
            report = evaluate(repo)
            self.assertEqual("blocked", report["verdict"])
            self.assertEqual(["repository_contract"], report["unknown"])
            self.assertEqual([], report["blocking"])

    def test_version_bump_without_catalog_and_profile_evidence_fails(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            repo = make_repo(directory)
            branch(repo, "bump")
            write_json(repo, f"{PACKAGE}/package.json", {"name": "com.lingkyn.demo", "version": "0.2.0"})
            write(repo, f"{PACKAGE}/CHANGELOG.md", "# Changelog\n\n## 0.2.0\n")
            commit_all(repo, "bump")
            report = evaluate(repo)
            self.assertIn("version_bump_carries_evidence", report["blocking"])
            check = next(item for item in report["checks"] if item["id"] == "version_bump_carries_evidence")
            self.assertEqual([{"package": "com.lingkyn.demo", "from": "0.1.0", "to": "0.2.0"}], check["evidence"]["bumped"])
            self.assertEqual(2, len(check["evidence"]["problems"]))

    def test_version_bump_with_recorded_evidence_passes(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            repo = make_repo(directory)
            branch(repo, "bump")
            write_json(repo, f"{PACKAGE}/package.json", {"name": "com.lingkyn.demo", "version": "0.2.0"})
            write_json(
                repo,
                "package-catalog.json",
                {"packages": [{"id": "com.lingkyn.demo", "path": PACKAGE, "version": "0.2.0", "maturity": "incubating"}]},
            )
            write_json(
                repo,
                "compatibility-profiles.json",
                {
                    "profiles": [
                        {
                            "id": "demo-profile",
                            "state": "verified",
                            "install_artifact": "com.lingkyn.demo",
                            "package_versions": {"com.lingkyn.demo": "0.2.0"},
                        }
                    ]
                },
            )
            write(repo, f"{PACKAGE}/CHANGELOG.md", "# Changelog\n\n## 0.2.0\n")
            write(repo, "CHANGELOG.md", "# Changelog\n\n## Unreleased\n\n- bump\n")
            commit_all(repo, "bump with evidence")
            report = evaluate(repo)
            self.assertEqual("pass", statuses(report)["version_bump_carries_evidence"])
            self.assertEqual("pass", statuses(report)["changelogs_updated"])

    def test_package_source_change_needs_package_changelog_and_reports_pending_editor_evidence(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            repo = make_repo(directory)
            branch(repo, "code")
            write(repo, f"{PACKAGE}/Runtime/Demo.cs", "namespace Lingkyn.Demo { public static class Demo { public const int X = 1; } }\n")
            commit_all(repo, "code without changelog")
            report = evaluate(repo)
            self.assertIn("changelogs_updated", report["blocking"])
            check = next(item for item in report["checks"] if item["id"] == "changelogs_updated")
            self.assertEqual([f"{PACKAGE}/CHANGELOG.md"], check["evidence"]["missing"])
            self.assertEqual("info", statuses(report)["unity_evidence"])

            write(repo, f"{PACKAGE}/CHANGELOG.md", "# Changelog\n\n## Unreleased\n\n- X\n")
            commit_all(repo, "changelog")
            report = evaluate(repo)
            self.assertEqual("pass", statuses(report)["changelogs_updated"])

    def test_receipt_binding_head_commit_satisfies_unity_evidence(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            repo = make_repo(directory)
            branch(repo, "code")
            write(repo, f"{PACKAGE}/Runtime/Demo.cs", "// changed\n")
            write(repo, f"{PACKAGE}/CHANGELOG.md", "# Changelog\n\n## Unreleased\n\n- changed\n")
            first = commit_all(repo, "code")
            write_json(repo, "docs/validation/receipt.json", {"commit": "0" * 40})
            commit_all(repo, "receipt naming an unknown commit")
            self.assertEqual("info", statuses(evaluate(repo))["unity_evidence"])

            write_json(repo, "docs/validation/receipt.json", {"commit": first})
            commit_all(repo, "receipt naming the observed commit")
            report = evaluate(repo)
            self.assertEqual("pass", statuses(report)["unity_evidence"], json.dumps(report, indent=2))

            write(repo, f"{PACKAGE}/Runtime/Demo.cs", "// changed again after the receipt\n")
            commit_all(repo, "code after receipt")
            self.assertEqual("info", statuses(evaluate(repo))["unity_evidence"])

    def test_repository_level_change_needs_root_changelog(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            repo = make_repo(directory)
            branch(repo, "tool")
            write(repo, "scripts/tool.py", "print('v2')\n")
            commit_all(repo, "tool")
            report = evaluate(repo)
            check = next(item for item in report["checks"] if item["id"] == "changelogs_updated")
            self.assertEqual(["CHANGELOG.md"], check["evidence"]["missing"])

    def test_maturity_change_is_not_routine(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            repo = make_repo(directory)
            branch(repo, "promote")
            write_json(repo, f"{PACKAGE}/foundry.component.json", {"id": "com.lingkyn.demo", "version": "0.1.0", "maturity": "candidate"})
            write(repo, f"{PACKAGE}/CHANGELOG.md", "# Changelog\n\n## Unreleased\n\n- promote\n")
            commit_all(repo, "promote")
            report = evaluate(repo)
            self.assertIn("maturity_unchanged", report["blocking"])

    def test_governance_change_requires_resolved_record_after_window(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            repo = make_repo(directory)
            branch(repo, "gov")
            write(repo, "GOVERNANCE.md", "# Governance\n\nNew rule.\n")
            commit_all(repo, "gov")
            self.assertIn("governance_review_window", evaluate(repo)["blocking"])

            record = Path(directory) / "record.json"
            record.write_text(
                json.dumps(
                    {
                        "status": "open",
                        "decision_class": "governance_policy",
                        "review_not_before": "2026-09-22T09:00:00Z",
                        "decision": None,
                    }
                ),
                encoding="utf-8",
            )
            self.assertIn("governance_review_window", evaluate(repo, deliberation_record=record)["blocking"])

            record.write_text(
                json.dumps(
                    {
                        "status": "resolved",
                        "decision_class": "governance_policy",
                        "review_not_before": "2026-09-22T09:00:00Z",
                        "decision": {"option_id": "OPT-KEEP", "decided_by": "@maintainer", "decided_at": "2026-09-23T09:00:00Z"},
                    }
                ),
                encoding="utf-8",
            )
            self.assertEqual("pass", statuses(evaluate(repo, deliberation_record=record))["governance_review_window"])

            early = evaluate(repo, deliberation_record=record, now=datetime(2026, 9, 20, tzinfo=timezone.utc))
            self.assertIn("governance_review_window", early["blocking"])

    def test_new_proposed_rfc_is_a_proposal_not_a_rule_change(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            repo = make_repo(directory)
            branch(repo, "rfc")
            write(repo, "docs/rfcs/0099-example.md", "# RFC 0099: Example\n\nStatus: **Proposed**\n")
            commit_all(repo, "rfc")
            report = evaluate(repo)
            self.assertEqual("pass", statuses(report)["governance_review_window"])
            check = next(item for item in report["checks"] if item["id"] == "governance_review_window")
            self.assertEqual(["docs/rfcs/0099-example.md"], check["evidence"]["proposed_rfcs"])

    def test_conflict_is_reported_with_paths(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            repo = make_repo(directory)
            branch(repo, "feature")
            write(repo, "docs/readme.md", "feature\n")
            commit_all(repo, "feature")
            git(repo, "checkout", "-q", "main")
            write(repo, "docs/readme.md", "main moved\n")
            commit_all(repo, "main")
            report = evaluate(repo, head="feature")
            self.assertIn("no_merge_conflict", report["blocking"])
            check = next(item for item in report["checks"] if item["id"] == "no_merge_conflict")
            self.assertEqual(["docs/readme.md"], check["evidence"]["conflicts"])
            self.assertEqual("unknown", statuses(report)["repository_contract"])

    def test_review_rules(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            repo = make_repo(directory)
            branch(repo, "docs")
            write(repo, "docs/readme.md", "changed\n")
            commit_all(repo, "docs")
            self_approved = {"author_login": "Author", "draft": False, "reviews": [{"login": "author", "state": "APPROVED"}]}
            self.assertIn("independent_review", evaluate(repo, pr_metadata=self_approved)["blocking"])
            informational = evaluate(repo, pr_metadata=self_approved, reviews_informational=True)
            self.assertEqual("info", statuses(informational)["independent_review"])
            changes = {"author_login": "author", "draft": False, "reviews": [{"login": "r", "state": "APPROVED"}, {"login": "r", "state": "CHANGES_REQUESTED"}]}
            self.assertIn("independent_review", evaluate(repo, pr_metadata=changes)["blocking"])
            bot = {"author_login": "author", "draft": True, "reviews": [{"login": "review-bot[bot]", "state": "APPROVED"}]}
            report = evaluate(repo, pr_metadata=bot)
            self.assertIn("not_draft", report["blocking"])
            self.assertIn("independent_review", report["blocking"])
            self.assertEqual("unknown", statuses(evaluate(repo, pr_metadata=None))["independent_review"])

    def test_markdown_and_cli(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            repo = make_repo(directory)
            branch(repo, "docs")
            write(repo, "docs/readme.md", "changed\n")
            commit_all(repo, "docs")
            output = Path(directory) / "out" / "verdict.json"
            stdout = io.StringIO()
            with contextlib.redirect_stdout(stdout):
                code = MODULE.main(
                [
                    "--repo",
                    str(repo),
                    "--base",
                    "main",
                    "--head",
                    "HEAD",
                    "--contract-command",
                    json.dumps(PASSING_CONTRACT),
                    "--reviews-informational",
                    "--output",
                    str(output),
                    "--markdown",
                ]
                )
            self.assertEqual(0, code)
            self.assertIn("Merge readiness: **ready**", stdout.getvalue())
            report = json.loads(output.read_text(encoding="utf-8"))
            self.assertEqual(MODULE.SCHEMA, report["schema"])
            self.assertEqual("ready", report["verdict"])
            self.assertFalse(report["authority"]["verdict_is_binding"])
            markdown = MODULE.render_markdown(report)
            self.assertIn("Merge readiness: **ready**", markdown)
            self.assertIn("`independent_review`", markdown)

    def test_github_review_payload_is_shaped_into_metadata(self) -> None:
        raw = [
            {"user": {"login": "reviewer"}, "state": "APPROVED"},
            {"user": {"login": "author"}, "state": "COMMENTED"},
            {"user": None, "state": "APPROVED"},
        ]
        metadata = MODULE.metadata_from_github_reviews(raw, author="author", draft="false")
        self.assertEqual("author", metadata["author_login"])
        self.assertFalse(metadata["draft"])
        self.assertEqual([{"login": "reviewer", "state": "APPROVED"}, {"login": "author", "state": "COMMENTED"}], metadata["reviews"])


if __name__ == "__main__":
    unittest.main()
