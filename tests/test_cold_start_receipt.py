from __future__ import annotations

import importlib.util
import json
import shutil
import sys
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
VALIDATOR = ROOT / "scripts" / "validate_repository.py"
SCHEMA = "docs/contributing/cold-start-receipt.schema.json"
TEMPLATE = "docs/contributing/cold-start-receipt.template.json"
CAPABILITY_PROFILES = "docs/contributing/capability-profiles.json"
RECEIPT_DIR = "docs/validation/cold-start"

GOOD_COMMIT = "a" * 40
BAD_COMMIT = "b" * 40


def load(path: Path, name: str):
    spec = importlib.util.spec_from_file_location(name, path)
    assert spec and spec.loader
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module  # dataclasses in the target module need this
    spec.loader.exec_module(module)
    return module


MODULE = load(VALIDATOR, "validate_repository_cold_start_receipt")


def fake_commit_reachable(root: Path, commit_sha: str) -> bool:
    return commit_sha == GOOD_COMMIT


def base_capability_profiles() -> dict:
    return {
        "schema": "xr-foundry.capability_profiles.v1",
        "version": "0.1.0",
        "policy": {},
        "profiles": [
            {"id": "ai_tokens_only", "title": "AI budget and a clone"},
            {"id": "unity_editor", "title": "A machine with the pinned Unity Editor"},
        ],
    }


def valid_receipt(receipt_id: str = "sample-cold-start") -> dict:
    return {
        "schema": "xr-foundry.cold_start_receipt.v1",
        "id": receipt_id,
        "contributor": "octocat",
        "capability": "ai_tokens_only",
        "tool": "Claude Code",
        "started_from_commit": GOOD_COMMIT,
        "pull_request": "https://github.com/Lingkyn/xr-foundry/pull/123",
        "verdict": "ready",
        "merged": True,
        "steps": [
            {"name": "clone", "minutes": 2, "note": "Cloned and set up the venv."},
            {"name": "install", "minutes": 1, "note": "Installed contract-requirements."},
            {"name": "change", "minutes": 5, "note": "Added one CHANGELOG line."},
            {"name": "verdict", "minutes": 1, "note": "merge_readiness printed READY."},
            {"name": "push", "minutes": 1, "note": "Pushed the branch."},
            {"name": "pull_request", "minutes": 2, "note": "Opened the five-line PR."},
        ],
        "confusing": [],
        "recorded_at": "2026-09-17T12:00:00Z",
        "non_claims": [
            "This does not claim the change was reviewed by the maintainer.",
        ],
    }


class ColdStartReceiptTests(unittest.TestCase):
    def setUp(self) -> None:
        self.tmp = Path(tempfile.mkdtemp(prefix="cold-start-receipt-"))
        self.addCleanup(shutil.rmtree, self.tmp, True)
        (self.tmp / "docs" / "contributing").mkdir(parents=True)
        shutil.copy(ROOT / SCHEMA, self.tmp / SCHEMA)
        (self.tmp / CAPABILITY_PROFILES).write_text(
            json.dumps(base_capability_profiles()), encoding="utf-8"
        )
        self._real_reachable = MODULE.commit_is_public_origin_reachable
        MODULE.commit_is_public_origin_reachable = fake_commit_reachable
        self.addCleanup(self._restore_reachable)

    def _restore_reachable(self) -> None:
        MODULE.commit_is_public_origin_reachable = self._real_reachable

    def receipt_dir(self) -> Path:
        directory = self.tmp / RECEIPT_DIR
        directory.mkdir(parents=True, exist_ok=True)
        return directory

    def write_receipt(self, filename: str, payload: dict) -> list[str]:
        (self.receipt_dir() / filename).write_text(json.dumps(payload), encoding="utf-8")
        return MODULE.validate_cold_start_receipts(self.tmp)

    def test_valid_receipt_passes(self) -> None:
        errors = self.write_receipt("sample-cold-start.json", valid_receipt())
        self.assertEqual(errors, [])

    def test_absent_directory_is_not_an_error(self) -> None:
        self.assertEqual(MODULE.validate_cold_start_receipts(self.tmp), [])

    def test_schema_violation_is_reported(self) -> None:
        payload = valid_receipt()
        del payload["verdict"]
        errors = self.write_receipt("sample-cold-start.json", payload)
        self.assertTrue(any("JSON Schema violation" in error for error in errors), errors)

    def test_owner_as_contributor_is_reported(self) -> None:
        payload = valid_receipt()
        payload["contributor"] = "Lingkyn"
        errors = self.write_receipt("sample-cold-start.json", payload)
        self.assertTrue(
            any("must not be the repository owner" in error for error in errors), errors
        )

    def test_undeclared_capability_is_reported(self) -> None:
        payload = valid_receipt()
        payload["capability"] = "telekinesis"
        errors = self.write_receipt("sample-cold-start.json", payload)
        self.assertTrue(
            any("is not a declared capability profile" in error for error in errors), errors
        )

    def test_not_tested_is_forbidden(self) -> None:
        payload = valid_receipt()
        payload["steps"][0]["note"] = "not_tested locally, only in CI"
        errors = self.write_receipt("sample-cold-start.json", payload)
        self.assertTrue(any("not_tested" in error for error in errors), errors)

    def test_malformed_pull_request_url_is_reported(self) -> None:
        payload = valid_receipt()
        payload["pull_request"] = "https://github.com/someone-else/other-repo/pull/1"
        errors = self.write_receipt("sample-cold-start.json", payload)
        self.assertTrue(
            any("pull_request must be an https URL" in error for error in errors), errors
        )

    def test_duplicate_step_names_is_reported(self) -> None:
        payload = valid_receipt()
        payload["steps"][1]["name"] = "clone"
        errors = self.write_receipt("sample-cold-start.json", payload)
        self.assertTrue(any("duplicate step name" in error for error in errors), errors)

    def test_stem_and_id_mismatch_is_reported(self) -> None:
        payload = valid_receipt()
        errors = self.write_receipt("different-file-name.json", payload)
        self.assertTrue(
            any("must equal the file name" in error for error in errors), errors
        )

    def test_unreachable_commit_is_reported(self) -> None:
        payload = valid_receipt()
        payload["started_from_commit"] = BAD_COMMIT
        errors = self.write_receipt("sample-cold-start.json", payload)
        self.assertTrue(
            any("started_from_commit must be reachable" in error for error in errors), errors
        )

    def test_hints_exist_for_each_error_shape(self) -> None:
        samples = [
            "cold start receipt sample-cold-start: JSON Schema violation at verdict: 'verdict' is a required property",
            "cold start receipt sample-cold-start: id 'other-id' must equal the file name sample-cold-start",
            "cold start receipt sample-cold-start: contributor must not be the repository owner",
            "cold start receipt sample-cold-start: capability 'telekinesis' is not a declared capability profile",
            "cold start receipt sample-cold-start: started_from_commit must be reachable from a fetched public origin ref",
            "cold start receipt sample-cold-start: pull_request must be an https URL under https://github.com/Lingkyn/xr-foundry/pull/",
            "cold start receipt sample-cold-start: duplicate step name(s): ['clone']",
            "cold start receipt sample-cold-start: must not contain the string 'not_tested'",
        ]
        generic = (
            "No specific hint is recorded for this rule yet; search "
            "scripts/validate_repository.py for the message text to find the check, "
            "and read docs/contributing/start-here.md."
        )
        for item in MODULE.explain_errors(samples):
            self.assertTrue(item.get("hint"), item)
            self.assertNotEqual(item["hint"], generic, item)

    def test_template_validates_once_placeholders_are_replaced(self) -> None:
        template = json.loads((ROOT / TEMPLATE).read_text(encoding="utf-8"))
        template["id"] = "sample-cold-start"
        template["contributor"] = "octocat"
        template["tool"] = "none"
        template["started_from_commit"] = GOOD_COMMIT
        template["pull_request"] = "https://github.com/Lingkyn/xr-foundry/pull/123"
        template["recorded_at"] = "2026-09-17T12:00:00Z"
        for step in template["steps"]:
            step["note"] = "Filled in."
        errors = MODULE.validate_json_schema_instance(
            template, self.tmp / SCHEMA, "cold start receipt template"
        )
        self.assertEqual(errors, [])

    def test_real_repository_returns_no_errors(self) -> None:
        self.assertEqual(MODULE.validate_cold_start_receipts(ROOT), [])


if __name__ == "__main__":
    unittest.main()
