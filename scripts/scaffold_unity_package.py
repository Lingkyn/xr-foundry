from __future__ import annotations

import argparse
import hashlib
import json
import re
from pathlib import Path, PurePosixPath
from typing import Any

from jsonschema import Draft202012Validator, FormatChecker


ROOT = Path(__file__).resolve().parents[1]
BLUEPRINT_SCHEMA = ROOT / "docs" / "foundry" / "unity-package-blueprint.schema.json"
FULL_PACKAGE_ID = re.compile(r"com\.lingkyn\.[a-z0-9_.-]+")


def load_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def canonical_json(payload: Any) -> str:
    return json.dumps(payload, sort_keys=True, separators=(",", ":"), ensure_ascii=False)


def deterministic_guid(package_id: str, relative_path: str) -> str:
    return hashlib.sha256(f"{package_id}:{relative_path}".encode("utf-8")).hexdigest()[:32]


def validate_blueprint(payload: Any, schema_path: Path = BLUEPRINT_SCHEMA) -> list[str]:
    if not schema_path.exists():
        return [f"Blueprint schema is missing: {schema_path}"]
    schema = load_json(schema_path)
    Draft202012Validator.check_schema(schema)
    validator = Draft202012Validator(schema, format_checker=FormatChecker())
    errors = [
        f"Blueprint schema violation at {'.'.join(str(part) for part in issue.absolute_path) or '$'}: {issue.message}"
        for issue in sorted(
            validator.iter_errors(payload),
            key=lambda item: ".".join(str(part) for part in item.absolute_path),
        )
    ]
    if not isinstance(payload, dict):
        return errors
    package = payload.get("package")
    if not isinstance(package, dict):
        return errors
    package_id = package.get("id")
    target_path = package.get("target_path")
    if isinstance(package_id, str) and isinstance(target_path, str):
        parts = PurePosixPath(target_path).parts
        if not parts or parts[-1] != package_id:
            errors.append("Blueprint target_path leaf must equal package.id")
        if PurePosixPath(target_path).is_absolute() or ".." in parts or "." in parts:
            errors.append("Blueprint target_path must be a safe repository-relative path")
        category = package.get("category")
        family = package.get("family")
        if category == "foundation" and not target_path.startswith("packages/unity/foundations/"):
            errors.append("Foundation blueprint must target packages/unity/foundations")
        if category == "system" and target_path != f"packages/unity/systems/{family}/{package_id}":
            errors.append("System blueprint target_path must bind its family and package id")
        if category == "adapter" and target_path != f"packages/unity/adapters/{family}/{package_id}":
            errors.append("Adapter blueprint target_path must bind its family and package id")
    return errors


def folder_meta(package_id: str, relative_path: str) -> str:
    return (
        "fileFormatVersion: 2\n"
        f"guid: {deterministic_guid(package_id, relative_path)}\n"
        "folderAsset: yes\n"
        "DefaultImporter:\n"
        "  externalObjects: {}\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n"
    )


def file_meta(package_id: str, relative_path: str, importer: str = "DefaultImporter") -> str:
    return (
        "fileFormatVersion: 2\n"
        f"guid: {deterministic_guid(package_id, relative_path)}\n"
        f"{importer}:\n"
        "  externalObjects: {}\n"
        "  serializedVersion: 2\n"
        "  defaultReferences: []\n"
        "  executionOrder: 0\n"
        "  icon: {instanceID: 0}\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n"
    )


def build_scaffold_plan(payload: dict[str, Any], license_text: str) -> dict[str, str]:
    package = payload["package"]
    scaffold = payload["scaffold"]
    package_id = package["id"]
    namespace = package["root_namespace"]
    sample_name = scaffold["sample_name"]
    test_assembly = f"{namespace}.Editor.Tests"
    manifest = {
        "name": package_id,
        "version": package["version"],
        "displayName": package["display_name"],
        "description": package["description"],
        "unity": package["unity"],
        "license": "MIT",
        "author": {
            "name": "XR Foundry Contributors",
            "url": "https://github.com/Lingkyn/xr-foundry",
        },
        "dependencies": package["dependencies"],
        "samples": [
            {
                "displayName": sample_name,
                "description": scaffold["sample_description"],
                "path": f"Samples~/{sample_name}",
            }
        ],
    }
    test_asmdef = {
        "name": test_assembly,
        "rootNamespace": test_assembly,
        "references": [],
        "includePlatforms": ["Editor"],
        "excludePlatforms": [],
        "allowUnsafeCode": False,
        "overrideReferences": False,
        "precompiledReferences": [],
        "autoReferenced": False,
        "defineConstraints": [],
        "versionDefines": [],
        "noEngineReferences": False,
        "optionalUnityReferences": ["TestAssemblies"],
    }
    blueprint_sha256 = hashlib.sha256(canonical_json(payload).encode("utf-8")).hexdigest()
    marker = {
        "schema": "xr-foundry.scaffold_marker.v1",
        "package_id": package_id,
        "blueprint_sha256": blueprint_sha256,
        "implementation_issue": payload["admission"]["implementation_issue"],
        "catalog_admission": False,
        "maturity_granted": False,
        "release_granted": False,
        "device_claim_granted": False,
        "required_next_action": "Replace the deliberate failing scaffold test with implemented behavior and focused tests before catalog admission.",
    }
    test_source = f'''using NUnit.Framework;

namespace {namespace}.Editor.Tests
{{
    public sealed class FoundryScaffoldContractTests
    {{
        [Test]
        public void ImplementationMustReplaceScaffoldMarker()
        {{
            Assert.Fail("Foundry scaffold is not an implementation. Replace this test with focused behavior tests before catalog admission.");
        }}
    }}
}}
'''
    plan = {
        ".foundry-scaffold.json": json.dumps(marker, indent=2) + "\n",
        "package.json": json.dumps(manifest, indent=2) + "\n",
        "README.md": (
            f"# {package['display_name']}\n\n"
            "Status: generated staging scaffold; not catalog-admitted or released.\n\n"
            f"{package['description']}\n\n"
            "The deliberate failing test must be replaced by implemented behavior and focused tests.\n"
        ),
        "CHANGELOG.md": (
            "# Changelog\n\n"
            "## [Unreleased]\n\n"
            "- Generated an unadmitted Foundry staging scaffold.\n"
        ),
        "LICENSE.md": license_text.rstrip() + "\n",
        "Documentation~/index.md": (
            f"# {package['display_name']}\n\n"
            "This staging scaffold intentionally contains no implementation claim.\n"
        ),
        f"Samples~/{sample_name}/README.md": (
            f"# {sample_name}\n\n{scaffold['sample_description']}\n\n"
            "This sample cannot be accepted until the package implementation and tests exist.\n"
        ),
        "Tests.meta": folder_meta(package_id, "Tests"),
        "Tests/Editor.meta": folder_meta(package_id, "Tests/Editor"),
        f"Tests/Editor/{test_assembly}.asmdef": json.dumps(test_asmdef, indent=2) + "\n",
        f"Tests/Editor/{test_assembly}.asmdef.meta": file_meta(
            package_id, f"Tests/Editor/{test_assembly}.asmdef"
        ),
        "Tests/Editor/FoundryScaffoldContractTests.cs": test_source,
        "Tests/Editor/FoundryScaffoldContractTests.cs.meta": file_meta(
            package_id,
            "Tests/Editor/FoundryScaffoldContractTests.cs",
            "MonoImporter",
        ),
    }
    return dict(sorted(plan.items()))


def resolve_target(output_root: Path, target_path: str) -> Path:
    output_root = output_root.resolve()
    target = (output_root / PurePosixPath(target_path)).resolve()
    try:
        target.relative_to(output_root)
    except ValueError as error:
        raise ValueError("Scaffold target resolves outside output root") from error
    return target


def write_scaffold(target: Path, plan: dict[str, str]) -> None:
    if target.exists():
        raise FileExistsError(f"Scaffold target already exists: {target}")
    target.mkdir(parents=True, exist_ok=False)
    for relative_path, content in plan.items():
        path = target / PurePosixPath(relative_path)
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding="utf-8", newline="\n")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("blueprint", type=Path)
    parser.add_argument("--output-root", type=Path, required=True)
    parser.add_argument("--write", action="store_true")
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()

    blueprint_path = args.blueprint.resolve()
    payload = load_json(blueprint_path)
    errors = validate_blueprint(payload)
    if args.write and payload.get("record_status") != "admitted":
        errors.append("Only an admitted blueprint may write a scaffold")
    target_path = payload.get("package", {}).get("target_path", "")
    try:
        target = resolve_target(args.output_root, target_path)
    except (OSError, RuntimeError, ValueError) as error:
        errors.append(str(error))
        target = args.output_root.resolve()

    plan: dict[str, str] = {}
    if not errors:
        plan = build_scaffold_plan(payload, (ROOT / "LICENSE").read_text(encoding="utf-8"))
        if args.write:
            try:
                write_scaffold(target, plan)
            except (FileExistsError, OSError) as error:
                errors.append(str(error))

    report = {
        "schema": "xr-foundry.scaffold_result.v1",
        "status": "fail" if errors else ("written" if args.write else "preview"),
        "blueprint": str(blueprint_path),
        "target": str(target),
        "record_status": payload.get("record_status"),
        "catalog_admission": False,
        "files": list(plan),
        "errors": errors,
    }
    print(json.dumps(report, indent=2))
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())

