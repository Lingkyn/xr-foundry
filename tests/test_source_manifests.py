from __future__ import annotations

import copy
import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
VALIDATOR = ROOT / "scripts" / "validate_repository.py"

SPEC = importlib.util.spec_from_file_location("validate_repository_source_manifests", VALIDATOR)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE  # dataclasses in the target module need this
SPEC.loader.exec_module(MODULE)

FAMILY = "widgets"


def write(repo: Path, path: str, content: str) -> None:
    target = repo / path
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(content, encoding="utf-8")


def write_json(repo: Path, path: str, payload: dict) -> None:
    write(repo, path, json.dumps(payload, indent=2) + "\n")


def base_source(source_id: str = "widgets-official-docs") -> dict:
    return {
        "id": source_id,
        "kind": "official_engine_documentation",
        "authority": "Example Standards Body",
        "url": "https://example.test/widgets/manual",
        "license_or_terms": "Example documentation terms; architecture facts only",
        "maintenance_evidence": "Current example manual reviewed 2026-01-01; not fetched from this environment",
        "admitted_role": "Adapter boundary example for the Widgets family",
        "admitted_claims": ["Widgets are identified separately from their presentation"],
        "excluded_uses": ["No engine type is copied into the Core"],
    }


def base_manifest(sources: list[dict] | None = None) -> dict:
    return {
        "schema": f"xr-foundry.{FAMILY}_source_manifest.v1",
        "scope": "Test-only Widgets family scope",
        "derivation_policy": "positive_external_public_sources_only",
        "consumer_material_allowed": False,
        "review_note": (
            "The URLs below were not fetched from the authoring environment; "
            "the admitted claims must be confirmed by a person before signing."
        ),
        "sources": sources if sources is not None else [base_source()],
    }


def make_repo(
    directory: str,
    *,
    family: str = FAMILY,
    manifest: dict | None = None,
    include_manifest: bool = True,
    include_contract: bool = True,
) -> Path:
    repo = Path(directory)
    if include_manifest:
        write_json(repo, f"docs/standards/{family}/source-manifest.json", manifest if manifest is not None else base_manifest())
    if include_contract:
        write(repo, f"docs/standards/{family}/verification-contract.md", f"# {family.title()}\n")
    return repo


def run_rule(**kwargs) -> list[str]:
    with tempfile.TemporaryDirectory() as directory:
        repo = make_repo(directory, **kwargs)
        return MODULE.validate_family_source_manifests(repo)


def assert_single_error(case: unittest.TestCase, errors: list[str], *fragments: str) -> None:
    case.assertEqual(1, len(errors), errors)
    for fragment in fragments:
        case.assertIn(fragment, errors[0])


class FamilySourceManifestTests(unittest.TestCase):
    def test_valid_manifest_passes(self) -> None:
        self.assertEqual([], run_rule())

    def test_wrong_schema_is_reported(self) -> None:
        manifest = base_manifest()
        manifest["schema"] = "not-a-valid-schema-string"
        errors = run_rule(manifest=manifest)
        assert_single_error(self, errors, "schema must match xr-foundry")

    def test_family_mismatch_is_reported(self) -> None:
        manifest = base_manifest()
        manifest["schema"] = "xr-foundry.gizmos_source_manifest.v1"
        errors = run_rule(manifest=manifest)
        assert_single_error(self, errors, "schema family segment", "does not match directory")

    def test_duplicate_source_id_is_reported(self) -> None:
        manifest = base_manifest(sources=[base_source("dup-id"), base_source("dup-id")])
        errors = run_rule(manifest=manifest)
        self.assertTrue(any("source id is missing or duplicated: dup-id" in e for e in errors), errors)

    def test_non_https_url_is_reported(self) -> None:
        manifest = base_manifest()
        manifest["sources"][0]["url"] = "http://example.test/insecure"
        errors = run_rule(manifest=manifest)
        assert_single_error(self, errors, "source must use a public HTTPS URL")

    def test_empty_admitted_claims_is_reported(self) -> None:
        manifest = base_manifest()
        manifest["sources"][0]["admitted_claims"] = []
        errors = run_rule(manifest=manifest)
        assert_single_error(self, errors, "source must state non-empty admitted_claims")

    def test_missing_field_is_reported(self) -> None:
        manifest = base_manifest()
        del manifest["sources"][0]["license_or_terms"]
        errors = run_rule(manifest=manifest)
        assert_single_error(self, errors, "source must state non-empty license_or_terms")

    def test_missing_review_note_is_reported(self) -> None:
        manifest = base_manifest()
        del manifest["review_note"]
        errors = run_rule(manifest=manifest)
        assert_single_error(self, errors, "must record that source URLs were not fetched")

    def test_per_source_review_note_satisfies_the_rule(self) -> None:
        manifest = base_manifest()
        del manifest["review_note"]
        manifest["sources"][0]["review_note"] = "Not fetched from this environment; confirm before signing."
        self.assertEqual([], run_rule(manifest=manifest))

    def test_non_public_marker_is_reported(self) -> None:
        manifest = base_manifest()
        marker = MODULE.forbidden_public_markers()[0]
        manifest["sources"][0]["admitted_role"] = f"Mentions {marker} in passing"
        errors = run_rule(manifest=manifest)
        assert_single_error(self, errors, "source names a non-public marker")

    def test_contract_without_manifest_is_reported(self) -> None:
        errors = run_rule(include_manifest=False, include_contract=True)
        assert_single_error(
            self,
            errors,
            f"docs/standards/{FAMILY}/verification-contract.md",
            "source manifest is missing for a family with a verification contract",
        )

    def test_manifest_without_contract_is_reported(self) -> None:
        errors = run_rule(include_contract=False)
        assert_single_error(
            self,
            errors,
            f"docs/standards/{FAMILY}/source-manifest.json",
            "verification contract is missing for a family with a source manifest",
        )

    def test_inventory_family_is_skipped_by_this_rule(self) -> None:
        # Inventory keeps validate_inventory_source_manifest's own stricter shape;
        # this rule must not also apply the generic checks to it.
        manifest = {"schema": "xr-foundry.inventory_source_manifest.v1"}
        errors = run_rule(family="inventory", manifest=manifest)
        self.assertEqual([], errors)

    def test_explain_errors_gives_a_specific_hint_for_every_new_error_shape(self) -> None:
        path = f"docs/standards/{FAMILY}/source-manifest.json"
        contract_path = f"docs/standards/{FAMILY}/verification-contract.md"
        samples = [
            f"family source manifest {path}: schema must match xr-foundry.<family>_source_manifest.v<N>: 'nope'",
            f"family source manifest {path}: schema family segment 'gizmos' does not match directory 'widgets'",
            f"family source manifest {path}: must contain admitted sources",
            f"family source manifest {path}: source entries must be objects",
            f"family source manifest {path}: source id is missing or duplicated: dup-id",
            f"family source manifest {path}: source must use a public HTTPS URL: widgets-official-docs",
            f"family source manifest {path}: source must state non-empty admitted_claims: widgets-official-docs",
            f"family source manifest {path}: source names a non-public marker: widgets-official-docs",
            f"family source manifest {contract_path}: source manifest is missing for a family with a verification contract: widgets",
            f"family source manifest {path}: verification contract is missing for a family with a source manifest: widgets",
            f"family source manifest {path}: must record that source URLs were not fetched: add a non-empty review_note or a per-source note",
            f"family source manifest {path}: stale family-source-manifest allowlist entry no longer produced; remove it from FAMILY_SOURCE_MANIFEST_UNVERIFIED_CLAIMS: x",
        ]
        fallback = MODULE.explain_errors(["something nobody anticipated"])[0]["hint"]
        for item in MODULE.explain_errors(samples):
            self.assertNotEqual(fallback, item["hint"], item["error"])
            self.assertTrue(item["hint"].strip(), item["error"])

    def test_real_repository_returns_no_errors_and_the_allowlist_is_empty(self) -> None:
        # Every docs/standards/<family>/ now holds a source manifest and a
        # verification contract together, so no suppressed message remains and the
        # allowlist itself must be empty rather than merely unproduced.
        self.assertEqual(frozenset(), MODULE.FAMILY_SOURCE_MANIFEST_UNVERIFIED_CLAIMS)
        self.assertEqual([], MODULE.validate_family_source_manifests(ROOT))


if __name__ == "__main__":
    unittest.main()
