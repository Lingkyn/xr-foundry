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
SCHEMA_REL = "docs/standards/live-tuning/tunable-surface.schema.json"

SPEC = importlib.util.spec_from_file_location("validate_repository_tunable_surfaces", VALIDATOR)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE  # dataclasses in the target module need this
SPEC.loader.exec_module(MODULE)

PACKAGE_REL = "packages/unity/systems/inventory/com.lingkyn.inventoryfixture.testskin"
CAPABILITY = {"id": "xr-foundry.tuning.surface", "version": "1.0.0"}


def write_json(root: Path, path: str, payload: dict) -> None:
    target = root / path
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def base_component(*, provides_capability: bool = True, package_id: str | None = None) -> dict:
    provides = [{"id": "xr-foundry.inventory.renderer.test", "version": "1.0.0"}]
    if provides_capability:
        provides.append(dict(CAPABILITY))
    return {
        "id": package_id or "com.lingkyn.inventoryfixture.testskin",
        "family": "inventory",
        "provides": provides,
    }


def base_seam(**overrides) -> dict:
    seam = {
        "id": "skin",
        "kind": "skin",
        "type": "Lingkyn.Example.TestSkin",
        "entry_point": "ApplySkin",
        "member_set": ["panelColor", "accentColor"],
    }
    seam.update(overrides)
    return seam


def base_tunable(**overrides) -> dict:
    tunable = {
        "id": "inventory.testskin.skin.surface_panel",
        "kind": "colour",
        "default": [0.031, 0.094, 0.122, 0.96],
        "target": {"seam": "skin", "member": "panelColor"},
        "label": "Panel background",
        "group": "Surfaces",
        "scope": {"package": "com.lingkyn.inventoryfixture.testskin", "skin": "TestSkin"},
        "token": "surface.panel",
    }
    tunable.update(overrides)
    return tunable


def base_manifest(*, tunables: list[dict] | None = None, seams: list[dict] | None = None, package_id: str | None = None) -> dict:
    return {
        "schema": "xr-foundry.tunable_surface.v1",
        "package_id": package_id or "com.lingkyn.inventoryfixture.testskin",
        "family": "inventory",
        "seams": seams if seams is not None else [base_seam()],
        "export": {"destination_kind": "asset", "path": "Assets/Example/TestSkin.asset"},
        "tunables": tunables if tunables is not None else [base_tunable()],
    }


def write_package(
    root: Path,
    *,
    package_rel: str = PACKAGE_REL,
    component: dict | None = None,
    manifest: dict | None | bool = True,
) -> Path:
    """manifest=True writes base_manifest(); a dict writes it verbatim; False/None writes no file."""

    package_root = root / package_rel
    write_json(root, f"{package_rel}/foundry.component.json", component if component is not None else base_component())
    if manifest is True:
        write_json(root, f"{package_rel}/foundry.tunables.json", base_manifest())
    elif isinstance(manifest, dict):
        write_json(root, f"{package_rel}/foundry.tunables.json", manifest)
    return package_root


def write_catalog(root: Path, entries: list[tuple[str, str]]) -> None:
    write_json(root, "package-catalog.json", {"packages": [{"id": pid, "path": path} for pid, path in entries]})


def stage_schema(root: Path) -> None:
    target = root / SCHEMA_REL
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy(ROOT / SCHEMA_REL, target)


def write_design_language(root: Path) -> None:
    write_json(
        root,
        "docs/standards/design-language/ui-design-language-standard.json",
        {
            "design_tokens": {
                "surface": {"panel": {"rgba": [0.031, 0.094, 0.122, 0.96]}},
                "slot_states": {"hover": {"rgba": [0.09, 0.345, 0.388, 1.0]}},
            }
        },
    )


class TunableSurfaceTests(unittest.TestCase):
    def test_valid_manifest_passes(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            stage_schema(root)
            write_package(root)
            write_design_language(root)
            errors = MODULE.validate_tunable_surfaces(root)
        self.assertEqual([], errors)

    def test_schema_violation_is_reported(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            stage_schema(root)
            bad_manifest = base_manifest()
            del bad_manifest["export"]
            write_package(root, manifest=bad_manifest)
            write_design_language(root)
            errors = MODULE.validate_tunable_surfaces(root)
        self.assertTrue(any("JSON Schema violation" in error for error in errors), errors)

    def test_duplicate_canonical_id_across_manifests_is_reported(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            stage_schema(root)
            write_package(root)
            write_package(
                root,
                package_rel="packages/unity/systems/inventory/com.lingkyn.inventoryfixture.othertestskin",
                component=base_component(package_id="com.lingkyn.inventoryfixture.othertestskin"),
                manifest=base_manifest(package_id="com.lingkyn.inventoryfixture.othertestskin"),
            )
            write_design_language(root)
            errors = MODULE.validate_tunable_surfaces(root)
        self.assertTrue(any("duplicate canonical tunable id" in error for error in errors), errors)

    def test_kind_outside_closed_set_is_a_schema_violation(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            stage_schema(root)
            manifest = base_manifest(tunables=[base_tunable(kind="matrix4x4")])
            write_package(root, manifest=manifest)
            write_design_language(root)
            errors = MODULE.validate_tunable_surfaces(root)
        self.assertTrue(any("JSON Schema violation" in error for error in errors), errors)

    def test_default_out_of_range_is_reported(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            stage_schema(root)
            manifest = base_manifest(
                tunables=[
                    base_tunable(
                        id="inventory.testskin.skin.corner_radius",
                        kind="float",
                        default=99.0,
                        range={"min": 0.0, "max": 32.0, "step": 1.0},
                        target={"seam": "skin", "member": "panelColor"},
                        token=None,
                    )
                ]
            )
            del manifest["tunables"][0]["token"]
            write_package(root, manifest=manifest)
            write_design_language(root)
            errors = MODULE.validate_tunable_surfaces(root)
        self.assertTrue(any("outside its declared range" in error for error in errors), errors)

    def test_default_outside_value_set_is_reported(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            stage_schema(root)
            manifest = base_manifest(
                tunables=[
                    {
                        "id": "inventory.testskin.skin.mode",
                        "kind": "enum",
                        "default": "ghost",
                        "values": ["a", "b"],
                        "target": {"seam": "skin", "member": "panelColor"},
                        "label": "Mode",
                        "group": "Surfaces",
                        "scope": {"package": "com.lingkyn.inventoryfixture.testskin", "skin": "TestSkin"},
                    }
                ]
            )
            write_package(root, manifest=manifest)
            write_design_language(root)
            errors = MODULE.validate_tunable_surfaces(root)
        self.assertTrue(any("is not one of its declared values" in error for error in errors), errors)

    def test_target_member_outside_seam_member_set_is_reported(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            stage_schema(root)
            manifest = base_manifest(tunables=[base_tunable(target={"seam": "skin", "member": "ghostMember"})])
            write_package(root, manifest=manifest)
            write_design_language(root)
            errors = MODULE.validate_tunable_surfaces(root)
        self.assertTrue(any("is not in seam" in error for error in errors), errors)

    def test_target_seam_not_declared_is_reported(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            stage_schema(root)
            manifest = base_manifest(tunables=[base_tunable(target={"seam": "ghost", "member": "panelColor"})])
            write_package(root, manifest=manifest)
            write_design_language(root)
            errors = MODULE.validate_tunable_surfaces(root)
        self.assertTrue(any("does not name a declared seam" in error for error in errors), errors)

    def test_unresolved_token_is_reported(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            stage_schema(root)
            manifest = base_manifest(tunables=[base_tunable(token="surface.does_not_exist")])
            write_package(root, manifest=manifest)
            write_design_language(root)
            errors = MODULE.validate_tunable_surfaces(root)
        self.assertTrue(any("does not resolve inside" in error for error in errors), errors)

    def test_package_id_mismatch_with_sibling_component_is_reported(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            stage_schema(root)
            manifest = base_manifest(package_id="com.lingkyn.inventoryfixture.wrongid")
            write_package(root, manifest=manifest)
            write_design_language(root)
            errors = MODULE.validate_tunable_surfaces(root)
        self.assertTrue(any("package_id" in error and "does not match" in error for error in errors), errors)

    def test_manifest_without_matching_provides_is_reported(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            stage_schema(root)
            write_package(root, component=base_component(provides_capability=False))
            write_design_language(root)
            errors = MODULE.validate_tunable_surfaces(root)
        self.assertTrue(
            any("does not provide xr-foundry.tuning.surface" in error for error in errors), errors
        )

    def test_provides_capability_without_manifest_is_reported(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            stage_schema(root)
            write_package(root, manifest=False)
            write_catalog(root, [("com.lingkyn.inventoryfixture.testskin", PACKAGE_REL)])
            write_design_language(root)
            errors = MODULE.validate_tunable_surfaces(root)
        self.assertTrue(any("ships no foundry.tunables.json" in error for error in errors), errors)

    def test_hints_exist_for_each_new_error_shape(self) -> None:
        explained = MODULE.explain_errors(
            [
                "tunable surface x/foundry.tunables.json: JSON Schema violation at $: 'export' is a required property",
                "tunable surface x/foundry.tunables.json: no colocated foundry.component.json to bind package_id and family against",
                "tunable surface x/foundry.tunables.json: package_id 'a' does not match the sibling foundry.component.json id 'b'",
                "tunable surface x/foundry.tunables.json: family 'a' does not match the sibling foundry.component.json family 'b'",
                "tunable surface x/foundry.tunables.json: package ships foundry.tunables.json but its foundry.component.json does not provide xr-foundry.tuning.surface@1.0.0",
                "tunable surface: com.lingkyn.fixture provides xr-foundry.tuning.surface@1.0.0 but ships no foundry.tunables.json at x",
                "tunable surface x/foundry.tunables.json: duplicate seam id: skin",
                "tunable surface: duplicate canonical tunable id 'a.b' in x and y",
                "tunable surface x/foundry.tunables.json: a.b range min/max must be numbers",
                "tunable surface x/foundry.tunables.json: a.b range min must not exceed max",
                "tunable surface x/foundry.tunables.json: a.b range step must be a positive number",
                "tunable surface x/foundry.tunables.json: a.b default must be an integer for kind int",
                "tunable surface x/foundry.tunables.json: a.b default 99 is outside its declared range [0, 32]",
                "tunable surface x/foundry.tunables.json: a.b must declare a non-empty values list for kind enum",
                "tunable surface x/foundry.tunables.json: a.b default 'ghost' is not one of its declared values",
                "tunable surface x/foundry.tunables.json: a.b default must be a 4-element [r, g, b, a] array with each channel in [0, 1] for kind colour",
                "tunable surface x/foundry.tunables.json: a.b default must be a 2-element number array for kind vector2",
                "tunable surface x/foundry.tunables.json: a.b target.seam 'ghost' does not name a declared seam",
                "tunable surface x/foundry.tunables.json: a.b target.member 'ghost' is not in seam 'skin''s member_set",
                "tunable surface x/foundry.tunables.json: a.b token 'ghost' does not resolve inside ui-design-language-standard.json design_tokens",
                "tunable surface x/foundry.tunables.json: a.b names token 'ghost' but the design-language token document could not be read",
            ]
        )
        placeholder = (
            "No specific hint is recorded for this rule yet; search "
            "scripts/validate_repository.py for the message text to find the check, and read "
            "docs/contributing/start-here.md."
        )
        for entry in explained:
            self.assertNotEqual(placeholder, entry["hint"], entry)

    def test_real_repository_has_no_tunable_surface_drift(self) -> None:
        self.assertEqual([], MODULE.validate_tunable_surfaces(ROOT))


if __name__ == "__main__":
    unittest.main()
