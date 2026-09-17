from __future__ import annotations

import copy
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "scripts" / "validate_repository.py"
SPEC = importlib.util.spec_from_file_location("validate_repository_coverage_claims", SCRIPT)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)

MAP_PATH = "docs/standards/demo/coverage-map.json"
PACKAGE_ROOT = "packages/unity/systems/demo/com.lingkyn.demo.core"
ASSEMBLY = "Lingkyn.Demo.Core.Editor.Tests"
TEST_SOURCE = f"""using NUnit.Framework;
using UnityEngine.TestTools;

namespace Lingkyn.Demo.Core.Tests
{{
    public sealed class DemoContractTests
    {{
        [Test]
        public void AddRejectsOverflow()
        {{
        }}

        [Test]
        public void SnapshotIsImmutable()
        {{
        }}

        [UnityTest]
        public System.Collections.IEnumerator PrefabLoadsWithoutMissingScripts()
        {{
            yield return null;
        }}

        [TestCase(1)]
        [TestCase(2)]
        public void StackLimitIsEnforced(int limit)
        {{
        }}

        public void HelperThatIsNotATest()
        {{
        }}
    }}
}}
"""


def write(repo: Path, path: str, content: str) -> None:
    target = repo / path
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(content, encoding="utf-8")


def write_json(repo: Path, path: str, payload: dict) -> None:
    write(repo, path, json.dumps(payload, indent=2) + "\n")


def passing_map() -> dict:
    return {
        "schema": "xr-foundry.verification_coverage_map.v1",
        "version": "0.1.0",
        "family": "demo",
        "contract": "docs/standards/demo/verification-contract.md",
        "gates": [
            {
                "id": "core",
                "package_id": "com.lingkyn.demo.core",
                "test_assembly": ASSEMBLY,
                "clauses": [
                    {"id": "D-01", "clause": "overflow", "coverage": "covered", "tests": ["AddRejectsOverflow"], "missing": []},
                    {
                        "id": "D-02",
                        "clause": "snapshot immutability",
                        "coverage": "covered",
                        "tests": ["DemoContractTests.SnapshotIsImmutable", "validator:validate_coverage_map_claims"],
                        "missing": [],
                    },
                    {
                        "id": "D-03",
                        "clause": "prefab integrity",
                        "coverage": "partial",
                        "tests": ["PrefabLoadsWithoutMissingScripts"],
                        "missing": ["a Device Lab receipt"],
                    },
                    {"id": "D-04", "clause": "a headset check", "coverage": "unmapped", "tests": [], "missing": ["a test"]},
                ],
                "additional_tests_outside_clauses": ["StackLimitIsEnforced"],
            },
            {
                "id": "device",
                "package_id": "com.lingkyn.demo.core",
                "test_assembly": None,
                "clauses": [
                    {"id": "D-DEV-01", "clause": "comfort", "coverage": "partial", "tests": [], "missing": ["a receipt"]},
                ],
                "additional_tests_outside_clauses": [],
            },
        ],
        "summary": {"clauses": 5, "covered": 2, "partial": 2, "unmapped": 1, "open_gap_tests": 1, "open_evidence_gaps": 2},
    }


def make_repo(directory: str, coverage_map: dict | None = None) -> Path:
    repo = Path(directory)
    write_json(repo, f"{PACKAGE_ROOT}/package.json", {"name": "com.lingkyn.demo.core", "version": "0.1.0"})
    write_json(repo, f"{PACKAGE_ROOT}/Tests/Editor/{ASSEMBLY}.asmdef", {"name": ASSEMBLY})
    write(repo, f"{PACKAGE_ROOT}/Tests/Editor/DemoContractTests.cs", TEST_SOURCE)
    write(repo, "docs/standards/demo/verification-contract.md", "# Demo\n")
    write(repo, "scripts/validate_repository.py", "def validate_coverage_map_claims(root):\n    return []\n")
    write(repo, "tests/test_demo.py", "class TestDemo:\n    def test_demo_passes(self):\n        pass\n")
    write_json(repo, MAP_PATH, coverage_map if coverage_map is not None else passing_map())
    return repo


def run_rule(coverage_map: dict | None = None, mutate=None) -> list[str]:
    with tempfile.TemporaryDirectory() as directory:
        repo = make_repo(directory, coverage_map)
        if mutate is not None:
            mutate(repo)
        return MODULE.validate_coverage_map_claims(repo)


def mutated_map(change) -> dict:
    payload = passing_map()
    change(payload)
    return payload


class CoverageMapClaimsTests(unittest.TestCase):
    def assert_single_error(self, errors: list[str], *fragments: str) -> None:
        self.assertEqual(1, len(errors), errors)
        for fragment in fragments:
            self.assertIn(fragment, errors[0])

    def test_passing_map_produces_no_errors(self) -> None:
        self.assertEqual([], run_rule())

    def test_python_test_reference_forms_resolve(self) -> None:
        def change(payload: dict) -> None:
            payload["gates"][0]["clauses"][0]["tests"].extend(
                ["tests/test_demo.py::TestDemo::test_demo_passes", "tests/test_demo.py::test_demo_passes"]
            )

        self.assertEqual([], run_rule(mutated_map(change)))

        def broken(payload: dict) -> None:
            payload["gates"][0]["clauses"][0]["tests"].append("tests/test_demo.py::TestDemo::test_absent")

        self.assert_single_error(run_rule(mutated_map(broken)), MAP_PATH, "gate core", "clause D-01", "test_absent", "no def test_absent")

    def test_unknown_assembly_is_reported(self) -> None:
        def change(payload: dict) -> None:
            payload["gates"][0]["test_assembly"] = "Lingkyn.Demo.Missing.Tests"

        errors = run_rule(mutated_map(change))
        self.assertTrue(any("test_assembly must name exactly one asmdef" in error and "matches 0" in error for error in errors), errors)
        self.assertTrue(all(MAP_PATH in error for error in errors), errors)

    def test_package_id_must_own_the_assembly(self) -> None:
        def change(payload: dict) -> None:
            payload["gates"][0]["package_id"] = "com.lingkyn.other"

        self.assert_single_error(run_rule(mutated_map(change)), "gate core", "package_id does not match the package that owns", "'com.lingkyn.other'", "'com.lingkyn.demo.core'")

    def test_staging_manifest_owns_assembly(self) -> None:
        def relocate(repo: Path) -> None:
            (repo / PACKAGE_ROOT / "package.json").unlink()
            write_json(repo, f"{PACKAGE_ROOT}/package.staging.json", {"name": "com.lingkyn.demo.core"})

        self.assertEqual([], run_rule(mutate=relocate))

    def test_unknown_test_name_is_reported(self) -> None:
        def change(payload: dict) -> None:
            payload["gates"][0]["clauses"][0]["tests"] = ["AddRejectsOverflowRenamed"]

        errors = run_rule(mutated_map(change))
        self.assertEqual(2, len(errors), errors)
        for fragment in (MAP_PATH, "gate core", "clause D-01", "'AddRejectsOverflowRenamed'", "does not resolve"):
            self.assertIn(fragment, errors[0])
        # The real method that the stale name replaced is now an unlisted claim gap too.
        self.assertIn("test method is not listed in any clause or additional_tests_outside_clauses: AddRejectsOverflow (", errors[1])

    def test_unknown_additional_test_is_reported(self) -> None:
        def change(payload: dict) -> None:
            payload["gates"][0]["additional_tests_outside_clauses"].append("HelperThatIsNotATest")

        self.assert_single_error(run_rule(mutated_map(change)), "clause additional_tests_outside_clauses", "'HelperThatIsNotATest'")

    def test_qualified_entry_must_match_class_or_file(self) -> None:
        def change(payload: dict) -> None:
            payload["gates"][0]["clauses"][0]["tests"] = ["OtherFixture.AddRejectsOverflow"]

        self.assert_single_error(run_rule(mutated_map(change)), "'OtherFixture.AddRejectsOverflow'", "not in a class or file named OtherFixture")

    def test_covered_without_tests_is_reported(self) -> None:
        def change(payload: dict) -> None:
            payload["gates"][0]["clauses"][0]["tests"] = []

        errors = run_rule(mutated_map(change))
        self.assertTrue(any("clause D-01 coverage state covered requires a non-empty tests list" in error for error in errors), errors)

    def test_covered_with_missing_is_reported(self) -> None:
        def change(payload: dict) -> None:
            payload["gates"][0]["clauses"][0]["missing"] = ["something"]

        self.assert_single_error(run_rule(mutated_map(change)), "clause D-01 coverage state covered forbids a missing list")

    def test_partial_without_missing_is_reported(self) -> None:
        def change(payload: dict) -> None:
            payload["gates"][0]["clauses"][2]["missing"] = []

        self.assert_single_error(run_rule(mutated_map(change)), "clause D-03 coverage state partial requires a non-empty missing list")

    def test_unmapped_with_tests_is_reported(self) -> None:
        def change(payload: dict) -> None:
            payload["gates"][0]["clauses"][3]["tests"] = ["SnapshotIsImmutable"]

        self.assert_single_error(run_rule(mutated_map(change)), "clause D-04 coverage state unmapped forbids a tests list")

    def test_summary_mismatch_is_reported(self) -> None:
        def change(payload: dict) -> None:
            payload["summary"]["covered"] = 3
            payload["summary"]["open_gap_tests"] = 0

        errors = run_rule(mutated_map(change))
        self.assertEqual(2, len(errors), errors)
        self.assertIn("summary.covered does not match the recounted clauses: declared 3, recounted 2", errors[0])
        self.assertIn("summary.open_gap_tests + open_evidence_gaps must equal the recounted partial + unmapped clauses: declared 0 + 2, recounted 3", errors[1])

    def test_duplicate_clause_id_is_reported_across_gates(self) -> None:
        def change(payload: dict) -> None:
            payload["gates"][1]["clauses"][0]["id"] = "D-01"

        self.assert_single_error(run_rule(mutated_map(change)), "gate device duplicate clause id: D-01")

    def test_unlisted_test_method_is_reported(self) -> None:
        def change(payload: dict) -> None:
            payload["gates"][0]["additional_tests_outside_clauses"] = []

        self.assert_single_error(run_rule(mutated_map(change)), "gate core test method is not listed in any clause or additional_tests_outside_clauses: StackLimitIsEnforced (", "DemoContractTests.cs")

    def test_missing_validator_function_is_reported(self) -> None:
        def change(payload: dict) -> None:
            payload["gates"][0]["clauses"][1]["tests"][1] = "validator:validate_nothing"

        self.assert_single_error(run_rule(mutated_map(change)), "clause D-02", "'validator:validate_nothing'", "no function named validate_nothing is defined in scripts/validate_repository.py")

    def test_null_assembly_cannot_carry_csharp_tests(self) -> None:
        def change(payload: dict) -> None:
            payload["gates"][1]["clauses"][0]["tests"] = ["SnapshotIsImmutable"]
            payload["gates"][1]["clauses"][0]["coverage"] = "covered"
            payload["gates"][1]["clauses"][0]["missing"] = []
            payload["summary"].update({"covered": 3, "partial": 1, "open_evidence_gaps": 1})

        self.assert_single_error(run_rule(mutated_map(change)), "gate device lists C# tests but test_assembly is null")

    def test_flat_core_map_shape_is_checked(self) -> None:
        flat = {
            "schema": "xr-foundry.verification_coverage_map.v1",
            "package_id": "com.lingkyn.demo.core",
            "test_assembly": ASSEMBLY,
            "test_sources": [f"{PACKAGE_ROOT}/Tests/Editor/DemoContractTests.cs"],
            "clauses": [
                {"id": "C-01", "clause": "all", "coverage": "covered", "tests": ["AddRejectsOverflow", "SnapshotIsImmutable", "PrefabLoadsWithoutMissingScripts"], "missing": []},
            ],
            "additional_tests_outside_clauses": ["StackLimitIsEnforced"],
            "summary": {"clauses": 1, "covered": 1, "partial": 0, "unmapped": 0, "open_gap_tests": 0},
        }
        with tempfile.TemporaryDirectory() as directory:
            repo = make_repo(directory)
            write_json(repo, "docs/standards/demo/coverage-map.core.json", flat)
            self.assertEqual([], MODULE.validate_coverage_map_claims(repo))
            flat["test_sources"] = ["packages/nowhere.cs"]
            write_json(repo, "docs/standards/demo/coverage-map.core.json", flat)
            errors = MODULE.validate_coverage_map_claims(repo)
        self.assert_single_error(errors, "coverage-map.core.json", "test_sources path does not exist: packages/nowhere.cs")

    def test_stale_allowlist_entry_is_reported(self) -> None:
        original = MODULE.COVERAGE_MAP_UNVERIFIED_CLAIMS
        MODULE.COVERAGE_MAP_UNVERIFIED_CLAIMS = frozenset({(MAP_PATH, "gate core duplicate clause id: D-99")})
        try:
            errors = run_rule()
        finally:
            MODULE.COVERAGE_MAP_UNVERIFIED_CLAIMS = original
        self.assert_single_error(errors, "stale coverage-map allowlist entry no longer produced", "D-99")

    def test_explain_errors_gives_a_specific_hint_for_every_new_error_shape(self) -> None:
        samples = [
            f"coverage map {MAP_PATH}: gate core test_assembly must name exactly one asmdef under packages/ or staging/: 'X' matches 0",
            f"coverage map {MAP_PATH}: gate core package_id does not match the package that owns X: declared 'a', owner 'b'",
            f"coverage map {MAP_PATH}: gate core clause D-01 test entry does not resolve: 'Nope': no [Test]/[TestCase]/[UnityTest] method with that name under the gate's assembly folder",
            f"coverage map {MAP_PATH}: gate core clause D-01 coverage state covered requires a non-empty tests list",
            f"coverage map {MAP_PATH}: gate core clause D-03 coverage state partial requires a non-empty missing list",
            f"coverage map {MAP_PATH}: gate core clause D-04 coverage state unmapped forbids a tests list",
            f"coverage map {MAP_PATH}: summary.covered does not match the recounted clauses: declared 3, recounted 2",
            f"coverage map {MAP_PATH}: summary.open_gap_tests + open_evidence_gaps must equal the recounted partial + unmapped clauses: declared 0 + 2, recounted 3",
            f"coverage map {MAP_PATH}: gate device duplicate clause id: D-01",
            f"coverage map {MAP_PATH}: gate core test method is not listed in any clause or additional_tests_outside_clauses: StackLimitIsEnforced (a.cs)",
            f"coverage map {MAP_PATH}: gate device lists C# tests but test_assembly is null",
            f"coverage map {MAP_PATH}: stale coverage-map allowlist entry no longer produced; remove it from COVERAGE_MAP_UNVERIFIED_CLAIMS: x",
            f"coverage map {MAP_PATH}: schema must be xr-foundry.verification_coverage_map.v1",
            f"coverage map {MAP_PATH}: test_sources path does not exist: packages/nowhere.cs",
        ]
        fallback = MODULE.explain_errors(["something nobody anticipated"])[0]["hint"]
        for item in MODULE.explain_errors(samples):
            self.assertNotEqual(fallback, item["hint"], item["error"])
            self.assertTrue(item["hint"].strip(), item["error"])

    def test_real_repository_maps_pass_or_are_exactly_allowlisted(self) -> None:
        self.assertEqual([], MODULE.validate_coverage_map_claims(ROOT))
        for map_path, message in MODULE.COVERAGE_MAP_UNVERIFIED_CLAIMS:
            self.assertTrue((ROOT / map_path).is_file(), map_path)
            self.assertTrue(message.strip(), map_path)
        # Every allowlisted pair is still produced by the raw rule, otherwise the
        # stale-entry check above would have fired; assert the raw rule reports
        # exactly the allowlisted pairs and nothing else.
        original = MODULE.COVERAGE_MAP_UNVERIFIED_CLAIMS
        MODULE.COVERAGE_MAP_UNVERIFIED_CLAIMS = frozenset()
        try:
            raw = MODULE.validate_coverage_map_claims(ROOT)
        finally:
            MODULE.COVERAGE_MAP_UNVERIFIED_CLAIMS = original
        expected = sorted(f"coverage map {map_path}: {message}" for map_path, message in original)
        self.assertEqual(expected, sorted(raw))


if __name__ == "__main__":
    unittest.main()
