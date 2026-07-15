from __future__ import annotations

import argparse
import json
import re
from datetime import datetime
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
INVENTORY_XR_DEVICE_RECEIPT_TEMPLATE = (
    Path("docs") / "validation" / "inventory-xr-device-receipt.template.json"
)
INVENTORY_XR_DEVICE_RECEIPT_GUIDE = (
    Path("docs") / "validation" / "inventory-xr-device-receipt-template.md"
)
REQUIRED_INVENTORY_XR_DEVICE_CHECKS = {
    "apk_install",
    "sample_open",
    "binocular_readability",
    "world_fixed_head_turn",
    "world_fixed_lateral_lean",
    "left_controller_lcr_hover",
    "left_controller_lcr_press",
    "right_controller_lcr_hover",
    "right_controller_lcr_press",
    "target_isolation",
    "hover_state_visible",
    "selected_state_visible",
    "disabled_state_visible",
    "disabled_no_mutation",
    "scene_occlusion",
    "panel_scale",
    "panel_angle",
    "controller_reach",
    "comfort_two_minutes",
}
OPTIONAL_INVENTORY_XR_DEVICE_CHECKS = {"direct_poke_device"}
DEVICE_CHECK_RESULTS = {"pass", "partial", "fail", "not_tested"}
INVENTORY_XR_DEVICE_CLAIM_BOUNDARIES = {
    "device_gate_passed",
    "headset_usability_claim_allowed",
    "controller_ray_claim_allowed",
    "direct_poke_device_claim_allowed",
}


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


def validate_inventory_xr_device_receipt(
    payload: Any, *, require_pass: bool
) -> list[str]:
    errors: list[str] = []
    if not isinstance(payload, dict):
        return ["Inventory XR device receipt must be a JSON object"]
    if payload.get("schema") != "xr-foundry.inventory_xr_device_receipt.v1":
        errors.append("Inventory XR device receipt schema is invalid")
    if payload.get("validation_profile") != "pico_tracked_controller_v1":
        errors.append("Inventory XR device receipt must use pico_tracked_controller_v1")

    serialized = json.dumps(payload, ensure_ascii=False)
    if re.search(r"\b[A-Za-z]:\\", serialized):
        errors.append("Inventory XR device receipt must not contain a machine-local Windows path")
    lowered = serialized.casefold()
    for marker in forbidden_public_markers():
        if marker in lowered:
            errors.append("Inventory XR device receipt contains a non-public marker")
            break

    required_text_fields = {
        "receipt_id": payload.get("receipt_id"),
    }
    package = payload.get("package")
    artifact = payload.get("artifact")
    software = payload.get("software")
    device = payload.get("device")
    execution = payload.get("execution")
    claim_boundary = payload.get("claim_boundary")
    for name, value in {
        "package": package,
        "artifact": artifact,
        "software": software,
        "device": device,
        "execution": execution,
        "claim_boundary": claim_boundary,
    }.items():
        if not isinstance(value, dict):
            errors.append(f"Inventory XR device receipt {name} must be an object")

    if isinstance(package, dict):
        if package.get("id") != "com.lingkyn.inventory.xr":
            errors.append("Inventory XR device receipt package id is invalid")
        required_text_fields.update(
            {
                "package.version": package.get("version"),
                "package.revision": package.get("revision"),
            }
        )
    if isinstance(artifact, dict):
        required_text_fields.update(
            {
                "artifact.apk_sha256": artifact.get("apk_sha256"),
                "artifact.artifact_ref": artifact.get("artifact_ref"),
                "artifact.application_id": artifact.get("application_id"),
            }
        )
    if isinstance(software, dict):
        for key in (
            "unity_version",
            "xri_version",
            "xr_management_version",
            "openxr_version",
            "pico_integration_version",
            "runtime_provider",
            "android_target_api",
            "graphics_api",
        ):
            required_text_fields[f"software.{key}"] = software.get(key)
    if isinstance(device, dict):
        for key in (
            "manufacturer",
            "model",
            "os_version",
            "firmware_version",
            "controller_input_mode",
        ):
            required_text_fields[f"device.{key}"] = device.get(key)
        if str(device.get("manufacturer", "")).casefold() != "pico":
            errors.append("pico_tracked_controller_v1 requires a named PICO device")
        if device.get("controller_input_mode") != "tracked_controllers":
            errors.append("pico_tracked_controller_v1 requires tracked_controllers")
    if isinstance(execution, dict):
        for key in ("tested_at_utc", "tester", "sample_scene", "posture"):
            required_text_fields[f"execution.{key}"] = execution.get(key)
        if execution.get("install_result") not in DEVICE_CHECK_RESULTS:
            errors.append("Inventory XR install_result is invalid")
        if execution.get("open_result") not in DEVICE_CHECK_RESULTS:
            errors.append("Inventory XR open_result is invalid")
        duration = execution.get("duration_seconds")
        if not isinstance(duration, int) or duration < 0:
            errors.append("Inventory XR duration_seconds must be a non-negative integer")
    if isinstance(claim_boundary, dict):
        if set(claim_boundary) != INVENTORY_XR_DEVICE_CLAIM_BOUNDARIES:
            errors.append("Inventory XR device receipt claim boundaries are incomplete")
        for key in INVENTORY_XR_DEVICE_CLAIM_BOUNDARIES:
            if not isinstance(claim_boundary.get(key), bool):
                errors.append(f"Inventory XR claim_boundary.{key} must be boolean")

    for field, value in required_text_fields.items():
        if not isinstance(value, str) or not value.strip():
            errors.append(f"Inventory XR device receipt requires {field}")

    checks = payload.get("checks")
    check_by_id: dict[str, dict[str, Any]] = {}
    if not isinstance(checks, list):
        errors.append("Inventory XR device receipt checks must be a list")
    else:
        for item in checks:
            if not isinstance(item, dict):
                errors.append("Inventory XR device checks must be objects")
                continue
            check_id = str(item.get("id", ""))
            if not check_id or check_id in check_by_id:
                errors.append(f"Inventory XR device check id is missing or duplicated: {check_id}")
                continue
            check_by_id[check_id] = item
            if item.get("status") not in DEVICE_CHECK_RESULTS:
                errors.append(f"Inventory XR device check status is invalid: {check_id}")
            if not isinstance(item.get("observation"), str):
                errors.append(f"Inventory XR device check observation must be text: {check_id}")
            if not isinstance(item.get("evidence_refs"), list):
                errors.append(f"Inventory XR device check evidence_refs must be a list: {check_id}")
    missing_checks = REQUIRED_INVENTORY_XR_DEVICE_CHECKS - set(check_by_id)
    if missing_checks:
        errors.append(f"Inventory XR device receipt is missing required checks: {sorted(missing_checks)}")
    missing_optional = OPTIONAL_INVENTORY_XR_DEVICE_CHECKS - set(check_by_id)
    if missing_optional:
        errors.append(f"Inventory XR device receipt must explicitly classify optional checks: {sorted(missing_optional)}")

    if payload.get("overall_result") not in {"pass", "partial", "fail", "not_run"}:
        errors.append("Inventory XR device receipt overall_result is invalid")
    if not isinstance(payload.get("failures_and_follow_up"), list):
        errors.append("Inventory XR device receipt failures_and_follow_up must be a list")

    if not require_pass:
        return errors

    if any(
        isinstance(value, str) and "replace-with" in value.casefold()
        for value in required_text_fields.values()
    ):
        errors.append("Completed Inventory XR device receipt contains placeholder text")
    revision = str(package.get("revision", "")) if isinstance(package, dict) else ""
    if not re.fullmatch(r"[0-9a-fA-F]{40}", revision) or set(revision) == {"0"}:
        errors.append("Completed Inventory XR package revision must be a non-zero full Git SHA")
    apk_sha = str(artifact.get("apk_sha256", "")) if isinstance(artifact, dict) else ""
    if not re.fullmatch(r"[0-9a-fA-F]{64}", apk_sha) or set(apk_sha) == {"0"}:
        errors.append("Completed Inventory XR APK SHA-256 must be a non-zero 64-character hash")

    if isinstance(execution, dict):
        if execution.get("install_result") != "pass":
            errors.append("Completed Inventory XR receipt requires install_result=pass")
        if execution.get("open_result") != "pass":
            errors.append("Completed Inventory XR receipt requires open_result=pass")
        if not isinstance(execution.get("duration_seconds"), int) or execution.get("duration_seconds", 0) < 120:
            errors.append("Completed Inventory XR device session must last at least 120 seconds")
        if execution.get("posture") not in {"seated", "standing", "seated_then_standing"}:
            errors.append("Completed Inventory XR posture must name the tested posture")
        timestamp = str(execution.get("tested_at_utc", ""))
        try:
            parsed_timestamp = datetime.fromisoformat(timestamp.replace("Z", "+00:00"))
            if parsed_timestamp.year <= 1970 or parsed_timestamp.tzinfo is None:
                raise ValueError
        except ValueError:
            errors.append("Completed Inventory XR tested_at_utc must be a real ISO-8601 timestamp")

    for check_id in sorted(REQUIRED_INVENTORY_XR_DEVICE_CHECKS):
        item = check_by_id.get(check_id, {})
        if item.get("status") != "pass":
            errors.append(f"Completed Inventory XR required check did not pass: {check_id}")
        if not str(item.get("observation", "")).strip():
            errors.append(f"Completed Inventory XR required check lacks an observation: {check_id}")
        evidence_refs = item.get("evidence_refs")
        if not isinstance(evidence_refs, list) or not evidence_refs or any(
            not isinstance(reference, str) or not reference.strip() for reference in evidence_refs
        ):
            errors.append(f"Completed Inventory XR required check lacks evidence: {check_id}")

    if payload.get("overall_result") != "pass":
        errors.append("Completed Inventory XR receipt requires overall_result=pass")
    if isinstance(claim_boundary, dict):
        for key in INVENTORY_XR_DEVICE_CLAIM_BOUNDARIES - {"direct_poke_device_claim_allowed"}:
            if claim_boundary.get(key) is not True:
                errors.append(f"Completed Inventory XR receipt requires claim_boundary.{key}=true")
        direct_poke_claim = claim_boundary.get("direct_poke_device_claim_allowed")
        if not isinstance(direct_poke_claim, bool):
            errors.append("Inventory XR direct-poke claim boundary must be boolean")
        elif direct_poke_claim:
            direct_poke = check_by_id.get("direct_poke_device", {})
            if direct_poke.get("status") != "pass":
                errors.append("Direct-poke device claim requires direct_poke_device=pass")
            if not str(direct_poke.get("observation", "")).strip() or not direct_poke.get("evidence_refs"):
                errors.append("Direct-poke device claim requires observation and evidence")
    return errors


def validate_inventory_xr_device_receipt_contract(root: Path) -> list[str]:
    errors: list[str] = []
    template_path = root / INVENTORY_XR_DEVICE_RECEIPT_TEMPLATE
    guide_path = root / INVENTORY_XR_DEVICE_RECEIPT_GUIDE
    if not template_path.exists():
        errors.append("Inventory XR device receipt JSON template is missing")
    else:
        try:
            template = load_json(template_path)
        except (json.JSONDecodeError, UnicodeDecodeError) as exc:
            errors.append(f"Inventory XR device receipt template is invalid JSON: {exc}")
        else:
            errors.extend(validate_inventory_xr_device_receipt(template, require_pass=False))
            template_checks = {
                str(item.get("id", "")): item
                for item in template.get("checks", [])
                if isinstance(item, dict)
            }
            for check_id in REQUIRED_INVENTORY_XR_DEVICE_CHECKS | OPTIONAL_INVENTORY_XR_DEVICE_CHECKS:
                if template_checks.get(check_id, {}).get("status") != "not_tested":
                    errors.append(f"Inventory XR template must start {check_id} as not_tested")
            if template.get("overall_result") != "not_run":
                errors.append("Inventory XR template must start with overall_result=not_run")
            claims = template.get("claim_boundary", {})
            if not isinstance(claims, dict) or any(value is not False for value in claims.values()):
                errors.append("Inventory XR template must start with every claim boundary false")
    if not guide_path.exists():
        errors.append("Inventory XR device receipt guide is missing")
    else:
        guide = guide_path.read_text(encoding="utf-8")
        required_guide_tokens = {
            "pico_tracked_controller_v1",
            "--device-receipt",
            "partial",
            "not_tested",
            "direct_poke_device_claim_allowed",
            "at least 120 seconds",
        }
        for token in required_guide_tokens:
            if token not in guide:
                errors.append(f"Inventory XR device receipt guide lacks required token: {token}")
    return errors


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
    manifest_path = package_root / "package.json"
    if manifest_path.exists():
        dependencies = load_json(manifest_path).get("dependencies", {})
        for package_id in dependencies:
            if not str(package_id).startswith("com.lingkyn."):
                continue
            dependency_root = package_root.parent / str(package_id)
            for source in dependency_root.rglob("*.cs") if dependency_root.exists() else []:
                declarations.update(re.findall(r"\bnamespace\s+(Lingkyn\.[A-Za-z0-9_.]+)", source.read_text(encoding="utf-8")))
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
    family_reference = reference_entries.get("inventory-package-family-standard")
    if not family_reference:
        errors.append("Inventory family reference catalog entry must exist")
        return errors

    package_family = {
        str(item.get("id", "")): item
        for item in standard.get("package_family", [])
        if isinstance(item, dict)
    }
    layer_references = {
        "com.lingkyn.inventory.core": "unity-inventory-core",
        "com.lingkyn.inventory.unity": "unity-inventory-authoring",
        "com.lingkyn.inventory.ugui": "unity-inventory-ugui",
        "com.lingkyn.inventory.xr": "unity-inventory-xr",
    }
    roadmap_text = roadmap_path.read_text(encoding="utf-8")
    for package_id, reference_id in layer_references.items():
        package = package_entries.get(package_id)
        if package is None:
            continue
        package_reference = reference_entries.get(reference_id)
        package_standard = package_family.get(package_id)
        label = package_id.removeprefix("com.lingkyn.inventory.").replace(".", " ").title()
        if package_reference is None or package_standard is None:
            errors.append(f"Inventory {label} package, standard, and reference entries must all exist")
            continue
        if package_reference.get("maturity") != package.get("maturity"):
            errors.append(f"Inventory {label} maturity must agree across package and reference catalogs")

        promotion = package.get("promotion")
        if not isinstance(promotion, dict):
            errors.append(f"Inventory {label} must declare machine-readable promotion state")
            continue
        errors.extend(validate_package_promotion(package_id, promotion))
        expected_status = "implemented_candidate" if package.get("maturity") == "candidate" else "implemented_incubating"
        if package_standard.get("implementation_status") != expected_status:
            errors.append(f"Inventory {label} standard must be represented as {expected_status}")
        if package_standard.get("earliest_failed_gate") != promotion.get("earliest_failed_gate"):
            errors.append(f"Inventory {label} earliest failed gate must agree across standard and package catalog")

        projection_row = (
            f"| `{package_id}` | `{package.get('version')}` | `{package.get('maturity')}` | "
            f"`{promotion.get('earliest_failed_gate')}` |"
        )
        if projection_row not in roadmap_text:
            errors.append(f"ROADMAP.md: stale or missing projection row for {package_id}")

    core = package_entries.get("com.lingkyn.inventory.core")
    core_standard = package_family.get("com.lingkyn.inventory.core", {})
    if core and standard.get("core_implementation_admitted") is True:
        stale_surfaces = {
            "ROADMAP.md": roadmap_text,
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

    xr_standard = package_family.get("com.lingkyn.inventory.xr", {})
    if xr_standard.get("implementation_status") in {"implemented_incubating", "implemented_candidate"}:
        xr_surfaces = {
            "README.md": (root / "README.md").read_text(encoding="utf-8"),
            "ROADMAP.md": roadmap_text,
            "docs/standards/inventory/README.md": standard_readme_path.read_text(encoding="utf-8"),
            "reference-catalog.json": json.dumps(reference, ensure_ascii=False),
        }
        stale_xr_markers = [
            "XR is still not implemented",
            "XR remains pending",
            "XR pending**",
        ]
        for surface, text in xr_surfaces.items():
            for marker in stale_xr_markers:
                if marker in text:
                    errors.append(f"{surface}: stale Inventory XR implementation claim: {marker}")
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


def validate_package_promotion(package_id: str, promotion: Any) -> list[str]:
    errors: list[str] = []
    if promotion is None:
        return errors
    if not isinstance(promotion, dict):
        return [f"{package_id}: promotion state must be an object"]
    status = promotion.get("candidate_status")
    earliest = str(promotion.get("earliest_failed_gate", "")).strip()
    satisfied = promotion.get("satisfied")
    pending = promotion.get("pending")
    if status not in {"blocked", "eligible", "passed"}:
        errors.append(f"{package_id}: candidate promotion status is invalid")
    if not earliest:
        errors.append(f"{package_id}: earliest failed gate or 'none' is required")
    if not isinstance(satisfied, list) or not satisfied:
        errors.append(f"{package_id}: satisfied promotion gates are required")
    if not isinstance(pending, list):
        errors.append(f"{package_id}: pending promotion gates must be a list")
    elif status == "passed" and (earliest != "none" or pending):
        errors.append(f"{package_id}: passed candidate must have no failed or pending gate")
    elif status == "blocked" and (earliest == "none" or not pending):
        errors.append(f"{package_id}: blocked candidate must name failed and pending gates")
    return errors


def validate_repository(root: Path) -> list[str]:
    errors: list[str] = scan_text_safety(root)
    errors.extend(validate_ignore_scope(root))
    errors.extend(validate_inventory_standard(root))
    errors.extend(validate_inventory_xr_device_receipt_contract(root))
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
        errors.extend(validate_package_promotion(package_id, item.get("promotion")))
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
    parser.add_argument(
        "--device-receipt",
        type=Path,
        help="also require a completed Inventory XR device receipt to pass",
    )
    args = parser.parse_args()
    root = args.root.resolve()
    errors = validate_repository(root)
    device_receipt_path: Path | None = None
    if args.device_receipt is not None:
        device_receipt_path = args.device_receipt
        if not device_receipt_path.is_absolute():
            device_receipt_path = root / device_receipt_path
        device_receipt_path = device_receipt_path.resolve()
        if not device_receipt_path.exists():
            errors.append(f"Inventory XR device receipt does not exist: {device_receipt_path}")
        else:
            try:
                receipt = load_json(device_receipt_path)
            except (json.JSONDecodeError, UnicodeDecodeError) as exc:
                errors.append(f"Inventory XR device receipt is invalid JSON: {exc}")
            else:
                errors.extend(validate_inventory_xr_device_receipt(receipt, require_pass=True))
    report = {
        "schema": "xr-foundry.repository_validation.v1",
        "root": str(root),
        "status": "pass" if not errors else "fail",
        "errors": errors,
    }
    if device_receipt_path is not None:
        report["device_receipt"] = str(device_receipt_path)
    print(json.dumps(report, indent=2))
    return 0 if not errors else 1


if __name__ == "__main__":
    raise SystemExit(main())
