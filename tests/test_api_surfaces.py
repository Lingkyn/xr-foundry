from __future__ import annotations

import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
VALIDATOR = ROOT / "scripts" / "validate_repository.py"


def load(path: Path, name: str):
    spec = importlib.util.spec_from_file_location(name, path)
    assert spec and spec.loader
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module  # dataclasses in the target module need this
    spec.loader.exec_module(module)
    return module


MODULE = load(VALIDATOR, "validate_repository_api_surfaces")


def write_package(
    root: Path,
    *,
    family: str,
    package_id: str,
    declared_names: tuple[str, ...],
) -> Path:
    package_rel = f"packages/unity/systems/{family}/{package_id}"
    package_root = root / package_rel
    runtime = package_root / "Runtime"
    runtime.mkdir(parents=True)
    body_lines = ["namespace Lingkyn.Example.Core", "{"]
    for name in declared_names:
        body_lines.append(f"    public sealed class {name} {{}}")
    body_lines.append("}")
    (runtime / "Example.cs").write_text("\n".join(body_lines) + "\n", encoding="utf-8")
    (package_root / "package.json").write_text(
        json.dumps({"name": package_id, "version": "0.1.0"}), encoding="utf-8"
    )
    (package_root / "foundry.component.json").write_text(
        json.dumps({"family": family}), encoding="utf-8"
    )
    return package_root


def write_catalog(root: Path, entries: list[tuple[str, str]]) -> None:
    catalog = {
        "packages": [{"id": package_id, "path": path} for package_id, path in entries]
    }
    (root / "package-catalog.json").write_text(json.dumps(catalog), encoding="utf-8")


def write_inventory(
    root: Path,
    *,
    family: str,
    package_id: str,
    listed_names: tuple[str, ...],
    stated_count: int,
) -> None:
    rows = "\n".join(f"| `{name}` | sealed class | none | yes |" for name in listed_names)
    text = (
        f"# Example public API surface\n\n"
        f"## `{package_id}` (namespace `Lingkyn.Example.Core`)\n\n"
        f"{stated_count} public types.\n\n"
        f"| Type | Kind | Public members | Consumer-facing |\n"
        f"| --- | --- | --- | --- |\n"
        f"{rows}\n"
    )
    inventory_path = root / "docs" / "standards" / family / "api-surface.md"
    inventory_path.parent.mkdir(parents=True, exist_ok=True)
    inventory_path.write_text(text, encoding="utf-8")


class ApiSurfaceInventoryTests(unittest.TestCase):
    def test_matching_inventory_passes(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            package_rel = "packages/unity/systems/persistence/com.example.persistence.core"
            write_package(
                root,
                family="persistence",
                package_id="com.example.persistence.core",
                declared_names=("Foo", "Bar"),
            )
            write_catalog(root, [("com.example.persistence.core", package_rel)])
            write_inventory(
                root,
                family="persistence",
                package_id="com.example.persistence.core",
                listed_names=("Foo", "Bar"),
                stated_count=2,
            )
            errors = MODULE.validate_api_surface_inventories(root)
        self.assertEqual([], errors)

    def test_missing_type_is_reported(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            package_rel = "packages/unity/systems/settings/com.example.settings.core"
            write_package(
                root,
                family="settings",
                package_id="com.example.settings.core",
                declared_names=("Foo", "Bar"),
            )
            write_catalog(root, [("com.example.settings.core", package_rel)])
            write_inventory(
                root,
                family="settings",
                package_id="com.example.settings.core",
                listed_names=("Foo",),
                stated_count=1,
            )
            errors = MODULE.validate_api_surface_inventories(root)
        self.assertTrue(
            any("omits a declared type" in error and "`Bar`" in error for error in errors),
            errors,
        )

    def test_extra_type_is_reported(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            package_rel = "packages/unity/systems/interaction/com.example.interaction.core"
            write_package(
                root,
                family="interaction",
                package_id="com.example.interaction.core",
                declared_names=("Foo",),
            )
            write_catalog(root, [("com.example.interaction.core", package_rel)])
            write_inventory(
                root,
                family="interaction",
                package_id="com.example.interaction.core",
                listed_names=("Foo", "Ghost"),
                stated_count=2,
            )
            errors = MODULE.validate_api_surface_inventories(root)
        self.assertTrue(
            any(
                "lists a type no source declares" in error and "`Ghost`" in error
                for error in errors
            ),
            errors,
        )

    def test_stated_count_mismatch_is_reported(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            package_rel = "packages/unity/systems/persistence/com.example.persistence.core"
            write_package(
                root,
                family="persistence",
                package_id="com.example.persistence.core",
                declared_names=("Foo", "Bar"),
            )
            write_catalog(root, [("com.example.persistence.core", package_rel)])
            write_inventory(
                root,
                family="persistence",
                package_id="com.example.persistence.core",
                listed_names=("Foo", "Bar"),
                stated_count=39,
            )
            errors = MODULE.validate_api_surface_inventories(root)
        self.assertTrue(
            any("states 39 public types" in error and "declares 2" in error for error in errors),
            errors,
        )

    def test_family_with_no_inventory_file_is_skipped(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            package_rel = "packages/unity/systems/settings/com.example.settings.core"
            write_package(
                root,
                family="settings",
                package_id="com.example.settings.core",
                declared_names=("Foo",),
            )
            write_catalog(root, [("com.example.settings.core", package_rel)])
            # No docs/standards/settings/api-surface.md is written at all.
            errors = MODULE.validate_api_surface_inventories(root)
        self.assertEqual([], errors)

    def test_hints_exist_for_each_new_error_shape(self) -> None:
        explained = MODULE.explain_errors(
            [
                "API surface inventory for persistence omits a declared type: "
                "com.example.persistence.core declares `Bar` in "
                "packages/unity/systems/persistence/com.example.persistence.core/Runtime/Example.cs "
                "but docs/standards/persistence/api-surface.md does not list it",
                "API surface inventory for interaction lists a type no source declares: "
                "docs/standards/interaction/api-surface.md lists `Ghost` for "
                "com.example.interaction.core but no Runtime/**/*.cs under "
                "packages/unity/systems/interaction/com.example.interaction.core declares it",
                "API surface inventory for persistence states 39 public types for "
                "com.example.persistence.core in docs/standards/persistence/api-surface.md "
                "but Runtime/ declares 2",
            ]
        )
        placeholder = (
            "No specific hint is recorded for this rule yet; search "
            "scripts/validate_repository.py for the message text to find the check, and read "
            "docs/contributing/start-here.md."
        )
        for entry in explained:
            self.assertNotEqual(placeholder, entry["hint"], entry)

    def test_real_repository_has_no_api_surface_drift(self) -> None:
        self.assertEqual([], MODULE.validate_api_surface_inventories(ROOT))


if __name__ == "__main__":
    unittest.main()
