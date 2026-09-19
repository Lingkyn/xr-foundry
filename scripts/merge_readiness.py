#!/usr/bin/env python3
"""Compute a merge-readiness verdict for a candidate branch from the repository's own rules.

The verdict is the process's answer to "may this change merge?". It is computed
from public, reproducible facts: the merge result, the repository contract, the
version-evidence rule (LESSON-008), changelog discipline, maturity and governance
boundaries, and (when pull-request metadata is supplied) independent human review.

The verdict grants no permission. Execution is GitHub's own auto-merge: once the
owner has made the CI `merge-readiness` job a required check and allowed auto-merge,
a pull request from a branch named by a live operating mandate merges by itself
when this verdict is `ready` and the change is routine (no governance, maturity,
or version-evidence change). Everything else, and every merge against a blocked
verdict, is a person's visible, accountable act (GOVERNANCE.md, "Process-decided
merges and lazy consensus"). Lazy consensus for governance deliberations is on by
default; `--no-lazy-consensus` evaluates under the pre-2026-09-15 rule.

A governance change is bound to the deliberation record that authorises it without
a separate flag: when `--deliberation-record` is not passed, the tool discovers
candidate records from the candidate's own changed files (any file added or
modified under `docs/governance/deliberations/`) and evaluates the governance
boundary against them — one candidate exactly as the explicit option would, several
candidates by requiring at least one that authorises the change. `--deliberation-
record` still wins when passed.

Usage:
    python scripts/merge_readiness.py --base origin/main --head HEAD --json
    python scripts/merge_readiness.py --head my-branch --pr-metadata pr.json --markdown

The command never writes to the repository, never pushes, and never merges. It may
create a detached worktree in a temporary directory to run the repository contract
against the merged tree.
"""

from __future__ import annotations

import argparse
import fnmatch
import json
import re
import shutil
import subprocess
import sys
import tempfile
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

SCHEMA = "xr-foundry.merge_readiness.v1"
CANONICAL_CONTRACT_COMMAND = [sys.executable, "scripts/validate_repository.py", "--json", "--run-contract-tests"]

GOVERNANCE_PATHS = (
    "GOVERNANCE.md",
    "docs/governance/governance-model.v1.json",
    "docs/governance/governance-model.schema.json",
    "docs/governance/agent-membership-model.v1.json",
    "docs/governance/agent-membership-model.schema.json",
    "docs/contributing/task-hall.md",
    "docs/contributing/task-hall.v1.json",
    "docs/contributing/task-hall.v1.schema.json",
    "docs/contributing/deliberation-protocol.md",
    ".github/CODEOWNERS",
)
GOVERNANCE_PREFIXES = ("docs/rfcs/",)
MANDATE_PREFIX = "docs/governance/mandates/"
DELIBERATION_PREFIX = "docs/governance/deliberations/"
DELIBERATION_CANDIDATE_STATUSES = {"A", "M"}
PACKAGE_SOURCE_SUFFIXES = (".cs", ".asmdef", ".uxml", ".uss", ".prefab", ".unity", ".asset")
CHANGELOG_EXEMPT_PREFIXES = ("docs/",)
CHANGELOG_EXEMPT_ROOT_FILES = {"README.md", "ROADMAP.md", "CONTRIBUTING.md", "SECURITY.md", "PROJECT_GITHUB_PLAYBOOK.md", "AGENTS.md", "CLAUDE.md"}
GOVERNANCE_DECISION_CLASSES = {"governance_policy", "constitutional_change"}
OBJECTION_DELTA_KINDS = {"risk", "counterexample"}
SHA_PATTERN = re.compile(r"\b[0-9a-f]{40}\b")


class MergeReadinessError(RuntimeError):
    """Raised when the inputs cannot be evaluated at all."""


@dataclass
class Check:
    id: str
    status: str  # pass | fail | unknown | info
    detail: str
    evidence: dict[str, Any] = field(default_factory=dict)

    def as_dict(self) -> dict[str, Any]:
        payload: dict[str, Any] = {"id": self.id, "status": self.status, "detail": self.detail}
        if self.evidence:
            payload["evidence"] = self.evidence
        return payload


def run_git(repo: Path, *args: str, check: bool = True) -> subprocess.CompletedProcess[str]:
    result = subprocess.run(
        ["git", "-C", str(repo), *args],
        capture_output=True,
        text=True,
        check=False,
    )
    if check and result.returncode != 0:
        raise MergeReadinessError(f"git {' '.join(args)} failed: {result.stderr.strip()}")
    return result


def rev_parse(repo: Path, ref: str) -> str:
    return run_git(repo, "rev-parse", "--verify", f"{ref}^{{commit}}").stdout.strip()


def read_blob(repo: Path, commit: str, path: str) -> str | None:
    result = run_git(repo, "show", f"{commit}:{path}", check=False)
    if result.returncode != 0:
        return None
    return result.stdout


def read_json_blob(repo: Path, commit: str, path: str) -> Any:
    text = read_blob(repo, commit, path)
    if text is None:
        return None
    try:
        return json.loads(text)
    except json.JSONDecodeError:
        return None


def merge_result(repo: Path, base: str, head: str) -> tuple[bool, str | None, list[str]]:
    """Return (clean, merged_tree_or_None, conflict_paths) for merging head into base."""

    merge_base = run_git(repo, "merge-base", base, head).stdout.strip()
    if merge_base == base:
        tree = run_git(repo, "rev-parse", f"{head}^{{tree}}").stdout.strip()
        return True, tree, []
    result = run_git(repo, "merge-tree", "--write-tree", "--name-only", base, head, check=False)
    lines = result.stdout.splitlines()
    if result.returncode == 0:
        return True, lines[0].strip() if lines else None, []
    if result.returncode == 1:
        # Output shape: tree OID, one conflicted path per line, a blank line, then messages.
        conflicts: list[str] = []
        for line in lines[1:]:
            if not line.strip():
                break
            conflicts.append(line.strip())
        return False, None, conflicts
    raise MergeReadinessError(f"git merge-tree failed: {result.stderr.strip()}")


def changed_files(repo: Path, base: str, head: str) -> dict[str, str]:
    merge_base = run_git(repo, "merge-base", base, head).stdout.strip()
    result = run_git(repo, "diff", "--name-status", "--no-renames", merge_base, head)
    changes: dict[str, str] = {}
    for line in result.stdout.splitlines():
        if not line.strip():
            continue
        status, _, path = line.partition("\t")
        changes[path.strip()] = status.strip()[:1]
    return changes


def package_paths(repo: Path, commit: str) -> dict[str, str]:
    catalog = read_json_blob(repo, commit, "package-catalog.json")
    packages = catalog.get("packages", []) if isinstance(catalog, dict) else []
    return {
        str(item.get("id")): str(item.get("path")).rstrip("/")
        for item in packages
        if isinstance(item, dict) and item.get("id") and item.get("path")
    }


def check_no_merge_conflict(clean: bool, conflicts: list[str]) -> Check:
    if clean:
        return Check("no_merge_conflict", "pass", "The head merges into the base without conflicts.")
    return Check(
        "no_merge_conflict",
        "fail",
        "The head conflicts with the base; merge the base into the head and resolve before review.",
        {"conflicts": conflicts},
    )


def check_repository_contract(
    repo: Path,
    base: str,
    head: str,
    merged_tree: str | None,
    command: list[str] | None,
    skip: bool,
) -> Check:
    if skip:
        return Check("repository_contract", "unknown", "Repository contract was not executed (--skip-contract).")
    if merged_tree is None:
        return Check("repository_contract", "unknown", "Repository contract cannot run on a conflicted merge.")
    merge_base = run_git(repo, "merge-base", base, head).stdout.strip()
    if merge_base == base:
        commit = head
    else:
        commit = run_git(
            repo,
            "commit-tree",
            merged_tree,
            "-p",
            head,
            "-p",
            base,
            "-m",
            "merge-readiness candidate (not for publication)",
        ).stdout.strip()
    worktree = Path(tempfile.mkdtemp(prefix="merge-readiness-"))
    run_git(repo, "worktree", "add", "--detach", str(worktree), commit)
    try:
        cmd = list(command) if command else list(CANONICAL_CONTRACT_COMMAND)
        result = subprocess.run(cmd, cwd=worktree, capture_output=True, text=True, check=False)
        payload: Any = None
        try:
            payload = json.loads(result.stdout.strip().splitlines()[-1]) if result.stdout.strip() else None
        except (json.JSONDecodeError, IndexError):
            payload = None
        if payload is None:
            start = result.stdout.find("{")
            if start >= 0:
                try:
                    payload = json.loads(result.stdout[start:])
                except json.JSONDecodeError:
                    payload = None
        status = payload.get("status") if isinstance(payload, dict) else None
        contract_tests = payload.get("contract_tests", {}) if isinstance(payload, dict) else {}
        tests_status = contract_tests.get("status") if isinstance(contract_tests, dict) else None
        evidence = {
            "command": cmd,
            "returncode": result.returncode,
            "validation_status": status,
            "contract_tests_status": tests_status,
        }
        if status == "pass" and tests_status == "pass" and result.returncode == 0:
            return Check("repository_contract", "pass", "Repository validation and contract tests pass on the merged tree.", evidence)
        errors = payload.get("errors", []) if isinstance(payload, dict) else []
        evidence["errors"] = errors[:20] if isinstance(errors, list) else []
        if not isinstance(payload, dict):
            evidence["stderr_tail"] = result.stderr[-2000:]
        return Check("repository_contract", "fail", "Repository validation or contract tests fail on the merged tree.", evidence)
    finally:
        run_git(repo, "worktree", "remove", "--force", str(worktree), check=False)
        shutil.rmtree(worktree, ignore_errors=True)


def check_version_bump_evidence(repo: Path, base: str, head: str, changes: dict[str, str]) -> Check:
    """LESSON-008: a package version moves only with the evidence that records it."""

    merge_base = run_git(repo, "merge-base", base, head).stdout.strip()
    paths_at_head = package_paths(repo, head)
    bumped: list[dict[str, Any]] = []
    problems: list[str] = []
    catalog_head = read_json_blob(repo, head, "package-catalog.json")
    profiles_head = read_json_blob(repo, head, "compatibility-profiles.json")
    catalog_versions = {
        str(item.get("id")): str(item.get("version"))
        for item in (catalog_head.get("packages", []) if isinstance(catalog_head, dict) else [])
        if isinstance(item, dict)
    }
    profile_list = profiles_head.get("profiles", []) if isinstance(profiles_head, dict) else []
    for package_id, package_path in paths_at_head.items():
        manifest_path = f"{package_path}/package.json"
        if manifest_path not in changes:
            continue
        before = read_json_blob(repo, merge_base, manifest_path)
        after = read_json_blob(repo, head, manifest_path)
        old_version = before.get("version") if isinstance(before, dict) else None
        new_version = after.get("version") if isinstance(after, dict) else None
        if old_version == new_version:
            continue
        record = {"package": package_id, "from": old_version, "to": new_version}
        bumped.append(record)
        if catalog_versions.get(package_id) != new_version:
            problems.append(f"{package_id}: package-catalog.json still records {catalog_versions.get(package_id)!r}")
        verified = [
            profile
            for profile in profile_list
            if isinstance(profile, dict)
            and profile.get("install_artifact") == package_id
            and profile.get("state") == "verified"
            and profile.get("package_versions", {}).get(package_id) == new_version
        ]
        if not verified:
            problems.append(f"{package_id}: no verified compatibility profile records version {new_version!r}")
        elif "compatibility-profiles.json" not in changes:
            problems.append(f"{package_id}: compatibility-profiles.json did not change with the version bump")
    if not bumped:
        return Check("version_bump_carries_evidence", "pass", "No package version changed; unverified work stays under Unreleased.")
    if problems:
        return Check(
            "version_bump_carries_evidence",
            "fail",
            "A package version moved without the evidence record for that version (LESSON-008).",
            {"bumped": bumped, "problems": problems},
        )
    return Check(
        "version_bump_carries_evidence",
        "pass",
        "Every package version change is recorded in the catalog and a verified compatibility profile.",
        {"bumped": bumped},
    )


def check_changelogs(repo: Path, head: str, changes: dict[str, str]) -> Check:
    paths_at_head = package_paths(repo, head)
    missing: list[str] = []
    for package_id, package_path in paths_at_head.items():
        touched = [path for path in changes if path.startswith(package_path + "/")]
        substantive = [path for path in touched if path != f"{package_path}/CHANGELOG.md"]
        if substantive and f"{package_path}/CHANGELOG.md" not in changes:
            missing.append(f"{package_path}/CHANGELOG.md")
    # Package-level changes are recorded in the package changelog; the root changelog
    # records repository-level changes (scripts, workflows, contracts, compositions).
    package_prefixes = tuple(path + "/" for path in paths_at_head.values())
    needs_root = any(
        not path.startswith(CHANGELOG_EXEMPT_PREFIXES)
        and not (package_prefixes and path.startswith(package_prefixes))
        and path != "CHANGELOG.md"
        and not (("/" not in path) and path in CHANGELOG_EXEMPT_ROOT_FILES)
        for path in changes
    )
    if needs_root and "CHANGELOG.md" not in changes:
        missing.append("CHANGELOG.md")
    if missing:
        return Check(
            "changelogs_updated",
            "fail",
            "Substantive changes must be recorded in the affected changelogs.",
            {"missing": missing},
        )
    return Check("changelogs_updated", "pass", "Every affected changelog changed with the code it describes.")


def check_maturity_unchanged(repo: Path, base: str, head: str, changes: dict[str, str]) -> Check:
    merge_base = run_git(repo, "merge-base", base, head).stdout.strip()
    promotions: list[str] = []
    for path in changes:
        if not path.endswith("foundry.component.json") and path != "package-catalog.json":
            continue
        before = read_json_blob(repo, merge_base, path)
        after = read_json_blob(repo, head, path)
        if path == "package-catalog.json":
            before_map = {
                item.get("id"): item.get("maturity")
                for item in (before.get("packages", []) if isinstance(before, dict) else [])
                if isinstance(item, dict)
            }
            after_map = {
                item.get("id"): item.get("maturity")
                for item in (after.get("packages", []) if isinstance(after, dict) else [])
                if isinstance(item, dict)
            }
            for package_id, maturity in after_map.items():
                if package_id in before_map and before_map[package_id] != maturity:
                    promotions.append(f"{package_id}: {before_map[package_id]} -> {maturity}")
        elif isinstance(before, dict) and isinstance(after, dict) and before.get("maturity") != after.get("maturity"):
            promotions.append(f"{after.get('id', path)}: {before.get('maturity')} -> {after.get('maturity')}")
    if promotions:
        return Check(
            "maturity_unchanged",
            "fail",
            "A maturity change is a promotion decision with its own evidence gate; it is not merge-ready by routine review.",
            {"promotions": sorted(set(promotions))},
        )
    return Check("maturity_unchanged", "pass", "No package maturity changed.")


def check_unity_evidence(repo: Path, head: str, changes: dict[str, str]) -> Check:
    paths_at_head = package_paths(repo, head)
    touched_sources = sorted(
        path
        for path in changes
        if any(path.startswith(package_path + "/") for package_path in paths_at_head.values())
        and path.endswith(PACKAGE_SOURCE_SUFFIXES)
    )
    if not touched_sources:
        return Check("unity_evidence", "pass", "No Unity package source changed; no Editor execution is owed.")
    receipts = [
        path
        for path in changes
        if path.startswith("docs/validation/") and path.endswith(".json") and changes[path] != "D"
    ]
    bound: list[dict[str, str]] = []
    for path in receipts:
        text = read_blob(repo, head, path) or ""
        for sha in sorted(set(SHA_PATTERN.findall(text))):
            ancestor = run_git(repo, "merge-base", "--is-ancestor", sha, head, check=False)
            if ancestor.returncode != 0:
                continue
            later = run_git(repo, "diff", "--name-only", sha, head, check=False).stdout.splitlines()
            if any(
                item.endswith(PACKAGE_SOURCE_SUFFIXES)
                and any(item.startswith(package_path + "/") for package_path in paths_at_head.values())
                for item in later
            ):
                continue
            bound.append({"receipt": path, "commit": sha})
    if bound:
        return Check(
            "unity_evidence",
            "pass",
            "A validation receipt in this change names a commit after which no package source changed.",
            {"receipts": bound, "sources": touched_sources[:50]},
        )
    return Check(
        "unity_evidence",
        "info",
        "Unity package sources changed without an Editor receipt for this head; merge is allowed only without any maturity, release, or device claim.",
        {"sources": touched_sources[:50]},
    )


def is_proposed_rfc(repo: Path, head: str, path: str, status: str) -> bool:
    if status != "A" or not path.startswith("docs/rfcs/"):
        return False
    text = read_blob(repo, head, path) or ""
    return "Status: **Proposed**" in text[:600]


def discover_deliberation_records(changes: dict[str, str]) -> list[str]:
    """Candidate deliberation records touched by this candidate: any file added or
    modified (not deleted) under docs/governance/deliberations/."""

    return sorted(
        path
        for path, status in changes.items()
        if path.startswith(DELIBERATION_PREFIX)
        and path.endswith(".json")
        and status in DELIBERATION_CANDIDATE_STATUSES
    )


def evaluate_deliberation_record(record: dict[str, Any], now: datetime, lazy_consensus: bool) -> tuple[bool, str, list[str], dict[str, Any]]:
    """Evaluate one deliberation record against the review-window rule.

    Returns (authorises_the_change, detail, problems, extra_evidence). This is the
    single-record rule that an explicit --deliberation-record and a lone discovered
    candidate both use unchanged.
    """

    problems: list[str] = []
    extra: dict[str, Any] = {}
    if record.get("decision_class") not in GOVERNANCE_DECISION_CLASSES:
        problems.append("decision_class must be governance_policy or constitutional_change")
    not_before = record.get("review_not_before")
    if not_before:
        extra["review_not_before"] = not_before
    try:
        not_before_dt = datetime.fromisoformat(str(not_before).replace("Z", "+00:00")) if not_before else None
    except ValueError:
        not_before_dt = None
    if not_before_dt is None:
        problems.append("review_not_before is required")
    elif not_before_dt > now:
        problems.append(f"the review window has not closed yet (review_not_before {not_before})")
    status = record.get("status")
    decision = record.get("decision")
    detail = ""
    if status == "resolved":
        decided_at = decision.get("decided_at") if isinstance(decision, dict) else None
        try:
            decided_dt = datetime.fromisoformat(str(decided_at).replace("Z", "+00:00")) if decided_at else None
        except ValueError:
            decided_dt = None
        if not isinstance(decision, dict) or decided_dt is None:
            problems.append("a resolved record must carry a decision with decided_at")
        elif not_before_dt is not None and decided_dt < not_before_dt:
            problems.append("the decision predates review_not_before")
        detail = "Governance changes are backed by a resolved deliberation record whose review window closed."
    elif status == "open" and not lazy_consensus:
        problems.append("an open record cannot satisfy the rule when lazy consensus is disabled")
    elif status == "open":
        # Lazy consensus: an open record whose window closed with no objection delta
        # resolves by process; the steward records decided_by process:<mandate>.
        objections = [
            item.get("id")
            for item in (record.get("deltas", []) if isinstance(record.get("deltas"), list) else [])
            if isinstance(item, dict) and item.get("kind") in OBJECTION_DELTA_KINDS
        ]
        if objections:
            problems.append(f"the open record carries unresolved objection deltas {objections}; a person must resolve them")
        elif decision is not None:
            problems.append("an open record cannot carry a decision")
        extra["lazy_consensus"] = not problems
        detail = "The review window closed with no objection delta: lazy consensus applies and the steward records the resolution."
    else:
        problems.append("record status must be open or resolved")
    return (not problems, detail, problems, extra)


def check_governance_boundary(
    repo: Path,
    head: str,
    changes: dict[str, str],
    deliberation_record: Path | None,
    now: datetime,
    lazy_consensus: bool = True,
) -> Check:
    governance_changes = sorted(
        path
        for path, status in changes.items()
        if (path in GOVERNANCE_PATHS or path.startswith(GOVERNANCE_PREFIXES))
        and not is_proposed_rfc(repo, head, path, status)
    )
    proposals = sorted(path for path, status in changes.items() if is_proposed_rfc(repo, head, path, status))
    mandate_changes = sorted(path for path in changes if path.startswith(MANDATE_PREFIX) and path.endswith(".json"))
    evidence: dict[str, Any] = {}
    if proposals:
        evidence["proposed_rfcs"] = proposals
    if mandate_changes:
        evidence["mandate_changes"] = mandate_changes
    if not governance_changes:
        detail = "No governance, RFC, Task Hall, or CODEOWNERS rule changed."
        if proposals:
            detail = "Only new Proposed RFCs were added; a proposal is not a rule change."
        if mandate_changes:
            detail += " Mandate records changed; they need a recorded maintainer decision note, no review window."
        return Check("governance_review_window", "pass", detail, evidence)
    evidence["governance_changes"] = governance_changes

    # An explicit --deliberation-record always wins over discovery.
    if deliberation_record is not None:
        try:
            record = json.loads(deliberation_record.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError) as error:
            evidence["record_error"] = str(error)
            return Check("governance_review_window", "fail", "The deliberation record cannot be read.", evidence)
        evidence["deliberation_record"] = str(deliberation_record)
        authorises, detail, problems, extra = evaluate_deliberation_record(record, now, lazy_consensus)
        evidence.update(extra)
        if not authorises:
            evidence["problems"] = problems
            return Check("governance_review_window", "fail", "The deliberation record does not satisfy the review-window rule.", evidence)
        return Check("governance_review_window", "pass", detail, evidence)

    # No explicit record: discover candidates from the candidate's own changed files
    # (a record added or modified under docs/governance/deliberations/).
    candidates = discover_deliberation_records(changes)
    if not candidates:
        return Check(
            "governance_review_window",
            "fail",
            "Governance rules changed without a resolved deliberation record; no candidate record was added or "
            f"modified under {DELIBERATION_PREFIX} in this change, and policy needs 7 days and constitutional "
            "changes 14 days of public review before a maintainer decision.",
            evidence,
        )
    evidence["candidate_deliberation_records"] = candidates

    if len(candidates) == 1:
        path = candidates[0]
        record = read_json_blob(repo, head, path)
        if not isinstance(record, dict):
            evidence["record_error"] = f"{path} is not a readable JSON object at {head}"
            return Check("governance_review_window", "fail", "The discovered deliberation record cannot be read.", evidence)
        evidence["deliberation_record"] = path
        authorises, detail, problems, extra = evaluate_deliberation_record(record, now, lazy_consensus)
        evidence.update(extra)
        if not authorises:
            evidence["problems"] = problems
            return Check("governance_review_window", "fail", "The discovered deliberation record does not satisfy the review-window rule.", evidence)
        return Check("governance_review_window", "pass", detail, evidence)

    # Several candidates: evaluate every one and require at least one that authorises
    # the change (the same single-record rule above, applied per candidate).
    rule = (
        "Several deliberation records were added or modified in this change; the rule requires at least one of "
        "them to authorise it under the same review-window rule a single record would need to satisfy."
    )
    per_record: dict[str, Any] = {}
    authorising: list[str] = []
    winning_detail = ""
    for path in candidates:
        record = read_json_blob(repo, head, path)
        if not isinstance(record, dict):
            per_record[path] = {"problems": [f"{path} is not a readable JSON object at {head}"]}
            continue
        authorises, detail, problems, extra = evaluate_deliberation_record(record, now, lazy_consensus)
        per_record[path] = {"authorises": authorises, "problems": problems, **extra}
        if authorises:
            authorising.append(path)
            winning_detail = detail
    evidence["candidate_results"] = per_record
    if authorising:
        evidence["authorising_records"] = authorising
        return Check("governance_review_window", "pass", f"{rule} {winning_detail}", evidence)
    return Check(
        "governance_review_window",
        "fail",
        f"{rule} None of the candidate records authorises this change.",
        evidence,
    )


def check_mandated_branch(repo: Path, head: str, head_branch: str | None, now: datetime) -> Check:
    """A process-executed merge is only for a branch that an unexpired operating mandate names."""

    if not head_branch:
        return Check("mandated_branch", "info", "No head branch supplied; process-executed merge eligibility is not evaluated.")
    listing = run_git(repo, "ls-tree", "--name-only", head, f"{MANDATE_PREFIX}", check=False).stdout.split()
    candidates: list[str] = []
    for path in listing:
        if not path.endswith(".mandate.json"):
            continue
        payload = read_json_blob(repo, head, path)
        if not isinstance(payload, dict):
            continue
        mandate_id = str(payload.get("mandate_id", path))
        branches = payload.get("resource_scope", {}).get("branches", []) if isinstance(payload.get("resource_scope"), dict) else []
        if not any(fnmatch.fnmatchcase(head_branch, str(pattern)) for pattern in branches):
            continue
        candidates.append(mandate_id)
        revocation = payload.get("revocation", {})
        if isinstance(revocation, dict) and revocation.get("status") == "revoked":
            continue
        try:
            not_before = datetime.fromisoformat(str(payload.get("not_before")).replace("Z", "+00:00"))
            expires_at = datetime.fromisoformat(str(payload.get("expires_at")).replace("Z", "+00:00"))
        except ValueError:
            continue
        if not_before <= now < expires_at:
            return Check(
                "mandated_branch",
                "pass",
                "The head branch is named by an unrevoked, unexpired operating mandate.",
                {"mandate_id": mandate_id, "head_branch": head_branch},
            )
    return Check(
        "mandated_branch",
        "fail",
        "The head branch is not covered by a live operating mandate; a person merges it.",
        {"head_branch": head_branch, "matching_but_inactive": candidates},
    )


def load_pr_metadata(path: Path | None) -> dict[str, Any] | None:
    if path is None:
        return None
    try:
        payload = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise MergeReadinessError(f"pull-request metadata cannot be read: {error}") from error
    if not isinstance(payload, dict):
        raise MergeReadinessError("pull-request metadata must be a JSON object")
    return payload


def metadata_from_github_reviews(raw_reviews: Any, *, author: str, draft: str | bool) -> dict[str, Any]:
    """Shape the GitHub REST pull-request reviews payload into the metadata this tool reads."""

    reviews: list[dict[str, str]] = []
    for review in raw_reviews if isinstance(raw_reviews, list) else []:
        if not isinstance(review, dict):
            continue
        user = review.get("user")
        login = user.get("login") if isinstance(user, dict) else None
        if not login:
            continue
        reviews.append({"login": str(login), "state": str(review.get("state", "")).upper()})
    is_draft = draft if isinstance(draft, bool) else str(draft).strip().casefold() == "true"
    return {"author_login": author, "draft": is_draft, "reviews": reviews}


def check_independent_review(metadata: dict[str, Any] | None, informational: bool) -> Check:
    if metadata is None:
        status = "info" if informational else "unknown"
        return Check("independent_review", status, "No pull-request metadata supplied; independent review cannot be evaluated here.")
    author = str(metadata.get("author_login", "")).strip().casefold()
    reviews = metadata.get("reviews", [])
    latest: dict[str, str] = {}
    for review in reviews if isinstance(reviews, list) else []:
        if not isinstance(review, dict):
            continue
        login = str(review.get("login", "")).strip().casefold()
        state = str(review.get("state", "")).strip().upper()
        if not login or login == author or login.endswith("[bot]"):
            continue
        if state in {"APPROVED", "CHANGES_REQUESTED"}:
            latest[login] = state
    approvers = sorted(login for login, state in latest.items() if state == "APPROVED")
    blockers = sorted(login for login, state in latest.items() if state == "CHANGES_REQUESTED")
    evidence = {"author": author or None, "approvers": approvers, "changes_requested": blockers}
    if blockers:
        return Check("independent_review", "fail", "A reviewer other than the author still requests changes.", evidence)
    if approvers:
        return Check("independent_review", "pass", "At least one human GitHub identity other than the author approved this head.", evidence)
    status = "info" if informational else "fail"
    return Check("independent_review", status, "No approval from a GitHub identity other than the author is recorded for this head.", evidence)


def check_not_draft(metadata: dict[str, Any] | None) -> Check:
    if metadata is None:
        return Check("not_draft", "info", "No pull-request metadata supplied.")
    if metadata.get("draft") is True:
        return Check("not_draft", "fail", "A draft pull request is not offered for merge.")
    return Check("not_draft", "pass", "The pull request is not a draft.")


def evaluate(
    repo: Path,
    base_ref: str,
    head_ref: str,
    *,
    pr_metadata: dict[str, Any] | None = None,
    deliberation_record: Path | None = None,
    skip_contract: bool = False,
    contract_command: list[str] | None = None,
    reviews_informational: bool = False,
    head_branch: str | None = None,
    lazy_consensus: bool = True,
    now: datetime | None = None,
) -> dict[str, Any]:
    now = now or datetime.now(timezone.utc)
    base = rev_parse(repo, base_ref)
    head = rev_parse(repo, head_ref)
    clean, merged_tree, conflicts = merge_result(repo, base, head)
    changes = changed_files(repo, base, head)
    checks: list[Check] = [check_no_merge_conflict(clean, conflicts)]
    checks.append(check_repository_contract(repo, base, head, merged_tree, contract_command, skip_contract))
    version_check = check_version_bump_evidence(repo, base, head, changes)
    checks.append(version_check)
    checks.append(check_changelogs(repo, head, changes))
    maturity_check = check_maturity_unchanged(repo, base, head, changes)
    checks.append(maturity_check)
    checks.append(check_unity_evidence(repo, head, changes))
    governance_check = check_governance_boundary(repo, head, changes, deliberation_record, now, lazy_consensus)
    checks.append(governance_check)
    draft_check = check_not_draft(pr_metadata)
    checks.append(draft_check)
    checks.append(check_independent_review(pr_metadata, reviews_informational))
    mandate_check = check_mandated_branch(repo, head, head_branch, now)
    checks.append(mandate_check)
    blocking = [check.id for check in checks if check.status == "fail" and check.id != "mandated_branch"]
    unknown = [check.id for check in checks if check.status == "unknown"]
    verdict = "ready" if not blocking and not unknown else "blocked"
    routine = (
        governance_check.status == "pass"
        and "governance_changes" not in governance_check.evidence
        and maturity_check.status == "pass"
        and not version_check.evidence.get("bumped")
    )
    decision_class = "routine_change" if routine else "non_routine"
    process_merge_eligible = (
        verdict == "ready"
        and routine
        and mandate_check.status == "pass"
        and draft_check.status != "fail"
    )
    return {
        "schema": SCHEMA,
        "evaluated_at": now.replace(microsecond=0).isoformat().replace("+00:00", "Z"),
        "base": {"ref": base_ref, "commit": base},
        "head": {"ref": head_ref, "commit": head, "branch": head_branch},
        "changed_files": len(changes),
        "checks": [check.as_dict() for check in checks],
        "verdict": verdict,
        "blocking": blocking,
        "unknown": unknown,
        "decision_class": decision_class,
        "process_merge_eligible": process_merge_eligible,
        "authority": {
            "verdict_is_binding": False,
            "grants_merge_permission": False,
            "process_merge_requires_live_mandate_and_routine_class": True,
            "override_requires_recorded_maintainer_reason": True,
        },
    }


def render_markdown(report: dict[str, Any]) -> str:
    icon = {"pass": "PASS", "fail": "FAIL", "unknown": "UNKNOWN", "info": "INFO"}
    lines = [
        f"## Merge readiness: **{report['verdict']}**",
        "",
        f"Base `{report['base']['commit'][:12]}` ({report['base']['ref']}), head `{report['head']['commit'][:12]}` ({report['head']['ref']}), {report['changed_files']} changed files.",
        "",
        "| Check | Result | Detail |",
        "| --- | --- | --- |",
    ]
    for check in report["checks"]:
        lines.append(f"| `{check['id']}` | {icon.get(check['status'], check['status'])} | {check['detail']} |")
    if report["blocking"]:
        lines += ["", "Blocking: " + ", ".join(f"`{item}`" for item in report["blocking"])]
    if report["unknown"]:
        lines += ["", "Not evaluated: " + ", ".join(f"`{item}`" for item in report["unknown"])]
    lines += [
        "",
        f"Decision class `{report['decision_class']}`; process-executed merge eligible: **{str(report['process_merge_eligible']).lower()}**.",
        "A merge against a blocked verdict needs a recorded maintainer reason.",
    ]
    return "\n".join(lines) + "\n"


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--repo", default=".", help="repository root (default: current directory)")
    parser.add_argument("--base", default="origin/main", help="base ref the change would merge into")
    parser.add_argument("--head", default="HEAD", help="candidate ref")
    parser.add_argument("--pr-metadata", type=Path, help="JSON file: {author_login, draft, reviews:[{login,state}]}")
    parser.add_argument("--github-reviews", type=Path, help="raw GitHub REST pulls/{n}/reviews payload (used with --pr-author/--pr-draft)")
    parser.add_argument("--pr-author", default="", help="pull-request author login for --github-reviews")
    parser.add_argument("--pr-draft", default="false", help="true/false draft flag for --github-reviews")
    parser.add_argument("--head-branch", help="head branch name, matched against operating-mandate branch patterns")
    parser.add_argument("--no-lazy-consensus", dest="lazy_consensus", action="store_false", help="reject an open deliberation record even when its window closed without objection (pre-2026-09-15 rule)")
    parser.add_argument("--deliberation-record", type=Path, help="explicit deliberation record for governance changes; overrides discovery from the candidate's changed files under docs/governance/deliberations/")
    parser.add_argument("--skip-contract", action="store_true", help="do not run the repository contract on the merged tree")
    parser.add_argument("--contract-command", help="override the contract command (shell words, JSON list)")
    parser.add_argument("--reviews-informational", action="store_true", help="report missing review as info instead of blocking")
    parser.add_argument("--json", action="store_true", help="print the JSON verdict")
    parser.add_argument("--markdown", action="store_true", help="print a Markdown summary")
    parser.add_argument("--output", type=Path, help="write the JSON verdict to this path")
    return parser


def main(argv: list[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    repo = Path(args.repo).resolve()
    command = json.loads(args.contract_command) if args.contract_command else None
    try:
        metadata = load_pr_metadata(args.pr_metadata)
        if args.github_reviews is not None:
            try:
                raw = json.loads(args.github_reviews.read_text(encoding="utf-8"))
            except (OSError, json.JSONDecodeError) as error:
                raise MergeReadinessError(f"GitHub reviews payload cannot be read: {error}") from error
            metadata = metadata_from_github_reviews(raw, author=args.pr_author, draft=args.pr_draft)
        report = evaluate(
            repo,
            args.base,
            args.head,
            pr_metadata=metadata,
            deliberation_record=args.deliberation_record,
            skip_contract=args.skip_contract,
            contract_command=command,
            reviews_informational=args.reviews_informational,
            head_branch=args.head_branch,
            lazy_consensus=args.lazy_consensus,
        )
    except MergeReadinessError as error:
        print(json.dumps({"schema": SCHEMA, "verdict": "blocked", "error": str(error)}, indent=2))
        return 2
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    if args.markdown:
        print(render_markdown(report))
    if args.json or not args.markdown:
        print(json.dumps(report, indent=2))
    return 0 if report["verdict"] == "ready" else 1


if __name__ == "__main__":
    sys.exit(main())
