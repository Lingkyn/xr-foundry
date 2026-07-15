from __future__ import annotations

import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "scripts/validate_repository.py"
SPEC = importlib.util.spec_from_file_location("validate_repository", SCRIPT)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class RepositoryContractTests(unittest.TestCase):
    def test_current_repository_passes(self) -> None:
        self.assertEqual([], MODULE.validate_repository(ROOT))

    def test_current_inventory_projections_are_coherent(self) -> None:
        self.assertEqual([], MODULE.validate_inventory_projection_coherence(ROOT))

    def test_inventory_projection_rejects_stale_unadmitted_claim(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            standard_root = root / "docs" / "standards" / "inventory"
            standard_root.mkdir(parents=True)
            (root / "ROADMAP.md").write_text(
                "Implementation remains unadmitted until later.", encoding="utf-8"
            )
            (standard_root / "README.md").write_text("Core status", encoding="utf-8")
            (standard_root / "inventory-standard.json").write_text(
                json.dumps(
                    {
                        "core_implementation_admitted": True,
                        "package_family": [
                            {
                                "id": "com.lingkyn.inventory.core",
                                "implementation_status": "implemented_incubating",
                                "earliest_failed_gate": "persistence_round_trip_and_migration",
                            }
                        ],
                    }
                ),
                encoding="utf-8",
            )
            (root / "package-catalog.json").write_text(
                json.dumps(
                    {
                        "packages": [
                            {
                                "id": "com.lingkyn.inventory.core",
                                "maturity": "incubating",
                                "promotion": {
                                    "candidate_status": "blocked",
                                    "earliest_failed_gate": "persistence_round_trip_and_migration",
                                    "satisfied": ["architecture_gate"],
                                    "pending": ["persistence_round_trip_and_migration"],
                                },
                            }
                        ]
                    }
                ),
                encoding="utf-8",
            )
            (root / "reference-catalog.json").write_text(
                json.dumps(
                    {
                        "artifacts": [
                            {
                                "id": "unity-inventory-core",
                                "maturity": "incubating",
                            },
                            {
                                "id": "inventory-package-family-standard",
                                "maturity": "incubating",
                            },
                        ]
                    }
                ),
                encoding="utf-8",
            )

            errors = MODULE.validate_inventory_projection_coherence(root)
            self.assertTrue(any("stale Inventory implementation claim" in error for error in errors))

    def test_passed_candidate_rejects_pending_gate(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            standard_root = root / "docs" / "standards" / "inventory"
            standard_root.mkdir(parents=True)
            (root / "ROADMAP.md").write_text("Candidate Core", encoding="utf-8")
            (standard_root / "README.md").write_text("Candidate Core", encoding="utf-8")
            (standard_root / "inventory-standard.json").write_text(
                json.dumps(
                    {
                        "core_implementation_admitted": True,
                        "package_family": [
                            {
                                "id": "com.lingkyn.inventory.core",
                                "implementation_status": "implemented_candidate",
                                "earliest_failed_gate": "none",
                            }
                        ],
                    }
                ),
                encoding="utf-8",
            )
            (root / "package-catalog.json").write_text(
                json.dumps(
                    {
                        "packages": [
                            {
                                "id": "com.lingkyn.inventory.core",
                                "maturity": "candidate",
                                "promotion": {
                                    "candidate_status": "passed",
                                    "earliest_failed_gate": "none",
                                    "satisfied": ["candidate_gate"],
                                    "pending": ["should_not_remain"],
                                },
                            }
                        ]
                    }
                ),
                encoding="utf-8",
            )
            (root / "reference-catalog.json").write_text(
                json.dumps(
                    {
                        "artifacts": [
                            {"id": "unity-inventory-core", "maturity": "candidate"},
                            {"id": "inventory-package-family-standard", "maturity": "incubating"},
                        ]
                    }
                ),
                encoding="utf-8",
            )

            errors = MODULE.validate_inventory_projection_coherence(root)
            self.assertTrue(any("no failed or pending gate" in error for error in errors))

    def test_inventory_projection_rejects_stale_ugui_roadmap_row(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            standard_root = root / "docs" / "standards" / "inventory"
            standard_root.mkdir(parents=True)
            (root / "ROADMAP.md").write_text(
                "| `com.lingkyn.inventory.ugui` | `0.1.0` | `candidate` | `none` |\n",
                encoding="utf-8",
            )
            (standard_root / "README.md").write_text("UGUI correction", encoding="utf-8")
            (standard_root / "inventory-standard.json").write_text(
                json.dumps(
                    {
                        "package_family": [
                            {
                                "id": "com.lingkyn.inventory.ugui",
                                "implementation_status": "implemented_incubating",
                                "earliest_failed_gate": "immutable_git_url_functional_consumer",
                            }
                        ]
                    }
                ),
                encoding="utf-8",
            )
            (root / "package-catalog.json").write_text(
                json.dumps(
                    {
                        "packages": [
                            {
                                "id": "com.lingkyn.inventory.ugui",
                                "version": "0.1.1",
                                "maturity": "incubating",
                                "promotion": {
                                    "candidate_status": "blocked",
                                    "earliest_failed_gate": "immutable_git_url_functional_consumer",
                                    "satisfied": ["wired_shipped_prefabs_local"],
                                    "pending": ["immutable_git_url_functional_consumer"],
                                },
                            }
                        ]
                    }
                ),
                encoding="utf-8",
            )
            (root / "reference-catalog.json").write_text(
                json.dumps(
                    {
                        "artifacts": [
                            {"id": "unity-inventory-ugui", "maturity": "incubating"},
                            {"id": "inventory-package-family-standard", "maturity": "incubating"},
                        ]
                    }
                ),
                encoding="utf-8",
            )

            errors = MODULE.validate_inventory_projection_coherence(root)
            self.assertTrue(any("stale or missing projection row" in error for error in errors))

    def test_consumer_project_marker_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            marker = "VR" + "soundscape"
            (root / "README.md").write_text(marker, encoding="utf-8")
            errors = MODULE.scan_text_safety(root)
            self.assertTrue(any("non-public marker" in error for error in errors))

    def test_internal_system_marker_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            marker = "AI" + "OS"
            (root / "README.md").write_text(marker, encoding="utf-8")
            errors = MODULE.scan_text_safety(root)
            self.assertTrue(any("non-public marker" in error for error in errors))

    def test_unanchored_unity_build_ignore_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / ".gitignore").write_text("Build/\n", encoding="utf-8")
            errors = MODULE.validate_ignore_scope(root)
            self.assertTrue(any("root-anchored" in error for error in errors))

    def test_missing_internal_namespace_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            package = Path(directory)
            (package / "Consumer.cs").write_text(
                "using Lingkyn.Unity.Missing; namespace Lingkyn.Unity.Consumer {}",
                encoding="utf-8",
            )
            errors = MODULE.validate_internal_namespace_links(package)
            self.assertTrue(any("no source declaration" in error for error in errors))

    def test_inventory_manifest_rejects_non_positive_source(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "source-manifest.json"
            path.write_text(
                json.dumps(
                    {
                        "schema": "xr-foundry.inventory_source_manifest.v1",
                        "derivation_policy": "admitted_positive_external_sources_only",
                        "consumer_material_allowed": False,
                        "screened_out_material_allowed": False,
                        "implementation_policy": "independently_authored_from_public_contracts",
                        "forbidden_source_scopes": [
                            "consumer_project",
                            "course_project",
                            "internal_prototype",
                            "screened_out_candidate",
                        ],
                        "sources": [
                            {
                                "id": "bad-seed",
                                "admission": "raw_material",
                                "provenance_scope": "external_public",
                                "code_seed_allowed": False,
                                "authority_class": "maintained_open_source_implementation",
                                "url": "https://example.com/bad-seed",
                                "positive_evidence": ["popular"],
                                "limits": ["not reviewed"],
                                "license_boundary": "unknown",
                            }
                        ],
                    }
                ),
                encoding="utf-8",
            )
            errors = MODULE.validate_inventory_source_manifest(path)
            self.assertTrue(any("not an admitted positive source" in error for error in errors))

    def test_inventory_manifest_rejects_consumer_material(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "source-manifest.json"
            path.write_text(
                json.dumps(
                    {
                        "schema": "xr-foundry.inventory_source_manifest.v1",
                        "derivation_policy": "admitted_positive_external_sources_only",
                        "consumer_material_allowed": True,
                        "screened_out_material_allowed": False,
                        "implementation_policy": "independently_authored_from_public_contracts",
                        "forbidden_source_scopes": [
                            "consumer_project",
                            "course_project",
                            "internal_prototype",
                            "screened_out_candidate",
                        ],
                        "sources": [],
                    }
                ),
                encoding="utf-8",
            )
            errors = MODULE.validate_inventory_source_manifest(path)
            self.assertTrue(any("reject consumer material" in error for error in errors))


if __name__ == "__main__":
    unittest.main()
