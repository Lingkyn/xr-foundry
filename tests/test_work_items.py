from __future__ import annotations

import copy
import importlib.util
import json
import shutil
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "scripts" / "validate_repository.py"
SPEC = importlib.util.spec_from_file_location("validate_repository_work_items", SCRIPT)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)

ITEMS = "docs/contributing/work-items.json"
SCHEMA = "docs/contributing/work-items.schema.json"
MILESTONES = "docs/milestones.md"


def base_payload() -> dict:
    return {
        "schema": "xr-foundry.work_items.v1",
        "version": "0.1.0",
        "policy": {
            "item_grants_no_permission": True,
            "acceptance_is_machine_checked_where_possible": True,
            "done_needs_proof_path": True,
            "disjoint_allowed_paths_run_in_parallel": True,
        },
        "items": [
            {
                "id": "WI-001",
                "title": "Demo source gate",
                "milestone_batch": "1a",
                "needs": "none",
                "decision_class": "routine",
                "size": "small",
                "status": "open",
                "depends_on": [],
                "read_first": ["AGENTS.md"],
                "allowed_paths": ["docs/standards/demo/"],
                "steps": ["Write the demo source manifest."],
                "acceptance": {
                    "commands": ["python scripts/validate_repository.py --json"],
                    "artifacts": ["docs/standards/demo/source-manifest.json"],
                },
                "evidence": "The manifest exists and the contract passes.",
                "done_proof": None,
            },
            {
                "id": "WI-002",
                "title": "Demo staged core",
                "milestone_batch": "1b",
                "needs": "none",
                "decision_class": "routine",
                "size": "large",
                "status": "open",
                "depends_on": ["WI-001"],
                "read_first": ["AGENTS.md"],
                "allowed_paths": ["staging/demo/"],
                "steps": ["Author the staged core with tests."],
                "acceptance": {"commands": ["python -m unittest tests.test_coverage_claims"], "artifacts": ["staging/demo/README.md"]},
                "evidence": "The coverage map passes the claims rule.",
                "done_proof": None,
            },
        ],
    }


class WorkItemsTests(unittest.TestCase):
    def setUp(self) -> None:
        self.tmp = Path(tempfile.mkdtemp(prefix="work-items-"))
        self.addCleanup(shutil.rmtree, self.tmp, True)
        (self.tmp / "docs" / "contributing").mkdir(parents=True)
        (self.tmp / "scripts").mkdir()
        shutil.copy(ROOT / SCHEMA, self.tmp / SCHEMA)
        (self.tmp / "AGENTS.md").write_text("# guide\n", encoding="utf-8")
        (self.tmp / "scripts" / "validate_repository.py").write_text("# stub\n", encoding="utf-8")
        (self.tmp / MILESTONES).write_text(
            "# Milestones\n\n## M1\n\n### Batch 1a: trust\n\n### Batch 1b: green\n", encoding="utf-8"
        )

    def write(self, payload: dict) -> list[str]:
        (self.tmp / ITEMS).write_text(json.dumps(payload), encoding="utf-8")
        return MODULE.validate_work_items(self.tmp)

    def test_valid_items_pass(self) -> None:
        self.assertEqual(self.write(base_payload()), [])

    def test_no_item_file_is_not_an_error(self) -> None:
        self.assertEqual(MODULE.validate_work_items(self.tmp), [])

    def test_schema_violation_is_reported(self) -> None:
        payload = base_payload()
        payload["items"][0]["needs"] = "wizard"
        self.assertTrue(any("work items" in e for e in self.write(payload)))

    def test_unknown_batch_is_reported(self) -> None:
        payload = base_payload()
        payload["items"][0]["milestone_batch"] = "2f"
        errors = self.write(payload)
        self.assertTrue(any("milestone_batch '2f' is not a '### Batch' heading" in e for e in errors), errors)

    def test_duplicate_id_and_unknown_dependency(self) -> None:
        payload = base_payload()
        payload["items"][1]["id"] = "WI-001"
        payload["items"][1]["depends_on"] = ["WI-009"]
        errors = self.write(payload)
        self.assertTrue(any("duplicate item id WI-001" in e for e in errors), errors)
        self.assertTrue(any("unknown item WI-009" in e for e in errors), errors)

    def test_dependency_cycle_is_reported(self) -> None:
        payload = base_payload()
        payload["items"][0]["depends_on"] = ["WI-002"]
        errors = self.write(payload)
        self.assertTrue(any("dependency cycle" in e for e in errors), errors)

    def test_missing_read_first_path_is_reported(self) -> None:
        payload = base_payload()
        payload["items"][0]["read_first"] = ["docs/nowhere.md"]
        errors = self.write(payload)
        self.assertTrue(any("read_first path does not exist: docs/nowhere.md" in e for e in errors), errors)

    def test_acceptance_command_must_name_repository_script(self) -> None:
        payload = base_payload()
        payload["items"][0]["acceptance"]["commands"] = ["python scripts/erase_everything.py"]
        errors = self.write(payload)
        self.assertTrue(any("not an accepted repository script" in e for e in errors), errors)

    def test_done_requires_existing_proof(self) -> None:
        payload = base_payload()
        payload["items"][0]["status"] = "done"
        errors = self.write(payload)
        self.assertTrue(any("status done requires a done_proof" in e for e in errors), errors)
        payload["items"][0]["done_proof"] = "AGENTS.md"
        self.assertEqual(self.write(payload), [])
        payload["items"][1]["done_proof"] = "AGENTS.md"
        errors = self.write(payload)
        self.assertTrue(any("done_proof is only allowed when status is done" in e for e in errors), errors)

    def test_relative_path_shape(self) -> None:
        payload = base_payload()
        payload["items"][0]["allowed_paths"] = ["/etc/"]
        payload["items"][0]["acceptance"]["artifacts"] = ["../outside"]
        errors = self.write(payload)
        self.assertEqual(sum("repository-relative" in e for e in errors), 2, errors)

    def test_hints_exist_for_each_error_shape(self) -> None:
        samples = [
            "work items WI-001: milestone_batch '2f' is not a '### Batch' heading in docs/milestones.md",
            "work items WI-001: depends_on names unknown item WI-009",
            "work items: dependency cycle WI-001 -> WI-002 -> WI-001",
            "work items WI-001: read_first path does not exist: x",
            "work items WI-001: acceptance command names a script that is not an accepted repository script: y",
            "work items WI-001: status done requires a done_proof path that exists in the tree",
            "work items WI-002: done_proof is only allowed when status is done",
        ]
        for item in MODULE.explain_errors(samples):
            self.assertTrue(item.get("hint"), item)

    def test_real_repository_items_pass(self) -> None:
        self.assertEqual(MODULE.validate_work_items(ROOT), [])


if __name__ == "__main__":
    unittest.main()
