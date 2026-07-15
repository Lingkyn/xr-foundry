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


def current_device_profiles() -> dict[str, dict]:
    return {
        payload["profile_id"]: payload
        for path in (ROOT / "docs" / "device-lab" / "profiles").glob("*.json")
        if isinstance(payload := json.loads(path.read_text(encoding="utf-8")), dict)
    }


def current_device_plans() -> dict[str, dict]:
    return {
        payload["test_plan_id"]: payload
        for path in (ROOT / "docs" / "device-lab" / "test-plans").glob("*.json")
        if isinstance(payload := json.loads(path.read_text(encoding="utf-8")), dict)
    }


def completed_device_lab_receipt() -> dict:
    payload = json.loads(
        (ROOT / "docs" / "device-lab" / "device-receipt.template.json").read_text(
            encoding="utf-8"
        )
    )
    payload["receipt_id"] = "inventory-world-ui-pico-pass"
    payload["task_url"] = "https://github.com/Lingkyn/xr-foundry/issues/1"
    payload["revision"]["commit_sha"] = "a" * 40
    payload["artifact"] = {
        "sha256": "b" * 64,
        "artifact_ref": "https://github.com/Lingkyn/xr-foundry/actions/runs/1",
        "application_id": "com.example.inventoryworldui",
    }
    for role in ("domain", "presentation", "renderer_adapter", "xr_adapter"):
        payload["package_tuple"][role]["version"] = "0.1.0"
    payload["software"].update(
        {
            "engine_version": "6000.3.19f1",
            "runtime_version": "recorded-runtime-version",
        }
    )
    payload["device"].update(
        {
            "model": "PICO 4",
            "os_version": "recorded-os-version",
        }
    )
    payload["input"]["device_description"] = "Recorded left and right tracked controllers"
    required_ids = {
        item["id"]
        for item in current_device_plans()["inventory-world-space-ui-v1"]["required_checks"]
    }
    for check in payload["checks"]:
        if check["id"] in required_ids:
            check.update(
                {
                    "status": "pass",
                    "observation": f"Observed {check['id']} on the recorded composition",
                    "evidence_refs": [
                        {
                            "kind": "repository_file",
                            "ref": f"docs/device-lab/evidence/{check['id']}.md",
                            "sha256": "c" * 64,
                        }
                    ],
                }
            )
    payload["overall_result"] = "pass"
    payload["claims_supported"] = ["inventory-ugui-xr-required-suite"]
    payload["claims_not_supported"] = [
        "inventory-ui-toolkit-xr-required-suite",
        "direct-poke",
        "hand-ray",
        "gaze-and-pinch",
    ]
    payload["tester"]["github_identity"] = "example-tester"
    payload["timestamps"] = {
        "started_at": "2026-07-15T12:00:00Z",
        "completed_at": "2026-07-15T12:05:00Z",
    }
    return payload


class RepositoryContractTests(unittest.TestCase):
    def test_current_repository_passes(self) -> None:
        self.assertEqual([], MODULE.validate_repository(ROOT))

    def test_agent_guide_rejects_existing_project_raw_material(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "AGENTS.md").write_text(
                "admitted positive public sources\nexisting project raw material\n",
                encoding="utf-8",
            )

            errors = MODULE.validate_agent_guide_source_boundary(root)

            self.assertTrue(any("must not admit existing project raw material" in error for error in errors))

    def test_agent_guide_rejects_mojibake(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "AGENTS.md").write_text(
                "admitted positive public sources\n"
                "Consumer implementations are not reference material unless independently reviewed "
                "and admitted as a positive public source.\n"
                "documentation\u9225\u650f",
                encoding="utf-8",
            )

            errors = MODULE.validate_agent_guide_source_boundary(root)

            self.assertTrue(any("contains mojibake" in error for error in errors))

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

    def test_task_contract_rejects_inferred_write_permission(self) -> None:
        payload = json.loads(
            (ROOT / "docs" / "contributing" / "task-contract.example.json").read_text(
                encoding="utf-8"
            )
        )
        payload["authority"]["write_permission_not_inferred"] = False

        errors = MODULE.validate_task_contract(payload, "unsafe task")

        self.assertTrue(any("write_permission_not_inferred=true" in error for error in errors))

    def test_task_hall_example_is_unclaimed(self) -> None:
        payload = json.loads(
            (ROOT / "docs" / "contributing" / "task-contract.example.json").read_text(
                encoding="utf-8"
            )
        )

        self.assertEqual("proposal", payload["state"])
        self.assertFalse(payload["blocked"])
        self.assertEqual("unclaimed", payload["claim"]["status"])
        self.assertIsNone(payload["claim"]["github_identity"])
        self.assertEqual([], MODULE.validate_task_contract(payload, "Task Hall example"))

    def test_task_contract_rejects_competing_lifecycle_vocabulary(self) -> None:
        payload = json.loads(
            (ROOT / "docs" / "contributing" / "task-contract.example.json").read_text(
                encoding="utf-8"
            )
        )
        payload["state"] = "in_progress"

        errors = MODULE.validate_task_contract(payload, "stale lifecycle task")

        self.assertTrue(any("canonical Task Hall lifecycle" in error for error in errors))

    def test_task_contract_enforces_conditional_source_and_device_gates(self) -> None:
        source = json.loads(
            (ROOT / "docs" / "contributing" / "task-contract.example.json").read_text(
                encoding="utf-8"
            )
        )
        required_source = json.loads(json.dumps(source))
        required_source["source_gate"] = {"required": True, "sources": []}
        required_source["state"] = "source_gate"
        source_errors = MODULE.validate_task_contract(required_source, "empty source gate")
        self.assertTrue(any("required source gate" in error for error in source_errors))
        self.assertTrue(any("JSON Schema violation" in error for error in source_errors))

        disabled_device = json.loads(json.dumps(source))
        disabled_device["device_gate"] = {
            "required": False,
            "profiles": ["pico-openxr-controller"],
        }
        device_errors = MODULE.validate_task_contract(disabled_device, "contradictory device gate")
        self.assertTrue(any("profiles must be empty" in error for error in device_errors))
        self.assertTrue(any("JSON Schema violation" in error for error in device_errors))

        device_state = json.loads(json.dumps(source))
        device_state["state"] = "device_test_if_required"
        state_errors = MODULE.validate_task_contract(device_state, "unbound device state")
        self.assertTrue(any("claim.status=claimed" in error for error in state_errors))
        self.assertTrue(any("device_gate.required=true" in error for error in state_errors))

    def test_task_contract_enforces_claim_state_and_complete_lease(self) -> None:
        source = json.loads(
            (ROOT / "docs" / "contributing" / "task-contract.example.json").read_text(
                encoding="utf-8"
            )
        )
        source["state"] = "work"
        source["claim"] = {
            "status": "claimed",
            "github_identity": "",
            "claimed_at": "2026-07-16T12:00:00Z",
            "expires_at": "2026-07-15T12:00:00Z",
            "confirmed_by_maintainer": None,
        }
        source["unexpected_authority"] = "write"

        errors = MODULE.validate_task_contract(source, "incomplete claim")

        self.assertTrue(any("claim.github_identity" in error for error in errors))
        self.assertTrue(any("claim.confirmed_by_maintainer" in error for error in errors))
        self.assertTrue(any("expires_at must be after" in error for error in errors))
        self.assertTrue(any("Additional properties" in error for error in errors))

    def test_task_hall_lifecycle_order_is_machine_enforced(self) -> None:
        payload = json.loads(
            (ROOT / "docs" / "contributing" / "task-hall.v1.json").read_text(
                encoding="utf-8"
            )
        )
        payload["lifecycle"]["ordered_states"][2:4] = ["claimed", "ready"]

        errors = MODULE.validate_task_hall_authority(payload)

        self.assertTrue(any("canonical ordered V1 states" in error for error in errors))

    def test_task_hall_global_authority_rejects_every_permission_escalation(self) -> None:
        source = json.loads(
            (ROOT / "docs" / "contributing" / "task-hall.v1.json").read_text(
                encoding="utf-8"
            )
        )
        violations = {
            "claim_grants_repository_write": True,
            "claim_grants_merge": True,
            "external_agent_auto_write": True,
            "external_agent_auto_merge": True,
            "maintainer_controls_ready_and_merge": False,
            "issue_comment_is_untrusted_input": False,
        }
        for field, unsafe in violations.items():
            with self.subTest(field=field):
                payload = json.loads(json.dumps(source))
                payload["authority"][field] = unsafe
                errors = MODULE.validate_task_hall_authority(payload)
                self.assertTrue(any(field in error for error in errors))

    def test_device_profiles_keep_claim_gates_and_no_capability_steps(self) -> None:
        profiles = current_device_profiles()

        self.assertTrue(profiles["pico-openxr-controller"]["claim_allowed"])
        self.assertFalse(profiles["quest-openxr-controller"]["claim_allowed"])
        self.assertFalse(profiles["vision-pro-spatial-input"]["claim_allowed"])
        self.assertTrue(
            profiles["quest-openxr-controller"]["claim_gate_issue"].endswith("/issues/29")
        )
        self.assertTrue(
            profiles["vision-pro-spatial-input"]["claim_gate_issue"].endswith("/issues/30")
        )
        for profile_id, profile in profiles.items():
            self.assertNotIn("required_checks", profile, profile_id)
            self.assertNotIn("required_scenarios", profile, profile_id)
            self.assertEqual([], MODULE.validate_device_profile(profile, profile_id))

    def test_not_tested_template_is_not_an_execution_receipt(self) -> None:
        payload = json.loads(
            (ROOT / "docs" / "device-lab" / "device-receipt.template.json").read_text(
                encoding="utf-8"
            )
        )

        errors = MODULE.validate_device_lab_execution_receipt(
            payload,
            current_device_profiles(),
            current_device_plans(),
            "blank receipt",
        )

        self.assertTrue(any("not an execution receipt" in error for error in errors))

    def test_revision_bound_device_execution_receipt_is_admissible(self) -> None:
        self.assertEqual(
            [],
            MODULE.validate_device_lab_execution_receipt(
                completed_device_lab_receipt(),
                current_device_profiles(),
                current_device_plans(),
                "revision-bound pass",
            ),
        )

    def test_device_receipt_rejects_unbound_revision_and_artifact(self) -> None:
        payload = completed_device_lab_receipt()
        payload["revision"]["commit_sha"] = None
        payload["artifact"] = {
            "sha256": None,
            "artifact_ref": None,
            "application_id": None,
        }

        errors = MODULE.validate_device_lab_execution_receipt(
            payload, current_device_profiles(), current_device_plans(), "unbound execution"
        )

        self.assertTrue(any("full 40-character commit SHA" in error for error in errors))
        self.assertTrue(any("artifact SHA-256" in error for error in errors))
        self.assertTrue(any("artifact.artifact_ref" in error for error in errors))
        self.assertTrue(any("artifact.application_id" in error for error in errors))

    def test_device_receipt_rejects_renderer_xr_adapter_mismatch(self) -> None:
        payload = completed_device_lab_receipt()
        payload["package_tuple"]["xr_adapter"]["id"] = "com.lingkyn.inventory.xr.uitoolkit"

        errors = MODULE.validate_device_lab_execution_receipt(
            payload, current_device_profiles(), current_device_plans(), "mismatched adapter"
        )

        self.assertTrue(any("xr_adapter does not match" in error for error in errors))

    def test_device_receipt_rejects_runtime_device_and_input_mismatch(self) -> None:
        payload = completed_device_lab_receipt()
        payload["software"]["runtime_id"] = "openxr-meta-quest"
        payload["device"]["family_id"] = "quest-standalone-family"
        payload["input"]["routes"] = ["gaze-and-pinch"]

        errors = MODULE.validate_device_lab_execution_receipt(
            payload, current_device_profiles(), current_device_plans(), "mismatched profile"
        )

        self.assertTrue(any("runtime_id does not match" in error for error in errors))
        self.assertTrue(any("device family does not match" in error for error in errors))
        self.assertTrue(any("input routes do not match" in error for error in errors))

    def test_device_receipt_rejects_untested_required_check(self) -> None:
        payload = completed_device_lab_receipt()
        target = next(check for check in payload["checks"] if check["id"] == "left-center-right-activate")
        target.update({"status": "not_tested", "observation": "", "evidence_refs": []})

        errors = MODULE.validate_device_lab_execution_receipt(
            payload, current_device_profiles(), current_device_plans(), "untested required check"
        )

        self.assertTrue(any("required check cannot remain not_tested" in error for error in errors))

    def test_device_receipt_rejects_unsupported_optional_claim(self) -> None:
        payload = completed_device_lab_receipt()
        claim = next(item for item in payload["optional_claims"] if item["id"] == "direct-poke")
        claim["supported"] = True

        errors = MODULE.validate_device_lab_execution_receipt(
            payload, current_device_profiles(), current_device_plans(), "unsupported optional claim"
        )

        self.assertTrue(any("lacks a passed check" in error for error in errors))
        self.assertTrue(any("not admitted by profile" in error for error in errors))

    def test_device_receipt_rejects_profile_with_claim_disabled(self) -> None:
        payload = completed_device_lab_receipt()
        payload["device_profile_id"] = "quest-openxr-controller"
        payload["software"]["runtime_id"] = "openxr-meta-quest"
        payload["device"].update(
            {
                "family_id": "quest-standalone-family",
                "model": "Meta Quest 3",
                "os_family": "Meta Horizon OS",
            }
        )

        errors = MODULE.validate_device_lab_execution_receipt(
            payload, current_device_profiles(), current_device_plans(), "disabled profile claim"
        )

        self.assertTrue(any("claim_allowed=false" in error for error in errors))

    def test_device_receipt_rejects_free_text_or_cross_composition_claims(self) -> None:
        payload = completed_device_lab_receipt()
        payload["claims_supported"] = ["works-on-every-xr-headset"]
        payload["claims_not_supported"].remove("inventory-ui-toolkit-xr-required-suite")

        errors = MODULE.validate_device_lab_execution_receipt(
            payload, current_device_profiles(), current_device_plans(), "invented claim"
        )

        self.assertTrue(any("not enumerated by the plan" in error for error in errors))
        self.assertTrue(any("claims_supported must equal derived claim IDs" in error for error in errors))
        self.assertTrue(any("claims_not_supported must enumerate" in error for error in errors))

    def test_device_receipt_result_is_derived_from_required_checks(self) -> None:
        payload = completed_device_lab_receipt()
        payload["overall_result"] = "fail"
        payload["claims_supported"] = []
        payload["claims_not_supported"] = [
            "inventory-ugui-xr-required-suite",
            "inventory-ui-toolkit-xr-required-suite",
            "direct-poke",
            "hand-ray",
            "gaze-and-pinch",
        ]

        errors = MODULE.validate_device_lab_execution_receipt(
            payload, current_device_profiles(), current_device_plans(), "false failure"
        )

        self.assertTrue(any("required-check result=pass" in error for error in errors))

    def test_device_receipt_rejects_unbound_version_evidence_and_time(self) -> None:
        payload = completed_device_lab_receipt()
        payload["package_tuple"]["domain"]["version"] = "latest"
        target = next(check for check in payload["checks"] if check["id"] == "artifact-install")
        target["evidence_refs"] = [
            {
                "kind": "repository_file",
                "ref": "../private/evidence.txt",
                "sha256": "0" * 64,
            }
        ]
        payload["timestamps"] = {
            "started_at": "2026-07-15T12:05:00Z",
            "completed_at": "2026-07-15T12:00:00Z",
        }

        errors = MODULE.validate_device_lab_execution_receipt(
            payload, current_device_profiles(), current_device_plans(), "unbound evidence"
        )

        self.assertTrue(any("exact semantic version" in error for error in errors))
        self.assertTrue(any("repository evidence path is unsafe" in error for error in errors))
        self.assertTrue(any("evidence ref requires non-zero SHA-256" in error for error in errors))
        self.assertTrue(any("completed_at must not precede" in error for error in errors))

    def test_workflow_security_rejects_comment_trigger_and_unpinned_action(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            workflows = root / ".github" / "workflows"
            workflows.mkdir(parents=True)
            (workflows / "unsafe.yml").write_text(
                """name: Unsafe\non:\n  issue_comment:\npermissions:\n  contents: read\njobs:\n  run:\n    runs-on: ubuntu-latest\n    steps:\n      - uses: actions/checkout@v6\n""",
                encoding="utf-8",
            )

            errors = MODULE.validate_workflow_security(root)

            self.assertTrue(any("comment-trigger workflows are forbidden" in error for error in errors))
            self.assertTrue(any("must use a full commit SHA" in error for error in errors))

    def test_workflow_security_parses_quoted_triggers_and_inline_permissions(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            workflows = root / ".github" / "workflows"
            workflows.mkdir(parents=True)
            (workflows / "unsafe.yml").write_text(
                """name: Unsafe\n"on":\n  "pull_request_target":\npermissions: {contents: write}\njobs:\n  run:\n    permissions: {issues: write}\n    runs-on: ubuntu-latest\n    steps:\n      - uses: actions/checkout@aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\n        with: {persist-credentials: "false"}\n""",
                encoding="utf-8",
            )

            errors = MODULE.validate_workflow_security(root)

            self.assertTrue(any("pull_request_target" in error for error in errors))
            self.assertTrue(any("workflow-level permission" in error for error in errors))
            self.assertTrue(any("job 'run' permission" in error for error in errors))
            self.assertTrue(any("YAML boolean" in error for error in errors))

    def test_workflow_security_requires_explicit_top_level_permissions(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            workflows = root / ".github" / "workflows"
            workflows.mkdir(parents=True)
            (workflows / "missing.yml").write_text(
                """name: Missing permissions\non: [pull_request]\njobs:\n  test:\n    runs-on: ubuntu-latest\n    steps: []\n""",
                encoding="utf-8",
            )

            errors = MODULE.validate_workflow_security(root)

            self.assertTrue(any("workflow-level permissions must be an explicit mapping" in error for error in errors))

    def test_public_leakage_scan_covers_all_decodable_text_and_skips_binary(self) -> None:
        marker = "vr" + "soundscape"
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            suffixes = [".py", ".uxml", ".uss", ".asset", ""]
            for index, suffix in enumerate(suffixes):
                (root / f"surface-{index}{suffix}").write_text(marker, encoding="utf-8")
            (root / "binary.asset").write_bytes(b"\x00\xff" + marker.encode("utf-8"))
            git_dir = root / ".git"
            git_dir.mkdir()
            (git_dir / "ignored").write_text(marker, encoding="utf-8")

            errors = MODULE.scan_text_safety(root)

            leak_paths = [error for error in errors if "non-public marker" in error]
            self.assertEqual(len(suffixes), len(leak_paths))
            self.assertFalse(any("binary.asset" in error for error in errors))
            self.assertFalse(any(".git" in error for error in errors))


if __name__ == "__main__":
    unittest.main()
