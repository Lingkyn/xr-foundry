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


def completed_inventory_xr_device_receipt(
    package_id: str = "com.lingkyn.inventory.xr.ugui",
) -> dict:
    payload = json.loads(
        (ROOT / "docs" / "validation" / "inventory-xr-device-receipt.template.json").read_text(
            encoding="utf-8"
        )
    )
    payload["package"].update(
        {
            "id": package_id,
            "renderer": MODULE.INVENTORY_XR_RENDERERS[package_id],
        }
    )
    payload["receipt_id"] = "inventory-xr-pico-acceptance-2026-07-15"
    payload["package"]["revision"] = "a" * 40
    payload["artifact"].update(
        {
            "apk_sha256": "b" * 64,
            "artifact_ref": "public-artifact-123",
            "application_id": "com.example.inventoryxrvalidation",
        }
    )
    payload["software"].update(
        {
            "unity_version": "6000.3.19f1",
            "xri_version": "3.5.1",
            "xr_management_version": "4.5.0",
            "openxr_version": "not_used",
            "pico_integration_version": "3.1.0",
            "runtime_provider": "PICO XR",
            "android_target_api": "35",
            "graphics_api": "Vulkan",
        }
    )
    payload["device"].update(
        {
            "model": "PICO 4",
            "os_version": "5.13.0",
            "firmware_version": "5.13.0",
        }
    )
    payload["execution"].update(
        {
            "tested_at_utc": "2026-07-15T12:00:00Z",
            "tester": "public-tester-1",
            "sample_scene": f"InventoryWorldSpaceValidation-{payload['package']['renderer']}",
            "posture": "seated",
            "duration_seconds": 120,
            "install_result": "pass",
            "open_result": "pass",
        }
    )
    for item in payload["checks"]:
        if item["id"] in MODULE.REQUIRED_INVENTORY_XR_DEVICE_CHECKS:
            item.update(
                {
                    "status": "pass",
                    "observation": f"Observed {item['id']} on the named device.",
                    "evidence_refs": [f"evidence/{item['id']}.md"],
                }
            )
    payload["claim_boundary"].update(
        {
            "device_gate_passed": True,
            "headset_usability_claim_allowed": True,
            "controller_ray_claim_allowed": True,
            "direct_poke_device_claim_allowed": False,
        }
    )
    payload["overall_result"] = "pass"
    return payload


class RepositoryContractTests(unittest.TestCase):
    def test_current_repository_passes(self) -> None:
        self.assertEqual([], MODULE.validate_repository(ROOT))

    def test_current_inventory_projections_are_coherent(self) -> None:
        self.assertEqual([], MODULE.validate_inventory_projection_coherence(ROOT))

    def test_current_inventory_xr_device_receipt_contract_passes(self) -> None:
        self.assertEqual([], MODULE.validate_inventory_xr_device_receipt_contract(ROOT))

    def test_reference_catalog_rejects_nonexistent_evidence_path(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            errors = MODULE.validate_reference_evidence_paths(
                root,
                {
                    "artifacts": [
                        {"id": "example", "evidence": ["missing/evidence.md"]}
                    ]
                },
            )
            self.assertTrue(any("does not exist" in error for error in errors))

    def test_repository_layout_rejects_catalog_path_drift(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            layout_root = root / "docs" / "architecture"
            package_root = (
                root / "packages" / "unity" / "systems" / "inventory"
                / "com.lingkyn.inventory.core"
            )
            layout_root.mkdir(parents=True)
            package_root.mkdir(parents=True)
            (package_root / "package.json").write_text(
                json.dumps({"name": "com.lingkyn.inventory.core"}), encoding="utf-8"
            )
            (layout_root / "repository-layout.v1.json").write_text(
                json.dumps(
                    {
                        "schema": "xr-foundry.repository_layout.v1",
                        "status": "accepted_initialization_architecture",
                        "package_root": "packages",
                        "engine_roots": {"unity": "packages/unity"},
                        "collections": [
                            {
                                "id": "unity-system-inventory",
                                "path": "packages/unity/systems/inventory",
                                "packages": ["com.lingkyn.inventory.core"],
                            }
                        ],
                        "invariants": {
                            "leaf_directory_equals_package_id": True,
                            "landing_page_groups_package_families": True,
                            "machine_catalog_keeps_package_entries": True,
                            "consumer_asset_path_uses_package_id": True,
                            "git_url_path_precedes_revision": True,
                            "full_commit_sha_required": True,
                            "old_path_compatibility_layers_allowed": False,
                            "empty_future_engine_roots_allowed": False,
                        },
                    }
                ),
                encoding="utf-8",
            )
            catalog = {
                "packages": [
                    {
                        "id": "com.lingkyn.inventory.core",
                        "path": "packages/unity/foundations/com.lingkyn.inventory.core",
                    }
                ]
            }
            errors = MODULE.validate_repository_layout(root, catalog)
            self.assertTrue(any("layout path mismatch" in error for error in errors))

    def test_repository_layout_rejects_old_root_compatibility_package(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            layout_root = root / "docs" / "architecture"
            package_root = (
                root / "packages" / "unity" / "systems" / "inventory"
                / "com.lingkyn.inventory.core"
            )
            old_root = root / "com.lingkyn.inventory.core"
            layout_root.mkdir(parents=True)
            package_root.mkdir(parents=True)
            old_root.mkdir()
            manifest = json.dumps({"name": "com.lingkyn.inventory.core"})
            (package_root / "package.json").write_text(manifest, encoding="utf-8")
            (old_root / "package.json").write_text(manifest, encoding="utf-8")
            (layout_root / "repository-layout.v1.json").write_text(
                (ROOT / "docs" / "architecture" / "repository-layout.v1.json").read_text(
                    encoding="utf-8"
                ),
                encoding="utf-8",
            )
            catalog = {
                "packages": [
                    {
                        "id": package_id,
                        "path": path,
                    }
                    for package_id, path in MODULE.package_paths_by_id(
                        MODULE.load_json(ROOT / "package-catalog.json")
                    ).items()
                ]
            }
            errors = MODULE.validate_repository_layout(root, catalog)
            self.assertTrue(any("Old root package paths are not allowed" in error for error in errors))

    def test_bug_template_rejects_package_catalog_drift(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            template_root = root / ".github" / "ISSUE_TEMPLATE"
            template_root.mkdir(parents=True)
            (template_root / "bug.yml").write_text(
                "options:\n  - com.lingkyn.inventory.core\n  - Repository tooling\n",
                encoding="utf-8",
            )
            catalog = {
                "packages": [
                    {
                        "id": "com.lingkyn.inventory.core",
                        "path": "packages/unity/systems/inventory/com.lingkyn.inventory.core",
                    },
                    {
                        "id": "com.lingkyn.inventory.ugui",
                        "path": "packages/unity/systems/inventory/com.lingkyn.inventory.ugui",
                    },
                ]
            }
            errors = MODULE.validate_bug_template_package_options(root, catalog)
            self.assertTrue(any("differ from the package catalog" in error for error in errors))

    def test_inventory_projection_rejects_dependency_drift(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            standard_root = root / "docs" / "standards" / "inventory"
            package_root = (
                root
                / "packages"
                / "unity"
                / "systems"
                / "inventory"
                / "com.lingkyn.inventory.presentation"
            )
            standard_root.mkdir(parents=True)
            package_root.mkdir(parents=True)
            (root / "README.md").write_text("Inventory family", encoding="utf-8")
            (root / "ROADMAP.md").write_text(
                "| `com.lingkyn.inventory.presentation` | `0.1.0` | `incubating` | `local_clean_consumer` |\n",
                encoding="utf-8",
            )
            (standard_root / "README.md").write_text("Inventory family", encoding="utf-8")
            (standard_root / "inventory-standard.json").write_text(
                json.dumps(
                    {
                        "package_family": [
                            {
                                "id": "com.lingkyn.inventory.presentation",
                                "required_dependencies": [],
                                "implementation_status": "implemented_incubating",
                                "earliest_failed_gate": "local_clean_consumer",
                            }
                        ]
                    }
                ),
                encoding="utf-8",
            )
            (package_root / "package.json").write_text(
                json.dumps(
                    {
                        "name": "com.lingkyn.inventory.presentation",
                        "dependencies": {"com.lingkyn.inventory.core": "0.1.0"},
                    }
                ),
                encoding="utf-8",
            )
            (root / "package-catalog.json").write_text(
                json.dumps(
                    {
                        "packages": [
                            {
                                "id": "com.lingkyn.inventory.presentation",
                                "path": "packages/unity/systems/inventory/com.lingkyn.inventory.presentation",
                                "version": "0.1.0",
                                "maturity": "incubating",
                                "promotion": {
                                    "candidate_status": "blocked",
                                    "earliest_failed_gate": "local_clean_consumer",
                                    "satisfied": ["renderer_neutral_api_extracted"],
                                    "pending": ["local_clean_consumer"],
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
                            {"id": "unity-inventory-presentation", "maturity": "incubating"},
                            {"id": "inventory-package-family-standard", "maturity": "incubating"},
                        ]
                    }
                ),
                encoding="utf-8",
            )

            errors = MODULE.validate_inventory_projection_coherence(root)
            self.assertTrue(any("dependency projection drift" in error for error in errors))

    def test_completed_inventory_xr_device_receipt_passes_for_both_renderers(self) -> None:
        for package_id in MODULE.INVENTORY_XR_RENDERERS:
            with self.subTest(package_id=package_id):
                self.assertEqual(
                    [],
                    MODULE.validate_inventory_xr_device_receipt(
                        completed_inventory_xr_device_receipt(package_id), require_pass=True
                    ),
                )

    def test_inventory_xr_device_receipt_rejects_renderer_package_mismatch(self) -> None:
        payload = completed_inventory_xr_device_receipt()
        payload["package"]["renderer"] = "uitoolkit"
        errors = MODULE.validate_inventory_xr_device_receipt(payload, require_pass=True)
        self.assertTrue(any("renderer must match" in error for error in errors))

    def test_inventory_xr_device_receipt_rejects_untested_required_check(self) -> None:
        payload = completed_inventory_xr_device_receipt()
        target = next(item for item in payload["checks"] if item["id"] == "right_controller_lcr_press")
        target.update({"status": "not_tested", "observation": "", "evidence_refs": []})
        errors = MODULE.validate_inventory_xr_device_receipt(payload, require_pass=True)
        self.assertTrue(any("right_controller_lcr_press" in error for error in errors))

    def test_inventory_xr_device_receipt_rejects_unproven_direct_poke_claim(self) -> None:
        payload = completed_inventory_xr_device_receipt()
        payload["claim_boundary"]["direct_poke_device_claim_allowed"] = True
        errors = MODULE.validate_inventory_xr_device_receipt(payload, require_pass=True)
        self.assertTrue(any("Direct-poke device claim" in error for error in errors))

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

    def test_inventory_projection_rejects_stale_xr_not_implemented_claim(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            standard_root = root / "docs" / "standards" / "inventory"
            standard_root.mkdir(parents=True)
            (root / "README.md").write_text("XR adapter is incubating.", encoding="utf-8")
            (root / "ROADMAP.md").write_text(
                "XR is still not implemented.\n"
                "| `com.lingkyn.inventory.xr.ugui` | `0.1.0` | `incubating` | "
                "`immutable_git_url_clean_consumer` |\n",
                encoding="utf-8",
            )
            (standard_root / "README.md").write_text("XR adapter is incubating.", encoding="utf-8")
            (standard_root / "inventory-standard.json").write_text(
                json.dumps(
                    {
                        "package_family": [
                            {
                                "id": "com.lingkyn.inventory.xr.ugui",
                                "implementation_status": "implemented_incubating",
                                "earliest_failed_gate": "immutable_git_url_clean_consumer",
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
                                "id": "com.lingkyn.inventory.xr.ugui",
                                "version": "0.1.0",
                                "maturity": "incubating",
                                "promotion": {
                                    "candidate_status": "blocked",
                                    "earliest_failed_gate": "immutable_git_url_clean_consumer",
                                    "satisfied": ["xr_adapter_tests"],
                                    "pending": ["immutable_git_url_clean_consumer"],
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
                            {"id": "unity-inventory-xr-ugui", "maturity": "incubating"},
                            {"id": "inventory-package-family-standard", "maturity": "incubating"},
                        ]
                    }
                ),
                encoding="utf-8",
            )

            errors = MODULE.validate_inventory_projection_coherence(root)
            self.assertTrue(any("stale Inventory XR implementation claim" in error for error in errors))

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

    def test_privacy_scan_covers_every_decodable_text_extension(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            marker = "VR" + "soundscape"
            suffixes = [".py", ".uxml", ".uss", ".prefab", ".asset", ".meta", ".custom"]
            for index, suffix in enumerate(suffixes):
                (root / f"surface-{index}{suffix}").write_text(marker, encoding="utf-8")
            errors = MODULE.scan_text_safety(root)
            for index, suffix in enumerate(suffixes):
                self.assertTrue(
                    any(f"surface-{index}{suffix}" in error for error in errors),
                    suffix,
                )

    def test_unanchored_unity_build_ignore_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / ".gitignore").write_text("Build/\n", encoding="utf-8")
            errors = MODULE.validate_ignore_scope(root)
            self.assertTrue(any("root-anchored" in error for error in errors))

    def test_active_old_root_git_package_url_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "README.md").write_text(
                "https://github.com/Lingkyn/xr-foundry.git?path=/"
                "com.lingkyn.inventory.core#" + "a" * 40,
                encoding="utf-8",
            )
            errors = MODULE.validate_active_repository_path_references(root)
            self.assertTrue(any("old root Git UPM path" in error for error in errors))

    def test_historical_validation_receipt_may_retain_old_path_fact(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            receipt_root = root / "docs" / "validation"
            receipt_root.mkdir(parents=True)
            (receipt_root / "2026-07-15-historical.md").write_text(
                "https://github.com/Lingkyn/xr-foundry.git?path=/"
                "com.lingkyn.inventory.core#" + "a" * 40,
                encoding="utf-8",
            )
            self.assertEqual([], MODULE.validate_active_repository_path_references(root))

    def test_active_validation_template_rejects_old_path(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            receipt_root = root / "docs" / "validation"
            receipt_root.mkdir(parents=True)
            (receipt_root / "receipt-template.md").write_text(
                "https://github.com/Lingkyn/xr-foundry.git?path=/"
                "com.lingkyn.inventory.core#" + "a" * 40,
                encoding="utf-8",
            )
            errors = MODULE.validate_active_repository_path_references(root)
            self.assertTrue(any("old root Git UPM path" in error for error in errors))

    def test_active_git_selector_must_match_catalog_canonical_path(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "README.md").write_text(
                "?" + "path=/packages/unity/foundations/com.lingkyn.inventory.core",
                encoding="utf-8",
            )
            catalog = {
                "packages": [
                    {
                        "id": "com.lingkyn.inventory.core",
                        "path": "packages/unity/systems/inventory/com.lingkyn.inventory.core",
                    }
                ]
            }
            errors = MODULE.validate_active_git_upm_selectors(root, catalog)
            self.assertTrue(any("selector path drift" in error for error in errors))

    def test_dated_historical_receipt_is_exempt_from_selector_projection(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            receipt_root = root / "docs" / "validation"
            receipt_root.mkdir(parents=True)
            (receipt_root / "2026-07-15-historical.md").write_text(
                "?" + "path=/com.lingkyn.inventory.core",
                encoding="utf-8",
            )
            catalog = {
                "packages": [
                    {
                        "id": "com.lingkyn.inventory.core",
                        "path": "packages/unity/systems/inventory/com.lingkyn.inventory.core",
                    }
                ]
            }
            self.assertEqual([], MODULE.validate_active_git_upm_selectors(root, catalog))

    def test_readme_git_install_matrix_requires_catalog_and_dependency_closure(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            core_path = "packages/unity/systems/inventory/com.lingkyn.inventory.core"
            renderer_path = "packages/unity/systems/inventory/com.lingkyn.inventory.ugui"
            (root / core_path).mkdir(parents=True)
            (root / renderer_path).mkdir(parents=True)
            (root / core_path / "package.json").write_text(
                json.dumps({"name": "com.lingkyn.inventory.core", "dependencies": {}}),
                encoding="utf-8",
            )
            (root / renderer_path / "package.json").write_text(
                json.dumps(
                    {
                        "name": "com.lingkyn.inventory.ugui",
                        "dependencies": {"com.lingkyn.inventory.core": "0.1.0"},
                    }
                ),
                encoding="utf-8",
            )
            (root / "README.md").write_text(
                '"com.lingkyn.inventory.ugui": "https://github.com/Lingkyn/'
                'xr-foundry.git?path=/packages/unity/systems/inventory/'
                'com.lingkyn.inventory.ugui#<full-40-character-commit-sha>"',
                encoding="utf-8",
            )
            catalog = {
                "packages": [
                    {"id": "com.lingkyn.inventory.core", "path": core_path},
                    {"id": "com.lingkyn.inventory.ugui", "path": renderer_path},
                ]
            }
            errors = MODULE.validate_readme_git_install_matrix(root, catalog)
            self.assertTrue(any("every package catalog entry" in error for error in errors))
            self.assertTrue(any("not dependency-closed" in error for error in errors))

    def test_readme_git_install_matrix_rejects_sibling_sha_drift(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            paths = {
                "com.lingkyn.inventory.core": (
                    "packages/unity/systems/inventory/com.lingkyn.inventory.core"
                ),
                "com.lingkyn.inventory.presentation": (
                    "packages/unity/systems/inventory/com.lingkyn.inventory.presentation"
                ),
            }
            for package_id, package_path in paths.items():
                (root / package_path).mkdir(parents=True)
                (root / package_path / "package.json").write_text(
                    json.dumps({"name": package_id, "dependencies": {}}), encoding="utf-8"
                )
            (root / "README.md").write_text(
                "\n".join(
                    f'"{package_id}": "https://github.com/Lingkyn/xr-foundry.git?path=/'
                    f'{package_path}#{revision}"'
                    for (package_id, package_path), revision in zip(
                        paths.items(), ("a" * 40, "b" * 40)
                    )
                ),
                encoding="utf-8",
            )
            catalog = {
                "packages": [
                    {"id": package_id, "path": package_path}
                    for package_id, package_path in paths.items()
                ]
            }
            errors = MODULE.validate_readme_git_install_matrix(root, catalog)
            self.assertTrue(any("same full Git SHA" in error for error in errors))

    def test_missing_internal_namespace_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            package = Path(directory)
            (package / "Consumer.cs").write_text(
                "using Lingkyn.Unity.Missing; namespace Lingkyn.Unity.Consumer {}",
                encoding="utf-8",
            )
            errors = MODULE.validate_internal_namespace_links(package)
            self.assertTrue(any("no source declaration" in error for error in errors))

    def test_repository_path_is_rejected_as_unity_asset_path(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            package = Path(directory)
            (package / "BadAssetPath.cs").write_text(
                'namespace Lingkyn.Example { public static class Bad { '
                'public const string Path = "packages/unity/systems/inventory/'
                'com.lingkyn.inventory.core/Runtime/Item.asset"; } }',
                encoding="utf-8",
            )
            errors = MODULE.validate_unity_asset_path_literals(
                package, {"com.lingkyn.inventory.core"}
            )
            self.assertTrue(any("repository path used as a Unity asset path" in error for error in errors))

    def test_package_id_mount_is_accepted_as_unity_asset_path(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            package = Path(directory)
            (package / "GoodAssetPath.cs").write_text(
                'namespace Lingkyn.Example { public static class Good { '
                'public const string Path = "Packages/com.lingkyn.inventory.core/'
                'Runtime/Item.asset"; } }',
                encoding="utf-8",
            )
            self.assertEqual(
                [],
                MODULE.validate_unity_asset_path_literals(
                    package, {"com.lingkyn.inventory.core"}
                ),
            )

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
