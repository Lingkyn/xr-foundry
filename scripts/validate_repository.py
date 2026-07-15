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
REQUIRED_INVENTORY_STANDARD_FILES = {
    "README.md",
    "architecture-contract.md",
    "coverage-matrix.md",
    "inventory-standard.json",
    "source-manifest.json",
    "verification-contract.md",
    "core-api-contract.md",
    "core-api-baseline.json",
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


def validate_ignore_scope(root: Path) -> list[str]:
    errors: list[str] = []
    ignore_path = root / ".gitignore"
    if not ignore_path.exists():
        return ["missing .gitignore"]
    unity_root_directories = {"Library/", "Temp/", "Logs/", "obj/", "Build/", "Builds/"}
    for raw_line in ignore_path.read_text(encoding="utf-8").splitlines():
        line = raw_line.strip()
        if line in unity_root_directories:
            errors.append(f"Unity generated-directory ignore must be root-anchored: {line}")
    return errors


def validate_internal_namespace_links(package_root: Path) -> list[str]:
    declarations: set[str] = set()
    imports: list[tuple[Path, str]] = []
    for source in package_root.rglob("*.cs"):
        text = source.read_text(encoding="utf-8")
        declarations.update(re.findall(r"\bnamespace\s+(Lingkyn\.[A-Za-z0-9_.]+)", text))
        imports.extend(
            (source, value)
            for value in re.findall(r"\busing\s+(?:static\s+)?(Lingkyn\.[A-Za-z0-9_.]+)\s*;", text)
        )
    errors: list[str] = []
    for source, imported in imports:
        if not any(namespace == imported or namespace.startswith(imported + ".") for namespace in declarations):
            errors.append(f"{source.relative_to(package_root)}: internal namespace has no source declaration: {imported}")
    return errors


def validate_inventory_source_manifest(path: Path) -> list[str]:
    errors: list[str] = []
    if not path.exists():
        return ["Inventory standard source manifest is missing"]
    payload = load_json(path)
    if payload.get("schema") != "xr-foundry.inventory_source_manifest.v1":
        errors.append("Inventory source manifest schema is invalid")
    if payload.get("derivation_policy") != "admitted_positive_external_sources_only":
        errors.append("Inventory derivation policy must allow admitted positive external sources only")
    if payload.get("consumer_material_allowed") is not False:
        errors.append("Inventory source manifest must reject consumer material")
    if payload.get("screened_out_material_allowed") is not False:
        errors.append("Inventory source manifest must reject screened-out material")
    if payload.get("implementation_policy") != "independently_authored_from_public_contracts":
        errors.append("Inventory implementation policy must require independent authorship")
    required_forbidden_scopes = {
        "consumer_project",
        "course_project",
        "internal_prototype",
        "screened_out_candidate",
    }
    if set(payload.get("forbidden_source_scopes", [])) != required_forbidden_scopes:
        errors.append("Inventory source manifest must forbid all non-standard source scopes")

    allowed_authority_classes = {
        "official_normative",
        "official_professional_guidance",
        "official_first_party_system",
        "official_first_party_reference",
        "professional_product_benchmark",
        "maintained_open_source_implementation",
    }
    ids: set[str] = set()
    sources = payload.get("sources", [])
    if not sources:
        errors.append("Inventory source manifest must contain admitted sources")
    for source in sources:
        if not isinstance(source, dict):
            errors.append("Inventory sources must be objects")
            continue
        source_id = str(source.get("id", ""))
        if not source_id or source_id in ids:
            errors.append(f"Inventory source id is missing or duplicated: {source_id}")
        ids.add(source_id)
        if source.get("admission") != "admitted_positive":
            errors.append(f"Inventory derivation source is not an admitted positive source: {source_id}")
        if source.get("provenance_scope") != "external_public":
            errors.append(f"Inventory source is not external public evidence: {source_id}")
        if source.get("code_seed_allowed") is not False:
            errors.append(f"Inventory first-round source cannot be used as a code seed: {source_id}")
        if source.get("authority_class") not in allowed_authority_classes:
            errors.append(f"Inventory source has a non-admitted authority class: {source_id}")
        if not str(source.get("url", "")).startswith("https://"):
            errors.append(f"Inventory source must use a public HTTPS URL: {source_id}")
        if not source.get("positive_evidence"):
            errors.append(f"Inventory source must state positive evidence: {source_id}")
        if not source.get("publisher") or not source.get("title") or not source.get("source_role"):
            errors.append(f"Inventory source must state publisher, title, and source role: {source_id}")
        if not source.get("limits"):
            errors.append(f"Inventory source must state limits: {source_id}")
        if not source.get("license_boundary"):
            errors.append(f"Inventory source must state a license boundary: {source_id}")
    return errors


def validate_inventory_standard(root: Path) -> list[str]:
    errors: list[str] = []
    standard_root = root / "docs" / "standards" / "inventory"
    for name in sorted(REQUIRED_INVENTORY_STANDARD_FILES):
        if not (standard_root / name).exists():
            errors.append(f"Inventory standard is missing {name}")
    errors.extend(validate_inventory_source_manifest(standard_root / "source-manifest.json"))
    contract_path = standard_root / "inventory-standard.json"
    if contract_path.exists():
        contract = load_json(contract_path)
        if contract.get("schema") != "xr-foundry.inventory_standard.v1":
            errors.append("Inventory standard schema is invalid")
        isolation = contract.get("source_isolation", {})
        if isolation.get("positive_external_sources_only") is not True:
            errors.append("Inventory standard must require positive external sources only")
        if isolation.get("consumer_projects_are_generation_inputs") is not False:
            errors.append("Inventory standard must exclude consumer projects from generation inputs")
        if isolation.get("screened_out_candidates_are_generation_inputs") is not False:
            errors.append("Inventory standard must exclude screened-out candidates from generation inputs")
        manifest_path = standard_root / "source-manifest.json"
        source_ids = {
            str(item.get("id", ""))
            for item in load_json(manifest_path).get("sources", [])
            if isinstance(item, dict)
        } if manifest_path.exists() else set()
        required_capabilities = {
            "definition_instance_identity",
            "inventory_equipment_separation",
            "containers_stacks_and_policies",
            "persistence_and_authority",
            "presentation_separation_and_composition",
            "package_and_test_contract",
            "xr_world_space_interaction",
        }
        coverage = contract.get("evidence_coverage", [])
        covered_capabilities = {
            str(item.get("capability", ""))
            for item in coverage
            if isinstance(item, dict)
        }
        if covered_capabilities != required_capabilities:
            errors.append("Inventory evidence coverage is incomplete")
        for item in coverage:
            if not isinstance(item, dict):
                errors.append("Inventory evidence coverage entries must be objects")
                continue
            references = set(item.get("source_ids", []))
            if len(references) < 2:
                errors.append(f"Inventory capability lacks convergent evidence: {item.get('capability', '')}")
            unknown = references - source_ids
            if unknown:
                errors.append(f"Inventory capability references unknown sources: {sorted(unknown)}")
        packages = contract.get("package_family", [])
        package_ids = [str(item.get("id", "")) for item in packages if isinstance(item, dict)]
        required_package_ids = {
            "com.lingkyn.inventory.core",
            "com.lingkyn.inventory.unity",
            "com.lingkyn.inventory.ugui",
            "com.lingkyn.inventory.xr",
        }
        if set(package_ids) != required_package_ids:
            errors.append("Inventory package family boundaries are incomplete")
    return errors


def validate_inventory_projection_coherence(root: Path) -> list[str]:
    errors: list[str] = []
    catalog_path = root / "package-catalog.json"
    reference_path = root / "reference-catalog.json"
    standard_path = root / "docs" / "standards" / "inventory" / "inventory-standard.json"
    standard_readme_path = root / "docs" / "standards" / "inventory" / "README.md"
    roadmap_path = root / "ROADMAP.md"
    required = [catalog_path, reference_path, standard_path, standard_readme_path, roadmap_path]
    if any(not path.exists() for path in required):
        return errors

    catalog = load_json(catalog_path)
    reference = load_json(reference_path)
    standard = load_json(standard_path)
    package_entries = {
        str(item.get("id", "")): item
        for item in catalog.get("packages", [])
        if isinstance(item, dict)
    }
    reference_entries = {
        str(item.get("id", "")): item
        for item in reference.get("artifacts", [])
        if isinstance(item, dict)
    }
    core = package_entries.get("com.lingkyn.inventory.core")
    core_reference = reference_entries.get("unity-inventory-core")
    family_reference = reference_entries.get("inventory-package-family-standard")
    if not core or not core_reference or not family_reference:
        errors.append("Inventory package and reference catalog entries must exist")
        return errors

    if core_reference.get("maturity") != core.get("maturity"):
        errors.append("Inventory Core maturity must agree across package and reference catalogs")

    promotion = core.get("promotion")
    if not isinstance(promotion, dict):
        errors.append("Inventory Core must declare machine-readable promotion state")
    else:
        if promotion.get("candidate_status") not in {"blocked", "eligible", "passed"}:
            errors.append("Inventory Core candidate promotion status is invalid")
        if not str(promotion.get("earliest_failed_gate", "")).strip():
            errors.append("Inventory Core must name its earliest failed promotion gate")
        if not promotion.get("satisfied") or not promotion.get("pending"):
            errors.append("Inventory Core promotion state must name satisfied and pending gates")

    package_family = {
        str(item.get("id", "")): item
        for item in standard.get("package_family", [])
        if isinstance(item, dict)
    }
    core_standard = package_family.get("com.lingkyn.inventory.core", {})
    if standard.get("core_implementation_admitted") is True:
        if core_standard.get("implementation_status") != "implemented_incubating":
            errors.append("Admitted Inventory Core must be represented as implemented_incubating")
        stale_surfaces = {
            "ROADMAP.md": roadmap_path.read_text(encoding="utf-8"),
            "docs/standards/inventory/README.md": standard_readme_path.read_text(encoding="utf-8"),
            "reference-catalog.json": json.dumps(reference, ensure_ascii=False),
        }
        stale_markers = [
            "Implementation remains unadmitted",
            "Implementation status: **not yet admitted**",
            "No Inventory implementation is admitted yet",
        ]
        for surface, text in stale_surfaces.items():
            for marker in stale_markers:
                if marker in text:
                    errors.append(f"{surface}: stale Inventory implementation claim: {marker}")

    if core_standard.get("earliest_failed_gate") != (
        promotion.get("earliest_failed_gate") if isinstance(promotion, dict) else None
    ):
        errors.append("Inventory Core earliest failed gate must agree across standard and package catalog")
    return errors


def validate_inventory_api_baseline(root: Path) -> list[str]:
    errors: list[str] = []
    baseline_path = root / "docs" / "standards" / "inventory" / "core-api-baseline.json"
    manifest_path = root / "com.lingkyn.inventory.core" / "package.json"
    runtime_root = root / "com.lingkyn.inventory.core" / "Runtime"
    if not baseline_path.exists() or not manifest_path.exists() or not runtime_root.exists():
        return errors

    baseline = load_json(baseline_path)
    manifest = load_json(manifest_path)
    if baseline.get("schema") != "xr-foundry.inventory_core_api_baseline.v1":
        errors.append("Inventory Core API baseline schema is invalid")
    if baseline.get("package_id") != manifest.get("name"):
        errors.append("Inventory Core API baseline package id must match package.json")
    if baseline.get("version") != manifest.get("version"):
        errors.append("Inventory Core API baseline version must match package.json")

    declared: set[str] = set()
    declaration = re.compile(
        r"\bpublic\s+(?:(?:sealed|static|abstract)\s+class|readonly\s+struct|enum|interface)\s+([A-Za-z0-9_]+)"
    )
    for source in runtime_root.glob("*.cs"):
        declared.update(declaration.findall(source.read_text(encoding="utf-8")))
    expected = baseline.get("public_types", [])
    if len(expected) != len(set(expected)):
        errors.append("Inventory Core API baseline contains duplicate public types")
    if set(expected) != declared:
        errors.append(
            "Inventory Core API public type baseline mismatch: "
            f"baseline={sorted(expected)} runtime={sorted(declared)}"
        )

    policy = baseline.get("compatibility_review", {})
    required_true = {
        "persistence_identifiers_are_contracts",
        "enum_values_append_only",
        "removal_requires_deprecation_or_documented_exception",
        "first_candidate_requires_upgrade_and_rollback_receipt",
    }
    for key in required_true:
        if policy.get(key) is not True:
            errors.append(f"Inventory Core API compatibility policy is incomplete: {key}")
    if policy.get("runtime_engine_dependencies") != []:
        errors.append("Inventory Core API baseline must declare no runtime engine dependencies")
    return errors


def validate_repository(root: Path) -> list[str]:
    errors: list[str] = scan_text_safety(root)
    errors.extend(validate_ignore_scope(root))
    errors.extend(validate_inventory_standard(root))
    errors.extend(validate_inventory_projection_coherence(root))
    errors.extend(validate_inventory_api_baseline(root))
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
        package_artifact_paths = {
            str(item.get("path", ""))
            for item in reference_catalog.get("artifacts", [])
            if isinstance(item, dict) and item.get("package_id")
        }
        package_paths = {
            str(item.get("path", ""))
            for item in catalog.get("packages", [])
            if isinstance(item, dict)
        }
        if package_artifact_paths != package_paths:
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
        errors.extend(validate_internal_namespace_links(package_root))
        for source in package_root.rglob("*.cs"):
            text = source.read_text(encoding="utf-8")
            namespaces = re.findall(r"\bnamespace\s+([A-Za-z0-9_.]+)", text)
            if not namespaces or any(not value.startswith("Lingkyn.") for value in namespaces):
                errors.append(f"{source.relative_to(root)}: namespace must start with Lingkyn.")
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
