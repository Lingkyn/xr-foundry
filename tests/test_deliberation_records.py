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
SPEC = importlib.util.spec_from_file_location("validate_repository_deliberation_records", SCRIPT)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)

SCHEMA_PATH = "docs/contributing/deliberation-record.schema.json"
MODEL_PATH = "docs/governance/governance-model.v1.json"
MANDATE_PATH = "docs/governance/mandates/weekly-steward.mandate.json"
MANDATE_ID = "mandate.weekly-steward.v1"
RECORDS_DIR = "docs/governance/deliberations"
RECORD_FILE = f"{RECORDS_DIR}/DLB-0100-example-rule.json"
RECORD_ID = "DLB-0100-EXAMPLE-RULE"
BLOB = "https://github.com/Lingkyn/xr-foundry/blob/main/"
DEFAULT_HINT_FRAGMENT = "No specific hint is recorded for this rule yet"


def write(repo: Path, path: str, content: str) -> None:
    target = repo / path
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(content, encoding="utf-8")


def write_json(repo: Path, path: str, payload: dict) -> None:
    write(repo, path, json.dumps(payload, indent=2) + "\n")


def resolved_record() -> dict:
    """A governance-policy record resolved by lazy consensus under the steward mandate."""

    return {
        "schema": "xr-foundry.deliberation.v1",
        "version": "0.1.0",
        "id": RECORD_ID,
        "title": "Example rule under test",
        "status": "resolved",
        "decision_class": "governance_policy",
        "governance_stage": "G0",
        "review_opened_at": "2026-09-15T09:00:00Z",
        "review_not_before": "2026-09-22T09:00:00Z",
        "objective": "Exercise the live deliberation record rule.",
        "current_question": "Does the rule re-derive every claim the record makes?",
        "assumptions": [
            {
                "statement": "The governance model defines the review windows.",
                "evidence": [f"{BLOB}{MODEL_PATH}"],
            }
        ],
        "options": [
            {
                "id": "OPT-KEEP",
                "summary": "Keep the rule.",
                "trade_offs": ["Adds one validator pass"],
                "evidence": [f"{BLOB}GOVERNANCE.md"],
            }
        ],
        "deltas": [],
        "synthesis": {
            "agreements": ["The record is re-derivable."],
            "disagreements": [],
            "missing_evidence": [],
            "updated_at": "2026-09-22T09:30:00Z",
        },
        "decision": {
            "option_id": "OPT-KEEP",
            "summary": "Keep the rule by lazy consensus.",
            "rationale": ["The window closed with no objection delta."],
            "decided_by": f"process:{MANDATE_ID}",
            "decided_at": "2026-09-22T09:30:00Z",
        },
        "execution": {
            "readiness": "ready",
            "task": "https://github.com/Lingkyn/xr-foundry/issues/1",
            "scope": ["Keep the validator rule."],
            "non_goals": ["No permission change."],
            "exact_next_action": "Nothing; the rule is in force.",
        },
        "reopen_conditions": ["The rule blocks a valid record."],
        "authority": {
            "grants_repository_permission": False,
            "grants_execution_authority": False,
            "grants_review_authority": False,
            "grants_merge_authority": False,
            "agent_or_model_ranking": False,
            "private_chain_of_thought_required": False,
        },
    }


def open_record() -> dict:
    payload = resolved_record()
    payload["status"] = "open"
    payload["decision"] = None
    payload["execution"] = {
        "readiness": "not_ready",
        "task": None,
        "scope": [],
        "non_goals": ["No permission change."],
        "exact_next_action": "Wait for the window to close.",
    }
    return payload


def objection_delta() -> dict:
    return {
        "id": "DELTA-OBJECTION",
        "kind": "risk",
        "github_identity": "@someone",
        "summary": "The rule may block a valid record.",
        "evidence": [],
        "created_at": "2026-09-16T09:00:00Z",
    }


def make_repo(directory: str, record: dict | None = None, mandate_change=None) -> Path:
    """Copy the schema, governance model, and steward mandate; write one record."""

    repo = Path(directory)
    for relative in (SCHEMA_PATH, MODEL_PATH, MANDATE_PATH):
        target = repo / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(ROOT / relative, target)
    write(repo, "GOVERNANCE.md", "# Governance\n")
    if mandate_change is not None:
        mandate = json.loads((repo / MANDATE_PATH).read_text(encoding="utf-8"))
        mandate_change(mandate)
        write_json(repo, MANDATE_PATH, mandate)
    write_json(repo, RECORD_FILE, record if record is not None else resolved_record())
    return repo


def run_rule(record: dict | None = None, mutate=None, mandate_change=None) -> list[str]:
    with tempfile.TemporaryDirectory() as directory:
        repo = make_repo(directory, record, mandate_change)
        if mutate is not None:
            mutate(repo)
        return MODULE.validate_live_deliberation_records(repo)


def mutated_record(change, base=resolved_record) -> dict:
    payload = base()
    change(payload)
    return payload


class LiveDeliberationRecordTests(unittest.TestCase):
    def assert_single_error(self, errors: list[str], *fragments: str) -> None:
        self.assertEqual(1, len(errors), errors)
        for fragment in fragments:
            self.assertIn(fragment, errors[0])

    def test_live_records_pass(self) -> None:
        live = sorted((ROOT / RECORDS_DIR).glob("*.json"))
        self.assertGreaterEqual(len(live), 3, live)
        self.assertEqual([], MODULE.validate_live_deliberation_records(ROOT))

    def test_registered_in_repository_validation_after_operating_mandates(self) -> None:
        source = SCRIPT.read_text(encoding="utf-8")
        body = source[source.index("def validate_repository(root: Path)") :]
        mandates = body.index("errors.extend(validate_operating_mandates(root))")
        records = body.index("errors.extend(validate_live_deliberation_records(root))")
        task_hall = body.index("errors.extend(validate_task_hall_contract(root))")
        self.assertTrue(mandates < records < task_hall)

    def test_resolved_and_open_fixtures_pass(self) -> None:
        self.assertEqual([], run_rule(resolved_record()))
        self.assertEqual([], run_rule(open_record()))

    def test_person_decided_record_passes(self) -> None:
        def change(payload: dict) -> None:
            payload["decision"]["decided_by"] = "@Lingkyn"
            payload["deltas"] = [objection_delta()]

        self.assertEqual([], run_rule(mutated_record(change)))

    def test_open_record_past_its_window_is_not_a_defect(self) -> None:
        # The window closed long ago relative to now; an open record is a pending
        # process step and the validator has no warnings channel, so nothing is reported.
        def change(payload: dict) -> None:
            payload["review_opened_at"] = "2020-01-01T00:00:00Z"
            payload["review_not_before"] = "2020-01-08T00:00:00Z"

        self.assertEqual([], run_rule(mutated_record(change, open_record)))

    def test_schema_violation_is_reported_and_stops_further_checks(self) -> None:
        def change(payload: dict) -> None:
            payload["decision"]["decided_by"] = "not-an-identity"
            payload["id"] = "DLB-WRONG"

        errors = run_rule(mutated_record(change))
        self.assertTrue(errors)
        self.assertTrue(all("JSON Schema violation" in error for error in errors), errors)
        self.assertTrue(any("decision.decided_by" in error for error in errors), errors)
        self.assertTrue(all(RECORD_FILE in error for error in errors), errors)

    def test_invalid_json_is_reported(self) -> None:
        def corrupt(repo: Path) -> None:
            write(repo, RECORD_FILE, "{not json")

        self.assert_single_error(run_rule(mutate=corrupt), RECORD_FILE, "invalid JSON")

    def test_id_must_equal_uppercased_filename_stem(self) -> None:
        def change(payload: dict) -> None:
            payload["id"] = "DLB-0100-OTHER-RULE"

        self.assert_single_error(
            run_rule(mutated_record(change)),
            RECORD_FILE,
            "id 'DLB-0100-OTHER-RULE' must equal the uppercased filename stem 'DLB-0100-EXAMPLE-RULE'",
        )

    def test_duplicate_id_is_reported(self) -> None:
        def duplicate(repo: Path) -> None:
            payload = resolved_record()
            payload["id"] = RECORD_ID
            write_json(repo, f"{RECORDS_DIR}/DLB-0101-second.json", payload)

        errors = run_rule(mutate=duplicate)
        self.assertEqual(2, len(errors), errors)
        self.assertIn("must equal the uppercased filename stem 'DLB-0101-SECOND'", errors[0])
        self.assertIn(f"duplicate deliberation id {RECORD_ID} (also recorded by {RECORD_FILE})", errors[1])
        self.assertIn("DLB-0101-second.json", errors[1])

    def test_window_shorter_than_class_minimum(self) -> None:
        def policy(payload: dict) -> None:
            payload["review_not_before"] = "2026-09-21T09:00:00Z"
            payload["decision"]["decided_at"] = "2026-09-21T09:00:00Z"

        self.assert_single_error(
            run_rule(mutated_record(policy)),
            RECORD_FILE,
            "governance_policy review_not_before must be at least 7 days after review_opened_at",
        )

        def constitutional(payload: dict) -> None:
            payload["decision_class"] = "constitutional_change"

        self.assert_single_error(
            run_rule(mutated_record(constitutional)),
            "constitutional_change review_not_before must be at least 14 days",
        )

    def test_model_minimum_is_authoritative_when_the_constant_lags(self) -> None:
        # A stricter window in the governance model is enforced from the model itself.
        def stricter(repo: Path) -> None:
            model = json.loads((repo / MODEL_PATH).read_text(encoding="utf-8"))
            for item in model["decision_classes"]:
                if item["id"] == "governance_policy":
                    item["minimum_review_days"] = 10
            write_json(repo, MODEL_PATH, model)

        self.assert_single_error(
            run_rule(mutate=stricter),
            RECORD_FILE,
            "governance_policy review window is shorter than the governance model minimum of 10 days",
        )

    def test_missing_decision_class_is_a_finding_not_a_guess(self) -> None:
        def change(payload: dict) -> None:
            for field in ("decision_class", "governance_stage", "review_opened_at", "review_not_before"):
                payload.pop(field)

        self.assert_single_error(
            run_rule(mutated_record(change)),
            RECORD_FILE,
            "names no decision_class, so its review window cannot be derived",
        )

    def test_unknown_class_in_model_is_a_finding(self) -> None:
        def drop_class(repo: Path) -> None:
            model = json.loads((repo / MODEL_PATH).read_text(encoding="utf-8"))
            model["decision_classes"] = [
                item for item in model["decision_classes"] if item["id"] != "governance_policy"
            ]
            write_json(repo, MODEL_PATH, model)

        self.assert_single_error(
            run_rule(mutate=drop_class),
            "decision_class 'governance_policy' is not defined by the governance model",
        )

    def test_non_utc_timestamp_is_reported(self) -> None:
        def change(payload: dict) -> None:
            payload["review_not_before"] = "2026-09-22T09:00:00+01:00"

        self.assert_single_error(run_rule(mutated_record(change)), "review_not_before must use UTC")

    def test_decided_before_window_close(self) -> None:
        def change(payload: dict) -> None:
            payload["decision"]["decided_at"] = "2026-09-22T08:59:59Z"

        self.assert_single_error(
            run_rule(mutated_record(change)),
            RECORD_FILE,
            "resolved governance decision predates review_not_before",
        )

    def test_process_decided_by_missing_mandate(self) -> None:
        def change(payload: dict) -> None:
            payload["decision"]["decided_by"] = "process:mandate.nobody.v1"

        self.assert_single_error(
            run_rule(mutated_record(change)),
            "decided_by names mandate 'mandate.nobody.v1' but no docs/governance/mandates/*.mandate.json carries that mandate_id",
        )

    def test_process_decided_by_expired_mandate(self) -> None:
        def expire(mandate: dict) -> None:
            mandate["expires_at"] = "2026-09-22T09:30:00Z"

        self.assert_single_error(
            run_rule(mandate_change=expire),
            f"decided_by names mandate '{MANDATE_ID}' that had expired at decided_at (expires_at 2026-09-22T09:30:00Z)",
        )

    def test_process_decided_by_mandate_not_yet_in_force(self) -> None:
        def later(mandate: dict) -> None:
            mandate["not_before"] = "2026-10-01T00:00:00Z"

        self.assert_single_error(
            run_rule(mandate_change=later),
            f"decided_by names mandate '{MANDATE_ID}' that was not yet in force at decided_at",
        )

    def test_process_decided_by_revoked_mandate(self) -> None:
        def revoke(mandate: dict) -> None:
            mandate["revocation"]["status"] = "revoked"
            mandate["revocation"]["revoked_at"] = "2026-09-20T00:00:00Z"
            mandate["revocation"]["revoked_by"] = "github:user:Lingkyn"

        self.assert_single_error(
            run_rule(mandate_change=revoke),
            f"decided_by names mandate '{MANDATE_ID}' that was revoked at decided_at (revoked_at 2026-09-20T00:00:00Z)",
        )

        def revoke_without_time(mandate: dict) -> None:
            mandate["revocation"]["status"] = "revoked"

        self.assert_single_error(run_rule(mandate_change=revoke_without_time), "was revoked at decided_at")

        def revoke_after_decision(mandate: dict) -> None:
            revoke(mandate)
            mandate["revocation"]["revoked_at"] = "2026-09-23T00:00:00Z"

        self.assertEqual([], run_rule(mandate_change=revoke_after_decision))

    def test_lazy_consensus_with_objection_delta(self) -> None:
        def change(payload: dict) -> None:
            payload["deltas"] = [objection_delta()]

        self.assert_single_error(
            run_rule(mutated_record(change)),
            RECORD_FILE,
            f"lazy-consensus decision by process:{MANDATE_ID} is invalid while objection deltas exist: ['DELTA-OBJECTION']",
        )

        def counterexample(payload: dict) -> None:
            delta = objection_delta()
            delta["kind"] = "counterexample"
            payload["deltas"] = [delta]

        self.assert_single_error(run_rule(mutated_record(counterexample)), "['DELTA-OBJECTION']")

        def evidence_only(payload: dict) -> None:
            delta = objection_delta()
            delta["kind"] = "evidence"
            payload["deltas"] = [delta]

        self.assertEqual([], run_rule(mutated_record(evidence_only)))

    def test_missing_referenced_path(self) -> None:
        def change(payload: dict) -> None:
            payload["options"][0]["evidence"] = [f"{BLOB}docs/rfcs/9999-missing.md#anchor"]

        self.assert_single_error(
            run_rule(mutated_record(change)),
            RECORD_FILE,
            "referenced repository path does not exist: docs/rfcs/9999-missing.md",
        )

        def traversal(payload: dict) -> None:
            payload["assumptions"][0]["evidence"] = [f"{BLOB}../etc/passwd"]

        self.assert_single_error(run_rule(mutated_record(traversal)), "does not exist: ../etc/passwd")

        def external(payload: dict) -> None:
            payload["assumptions"][0]["evidence"] = ["https://docs.unity3d.com/Manual/index.html"]

        self.assertEqual([], run_rule(mutated_record(external)))

    def test_missing_directory_or_model_fail_appropriately(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            self.assertEqual([], MODULE.validate_live_deliberation_records(Path(directory)))

        def drop_model(repo: Path) -> None:
            (repo / MODEL_PATH).unlink()

        errors = run_rule(mutate=drop_model)
        # The fixture cites the model as evidence, so its absence is reported twice:
        # once as an underivable window and once as a missing referenced path.
        self.assertEqual(2, len(errors), errors)
        self.assertIn(
            "the governance model is missing or unreadable, so the governance_policy review window cannot be derived",
            errors[0],
        )
        self.assertIn(f"referenced repository path does not exist: {MODEL_PATH}", errors[1])

    def test_explain_errors_hints_every_new_error_shape(self) -> None:
        samples: list[list[str]] = []

        def corrupt(repo: Path) -> None:
            write(repo, RECORD_FILE, "{not json")

        samples.append(run_rule(mutate=corrupt))
        samples.append(run_rule(mutated_record(lambda p: p.__setitem__("id", "DLB-OTHER"))))
        samples.append(run_rule(mutated_record(lambda p: p["decision"].__setitem__("decided_by", "nobody"))))

        def duplicate(repo: Path) -> None:
            write_json(repo, f"{RECORDS_DIR}/DLB-0100-EXAMPLE-RULE.copy.json", resolved_record())

        samples.append(run_rule(mutate=duplicate))

        def drop_metadata(payload: dict) -> None:
            for field in ("decision_class", "governance_stage", "review_opened_at", "review_not_before"):
                payload.pop(field)

        samples.append(run_rule(mutated_record(drop_metadata)))
        samples.append(run_rule(mutated_record(lambda p: p.__setitem__("review_not_before", "2026-09-22T09:00:00+01:00"))))
        samples.append(run_rule(mutated_record(lambda p: p.__setitem__("decision_class", "constitutional_change"))))
        samples.append(run_rule(mutated_record(lambda p: p["decision"].__setitem__("decided_at", "2026-09-01T00:00:00Z"))))
        samples.append(run_rule(mutated_record(lambda p: p["decision"].__setitem__("decided_by", "process:mandate.nobody.v1"))))
        samples.append(run_rule(mandate_change=lambda m: m.__setitem__("expires_at", "2026-09-22T09:30:00Z")))
        samples.append(run_rule(mutated_record(lambda p: p.__setitem__("deltas", [objection_delta()]))))
        samples.append(run_rule(mutated_record(lambda p: p["options"][0].__setitem__("evidence", [f"{BLOB}nope.md"]))))

        def stricter(repo: Path) -> None:
            model = json.loads((repo / MODEL_PATH).read_text(encoding="utf-8"))
            model["decision_classes"][1]["minimum_review_days"] = 10
            write_json(repo, MODEL_PATH, model)

        samples.append(run_rule(mutate=stricter))

        for errors in samples:
            self.assertTrue(errors, "every sample must produce at least one error")
            for entry in MODULE.explain_errors(errors):
                self.assertNotIn(DEFAULT_HINT_FRAGMENT, entry["hint"], entry)
                self.assertTrue(entry["hint"].strip(), entry)

    def test_no_temporary_allowlist_is_needed_for_live_records(self) -> None:
        # The three live records pass the strict rule, so no
        # DELIBERATION_RECORD_UNVERIFIED_CLAIMS allowlist exists; if one appears it
        # must be a deliberate, labelled decision, not a silent suppression.
        self.assertFalse(hasattr(MODULE, "DELIBERATION_RECORD_UNVERIFIED_CLAIMS"))


if __name__ == "__main__":
    unittest.main()
