from __future__ import annotations

import argparse
import json
import re
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
REQUIRED_ROOT_FILES = {
    "README.md", "LICENSE", "CHANGELOG.md", "ROADMAP.md", "CONTRIBUTING.md",
    "CODE_OF_CONDUCT.md", "SECURITY.md", "SUPPORT.md", "AGENTS.md", "CLAUDE.md",
    "SKILL.md", "package-catalog.json", "reference-catalog.json",
}
REQUIRED_PACKAGE_ENTRIES = {
    "package.json", "README.md", "CHANGELOG.md", "LICENSE.md",
    "Documentation~", "Tests", "Samples~",
}
TEXT_SUFFIXES = {".cs", ".asmdef", ".json", ".md", ".yml", ".yaml", ".txt"}


def forbidden_public_markers() -> list[str]:
    fragments = [
        ("ai", "os"),
        ("agent", "-os"),
        ("skill", "-system"),
        ("vr", "soundscape"),
        ("com.vr", "soundscape"),
        ("_steward", "ship"),
        ("work ", "packet"),
        (".", "ai", "os"),
    ]
    return ["".join(parts).casefold() for parts in fragments]


def load_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def scan_text_safety(root: Path) -> list[str]:
    errors: list[str] = []
    absolute_windows_path = re.compile(r"\b[A-Za-z]:\\(?:Users|Program Files|rrjm)\\", re.IGNORECASE)
    secret_pattern = re.compile(r"(api[_-]?key|access[_-]?token|client[_-]?secret)\s*[:=]\s*['\"][^'\"]+", re.IGNORECASE)
    for path in root.rglob("*"):
        if not path.is_file() or ".git" in path.parts or path.suffix.lower() not in TEXT_SUFFIXES:
            continue
        text = path.read_text(encoding="utf-8", errors="replace")
        lowered = text.casefold()
        for marker in forbidden_public_markers():
            if marker in lowered:
                errors.append(f"non-public marker in live repository: {path.relative_to(root)}")
        if absolute_windows_path.search(text):
            errors.append(f"machine-local Windows path in live repository: {path.relative_to(root)}")
        if secret_pattern.search(text):
            errors.append(f"possible credential in live repository: {path.relative_to(root)}")
    return errors


def validate_repository(root: Path) -> list[str]:
    errors: list[str] = scan_text_safety(root)
    for name in sorted(REQUIRED_ROOT_FILES):
        if not (root / name).exists():
            errors.append(f"missing root community/product file: {name}")

    catalog_path = root / "package-catalog.json"
    if not catalog_path.exists():
        return errors
    catalog = load_json(catalog_path)
    if catalog.get("schema") != "xr-foundry.unity_package_catalog.v1":
        errors.append("package catalog schema is invalid")

    reference_catalog_path = root / "reference-catalog.json"
    if not reference_catalog_path.exists():
        errors.append("reference catalog is missing")
    else:
        reference_catalog = load_json(reference_catalog_path)
        if reference_catalog.get("schema") != "xr-foundry.reference_catalog.v1":
            errors.append("reference catalog schema is invalid")
        artifact_paths = {
            str(item.get("path", ""))
            for item in reference_catalog.get("artifacts", [])
            if isinstance(item, dict)
        }
        package_paths = {
            str(item.get("path", ""))
            for item in catalog.get("packages", [])
            if isinstance(item, dict)
        }
        if artifact_paths != package_paths:
            errors.append("reference/package catalog paths must agree for all live packages")

    catalog_packages = catalog.get("packages", [])
    declared_paths: set[str] = set()
    for item in catalog_packages:
        if not isinstance(item, dict):
            errors.append("package catalog entries must be objects")
            continue
        package_id = str(item.get("id", ""))
        relative = str(item.get("path", ""))
        declared_paths.add(relative)
        package_root = root / relative
        if not package_id.startswith("com.lingkyn."):
            errors.append(f"package id must start with com.lingkyn.: {package_id}")
        if relative != package_id:
            errors.append(f"package path must match package id: {relative} != {package_id}")
        if item.get("maturity") not in catalog.get("maturity_states", []):
            errors.append(f"unknown maturity for {package_id}: {item.get('maturity')}")
        for required in sorted(REQUIRED_PACKAGE_ENTRIES):
            if not (package_root / required).exists():
                errors.append(f"{package_id}: missing {required}")
        manifest_path = package_root / "package.json"
        if not manifest_path.exists():
            continue
        manifest = load_json(manifest_path)
        if manifest.get("name") != package_id:
            errors.append(f"{package_id}: package.json name mismatch")
        if manifest.get("version") != item.get("version"):
            errors.append(f"{package_id}: catalog/package version mismatch")
        if not manifest.get("samples"):
            errors.append(f"{package_id}: package.json must declare Samples~")

    live_package_paths = {path.parent.name for path in root.glob("com.*/package.json")}
    if live_package_paths != declared_paths:
        errors.append(
            f"catalog/live package mismatch: catalog={sorted(declared_paths)} live={sorted(live_package_paths)}"
        )

    for path in root.rglob("*"):
        if not path.is_file() or ".git" in path.parts:
            continue
        if path.suffix.lower() == ".json":
            try:
                load_json(path)
            except (json.JSONDecodeError, UnicodeDecodeError) as exc:
                errors.append(f"invalid JSON {path.relative_to(root)}: {exc}")
        if path.suffix.lower() not in TEXT_SUFFIXES:
            continue
    for package_path in declared_paths:
        package_root = root / package_path
        for source in package_root.rglob("*.cs"):
            text = source.read_text(encoding="utf-8")
            namespaces = re.findall(r"\bnamespace\s+([A-Za-z0-9_.]+)", text)
            if not namespaces or any(not value.startswith("Lingkyn.Unity.") for value in namespaces):
                errors.append(f"{source.relative_to(root)}: namespace must start with Lingkyn.Unity.")
            if not source.with_name(source.name + ".meta").exists():
                errors.append(f"{source.relative_to(root)}: missing .meta")
        for asmdef in package_root.rglob("*.asmdef"):
            payload = load_json(asmdef)
            if not str(payload.get("name", "")).startswith("Lingkyn."):
                errors.append(f"{asmdef.relative_to(root)}: asmdef name must start with Lingkyn.")
            if not asmdef.with_name(asmdef.name + ".meta").exists():
                errors.append(f"{asmdef.relative_to(root)}: missing .meta")
    return errors


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=ROOT)
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    errors = validate_repository(args.root.resolve())
    report = {
        "schema": "xr-foundry.repository_validation.v1",
        "root": str(args.root.resolve()),
        "status": "pass" if not errors else "fail",
        "errors": errors,
    }
    print(json.dumps(report, indent=2))
    return 0 if not errors else 1


if __name__ == "__main__":
    raise SystemExit(main())
