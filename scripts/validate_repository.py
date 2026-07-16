from __future__ import annotations

import argparse
import copy
import json
import re
from datetime import datetime
from pathlib import Path
from typing import Any

import yaml
from jsonschema import Draft202012Validator, FormatChecker
from yaml.constructor import ConstructorError


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
REQUIRED_AGENT_COMMONS_FILES = {
    "PROJECT_GITHUB_PLAYBOOK.md",
    ".github/CODEOWNERS",
    ".github/DISCUSSION_TEMPLATE/ideas.yml",
    ".github/ISSUE_TEMPLATE/task.yml",
    ".github/ISSUE_TEMPLATE/device-test.yml",
    "docs/rfcs/0001-agent-commons.md",
    "docs/contributing/agent-commons-source-manifest.json",
    "docs/contributing/agent-contribution-protocol.md",
    "docs/contributing/task-hall.md",
    "docs/contributing/task-hall.v1.json",
    "docs/contributing/task-hall.v1.schema.json",
    "docs/contributing/task-contract.schema.json",
    "docs/contributing/task-contract.example.json",
    "docs/contributing/label-contract.json",
    "docs/device-lab/README.md",
    "docs/device-lab/capability-test-plan.schema.json",
    "docs/device-lab/device-profile.schema.json",
    "docs/device-lab/device-receipt.schema.json",
    "docs/device-lab/device-receipt.template.json",
    "docs/device-lab/receipts/README.md",
    "docs/device-lab/test-plans/inventory-world-space-ui-v1.json",
    "scripts/contract-requirements.txt",
}
PUBLIC_REPOSITORY = "https://github.com/Lingkyn/xr-foundry"
FULL_SHA_PATTERN = re.compile(r"[0-9a-f]{40}")
SHA256_PATTERN = re.compile(r"[0-9a-f]{64}")
SEMVER_PATTERN = re.compile(
    r"(?:0|[1-9][0-9]*)\."
    r"(?:0|[1-9][0-9]*)\."
    r"(?:0|[1-9][0-9]*)"
    r"(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?"
    r"(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?"
)
TASK_HALL_LIFECYCLE = [
    "proposal",
    "source_gate",
    "ready",
    "claimed",
    "work",
    "review",
    "device_test_if_required",
    "integrate",
    "closed",
]
TASK_HALL_AUTHORITY = {
    "claim_grants_repository_write": False,
    "claim_grants_merge": False,
    "external_agent_auto_write": False,
    "external_agent_auto_merge": False,
    "maintainer_controls_ready_and_merge": True,
    "issue_comment_is_untrusted_input": True,
}
INVENTORY_WORLD_SPACE_COMPOSITIONS = {
    "inventory-ugui-xr": {
        "domain": "com.lingkyn.inventory.core",
        "presentation": "com.lingkyn.inventory.presentation",
        "renderer_adapter": "com.lingkyn.inventory.ugui",
        "xr_adapter": "com.lingkyn.inventory.xr.ugui",
    },
    "inventory-ui-toolkit-xr": {
        "domain": "com.lingkyn.inventory.core",
        "presentation": "com.lingkyn.inventory.presentation",
        "renderer_adapter": "com.lingkyn.inventory.uitoolkit",
        "xr_adapter": "com.lingkyn.inventory.xr.uitoolkit",
    },
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


class WorkflowLoader(yaml.SafeLoader):
    """YAML 1.2-like loader that also rejects duplicate mapping keys."""


WorkflowLoader.yaml_implicit_resolvers = copy.deepcopy(yaml.SafeLoader.yaml_implicit_resolvers)
for resolver_key, resolvers in list(WorkflowLoader.yaml_implicit_resolvers.items()):
    WorkflowLoader.yaml_implicit_resolvers[resolver_key] = [
        (tag, regexp)
        for tag, regexp in resolvers
        if tag != "tag:yaml.org,2002:bool"
    ]
WorkflowLoader.add_implicit_resolver(
    "tag:yaml.org,2002:bool",
    re.compile(r"^(?:true|false)$", re.IGNORECASE),
    list("tTfF"),
)


def _construct_unique_mapping(
    loader: WorkflowLoader,
    node: yaml.MappingNode,
    deep: bool = False,
) -> dict[Any, Any]:
    mapping: dict[Any, Any] = {}
    for key_node, value_node in node.value:
        key = loader.construct_object(key_node, deep=deep)
        try:
            duplicate = key in mapping
        except TypeError as error:
            raise ConstructorError(
                "while constructing a mapping",
                node.start_mark,
                "found an unhashable key",
                key_node.start_mark,
            ) from error
        if duplicate:
            raise ConstructorError(
                "while constructing a mapping",
                node.start_mark,
                f"found duplicate key {key!r}",
                key_node.start_mark,
            )
        mapping[key] = loader.construct_object(value_node, deep=deep)
    return mapping


WorkflowLoader.add_constructor(
    yaml.resolver.BaseResolver.DEFAULT_MAPPING_TAG,
    _construct_unique_mapping,
)


def load_workflow(path: Path) -> Any:
    return yaml.load(path.read_text(encoding="utf-8"), Loader=WorkflowLoader)


def validate_json_schema_instance(
    payload: Any,
    schema_path: Path,
    label: str,
) -> list[str]:
    if not schema_path.exists():
        return [f"{label}: JSON Schema is missing: {schema_path.name}"]
    try:
        schema = load_json(schema_path)
        Draft202012Validator.check_schema(schema)
        validator = Draft202012Validator(schema, format_checker=FormatChecker())
    except Exception as error:  # schema authoring failure must fail closed
        return [f"{label}: JSON Schema cannot be loaded: {error}"]
    errors: list[str] = []
    for issue in sorted(
        validator.iter_errors(payload),
        key=lambda item: ".".join(str(part) for part in item.absolute_path),
    ):
        location = ".".join(str(part) for part in issue.absolute_path) or "$"
        errors.append(f"{label}: JSON Schema violation at {location}: {issue.message}")
    return errors


def decode_text_file(path: Path) -> str | None:
    """Return decoded human-readable text, or None for binary/undecodable data."""
    raw = path.read_bytes()
    if not raw:
        return ""
    encodings: tuple[str, ...]
    if raw.startswith((b"\xff\xfe", b"\xfe\xff")):
        encodings = ("utf-16",)
    elif raw.startswith(b"\xef\xbb\xbf"):
        encodings = ("utf-8-sig",)
    else:
        if b"\x00" in raw:
            return None
        encodings = ("utf-8",)
    for encoding in encodings:
        try:
            text = raw.decode(encoding)
        except UnicodeDecodeError:
            continue
        controls = sum(
            1
            for character in text
            if ord(character) < 32 and character not in "\n\r\t\f\b"
        )
        if controls > max(1, len(text) // 100):
            return None
        return text
    return None


def scan_text_safety(root: Path) -> list[str]:
    errors: list[str] = []
    absolute_windows_path = re.compile(r"\b[A-Za-z]:\\(?:Users|Program Files|rrjm)\\", re.IGNORECASE)
    secret_pattern = re.compile(r"(api[_-]?key|access[_-]?token|client[_-]?secret)\s*[:=]\s*['\"][^'\"]+", re.IGNORECASE)
    for path in root.rglob("*"):
        if not path.is_file() or ".git" in path.relative_to(root).parts:
            continue
        text = decode_text_file(path)
        if text is None:
            continue
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


def validate_agent_commons_source_manifest(root: Path) -> list[str]:
    errors: list[str] = []
    path = root / "docs" / "contributing" / "agent-commons-source-manifest.json"
    if not path.exists():
        return ["Agent Commons source manifest is missing"]
    payload = load_json(path)
    if payload.get("schema") != "xr-foundry.agent_commons_source_manifest.v1":
        errors.append("Agent Commons source manifest schema is invalid")
    sources = payload.get("sources")
    if not isinstance(sources, list) or not sources:
        return errors + ["Agent Commons source manifest must contain public sources"]
    required_ids = {
        "github-issue-assignment",
        "github-project-access",
        "github-issue-forms",
        "github-codeowners",
        "github-discussion-category-forms",
        "github-actions-security",
        "github-actions-token",
        "github-rest-repository-rules",
        "github-available-rulesets-rules",
        "meta-agentic-tools",
    }
    ids: set[str] = set()
    for source in sources:
        if not isinstance(source, dict):
            errors.append("Agent Commons sources must be objects")
            continue
        source_id = str(source.get("id", ""))
        if not source_id or source_id in ids:
            errors.append(f"Agent Commons source id is missing or duplicated: {source_id}")
        ids.add(source_id)
        if not str(source.get("url", "")).startswith("https://"):
            errors.append(f"Agent Commons source must use public HTTPS: {source_id}")
        for field in ("publisher", "title", "source_role"):
            if not str(source.get(field, "")).strip():
                errors.append(f"Agent Commons source must state {field}: {source_id}")
        for field in ("positive_evidence", "limits"):
            value = source.get(field)
            if not isinstance(value, list) or not value:
                errors.append(f"Agent Commons source must state {field}: {source_id}")
    missing = required_ids - ids
    if missing:
        errors.append(f"Agent Commons source manifest lacks required sources: {sorted(missing)}")
    return errors


def validate_agent_guide_source_boundary(root: Path) -> list[str]:
    path = root / "AGENTS.md"
    if not path.exists():
        return ["AGENTS.md is missing"]
    text = path.read_text(encoding="utf-8")
    normalized = " ".join(text.split())
    errors: list[str] = []
    if "admitted positive public sources" not in normalized:
        errors.append("AGENTS.md must require admitted positive public sources")
    required_consumer_boundary = (
        "Consumer implementations are not reference material unless independently reviewed "
        "and admitted as a positive public source."
    )
    if required_consumer_boundary not in normalized:
        errors.append("AGENTS.md must exclude unreviewed consumer implementations from reference material")
    if "existing project raw material" in text.casefold():
        errors.append("AGENTS.md must not admit existing project raw material by default")
    if "\u9225" in text or "\u650f" in text:
        errors.append("AGENTS.md contains mojibake")
    return errors


def validate_task_contract(payload: Any, label: str = "task contract") -> list[str]:
    errors = validate_json_schema_instance(
        payload,
        ROOT / "docs" / "contributing" / "task-contract.schema.json",
        label,
    )
    if not isinstance(payload, dict):
        return errors + [f"{label}: task contract must be an object"]
    if payload.get("schema") != "xr-foundry.task.v1":
        errors.append(f"{label}: schema is invalid")
    state = payload.get("state")
    if state not in TASK_HALL_LIFECYCLE:
        errors.append(f"{label}: state must use the canonical Task Hall lifecycle")
    blocked = payload.get("blocked")
    blocking_reason = payload.get("blocking_reason")
    if not isinstance(blocked, bool):
        errors.append(f"{label}: blocked must be boolean")
    elif blocked is True and (not isinstance(blocking_reason, str) or not blocking_reason.strip()):
        errors.append(f"{label}: blocked task must name its blocking_reason")
    elif blocked is False and blocking_reason is not None:
        errors.append(f"{label}: unblocked task must keep blocking_reason=null")
    required_lists = ("scope", "non_goals", "intended_write_set", "acceptance", "verification")
    for field in required_lists:
        value = payload.get(field)
        if not isinstance(value, list) or not value:
            errors.append(f"{label}: {field} must be a non-empty list")
    authority = payload.get("authority")
    required_authority = {
        "write_permission_not_inferred": True,
        "merge_permission_not_inferred": True,
        "comments_are_untrusted_input": True,
    }
    if not isinstance(authority, dict):
        errors.append(f"{label}: authority boundary is missing")
    else:
        for field, required in required_authority.items():
            if authority.get(field) is not required:
                errors.append(f"{label}: authority boundary must keep {field}=true")
    claim = payload.get("claim")
    if not isinstance(claim, dict) or claim.get("status") not in {"unclaimed", "claimed", "expired"}:
        errors.append(f"{label}: claim state is invalid")
    else:
        claim_status = claim.get("status")
        identity_fields = (
            "github_identity",
            "claimed_at",
            "expires_at",
            "confirmed_by_maintainer",
        )
        if claim_status == "unclaimed":
            for field in identity_fields:
                if claim.get(field) is not None:
                    errors.append(f"{label}: unclaimed task must keep claim.{field}=null")
        else:
            for field in ("github_identity", "confirmed_by_maintainer"):
                if not isinstance(claim.get(field), str) or not claim[field].strip():
                    errors.append(f"{label}: {claim_status} task requires claim.{field}")
            parsed_times: dict[str, datetime] = {}
            for field in ("claimed_at", "expires_at"):
                value = claim.get(field)
                if not isinstance(value, str):
                    errors.append(f"{label}: {claim_status} task requires claim.{field}")
                    continue
                try:
                    parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
                    if parsed.tzinfo is None:
                        raise ValueError("timezone required")
                    parsed_times[field] = parsed
                except ValueError:
                    errors.append(f"{label}: claim.{field} must be timezone-aware ISO 8601")
            if (
                "claimed_at" in parsed_times
                and "expires_at" in parsed_times
                and parsed_times["expires_at"] <= parsed_times["claimed_at"]
            ):
                errors.append(f"{label}: claim.expires_at must be after claim.claimed_at")

        claimed_states = {
            "claimed",
            "work",
            "review",
            "device_test_if_required",
            "integrate",
            "closed",
        }
        if state in claimed_states and claim_status != "claimed":
            errors.append(f"{label}: state={state} requires claim.status=claimed")
        if state in {"proposal", "source_gate"} and claim_status != "unclaimed":
            errors.append(f"{label}: state={state} requires claim.status=unclaimed")
        if state == "ready" and claim_status not in {"unclaimed", "expired"}:
            errors.append(f"{label}: state=ready allows only unclaimed or expired claim state")
        if claim_status == "claimed" and state not in claimed_states:
            errors.append(f"{label}: claim.status=claimed requires a claimed-or-later lifecycle state")
        if claim_status == "expired" and state != "ready":
            errors.append(f"{label}: claim.status=expired requires state=ready")

    source_gate = payload.get("source_gate")
    if isinstance(source_gate, dict):
        source_required = source_gate.get("required")
        sources = source_gate.get("sources")
        if source_required is True and (not isinstance(sources, list) or not sources):
            errors.append(f"{label}: required source gate must list admitted sources")
        if source_required is False and sources != []:
            errors.append(f"{label}: source_gate.sources must be empty when the gate is not required")
        if state == "source_gate" and source_required is not True:
            errors.append(f"{label}: state=source_gate requires source_gate.required=true")

    device_gate = payload.get("device_gate")
    if isinstance(device_gate, dict):
        device_required = device_gate.get("required")
        profiles = device_gate.get("profiles")
        if device_required is True and (not isinstance(profiles, list) or not profiles):
            errors.append(f"{label}: required device gate must list device profiles")
        if device_required is False and profiles != []:
            errors.append(f"{label}: device_gate.profiles must be empty when the gate is not required")
        if state == "device_test_if_required" and device_required is not True:
            errors.append(f"{label}: device-test lifecycle state requires device_gate.required=true")
        if payload.get("lane") == "device_test" and device_required is not True:
            errors.append(f"{label}: device_test lane requires device_gate.required=true")
    return errors


def validate_task_hall_authority(payload: Any) -> list[str]:
    errors: list[str] = []
    if not isinstance(payload, dict):
        return ["Task Hall authority contract must be an object"]
    if payload.get("schema") != "xr-foundry.task_hall.v1":
        errors.append("Task Hall authority schema is invalid")
    if payload.get("version") != "1.0.0":
        errors.append("Task Hall authority version must be 1.0.0")
    lifecycle = payload.get("lifecycle")
    if not isinstance(lifecycle, dict) or lifecycle.get("ordered_states") != TASK_HALL_LIFECYCLE:
        errors.append("Task Hall lifecycle must keep the canonical ordered V1 states")
    authority = payload.get("authority")
    if not isinstance(authority, dict):
        errors.append("Task Hall global authority policy is missing")
    else:
        if set(authority) != set(TASK_HALL_AUTHORITY):
            errors.append("Task Hall global authority policy fields are incomplete")
        for field, required in TASK_HALL_AUTHORITY.items():
            if authority.get(field) is not required:
                errors.append(f"Task Hall authority must keep {field}={str(required).lower()}")
    claim_policy = payload.get("claim_policy")
    if not isinstance(claim_policy, dict):
        errors.append("Task Hall claim policy is missing")
    else:
        if claim_policy.get("confirmation") != "manual_maintainer_confirmation":
            errors.append("Task Hall claims require manual maintainer confirmation")
        if claim_policy.get("external_contribution_route") != "fork_pull_request":
            errors.append("Task Hall external contribution route must remain fork_pull_request")
        lease_days = claim_policy.get("default_lease_days")
        if not isinstance(lease_days, int) or lease_days < 1:
            errors.append("Task Hall claim lease must have a positive duration")
    surfaces = payload.get("public_surfaces")
    expected_surfaces = {
        "rfc_discussion": "https://github.com/Lingkyn/xr-foundry/discussions/22",
        "task_hall_project": "https://github.com/users/Lingkyn/projects/2",
    }
    if surfaces != expected_surfaces:
        errors.append("Task Hall public RFC and Project surfaces are invalid")
    return errors


def validate_task_hall_contract(root: Path) -> list[str]:
    errors: list[str] = []
    for relative in sorted(REQUIRED_AGENT_COMMONS_FILES):
        if not (root / relative).exists():
            errors.append(f"Agent Commons is missing {relative}")

    errors.extend(validate_agent_commons_source_manifest(root))
    schema_path = root / "docs" / "contributing" / "task-contract.schema.json"
    example_path = root / "docs" / "contributing" / "task-contract.example.json"
    labels_path = root / "docs" / "contributing" / "label-contract.json"
    authority_path = root / "docs" / "contributing" / "task-hall.v1.json"
    authority_schema_path = root / "docs" / "contributing" / "task-hall.v1.schema.json"
    if authority_path.exists():
        authority_payload = load_json(authority_path)
        errors.extend(validate_task_hall_authority(authority_payload))
        if authority_schema_path.exists():
            errors.extend(
                validate_json_schema_instance(
                    authority_payload,
                    authority_schema_path,
                    "Task Hall authority",
                )
            )
    if authority_schema_path.exists():
        authority_schema = load_json(authority_schema_path)
        properties = authority_schema.get("properties", {})
        lifecycle_const = (
            properties.get("lifecycle", {})
            .get("properties", {})
            .get("ordered_states", {})
            .get("const")
        )
        if lifecycle_const != TASK_HALL_LIFECYCLE:
            errors.append("Task Hall JSON Schema must freeze the canonical V1 lifecycle")
        authority_properties = properties.get("authority", {}).get("properties", {})
        for field, required in TASK_HALL_AUTHORITY.items():
            if authority_properties.get(field, {}).get("const") is not required:
                errors.append(f"Task Hall JSON Schema must freeze {field}={str(required).lower()}")
    if schema_path.exists():
        schema = load_json(schema_path)
        authority_properties = schema.get("properties", {}).get("authority", {}).get("properties", {})
        for field in (
            "write_permission_not_inferred",
            "merge_permission_not_inferred",
            "comments_are_untrusted_input",
        ):
            if authority_properties.get(field, {}).get("const") is not True:
                errors.append(f"Task schema must require {field}=true")
    if example_path.exists():
        example = load_json(example_path)
        errors.extend(validate_task_contract(example, "Task Hall example"))
        claim = example.get("claim", {})
        if example.get("state") != "proposal" or claim.get("status") != "unclaimed":
            errors.append("Task Hall example must remain at proposal and unclaimed")
        for field in ("github_identity", "claimed_at", "expires_at", "confirmed_by_maintainer"):
            if claim.get(field) is not None:
                errors.append(f"Task Hall example must not pre-assign {field}")
    if labels_path.exists():
        labels = load_json(labels_path)
        if labels.get("schema") != "xr-foundry.task_hall_labels.v1":
            errors.append("Task Hall label contract schema is invalid")
        entries = labels.get("labels", [])
        names = [str(item.get("name", "")) for item in entries if isinstance(item, dict)]
        required_labels = {
            "rfc",
            "task:ready",
            "task:claimed",
            "task:review",
            "task:blocked",
            "device-lab",
            "needs-device:pico",
            "needs-device:quest",
            "needs-device:vision-pro",
            "renderer:ugui",
            "renderer:ui-toolkit",
            "role:research",
            "role:build",
            "role:review",
            "role:device-test",
            "security-boundary",
        }
        if len(names) != len(set(names)):
            errors.append("Task Hall label contract contains duplicate names")
        if missing := required_labels - set(names):
            errors.append(f"Task Hall label contract lacks required labels: {sorted(missing)}")
    return errors


def validate_device_profile(payload: Any, label: str = "device profile") -> list[str]:
    errors = validate_json_schema_instance(
        payload,
        ROOT / "docs" / "device-lab" / "device-profile.schema.json",
        label,
    )
    if not isinstance(payload, dict):
        return [f"{label}: profile must be an object"]
    if payload.get("schema") != "xr-foundry.device_profile.v1":
        errors.append(f"{label}: schema is invalid")
    if payload.get("profile_status") not in {"proposed", "open_for_evidence", "retired"}:
        errors.append(f"{label}: profile status is invalid")
    claim_allowed = payload.get("claim_allowed")
    if not isinstance(claim_allowed, bool):
        errors.append(f"{label}: claim_allowed must be boolean")
    if claim_allowed is True and payload.get("profile_status") != "open_for_evidence":
        errors.append(f"{label}: claim_allowed=true requires open_for_evidence")
    claim_gate = payload.get("claim_gate_issue")
    if claim_allowed is False and (
        not isinstance(claim_gate, str) or not claim_gate.startswith("https://github.com/")
    ):
        errors.append(f"{label}: claim_allowed=false requires a public claim gate Issue")
    if claim_allowed is True and claim_gate is not None:
        errors.append(f"{label}: admitted claim profile must not retain a pending claim gate")
    if payload.get("evidence_status") not in {"not_tested", "partial", "verified"}:
        errors.append(f"{label}: evidence status is invalid")
    device = payload.get("device")
    if not isinstance(device, dict):
        errors.append(f"{label}: hardware device envelope is missing")
    else:
        for field in ("vendor", "family_id", "family_name", "os_family"):
            value = device.get(field)
            if not isinstance(value, str) or not value.strip():
                errors.append(f"{label}: device.{field} must be stated")
    runtime = payload.get("runtime")
    if not isinstance(runtime, dict):
        errors.append(f"{label}: runtime envelope is missing")
    else:
        for field in ("runtime_id", "api_family"):
            value = runtime.get(field)
            if not isinstance(value, str) or not value.strip():
                errors.append(f"{label}: runtime.{field} must be stated")
    input_routes = payload.get("input_routes")
    if (
        not isinstance(input_routes, list)
        or not input_routes
        or any(not isinstance(value, str) or not value.strip() for value in input_routes)
        or len(input_routes) != len(set(input_routes))
    ):
        errors.append(f"{label}: input_routes must be a unique non-empty list")
    non_claims = payload.get("non_claims")
    if not isinstance(non_claims, list) or not non_claims:
        errors.append(f"{label}: non_claims must be a non-empty list")
    for capability_field in ("required_scenarios", "required_checks", "renderer", "package_tuple"):
        if capability_field in payload:
            errors.append(f"{label}: capability field belongs in a test plan, not profile: {capability_field}")
    return errors


def validate_capability_test_plan(
    payload: Any,
    profiles: dict[str, dict[str, Any]],
    label: str = "capability test plan",
) -> list[str]:
    errors = validate_json_schema_instance(
        payload,
        ROOT / "docs" / "device-lab" / "capability-test-plan.schema.json",
        label,
    )
    if not isinstance(payload, dict):
        return [f"{label}: plan must be an object"]
    if payload.get("schema") != "xr-foundry.capability_test_plan.v1":
        errors.append(f"{label}: schema is invalid")
    plan_id = payload.get("test_plan_id")
    if not isinstance(plan_id, str) or not plan_id.strip():
        errors.append(f"{label}: test_plan_id is required")
    compositions: dict[str, dict[str, Any]] = {}
    raw_compositions = payload.get("allowed_package_compositions")
    if not isinstance(raw_compositions, list) or not raw_compositions:
        errors.append(f"{label}: allowed package compositions are required")
    else:
        for item in raw_compositions:
            if not isinstance(item, dict):
                errors.append(f"{label}: package compositions must be objects")
                continue
            composition_id = str(item.get("id", ""))
            if not composition_id or composition_id in compositions:
                errors.append(f"{label}: composition id is missing or duplicated: {composition_id}")
            compositions[composition_id] = item
            claim_id = str(item.get("required_claim_id", ""))
            if not claim_id:
                errors.append(f"{label}: composition {composition_id} requires required_claim_id")
            for field in ("domain", "presentation", "renderer_adapter", "xr_adapter"):
                value = item.get(field)
                if not isinstance(value, str) or not value.strip():
                    errors.append(f"{label}: composition {composition_id} requires {field}")

    targets = payload.get("allowed_targets")
    target_profiles: set[str] = set()
    if not isinstance(targets, list) or not targets:
        errors.append(f"{label}: allowed targets are required")
    else:
        for target in targets:
            if not isinstance(target, dict):
                errors.append(f"{label}: allowed targets must be objects")
                continue
            profile_id = str(target.get("device_profile_id", ""))
            if not profile_id or profile_id in target_profiles:
                errors.append(f"{label}: target profile is missing or duplicated: {profile_id}")
            target_profiles.add(profile_id)
            profile = profiles.get(profile_id)
            if profile is None:
                errors.append(f"{label}: target references unknown profile {profile_id}")
                continue
            composition_ids = target.get("composition_ids")
            if not isinstance(composition_ids, list) or not composition_ids:
                errors.append(f"{label}: target {profile_id} requires composition_ids")
            else:
                unknown = set(composition_ids) - set(compositions)
                if unknown:
                    errors.append(f"{label}: target {profile_id} references unknown compositions: {sorted(unknown)}")
            required_routes = target.get("required_input_routes")
            if not isinstance(required_routes, list) or not required_routes:
                errors.append(f"{label}: target {profile_id} requires input routes")
            else:
                unsupported = set(required_routes) - set(profile.get("input_routes", []))
                if unsupported:
                    errors.append(f"{label}: target {profile_id} uses profile-unsupported input: {sorted(unsupported)}")

    check_ids: set[str] = set()
    required_checks = payload.get("required_checks")
    if not isinstance(required_checks, list) or not required_checks:
        errors.append(f"{label}: required checks are missing")
    else:
        for check in required_checks:
            if not isinstance(check, dict):
                errors.append(f"{label}: required checks must be objects")
                continue
            check_id = str(check.get("id", ""))
            if not check_id or check_id in check_ids:
                errors.append(f"{label}: required check id is missing or duplicated: {check_id}")
            check_ids.add(check_id)
            for field in ("purpose", "expected"):
                if not str(check.get(field, "")).strip():
                    errors.append(f"{label}: required check {check_id} requires {field}")
    claim_ids: set[str] = set()
    for composition_id, composition in compositions.items():
        claim_id = str(composition.get("required_claim_id", ""))
        if not claim_id or claim_id in claim_ids:
            errors.append(f"{label}: required claim id is missing or duplicated: {claim_id}")
        claim_ids.add(claim_id)
    optional_checks = payload.get("optional_checks")
    if not isinstance(optional_checks, list):
        errors.append(f"{label}: optional checks must be a list")
    else:
        for check in optional_checks:
            if not isinstance(check, dict):
                errors.append(f"{label}: optional checks must be objects")
                continue
            check_id = str(check.get("id", ""))
            claim_id = str(check.get("claim_id", ""))
            if not check_id or check_id in check_ids:
                errors.append(f"{label}: optional check id is missing or duplicated: {check_id}")
            check_ids.add(check_id)
            if not claim_id or claim_id in claim_ids:
                errors.append(f"{label}: optional claim id is missing or duplicated: {claim_id}")
            claim_ids.add(claim_id)
            for field in ("required_input_route", "purpose", "expected"):
                if not str(check.get(field, "")).strip():
                    errors.append(f"{label}: optional check {check_id} requires {field}")
    if not isinstance(payload.get("non_claims"), list) or not payload.get("non_claims"):
        errors.append(f"{label}: non_claims must be a non-empty list")
    return errors


def validate_device_lab_execution_receipt(
    payload: Any,
    profiles: dict[str, dict[str, Any]],
    plans: dict[str, dict[str, Any]],
    label: str = "device execution receipt",
    allow_template: bool = False,
) -> list[str]:
    errors = validate_json_schema_instance(
        payload,
        ROOT / "docs" / "device-lab" / "device-receipt.schema.json",
        label,
    )
    if not isinstance(payload, dict):
        return [f"{label}: receipt must be an object"]
    if payload.get("schema") != "xr-foundry.device_execution_receipt.v1":
        errors.append(f"{label}: schema is invalid")
    overall = payload.get("overall_result")
    if overall not in {"pass", "fail", "blocked", "inconclusive", "not_tested"}:
        errors.append(f"{label}: overall_result is invalid")
    if allow_template:
        if overall != "not_tested":
            errors.append(f"{label}: template must remain not_tested")
        if payload.get("claims_supported"):
            errors.append(f"{label}: template cannot support claims")
        if any(
            isinstance(item, dict) and item.get("status") != "not_tested"
            for item in payload.get("checks", [])
        ):
            errors.append(f"{label}: every template check must remain not_tested")
        if any(
            isinstance(item, dict) and item.get("supported") is not False
            for item in payload.get("optional_claims", [])
        ):
            errors.append(f"{label}: every template optional claim must remain false")
        revision = payload.get("revision", {})
        artifact = payload.get("artifact", {})
        tester = payload.get("tester", {})
        if revision.get("commit_sha") is not None:
            errors.append(f"{label}: template must not contain a commit SHA")
        if any(artifact.get(field) is not None for field in ("sha256", "artifact_ref", "application_id")):
            errors.append(f"{label}: template must not contain artifact evidence")
        if tester.get("github_identity") is not None:
            errors.append(f"{label}: template must not pre-assign a tester")
        plan = plans.get(str(payload.get("test_plan_id", "")))
        profile = profiles.get(str(payload.get("device_profile_id", "")))
        if plan is None:
            errors.append(f"{label}: template must reference a known test plan")
        if profile is None:
            errors.append(f"{label}: template must reference a known device profile")
        declared_claims: set[str] = set()
        if isinstance(plan, dict):
            declared_claims.update(
                str(item.get("required_claim_id", ""))
                for item in plan.get("allowed_package_compositions", [])
                if isinstance(item, dict)
            )
            declared_claims.update(
                str(item.get("claim_id", ""))
                for item in plan.get("optional_checks", [])
                if isinstance(item, dict)
            )
        if set(payload.get("claims_not_supported", [])) != declared_claims:
            errors.append(f"{label}: template must enumerate every declared plan claim as unsupported")
        return errors
    if overall == "not_tested":
        errors.append(f"{label}: not_tested is not an execution receipt")

    task_url = payload.get("task_url")
    if not isinstance(task_url, str) or not re.fullmatch(
        r"https://github\.com/Lingkyn/xr-foundry/issues/[1-9][0-9]*",
        task_url,
    ):
        errors.append(f"{label}: canonical public xr-foundry Issue URL is required")
    revision = payload.get("revision")
    if not isinstance(revision, dict):
        errors.append(f"{label}: revision is missing")
    else:
        repository = revision.get("repository")
        if repository != PUBLIC_REPOSITORY:
            errors.append(f"{label}: revision.repository must be {PUBLIC_REPOSITORY}")
        commit_sha = str(revision.get("commit_sha", ""))
        if not FULL_SHA_PATTERN.fullmatch(commit_sha) or set(commit_sha) == {"0"}:
            errors.append(f"{label}: non-zero full 40-character commit SHA is required")
    artifact = payload.get("artifact")
    if not isinstance(artifact, dict):
        errors.append(f"{label}: artifact identity is missing")
    else:
        digest = str(artifact.get("sha256", ""))
        if not SHA256_PATTERN.fullmatch(digest) or set(digest) == {"0"}:
            errors.append(f"{label}: non-zero artifact SHA-256 is required")
        artifact_ref = artifact.get("artifact_ref")
        if not isinstance(artifact_ref, str) or not artifact_ref.startswith("https://"):
            errors.append(f"{label}: artifact.artifact_ref must be a public HTTPS reference")
        application_id = artifact.get("application_id")
        if not isinstance(application_id, str) or not re.fullmatch(
            r"[A-Za-z][A-Za-z0-9_]*(?:\.[A-Za-z][A-Za-z0-9_]*)+",
            application_id,
        ):
            errors.append(f"{label}: artifact.application_id must be an exact reverse-domain identifier")

    profile_id = str(payload.get("device_profile_id", ""))
    profile = profiles.get(profile_id)
    if profile is None:
        errors.append(f"{label}: unknown device profile {profile_id}")
    elif profile.get("claim_allowed") is not True:
        errors.append(f"{label}: device profile {profile_id} has claim_allowed=false")
    plan_id = str(payload.get("test_plan_id", ""))
    plan = plans.get(plan_id)
    if plan is None:
        errors.append(f"{label}: unknown capability test plan {plan_id}")

    package_tuple = payload.get("package_tuple")
    composition_id = ""
    composition: dict[str, Any] | None = None
    if not isinstance(package_tuple, dict):
        errors.append(f"{label}: exact package tuple is missing")
    else:
        composition_id = str(package_tuple.get("composition_id", ""))
        if isinstance(plan, dict):
            composition = next(
                (
                    item
                    for item in plan.get("allowed_package_compositions", [])
                    if isinstance(item, dict) and item.get("id") == composition_id
                ),
                None,
            )
        if composition is None:
            errors.append(f"{label}: package composition {composition_id} is not admitted by the plan")
        for role in ("domain", "presentation", "renderer_adapter", "xr_adapter"):
            package = package_tuple.get(role)
            if not isinstance(package, dict):
                errors.append(f"{label}: package tuple requires {role}")
                continue
            package_id = package.get("id")
            version = package.get("version")
            if composition is not None and package_id != composition.get(role):
                errors.append(f"{label}: {role} does not match composition {composition_id}")
            if not isinstance(version, str) or not SEMVER_PATTERN.fullmatch(version):
                errors.append(f"{label}: {role} version must be an exact semantic version")

    target: dict[str, Any] | None = None
    if isinstance(plan, dict):
        target = next(
            (
                item
                for item in plan.get("allowed_targets", [])
                if isinstance(item, dict) and item.get("device_profile_id") == profile_id
            ),
            None,
        )
        if target is None:
            errors.append(f"{label}: device profile {profile_id} is not admitted by test plan {plan_id}")
        elif composition_id not in target.get("composition_ids", []):
            errors.append(f"{label}: composition {composition_id} is not admitted for device profile {profile_id}")

    software = payload.get("software")
    device = payload.get("device")
    input_state = payload.get("input")
    if not isinstance(software, dict):
        errors.append(f"{label}: software environment is missing")
    else:
        for field in ("engine", "engine_version", "runtime_id", "runtime_version"):
            value = software.get(field)
            if not isinstance(value, str) or not value.strip():
                errors.append(f"{label}: software.{field} is required")
        if software.get("engine") != "Unity":
            errors.append(f"{label}: Inventory package test plan requires software.engine=Unity")
        if isinstance(profile, dict) and software.get("runtime_id") != profile.get("runtime", {}).get("runtime_id"):
            errors.append(f"{label}: runtime_id does not match device profile {profile_id}")
    if not isinstance(device, dict):
        errors.append(f"{label}: device environment is missing")
    else:
        for field in ("family_id", "model", "os_family", "os_version"):
            value = device.get(field)
            if not isinstance(value, str) or not value.strip():
                errors.append(f"{label}: device.{field} is required")
        if isinstance(profile, dict):
            profile_device = profile.get("device", {})
            if device.get("family_id") != profile_device.get("family_id"):
                errors.append(f"{label}: device family does not match profile {profile_id}")
            if device.get("os_family") != profile_device.get("os_family"):
                errors.append(f"{label}: device OS family does not match profile {profile_id}")
    routes: set[str] = set()
    if not isinstance(input_state, dict):
        errors.append(f"{label}: input environment is missing")
    else:
        raw_routes = input_state.get("routes")
        if not isinstance(raw_routes, list) or not raw_routes or len(raw_routes) != len(set(raw_routes)):
            errors.append(f"{label}: input routes must be a unique non-empty list")
        else:
            routes = set(raw_routes)
        description = input_state.get("device_description")
        if not isinstance(description, str) or not description.strip():
            errors.append(f"{label}: input device_description is required")
        if isinstance(profile, dict):
            unsupported = routes - set(profile.get("input_routes", []))
            if unsupported:
                errors.append(f"{label}: input routes do not match device profile: {sorted(unsupported)}")
        if isinstance(target, dict):
            missing_routes = set(target.get("required_input_routes", [])) - routes
            if missing_routes:
                errors.append(f"{label}: test-plan required input routes are missing: {sorted(missing_routes)}")

    required_checks: dict[str, dict[str, Any]] = {}
    optional_checks: dict[str, dict[str, Any]] = {}
    if isinstance(plan, dict):
        required_checks = {
            str(item.get("id", "")): item
            for item in plan.get("required_checks", [])
            if isinstance(item, dict)
        }
        optional_checks = {
            str(item.get("id", "")): item
            for item in plan.get("optional_checks", [])
            if isinstance(item, dict)
        }
    check_by_id: dict[str, dict[str, Any]] = {}
    checks = payload.get("checks")
    if not isinstance(checks, list):
        errors.append(f"{label}: checks must be a list")
    else:
        for check in checks:
            if not isinstance(check, dict):
                errors.append(f"{label}: checks must be objects")
                continue
            check_id = str(check.get("id", ""))
            if not check_id or check_id in check_by_id:
                errors.append(f"{label}: check id is missing or duplicated: {check_id}")
            check_by_id[check_id] = check
            if check_id not in required_checks and check_id not in optional_checks:
                errors.append(f"{label}: check is not declared by test plan: {check_id}")
            status = check.get("status")
            if status not in {"pass", "fail", "blocked", "inconclusive", "not_tested"}:
                errors.append(f"{label}: check status is invalid: {check_id}")
            if status != "not_tested":
                if not str(check.get("observation", "")).strip():
                    errors.append(f"{label}: executed check lacks observation: {check_id}")
                evidence_refs = check.get("evidence_refs")
                if not isinstance(evidence_refs, list) or not evidence_refs:
                    errors.append(f"{label}: executed check lacks evidence refs: {check_id}")
                else:
                    for evidence in evidence_refs:
                        if not isinstance(evidence, dict):
                            errors.append(f"{label}: evidence ref must be structured: {check_id}")
                            continue
                        kind = evidence.get("kind")
                        reference = evidence.get("ref")
                        evidence_digest = str(evidence.get("sha256", ""))
                        if (
                            not SHA256_PATTERN.fullmatch(evidence_digest)
                            or set(evidence_digest) == {"0"}
                        ):
                            errors.append(f"{label}: evidence ref requires non-zero SHA-256: {check_id}")
                        if kind == "repository_file":
                            if (
                                not isinstance(reference, str)
                                or not reference.strip()
                                or reference.startswith(("/", "\\"))
                                or re.match(r"^[A-Za-z]:", reference)
                                or ".." in reference.replace("\\", "/").split("/")
                            ):
                                errors.append(f"{label}: repository evidence path is unsafe: {check_id}")
                        elif kind == "public_url":
                            if not isinstance(reference, str) or not reference.startswith("https://"):
                                errors.append(f"{label}: public evidence URL must use HTTPS: {check_id}")
                        else:
                            errors.append(f"{label}: evidence ref kind is invalid: {check_id}")
            elif check.get("observation") or check.get("evidence_refs"):
                errors.append(f"{label}: not_tested check cannot carry observation or evidence: {check_id}")
    missing_required = set(required_checks) - set(check_by_id)
    if missing_required:
        errors.append(f"{label}: required checks are missing: {sorted(missing_required)}")
    missing_optional = set(optional_checks) - set(check_by_id)
    if missing_optional:
        errors.append(f"{label}: optional checks must be explicitly classified: {sorted(missing_optional)}")
    for check_id in required_checks:
        check = check_by_id.get(check_id, {})
        if check.get("status") == "not_tested":
            errors.append(f"{label}: required check cannot remain not_tested: {check_id}")
    required_statuses = [check_by_id.get(check_id, {}).get("status") for check_id in required_checks]
    if required_checks and not missing_required and all(
        status in {"pass", "fail", "blocked", "inconclusive"}
        for status in required_statuses
    ):
        if "fail" in required_statuses:
            derived_result = "fail"
        elif "blocked" in required_statuses:
            derived_result = "blocked"
        elif "inconclusive" in required_statuses:
            derived_result = "inconclusive"
        else:
            derived_result = "pass"
        if overall != derived_result:
            errors.append(
                f"{label}: overall_result={overall} conflicts with required-check result={derived_result}"
            )

    optional_by_claim = {
        str(item.get("claim_id", "")): item
        for item in optional_checks.values()
    }
    claim_by_id: dict[str, dict[str, Any]] = {}
    claims = payload.get("optional_claims")
    if not isinstance(claims, list):
        errors.append(f"{label}: optional_claims must be a list")
    else:
        for claim in claims:
            if not isinstance(claim, dict):
                errors.append(f"{label}: optional claims must be objects")
                continue
            claim_id = str(claim.get("id", ""))
            if not claim_id or claim_id in claim_by_id:
                errors.append(f"{label}: optional claim id is missing or duplicated: {claim_id}")
            claim_by_id[claim_id] = claim
            if claim_id not in optional_by_claim:
                errors.append(f"{label}: optional claim is not declared by test plan: {claim_id}")
            if not isinstance(claim.get("supported"), bool):
                errors.append(f"{label}: optional claim supported must be boolean: {claim_id}")
    if missing_claims := set(optional_by_claim) - set(claim_by_id):
        errors.append(f"{label}: optional claims must be explicitly classified: {sorted(missing_claims)}")
    accepted_optional_claims: set[str] = set()
    for claim_id, definition in optional_by_claim.items():
        claim = claim_by_id.get(claim_id, {})
        if claim.get("supported") is not True:
            continue
        check = check_by_id.get(str(definition.get("id", "")), {})
        required_route = str(definition.get("required_input_route", ""))
        if overall != "pass":
            errors.append(f"{label}: optional claim requires overall_result=pass: {claim_id}")
        if check.get("status") != "pass":
            errors.append(f"{label}: unsupported optional claim lacks a passed check: {claim_id}")
        if required_route not in routes:
            errors.append(f"{label}: optional claim input route was not executed: {claim_id}")
        if isinstance(profile, dict) and required_route not in profile.get("input_routes", []):
            errors.append(f"{label}: optional claim input route is not admitted by profile: {claim_id}")
        if (
            overall == "pass"
            and check.get("status") == "pass"
            and required_route in routes
            and isinstance(profile, dict)
            and required_route in profile.get("input_routes", [])
        ):
            accepted_optional_claims.add(claim_id)

    claims_supported = payload.get("claims_supported")
    claims_not_supported = payload.get("claims_not_supported")
    if not isinstance(claims_supported, list):
        errors.append(f"{label}: claims_supported must be a list")
        claims_supported_set: set[str] = set()
    else:
        claims_supported_set = {str(item) for item in claims_supported}
        if len(claims_supported_set) != len(claims_supported):
            errors.append(f"{label}: claims_supported contains duplicates")
    if not isinstance(claims_not_supported, list) or not claims_not_supported:
        errors.append(f"{label}: claims_not_supported must be a non-empty list")
        claims_not_supported_set: set[str] = set()
    else:
        claims_not_supported_set = {str(item) for item in claims_not_supported}
        if len(claims_not_supported_set) != len(claims_not_supported):
            errors.append(f"{label}: claims_not_supported contains duplicates")

    required_claims: set[str] = set()
    if isinstance(plan, dict):
        required_claims = {
            str(item.get("required_claim_id", ""))
            for item in plan.get("allowed_package_compositions", [])
            if isinstance(item, dict)
        }
    declared_claims = required_claims | set(optional_by_claim)
    expected_supported = set(accepted_optional_claims)
    if overall == "pass" and isinstance(composition, dict):
        expected_supported.add(str(composition.get("required_claim_id", "")))
    expected_not_supported = declared_claims - expected_supported
    unknown_claims = (claims_supported_set | claims_not_supported_set) - declared_claims
    if unknown_claims:
        errors.append(f"{label}: capability claims are not enumerated by the plan: {sorted(unknown_claims)}")
    overlap = claims_supported_set & claims_not_supported_set
    if overlap:
        errors.append(f"{label}: capability claims cannot be both supported and unsupported: {sorted(overlap)}")
    if claims_supported_set != expected_supported:
        errors.append(
            f"{label}: claims_supported must equal derived claim IDs: {sorted(expected_supported)}"
        )
    if claims_not_supported_set != expected_not_supported:
        errors.append(
            f"{label}: claims_not_supported must enumerate remaining claim IDs: {sorted(expected_not_supported)}"
        )

    tester = payload.get("tester")
    tester_identity = tester.get("github_identity") if isinstance(tester, dict) else None
    if not isinstance(tester_identity, str) or not tester_identity.strip():
        errors.append(f"{label}: accountable GitHub tester identity is required")
    timestamps = payload.get("timestamps")
    if not isinstance(timestamps, dict):
        errors.append(f"{label}: timestamps are missing")
    else:
        parsed_timestamps: dict[str, datetime] = {}
        for field in ("started_at", "completed_at"):
            value = timestamps.get(field)
            if not isinstance(value, str) or not value.strip():
                errors.append(f"{label}: timestamp {field} is required")
                continue
            try:
                parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
                if parsed.tzinfo is None:
                    raise ValueError("timezone required")
                parsed_timestamps[field] = parsed
            except ValueError:
                errors.append(f"{label}: timestamp {field} must be timezone-aware ISO 8601")
        if (
            "started_at" in parsed_timestamps
            and "completed_at" in parsed_timestamps
            and parsed_timestamps["completed_at"] < parsed_timestamps["started_at"]
        ):
            errors.append(f"{label}: completed_at must not precede started_at")
    return errors


def validate_device_lab_contract(root: Path) -> list[str]:
    errors: list[str] = []
    lab_root = root / "docs" / "device-lab"
    profile_schema_path = lab_root / "device-profile.schema.json"
    plan_schema_path = lab_root / "capability-test-plan.schema.json"
    receipt_schema_path = lab_root / "device-receipt.schema.json"
    expected_schema_consts = {
        profile_schema_path: "xr-foundry.device_profile.v1",
        plan_schema_path: "xr-foundry.capability_test_plan.v1",
        receipt_schema_path: "xr-foundry.device_execution_receipt.v1",
    }
    for path, expected in expected_schema_consts.items():
        if not path.exists():
            continue
        schema = load_json(path)
        actual = schema.get("properties", {}).get("schema", {}).get("const")
        if actual != expected:
            errors.append(f"Device Lab schema contract is invalid: {path.name}")

    profiles: dict[str, dict[str, Any]] = {}
    profiles_root = lab_root / "profiles"
    for path in sorted(profiles_root.glob("*.json")) if profiles_root.exists() else []:
        profile = load_json(path)
        label = f"Device profile {path.name}"
        profile_id = str(profile.get("profile_id", ""))
        if profile_id != path.stem:
            errors.append(f"{label}: profile_id must match filename")
        if not profile_id or profile_id in profiles:
            errors.append(f"{label}: profile id is missing or duplicated")
        profiles[profile_id] = profile
        errors.extend(validate_device_profile(profile, label))
    expected_profile_policy = {
        "pico-openxr-controller": {
            "profile_status": "open_for_evidence",
            "claim_allowed": True,
            "claim_gate_issue": None,
        },
        "quest-openxr-controller": {
            "profile_status": "proposed",
            "claim_allowed": False,
            "claim_gate_issue": "https://github.com/Lingkyn/xr-foundry/issues/29",
        },
        "vision-pro-spatial-input": {
            "profile_status": "proposed",
            "claim_allowed": False,
            "claim_gate_issue": "https://github.com/Lingkyn/xr-foundry/issues/30",
        },
    }
    for profile_id, expected in expected_profile_policy.items():
        profile = profiles.get(profile_id)
        if profile is None:
            errors.append(f"Device Lab lacks starter profile: {profile_id}")
            continue
        for field, value in expected.items():
            if profile.get(field) != value:
                errors.append(f"Device profile {profile_id}: must keep {field}={value!r}")

    plans: dict[str, dict[str, Any]] = {}
    plans_root = lab_root / "test-plans"
    for path in sorted(plans_root.glob("*.json")) if plans_root.exists() else []:
        plan = load_json(path)
        label = f"Capability test plan {path.name}"
        plan_id = str(plan.get("test_plan_id", ""))
        if plan_id != path.stem:
            errors.append(f"{label}: test_plan_id must match filename")
        if not plan_id or plan_id in plans:
            errors.append(f"{label}: test plan id is missing or duplicated")
        plans[plan_id] = plan
        errors.extend(validate_capability_test_plan(plan, profiles, label))

    inventory_plan = plans.get("inventory-world-space-ui-v1")
    if inventory_plan is None:
        errors.append("Device Lab lacks Inventory world-space UI capability plan")
    else:
        compositions = {
            str(item.get("id", "")): {
                role: item.get(role)
                for role in ("domain", "presentation", "renderer_adapter", "xr_adapter")
            }
            for item in inventory_plan.get("allowed_package_compositions", [])
            if isinstance(item, dict)
        }
        if compositions != INVENTORY_WORLD_SPACE_COMPOSITIONS:
            errors.append("Inventory world-space plan must keep renderer/XR adapter package pairs exact")
        targets = {
            str(item.get("device_profile_id", "")): item
            for item in inventory_plan.get("allowed_targets", [])
            if isinstance(item, dict)
        }
        if set(targets) != {"pico-openxr-controller", "quest-openxr-controller"}:
            errors.append("Inventory world-space plan target profiles are invalid")
        for profile_id, target in targets.items():
            if set(target.get("composition_ids", [])) != set(INVENTORY_WORLD_SPACE_COMPOSITIONS):
                errors.append(f"Inventory world-space plan compositions are incomplete for {profile_id}")
            if target.get("required_input_routes") != ["tracked-controller-ray"]:
                errors.append(f"Inventory world-space plan must require tracked-controller-ray for {profile_id}")

    template_path = lab_root / "device-receipt.template.json"
    if template_path.exists():
        template = load_json(template_path)
        errors.extend(
            validate_device_lab_execution_receipt(
                template,
                profiles,
                plans,
                "Device Lab receipt template",
                allow_template=True,
            )
        )

    receipt_profiles: set[str] = set()
    passing_profiles: set[str] = set()
    receipts_root = lab_root / "receipts"
    for path in sorted(receipts_root.glob("*.json")) if receipts_root.exists() else []:
        receipt = load_json(path)
        errors.extend(
            validate_device_lab_execution_receipt(
                receipt,
                profiles,
                plans,
                f"Device Lab receipt {path.name}",
            )
        )
        profile_id = str(receipt.get("device_profile_id", ""))
        receipt_profiles.add(profile_id)
        if receipt.get("overall_result") == "pass":
            passing_profiles.add(profile_id)
    for profile_id, profile in profiles.items():
        evidence_status = profile.get("evidence_status")
        if profile_id in receipt_profiles and evidence_status == "not_tested":
            errors.append(f"Device profile {profile_id}: committed receipts require partial or verified evidence status")
        if evidence_status in {"partial", "verified"} and profile_id not in receipt_profiles:
            errors.append(f"Device profile {profile_id}: {evidence_status} status requires a committed receipt")
        if evidence_status == "verified" and profile_id not in passing_profiles:
            errors.append(f"Device profile {profile_id}: verified status requires a passing generic receipt")
    return errors


def validate_workflow_security(root: Path) -> list[str]:
    errors: list[str] = []
    workflow_root = root / ".github" / "workflows"
    for path in sorted(workflow_root.glob("*.y*ml")) if workflow_root.exists() else []:
        label = path.relative_to(root)
        try:
            workflow = load_workflow(path)
        except (yaml.YAMLError, UnicodeDecodeError) as error:
            errors.append(f"{label}: workflow YAML cannot be parsed safely: {error}")
            continue
        if not isinstance(workflow, dict):
            errors.append(f"{label}: workflow root must be a mapping")
            continue

        triggers = workflow.get("on")
        if isinstance(triggers, str):
            trigger_names = {triggers}
        elif isinstance(triggers, list):
            trigger_names = {item for item in triggers if isinstance(item, str)}
            if len(trigger_names) != len(triggers):
                errors.append(f"{label}: workflow trigger list must contain only event names")
        elif isinstance(triggers, dict):
            trigger_names = {str(item) for item in triggers}
        else:
            trigger_names = set()
            errors.append(f"{label}: workflow must declare a structured on trigger")
        allowed_triggers = {"pull_request", "push", "workflow_dispatch"}
        dangerous = trigger_names - allowed_triggers
        if "issue_comment" in dangerous:
            errors.append(f"{label}: comment-trigger workflows are forbidden in V1")
        if "pull_request_target" in dangerous:
            errors.append(f"{label}: pull_request_target requires a separate security decision")
        other_dangerous = dangerous - {"issue_comment", "pull_request_target"}
        if other_dangerous:
            errors.append(f"{label}: workflow uses forbidden or unreviewed triggers: {sorted(dangerous)}")
        if not trigger_names:
            errors.append(f"{label}: workflow must declare at least one admitted trigger")

        def validate_permissions(value: Any, scope: str, *, required: bool) -> None:
            if value is None:
                if required:
                    errors.append(f"{label}: {scope} permissions must be an explicit mapping")
                return
            if not isinstance(value, dict):
                errors.append(f"{label}: {scope} permissions must be a mapping of read/none grants")
                return
            if required and value.get("contents") != "read":
                errors.append(f"{label}: {scope} permissions must keep contents=read")
            for permission, access in value.items():
                if not isinstance(permission, str) or access not in {"read", "none"}:
                    errors.append(
                        f"{label}: {scope} permission {permission!r} must be read or none, got {access!r}"
                    )

        validate_permissions(workflow.get("permissions"), "workflow-level", required=True)

        jobs = workflow.get("jobs")
        if not isinstance(jobs, dict) or not jobs:
            errors.append(f"{label}: jobs must be a non-empty mapping")
            continue
        uses_entries: list[tuple[str, dict[str, Any], str]] = []
        for job_id, job in jobs.items():
            if not isinstance(job, dict):
                errors.append(f"{label}: job {job_id!r} must be a mapping")
                continue
            if "permissions" in job:
                validate_permissions(job.get("permissions"), f"job {job_id!r}", required=False)
            if isinstance(job.get("uses"), str):
                uses_entries.append((job["uses"], job, f"job {job_id!r}"))
            steps = job.get("steps", [])
            if not isinstance(steps, list):
                errors.append(f"{label}: job {job_id!r} steps must be a list")
                continue
            for index, step in enumerate(steps):
                if not isinstance(step, dict):
                    errors.append(f"{label}: job {job_id!r} step {index} must be a mapping")
                    continue
                if isinstance(step.get("uses"), str):
                    uses_entries.append((step["uses"], step, f"job {job_id!r} step {index}"))

        for uses, owner, location in uses_entries:
            if uses.startswith("./"):
                continue
            if "@" not in uses:
                errors.append(f"{label}: {location} external Action lacks an immutable revision: {uses}")
                continue
            action, revision = uses.rsplit("@", 1)
            if not action or not FULL_SHA_PATTERN.fullmatch(revision) or set(revision) == {"0"}:
                errors.append(f"{label}: third-party Action must use a full commit SHA (non-zero): {uses}")
            if action.casefold() == "actions/checkout":
                with_payload = owner.get("with")
                if not isinstance(with_payload, dict) or with_payload.get("persist-credentials") is not False:
                    errors.append(f"{label}: checkout must set persist-credentials=false as a YAML boolean")
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
    errors.extend(validate_agent_guide_source_boundary(root))
    errors.extend(validate_task_hall_contract(root))
    errors.extend(validate_device_lab_contract(root))
    errors.extend(validate_workflow_security(root))
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
    parser.add_argument(
        "--device-lab-receipt",
        type=Path,
        help="also validate one completed generic Device Lab execution receipt",
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

    device_lab_receipt_path: Path | None = None
    if args.device_lab_receipt is not None:
        device_lab_receipt_path = args.device_lab_receipt
        if not device_lab_receipt_path.is_absolute():
            device_lab_receipt_path = root / device_lab_receipt_path
        device_lab_receipt_path = device_lab_receipt_path.resolve()
        if not device_lab_receipt_path.exists():
            errors.append(f"Device Lab receipt does not exist: {device_lab_receipt_path}")
        else:
            try:
                receipt = load_json(device_lab_receipt_path)
            except (json.JSONDecodeError, UnicodeDecodeError) as exc:
                errors.append(f"Device Lab receipt is invalid JSON: {exc}")
            else:
                profiles = {
                    str(payload.get("profile_id", "")): payload
                    for path in (root / "docs" / "device-lab" / "profiles").glob("*.json")
                    if isinstance(payload := load_json(path), dict)
                }
                plans = {
                    str(payload.get("test_plan_id", "")): payload
                    for path in (root / "docs" / "device-lab" / "test-plans").glob("*.json")
                    if isinstance(payload := load_json(path), dict)
                }
                errors.extend(
                    validate_device_lab_execution_receipt(
                        receipt,
                        profiles,
                        plans,
                        "Device Lab CLI receipt",
                    )
                )
    report = {
        "schema": "xr-foundry.repository_validation.v1",
        "root": str(root),
        "status": "pass" if not errors else "fail",
        "errors": errors,
    }
    if device_receipt_path is not None:
        report["device_receipt"] = str(device_receipt_path)
    if device_lab_receipt_path is not None:
        report["device_lab_receipt"] = str(device_lab_receipt_path)
    print(json.dumps(report, indent=2))
    return 0 if not errors else 1


if __name__ == "__main__":
    raise SystemExit(main())
