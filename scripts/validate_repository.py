from __future__ import annotations

import argparse
import copy
import functools
import hashlib
import json
import os
import re
import stat
import struct
import subprocess
import sys
import unicodedata
import xml.etree.ElementTree as ET
import zipfile
from datetime import datetime
from pathlib import Path, PurePosixPath
from typing import Any

import yaml
from jsonschema import Draft202012Validator, FormatChecker
from yaml.constructor import ConstructorError


ROOT = Path(__file__).resolve().parents[1]
REQUIRED_ROOT_FILES = {
    "README.md", "LICENSE", "CHANGELOG.md", "ROADMAP.md", "CONTRIBUTING.md",
    "CODE_OF_CONDUCT.md", "SECURITY.md", "SUPPORT.md", "AGENTS.md", "CLAUDE.md",
    "SKILL.md", "package-catalog.json", "reference-catalog.json",
    "compatibility-profiles.json",
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
    "renderer-neutral-architecture.md",
}
TEXT_SUFFIXES = {
    ".asset",
    ".asmdef",
    ".cs",
    ".json",
    ".md",
    ".meta",
    ".prefab",
    ".py",
    ".txt",
    ".uss",
    ".uxml",
    ".xml",
    ".yaml",
    ".yml",
}
STRICT_TEXT_SUFFIXES = TEXT_SUFFIXES - {".asset"}
PLACEHOLDER_PATTERN = re.compile(
    r"(?:\bTBD\b|\bTODO\b|<placeholder>|replace[ _-]?me|lorem ipsum)",
    re.IGNORECASE,
)
COMPATIBILITY_PROFILES = Path("compatibility-profiles.json")
COMPATIBILITY_PROFILES_SCHEMA = (
    Path("docs") / "architecture" / "compatibility-profiles.schema.json"
)
COMPATIBILITY_EVIDENCE_SCHEMA = (
    Path("docs") / "validation" / "compatibility-evidence.schema.json"
)
UNITY_COMPILE_RESULT_SCHEMA = (
    Path("docs") / "validation" / "unity-compile-result.schema.json"
)
COMPATIBILITY_TUPLE_DIMENSIONS = (
    "engine",
    "editor",
    "renderer",
    "requested_dependencies",
    "resolved_dependencies",
    "build_target",
    "graphics_api",
    "scripting_backend",
    "architecture",
    "xr_provider",
    "runtime",
    "input_routes",
    "device",
)
COMPATIBILITY_PROFILE_STATES = (
    "candidate",
    "pending_automated_validation",
    "verified",
)
REQUIRED_AGENT_COMMONS_FILES = {
    "PROJECT_GITHUB_PLAYBOOK.md",
    ".github/CODEOWNERS",
    ".github/DISCUSSION_TEMPLATE/ideas.yml",
    ".github/ISSUE_TEMPLATE/task.yml",
    ".github/ISSUE_TEMPLATE/device-test.yml",
    "docs/rfcs/0001-agent-commons.md",
    "docs/contributing/agent-commons-source-manifest.json",
    "docs/contributing/agent-contribution-protocol.md",
    "docs/contributing/contribution-credit.example.json",
    "docs/contributing/contribution-credit.schema.json",
    "docs/contributing/deliberation-protocol.md",
    "docs/contributing/deliberation-record.open.example.json",
    "docs/contributing/deliberation-record.resolved.example.json",
    "docs/contributing/deliberation-record.schema.json",
    "docs/contributing/recognition-policy.md",
    "docs/contributing/task-hall.md",
    "docs/contributing/task-hall.v1.json",
    "docs/contributing/task-hall.v1.schema.json",
    "docs/contributing/task-contract.schema.json",
    "docs/contributing/task-contract.example.json",
    "docs/contributing/work-continuation.schema.json",
    "docs/contributing/work-continuation.example.json",
    "docs/contributing/tasks/task-registry.json",
    "docs/contributing/tasks/task-registry.schema.json",
    "docs/contributing/label-contract.json",
    "docs/device-lab/README.md",
    "docs/device-lab/capability-test-plan.schema.json",
    "docs/device-lab/device-profile.schema.json",
    "docs/device-lab/device-receipt.schema.json",
    "docs/device-lab/device-receipt.template.json",
    "docs/device-lab/receipts/README.md",
    "docs/device-lab/test-plans/inventory-world-space-ui-v1.json",
    "docs/rfcs/0002-public-workbench.md",
    "docs/validation/fail-fast-validation.md",
    "docs/validation/independent-review-receipt.example.json",
    "docs/validation/independent-review-receipt.schema.json",
    "scripts/contract-requirements.txt",
}
REQUIRED_FOUNDRY_FILES = {
    "docs/foundry/README.md",
    "docs/foundry/release-policy.md",
    "docs/foundry/source-manifest.json",
    "docs/foundry/foundry-manifest.json",
    "docs/foundry/foundry-manifest.schema.json",
    "docs/foundry/unity-package-blueprint.example.json",
    "docs/foundry/unity-package-blueprint.schema.json",
    "docs/foundry/batches/unity-first-batch.v1.json",
    "docs/foundry/batches/unity-first-batch.schema.json",
    "docs/foundry/batches/batch-registry.v1.json",
    "docs/foundry/batches/batch-registry.schema.json",
    "docs/foundry/batches/package-batch.schema.json",
    "docs/foundry/batches/unity-next-systems.v1.json",
    "docs/foundry/queue/next-batch.json",
    "docs/foundry/queue/next-batch.schema.json",
    "docs/rfcs/0003-foundry-production-line.md",
    "scripts/scaffold_unity_package.py",
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
UNITY_EDITOR_VERSION_PATTERN = re.compile(
    r"(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)[abfp](?:0|[1-9][0-9]*)"
)
EXACT_RUNTIME_VERSION_PATTERN = re.compile(
    r"(?:0|[1-9][0-9]*)(?:\.[0-9A-Za-z]+)+(?:[-+._][0-9A-Za-z]+)*"
)


def parse_semver_precedence(
    value: Any,
) -> tuple[tuple[int, int, int], tuple[str, ...] | None] | None:
    """Parse SemVer precedence while deliberately ignoring build metadata."""

    if not isinstance(value, str) or SEMVER_PATTERN.fullmatch(value) is None:
        return None
    precedence = value.split("+", 1)[0]
    core, separator, prerelease = precedence.partition("-")
    try:
        major, minor, patch = (int(part) for part in core.split("."))
    except (TypeError, ValueError):
        return None
    return (major, minor, patch), tuple(prerelease.split(".")) if separator else None


def compare_semver_precedence(left: Any, right: Any) -> int | None:
    parsed_left = parse_semver_precedence(left)
    parsed_right = parse_semver_precedence(right)
    if parsed_left is None or parsed_right is None:
        return None
    left_core, left_pre = parsed_left
    right_core, right_pre = parsed_right
    if left_core != right_core:
        return 1 if left_core > right_core else -1
    if left_pre is None or right_pre is None:
        if left_pre is right_pre:
            return 0
        return 1 if left_pre is None else -1
    for left_part, right_part in zip(left_pre, right_pre):
        if left_part == right_part:
            continue
        left_numeric = left_part.isdigit()
        right_numeric = right_part.isdigit()
        if left_numeric and right_numeric:
            return 1 if int(left_part) > int(right_part) else -1
        if left_numeric != right_numeric:
            return -1 if left_numeric else 1
        return 1 if left_part > right_part else -1
    if len(left_pre) == len(right_pre):
        return 0
    return 1 if len(left_pre) > len(right_pre) else -1


def unity_external_dependency_is_satisfied(declared: Any, resolved: Any) -> bool:
    """Accept Unity's exact resolved node when it satisfies a SemVer edge request."""

    parsed_declared = parse_semver_precedence(declared)
    parsed_resolved = parse_semver_precedence(resolved)
    if parsed_declared is None or parsed_resolved is None:
        return False
    if parsed_declared[1] is None and parsed_resolved[1] is not None:
        return False
    comparison = compare_semver_precedence(resolved, declared)
    return comparison is not None and comparison >= 0
TASK_HALL_VERSION = "0.3.0"
TASK_HALL_REGISTRY_PATH = "docs/contributing/tasks/task-registry.json"
TASK_HALL_PROJECT = "https://github.com/users/Lingkyn/projects/2"
TASK_HALL_TASKS_DIR = "docs/contributing/tasks"
TASK_HALL_UMBRELLA_STATES = [
    "proposal",
    "source_gate",
    "ready",
    "active",
    "waiting",
    "integration",
    "closed",
    "cancelled",
]
TASK_HALL_CHECKPOINT_STATES = [
    "draft",
    "source_gate",
    "ready",
    "claimed",
    "in_progress",
    "waiting_on_author",
    "waiting_on_review",
    "waiting_on_device",
    "integrating",
    "completed",
    "cancelled",
]
TASK_HALL_WAITING_STATES = [
    "waiting_on_author",
    "waiting_on_review",
    "waiting_on_device",
]
TASK_HALL_TERMINAL_CHECKPOINT_STATES = ["completed", "cancelled"]
TASK_HALL_CHECKPOINT_POLICY = {
    "graph": "directed_acyclic_graph",
    "ids_unique_within_task": True,
    "dependencies_complete_before_ready": True,
    "one_outcome_per_checkpoint": True,
    "allowed_paths_required": True,
    "read_only_checkpoint_uses_empty_allowed_paths": True,
    "source_gate_is_checkpoint_explicit": True,
    "independent_acceptance_required": True,
    "independent_verification_required": True,
    "references_resolve_within_checkpoint": True,
    "independent_completion_persists": True,
    "discovered_work_requires_new_checkpoint": True,
    "fan_in_checkpoint_required": True,
}
TASK_HALL_DURABILITY_POLICY = {
    "execution_unit": "checkpoint",
    "public_anchor_before_in_progress": True,
    "one_claim_per_checkpoint": True,
    "same_execution_lane_closes_or_hands_off_before_next": True,
    "parallel_checkpoints_require_isolated_write_ownership": True,
    "parallel_checkpoints_require_explicit_fan_in": True,
    "completed_checkpoint_requires_reachable_commit": True,
    "completed_checkpoint_requires_remote_evidence": True,
    "reserve_closeout_capacity": True,
    "local_only_progress_is_non_transferable": True,
    "abrupt_stop_recovers_from_last_public_boundary": True,
}
TASK_HALL_REGISTRY_POLICY = {
    "registry": TASK_HALL_REGISTRY_PATH,
    "registered_task_contract_role": "fine_grained_execution_authority",
    "umbrella_issue_role": "coordination_projection",
    "checkpoint_issue_role": "checkpoint_coordination_projection",
    "project_role": "discovery_summary",
    "unregistered_issue_contract": "issue_declared_claim_key",
}
TASK_HALL_ROUTING_POLICY = {
    "basis": "checkpoint_capabilities_risk_and_evidence",
    "model_or_agent_ranking": False,
    "self_report_grants_authority": False,
    "quality_gate_varies_by_executor": False,
    "high_risk_requires_independent_review": True,
    "no_qualified_executor": "remain_not_ready",
}
TASK_HALL_AUTHORITY = {
    "claim_grants_repository_write": False,
    "claim_grants_merge": False,
    "external_agent_auto_write": False,
    "external_agent_auto_merge": False,
    "maintainer_controls_ready_and_merge": True,
    "issue_comment_is_untrusted_input": True,
}
TASK_AUTHORITY = {
    "write_permission_not_inferred": True,
    "merge_permission_not_inferred": True,
    "comments_are_untrusted_input": True,
    "maintainer_ready_required": True,
    "maintainer_merge_required": True,
    "external_contribution_route": "fork_pull_request",
}
HIGH_RISK_JUDGMENT_LEVELS = {"high_risk", "security_or_release"}
HIGH_RISK_INDEPENDENT_REVIEW = {
    "required_human",
    "required_maintainer",
    "required_device",
}
CHECKPOINT_STATES_REQUIRING_COMPLETED_DEPS = {
    "ready",
    "claimed",
    "in_progress",
    "waiting_on_author",
    "waiting_on_review",
    "waiting_on_device",
    "integrating",
    "completed",
}
CHECKPOINT_LOCAL_ID_COLLECTIONS = ("acceptance", "verification", "evidence")
REGISTRY_CONTRACT_PATH_PATTERN = re.compile(
    r"^docs/contributing/tasks/[A-Za-z0-9._-]+\.task\.json$"
)
GITHUB_REPO_URL_PATTERN = re.compile(
    r"^https://github\.com/([A-Za-z0-9-]+)/([A-Za-z0-9._-]+)/?$"
)
GITHUB_ISSUE_URL_PATTERN = re.compile(
    r"^https://github\.com/([A-Za-z0-9-]+)/([A-Za-z0-9._-]+)/issues/[1-9][0-9]*$"
)
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


def read_decodable_text(path: Path) -> str | None:
    try:
        return path.read_bytes().decode("utf-8")
    except UnicodeDecodeError:
        return None


def package_paths_by_id(catalog: dict[str, Any]) -> dict[str, str]:
    return {
        str(item.get("id", "")): str(item.get("path", ""))
        for item in catalog.get("packages", [])
        if isinstance(item, dict) and item.get("id") and item.get("path")
    }


def discover_lingkyn_package_paths(root: Path) -> set[str]:
    paths: set[str] = set()
    for manifest_path in root.rglob("package.json"):
        if ".git" in manifest_path.parts:
            continue
        try:
            manifest = load_json(manifest_path)
        except (json.JSONDecodeError, UnicodeDecodeError):
            continue
        if str(manifest.get("name", "")).startswith("com.lingkyn."):
            paths.add(manifest_path.parent.relative_to(root).as_posix())
    return paths


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
            if path.suffix.lower() in STRICT_TEXT_SUFFIXES:
                errors.append(
                    f"undecodable controlled text file: {path.relative_to(root)}"
                )
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


def validate_active_repository_path_references(root: Path) -> list[str]:
    errors: list[str] = []
    old_reference_patterns = {
        "old root Git UPM path": re.compile(r"\?path=/?com\.lingkyn", re.IGNORECASE),
        "old root GitHub source link": re.compile(
            r"github\.com/Lingkyn/xr-foundry/(?:tree|blob)/main/com\.lingkyn",
            re.IGNORECASE,
        ),
        "old root local file path": re.compile(
            r"file:(?:\.\./)+xr-foundry/com\.lingkyn", re.IGNORECASE
        ),
        "old root relative package link": re.compile(r"\]\(com\.lingkyn[^)]*/\)"),
        "old root catalog path": re.compile(r'''["']path["']\s*:\s*["']com\.lingkyn'''),
    }
    for path in root.rglob("*"):
        if not path.is_file() or ".git" in path.parts:
            continue
        relative = path.relative_to(root)
        if path.suffix.lower() not in TEXT_SUFFIXES:
            continue
        text = path.read_text(encoding="utf-8", errors="replace")
        for label, pattern in old_reference_patterns.items():
            if pattern.search(text):
                errors.append(f"{label} in active repository surface: {relative.as_posix()}")
    return errors


def validate_active_git_upm_selectors(
    root: Path, catalog: dict[str, Any]
) -> list[str]:
    errors: list[str] = []
    catalog_paths = package_paths_by_id(catalog)
    selector_pattern = re.compile(
        r"(?<!\\)\?path=(/[A-Za-z0-9._-]+(?:/[A-Za-z0-9._-]+)*)"
        r"(?=[#\s\"'`<>,)\]}]|$)"
    )
    for path in root.rglob("*"):
        if not path.is_file() or ".git" in path.parts:
            continue
        relative = path.relative_to(root)
        text = read_decodable_text(path)
        if text is None:
            continue
        for selected_path in selector_pattern.findall(text):
            normalized = selected_path.rstrip("/")
            package_id = Path(normalized).name
            expected_path = catalog_paths.get(package_id)
            if expected_path is None:
                errors.append(
                    f"Git UPM selector names an undeclared package in {relative.as_posix()}: "
                    f"{selected_path}"
                )
                continue
            canonical = f"/{expected_path}"
            if normalized != canonical:
                errors.append(
                    f"Git UPM selector path drift for {package_id} in {relative.as_posix()}: "
                    f"selector={normalized} catalog={canonical}"
                )
    return errors


def validate_readme_git_install_matrix(
    root: Path, catalog: dict[str, Any]
) -> list[str]:
    readme_path = root / "README.md"
    if not readme_path.exists():
        return ["README Git install matrix is missing"]

    errors: list[str] = []
    catalog_paths = package_paths_by_id(catalog)
    entry_pattern = re.compile(
        r'"(com\.lingkyn\.[A-Za-z0-9_.-]+)"\s*:\s*'
        r'"(https://github\.com/Lingkyn/xr-foundry\.git\?path=[^"]+)"'
    )
    entries = entry_pattern.findall(readme_path.read_text(encoding="utf-8"))
    entry_ids = [package_id for package_id, _ in entries]
    if len(entry_ids) != len(set(entry_ids)):
        errors.append("README Git install matrix contains duplicate package ids")
    entry_urls = dict(entries)
    if set(entry_urls) != set(catalog_paths):
        errors.append(
            "README Git install matrix must be derived from every package catalog entry: "
            f"readme={sorted(entry_urls)} catalog={sorted(catalog_paths)}"
        )

    url_pattern = re.compile(
        r"^https://github\.com/Lingkyn/xr-foundry\.git\?path="
        r"(?P<path>/[A-Za-z0-9._/-]+)#(?P<revision>[^\s]+)$"
    )
    revisions: dict[str, str] = {}
    for package_id, url in entries:
        match = url_pattern.fullmatch(url)
        if match is None:
            errors.append(f"README Git install URL is malformed for {package_id}")
            continue
        expected_path = f"/{catalog_paths.get(package_id, '')}"
        if match.group("path") != expected_path:
            errors.append(
                f"README Git install path drift for {package_id}: "
                f"readme={match.group('path')} catalog={expected_path}"
            )
        revisions[package_id] = match.group("revision")

    placeholder_anchor = "<full-40-character-commit-sha>"
    placeholder_sibling = "<same-full-40-character-commit-sha>"
    revision_values = list(revisions.values())
    placeholder_values = {placeholder_anchor, placeholder_sibling}
    if revision_values and set(revision_values) <= placeholder_values:
        if revision_values.count(placeholder_anchor) != 1:
            errors.append("README Git install matrix must contain one full-SHA placeholder anchor")
        if len(revision_values) > 1 and revision_values.count(placeholder_sibling) != len(revision_values) - 1:
            errors.append("README Git install siblings must use the same-full-SHA placeholder")
    elif revision_values:
        if any(re.fullmatch(r"[0-9a-fA-F]{40}", value) is None for value in revision_values):
            errors.append("README Git install revisions must be full 40-character Git SHAs")
        elif len(set(value.casefold() for value in revision_values)) != 1:
            errors.append("README Git install siblings must pin the same full Git SHA")

    for package_id, package_path in catalog_paths.items():
        manifest_path = root / package_path / "package.json"
        if not manifest_path.exists():
            continue
        dependencies = load_json(manifest_path).get("dependencies", {})
        for dependency_id in dependencies:
            dependency_id = str(dependency_id)
            if not dependency_id.startswith("com.lingkyn."):
                continue
            if dependency_id not in catalog_paths:
                errors.append(
                    f"{package_id}: custom sibling dependency is absent from package catalog: "
                    f"{dependency_id}"
                )
            if package_id in entry_urls and dependency_id not in entry_urls:
                errors.append(
                    f"README Git install matrix is not dependency-closed: "
                    f"{package_id} requires {dependency_id}"
                )
    return errors


def validate_internal_namespace_links(
    package_root: Path, package_roots: dict[str, Path] | None = None
) -> list[str]:
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
            dependency_root = (
                package_roots.get(str(package_id), package_root.parent / str(package_id))
                if package_roots is not None
                else package_root.parent / str(package_id)
            )
            for source in dependency_root.rglob("*.cs") if dependency_root.exists() else []:
                declarations.update(re.findall(r"\bnamespace\s+(Lingkyn\.[A-Za-z0-9_.]+)", source.read_text(encoding="utf-8")))
    errors: list[str] = []
    for source, imported in imports:
        if not any(namespace == imported or namespace.startswith(imported + ".") for namespace in declarations):
            errors.append(f"{source.relative_to(package_root)}: internal namespace has no source declaration: {imported}")
    return errors


def validate_unity_asset_path_literals(
    package_root: Path, known_package_ids: set[str]
) -> list[str]:
    errors: list[str] = []
    quoted_path = re.compile(r'''["']([^"']*[Pp]ackages/[^"']*)["']''')
    for source in package_root.rglob("*.cs"):
        text = source.read_text(encoding="utf-8")
        for value in quoted_path.findall(text):
            normalized = value.replace("\\", "/")
            lowered = normalized.casefold()
            if "packages/unity/" in lowered:
                errors.append(
                    f"{source.relative_to(package_root)}: repository path used as a Unity asset path: {value}"
                )
                continue
            marker = lowered.find("packages/")
            if marker < 0:
                continue
            mounted = normalized[marker:]
            if not mounted.startswith("Packages/"):
                errors.append(
                    f"{source.relative_to(package_root)}: Unity asset path must start with Packages/: {value}"
                )
                continue
            segments = mounted.split("/")
            if len(segments) < 2:
                errors.append(f"{source.relative_to(package_root)}: incomplete Unity package asset path: {value}")
                continue
            mounted_package = segments[1]
            if mounted_package.startswith("com.lingkyn.") and mounted_package not in known_package_ids:
                errors.append(
                    f"{source.relative_to(package_root)}: Unity asset path names an undeclared package: {mounted_package}"
                )
    return errors


def validate_reference_evidence_paths(root: Path, reference_catalog: dict) -> list[str]:
    errors: list[str] = []
    for artifact in reference_catalog.get("artifacts", []):
        if not isinstance(artifact, dict):
            continue
        artifact_id = str(artifact.get("id", ""))
        for evidence_path in artifact.get("evidence", []):
            if not isinstance(evidence_path, str) or not evidence_path.strip():
                errors.append(f"{artifact_id}: reference evidence paths must be non-empty strings")
                continue
            if not (root / evidence_path).exists():
                errors.append(f"{artifact_id}: reference evidence path does not exist: {evidence_path}")
    return errors


def validate_repository_layout(root: Path, catalog: dict[str, Any]) -> list[str]:
    errors: list[str] = []
    layout_path = root / "docs" / "architecture" / "repository-layout.v1.json"
    if not layout_path.exists():
        return ["Repository layout contract is missing"]

    layout = load_json(layout_path)
    if layout.get("schema") != "xr-foundry.repository_layout.v1":
        errors.append("Repository layout schema is invalid")
    if layout.get("status") != "accepted_initialization_architecture":
        errors.append("Repository layout must be the accepted initialization architecture")
    if layout.get("package_root") != "packages":
        errors.append("Repository package root must be packages")

    catalog_paths = package_paths_by_id(catalog)
    if len(catalog_paths) != len(catalog.get("packages", [])):
        errors.append("Package catalog ids and paths must be unique and non-empty")

    collection_ids: set[str] = set()
    collection_paths: set[str] = set()
    layout_packages: dict[str, str] = {}
    for collection in layout.get("collections", []):
        if not isinstance(collection, dict):
            errors.append("Repository layout collections must be objects")
            continue
        collection_id = str(collection.get("id", ""))
        collection_path = str(collection.get("path", ""))
        if not collection_id or collection_id in collection_ids:
            errors.append(f"Repository layout collection id is missing or duplicated: {collection_id}")
        collection_ids.add(collection_id)
        if not collection_path or collection_path in collection_paths:
            errors.append(f"Repository layout collection path is missing or duplicated: {collection_path}")
        collection_paths.add(collection_path)
        if not collection_path.startswith("packages/") or ".." in Path(collection_path).parts:
            errors.append(f"Repository layout collection path is unsafe: {collection_path}")
        if not (root / collection_path).is_dir():
            errors.append(f"Repository layout collection does not exist: {collection_path}")
        for package_id in collection.get("packages", []):
            package_id = str(package_id)
            if not package_id or package_id in layout_packages:
                errors.append(f"Repository layout package is missing or duplicated: {package_id}")
                continue
            layout_packages[package_id] = f"{collection_path}/{package_id}"

    if set(layout_packages) != set(catalog_paths):
        errors.append(
            "Repository layout/catalog package ids differ: "
            f"layout={sorted(layout_packages)} catalog={sorted(catalog_paths)}"
        )
    for package_id, expected_path in layout_packages.items():
        actual_path = catalog_paths.get(package_id)
        if actual_path != expected_path:
            errors.append(
                f"Repository layout path mismatch for {package_id}: "
                f"layout={expected_path} catalog={actual_path}"
            )
        if Path(expected_path).name != package_id:
            errors.append(f"Repository package leaf must equal package id: {expected_path}")

    engine_roots = layout.get("engine_roots")
    if not isinstance(engine_roots, dict) or engine_roots != {"unity": "packages/unity"}:
        errors.append("Repository layout may advertise only the implemented Unity engine root")
    declared_engines = set(engine_roots) if isinstance(engine_roots, dict) else set()
    actual_engine_roots = {
        path.name
        for path in (root / "packages").iterdir()
        if path.is_dir()
    } if (root / "packages").is_dir() else set()
    if actual_engine_roots != declared_engines:
        errors.append(
            "Repository engine roots differ from the layout contract: "
            f"actual={sorted(actual_engine_roots)} declared={sorted(declared_engines)}"
        )

    live_paths = discover_lingkyn_package_paths(root)
    if live_paths != set(catalog_paths.values()):
        errors.append(
            "Repository contains missing, duplicate, or undeclared Lingkyn package manifests: "
            f"catalog={sorted(catalog_paths.values())} live={sorted(live_paths)}"
        )
    root_legacy_paths = sorted(path.name for path in root.glob("com.lingkyn.*") if path.is_dir())
    if root_legacy_paths:
        errors.append(f"Old root package paths are not allowed: {root_legacy_paths}")

    invariants = layout.get("invariants", {})
    required_invariants = {
        "leaf_directory_equals_package_id": True,
        "landing_page_groups_package_families": True,
        "machine_catalog_keeps_package_entries": True,
        "consumer_asset_path_uses_package_id": True,
        "git_url_path_precedes_revision": True,
        "full_commit_sha_required": True,
        "old_path_compatibility_layers_allowed": False,
        "empty_future_engine_roots_allowed": False,
    }
    if invariants != required_invariants:
        errors.append("Repository layout invariants are incomplete or have drifted")
    return errors


def validate_bug_template_package_options(root: Path, catalog: dict[str, Any]) -> list[str]:
    template_path = root / ".github" / "ISSUE_TEMPLATE" / "bug.yml"
    if not template_path.exists():
        return ["Bug report issue template is missing"]
    package_options = set(
        re.findall(
            r"^\s*-\s+(com\.lingkyn\.[A-Za-z0-9_.-]+)\s*$",
            template_path.read_text(encoding="utf-8"),
            re.MULTILINE,
        )
    )
    catalog_ids = set(package_paths_by_id(catalog))
    if package_options != catalog_ids:
        return [
            "Bug report package options differ from the package catalog: "
            f"template={sorted(package_options)} catalog={sorted(catalog_ids)}"
        ]
    return []


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


def normalize_github_repository(url: str | None) -> str | None:
    if not isinstance(url, str):
        return None
    match = GITHUB_REPO_URL_PATTERN.fullmatch(url.rstrip("/"))
    if match is None:
        match = GITHUB_ISSUE_URL_PATTERN.fullmatch(url)
        if match is None:
            return None
    return f"https://github.com/{match.group(1)}/{match.group(2)}"


def is_safe_registry_contract_path(relative_path: str) -> bool:
    if not isinstance(relative_path, str) or not REGISTRY_CONTRACT_PATH_PATTERN.fullmatch(
        relative_path
    ):
        return False
    candidate = Path(relative_path)
    if candidate.is_absolute() or ".." in candidate.parts:
        return False
    return candidate.as_posix() == relative_path


def policy_matches(actual: Any, expected: dict[str, Any], label: str) -> list[str]:
    errors: list[str] = []
    if not isinstance(actual, dict):
        return [f"{label} is missing"]
    if set(actual) != set(expected):
        errors.append(f"{label} fields are incomplete")
    for field, required in expected.items():
        if actual.get(field) != required:
            errors.append(f"{label} must keep {field}={required!r}")
    return errors


def checkpoint_dependency_cycles(checkpoints_by_id: dict[str, dict[str, Any]]) -> list[str]:
    visiting: set[str] = set()
    visited: set[str] = set()
    cyclic: list[str] = []

    def visit(node: str) -> None:
        if node in visited:
            return
        if node in visiting:
            cyclic.append(node)
            return
        visiting.add(node)
        checkpoint = checkpoints_by_id.get(node, {})
        deps = checkpoint.get("depends_on", [])
        if isinstance(deps, list):
            for dep in deps:
                if isinstance(dep, str) and dep in checkpoints_by_id:
                    visit(dep)
        visiting.remove(node)
        visited.add(node)

    for checkpoint_id in checkpoints_by_id:
        visit(checkpoint_id)
    return cyclic


def checkpoint_transitive_dependencies(
    checkpoint_id: str,
    checkpoints_by_id: dict[str, dict[str, Any]],
) -> set[str]:
    ancestors: set[str] = set()
    pending = [checkpoint_id]
    seen: set[str] = set()
    while pending:
        node = pending.pop()
        if node in seen:
            continue
        seen.add(node)
        checkpoint = checkpoints_by_id.get(node, {})
        deps = checkpoint.get("depends_on", [])
        if not isinstance(deps, list):
            continue
        for dep in deps:
            if not isinstance(dep, str) or dep not in checkpoints_by_id:
                continue
            if dep not in ancestors:
                ancestors.add(dep)
                pending.append(dep)
    return ancestors


def checkpoints_have_dependency_ordering(
    left_id: str,
    right_id: str,
    checkpoints_by_id: dict[str, dict[str, Any]],
) -> bool:
    left_ancestors = checkpoint_transitive_dependencies(left_id, checkpoints_by_id)
    if right_id in left_ancestors:
        return True
    right_ancestors = checkpoint_transitive_dependencies(right_id, checkpoints_by_id)
    return left_id in right_ancestors


# Portable ownership-key contract for repository-relative allowed_paths.
# Stored form must already be canonical POSIX; * and ? remain glob operators only.
# Windows officially reserves superscript COM¹/²/³ and LPT¹/²/³ device stems
# (ISO/IEC 8859-1 digits) in every directory, including with extensions:
# https://learn.microsoft.com/windows/win32/fileio/naming-a-file
_WINDOWS_SUPERSCRIPT_DEVICE_DIGITS = ("¹", "²", "³")
_WINDOWS_RESERVED_DEVICE_STEMS = frozenset(
    {
        "con",
        "prn",
        "aux",
        "nul",
        *(f"com{index}" for index in range(1, 10)),
        *(f"lpt{index}" for index in range(1, 10)),
        *(f"com{digit}" for digit in _WINDOWS_SUPERSCRIPT_DEVICE_DIGITS),
        *(f"lpt{digit}" for digit in _WINDOWS_SUPERSCRIPT_DEVICE_DIGITS),
    }
)
_WINDOWS_INVALID_LITERAL_CHARS = frozenset("<>:\"|\\")


def allowed_path_ownership_key(pattern: str) -> str | None:
    """Return the portable ownership key, or None when storage form is unsafe.

    Safety, duplicate detection, matching, and overlap/intersection all share this
    key: reject all Unicode Other (C*) categories (Cc controls, Cf format/bidi,
    Cs/Co/Cn), outer whitespace, relative backslashes, colon/ADS and other
    Windows-invalid literals (keeping * / ? as globs), trailing segment
    dot/space, reserved device stems including COM1–9/LPT1–9 and superscript
    COM¹/²/³ and LPT¹/²/³ (with extensions), and non-NFC storage; then NFC
    before casefold.
    """
    if not isinstance(pattern, str) or not pattern:
        return None
    # Conservative portable rule: any Unicode General Category Other (C*).
    if any(unicodedata.category(char).startswith("C") for char in pattern):
        return None
    if pattern.strip() != pattern:
        return None
    if "\\" in pattern:
        return None
    nfc = unicodedata.normalize("NFC", pattern)
    if nfc != pattern:
        return None
    if any(char in _WINDOWS_INVALID_LITERAL_CHARS for char in pattern):
        return None
    if pattern.startswith("/") or pattern.startswith("//"):
        return None
    if re.match(r"^[A-Za-z]:", pattern) is not None:
        return None
    try:
        if Path(pattern).is_absolute() or PurePosixPath(pattern).is_absolute():
            return None
    except (OSError, RuntimeError, ValueError):
        return None
    parts = pattern.split("/")
    for part in parts:
        if part == "" or part == "." or part == "..":
            return None
        # Fail closed: ** is legal only as a complete path segment.
        if "**" in part and part != "**":
            return None
        if part.endswith(".") or part.endswith(" "):
            return None
        stem = part.split(".", 1)[0].casefold()
        if stem in _WINDOWS_RESERVED_DEVICE_STEMS:
            return None
    return nfc.casefold()


def normalize_allowed_path_pattern(pattern: str) -> str:
    """Return NFC text without rewriting noncanonical storage aliases."""
    if not isinstance(pattern, str):
        return ""
    return unicodedata.normalize("NFC", pattern)


def is_safe_repository_relative_allowed_path(pattern: str) -> bool:
    """Accept only canonical portable repository-relative allowed_paths."""
    return allowed_path_ownership_key(pattern) is not None


def tokenize_allowed_path_pattern(pattern: str) -> list[str]:
    key = allowed_path_ownership_key(pattern)
    if key is None:
        return []
    stripped = key.strip("/")
    if not stripped:
        return []
    return list(stripped.split("/"))


def casefold_allowed_path_pattern(pattern: str) -> str:
    key = allowed_path_ownership_key(pattern)
    return key if key is not None else ""


def segment_glob_matches_text(pattern: str, text: str) -> bool:
    """Match one concrete segment against a * / ? glob segment."""
    memo: dict[tuple[int, int], bool] = {}

    def dp(i: int, j: int) -> bool:
        key = (i, j)
        if key in memo:
            return memo[key]
        if i == len(pattern) and j == len(text):
            memo[key] = True
            return True
        if i == len(pattern):
            memo[key] = False
            return False
        if pattern[i] == "*":
            if dp(i + 1, j):
                memo[key] = True
                return True
            if j < len(text) and dp(i, j + 1):
                memo[key] = True
                return True
            memo[key] = False
            return False
        if j == len(text):
            memo[key] = False
            return False
        if pattern[i] == "?" or pattern[i] == text[j]:
            result = dp(i + 1, j + 1)
        else:
            result = False
        memo[key] = result
        return result

    return dp(0, 0)


def segment_globs_intersect(left: str, right: str) -> bool:
    """Return True when some single path segment matches both * / ? globs."""
    memo: dict[tuple[int, int], bool] = {}

    def dp(i: int, j: int) -> bool:
        key = (i, j)
        if key in memo:
            return memo[key]
        if i == len(left) and j == len(right):
            memo[key] = True
            return True

        left_star = i < len(left) and left[i] == "*"
        right_star = j < len(right) and right[j] == "*"
        if left_star and right_star:
            result = dp(i + 1, j) or dp(i, j + 1)
            memo[key] = result
            return result
        if left_star:
            if dp(i + 1, j):
                memo[key] = True
                return True
            if j < len(right):
                # Consume one concrete/? character from the right pattern.
                if dp(i, j + 1) or dp(i + 1, j + 1):
                    memo[key] = True
                    return True
            memo[key] = False
            return False
        if right_star:
            if dp(i, j + 1):
                memo[key] = True
                return True
            if i < len(left):
                if dp(i + 1, j) or dp(i + 1, j + 1):
                    memo[key] = True
                    return True
            memo[key] = False
            return False
        if i == len(left) or j == len(right):
            memo[key] = False
            return False
        left_char = left[i]
        right_char = right[j]
        if left_char == "?" or right_char == "?" or left_char == right_char:
            result = dp(i + 1, j + 1)
        else:
            result = False
        memo[key] = result
        return result

    return dp(0, 0)


def allowed_path_patterns_intersect(left_tokens: list[str], right_tokens: list[str]) -> bool:
    """Sound intersection for the supported path grammar (*, ?, and ** segments)."""
    memo: dict[tuple[int, int], bool] = {}

    def dp(i: int, j: int) -> bool:
        key = (i, j)
        if key in memo:
            return memo[key]
        if i == len(left_tokens) and j == len(right_tokens):
            memo[key] = True
            return True

        left_token = left_tokens[i] if i < len(left_tokens) else None
        right_token = right_tokens[j] if j < len(right_tokens) else None

        if left_token == "**":
            # Match zero segments, or consume one segment while keeping **.
            if dp(i + 1, j):
                memo[key] = True
                return True
            if j < len(right_tokens):
                result = dp(i, j + 1)
                memo[key] = result
                return result
            memo[key] = False
            return False

        if right_token == "**":
            if dp(i, j + 1):
                memo[key] = True
                return True
            if i < len(left_tokens):
                result = dp(i + 1, j)
                memo[key] = result
                return result
            memo[key] = False
            return False

        if left_token is None or right_token is None:
            memo[key] = False
            return False

        result = segment_globs_intersect(left_token, right_token) and dp(i + 1, j + 1)
        memo[key] = result
        return result

    return dp(0, 0)


def path_tokens_match_pattern(path_tokens: list[str], pattern_tokens: list[str]) -> bool:
    """Match concrete path tokens against the unified *, ?, ** segment grammar."""
    memo: dict[tuple[int, int], bool] = {}

    def dp(i: int, j: int) -> bool:
        key = (i, j)
        if key in memo:
            return memo[key]
        if i == len(path_tokens) and j == len(pattern_tokens):
            memo[key] = True
            return True

        pattern_token = pattern_tokens[j] if j < len(pattern_tokens) else None
        if pattern_token == "**":
            if dp(i, j + 1):
                memo[key] = True
                return True
            if i < len(path_tokens) and dp(i + 1, j):
                memo[key] = True
                return True
            memo[key] = False
            return False

        if i == len(path_tokens) or pattern_token is None:
            memo[key] = False
            return False

        result = segment_glob_matches_text(pattern_token, path_tokens[i]) and dp(i + 1, j + 1)
        memo[key] = result
        return result

    return dp(0, 0)


def allowed_path_matches(path: str, pattern: str) -> bool:
    path_key = allowed_path_ownership_key(path)
    pattern_key = allowed_path_ownership_key(pattern)
    if path_key is None or pattern_key is None:
        return False
    return path_tokens_match_pattern(
        tokenize_allowed_path_pattern(path),
        tokenize_allowed_path_pattern(pattern),
    )


def allowed_paths_overlap(left: str, right: str) -> bool:
    # Fail closed: unsafe patterns cannot be proven disjoint.
    left_key = allowed_path_ownership_key(left)
    right_key = allowed_path_ownership_key(right)
    if left_key is None or right_key is None:
        return True
    if left_key == right_key:
        return True
    return allowed_path_patterns_intersect(
        tokenize_allowed_path_pattern(left),
        tokenize_allowed_path_pattern(right),
    )


def validate_checkpoint_allowed_paths(
    checkpoint: dict[str, Any],
    label: str,
) -> list[str]:
    errors: list[str] = []
    paths = checkpoint.get("allowed_paths")
    if not isinstance(paths, list):
        return errors
    seen_ownership_keys: set[str] = set()
    for path in paths:
        if not isinstance(path, str):
            continue
        ownership_key = allowed_path_ownership_key(path)
        if ownership_key is None:
            errors.append(
                f"{label}: allowed_path {path!r} is not a safe repository-relative path"
            )
            continue
        if ownership_key in seen_ownership_keys:
            errors.append(
                f"{label}: allowed_path {path!r} collides under portable ownership-key aliasing"
            )
        seen_ownership_keys.add(ownership_key)
    return errors


def collection_duplicate_local_ids(items: Any) -> list[str]:
    if not isinstance(items, list):
        return []
    seen: set[str] = set()
    duplicates: list[str] = []
    for item in items:
        if not isinstance(item, dict):
            continue
        item_id = item.get("id")
        if not isinstance(item_id, str) or not item_id:
            continue
        if item_id in seen and item_id not in duplicates:
            duplicates.append(item_id)
        seen.add(item_id)
    return duplicates


def validate_checkpoint_local_id_uniqueness(
    checkpoint: dict[str, Any],
    label: str,
) -> list[str]:
    errors: list[str] = []
    for collection_name in CHECKPOINT_LOCAL_ID_COLLECTIONS:
        duplicates = collection_duplicate_local_ids(checkpoint.get(collection_name))
        for item_id in duplicates:
            errors.append(
                f"{label}: duplicate {collection_name} id {item_id}"
            )
    return errors


def validate_device_evidence_references(
    device: Any,
    evidence: Any,
    label: str,
) -> list[str]:
    errors: list[str] = []
    if not isinstance(device, dict):
        return errors
    refs = device.get("evidence")
    if not isinstance(refs, list) or not refs:
        return errors
    evidence_items = evidence if isinstance(evidence, list) else []
    evidence_ids = {
        item.get("id")
        for item in evidence_items
        if isinstance(item, dict) and isinstance(item.get("id"), str)
    }
    for ref in refs:
        if not isinstance(ref, str) or ref not in evidence_ids:
            errors.append(
                f"{label}: device evidence reference {ref!r} does not resolve within the checkpoint"
            )
    return errors


def validate_parallel_checkpoint_write_ownership(
    checkpoints_by_id: dict[str, dict[str, Any]],
    label: str,
) -> list[str]:
    errors: list[str] = []
    concurrent_ids = sorted(
        checkpoint_id
        for checkpoint_id, checkpoint in checkpoints_by_id.items()
        if checkpoint.get("status") not in TASK_HALL_TERMINAL_CHECKPOINT_STATES
    )
    for index, left_id in enumerate(concurrent_ids):
        left = checkpoints_by_id[left_id]
        left_paths = left.get("allowed_paths")
        if not isinstance(left_paths, list) or not left_paths:
            continue
        for right_id in concurrent_ids[index + 1 :]:
            if checkpoints_have_dependency_ordering(left_id, right_id, checkpoints_by_id):
                continue
            right = checkpoints_by_id[right_id]
            right_paths = right.get("allowed_paths")
            if not isinstance(right_paths, list) or not right_paths:
                continue
            overlapping = False
            for left_path in left_paths:
                if not isinstance(left_path, str):
                    continue
                for right_path in right_paths:
                    if not isinstance(right_path, str):
                        continue
                    if allowed_paths_overlap(left_path, right_path):
                        overlapping = True
                        break
                if overlapping:
                    break
            if overlapping:
                errors.append(
                    f"{label}: concurrent checkpoints {left_id} and {right_id} "
                    "claim overlapping allowed_paths"
                )
    return errors


def validate_integration_fan_in(
    integration: Any,
    checkpoints_by_id: dict[str, dict[str, Any]],
    label: str,
) -> list[str]:
    errors: list[str] = []
    if not isinstance(integration, dict):
        return errors
    fan_in_id = integration.get("checkpoint_id")
    if not isinstance(fan_in_id, str) or fan_in_id not in checkpoints_by_id:
        return errors
    ancestors = checkpoint_transitive_dependencies(fan_in_id, checkpoints_by_id)
    for checkpoint_id, checkpoint in checkpoints_by_id.items():
        if checkpoint_id == fan_in_id:
            continue
        if checkpoint.get("status") == "cancelled":
            continue
        if checkpoint_id not in ancestors:
            errors.append(
                f"{label}: integration checkpoint {fan_in_id} must be downstream of "
                f"{checkpoint_id}"
            )
    return errors


def validate_checkpoint_routing(
    routing: Any,
    label: str,
    *,
    device: Any = None,
    evidence: Any = None,
    status: Any = None,
    root: Path | None = None,
) -> list[str]:
    errors: list[str] = []
    if not isinstance(routing, dict):
        return [f"{label}: routing is missing"]
    if routing.get("self_report_grants_authority") is not False:
        errors.append(f"{label}: self-authorizing routing is prohibited")
    if routing.get("agent_or_model_ranking_prohibited") is not True:
        errors.append(f"{label}: agent or model ranking must remain prohibited")
    if routing.get("no_qualified_executor") != "remain_not_ready":
        errors.append(f"{label}: unqualified execution must remain_not_ready")
    judgment = routing.get("judgment_level")
    review = routing.get("independent_review")
    if judgment in HIGH_RISK_JUDGMENT_LEVELS:
        capabilities = routing.get("required_capabilities")
        qualification = routing.get("qualification_evidence")
        if not isinstance(capabilities, list) or not capabilities:
            errors.append(f"{label}: high-risk routing requires required_capabilities")
        if not isinstance(qualification, list) or not qualification:
            errors.append(f"{label}: high-risk routing requires qualification_evidence")
        if review not in HIGH_RISK_INDEPENDENT_REVIEW:
            errors.append(
                f"{label}: unqualified high-risk execution requires independent review"
            )
        if status == "completed":
            evidence_items = evidence if isinstance(evidence, list) else []
            review_evidence = [
                item
                for item in evidence_items
                if isinstance(item, dict)
                and item.get("kind") == "review"
                and isinstance(item.get("location"), str)
                and bool(item.get("location"))
                and isinstance(item.get("commit"), str)
                and FULL_SHA_PATTERN.fullmatch(item.get("commit", "")) is not None
                and item.get("commit") != "0" * 40
                and commit_is_public_origin_reachable(
                    root or ROOT,
                    item.get("commit", ""),
                )
            ]
            if not review_evidence:
                errors.append(
                    f"{label}: completed high-risk checkpoint requires immutable review evidence"
                )
    if review == "required_device":
        required_devices = routing.get("required_devices")
        if not isinstance(required_devices, list) or not required_devices:
            errors.append(
                f"{label}: required_device review requires non-empty required_devices"
            )
        if not isinstance(device, dict) or device.get("required") is not True:
            errors.append(f"{label}: required_device review requires a coherent device gate")
        else:
            profiles = device.get("profiles")
            acceptance = device.get("acceptance")
            if not isinstance(profiles, list) or not profiles:
                errors.append(
                    f"{label}: required_device review requires device.profiles"
                )
            if not isinstance(acceptance, list) or not acceptance:
                errors.append(
                    f"{label}: required_device review requires device.acceptance"
                )
        if status == "completed":
            evidence_items = evidence if isinstance(evidence, list) else []
            evidence_by_id = {
                item.get("id"): item
                for item in evidence_items
                if isinstance(item, dict) and isinstance(item.get("id"), str)
            }
            device_evidence_ids = (
                device.get("evidence") if isinstance(device, dict) else None
            )
            if not isinstance(device_evidence_ids, list) or not device_evidence_ids:
                errors.append(
                    f"{label}: completed required_device checkpoint requires device evidence"
                )
            else:
                for evidence_id in device_evidence_ids:
                    item = evidence_by_id.get(evidence_id)
                    if not isinstance(item, dict) or item.get("kind") != "device":
                        errors.append(
                            f"{label}: completed required_device checkpoint requires device evidence"
                        )
                        break
    errors.extend(validate_device_evidence_references(device, evidence, label))
    return errors


def validate_independent_review_receipt(
    payload: Any,
    label: str,
    *,
    root: Path | None = None,
) -> list[str]:
    schema_path = (
        (root or ROOT)
        / "docs"
        / "validation"
        / "independent-review-receipt.schema.json"
    )
    errors = validate_json_schema_instance(payload, schema_path, label)
    if not isinstance(payload, dict) or payload.get("record_status") != "accepted":
        return errors

    executor = payload.get("executor")
    reviewer = payload.get("independent_review")
    executor_agents = {
        str(value)
        for value in executor.get("assisted_by", [])
    } if isinstance(executor, dict) else set()
    reviewer_agents = {
        str(value)
        for value in reviewer.get("assisted_by", [])
    } if isinstance(reviewer, dict) else set()
    if not executor_agents or not reviewer_agents or executor_agents & reviewer_agents:
        errors.append(
            f"{label}: accepted review requires disjoint executor and reviewer assistance"
        )

    reviewed_commit = payload.get("reviewed_commit")
    if root is not None and isinstance(reviewed_commit, str):
        if not commit_is_public_origin_reachable(root, reviewed_commit):
            errors.append(
                f"{label}: reviewed_commit must be reachable from a public origin ref"
            )
    return errors


def is_symlink_junction_or_reparse(path: Path) -> bool:
    try:
        if path.is_symlink():
            return True
        is_junction = getattr(path, "is_junction", None)
        if callable(is_junction) and is_junction():
            return True
        path_stat = path.lstat()
    except FileNotFoundError:
        return False
    except OSError:
        raise
    attributes = getattr(path_stat, "st_file_attributes", 0)
    # Windows FILE_ATTRIBUTE_REPARSE_POINT
    return bool(attributes & 0x400)


def resolve_controlled_tasks_root(
    root: Path,
    label: str,
) -> tuple[Path | None, Path | None, list[str]]:
    """Resolve repo + controlled tasks roots; reject link/reparse escapes fail-closed."""
    try:
        repo_root = root.resolve()
    except (OSError, RuntimeError) as error:
        return None, None, [f"{label}: repository root is unreadable: {error}"]

    try:
        cursor = root
        for part in PurePosixPath(TASK_HALL_TASKS_DIR).parts:
            cursor = cursor / part
            try:
                if is_symlink_junction_or_reparse(cursor):
                    return None, None, [
                        f"{label}: controlled tasks path component {cursor.as_posix()!r} "
                        "must not be a symlink, junction, or reparse point"
                    ]
            except (OSError, RuntimeError) as error:
                return None, None, [
                    f"{label}: controlled tasks directory is unreadable: {error}"
                ]
        tasks_root = (root / TASK_HALL_TASKS_DIR).resolve(strict=True)
    except (OSError, RuntimeError) as error:
        return None, None, [f"{label}: controlled tasks directory is unreadable: {error}"]

    try:
        tasks_root.relative_to(repo_root)
    except ValueError:
        return None, None, [
            f"{label}: controlled tasks directory resolves outside the repository root"
        ]
    return repo_root, tasks_root, []


def inspect_registered_contract_path(
    root: Path,
    relative_path: str,
    label: str,
) -> tuple[Path | None, list[str]]:
    errors: list[str] = []
    if not isinstance(relative_path, str) or not is_safe_registry_contract_path(relative_path):
        return None, [f"{label}: contract path is unsafe"]
    repo_root, tasks_root, root_errors = resolve_controlled_tasks_root(root, label)
    if root_errors or repo_root is None or tasks_root is None:
        return None, root_errors
    absolute = root / relative_path
    try:
        if is_symlink_junction_or_reparse(absolute):
            return None, [f"{label}: registered contract must not be a symlink"]
        if not absolute.exists():
            return None, [f"{label}: registered contract does not exist"]
        resolved = absolute.resolve(strict=True)
    except (OSError, RuntimeError) as error:
        return None, [f"{label}: registered contract is unreadable: {error}"]
    try:
        resolved.relative_to(repo_root)
    except ValueError:
        return None, [
            f"{label}: registered contract resolved path escapes the repository root"
        ]
    try:
        resolved.relative_to(tasks_root)
    except ValueError:
        return None, [
            f"{label}: registered contract resolved path escapes {TASK_HALL_TASKS_DIR}"
        ]
    try:
        if is_symlink_junction_or_reparse(resolved):
            return None, [f"{label}: registered contract must not be a symlink"]
        file_stat = resolved.stat()
    except (OSError, RuntimeError) as error:
        return None, [f"{label}: registered contract is unreadable: {error}"]
    if not stat.S_ISREG(file_stat.st_mode):
        return None, [f"{label}: registered contract must be a regular file"]
    if file_stat.st_nlink > 1:
        return None, [f"{label}: registered contract must not be a hardlink"]
    return resolved, errors


def validate_task_contract(
    payload: Any,
    label: str = "task contract",
    *,
    root: Path | None = None,
    require_canonical_repository: bool = False,
) -> list[str]:
    contract_root = ROOT if root is None else root
    errors = validate_json_schema_instance(
        payload,
        contract_root / "docs" / "contributing" / "task-contract.schema.json",
        label,
    )
    if not isinstance(payload, dict):
        return errors + [f"{label}: task contract must be an object"]
    if payload.get("schema") != "xr-foundry.task.v1":
        errors.append(f"{label}: schema is invalid")
    state = payload.get("state")
    if state not in TASK_HALL_UMBRELLA_STATES:
        errors.append(f"{label}: state must use the canonical Task Hall umbrella lifecycle")

    for field in ("scope", "non_goals", "checkpoints"):
        value = payload.get(field)
        if not isinstance(value, list) or not value:
            errors.append(f"{label}: {field} must be a non-empty list")

    authority = payload.get("authority")
    errors.extend(policy_matches(authority, TASK_AUTHORITY, f"{label}: authority boundary"))

    projection = payload.get("public_projection")
    repository = None
    umbrella = None
    if not isinstance(projection, dict):
        errors.append(f"{label}: public_projection is missing")
    else:
        repository = normalize_github_repository(str(projection.get("repository", "")))
        if repository is None:
            errors.append(f"{label}: public_projection.repository is invalid")
        elif require_canonical_repository and repository != PUBLIC_REPOSITORY:
            errors.append(f"{label}: registered task must use the canonical repository")
        if projection.get("contract_role") != "fine_grained_execution_authority":
            errors.append(f"{label}: public_projection.contract_role is invalid")
        if projection.get("issue_role") != "coordination_projection":
            errors.append(f"{label}: public_projection.issue_role is invalid")
        if projection.get("project_role") != "discovery_summary":
            errors.append(f"{label}: public_projection.project_role is invalid")
        project = projection.get("project")
        if require_canonical_repository:
            if project != TASK_HALL_PROJECT:
                errors.append(
                    f"{label}: public_projection.project must be the canonical Task Hall Project"
                )
        umbrella = projection.get("umbrella_issue")
        if isinstance(umbrella, str):
            umbrella_repo = normalize_github_repository(umbrella)
            if repository is not None and umbrella_repo != repository:
                errors.append(f"{label}: umbrella Issue belongs to a foreign repository")

    checkpoints = payload.get("checkpoints")
    checkpoints_by_id: dict[str, dict[str, Any]] = {}
    if isinstance(checkpoints, list):
        for index, checkpoint in enumerate(checkpoints):
            checkpoint_label = f"{label} checkpoint[{index}]"
            if not isinstance(checkpoint, dict):
                errors.append(f"{checkpoint_label}: must be an object")
                continue
            checkpoint_id = checkpoint.get("id")
            if not isinstance(checkpoint_id, str) or not checkpoint_id:
                errors.append(f"{checkpoint_label}: id is missing")
                continue
            if checkpoint_id in checkpoints_by_id:
                errors.append(f"{label}: duplicate checkpoint id {checkpoint_id}")
            checkpoints_by_id[checkpoint_id] = checkpoint
            status = checkpoint.get("status")
            if status not in TASK_HALL_CHECKPOINT_STATES:
                errors.append(
                    f"{label}: checkpoint {checkpoint_id} uses an invalid checkpoint lifecycle state"
                )
            checkpoint_label = f"{label}: checkpoint {checkpoint_id}"
            errors.extend(
                validate_checkpoint_local_id_uniqueness(checkpoint, checkpoint_label)
            )
            errors.extend(validate_checkpoint_allowed_paths(checkpoint, checkpoint_label))
            errors.extend(
                validate_checkpoint_routing(
                    checkpoint.get("routing"),
                    checkpoint_label,
                    device=checkpoint.get("device"),
                    evidence=checkpoint.get("evidence"),
                    status=status,
                    root=root,
                )
            )
            deps = checkpoint.get("depends_on", [])
            if isinstance(deps, list):
                for dep in deps:
                    if dep == checkpoint_id:
                        errors.append(
                            f"{label}: checkpoint {checkpoint_id} cannot depend on itself"
                        )

        known_ids = set(checkpoints_by_id)
        for checkpoint_id, checkpoint in checkpoints_by_id.items():
            deps = checkpoint.get("depends_on", [])
            if not isinstance(deps, list):
                continue
            for dep in deps:
                if not isinstance(dep, str):
                    continue
                if dep not in known_ids:
                    errors.append(
                        f"{label}: checkpoint {checkpoint_id} depends on missing {dep}"
                    )
            status = checkpoint.get("status")
            if status in CHECKPOINT_STATES_REQUIRING_COMPLETED_DEPS:
                for dep in deps:
                    if not isinstance(dep, str) or dep not in checkpoints_by_id:
                        continue
                    if checkpoints_by_id[dep].get("status") != "completed":
                        errors.append(
                            f"{label}: checkpoint {checkpoint_id} requires completed dependency {dep}"
                        )
        for node in checkpoint_dependency_cycles(checkpoints_by_id):
            errors.append(f"{label}: checkpoint dependency cycle involving {node}")
        errors.extend(
            validate_parallel_checkpoint_write_ownership(checkpoints_by_id, label)
        )

    if isinstance(projection, dict):
        projections = projection.get("checkpoint_issues")
        if not isinstance(projections, list) or not projections:
            errors.append(f"{label}: checkpoint Issue projections are missing")
        else:
            projected_ids: list[str] = []
            issue_urls: list[str] = []
            for index, entry in enumerate(projections):
                entry_label = f"{label} checkpoint_issues[{index}]"
                if not isinstance(entry, dict):
                    errors.append(f"{entry_label}: must be an object")
                    continue
                checkpoint_id = entry.get("checkpoint_id")
                issue = entry.get("issue")
                if not isinstance(checkpoint_id, str) or not checkpoint_id:
                    errors.append(f"{entry_label}: checkpoint_id is missing")
                else:
                    projected_ids.append(checkpoint_id)
                if not isinstance(issue, str) or not issue:
                    errors.append(f"{entry_label}: issue is missing")
                else:
                    issue_urls.append(issue)
                    issue_repo = normalize_github_repository(issue)
                    if repository is not None and issue_repo != repository:
                        errors.append(
                            f"{label}: checkpoint Issue projection uses a foreign repository"
                        )
                    if isinstance(umbrella, str) and issue == umbrella:
                        errors.append(
                            f"{label}: checkpoint Issue cannot equal umbrella Issue"
                        )
            if len(projected_ids) != len(set(projected_ids)):
                errors.append(f"{label}: duplicate checkpoint Issue projections")
            if len(issue_urls) != len(set(issue_urls)):
                errors.append(f"{label}: duplicate Issue projections")
            role_separated_urls = [
                *( [umbrella] if isinstance(umbrella, str) else [] ),
                *issue_urls,
            ]
            if len(role_separated_urls) != len(set(role_separated_urls)):
                errors.append(
                    f"{label}: umbrella and checkpoint Issues must form a unique role-separated set"
                )
            projected_set = set(projected_ids)
            checkpoint_ids = set(checkpoints_by_id)
            missing = checkpoint_ids - projected_set
            extra = projected_set - checkpoint_ids
            if missing:
                errors.append(
                    f"{label}: missing checkpoint Issue projections for {sorted(missing)}"
                )
            if extra:
                errors.append(
                    f"{label}: checkpoint Issue projections reference unknown {sorted(extra)}"
                )

    integration = payload.get("integration")
    if isinstance(integration, dict):
        fan_in_id = integration.get("checkpoint_id")
        if fan_in_id not in checkpoints_by_id:
            errors.append(f"{label}: integration.checkpoint_id must name an existing checkpoint")
        else:
            errors.extend(
                validate_integration_fan_in(integration, checkpoints_by_id, label)
            )

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
    return errors


def validate_work_continuation(
    payload: Any,
    label: str = "work continuation",
    *,
    root: Path | None = None,
) -> list[str]:
    continuation_root = ROOT if root is None else root
    return validate_json_schema_instance(
        payload,
        continuation_root / "docs" / "contributing" / "work-continuation.schema.json",
        label,
    )


def validate_task_registry(root: Path, payload: Any, label: str = "task registry") -> list[str]:
    errors = validate_json_schema_instance(
        payload,
        root / "docs" / "contributing" / "tasks" / "task-registry.schema.json",
        label,
    )
    if not isinstance(payload, dict):
        return errors + [f"{label}: registry must be an object"]
    if payload.get("schema") != "xr-foundry.task_registry.v1":
        errors.append(f"{label}: schema is invalid")
    coverage = payload.get("coverage")
    if not isinstance(coverage, dict) or coverage.get("mode") != "explicit_registration":
        errors.append(f"{label}: coverage.mode must be explicit_registration")
    authority = payload.get("authority")
    expected_authority = {
        "registered_record": "fine_grained_execution_authority",
        "umbrella_issue": "coordination_projection",
        "checkpoint_issue": "checkpoint_coordination_projection",
        "project": "discovery_summary",
    }
    errors.extend(policy_matches(authority, expected_authority, f"{label}: authority"))

    tasks = payload.get("tasks")
    if not isinstance(tasks, list) or not tasks:
        errors.append(f"{label}: tasks must be a non-empty list")
        return errors

    seen_ids: set[str] = set()
    seen_contracts: set[str] = set()
    seen_umbrellas: set[str] = set()
    seen_checkpoint_issues: set[str] = set()
    for index, entry in enumerate(tasks):
        entry_label = f"{label} tasks[{index}]"
        if not isinstance(entry, dict):
            errors.append(f"{entry_label}: must be an object")
            continue
        task_id = entry.get("task_id")
        contract_path = entry.get("contract")
        umbrella_issue = entry.get("umbrella_issue")
        state = entry.get("state")
        if not isinstance(task_id, str) or not task_id:
            errors.append(f"{entry_label}: task_id is missing")
            continue
        if task_id in seen_ids:
            errors.append(f"{label}: duplicate task id {task_id}")
        seen_ids.add(task_id)
        if state not in TASK_HALL_UMBRELLA_STATES:
            errors.append(f"{entry_label}: state must use the canonical umbrella lifecycle")
        if not isinstance(contract_path, str) or not is_safe_registry_contract_path(contract_path):
            errors.append(f"{entry_label}: contract path is unsafe")
            continue
        if contract_path in seen_contracts:
            errors.append(f"{label}: duplicate contract path {contract_path}")
        seen_contracts.add(contract_path)
        if isinstance(umbrella_issue, str):
            if umbrella_issue in seen_umbrellas:
                errors.append(f"{label}: duplicate umbrella Issue {umbrella_issue}")
            if umbrella_issue in seen_checkpoint_issues:
                errors.append(
                    f"{label}: umbrella Issue collides with a checkpoint Issue projection"
                )
            seen_umbrellas.add(umbrella_issue)
        contract_file, contract_errors = inspect_registered_contract_path(
            root, contract_path, entry_label
        )
        errors.extend(contract_errors)
        if contract_file is None:
            continue
        try:
            task_payload = load_json(contract_file)
        except (OSError, json.JSONDecodeError, UnicodeDecodeError) as error:
            errors.append(f"{entry_label}: registered contract is not valid JSON: {error}")
            continue
        errors.extend(
            validate_task_contract(
                task_payload,
                f"registered task {task_id}",
                root=root,
                require_canonical_repository=True,
            )
        )
        if task_payload.get("id") != task_id:
            errors.append(f"{entry_label}: contract id must equal registered task_id")
        if task_payload.get("state") != state:
            errors.append(f"{entry_label}: contract state must equal registered state")
        projection = task_payload.get("public_projection")
        if isinstance(projection, dict):
            if projection.get("umbrella_issue") != umbrella_issue:
                errors.append(
                    f"{entry_label}: contract umbrella Issue must equal registered umbrella Issue"
                )
            checkpoint_issues = projection.get("checkpoint_issues")
            if isinstance(checkpoint_issues, list):
                for issue_entry in checkpoint_issues:
                    if not isinstance(issue_entry, dict):
                        continue
                    issue_url = issue_entry.get("issue")
                    if not isinstance(issue_url, str):
                        continue
                    if issue_url in seen_checkpoint_issues:
                        errors.append(f"{label}: duplicate checkpoint Issue URL {issue_url}")
                    if issue_url in seen_umbrellas:
                        errors.append(
                            f"{label}: checkpoint Issue collides with an umbrella Issue"
                        )
                    seen_checkpoint_issues.add(issue_url)
    return errors


def validate_task_hall_authority(payload: Any) -> list[str]:
    errors: list[str] = []
    if not isinstance(payload, dict):
        return ["Task Hall authority contract must be an object"]
    if payload.get("schema") != "xr-foundry.task_hall.v1":
        errors.append("Task Hall authority schema is invalid")
    if payload.get("version") != TASK_HALL_VERSION:
        errors.append(f"Task Hall authority version must be {TASK_HALL_VERSION}")
    lifecycle = payload.get("lifecycle")
    if not isinstance(lifecycle, dict):
        errors.append("Task Hall lifecycle is missing")
    else:
        if lifecycle.get("umbrella_states") != TASK_HALL_UMBRELLA_STATES:
            errors.append("Task Hall umbrella lifecycle must keep the canonical V1 states")
        if lifecycle.get("checkpoint_states") != TASK_HALL_CHECKPOINT_STATES:
            errors.append("Task Hall checkpoint lifecycle must keep the canonical V1 states")
        if lifecycle.get("waiting_states") != TASK_HALL_WAITING_STATES:
            errors.append("Task Hall waiting lifecycle must keep the canonical V1 states")
        if lifecycle.get("terminal_checkpoint_states") != TASK_HALL_TERMINAL_CHECKPOINT_STATES:
            errors.append("Task Hall terminal checkpoint states must stay frozen")
    errors.extend(
        policy_matches(
            payload.get("checkpoint_policy"),
            TASK_HALL_CHECKPOINT_POLICY,
            "Task Hall checkpoint policy",
        )
    )
    errors.extend(
        policy_matches(
            payload.get("durability_policy"),
            TASK_HALL_DURABILITY_POLICY,
            "Task Hall durability policy",
        )
    )
    errors.extend(
        policy_matches(
            payload.get("registry_policy"),
            TASK_HALL_REGISTRY_POLICY,
            "Task Hall registry policy",
        )
    )
    errors.extend(
        policy_matches(
            payload.get("routing_policy"),
            TASK_HALL_ROUTING_POLICY,
            "Task Hall routing policy",
        )
    )
    errors.extend(policy_matches(payload.get("authority"), TASK_HALL_AUTHORITY, "Task Hall authority"))
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
        if claim_policy.get("scope") != "checkpoint":
            errors.append("Task Hall claims must stay scoped to a checkpoint")
    surfaces = payload.get("public_surfaces")
    expected_surfaces = {
        "rfc_discussion": "https://github.com/Lingkyn/xr-foundry/discussions/22",
        "task_hall_project": TASK_HALL_PROJECT,
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
    continuation_schema_path = root / "docs" / "contributing" / "work-continuation.schema.json"
    continuation_example_path = root / "docs" / "contributing" / "work-continuation.example.json"
    credit_schema_path = root / "docs" / "contributing" / "contribution-credit.schema.json"
    credit_example_path = root / "docs" / "contributing" / "contribution-credit.example.json"
    deliberation_schema_path = root / "docs" / "contributing" / "deliberation-record.schema.json"
    deliberation_open_path = root / "docs" / "contributing" / "deliberation-record.open.example.json"
    deliberation_resolved_path = root / "docs" / "contributing" / "deliberation-record.resolved.example.json"
    review_schema_path = root / "docs" / "validation" / "independent-review-receipt.schema.json"
    review_example_path = root / "docs" / "validation" / "independent-review-receipt.example.json"
    registry_path = root / TASK_HALL_REGISTRY_PATH
    registry_schema_path = root / "docs" / "contributing" / "tasks" / "task-registry.schema.json"
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
        if properties.get("version", {}).get("const") != TASK_HALL_VERSION:
            errors.append(f"Task Hall JSON Schema must freeze version={TASK_HALL_VERSION}")
        lifecycle_properties = properties.get("lifecycle", {}).get("properties", {})
        if lifecycle_properties.get("umbrella_states", {}).get("const") != TASK_HALL_UMBRELLA_STATES:
            errors.append("Task Hall JSON Schema must freeze the canonical umbrella lifecycle")
        if (
            lifecycle_properties.get("checkpoint_states", {}).get("const")
            != TASK_HALL_CHECKPOINT_STATES
        ):
            errors.append("Task Hall JSON Schema must freeze the canonical checkpoint lifecycle")
        for policy_name, expected in (
            ("durability_policy", TASK_HALL_DURABILITY_POLICY),
            ("registry_policy", TASK_HALL_REGISTRY_POLICY),
            ("routing_policy", TASK_HALL_ROUTING_POLICY),
            ("authority", TASK_HALL_AUTHORITY),
        ):
            policy_properties = properties.get(policy_name, {}).get("properties", {})
            for field, required in expected.items():
                if policy_properties.get(field, {}).get("const") != required:
                    errors.append(
                        f"Task Hall JSON Schema must freeze {policy_name}.{field}={required!r}"
                    )
    if schema_path.exists():
        schema = load_json(schema_path)
        authority_properties = (
            schema.get("$defs", {}).get("authority", {}).get("properties", {})
        )
        for field, required in TASK_AUTHORITY.items():
            if authority_properties.get(field, {}).get("const") != required:
                errors.append(f"Task schema must require {field}={required!r}")
        routing_properties = schema.get("$defs", {}).get("routing", {}).get("properties", {})
        if routing_properties.get("self_report_grants_authority", {}).get("const") is not False:
            errors.append("Task schema must forbid self-authorizing routing")
        if routing_properties.get("agent_or_model_ranking_prohibited", {}).get("const") is not True:
            errors.append("Task schema must prohibit agent or model ranking")
        if routing_properties.get("no_qualified_executor", {}).get("const") != "remain_not_ready":
            errors.append("Task schema must keep no_qualified_executor=remain_not_ready")
    if example_path.exists():
        example = load_json(example_path)
        errors.extend(validate_task_contract(example, "Task Hall example", root=root))
    if continuation_example_path.exists():
        continuation = load_json(continuation_example_path)
        errors.extend(
            validate_work_continuation(
                continuation, "Task Hall continuation example", root=root
            )
        )
        if continuation_schema_path.exists():
            errors.extend(
                validate_json_schema_instance(
                    continuation,
                    continuation_schema_path,
                    "Task Hall continuation example schema binding",
                )
            )
    if credit_schema_path.exists():
        try:
            Draft202012Validator.check_schema(load_json(credit_schema_path))
        except Exception as error:
            errors.append(f"Contribution credit JSON Schema is invalid: {error}")
    if credit_example_path.exists():
        credit_example = load_json(credit_example_path)
        errors.extend(
            validate_json_schema_instance(
                credit_example,
                credit_schema_path,
                "Contribution credit example schema binding",
            )
        )
        if credit_example.get("record_status") != "example":
            errors.append("Contribution credit example must not masquerade as accepted credit")
    for path, label, expected_status in (
        (deliberation_open_path, "Open deliberation example", "open"),
        (deliberation_resolved_path, "Resolved deliberation example", "resolved"),
    ):
        if path.exists():
            deliberation = load_json(path)
            errors.extend(
                validate_json_schema_instance(deliberation, deliberation_schema_path, label)
            )
            if deliberation.get("status") != expected_status:
                errors.append(f"{label}: status must remain {expected_status}")
    if review_schema_path.exists():
        try:
            Draft202012Validator.check_schema(load_json(review_schema_path))
        except Exception as error:
            errors.append(f"Independent review JSON Schema is invalid: {error}")
    if review_example_path.exists():
        review_example = load_json(review_example_path)
        errors.extend(
            validate_independent_review_receipt(
                review_example,
                "Independent review example",
                root=root,
            )
        )
        if review_example.get("record_status") != "example":
            errors.append("Independent review example must not masquerade as accepted review")
    review_records_root = root / "docs" / "validation" / "reviews"
    if review_records_root.exists():
        for path in sorted(review_records_root.glob("*.json")):
            review = load_json(path)
            errors.extend(
                validate_independent_review_receipt(
                    review,
                    f"Independent review receipt {path.name}",
                    root=root,
                )
            )
            if review.get("record_status") != "accepted":
                errors.append(
                    f"Independent review receipt {path.name}: live receipts must be accepted"
                )
    if registry_path.exists():
        registry_payload = load_json(registry_path)
        errors.extend(validate_task_registry(root, registry_payload, "Task Hall registry"))
        if registry_schema_path.exists():
            errors.extend(
                validate_json_schema_instance(
                    registry_payload,
                    registry_schema_path,
                    "Task Hall registry schema binding",
                )
            )
    elif TASK_HALL_REGISTRY_PATH in REQUIRED_AGENT_COMMONS_FILES:
        errors.append(f"Task Hall registry is missing: {TASK_HALL_REGISTRY_PATH}")
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


def validate_foundry_contract(root: Path) -> list[str]:
    errors: list[str] = []
    for relative in sorted(REQUIRED_FOUNDRY_FILES):
        if not (root / relative).exists():
            errors.append(f"Foundry V1 is missing {relative}")

    bindings = (
        (
            "docs/foundry/foundry-manifest.json",
            "docs/foundry/foundry-manifest.schema.json",
            "Foundry production-line manifest",
        ),
        (
            "docs/foundry/unity-package-blueprint.example.json",
            "docs/foundry/unity-package-blueprint.schema.json",
            "Foundry Unity blueprint example",
        ),
        (
            "docs/foundry/batches/unity-first-batch.v1.json",
            "docs/foundry/batches/unity-first-batch.schema.json",
            "Foundry first batch",
        ),
        (
            "docs/foundry/batches/batch-registry.v1.json",
            "docs/foundry/batches/batch-registry.schema.json",
            "Foundry batch registry",
        ),
        (
            "docs/foundry/batches/unity-next-systems.v1.json",
            "docs/foundry/batches/package-batch.schema.json",
            "Foundry next systems batch",
        ),
        (
            "docs/foundry/queue/next-batch.json",
            "docs/foundry/queue/next-batch.schema.json",
            "Foundry next-batch queue",
        ),
    )
    for instance_rel, schema_rel, label in bindings:
        instance_path = root / instance_rel
        schema_path = root / schema_rel
        if instance_path.exists():
            errors.extend(
                validate_json_schema_instance(load_json(instance_path), schema_path, label)
            )

    blueprint_path = root / "docs" / "foundry" / "unity-package-blueprint.example.json"
    if blueprint_path.exists():
        blueprint = load_json(blueprint_path)
        if blueprint.get("record_status") != "example":
            errors.append("Foundry blueprint example must not masquerade as admitted")
        package = blueprint.get("package")
        if isinstance(package, dict):
            package_id = package.get("id")
            target_path = package.get("target_path")
            if isinstance(package_id, str) and isinstance(target_path, str):
                if PurePosixPath(target_path).name != package_id:
                    errors.append("Foundry blueprint target leaf must equal package id")

    source_path = root / "docs" / "foundry" / "source-manifest.json"
    if source_path.exists():
        source = load_json(source_path)
        if source.get("schema") != "xr-foundry.foundry_source_manifest.v1":
            errors.append("Foundry source manifest schema is invalid")
        sources = source.get("sources")
        if not isinstance(sources, list) or not sources:
            errors.append("Foundry source manifest requires positive public sources")
        else:
            ids = [str(item.get("id", "")) for item in sources if isinstance(item, dict)]
            if len(ids) != len(set(ids)):
                errors.append("Foundry source manifest contains duplicate source ids")
            allowed_prefixes = (
                "https://docs.unity3d.com/",
                "https://docs.github.com/",
            )
            for item in sources:
                if not isinstance(item, dict):
                    errors.append("Foundry source entries must be objects")
                    continue
                url = item.get("url")
                claims = item.get("admitted_claims")
                if not isinstance(url, str) or not url.startswith(allowed_prefixes):
                    errors.append("Foundry V1 sources must use admitted official domains")
                if not isinstance(claims, list) or not claims:
                    errors.append("Foundry source entries require admitted claims")

    batch_registry_path = root / "docs" / "foundry" / "batches" / "batch-registry.v1.json"
    catalog_path = root / "package-catalog.json"
    profiles_path = root / "compatibility-profiles.json"
    if batch_registry_path.exists() and catalog_path.exists() and profiles_path.exists():
        registry = load_json(batch_registry_path)
        catalog = load_json(catalog_path)
        profiles = load_json(profiles_path)
        catalog_by_id = {
            str(item.get("id", "")): item
            for item in catalog.get("packages", [])
            if isinstance(item, dict)
        }
        profile_by_id = {
            str(item.get("id", "")): item
            for item in profiles.get("profiles", [])
            if isinstance(item, dict)
        }
        registry_items = registry.get("batches")
        if not isinstance(registry_items, list):
            registry_items = []
        registry_ids = [
            str(item.get("id", ""))
            for item in registry_items
            if isinstance(item, dict)
        ]
        if len(registry_ids) != len(set(registry_ids)):
            errors.append("Foundry batch registry contains duplicate batch ids")

        all_batch_ids: list[str] = []
        for registration in registry_items:
            if not isinstance(registration, dict):
                errors.append("Foundry batch registry entries must be objects")
                continue
            batch_id = str(registration.get("id", ""))
            relative_batch_path = str(registration.get("path", ""))
            batch_path = root / relative_batch_path
            if not batch_path.exists():
                errors.append(f"Foundry batch registry path is missing: {relative_batch_path}")
                continue
            batch = load_json(batch_path)
            if batch.get("schema") != "xr-foundry.package_batch.v1":
                errors.append(f"Foundry batch {batch_id}: schema is invalid")
            if batch.get("batch_id") != batch_id:
                errors.append(f"Foundry batch {batch_id}: registered id does not match batch file")
            expected_state = registration.get("state")
            actual_state = batch.get("status")
            if expected_state == "released":
                actual_state = "released" if actual_state == "approved_for_release" else actual_state
            if actual_state != expected_state:
                errors.append(f"Foundry batch {batch_id}: registry/file state mismatch")
            registered_tag = registration.get("release_tag")
            if registered_tag != batch.get("release", {}).get("tag"):
                errors.append(f"Foundry batch {batch_id}: registry/file release tag mismatch")

            batch_items = batch.get("packages")
            if not isinstance(batch_items, list):
                batch_items = []
            local_ids = [
                str(item.get("id", ""))
                for item in batch_items
                if isinstance(item, dict)
            ]
            if len(local_ids) != len(set(local_ids)):
                errors.append(f"Foundry batch {batch_id} contains duplicate package ids")
            all_batch_ids.extend(local_ids)

            for item in batch_items:
                if not isinstance(item, dict):
                    errors.append(f"Foundry batch {batch_id} package entries must be objects")
                    continue
                package_id = str(item.get("id", ""))
                catalog_item = catalog_by_id.get(package_id)
                if not isinstance(catalog_item, dict):
                    continue
                for field in ("path", "version", "maturity", "device_evidence"):
                    if item.get(field) != catalog_item.get(field):
                        errors.append(
                            f"Foundry batch {batch_id} {package_id}: {field} must match package catalog"
                        )
                manifest_path = root / str(item.get("path", "")) / "package.json"
                if manifest_path.exists():
                    manifest = load_json(manifest_path)
                    if manifest.get("name") != package_id or manifest.get("version") != item.get("version"):
                        errors.append(
                            f"Foundry batch {batch_id} {package_id}: package manifest identity/version drift"
                        )
                profile_id = str(item.get("compatibility_profile", ""))
                profile = profile_by_id.get(profile_id)
                if not isinstance(profile, dict) or profile.get("state") != "verified":
                    errors.append(
                        f"Foundry batch {batch_id} {package_id}: compatibility profile must exist and be verified"
                    )
                elif (
                    profile.get("install_artifact") != package_id
                    or profile.get("package_versions", {}).get(package_id)
                    != item.get("version")
                ):
                    errors.append(
                        f"Foundry batch {batch_id} {package_id}: compatibility profile identity/version mismatch"
                    )

        if len(all_batch_ids) != len(set(all_batch_ids)):
            errors.append("Foundry batch registry assigns a package to more than one batch")
        if set(all_batch_ids) != set(catalog_by_id):
            errors.append(
                "Foundry registered batches must cover every live catalog package exactly once"
            )

    queue_path = root / "docs" / "foundry" / "queue" / "next-batch.json"
    if queue_path.exists():
        queue = load_json(queue_path)
        candidates = queue.get("candidates")
        if isinstance(candidates, list):
            ids = [str(item.get("id", "")) for item in candidates if isinstance(item, dict)]
            if len(ids) != len(set(ids)):
                errors.append("Foundry next-batch queue contains duplicate candidate ids")
            if any(item.get("package_ids") for item in candidates if isinstance(item, dict)):
                errors.append("Foundry proposal/source-gate queue must not reserve package ids")
            for item in candidates:
                if not isinstance(item, dict):
                    continue
                controlled_text = [
                    item.get("title"),
                    item.get("objective"),
                    item.get("exact_next_action"),
                    *(item.get("source_requirements") or []),
                ]
                if any(
                    isinstance(value, str) and PLACEHOLDER_PATTERN.search(value)
                    for value in controlled_text
                ):
                    errors.append(
                        f"Foundry next-batch {item.get('id')}: placeholder text is prohibited"
                    )

    packages_root = root / "packages"
    if packages_root.exists():
        for marker in packages_root.rglob(".foundry-scaffold.json"):
            errors.append(
                f"Foundry staging scaffold cannot enter live packages: {marker.relative_to(root)}"
            )
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
            required_source_ids = target.get("required_input_source_ids")
            if (
                not isinstance(required_source_ids, list)
                or not required_source_ids
                or len(required_source_ids) != len(set(required_source_ids))
            ):
                errors.append(f"{label}: target {profile_id} requires unique input source IDs")
            else:
                profile_source_ids = {
                    str(item.get("id", ""))
                    for item in profile.get("input_sources", [])
                    if isinstance(item, dict)
                }
                unsupported_sources = set(required_source_ids) - profile_source_ids
                if unsupported_sources:
                    errors.append(
                        f"{label}: target {profile_id} uses profile-unsupported input sources: "
                        f"{sorted(unsupported_sources)}"
                    )

    execution_requirements = payload.get("execution_requirements")
    if not isinstance(execution_requirements, dict):
        errors.append(f"{label}: execution_requirements are required")
    else:
        minimum_duration = execution_requirements.get("minimum_duration_seconds")
        if not isinstance(minimum_duration, int) or isinstance(minimum_duration, bool) or minimum_duration < 120:
            errors.append(f"{label}: minimum_duration_seconds must be at least 120")
        postures = execution_requirements.get("allowed_postures")
        if (
            not isinstance(postures, list)
            or not postures
            or len(postures) != len(set(postures))
            or any(not isinstance(posture, str) or not posture for posture in postures)
        ):
            errors.append(f"{label}: allowed_postures must be a unique non-empty list")
        required_packages = execution_requirements.get("required_resolved_packages")
        if (
            not isinstance(required_packages, list)
            or not required_packages
            or len(required_packages) != len(set(required_packages))
            or any(not isinstance(package_id, str) or not package_id for package_id in required_packages)
        ):
            errors.append(f"{label}: required_resolved_packages must be a unique non-empty list")

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
        interaction_matrix = {
            f"{hand}-controller-{target}-target-{action}"
            for hand in ("left", "right")
            for target in ("left", "center", "right")
            for action in ("hover", "activate")
        }
        interaction_matrix.update(
            {
                "left-controller-target-isolation",
                "right-controller-target-isolation",
                "left-controller-disabled-no-mutation",
                "right-controller-disabled-no-mutation",
            }
        )
        missing_interaction_checks = interaction_matrix - check_ids
        if missing_interaction_checks:
            errors.append(
                f"{label}: controller interaction matrix is incomplete: "
                f"{sorted(missing_interaction_checks)}"
            )
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
        if any(artifact.get(field) is not None for field in ("sha256", "repository_path", "application_id")):
            errors.append(f"{label}: template must not contain artifact evidence")
        if payload.get("compatibility_profile_id") is not None:
            errors.append(f"{label}: template must not bind a compatibility profile")
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
    commit_sha = ""
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
        elif not commit_is_public_origin_reachable(ROOT, commit_sha):
            errors.append(
                f"{label}: revision commit_sha must resolve and be reachable from a fetched "
                "public origin ref"
            )
    artifact = payload.get("artifact")
    verified_artifact_path: Path | None = None
    if not isinstance(artifact, dict):
        errors.append(f"{label}: artifact identity is missing")
    else:
        artifact_kind = artifact.get("kind")
        if artifact_kind != "android-apk":
            errors.append(f"{label}: artifact.kind must be android-apk")
        file_name = artifact.get("file_name")
        expected_suffix = ".apk"
        if (
            not isinstance(file_name, str)
            or Path(file_name).name != file_name
            or not file_name.casefold().endswith(expected_suffix)
        ):
            errors.append(f"{label}: artifact.file_name must be an exact {expected_suffix} basename")
        digest = str(artifact.get("sha256", ""))
        if not SHA256_PATTERN.fullmatch(digest) or set(digest) == {"0"}:
            errors.append(f"{label}: non-zero artifact SHA-256 is required")
        repository_path = artifact.get("repository_path")
        artifact_path: Path | None = None
        if (
            not isinstance(repository_path, str)
            or not repository_path.startswith("docs/validation/evidence/")
            or "\\" in repository_path
            or ".." in Path(repository_path).parts
            or Path(repository_path).is_absolute()
        ):
            errors.append(f"{label}: artifact.repository_path must stay under public validation evidence")
        else:
            artifact_path = ROOT / repository_path
            evidence_root = (ROOT / "docs" / "validation" / "evidence").resolve()
            try:
                resolved_artifact = artifact_path.resolve(strict=True)
                resolved_artifact.relative_to(evidence_root)
            except (OSError, ValueError):
                resolved_artifact = None
                errors.append(f"{label}: artifact resolved path escapes validation evidence")
            if artifact_path.is_symlink():
                errors.append(f"{label}: artifact repository file must not be a symbolic link")
            if not artifact_path.is_file() or resolved_artifact is None:
                errors.append(f"{label}: artifact repository file does not exist")
            else:
                if artifact_path.name != file_name:
                    errors.append(f"{label}: artifact.file_name must match repository_path basename")
                artifact_size = artifact_path.stat().st_size
                if artifact_size <= 0:
                    errors.append(f"{label}: artifact repository file must be non-empty")
                if artifact_size > 4 * 1024 * 1024 * 1024:
                    errors.append(f"{label}: artifact repository file exceeds the 4 GiB evidence limit")
                with artifact_path.open("rb") as artifact_stream:
                    artifact_prefix = artifact_stream.read(64)
                    actual_digest: str | None = None
                    if artifact_size <= 4 * 1024 * 1024 * 1024:
                        actual_hash = hashlib.sha256()
                        actual_hash.update(artifact_prefix)
                        while chunk := artifact_stream.read(1024 * 1024):
                            actual_hash.update(chunk)
                        actual_digest = actual_hash.hexdigest()
                if artifact_prefix.startswith(b"version https://git-lfs.github.com/spec/v1"):
                    errors.append(f"{label}: artifact repository file must be materialized, not a Git LFS pointer")
                if not artifact_prefix.startswith(b"PK\x03\x04"):
                    errors.append(f"{label}: APK artifact must begin with a ZIP local-file header")
                if actual_digest is not None and actual_digest != digest:
                    errors.append(f"{label}: artifact SHA-256 does not match repository file")
                elif (
                    actual_digest is not None
                    and artifact_size > 0
                    and artifact_size <= 4 * 1024 * 1024 * 1024
                    and artifact_prefix.startswith(b"PK\x03\x04")
                    and not artifact_prefix.startswith(
                        b"version https://git-lfs.github.com/spec/v1"
                    )
                ):
                    verified_artifact_path = artifact_path
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

    dependency_resolution = payload.get("dependency_resolution")
    resolved_packages: dict[str, str] = {}
    manifest_dependencies: dict[str, str] = {}
    lock_dependencies: dict[str, Any] = {}
    if not isinstance(dependency_resolution, dict):
        errors.append(f"{label}: dependency_resolution is missing")
    else:
        manifest = dependency_resolution.get("manifest")
        if not isinstance(manifest, dict):
            errors.append(f"{label}: dependency manifest is missing")
        else:
            if manifest.get("format") != "unity-manifest-v1":
                errors.append(f"{label}: dependency manifest format must be unity-manifest-v1")
            if manifest.get("kind") != "repository_file":
                errors.append(f"{label}: dependency manifest must be a repository_file")
            manifest_ref = manifest.get("ref")
            manifest_path: Path | None = None
            if (
                not isinstance(manifest_ref, str)
                or not manifest_ref.startswith("docs/validation/evidence/")
                or manifest_ref.startswith(("/", "\\"))
                or re.match(r"^[A-Za-z]:", manifest_ref)
                or "\\" in manifest_ref
                or ".." in Path(manifest_ref).parts
            ):
                errors.append(f"{label}: dependency manifest repository path is unsafe")
            else:
                manifest_path = ROOT / manifest_ref
                try:
                    manifest_path.resolve(strict=True).relative_to(
                        (ROOT / "docs" / "validation" / "evidence").resolve()
                    )
                except (OSError, ValueError):
                    manifest_path = None
                    errors.append(f"{label}: dependency manifest resolved path escapes evidence")
                if manifest_path is None or not manifest_path.is_file():
                    errors.append(f"{label}: dependency manifest repository file does not exist")
                elif manifest_path.is_symlink():
                    errors.append(f"{label}: dependency manifest must not be a symbolic link")
            manifest_digest = str(manifest.get("sha256", ""))
            if not SHA256_PATTERN.fullmatch(manifest_digest) or set(manifest_digest) == {"0"}:
                errors.append(f"{label}: dependency manifest requires a non-zero SHA-256")
            elif manifest_path is not None and manifest_path.is_file():
                actual_digest = hashlib.sha256(manifest_path.read_bytes()).hexdigest()
                if manifest_digest != actual_digest:
                    errors.append(f"{label}: dependency manifest SHA-256 does not match repository file")
                try:
                    manifest_payload = load_json(manifest_path)
                except (json.JSONDecodeError, UnicodeDecodeError) as exc:
                    errors.append(f"{label}: dependency manifest is invalid JSON: {exc}")
                else:
                    raw_manifest_dependencies = (
                        manifest_payload.get("dependencies")
                        if isinstance(manifest_payload, dict)
                        else None
                    )
                    if not isinstance(raw_manifest_dependencies, dict) or not raw_manifest_dependencies:
                        errors.append(
                            f"{label}: dependency manifest must be a real Unity Packages/manifest.json"
                        )
                    elif any(
                        not isinstance(package_id, str) or not isinstance(selector, str)
                        for package_id, selector in raw_manifest_dependencies.items()
                    ):
                        errors.append(f"{label}: Unity manifest dependencies must be string mappings")
                    else:
                        manifest_dependencies = raw_manifest_dependencies
        lock = dependency_resolution.get("lock")
        if not isinstance(lock, dict):
            errors.append(f"{label}: dependency lock is missing")
        else:
            if lock.get("format") != "unity-packages-lock-v1":
                errors.append(f"{label}: dependency lock format must be unity-packages-lock-v1")
            if lock.get("kind") != "repository_file":
                errors.append(f"{label}: dependency lock must be a repository_file for verifiable evidence")
            lock_ref = lock.get("ref")
            lock_path: Path | None = None
            if (
                not isinstance(lock_ref, str)
                or not lock_ref.startswith("docs/validation/evidence/")
                or lock_ref.startswith(("/", "\\"))
                or re.match(r"^[A-Za-z]:", lock_ref)
                or "\\" in lock_ref
                or ".." in Path(lock_ref).parts
            ):
                errors.append(f"{label}: dependency lock repository path is unsafe")
            else:
                lock_path = ROOT / lock_ref
                try:
                    lock_path.resolve(strict=True).relative_to(
                        (ROOT / "docs" / "validation" / "evidence").resolve()
                    )
                except (OSError, ValueError):
                    lock_path = None
                    errors.append(f"{label}: dependency lock resolved path escapes evidence")
                if lock_path is None or not lock_path.is_file():
                    errors.append(f"{label}: dependency lock repository file does not exist")
                elif lock_path.is_symlink():
                    errors.append(f"{label}: dependency lock must not be a symbolic link")
            lock_digest = str(lock.get("sha256", ""))
            if not SHA256_PATTERN.fullmatch(lock_digest) or set(lock_digest) == {"0"}:
                errors.append(f"{label}: dependency lock requires a non-zero SHA-256")
            elif lock_path is not None and lock_path.is_file():
                actual_digest = hashlib.sha256(lock_path.read_bytes()).hexdigest()
                if lock_digest != actual_digest:
                    errors.append(f"{label}: dependency lock SHA-256 does not match repository file")
                try:
                    lock_payload = load_json(lock_path)
                except (json.JSONDecodeError, UnicodeDecodeError) as exc:
                    errors.append(f"{label}: dependency lock is invalid JSON: {exc}")
                else:
                    raw_lock_dependencies = (
                        lock_payload.get("dependencies")
                        if isinstance(lock_payload, dict)
                        else None
                    )
                    if not isinstance(raw_lock_dependencies, dict) or not raw_lock_dependencies:
                        errors.append(
                            f"{label}: dependency lock must be a real Unity packages-lock.json"
                        )
                    elif any(not isinstance(entry, dict) for entry in raw_lock_dependencies.values()):
                        errors.append(
                            f"{label}: every Unity lock dependency entry must be an object"
                        )
                    else:
                        lock_dependencies = raw_lock_dependencies
        raw_resolved_packages = dependency_resolution.get("resolved_packages")
        if not isinstance(raw_resolved_packages, list) or not raw_resolved_packages:
            errors.append(f"{label}: resolved package versions are required")
        else:
            for package in raw_resolved_packages:
                if not isinstance(package, dict):
                    errors.append(f"{label}: resolved packages must be objects")
                    continue
                package_id = str(package.get("id", ""))
                version = package.get("version")
                if not package_id or package_id in resolved_packages:
                    errors.append(f"{label}: resolved package id is missing or duplicated: {package_id}")
                if not isinstance(version, str) or SEMVER_PATTERN.fullmatch(version) is None:
                    errors.append(f"{label}: resolved package version must be exact: {package_id}")
                    continue
                resolved_packages[package_id] = version
        if isinstance(plan, dict):
            requirements = plan.get("execution_requirements", {})
            required_packages = (
                requirements.get("required_resolved_packages", [])
                if isinstance(requirements, dict)
                else []
            )
            missing_packages = set(required_packages) - set(resolved_packages)
            if missing_packages:
                errors.append(
                    f"{label}: plan-required resolved packages are missing: {sorted(missing_packages)}"
                )

    if lock_dependencies and manifest_dependencies and isinstance(package_tuple, dict) and commit_sha:
        package_catalog = load_json(ROOT / "package-catalog.json")
        catalog_paths = {
            str(item.get("id", "")): str(item.get("path", ""))
            for item in package_catalog.get("packages", [])
            if isinstance(item, dict) and item.get("id") and item.get("path")
        }
        custom_versions = {
            str(package.get("id", "")): str(package.get("version", ""))
            for role in ("domain", "presentation", "renderer_adapter", "xr_adapter")
            if isinstance(package := package_tuple.get(role), dict)
        }
        for package_id, expected_version in custom_versions.items():
            package_path = catalog_paths.get(package_id)
            entry = lock_dependencies.get(package_id)
            if not package_path or not isinstance(entry, dict):
                errors.append(f"{label}: Unity lock omits composition package: {package_id}")
                continue
            expected_selector = (
                "https://github.com/Lingkyn/xr-foundry.git?path=/"
                f"{package_path}#{commit_sha}"
            )
            if manifest_dependencies.get(package_id) != expected_selector:
                errors.append(
                    f"{label}: Unity manifest must pin every composition package to its canonical path "
                    f"and revision: {package_id}"
                )
            if entry.get("source") != "git":
                errors.append(f"{label}: custom lock package source must be git: {package_id}")
            if entry.get("hash") != commit_sha:
                errors.append(
                    f"{label}: custom lock package hash must equal receipt revision: {package_id}"
                )
            if entry.get("version") != expected_selector:
                errors.append(
                    f"{label}: custom lock selector must bind canonical path and revision: {package_id}"
                )
            try:
                manifest_result = subprocess.run(
                    ["git", "show", f"{commit_sha}:{package_path}/package.json"],
                    cwd=ROOT,
                    env=evidence_git_env(),
                    capture_output=True,
                    check=False,
                    timeout=10,
                )
            except (OSError, subprocess.SubprocessError):
                manifest_result = None
            package_manifest: dict[str, Any] | None = None
            if manifest_result is not None and manifest_result.returncode == 0:
                try:
                    decoded = json.loads(manifest_result.stdout.decode("utf-8"))
                    if isinstance(decoded, dict):
                        package_manifest = decoded
                except (UnicodeDecodeError, json.JSONDecodeError):
                    pass
            if package_manifest is None:
                errors.append(f"{label}: package.json is unreadable at receipt revision: {package_id}")
                continue
            if package_manifest.get("version") != expected_version:
                errors.append(
                    f"{label}: package tuple version does not match receipt revision: {package_id}"
                )
            if entry.get("dependencies") != package_manifest.get("dependencies", {}):
                errors.append(
                    f"{label}: custom lock dependency edges drift from receipt revision: {package_id}"
                )

        for package_id in manifest_dependencies:
            entry = lock_dependencies.get(package_id)
            if not isinstance(entry, dict):
                errors.append(f"{label}: Unity lock omits direct manifest dependency: {package_id}")
            elif entry.get("depth") != 0:
                errors.append(f"{label}: direct manifest dependency must have depth=0: {package_id}")

        required_external: set[str] = set()
        if isinstance(plan, dict):
            execution_requirements = plan.get("execution_requirements", {})
            if isinstance(execution_requirements, dict):
                required_external = {
                    str(package_id)
                    for package_id in execution_requirements.get(
                        "required_resolved_packages", []
                    )
                }
        relevant_external: dict[str, str] = {}
        visited: set[str] = set()
        pending = list(manifest_dependencies)
        traversed_edges = 0
        while pending:
            package_id = pending.pop()
            if package_id in visited:
                continue
            visited.add(package_id)
            entry = lock_dependencies.get(package_id)
            if not isinstance(entry, dict):
                errors.append(f"{label}: Unity lock omits reachable dependency: {package_id}")
                continue
            dependencies = entry.get("dependencies")
            depth = entry.get("depth")
            if not isinstance(depth, int) or isinstance(depth, bool) or depth < 0:
                errors.append(f"{label}: Unity lock dependency depth is invalid: {package_id}")
            if not isinstance(dependencies, dict):
                errors.append(f"{label}: Unity lock dependency edges are malformed: {package_id}")
                dependencies = {}
            for dependency_id, edge_version in dependencies.items():
                dependency_id = str(dependency_id)
                child = lock_dependencies.get(dependency_id)
                if not isinstance(child, dict):
                    errors.append(
                        f"{label}: Unity lock edge points to a missing dependency: "
                        f"{package_id} -> {dependency_id}"
                    )
                    continue
                traversed_edges += 1
                if dependency_id in custom_versions:
                    edge_is_satisfied = edge_version == custom_versions[dependency_id]
                else:
                    edge_is_satisfied = unity_external_dependency_is_satisfied(
                        edge_version, child.get("version")
                    )
                if not edge_is_satisfied:
                    errors.append(
                        f"{label}: Unity lock edge version drift: {package_id} -> {dependency_id}"
                    )
                parent_depth = entry.get("depth")
                child_depth = child.get("depth")
                if (
                    isinstance(parent_depth, int)
                    and not isinstance(parent_depth, bool)
                    and isinstance(child_depth, int)
                    and not isinstance(child_depth, bool)
                    and child_depth > parent_depth + 1
                ):
                    errors.append(
                        f"{label}: Unity lock child depth is inconsistent: "
                        f"{package_id} -> {dependency_id}"
                    )
            if package_id not in catalog_paths:
                version = entry.get("version")
                if entry.get("source") not in {"registry", "builtin"}:
                    errors.append(f"{label}: reachable external lock source is unsupported: {package_id}")
                if not isinstance(version, str) or SEMVER_PATTERN.fullmatch(version) is None:
                    errors.append(f"{label}: reachable external lock version must be exact: {package_id}")
                else:
                    relevant_external[package_id] = version
            pending.extend(str(dependency_id) for dependency_id in dependencies)
        if relevant_external and traversed_edges == 0:
            errors.append(f"{label}: Unity lock reachable dependency graph must contain real edges")
        expected_depths: dict[str, int] = {}
        depth_queue = [(str(package_id), 0) for package_id in manifest_dependencies]
        while depth_queue:
            package_id, depth = depth_queue.pop(0)
            if package_id in expected_depths and expected_depths[package_id] <= depth:
                continue
            expected_depths[package_id] = depth
            entry = lock_dependencies.get(package_id)
            if not isinstance(entry, dict):
                continue
            dependencies = entry.get("dependencies", {})
            if isinstance(dependencies, dict):
                depth_queue.extend((str(child_id), depth + 1) for child_id in dependencies)
        for package_id, expected_depth in expected_depths.items():
            entry = lock_dependencies.get(package_id)
            if isinstance(entry, dict) and entry.get("depth") != expected_depth:
                errors.append(
                    f"{label}: Unity lock depth must equal the shortest manifest path: {package_id}"
                )
        if dict(sorted(resolved_packages.items())) != dict(sorted(relevant_external.items())):
            errors.append(
                f"{label}: resolved_packages must equal the exact reachable non-custom lock graph"
            )

    software = payload.get("software")
    build = payload.get("build")
    device = payload.get("device")
    input_state = payload.get("input")
    if not isinstance(software, dict):
        errors.append(f"{label}: software environment is missing")
    else:
        for field in (
            "engine",
            "engine_version",
            "editor_version",
            "runtime_id",
            "runtime_version",
        ):
            value = software.get(field)
            if not isinstance(value, str) or not value.strip():
                errors.append(f"{label}: software.{field} is required")
        if software.get("engine") != "Unity":
            errors.append(f"{label}: Inventory package test plan requires software.engine=Unity")
        for field in ("engine_version", "editor_version"):
            if not isinstance(software.get(field), str) or UNITY_EDITOR_VERSION_PATTERN.fullmatch(
                str(software.get(field))
            ) is None:
                errors.append(f"{label}: software.{field} must be an exact Unity release version")
        if software.get("engine_version") != software.get("editor_version"):
            errors.append(f"{label}: software engine and editor versions must match exactly")
        if not is_exact_runtime_version(software.get("runtime_version")):
            errors.append(f"{label}: software.runtime_version must be an exact dotted runtime version")
        if isinstance(profile, dict) and software.get("runtime_id") != profile.get("runtime", {}).get("runtime_id"):
            errors.append(f"{label}: runtime_id does not match device profile {profile_id}")
    if not isinstance(build, dict):
        errors.append(f"{label}: build environment is missing")
    else:
        for field in ("target", "graphics_api", "scripting_backend", "architecture"):
            if not is_concrete_version_literal(build.get(field)):
                errors.append(f"{label}: build.{field} must be an exact concrete value")
    if verified_artifact_path is not None and isinstance(artifact, dict) and isinstance(build, dict):
        errors.extend(
            validate_unity_android_apk(
                verified_artifact_path,
                expected_application_id=str(artifact.get("application_id", "")),
                build=build,
                label=label,
            )
        )
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
        model = str(device.get("model", "")).strip()
        if model.casefold() in {"unknown", "any", "latest", "not_tested"}:
            errors.append(f"{label}: device.model must be an exact concrete value")
        if not is_exact_runtime_version(device.get("os_version")):
            errors.append(f"{label}: device.os_version must be an exact dotted OS version")
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
        raw_sources = input_state.get("sources")
        source_by_id: dict[str, dict[str, Any]] = {}
        if not isinstance(raw_sources, list) or not raw_sources:
            errors.append(f"{label}: input sources must be a non-empty list")
        else:
            for source in raw_sources:
                if not isinstance(source, dict):
                    errors.append(f"{label}: input sources must be objects")
                    continue
                source_id = str(source.get("id", ""))
                if not source_id or source_id in source_by_id:
                    errors.append(f"{label}: input source id is missing or duplicated: {source_id}")
                source_by_id[source_id] = source
                if not str(source.get("description", "")).strip():
                    errors.append(f"{label}: input source description is required: {source_id}")
        if isinstance(profile, dict):
            profile_sources = {
                str(item.get("id", "")): item
                for item in profile.get("input_sources", [])
                if isinstance(item, dict)
            }
            for source_id, source in source_by_id.items():
                admitted = profile_sources.get(source_id)
                if admitted is None:
                    errors.append(f"{label}: input source is not admitted by profile: {source_id}")
                    continue
                for field in ("kind", "handedness"):
                    if source.get(field) != admitted.get(field):
                        errors.append(
                            f"{label}: input source {field} does not match profile: {source_id}"
                        )
        if isinstance(target, dict):
            missing_sources = set(target.get("required_input_source_ids", [])) - set(source_by_id)
            if missing_sources:
                errors.append(
                    f"{label}: test-plan required input sources are missing: {sorted(missing_sources)}"
                )

    execution_context = payload.get("execution_context")
    duration_seconds: int | None = None
    if not isinstance(execution_context, dict):
        errors.append(f"{label}: execution_context is missing")
    else:
        posture = execution_context.get("posture")
        duration = execution_context.get("duration_seconds")
        if not isinstance(duration, int) or isinstance(duration, bool):
            errors.append(f"{label}: execution duration_seconds must be an integer")
        else:
            duration_seconds = duration
        if isinstance(plan, dict):
            requirements = plan.get("execution_requirements", {})
            minimum_duration = (
                requirements.get("minimum_duration_seconds")
                if isinstance(requirements, dict)
                else None
            )
            allowed_postures = (
                requirements.get("allowed_postures", [])
                if isinstance(requirements, dict)
                else []
            )
            if not isinstance(minimum_duration, int) or duration_seconds is None or duration_seconds < minimum_duration:
                errors.append(f"{label}: execution duration is below the plan minimum")
            if posture not in allowed_postures:
                errors.append(f"{label}: execution posture is not admitted by the plan")

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
                            unsafe_reference = (
                                not isinstance(reference, str)
                                or not reference.strip()
                                or not reference.startswith("docs/validation/evidence/")
                                or reference.startswith(("/", "\\"))
                                or re.match(r"^[A-Za-z]:", reference)
                                or "\\" in reference
                                or ".." in reference.replace("\\", "/").split("/")
                            )
                            if unsafe_reference:
                                errors.append(f"{label}: repository evidence path is unsafe: {check_id}")
                            else:
                                evidence_path = ROOT / str(reference)
                                try:
                                    evidence_path.resolve(strict=True).relative_to(
                                        (ROOT / "docs" / "validation" / "evidence").resolve()
                                    )
                                except (OSError, ValueError):
                                    evidence_path = None
                                    errors.append(
                                        f"{label}: repository evidence resolved path escapes: {check_id}"
                                    )
                                if evidence_path is None or not evidence_path.is_file():
                                    errors.append(
                                        f"{label}: repository evidence file does not exist: {check_id}"
                                    )
                                elif evidence_path.is_symlink():
                                    errors.append(
                                        f"{label}: repository evidence must not be a symbolic link: "
                                        f"{check_id}"
                                    )
                                elif SHA256_PATTERN.fullmatch(evidence_digest):
                                    actual_digest = hashlib.sha256(evidence_path.read_bytes()).hexdigest()
                                    if actual_digest != evidence_digest:
                                        errors.append(
                                            f"{label}: repository evidence SHA-256 does not match file: "
                                            f"{check_id}"
                                        )
                        else:
                            errors.append(
                                f"{label}: evidence ref must be a repository_file with a locally "
                                f"verified digest: {check_id}"
                            )
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
        elif (
            "started_at" in parsed_timestamps
            and "completed_at" in parsed_timestamps
            and duration_seconds is not None
            and (
                parsed_timestamps["completed_at"] - parsed_timestamps["started_at"]
            ).total_seconds()
            < duration_seconds
        ):
            errors.append(f"{label}: timestamp interval is shorter than execution duration")
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
            "com.lingkyn.inventory.presentation",
            "com.lingkyn.inventory.ugui",
            "com.lingkyn.inventory.uitoolkit",
            "com.lingkyn.inventory.xr.ugui",
            "com.lingkyn.inventory.xr.uitoolkit",
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
        "com.lingkyn.inventory.presentation": "unity-inventory-presentation",
        "com.lingkyn.inventory.ugui": "unity-inventory-ugui",
        "com.lingkyn.inventory.uitoolkit": "unity-inventory-uitoolkit",
        "com.lingkyn.inventory.xr.ugui": "unity-inventory-xr-ugui",
        "com.lingkyn.inventory.xr.uitoolkit": "unity-inventory-xr-uitoolkit",
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

        package_path = package.get("path")
        manifest_path = root / str(package_path) / "package.json"
        if manifest_path.exists():
            manifest_dependencies = set(load_json(manifest_path).get("dependencies", {}).keys())
            standard_dependencies = set(package_standard.get("required_dependencies", []))
            if manifest_dependencies != standard_dependencies:
                errors.append(
                    f"Inventory {label} dependency projection drift: "
                    f"standard={sorted(standard_dependencies)} manifest={sorted(manifest_dependencies)}"
                )

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

    xr_standards = [
        entry
        for package_id, entry in package_family.items()
        if package_id.startswith("com.lingkyn.inventory.xr.")
    ]
    if any(
        entry.get("implementation_status") in {"implemented_incubating", "implemented_candidate"}
        for entry in xr_standards
    ):
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
    catalog_path = root / "package-catalog.json"
    if not catalog_path.exists():
        return errors
    core_path = package_paths_by_id(load_json(catalog_path)).get("com.lingkyn.inventory.core")
    if not core_path:
        return errors
    manifest_path = root / core_path / "package.json"
    runtime_root = root / core_path / "Runtime"
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


def is_concrete_version_literal(value: Any, *, allow_not_applicable: bool = False) -> bool:
    if not isinstance(value, str) or not value:
        return False
    lowered = value.casefold()
    if allow_not_applicable and lowered == "not_applicable":
        return True
    if lowered in {
        "any",
        "latest",
        "current",
        "unknown",
        "unbounded",
        "not_applicable",
        "not_tested",
        "placeholder",
    }:
        return False
    if re.search(r"[<>=^~*|,\s]", value):
        return False
    return re.fullmatch(r"[0-9A-Za-z][0-9A-Za-z._+-]*", value) is not None


def is_exact_runtime_version(value: Any, *, allow_not_applicable: bool = False) -> bool:
    if allow_not_applicable and value == "not_applicable":
        return True
    if not isinstance(value, str):
        return False
    lowered = value.casefold()
    if any(token in lowered for token in ("recorded", "placeholder", "replace", "current", "latest")):
        return False
    return EXACT_RUNTIME_VERSION_PATTERN.fullmatch(value) is not None


def evidence_git_env() -> dict[str, str]:
    environment = dict(os.environ)
    environment.update(
        {
            "GIT_NO_LAZY_FETCH": "1",
            "GIT_NO_REPLACE_OBJECTS": "1",
            "GIT_OPTIONAL_LOCKS": "0",
        }
    )
    return environment


def commit_is_public_origin_reachable(root: Path, commit_sha: str) -> bool:
    """Require evidence revisions to be reachable from a fetched public origin ref.

    This deliberately uses only local remote-tracking refs. Validation therefore has no
    network dependency, while a local-only object or unpushed commit cannot support a
    public compatibility or device claim.
    """
    git_env = evidence_git_env()
    try:
        commit_result = subprocess.run(
            ["git", "cat-file", "-e", f"{commit_sha}^{{commit}}"],
            cwd=root,
            env=git_env,
            capture_output=True,
            check=False,
            timeout=10,
        )
        remote_url = subprocess.run(
            ["git", "remote", "get-url", "origin"],
            cwd=root,
            env=git_env,
            capture_output=True,
            text=True,
            check=False,
            timeout=10,
        )
        refs_result = subprocess.run(
            [
                "git",
                "for-each-ref",
                "--format=%(refname)",
                "refs/remotes/origin",
            ],
            cwd=root,
            env=git_env,
            capture_output=True,
            text=True,
            check=False,
            timeout=10,
        )
        git_dir_result = subprocess.run(
            ["git", "rev-parse", "--git-path", "shallow"],
            cwd=root,
            env=git_env,
            capture_output=True,
            text=True,
            check=False,
            timeout=10,
        )
        promisor_result = subprocess.run(
            ["git", "config", "--bool", "--get", "remote.origin.promisor"],
            cwd=root,
            env=git_env,
            capture_output=True,
            text=True,
            check=False,
            timeout=10,
        )
        partial_clone_result = subprocess.run(
            ["git", "config", "--get", "extensions.partialClone"],
            cwd=root,
            env=git_env,
            capture_output=True,
            text=True,
            check=False,
            timeout=10,
        )
    except (OSError, subprocess.SubprocessError):
        return False
    if commit_result.returncode != 0 or remote_url.returncode != 0 or refs_result.returncode != 0:
        return False
    if promisor_result.returncode == 0 and promisor_result.stdout.strip().casefold() == "true":
        return False
    if partial_clone_result.returncode == 0 and partial_clone_result.stdout.strip():
        return False
    if git_dir_result.returncode != 0:
        return False
    shallow_path = Path(git_dir_result.stdout.strip())
    if not shallow_path.is_absolute():
        shallow_path = root / shallow_path
    if shallow_path.is_file() and shallow_path.stat().st_size > 0:
        return False
    normalized_remote = remote_url.stdout.strip().removesuffix(".git")
    if normalized_remote not in {
        PUBLIC_REPOSITORY,
        "git@github.com:Lingkyn/xr-foundry",
    }:
        return False
    refs = [
        ref.strip()
        for ref in refs_result.stdout.splitlines()
        if ref.strip() == "refs/remotes/origin/main"
        or ref.strip().startswith("refs/remotes/origin/codex/")
    ]
    for ref in refs:
        try:
            result = subprocess.run(
                ["git", "merge-base", "--is-ancestor", commit_sha, ref],
                cwd=root,
                env=git_env,
                capture_output=True,
                check=False,
                timeout=10,
            )
        except (OSError, subprocess.SubprocessError):
            return False
        if result.returncode == 0:
            return True
    return False


def _decode_axml_length8(data: bytes, offset: int, limit: int) -> tuple[int, int]:
    if offset >= limit:
        raise ValueError("truncated UTF-8 string length")
    first = data[offset]
    offset += 1
    if first & 0x80:
        if offset >= limit:
            raise ValueError("truncated UTF-8 string length")
        return ((first & 0x7F) << 8) | data[offset], offset + 1
    return first, offset


def _decode_axml_length16(data: bytes, offset: int, limit: int) -> tuple[int, int]:
    if offset + 2 > limit:
        raise ValueError("truncated UTF-16 string length")
    first = struct.unpack_from("<H", data, offset)[0]
    offset += 2
    if first & 0x8000:
        if offset + 2 > limit:
            raise ValueError("truncated UTF-16 string length")
        second = struct.unpack_from("<H", data, offset)[0]
        return ((first & 0x7FFF) << 16) | second, offset + 2
    return first, offset


def _parse_axml_string_pool(
    data: bytes,
    chunk_offset: int,
    header_size: int,
    chunk_size: int,
) -> list[str]:
    chunk_end = chunk_offset + chunk_size
    if header_size < 28 or chunk_offset + header_size > chunk_end:
        raise ValueError("invalid Android string-pool header")
    string_count, style_count, flags, strings_start, styles_start = struct.unpack_from(
        "<IIIII", data, chunk_offset + 8
    )
    if string_count > 1_000_000 or style_count > 1_000_000:
        raise ValueError("unreasonable Android string-pool count")
    offsets_start = chunk_offset + header_size
    offsets_end = offsets_start + (string_count + style_count) * 4
    strings_base = chunk_offset + strings_start
    if offsets_end > chunk_end or strings_base < offsets_end or strings_base > chunk_end:
        raise ValueError("invalid Android string-pool offsets")
    if styles_start and not (strings_start <= styles_start <= chunk_size):
        raise ValueError("invalid Android string-pool style offset")
    string_limit = chunk_offset + (styles_start or chunk_size)
    utf8 = bool(flags & 0x00000100)
    strings: list[str] = []
    for index in range(string_count):
        relative_offset = struct.unpack_from("<I", data, offsets_start + index * 4)[0]
        cursor = strings_base + relative_offset
        if cursor < strings_base or cursor >= string_limit:
            raise ValueError("Android string offset escapes string data")
        if utf8:
            _, cursor = _decode_axml_length8(data, cursor, string_limit)
            byte_length, cursor = _decode_axml_length8(data, cursor, string_limit)
            end = cursor + byte_length
            if end >= string_limit or data[end] != 0:
                raise ValueError("unterminated Android UTF-8 string")
            try:
                value = data[cursor:end].decode("utf-8")
            except UnicodeDecodeError as exc:
                raise ValueError("invalid Android UTF-8 string") from exc
        else:
            character_length, cursor = _decode_axml_length16(data, cursor, string_limit)
            end = cursor + character_length * 2
            if end + 2 > string_limit or data[end : end + 2] != b"\x00\x00":
                raise ValueError("unterminated Android UTF-16 string")
            try:
                value = data[cursor:end].decode("utf-16-le")
            except UnicodeDecodeError as exc:
                raise ValueError("invalid Android UTF-16 string") from exc
        strings.append(value)
    return strings


def parse_binary_android_manifest_package(data: bytes) -> str:
    """Extract the package ID from a compiled Android binary XML manifest."""
    if len(data) < 8:
        raise ValueError("AndroidManifest.xml is truncated")
    chunk_type, header_size, document_size = struct.unpack_from("<HHI", data, 0)
    if chunk_type != 0x0003 or header_size < 8 or document_size != len(data):
        raise ValueError("AndroidManifest.xml must be compiled Android binary XML")
    cursor = header_size
    strings: list[str] | None = None
    root_seen = False
    root_closed = False
    element_stack: list[tuple[int, int]] = []
    package_value: str | None = None
    chunk_count = 0
    while cursor < document_size:
        chunk_count += 1
        if chunk_count > 1_000_000:
            raise ValueError("Android binary XML has too many chunks")
        if cursor + 8 > document_size:
            raise ValueError("truncated Android binary XML chunk")
        child_type, child_header_size, child_size = struct.unpack_from("<HHI", data, cursor)
        if (
            child_header_size < 8
            or child_size < child_header_size
            or cursor + child_size > document_size
            or child_size % 4 != 0
        ):
            raise ValueError("invalid Android binary XML chunk bounds")
        if child_type == 0x0001:
            if strings is not None:
                raise ValueError("duplicate Android string pool")
            strings = _parse_axml_string_pool(
                data, cursor, child_header_size, child_size
            )
        elif child_type == 0x0102:
            if strings is None or child_header_size < 16:
                raise ValueError("Android start element precedes its string pool")
            if root_closed:
                raise ValueError("Android binary XML must contain exactly one root element")
            extension = cursor + child_header_size
            if extension + 20 > cursor + child_size:
                raise ValueError("truncated Android start-element extension")
            element_namespace_index, name_index = struct.unpack_from(
                "<II", data, extension
            )
            attribute_start, attribute_size, attribute_count = struct.unpack_from(
                "<HHH", data, extension + 8
            )
            if name_index >= len(strings) or attribute_size < 20:
                raise ValueError("invalid Android start-element metadata")
            element_name = strings[name_index]
            is_root_element = not element_stack
            if is_root_element:
                if root_seen:
                    raise ValueError("Android binary XML must contain exactly one root element")
                root_seen = True
                if element_name != "manifest" or element_namespace_index != 0xFFFFFFFF:
                    raise ValueError(
                        "Android binary XML root element must be an unnamespaced manifest"
                    )
            elif element_name == "manifest":
                raise ValueError("Android binary XML must not contain a nested manifest element")
            attributes_offset = extension + attribute_start
            attributes_end = attributes_offset + attribute_count * attribute_size
            if attributes_offset < extension + 20 or attributes_end > cursor + child_size:
                raise ValueError("Android attributes escape start-element chunk")
            if is_root_element:
                for index in range(attribute_count):
                    attribute = attributes_offset + index * attribute_size
                    attribute_namespace_index = struct.unpack_from(
                        "<I", data, attribute
                    )[0]
                    name, raw_value = struct.unpack_from("<II", data, attribute + 4)
                    if name >= len(strings):
                        raise ValueError("Android attribute name index is invalid")
                    if strings[name] != "package":
                        continue
                    if attribute_namespace_index != 0xFFFFFFFF:
                        raise ValueError("Android manifest package attribute must be unnamespaced")
                    if package_value is not None:
                        raise ValueError("Android manifest contains duplicate package attributes")
                    value_size, value_res0, value_type, typed_data = struct.unpack_from(
                        "<HBBI", data, attribute + 12
                    )
                    if value_size != 8 or value_res0 != 0 or value_type != 0x03:
                        raise ValueError("Android package attribute is not a typed string")
                    if raw_value != 0xFFFFFFFF:
                        value_index = raw_value
                        if typed_data != raw_value:
                            raise ValueError(
                                "Android package raw and typed string values disagree"
                            )
                    else:
                        value_index = typed_data
                    if value_index >= len(strings):
                        raise ValueError("Android package string index is invalid")
                    package_value = strings[value_index]
            element_stack.append((element_namespace_index, name_index))
        elif child_type == 0x0103:
            if strings is None or child_header_size < 16:
                raise ValueError("invalid Android end element")
            extension = cursor + child_header_size
            if extension + 8 > cursor + child_size:
                raise ValueError("truncated Android end element")
            namespace_index, name_index = struct.unpack_from("<II", data, extension)
            if name_index >= len(strings):
                raise ValueError("Android end-element name index is invalid")
            if not element_stack:
                raise ValueError("Android end element has no matching start element")
            if element_stack[-1] != (namespace_index, name_index):
                raise ValueError("Android end element does not match its start element")
            element_stack.pop()
            if not element_stack:
                root_closed = True
        cursor += child_size
    if cursor != document_size or not root_seen or not root_closed or element_stack:
        raise ValueError("Android binary XML document is incomplete")
    if package_value is None:
        raise ValueError("Android manifest package attribute is missing")
    if re.fullmatch(
        r"[A-Za-z][A-Za-z0-9_]*(?:\.[A-Za-z][A-Za-z0-9_]*)+",
        package_value,
    ) is None:
        raise ValueError("Android manifest package ID is invalid")
    return package_value


def validate_unity_android_apk(
    path: Path,
    *,
    expected_application_id: str,
    build: dict[str, Any],
    label: str,
) -> list[str]:
    max_entries = 100_000
    max_entry_size = 4 * 1024 * 1024 * 1024
    max_total_size = 8 * 1024 * 1024 * 1024
    max_manifest_size = 16 * 1024 * 1024
    max_compression_ratio = 10_000
    errors: list[str] = []
    try:
        with zipfile.ZipFile(path, "r") as archive:
            infos = archive.infolist()
            names = [info.orig_filename for info in infos]
            if not infos or len(infos) > max_entries:
                errors.append(f"{label}: APK ZIP entry count is invalid")
            normalized_names = [unicodedata.normalize("NFC", name) for name in names]
            if (
                len(names) != len(set(names))
                or len(names) != len(set(normalized_names))
                or len(names) != len({name.casefold() for name in normalized_names})
            ):
                errors.append(f"{label}: APK must not contain duplicate entry names")
            declared_total = 0
            safe_to_stream = True
            for info in infos:
                name = info.orig_filename
                pure = PurePosixPath(name)
                segments = name.split("/")
                if (
                    not name
                    or "\x00" in name
                    or "\\" in name
                    or pure.is_absolute()
                    or any(segment in {"", ".", ".."} for segment in segments)
                    or re.match(r"^[A-Za-z]:", name)
                    or unicodedata.normalize("NFC", name) != name
                ):
                    errors.append(f"{label}: APK contains an unsafe entry path: {name}")
                    safe_to_stream = False
                if info.flag_bits & ((1 << 0) | (1 << 6)):
                    errors.append(f"{label}: APK entries must not be ZIP-encrypted: {name}")
                    safe_to_stream = False
                unix_mode = (info.external_attr >> 16) & 0xFFFF
                if stat.S_IFMT(unix_mode) == stat.S_IFLNK:
                    errors.append(f"{label}: APK entries must not be symbolic links: {name}")
                    safe_to_stream = False
                if info.compress_type not in {zipfile.ZIP_STORED, zipfile.ZIP_DEFLATED}:
                    errors.append(f"{label}: APK uses unsupported ZIP compression: {name}")
                    safe_to_stream = False
                if (
                    info.file_size < 0
                    or info.compress_size < 0
                    or info.file_size > max_entry_size
                ):
                    errors.append(f"{label}: APK entry exceeds the size limit: {name}")
                    safe_to_stream = False
                declared_total += max(info.file_size, 0)
                if declared_total > max_total_size:
                    errors.append(f"{label}: APK declared uncompressed size exceeds the limit")
                    safe_to_stream = False
                if info.file_size > 0 and (
                    info.compress_size == 0
                    or info.file_size / info.compress_size > max_compression_ratio
                ):
                    errors.append(f"{label}: APK entry compression ratio exceeds the limit: {name}")
                    safe_to_stream = False
            if safe_to_stream:
                for info in infos:
                    actual_size = 0
                    try:
                        with archive.open(info, "r") as stream:
                            while chunk := stream.read(1024 * 1024):
                                actual_size += len(chunk)
                                if actual_size > info.file_size or actual_size > max_entry_size:
                                    raise ValueError("decompressed entry exceeds declared size")
                    except (OSError, RuntimeError, ValueError, zipfile.BadZipFile) as exc:
                        errors.append(
                            f"{label}: APK entry CRC/stream validation failed: "
                            f"{info.orig_filename}: {exc}"
                        )
                        break
                    if actual_size != info.file_size:
                        errors.append(f"{label}: APK entry size disagrees with ZIP metadata: {info.orig_filename}")
                        break

            manifests = [info for info in infos if info.orig_filename == "AndroidManifest.xml"]
            if len(manifests) != 1:
                errors.append(f"{label}: APK must contain exactly one AndroidManifest.xml")
            elif manifests[0].file_size > max_manifest_size:
                errors.append(f"{label}: APK AndroidManifest.xml exceeds the size limit")
            else:
                try:
                    manifest_package = parse_binary_android_manifest_package(
                        archive.read(manifests[0])
                    )
                except (KeyError, RuntimeError, ValueError, zipfile.BadZipFile) as exc:
                    errors.append(f"{label}: APK AndroidManifest.xml is invalid: {exc}")
                else:
                    if manifest_package != expected_application_id:
                        errors.append(
                            f"{label}: receipt application_id must equal the APK manifest package ID"
                        )
            dex_infos = [
                info
                for info in infos
                if re.fullmatch(r"classes(?:[2-9][0-9]*)?\.dex", info.orig_filename)
            ]
            if not dex_infos:
                errors.append(f"{label}: APK must contain classes.dex payload")
            elif any(archive.open(info).read(4) != b"dex\n" for info in dex_infos):
                errors.append(f"{label}: APK DEX payload has invalid magic")
            if build.get("architecture") == "ARM64":
                required_libraries = ["lib/arm64-v8a/libunity.so"]
                if build.get("scripting_backend") == "IL2CPP":
                    required_libraries.append("lib/arm64-v8a/libil2cpp.so")
                for library in required_libraries:
                    matches = [info for info in infos if info.orig_filename == library]
                    if len(matches) != 1:
                        errors.append(f"{label}: APK is missing required Unity library: {library}")
                    elif archive.open(matches[0]).read(4) != b"\x7fELF":
                        errors.append(f"{label}: APK Unity library has invalid ELF magic: {library}")
            unity_data = [
                info
                for info in infos
                if info.orig_filename.startswith("assets/bin/Data/")
                and not info.is_dir()
                and info.file_size > 0
            ]
            if not unity_data:
                errors.append(f"{label}: APK must contain non-empty Unity player data")
    except (OSError, RuntimeError, zipfile.BadZipFile, zipfile.LargeZipFile) as exc:
        return [f"{label}: artifact must be a valid APK ZIP: {exc}"]
    return errors


def validate_unity_compile_result(
    path: Path,
    *,
    root: Path,
    profile_id: str,
    commit_sha: str,
    target: dict[str, Any],
    manifest_sha256: str,
    lock_sha256: str,
    label: str,
) -> list[str]:
    errors: list[str] = []
    try:
        payload = load_json(path)
    except (json.JSONDecodeError, UnicodeDecodeError) as exc:
        return [f"{label}: compile result must be structured JSON: {exc}"]
    errors.extend(
        validate_json_schema_instance(payload, root / UNITY_COMPILE_RESULT_SCHEMA, label)
    )
    if not isinstance(payload, dict):
        return errors + [f"{label}: compile result must be an object"]
    expected = {
        "profile_id": profile_id,
        "commit_sha": commit_sha,
        "unity_version": target.get("editor", {}).get("version"),
        "build_target": target.get("build_target"),
        "graphics_api": target.get("graphics_api"),
        "scripting_backend": target.get("scripting_backend"),
        "architecture": target.get("architecture"),
        "manifest_sha256": manifest_sha256,
        "lock_sha256": lock_sha256,
    }
    for field, value in expected.items():
        if payload.get(field) != value:
            errors.append(f"{label}: compile result {field} must match the evidence tuple")
    if payload.get("result") != "pass" or payload.get("error_count") != 0:
        errors.append(f"{label}: compile result must report pass with error_count=0")
    if payload.get("batchmode") is not True:
        errors.append(f"{label}: compile result must record batchmode=true")
    parsed: dict[str, datetime] = {}
    for field in ("started_at", "completed_at"):
        value = payload.get(field)
        try:
            parsed[field] = datetime.fromisoformat(str(value).replace("Z", "+00:00"))
            if parsed[field].tzinfo is None:
                raise ValueError("timezone required")
        except ValueError:
            errors.append(f"{label}: compile result {field} must be timezone-aware ISO 8601")
    if (
        "started_at" in parsed
        and "completed_at" in parsed
        and parsed["completed_at"] < parsed["started_at"]
    ):
        errors.append(f"{label}: compile completion must not precede start")
    return errors


@functools.lru_cache(maxsize=128)
def _derive_expected_test_assemblies_cached(
    root_text: str,
    commit_sha: str,
    package_paths: tuple[tuple[str, str], ...],
    package_ids: tuple[str, ...],
    mode: str,
) -> tuple[tuple[str, ...], tuple[str, ...]]:
    root = Path(root_text)
    paths_by_id = dict(package_paths)
    expected: set[str] = set()
    errors: list[str] = []
    for package_id in package_ids:
        package_path = paths_by_id.get(package_id)
        if not package_path:
            errors.append(f"test package is absent from catalog: {package_id}")
            continue
        tests_root = f"{package_path}/Tests"
        try:
            tree_result = subprocess.run(
                ["git", "ls-tree", "-r", "--name-only", commit_sha, "--", tests_root],
                cwd=root,
                env=evidence_git_env(),
                capture_output=True,
                text=True,
                check=False,
                timeout=20,
            )
        except (OSError, subprocess.SubprocessError):
            errors.append(f"cannot enumerate test assemblies at evidence commit: {package_id}")
            continue
        if tree_result.returncode != 0:
            errors.append(f"cannot enumerate test assemblies at evidence commit: {package_id}")
            continue
        for asmdef_path in sorted(
            path for path in tree_result.stdout.splitlines() if path.endswith(".asmdef")
        ):
            asmdef = load_json_at_git_revision(root, commit_sha, asmdef_path)
            if asmdef is None:
                errors.append(f"test asmdef is unreadable at evidence commit: {asmdef_path}")
                continue
            test_references = asmdef.get("optionalUnityReferences", [])
            if not isinstance(test_references, list) or "TestAssemblies" not in test_references:
                errors.append(
                    f"Tests asmdef must declare optionalUnityReferences/TestAssemblies: {asmdef_path}"
                )
                continue
            include = asmdef.get("includePlatforms", [])
            exclude = asmdef.get("excludePlatforms", [])
            define_constraints = asmdef.get("defineConstraints", [])
            if (
                not isinstance(include, list)
                or not all(isinstance(item, str) and item for item in include)
                or len(include) != len(set(include))
                or not isinstance(exclude, list)
                or not all(isinstance(item, str) and item for item in exclude)
                or len(exclude) != len(set(exclude))
            ):
                errors.append(f"test asmdef platform filters are malformed: {asmdef_path}")
                continue
            if include and exclude:
                errors.append(f"test asmdef cannot combine include/exclude platforms: {asmdef_path}")
                continue
            if (
                not isinstance(define_constraints, list)
                or not all(isinstance(item, str) and item for item in define_constraints)
                or len(define_constraints) != len(set(define_constraints))
                or any(item != "UNITY_INCLUDE_TESTS" for item in define_constraints)
            ):
                errors.append(
                    f"test asmdef defineConstraints are unsupported for evidence derivation: "
                    f"{asmdef_path}"
                )
                continue
            is_editmode = include == ["Editor"]
            editor_available = (
                (not include or "Editor" in include) and "Editor" not in exclude
            )
            is_playmode = editor_available and not is_editmode
            applies = (mode == "EditMode" and is_editmode) or (
                mode == "PlayMode" and is_playmode
            )
            if not applies:
                continue
            assembly_name = asmdef.get("name")
            if not isinstance(assembly_name, str) or not assembly_name.strip():
                errors.append(f"test asmdef name is missing: {asmdef_path}")
                continue
            expected.add(f"{assembly_name}.dll")
    return tuple(sorted(expected)), tuple(errors)


def load_json_at_git_revision(
    root: Path, commit_sha: str, repository_path: str
) -> dict[str, Any] | None:
    try:
        result = subprocess.run(
            ["git", "show", f"{commit_sha}:{repository_path}"],
            cwd=root,
            env=evidence_git_env(),
            capture_output=True,
            check=False,
            timeout=10,
        )
    except (OSError, subprocess.SubprocessError):
        return None
    if result.returncode != 0:
        return None
    try:
        payload = json.loads(result.stdout.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError):
        return None
    return payload if isinstance(payload, dict) else None


def derive_expected_test_assemblies(
    root: Path,
    commit_sha: str,
    catalog_paths: dict[str, str],
    package_ids: set[str],
    mode: str,
) -> tuple[set[str], list[str]]:
    expected, errors = _derive_expected_test_assemblies_cached(
        str(root.resolve()),
        commit_sha,
        tuple(sorted(catalog_paths.items())),
        tuple(sorted(package_ids)),
        mode,
    )
    return set(expected), list(errors)


def _nunit_properties(element: ET.Element) -> dict[str, list[str]]:
    properties: dict[str, list[str]] = {}
    for child in element:
        if child.tag.rsplit("}", 1)[-1] != "properties":
            continue
        for item in child:
            if item.tag.rsplit("}", 1)[-1] != "property":
                continue
            name = item.attrib.get("name")
            value = item.attrib.get("value")
            if isinstance(name, str) and isinstance(value, str):
                properties.setdefault(name, []).append(value)
    return properties


def _required_nunit_count(
    element: ET.Element, field: str, label: str, errors: list[str]
) -> int:
    try:
        value = int(element.attrib[field])
    except (KeyError, TypeError, ValueError):
        errors.append(f"{label}: NUnit element requires an integer {field} attribute")
        return -1
    if value < 0:
        errors.append(f"{label}: NUnit {field} must not be negative")
    return value


def validate_nunit_result(
    path: Path,
    label: str,
    *,
    check_id: str,
    root: Path,
    commit_sha: str,
    catalog_paths: dict[str, str],
    package_ids: set[str],
    manifest_path: Path | None,
) -> list[str]:
    max_xml_size = 128 * 1024 * 1024
    try:
        xml_bytes = path.read_bytes()
        if not xml_bytes or len(xml_bytes) > max_xml_size:
            return [f"{label}: NUnit XML size is outside the accepted evidence bound"]
        upper_xml = xml_bytes.upper()
        if b"<!DOCTYPE" in upper_xml or b"<!ENTITY" in upper_xml:
            return [f"{label}: NUnit XML must not contain DTD or entity declarations"]
        root_element = ET.fromstring(xml_bytes)
    except (ET.ParseError, OSError, ValueError) as exc:
        return [f"{label}: test evidence must be parseable NUnit XML: {exc}"]
    tag = root_element.tag.rsplit("}", 1)[-1]
    if tag != "test-run":
        return [f"{label}: NUnit evidence root must be test-run"]
    errors: list[str] = []
    mode_by_check = {"editmode_tests": "EditMode", "playmode_tests": "PlayMode"}
    mode = mode_by_check.get(check_id)
    if mode is None:
        return [f"{label}: NUnit evidence is not valid for check {check_id}"]
    required_assemblies, derivation_errors = derive_expected_test_assemblies(
        root, commit_sha, catalog_paths, package_ids, mode
    )
    errors.extend(f"{label}: {error}" for error in derivation_errors)
    if not required_assemblies:
        errors.append(f"{label}: evidence commit has no applicable {mode} test assemblies")

    declared_packages: set[str] = set()
    if manifest_path is None:
        errors.append(f"{label}: NUnit evidence requires its bound Unity manifest")
    else:
        try:
            consumer_manifest = load_json(manifest_path)
        except (json.JSONDecodeError, UnicodeDecodeError, OSError) as exc:
            errors.append(f"{label}: bound Unity manifest is invalid JSON: {exc}")
            consumer_manifest = {}
        testables = consumer_manifest.get("testables") if isinstance(consumer_manifest, dict) else None
        manifest_dependencies = (
            consumer_manifest.get("dependencies", {})
            if isinstance(consumer_manifest, dict)
            else {}
        )
        if (
            not isinstance(testables, list)
            or not testables
            or not all(isinstance(item, str) and item for item in testables)
            or len(testables) != len(set(testables))
        ):
            errors.append(
                f"{label}: bound Unity manifest testables must be a non-empty unique package list"
            )
        elif not isinstance(manifest_dependencies, dict):
            errors.append(f"{label}: bound Unity manifest dependencies must be an object")
        else:
            for package_id in testables:
                package_path = catalog_paths.get(package_id)
                if package_path is None:
                    errors.append(
                        f"{label}: manifest testable is absent from the package catalog: {package_id}"
                    )
                    continue
                expected_selector = (
                    "https://github.com/Lingkyn/xr-foundry.git?path=/"
                    f"{package_path}#{commit_sha}"
                )
                if manifest_dependencies.get(package_id) != expected_selector:
                    errors.append(
                        f"{label}: every testable package must use its canonical same-commit "
                        f"manifest selector: {package_id}"
                    )
                    continue
                declared_packages.add(package_id)
    declared_assemblies, declared_errors = derive_expected_test_assemblies(
        root, commit_sha, catalog_paths, declared_packages, mode
    )
    errors.extend(f"{label}: {error}" for error in declared_errors)
    numeric = {
        field: _required_nunit_count(root_element, field, label, errors)
        for field in ("total", "passed", "failed", "inconclusive", "skipped")
    }
    if root_element.attrib.get("result") != "Passed":
        errors.append(f"{label}: NUnit test-run result must be Passed")
    if numeric["total"] < 1 or numeric["passed"] < 1:
        errors.append(f"{label}: NUnit result must contain at least one passed test")
    if any(numeric[field] != 0 for field in ("failed", "inconclusive", "skipped")):
        errors.append(f"{label}: NUnit result must report failed/inconclusive/skipped=0")
    if numeric["total"] != numeric["passed"]:
        errors.append(f"{label}: NUnit root total must equal passed")
    project_suites = [
        element
        for element in root_element
        if element.tag.rsplit("}", 1)[-1] == "test-suite"
        and element.attrib.get("type") == "TestSuite"
    ]
    if len(project_suites) != 1:
        errors.append(f"{label}: Unity NUnit test-run must contain exactly one project TestSuite")
        project_suite = None
        assembly_suites: list[ET.Element] = []
    else:
        project_suite = project_suites[0]
        project_label = f"{label}: Unity NUnit project suite"
        project_counts = {
            field: _required_nunit_count(project_suite, field, project_label, errors)
            for field in ("total", "passed", "failed", "inconclusive", "skipped")
        }
        if project_suite.attrib.get("result") != "Passed":
            errors.append(f"{project_label}: result must be Passed")
        if project_counts != numeric:
            errors.append(f"{project_label}: counts must exactly equal the test-run counts")
        if _nunit_properties(project_suite).get("platform") != [mode]:
            errors.append(f"{project_label}: platform property must equal {mode}")
        assembly_suites = [
            element
            for element in project_suite
            if element.tag.rsplit("}", 1)[-1] == "test-suite"
            and element.attrib.get("type") == "Assembly"
        ]
    actual_names = [PurePosixPath(item.attrib.get("name", "")).name for item in assembly_suites]
    if len(actual_names) != len(set(actual_names)):
        errors.append(f"{label}: NUnit Assembly suites must not be duplicated")
    actual_assemblies = set(actual_names)
    missing_required = required_assemblies - actual_assemblies
    if missing_required:
        errors.append(
            f"{label}: NUnit result omits commit-required {mode} assemblies: "
            f"{sorted(missing_required)}"
        )
    if actual_assemblies != declared_assemblies:
        errors.append(
            f"{label}: NUnit Assembly suites must exactly equal manifest-testables-derived "
            f"{mode} assemblies; expected={sorted(declared_assemblies)}, "
            f"actual={sorted(actual_assemblies)}"
        )
    aggregate_total = 0
    aggregate_passed = 0
    for suite in assembly_suites:
        assembly_name = PurePosixPath(suite.attrib.get("name", "")).name
        suite_label = f"{label}: NUnit assembly {assembly_name}"
        suite_counts = {
            field: _required_nunit_count(suite, field, suite_label, errors)
            for field in ("total", "passed", "failed", "inconclusive", "skipped")
        }
        aggregate_total += max(suite_counts["total"], 0)
        aggregate_passed += max(suite_counts["passed"], 0)
        if suite.attrib.get("result") != "Passed":
            errors.append(f"{suite_label}: result must be Passed")
        if suite_counts["total"] < 1 or suite_counts["total"] != suite_counts["passed"]:
            errors.append(f"{suite_label}: total must equal passed and be at least one")
        if any(
            suite_counts[field] != 0 for field in ("failed", "inconclusive", "skipped")
        ):
            errors.append(f"{suite_label}: failed/inconclusive/skipped must equal zero")
        properties = _nunit_properties(suite)
        if properties.get("platform") != [mode]:
            errors.append(f"{suite_label}: platform property must equal {mode}")
        expected_editor_only = "True" if mode == "EditMode" else "False"
        if properties.get("EditorOnly") != [expected_editor_only]:
            errors.append(
                f"{suite_label}: EditorOnly property must equal {expected_editor_only}"
            )
        cases = [
            element
            for element in suite.iter()
            if element.tag.rsplit("}", 1)[-1] == "test-case"
        ]
        if len(cases) != suite_counts["total"]:
            errors.append(f"{suite_label}: test-case count must equal suite total")
        if not cases or any(case.attrib.get("result") != "Passed" for case in cases):
            errors.append(f"{suite_label}: every required test-case must be present and Passed")
    if aggregate_total != numeric["total"] or aggregate_passed != numeric["passed"]:
        errors.append(f"{label}: NUnit root aggregates must equal Assembly suite totals")
    return errors


def validate_concrete_package_manifests(
    root: Path, catalog: dict[str, Any]
) -> list[str]:
    """Keep installable artifacts concrete even when generation is version-adaptive."""
    errors: list[str] = []
    for item in catalog.get("packages", []):
        if not isinstance(item, dict):
            continue
        package_id = str(item.get("id", ""))
        package_path = root / str(item.get("path", "")) / "package.json"
        if not package_path.exists():
            continue
        try:
            manifest = load_json(package_path)
        except (json.JSONDecodeError, UnicodeDecodeError):
            continue
        version = manifest.get("version")
        if not isinstance(version, str) or SEMVER_PATTERN.fullmatch(version) is None:
            errors.append(f"{package_id}: installable manifest version must be an exact semantic version")
        unity = manifest.get("unity")
        if not isinstance(unity, str) or re.fullmatch(r"[0-9]+\.[0-9]+", unity) is None:
            errors.append(f"{package_id}: Unity compatibility floor must be a concrete major.minor value")
        unity_release = manifest.get("unityRelease")
        if unity_release is not None and (
            not isinstance(unity_release, str)
            or re.fullmatch(r"[0-9]+[abfp][0-9]+", unity_release) is None
        ):
            errors.append(f"{package_id}: unityRelease must be a concrete release value")
        dependencies = manifest.get("dependencies", {})
        if not isinstance(dependencies, dict):
            errors.append(f"{package_id}: package dependencies must be a concrete version mapping")
            continue
        for dependency_id, dependency_version in dependencies.items():
            if (
                not isinstance(dependency_version, str)
                or SEMVER_PATTERN.fullmatch(dependency_version) is None
            ):
                errors.append(
                    f"{package_id}: dependency {dependency_id} must use an exact semantic version"
                )
    return errors


def validate_compatibility_profile_payload(
    payload: Any,
    root: Path,
    catalog: dict[str, Any],
) -> list[str]:
    errors = validate_json_schema_instance(
        payload,
        root / COMPATIBILITY_PROFILES_SCHEMA,
        "compatibility profiles",
    )
    if not isinstance(payload, dict):
        return errors + ["compatibility profiles must be a JSON object"]

    if payload.get("schema") != "xr-foundry.compatibility_profiles.v2":
        errors.append("compatibility profile schema is invalid")
    expected_capability = {
        "reference_and_generation": "version_adaptive",
        "engine_version_binding": "exact_profile",
        "tool_version_binding": "exact_profile",
        "installable_manifest_versions": "concrete",
        "support_claim_source": "exact_verified_profile_only",
    }
    if payload.get("capability") != expected_capability:
        errors.append(
            "compatibility capability must stay version-adaptive while installable manifests stay concrete"
        )
    evidence_policy = payload.get("evidence_policy")
    if not isinstance(evidence_policy, dict):
        errors.append("compatibility evidence policy must be an object")
    else:
        if evidence_policy.get("claim_unit") != "exact_execution_tuple":
            errors.append("compatibility claims must bind the exact target tuple")
        if evidence_policy.get("tuple_dimensions") != list(COMPATIBILITY_TUPLE_DIMENSIONS):
            errors.append(
                "compatibility tuple dimensions must bind engine/editor, renderer, dependency "
                "resolution, build target, graphics API, XR provider/runtime, input, and device"
            )
        if evidence_policy.get("cross_tuple_transfer_allowed") is not False:
            errors.append("compatibility evidence must not transfer across target tuples")
        if evidence_policy.get("unmatched_target") != {
            "route": "raw_material_regeneration",
            "result_state": "candidate",
            "support_claim_allowed": False,
        }:
            errors.append(
                "unmatched compatibility targets must route to raw_material_regeneration as unsupported candidates"
            )
    if payload.get("allowed_states") != list(COMPATIBILITY_PROFILE_STATES):
        errors.append("compatibility profile states have drifted")

    catalog_versions: dict[str, str] = {}
    catalog_paths: dict[str, str] = {}
    manifests: dict[str, dict[str, Any]] = {}
    for item in catalog.get("packages", []):
        if not isinstance(item, dict):
            continue
        package_id = str(item.get("id", ""))
        if not package_id:
            continue
        catalog_versions[package_id] = str(item.get("version", ""))
        catalog_paths[package_id] = str(item.get("path", ""))
        manifest_path = root / catalog_paths[package_id] / "package.json"
        if manifest_path.exists():
            try:
                manifest = load_json(manifest_path)
                if isinstance(manifest, dict):
                    manifests[package_id] = manifest
            except (json.JSONDecodeError, UnicodeDecodeError):
                pass
    canonical_device_routes: set[str] = set()
    for device_profile_path in (root / "docs" / "device-lab" / "profiles").glob("*.json"):
        try:
            device_profile = load_json(device_profile_path)
        except (json.JSONDecodeError, UnicodeDecodeError):
            continue
        if isinstance(device_profile, dict):
            canonical_device_routes.update(
                str(route) for route in device_profile.get("input_routes", [])
            )

    profiles = payload.get("profiles")
    if not isinstance(profiles, list):
        return errors + ["compatibility profiles must be a list"]
    current_profile_ids = payload.get("current_profile_ids")
    if not isinstance(current_profile_ids, list) or not current_profile_ids:
        errors.append("compatibility current_profile_ids must be a non-empty list")
        current_ids: set[str] = set()
    else:
        current_ids = {str(item) for item in current_profile_ids}

    def dependency_closure(artifact: str) -> tuple[set[str], list[str]]:
        closure: set[str] = set()
        pending = [artifact]
        closure_errors: list[str] = []
        while pending:
            package_id = pending.pop()
            if package_id in closure:
                continue
            manifest = manifests.get(package_id)
            if manifest is None:
                closure_errors.append(f"missing installable manifest for {package_id}")
                continue
            closure.add(package_id)
            dependencies = manifest.get("dependencies", {})
            if not isinstance(dependencies, dict):
                closure_errors.append(f"manifest dependencies must be an object for {package_id}")
                continue
            for dependency_id in dependencies:
                if dependency_id in catalog_versions:
                    pending.append(str(dependency_id))
        return closure, closure_errors

    def expected_resolution(
        package_ids: set[str], requested: dict[str, Any]
    ) -> tuple[dict[str, str], list[str]]:
        resolution: dict[str, str] = {}
        resolution_errors: list[str] = []
        for package_id in sorted(package_ids):
            dependencies = manifests.get(package_id, {}).get("dependencies", {})
            if not isinstance(dependencies, dict):
                continue
            for dependency_id, version in dependencies.items():
                dependency_id = str(dependency_id)
                version = str(version)
                previous = resolution.get(dependency_id)
                if previous is not None and previous != version:
                    resolution_errors.append(
                        f"dependency conflict for {dependency_id}: {previous} versus {version}"
                    )
                else:
                    resolution[dependency_id] = version
        for dependency_id, version in requested.items():
            dependency_id = str(dependency_id)
            version = str(version)
            if dependency_id in catalog_versions:
                resolution_errors.append(
                    f"requested_dependencies must not override repository package {dependency_id}"
                )
                continue
            resolution[dependency_id] = version
        return dict(sorted(resolution.items())), resolution_errors

    def axis_is_not_applicable(axis: Any) -> bool:
        return axis == {"id": "not_applicable", "version": "not_applicable"}

    def validate_local_compatibility_evidence(
        relative_path: Any,
        expected_digest: Any,
        evidence_label: str,
    ) -> list[str]:
        evidence_errors: list[str] = []
        relative = str(relative_path or "")
        digest = str(expected_digest or "")
        parts = Path(relative).parts
        validation_root = (root / "docs" / "validation" / "evidence").resolve()
        if (
            not relative.startswith("docs/validation/evidence/")
            or "\\" in relative
            or ".." in parts
            or Path(relative).is_absolute()
        ):
            return [f"{evidence_label}: path must stay under docs/validation/evidence"]
        evidence_path = root / relative
        try:
            resolved_path = evidence_path.resolve()
            resolved_path.relative_to(validation_root)
        except (OSError, ValueError):
            return [f"{evidence_label}: resolved path escapes docs/validation/evidence"]
        if not evidence_path.is_file():
            evidence_errors.append(f"{evidence_label}: repository evidence file does not exist")
            return evidence_errors
        if evidence_path.is_symlink():
            evidence_errors.append(f"{evidence_label}: repository evidence must not be a symbolic link")
            return evidence_errors
        if SHA256_PATTERN.fullmatch(digest) is None or set(digest) == {"0"}:
            evidence_errors.append(f"{evidence_label}: requires a non-zero SHA-256")
            return evidence_errors
        actual_digest = hashlib.sha256(evidence_path.read_bytes()).hexdigest()
        if actual_digest != digest:
            evidence_errors.append(f"{evidence_label}: SHA-256 does not match repository file")
        return evidence_errors

    def commit_exists(commit_sha: str) -> bool:
        return commit_is_public_origin_reachable(root, commit_sha)

    def load_json_at_commit(commit_sha: str, repository_path: str) -> dict[str, Any] | None:
        try:
            result = subprocess.run(
                ["git", "show", f"{commit_sha}:{repository_path}"],
                cwd=root,
                env=evidence_git_env(),
                capture_output=True,
                check=False,
                timeout=10,
            )
        except (OSError, subprocess.SubprocessError):
            return None
        if result.returncode != 0:
            return None
        try:
            payload_at_commit = json.loads(result.stdout.decode("utf-8"))
        except (UnicodeDecodeError, json.JSONDecodeError):
            return None
        return payload_at_commit if isinstance(payload_at_commit, dict) else None

    def canonical_git_selector(package_id: str, commit_sha: str) -> str:
        return (
            "https://github.com/Lingkyn/xr-foundry.git?path=/"
            f"{catalog_paths.get(package_id, '')}#{commit_sha}"
        )

    def validate_unity_consumer_binding(
        *,
        profile_id: str,
        install_artifact: str,
        commit_sha: str,
        package_versions: dict[str, Any],
        requested_dependencies: dict[str, Any],
        resolved_dependencies: dict[str, Any],
        manifest_path: Path,
        lock_path: Path,
        non_xr_isolation_required: bool,
    ) -> list[str]:
        binding_errors: list[str] = []
        try:
            consumer_manifest = load_json(manifest_path)
            consumer_lock = load_json(lock_path)
        except (json.JSONDecodeError, UnicodeDecodeError) as exc:
            return [f"{profile_id}: Unity manifest/lock evidence is invalid JSON: {exc}"]
        manifest_dependencies = (
            consumer_manifest.get("dependencies")
            if isinstance(consumer_manifest, dict)
            else None
        )
        lock_dependencies = (
            consumer_lock.get("dependencies") if isinstance(consumer_lock, dict) else None
        )
        if not isinstance(manifest_dependencies, dict) or not all(
            isinstance(package_id, str) and isinstance(selector, str)
            for package_id, selector in manifest_dependencies.items()
        ):
            binding_errors.append(
                f"{profile_id}: evidence manifest must be a real Unity Packages/manifest.json"
            )
            manifest_dependencies = {}
        if not isinstance(lock_dependencies, dict) or not lock_dependencies:
            binding_errors.append(
                f"{profile_id}: evidence lock must be a real Unity Packages/packages-lock.json"
            )
            lock_dependencies = {}
        elif any(not isinstance(entry, dict) for entry in lock_dependencies.values()):
            binding_errors.append(
                f"{profile_id}: every Unity lock dependency entry must be an object"
            )

        expected_root_selector = canonical_git_selector(install_artifact, commit_sha)
        if manifest_dependencies.get(install_artifact) != expected_root_selector:
            binding_errors.append(
                f"{profile_id}: consumer manifest must pin install_artifact to its canonical path and commit"
            )
        for dependency_id, requested_version in requested_dependencies.items():
            if manifest_dependencies.get(dependency_id) != requested_version:
                binding_errors.append(
                    f"{profile_id}: consumer manifest requested dependency drift: {dependency_id}"
                )
        for dependency_id in manifest_dependencies:
            entry = lock_dependencies.get(dependency_id)
            if not isinstance(entry, dict):
                binding_errors.append(
                    f"{profile_id}: Unity lock omits direct manifest dependency: {dependency_id}"
                )
            elif entry.get("depth") != 0:
                binding_errors.append(
                    f"{profile_id}: direct manifest dependency must have depth=0: {dependency_id}"
                )

        commit_manifests: dict[str, dict[str, Any]] = {}
        expected_custom_closure: set[str] = set()
        pending_custom = [install_artifact]
        while pending_custom:
            package_id = pending_custom.pop()
            if package_id in expected_custom_closure:
                continue
            package_path = catalog_paths.get(package_id)
            if package_path is None:
                binding_errors.append(
                    f"{profile_id}: commit-bound custom package is absent from catalog: {package_id}"
                )
                continue
            package_manifest = load_json_at_commit(
                commit_sha, f"{package_path}/package.json"
            )
            if package_manifest is None:
                binding_errors.append(
                    f"{profile_id}: package.json is not readable at evidence commit: {package_id}"
                )
                continue
            commit_manifests[package_id] = package_manifest
            expected_custom_closure.add(package_id)
            dependencies = package_manifest.get("dependencies", {})
            if not isinstance(dependencies, dict):
                binding_errors.append(
                    f"{profile_id}: commit package dependencies are malformed: {package_id}"
                )
                continue
            pending_custom.extend(
                str(dependency_id)
                for dependency_id in dependencies
                if dependency_id in catalog_versions
            )
        if set(package_versions) != expected_custom_closure:
            binding_errors.append(
                f"{profile_id}: receipt package_versions must equal commit-bound custom closure"
            )

        for package_id, expected_version in package_versions.items():
            package_manifest = commit_manifests.get(package_id)
            if package_manifest is None:
                continue
            expected_selector = canonical_git_selector(package_id, commit_sha)
            if manifest_dependencies.get(package_id) != expected_selector:
                binding_errors.append(
                    f"{profile_id}: consumer manifest must pin every custom closure package "
                    f"to its canonical path and commit: {package_id}"
                )
            if package_manifest.get("version") != expected_version:
                binding_errors.append(
                    f"{profile_id}: package version does not match package.json at evidence commit: "
                    f"{package_id}"
                )
            entry = lock_dependencies.get(package_id)
            if not isinstance(entry, dict):
                binding_errors.append(
                    f"{profile_id}: Unity lock omits custom package entry: {package_id}"
                )
                continue
            if entry.get("source") != "git":
                binding_errors.append(
                    f"{profile_id}: custom lock package source must be git: {package_id}"
                )
            if entry.get("hash") != commit_sha:
                binding_errors.append(
                    f"{profile_id}: custom lock package hash must equal evidence commit: {package_id}"
                )
            if entry.get("version") != expected_selector:
                binding_errors.append(
                    f"{profile_id}: custom lock selector must bind canonical path and commit: {package_id}"
                )
            expected_dependencies = package_manifest.get("dependencies", {})
            if entry.get("dependencies") != expected_dependencies:
                binding_errors.append(
                    f"{profile_id}: custom lock dependency edges drift from commit package.json: "
                    f"{package_id}"
                )

        relevant_external: dict[str, str] = {}
        visited: set[str] = set()
        pending_lock = list(manifest_dependencies)
        traversed_edges = 0
        while pending_lock:
            dependency_id = pending_lock.pop()
            if dependency_id in visited:
                continue
            visited.add(dependency_id)
            entry = lock_dependencies.get(dependency_id)
            if not isinstance(entry, dict):
                binding_errors.append(
                    f"{profile_id}: Unity lock omits reachable dependency entry: {dependency_id}"
                )
                continue
            depth = entry.get("depth")
            dependencies = entry.get("dependencies")
            if not isinstance(depth, int) or isinstance(depth, bool) or depth < 0:
                binding_errors.append(
                    f"{profile_id}: Unity lock dependency depth is invalid: {dependency_id}"
                )
            if not isinstance(dependencies, dict):
                binding_errors.append(
                    f"{profile_id}: Unity lock dependency edges are malformed: {dependency_id}"
                )
                dependencies = {}
            for child_id, edge_version in dependencies.items():
                child_id = str(child_id)
                child = lock_dependencies.get(child_id)
                if not isinstance(child, dict):
                    binding_errors.append(
                        f"{profile_id}: Unity lock edge points to a missing dependency: "
                        f"{dependency_id} -> {child_id}"
                    )
                    continue
                traversed_edges += 1
                if child_id in package_versions:
                    edge_is_satisfied = edge_version == package_versions[child_id]
                else:
                    edge_is_satisfied = unity_external_dependency_is_satisfied(
                        edge_version, child.get("version")
                    )
                if not edge_is_satisfied:
                    binding_errors.append(
                        f"{profile_id}: Unity lock edge version drift: "
                        f"{dependency_id} -> {child_id}"
                    )
                parent_depth = entry.get("depth")
                child_depth = child.get("depth")
                if (
                    isinstance(parent_depth, int)
                    and not isinstance(parent_depth, bool)
                    and isinstance(child_depth, int)
                    and not isinstance(child_depth, bool)
                    and child_depth > parent_depth + 1
                ):
                    binding_errors.append(
                        f"{profile_id}: Unity lock child depth is inconsistent: "
                        f"{dependency_id} -> {child_id}"
                    )
            if dependency_id not in catalog_versions:
                version = entry.get("version")
                if entry.get("source") not in {"registry", "builtin"}:
                    binding_errors.append(
                        f"{profile_id}: reachable non-custom lock source is unsupported: {dependency_id}"
                    )
                if not isinstance(version, str) or SEMVER_PATTERN.fullmatch(version) is None:
                    binding_errors.append(
                        f"{profile_id}: reachable non-custom lock version must be exact: {dependency_id}"
                    )
                else:
                    relevant_external[dependency_id] = version
            pending_lock.extend(str(child_id) for child_id in dependencies)

        if relevant_external and traversed_edges == 0:
            binding_errors.append(
                f"{profile_id}: Unity lock reachable dependency graph must contain real edges"
            )
        expected_depths: dict[str, int] = {}
        depth_queue = [(str(package_id), 0) for package_id in manifest_dependencies]
        while depth_queue:
            dependency_id, depth = depth_queue.pop(0)
            if dependency_id in expected_depths and expected_depths[dependency_id] <= depth:
                continue
            expected_depths[dependency_id] = depth
            entry = lock_dependencies.get(dependency_id)
            if not isinstance(entry, dict):
                continue
            dependencies = entry.get("dependencies", {})
            if isinstance(dependencies, dict):
                depth_queue.extend((str(child_id), depth + 1) for child_id in dependencies)
        for dependency_id, expected_depth in expected_depths.items():
            entry = lock_dependencies.get(dependency_id)
            if isinstance(entry, dict) and entry.get("depth") != expected_depth:
                binding_errors.append(
                    f"{profile_id}: Unity lock depth must equal the shortest manifest path: "
                    f"{dependency_id}"
                )
        if dict(sorted(relevant_external.items())) != dict(sorted(resolved_dependencies.items())):
            binding_errors.append(
                f"{profile_id}: resolved_dependencies must equal the exact reachable non-custom lock graph"
            )
        if non_xr_isolation_required:
            forbidden_xr_entries = {
                package_id
                for package_id in lock_dependencies
                if package_id.startswith("com.unity.xr.")
                or package_id.startswith("com.lingkyn.inventory.xr.")
            }
            if forbidden_xr_entries:
                binding_errors.append(
                    f"{profile_id}: non-XR UGUI lock must contain no XR package entries: "
                    f"{sorted(forbidden_xr_entries)}"
                )
        return binding_errors

    def validate_device_runtime_binding(
        *,
        profile_id: str,
        commit_sha: str,
        target: dict[str, Any],
        package_versions: dict[str, Any],
        resolved_dependencies: dict[str, Any],
        manifest_artifact: dict[str, Any],
        lock_artifact: dict[str, Any],
        binding: Any,
    ) -> tuple[list[str], tuple[str, str] | None]:
        binding_errors: list[str] = []
        if not isinstance(binding, dict):
            return [f"{profile_id}: device_runtime evidence requires a Device Lab receipt binding"], None
        relative_path = str(binding.get("path", ""))
        digest = str(binding.get("sha256", ""))
        path = root / relative_path
        receipt_root = (root / "docs" / "device-lab" / "receipts").resolve()
        try:
            resolved_path = path.resolve(strict=True)
            resolved_path.relative_to(receipt_root)
        except (OSError, ValueError):
            resolved_path = None
        if (
            not relative_path.startswith("docs/device-lab/receipts/")
            or Path(relative_path).parent.as_posix() != "docs/device-lab/receipts"
            or Path(relative_path).suffix != ".json"
            or "\\" in relative_path
            or ".." in Path(relative_path).parts
            or path.is_symlink()
            or resolved_path is None
            or not path.is_file()
        ):
            return [
                f"{profile_id}: device_runtime must bind a real docs/device-lab/receipts/*.json file"
            ], None
        if SHA256_PATTERN.fullmatch(digest) is None or set(digest) == {"0"}:
            binding_errors.append(f"{profile_id}: Device Lab receipt requires a non-zero SHA-256")
        elif hashlib.sha256(path.read_bytes()).hexdigest() != digest:
            binding_errors.append(f"{profile_id}: Device Lab receipt SHA-256 does not match file")
        try:
            device_receipt = load_json(path)
        except (json.JSONDecodeError, UnicodeDecodeError) as exc:
            return binding_errors + [f"{profile_id}: Device Lab receipt is invalid JSON: {exc}"], None
        device_profiles = {
            str(item.get("profile_id", "")): item
            for profile_path in (root / "docs" / "device-lab" / "profiles").glob("*.json")
            if isinstance(item := load_json(profile_path), dict)
        }
        device_plans = {
            str(item.get("test_plan_id", "")): item
            for plan_path in (root / "docs" / "device-lab" / "test-plans").glob("*.json")
            if isinstance(item := load_json(plan_path), dict)
        }
        binding_errors.extend(
            validate_device_lab_execution_receipt(
                device_receipt,
                device_profiles,
                device_plans,
                f"{profile_id}: bound Device Lab receipt",
            )
        )
        if device_receipt.get("overall_result") != "pass":
            binding_errors.append(f"{profile_id}: device_runtime requires a passing Device Lab receipt")
        if device_receipt.get("compatibility_profile_id") != profile_id:
            binding_errors.append(
                f"{profile_id}: Device Lab receipt compatibility_profile_id must match"
            )
        revision = device_receipt.get("revision", {})
        if not isinstance(revision, dict) or revision.get("commit_sha") != commit_sha:
            binding_errors.append(f"{profile_id}: Device Lab receipt revision must match exactly")
        dependency_resolution = device_receipt.get("dependency_resolution", {})
        if not isinstance(dependency_resolution, dict):
            dependency_resolution = {}
        for field, expected in (
            ("manifest", manifest_artifact),
            ("lock", lock_artifact),
        ):
            artifact = dependency_resolution.get(field)
            if not isinstance(artifact, dict) or (
                artifact.get("ref") != expected.get("path")
                or artifact.get("sha256") != expected.get("sha256")
            ):
                binding_errors.append(
                    f"{profile_id}: Device Lab {field} must match compatibility evidence exactly"
                )
        package_tuple = device_receipt.get("package_tuple", {})
        tuple_versions: dict[str, str] = {}
        if isinstance(package_tuple, dict):
            for role in ("domain", "presentation", "renderer_adapter", "xr_adapter"):
                package = package_tuple.get(role)
                if isinstance(package, dict):
                    tuple_versions[str(package.get("id", ""))] = str(package.get("version", ""))
        if tuple_versions != package_versions:
            binding_errors.append(
                f"{profile_id}: Device Lab package tuple must equal compatibility package_versions"
            )
        resolved = dependency_resolution.get("resolved_packages", [])
        device_resolved = {
            str(item.get("id", "")): str(item.get("version", ""))
            for item in resolved
            if isinstance(item, dict)
        }
        if device_resolved != resolved_dependencies:
            binding_errors.append(
                f"{profile_id}: Device Lab resolved graph must equal compatibility resolved_dependencies"
            )
        software = device_receipt.get("software", {})
        if not isinstance(software, dict):
            software = {}
        engine = target.get("engine", {})
        editor = target.get("editor", {})
        runtime = target.get("runtime", {})
        if (
            not isinstance(engine, dict)
            or engine.get("id") != "unity"
            or engine.get("version") != software.get("engine_version")
            or not isinstance(editor, dict)
            or editor.get("id") != "unity-editor"
            or editor.get("version") != software.get("editor_version")
        ):
            binding_errors.append(f"{profile_id}: Device Lab engine/editor tuple must match exactly")
        if (
            not isinstance(runtime, dict)
            or axis_is_not_applicable(runtime)
            or runtime.get("id") != software.get("runtime_id")
            or runtime.get("version") != software.get("runtime_version")
        ):
            binding_errors.append(f"{profile_id}: Device Lab runtime tuple must match exactly")
        build = device_receipt.get("build", {})
        expected_build = {
            "target": target.get("build_target"),
            "graphics_api": target.get("graphics_api"),
            "scripting_backend": target.get("scripting_backend"),
            "architecture": target.get("architecture"),
        }
        if build != expected_build:
            binding_errors.append(f"{profile_id}: Device Lab build tuple must match exactly")
        device = device_receipt.get("device", {})
        target_device = target.get("device", {})
        if (
            not isinstance(device, dict)
            or not isinstance(target_device, dict)
            or axis_is_not_applicable(target_device)
            or target_device.get("id") != device.get("family_id")
            or target_device.get("version") != device.get("os_version")
        ):
            binding_errors.append(f"{profile_id}: Device Lab device tuple must match exactly")
        input_state = device_receipt.get("input", {})
        if (
            not isinstance(input_state, dict)
            or sorted(input_state.get("routes", [])) != target.get("input_routes")
        ):
            binding_errors.append(f"{profile_id}: Device Lab input route tuple must match exactly")
        for axis_name in ("renderer", "xr_provider"):
            axis = target.get(axis_name, {})
            if (
                not isinstance(axis, dict)
                or axis_is_not_applicable(axis)
                or device_resolved.get(str(axis.get("id", ""))) != axis.get("version")
            ):
                binding_errors.append(
                    f"{profile_id}: Device Lab {axis_name} tuple must be applicable and exact"
                )
        manifest_path = root / str(manifest_artifact.get("path", ""))
        if manifest_path.is_file():
            manifest_payload = load_json(manifest_path)
            direct_dependencies = manifest_payload.get("dependencies", {})
            if isinstance(direct_dependencies, dict):
                requested = {
                    str(package_id): str(version)
                    for package_id, version in direct_dependencies.items()
                    if package_id not in package_versions
                }
                if requested != target.get("requested_dependencies"):
                    binding_errors.append(
                        f"{profile_id}: Device Lab manifest direct dependencies must match the "
                        "requested dependency tuple"
                    )
        return binding_errors, (relative_path, digest)

    profile_ids: set[str] = set()
    current_install_artifacts: dict[str, str] = {}
    for profile in profiles:
        if not isinstance(profile, dict):
            errors.append("compatibility profile entries must be objects")
            continue
        profile_id = str(profile.get("id", ""))
        if not profile_id or profile_id in profile_ids:
            errors.append(f"compatibility profile id is missing or duplicated: {profile_id}")
        profile_ids.add(profile_id)
        state = profile.get("state")
        if state not in COMPATIBILITY_PROFILE_STATES:
            errors.append(f"{profile_id}: compatibility profile state is invalid")
        is_current = profile_id in current_ids
        install_artifact = str(profile.get("install_artifact", ""))
        if install_artifact not in catalog_versions:
            errors.append(f"{profile_id}: install_artifact must match a catalog package")
        elif is_current:
            previous_profile = current_install_artifacts.get(install_artifact)
            if previous_profile is not None:
                errors.append(
                    f"{profile_id}: current install artifact duplicates profile {previous_profile}: "
                    f"{install_artifact}"
                )
            current_install_artifacts[install_artifact] = profile_id

        target = profile.get("target")
        if not isinstance(target, dict):
            errors.append(f"{profile_id}: target tuple must be an object")
            target = {}
        elif set(target) != set(COMPATIBILITY_TUPLE_DIMENSIONS):
            errors.append(f"{profile_id}: target tuple must contain every exact execution dimension")
        for dimension in ("engine", "editor", "renderer", "xr_provider", "runtime", "device"):
            axis = target.get(dimension)
            if not isinstance(axis, dict):
                errors.append(f"{profile_id}: target.{dimension} must be an object")
                continue
            if not is_concrete_version_literal(
                axis.get("version"),
                allow_not_applicable=(
                    dimension == "device"
                    or str(axis.get("id", "")).casefold() == "not_applicable"
                ),
            ):
                errors.append(
                    f"{profile_id}: target.{dimension}.version must be a concrete profile value"
                )
            if isinstance(axis, dict) and (
                (axis.get("id") == "not_applicable")
                != (axis.get("version") == "not_applicable")
            ):
                errors.append(
                    f"{profile_id}: target.{dimension} must use the complete not_applicable axis"
                )
        for dimension in ("engine", "editor"):
            axis = target.get(dimension)
            if isinstance(axis, dict) and UNITY_EDITOR_VERSION_PATTERN.fullmatch(
                str(axis.get("version", ""))
            ) is None:
                errors.append(
                    f"{profile_id}: target.{dimension}.version must be an exact Unity release version"
                )
        for dimension in ("renderer", "xr_provider"):
            axis = target.get(dimension)
            if (
                isinstance(axis, dict)
                and not axis_is_not_applicable(axis)
                and SEMVER_PATTERN.fullmatch(str(axis.get("version", ""))) is None
            ):
                errors.append(
                    f"{profile_id}: target.{dimension}.version must be an exact semantic version"
                )
        for dimension in ("runtime", "device"):
            axis = target.get(dimension)
            if isinstance(axis, dict) and not is_exact_runtime_version(
                axis.get("version"), allow_not_applicable=True
            ):
                errors.append(
                    f"{profile_id}: target.{dimension}.version must be an exact dotted version"
                )
        engine = target.get("engine")
        editor = target.get("editor")
        if isinstance(engine, dict) and engine.get("id") != "unity":
            errors.append(f"{profile_id}: target.engine.id must be unity")
        if isinstance(editor, dict) and editor.get("id") != "unity-editor":
            errors.append(f"{profile_id}: target.editor.id must be unity-editor")
        if (
            isinstance(engine, dict)
            and isinstance(editor, dict)
            and engine.get("version") != editor.get("version")
        ):
            errors.append(f"{profile_id}: engine and editor versions must match exactly")
        for scalar in ("build_target", "graphics_api", "scripting_backend", "architecture"):
            if not is_concrete_version_literal(target.get(scalar)):
                errors.append(f"{profile_id}: target.{scalar} must be an exact concrete value")
        input_routes = target.get("input_routes")
        if (
            not isinstance(input_routes, list)
            or not input_routes
            or any(not isinstance(route, str) or not route for route in input_routes)
            or input_routes != sorted(set(input_routes))
        ):
            errors.append(f"{profile_id}: target.input_routes must be a sorted unique non-empty list")
            input_routes = []
        requested_dependencies = target.get("requested_dependencies")
        resolved_dependencies = target.get("resolved_dependencies")
        if not isinstance(requested_dependencies, dict):
            errors.append(f"{profile_id}: target.requested_dependencies must be an exact mapping")
            requested_dependencies = {}
        if not isinstance(resolved_dependencies, dict):
            errors.append(f"{profile_id}: target.resolved_dependencies must be an exact mapping")
            resolved_dependencies = {}

        package_versions = profile.get("package_versions")
        if not isinstance(package_versions, dict) or not package_versions:
            errors.append(f"{profile_id}: package_versions must be a non-empty mapping")
            package_versions = {}
        for package_id, version in package_versions.items():
            if package_id not in catalog_versions:
                errors.append(f"{profile_id}: unknown package in profile: {package_id}")
                continue
            manifest_version = str(manifests.get(package_id, {}).get("version", ""))
            if is_current and (
                version != catalog_versions[package_id] or version != manifest_version
            ):
                errors.append(
                    f"{profile_id}: current package version must match catalog and installable manifest: "
                    f"{package_id}"
                )

        if is_current and install_artifact in catalog_versions:
            closure, closure_errors = dependency_closure(install_artifact)
            for error in closure_errors:
                errors.append(f"{profile_id}: {error}")
            if set(package_versions) != closure:
                errors.append(
                    f"{profile_id}: package_versions must equal the install artifact dependency closure; "
                    f"expected {sorted(closure)}"
                )
            expected_dependencies, resolution_errors = expected_resolution(
                closure, requested_dependencies
            )
            for error in resolution_errors:
                errors.append(f"{profile_id}: {error}")
            custom_resolution_ids = set(resolved_dependencies) & set(catalog_versions)
            if custom_resolution_ids:
                errors.append(
                    f"{profile_id}: resolved_dependencies must contain only non-custom lock entries: "
                    f"{sorted(custom_resolution_ids)}"
                )
            required_external_ids = set(expected_dependencies) - set(catalog_versions)
            missing_direct_dependencies = required_external_ids - set(resolved_dependencies)
            if missing_direct_dependencies:
                errors.append(
                    f"{profile_id}: resolved_dependencies omit direct external requirements: "
                    f"{sorted(missing_direct_dependencies)}"
                )
            for dependency_id, requested_version in requested_dependencies.items():
                if resolved_dependencies.get(dependency_id) != requested_version:
                    errors.append(
                        f"{profile_id}: requested dependency must match its resolved version exactly: "
                        f"{dependency_id}"
                    )

        renderer = target.get("renderer")
        if isinstance(renderer, dict) and not axis_is_not_applicable(renderer):
            if resolved_dependencies.get(renderer.get("id")) != renderer.get("version"):
                errors.append(f"{profile_id}: renderer must match resolved_dependencies exactly")
        xr_provider = target.get("xr_provider")
        runtime = target.get("runtime")
        provider_not_applicable = axis_is_not_applicable(xr_provider)
        runtime_not_applicable = axis_is_not_applicable(runtime)
        if provider_not_applicable:
            if not runtime_not_applicable:
                errors.append(f"{profile_id}: runtime requires an applicable XR provider")
            xr_dependencies = {
                dependency_id
                for dependency_id in resolved_dependencies
                if dependency_id.startswith("com.unity.xr.")
            }
            if xr_dependencies:
                errors.append(
                    f"{profile_id}: non-XR target must not resolve XR packages: {sorted(xr_dependencies)}"
                )
            if any(str(route).startswith("xri-") for route in input_routes):
                errors.append(f"{profile_id}: non-XR target must not declare XRI input routes")
        else:
            if not isinstance(xr_provider, dict) or (
                resolved_dependencies.get(xr_provider.get("id")) != xr_provider.get("version")
            ):
                errors.append(f"{profile_id}: XR provider must match resolved_dependencies exactly")
            if "com.unity.xr.interaction.toolkit" not in resolved_dependencies:
                errors.append(f"{profile_id}: XR target must resolve XR Interaction Toolkit")
            unsupported_routes = {
                str(route)
                for route in input_routes
                if not str(route).startswith("xri-")
                and str(route) not in canonical_device_routes
            }
            if unsupported_routes or not input_routes:
                errors.append(
                    f"{profile_id}: XR target input routes must use XRI or a canonical Device Lab route: "
                    f"{sorted(unsupported_routes)}"
                )

        claims = profile.get("verified_claims")
        evidence = profile.get("evidence")
        if not isinstance(claims, list):
            claims = []
        if not isinstance(evidence, list):
            evidence = []
        if state in {"candidate", "pending_automated_validation"}:
            if claims:
                errors.append(f"{profile_id}: unverified profile must not publish verified claims")
            if evidence:
                errors.append(f"{profile_id}: unverified profile must not carry verification evidence")
        elif state == "verified":
            if not claims:
                errors.append(f"{profile_id}: verified profile must name exact verified claims")
            if not evidence:
                errors.append(f"{profile_id}: verified profile must carry revision-bound evidence")

        evidence_kinds: set[str] = set()
        passed_evidence_checks: set[str] = set()
        for item in evidence:
            if not isinstance(item, dict):
                errors.append(f"{profile_id}: evidence entries must be objects")
                continue
            evidence_kinds.add(str(item.get("kind", "")))
            if item.get("profile_id") != profile_id:
                errors.append(f"{profile_id}: evidence profile_id must match its profile")
            commit_sha = str(item.get("commit_sha", ""))
            if FULL_SHA_PATTERN.fullmatch(commit_sha) is None or set(commit_sha) == {"0"}:
                errors.append(f"{profile_id}: evidence requires a non-zero full commit SHA")
            elif not commit_exists(commit_sha):
                errors.append(
                    f"{profile_id}: evidence commit_sha must resolve and be reachable from a fetched "
                    "public origin ref"
                )
            for digest_field in ("manifest_sha256", "lock_sha256"):
                digest = str(item.get(digest_field, ""))
                if SHA256_PATTERN.fullmatch(digest) is None or set(digest) == {"0"}:
                    errors.append(f"{profile_id}: evidence requires a non-zero {digest_field}")
            receipt_path = str(item.get("receipt_path", ""))
            receipt_parts = Path(receipt_path).parts
            if (
                not receipt_path.startswith("docs/validation/")
                or "\\" in receipt_path
                or ".." in receipt_parts
                or not receipt_path.endswith(".json")
                or not (root / receipt_path).is_file()
            ):
                errors.append(
                    f"{profile_id}: evidence receipt_path must name an existing machine-readable public receipt"
                )
                continue
            try:
                receipt = load_json(root / receipt_path)
            except (json.JSONDecodeError, UnicodeDecodeError) as exc:
                errors.append(f"{profile_id}: compatibility evidence receipt is invalid JSON: {exc}")
                continue
            errors.extend(
                validate_json_schema_instance(
                    receipt,
                    root / COMPATIBILITY_EVIDENCE_SCHEMA,
                    f"{profile_id}: compatibility evidence receipt",
                )
            )
            if not isinstance(receipt, dict):
                continue
            exact_bindings = {
                "kind": item.get("kind"),
                "profile_id": profile_id,
                "commit_sha": commit_sha,
                "target": target,
                "package_versions": package_versions,
                "resolved_dependencies": resolved_dependencies,
            }
            for field, expected in exact_bindings.items():
                if receipt.get(field) != expected:
                    errors.append(
                        f"{profile_id}: receipt {field} must exactly match its evidence reference/profile"
                    )
            bound_artifact_paths: dict[str, Path] = {}
            for artifact_field, reference_field in (
                ("manifest", "manifest_sha256"),
                ("lock", "lock_sha256"),
            ):
                artifact = receipt.get(artifact_field)
                if not isinstance(artifact, dict):
                    errors.append(f"{profile_id}: receipt {artifact_field} must be a bound artifact")
                    continue
                digest = str(artifact.get("sha256", ""))
                if digest != item.get(reference_field):
                    errors.append(
                        f"{profile_id}: receipt {artifact_field} hash must match {reference_field}"
                    )
                if SHA256_PATTERN.fullmatch(digest) is None or set(digest) == {"0"}:
                    errors.append(f"{profile_id}: receipt {artifact_field} requires a non-zero SHA-256")
                artifact_path = str(artifact.get("path", ""))
                errors.extend(
                    validate_local_compatibility_evidence(
                        artifact_path,
                        digest,
                        f"{profile_id}: receipt {artifact_field}",
                    )
                )
                candidate_path = root / artifact_path
                if candidate_path.is_file():
                    bound_artifact_paths[artifact_field] = candidate_path
            if {"manifest", "lock"}.issubset(bound_artifact_paths):
                errors.extend(
                    validate_unity_consumer_binding(
                        profile_id=profile_id,
                        install_artifact=install_artifact,
                        commit_sha=commit_sha,
                        package_versions=package_versions,
                        requested_dependencies=requested_dependencies,
                        resolved_dependencies=resolved_dependencies,
                        manifest_path=bound_artifact_paths["manifest"],
                        lock_path=bound_artifact_paths["lock"],
                        non_xr_isolation_required=(
                            install_artifact == "com.lingkyn.inventory.ugui"
                            and provider_not_applicable
                        ),
                    )
                )
            bound_device_receipt: tuple[str, str] | None = None
            if receipt.get("kind") == "device_runtime":
                device_errors, bound_device_receipt = validate_device_runtime_binding(
                    profile_id=profile_id,
                    commit_sha=commit_sha,
                    target=target,
                    package_versions=package_versions,
                    resolved_dependencies=resolved_dependencies,
                    manifest_artifact=(
                        receipt.get("manifest")
                        if isinstance(receipt.get("manifest"), dict)
                        else {}
                    ),
                    lock_artifact=(
                        receipt.get("lock")
                        if isinstance(receipt.get("lock"), dict)
                        else {}
                    ),
                    binding=receipt.get("device_lab_receipt"),
                )
                errors.extend(device_errors)
            elif receipt.get("device_lab_receipt") is not None:
                errors.append(
                    f"{profile_id}: only device_runtime evidence may bind a Device Lab receipt"
                )
            receipt_checks = receipt.get("checks")
            passed_checks: set[str] = set()
            seen_checks: set[str] = set()
            expected_ref_kind = {
                "editor_compile": "compile_result",
                "editmode_tests": "nunit_result",
                "playmode_tests": "nunit_result",
                "android_build": "build_artifact",
                "device_runtime": "device_lab_receipt",
            }
            if not isinstance(receipt_checks, list):
                errors.append(f"{profile_id}: receipt checks must be a list")
                receipt_checks = []
            for check in receipt_checks:
                if not isinstance(check, dict):
                    continue
                check_id = str(check.get("id", ""))
                if check_id in seen_checks:
                    errors.append(f"{profile_id}: receipt check is duplicated: {check_id}")
                seen_checks.add(check_id)
                if check.get("status") == "pass":
                    refs = check.get("evidence_refs")
                    if not isinstance(refs, list) or not refs:
                        errors.append(f"{profile_id}: passed receipt check requires evidence_refs: {check_id}")
                    else:
                        for ref in refs:
                            digest = str(ref.get("sha256", "")) if isinstance(ref, dict) else ""
                            if SHA256_PATTERN.fullmatch(digest) is None or set(digest) == {"0"}:
                                errors.append(
                                    f"{profile_id}: passed check evidence requires a non-zero SHA-256: "
                                    f"{check_id}"
                                )
                            reference = ref.get("ref") if isinstance(ref, dict) else None
                            kind = ref.get("kind") if isinstance(ref, dict) else None
                            if kind != expected_ref_kind.get(check_id):
                                errors.append(
                                    f"{profile_id}: passed check evidence kind is invalid for "
                                    f"{check_id}"
                                )
                            if kind == "device_lab_receipt":
                                if bound_device_receipt != (str(reference or ""), digest):
                                    errors.append(
                                        f"{profile_id}: device_runtime check must reference the same "
                                        "bound Device Lab receipt"
                                    )
                            else:
                                errors.extend(
                                    validate_local_compatibility_evidence(
                                        reference,
                                        digest,
                                        f"{profile_id}: passed check evidence {check_id}",
                                    )
                                )
                            evidence_path = root / str(reference or "")
                            if evidence_path.is_file() and kind == "compile_result":
                                errors.extend(
                                    validate_unity_compile_result(
                                        evidence_path,
                                        root=root,
                                        profile_id=profile_id,
                                        commit_sha=commit_sha,
                                        target=target,
                                        manifest_sha256=str(item.get("manifest_sha256", "")),
                                        lock_sha256=str(item.get("lock_sha256", "")),
                                        label=(
                                            f"{profile_id}: passed check evidence {check_id}"
                                        ),
                                    )
                                )
                            elif evidence_path.is_file() and kind == "nunit_result":
                                errors.extend(
                                    validate_nunit_result(
                                        evidence_path,
                                        f"{profile_id}: passed check evidence {check_id}",
                                        check_id=check_id,
                                        root=root,
                                        commit_sha=commit_sha,
                                        catalog_paths=catalog_paths,
                                        package_ids=set(package_versions),
                                        manifest_path=bound_artifact_paths.get("manifest"),
                                    )
                                )
                            elif evidence_path.is_file() and kind == "build_artifact":
                                build_bytes = evidence_path.read_bytes()
                                if (
                                    not build_bytes
                                    or evidence_path.suffix.casefold() != ".apk"
                                    or build_bytes.startswith(
                                        b"version https://git-lfs.github.com/spec/v1"
                                    )
                                ):
                                    errors.append(
                                        f"{profile_id}: android_build evidence must be a materialized "
                                        "non-empty APK"
                                    )
                    passed_checks.add(check_id)
            referenced_checks = item.get("checks")
            if not isinstance(referenced_checks, list):
                referenced_checks = []
            if set(referenced_checks) != passed_checks:
                errors.append(
                    f"{profile_id}: evidence reference checks must exactly equal passed receipt checks"
                )
            passed_evidence_checks.update(passed_checks)

        required_evidence_kinds = {
            "editor_compile": "editor_automated",
            "editmode_tests": "editor_automated",
            "playmode_tests": "editor_automated",
            "android_build": "android_build",
            "device_runtime": "device_runtime",
        }
        for claim in claims:
            required_kind = required_evidence_kinds.get(str(claim))
            if required_kind and required_kind not in evidence_kinds:
                errors.append(f"{profile_id}: verified claim {claim} lacks matching evidence")
            if str(claim) not in passed_evidence_checks:
                errors.append(f"{profile_id}: verified claim {claim} lacks a passed receipt check")

    unknown = current_ids - profile_ids
    if unknown:
        errors.append(f"compatibility current_profile_ids are unknown: {sorted(unknown)}")
    uncovered = set(catalog_versions) - set(current_install_artifacts)
    unexpected = set(current_install_artifacts) - set(catalog_versions)
    if uncovered or unexpected:
        errors.append(
            "current compatibility profiles must cover every installable catalog artifact exactly once; "
            f"uncovered={sorted(uncovered)}, unexpected={sorted(unexpected)}"
        )
    errors.extend(validate_concrete_package_manifests(root, catalog))
    return errors


def validate_compatibility_profiles(
    root: Path, catalog: dict[str, Any]
) -> list[str]:
    schema_path = root / COMPATIBILITY_EVIDENCE_SCHEMA
    schema_errors: list[str] = []
    for current_schema, label in (
        (schema_path, "compatibility evidence"),
        (root / UNITY_COMPILE_RESULT_SCHEMA, "Unity compile result"),
    ):
        if not current_schema.is_file():
            schema_errors.append(f"{label} JSON Schema is missing")
        else:
            try:
                Draft202012Validator.check_schema(load_json(current_schema))
            except Exception as error:
                schema_errors.append(f"{label} JSON Schema is invalid: {error}")
    path = root / COMPATIBILITY_PROFILES
    if not path.exists():
        return schema_errors + ["compatibility profiles are missing"]
    try:
        payload = load_json(path)
    except (json.JSONDecodeError, UnicodeDecodeError) as exc:
        return schema_errors + [f"compatibility profiles are invalid JSON: {exc}"]
    return schema_errors + validate_compatibility_profile_payload(payload, root, catalog)


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


def validate_asmdef_identity(package_root: Path, display_root: Path | None = None) -> list[str]:
    errors: list[str] = []
    label_root = display_root or package_root
    for asmdef in package_root.rglob("*.asmdef"):
        payload = load_json(asmdef)
        declared_name = str(payload.get("name", ""))
        try:
            label = asmdef.relative_to(label_root)
        except ValueError:
            label = asmdef
        if not declared_name.startswith("Lingkyn."):
            errors.append(f"{label}: asmdef name must start with Lingkyn.")
        if asmdef.stem != declared_name:
            errors.append(f"{label}: asmdef filename stem must equal declared name")
        if not asmdef.with_name(asmdef.name + ".meta").exists():
            errors.append(f"{label}: missing .meta")
    return errors


def validate_reference_package_use_modes(reference_catalog: Any) -> list[str]:
    errors: list[str] = []
    if not isinstance(reference_catalog, dict):
        return ["reference catalog must be an object"]
    for artifact in reference_catalog.get("artifacts", []):
        if not isinstance(artifact, dict) or not artifact.get("package_id"):
            continue
        use_modes = artifact.get("use_modes")
        artifact_id = str(artifact.get("id", artifact.get("package_id", "")))
        if not isinstance(use_modes, list) or "install" not in use_modes:
            errors.append(f"{artifact_id}: installable package reference must include install use mode")
        if not isinstance(use_modes, list) or "raw_material" not in use_modes:
            errors.append(
                f"{artifact_id}: version-adaptive package reference must include raw_material use mode"
            )
    return errors


def validate_repository(root: Path) -> list[str]:
    errors: list[str] = scan_text_safety(root)
    errors.extend(validate_ignore_scope(root))
    errors.extend(validate_active_repository_path_references(root))
    errors.extend(validate_agent_guide_source_boundary(root))
    errors.extend(validate_task_hall_contract(root))
    errors.extend(validate_foundry_contract(root))
    errors.extend(validate_device_lab_contract(root))
    errors.extend(validate_workflow_security(root))
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
    errors.extend(validate_compatibility_profiles(root, catalog))
    errors.extend(validate_repository_layout(root, catalog))
    errors.extend(validate_bug_template_package_options(root, catalog))
    errors.extend(validate_active_git_upm_selectors(root, catalog))
    errors.extend(validate_readme_git_install_matrix(root, catalog))

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
        errors.extend(validate_reference_package_use_modes(reference_catalog))
        errors.extend(validate_reference_evidence_paths(root, reference_catalog))

    catalog_packages = catalog.get("packages", [])
    declared_paths: set[str] = set()
    declared_package_roots: dict[str, Path] = {}
    for item in catalog_packages:
        if not isinstance(item, dict):
            errors.append("package catalog entries must be objects")
            continue
        package_id = str(item.get("id", ""))
        relative = str(item.get("path", ""))
        declared_paths.add(relative)
        package_root = root / relative
        declared_package_roots[package_id] = package_root
        if not package_id.startswith("com.lingkyn."):
            errors.append(f"package id must start with com.lingkyn.: {package_id}")
        if Path(relative).name != package_id:
            errors.append(f"package path leaf must match package id: {relative} != {package_id}")
        if not relative.startswith("packages/unity/") or ".." in Path(relative).parts:
            errors.append(f"package path must stay under packages/unity: {relative}")
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

    live_package_paths = discover_lingkyn_package_paths(root)
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
        errors.extend(validate_internal_namespace_links(package_root, declared_package_roots))
        errors.extend(validate_unity_asset_path_literals(package_root, set(declared_package_roots)))
        for source in package_root.rglob("*.cs"):
            text = source.read_text(encoding="utf-8")
            namespaces = re.findall(r"\bnamespace\s+([A-Za-z0-9_.]+)", text)
            if not namespaces or any(not value.startswith("Lingkyn.") for value in namespaces):
                errors.append(f"{source.relative_to(root)}: namespace must start with Lingkyn.")
            if not source.with_name(source.name + ".meta").exists():
                errors.append(f"{source.relative_to(root)}: missing .meta")
        errors.extend(validate_asmdef_identity(package_root, root))
    return errors


def validate_fast_structure(root: Path) -> list[str]:
    errors: list[str] = scan_text_safety(root)
    errors.extend(validate_ignore_scope(root))
    errors.extend(validate_foundry_contract(root))
    for name in sorted(REQUIRED_ROOT_FILES):
        if not (root / name).exists():
            errors.append(f"missing root community/product file: {name}")
    catalog_path = root / "package-catalog.json"
    if not catalog_path.exists():
        errors.append("package catalog is missing")
        return errors
    catalog = load_json(catalog_path)
    errors.extend(validate_repository_layout(root, catalog))
    declared_paths = {
        str(item.get("path", ""))
        for item in catalog.get("packages", [])
        if isinstance(item, dict)
    }
    if discover_lingkyn_package_paths(root) != declared_paths:
        errors.append("fast structure: catalog/live package paths must agree")
    for item in catalog.get("packages", []):
        if not isinstance(item, dict):
            errors.append("fast structure: package catalog entries must be objects")
            continue
        package_id = str(item.get("id", ""))
        manifest_path = root / str(item.get("path", "")) / "package.json"
        if not manifest_path.exists():
            errors.append(f"fast structure: {package_id} package.json is missing")
            continue
        manifest = load_json(manifest_path)
        if manifest.get("name") != package_id or manifest.get("version") != item.get("version"):
            errors.append(f"fast structure: {package_id} catalog/manifest drift")
    return errors


def run_contract_test_gate(root: Path, repository_errors: list[str]) -> dict[str, Any]:
    if repository_errors:
        return {
            "status": "skipped",
            "reason": "repository_validation_failed",
        }
    result = subprocess.run(
        [
            sys.executable,
            "-m",
            "unittest",
            "discover",
            "-s",
            "tests",
            "-p",
            "test_*.py",
        ],
        cwd=root,
        text=True,
        capture_output=True,
        check=False,
    )
    if result.returncode != 0:
        repository_errors.append(
            "Contract test suite failed; no commit or push may proceed"
        )
    report: dict[str, Any] = {
        "status": "pass" if result.returncode == 0 else "fail",
        "returncode": result.returncode,
    }
    if result.returncode != 0:
        report["stdout_tail"] = result.stdout[-4000:]
        report["stderr_tail"] = result.stderr[-4000:]
    return report


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=ROOT)
    parser.add_argument("--json", action="store_true")
    parser.add_argument(
        "--fast-structure",
        action="store_true",
        help="run the non-promoting Foundry structure feedback gate",
    )
    parser.add_argument(
        "--run-contract-tests",
        action="store_true",
        help="run the complete contract suite only after repository validation passes",
    )
    parser.add_argument(
        "--device-lab-receipt",
        type=Path,
        help="also validate one completed generic Device Lab execution receipt",
    )
    args = parser.parse_args()
    if args.fast_structure and (args.run_contract_tests or args.device_lab_receipt is not None):
        parser.error("--fast-structure cannot be combined with full tests or Device Lab receipt validation")
    root = args.root.resolve()
    errors = validate_fast_structure(root) if args.fast_structure else validate_repository(root)
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
    contract_tests: dict[str, Any] | None = None
    if args.run_contract_tests:
        contract_tests = run_contract_test_gate(root, errors)
    report = {
        "schema": "xr-foundry.repository_validation.v1",
        "root": str(root),
        "mode": "fast_structure" if args.fast_structure else "repository_contract",
        "status": "pass" if not errors else "fail",
        "errors": errors,
    }
    if device_lab_receipt_path is not None:
        report["device_lab_receipt"] = str(device_lab_receipt_path)
    if contract_tests is not None:
        report["contract_tests"] = contract_tests
    print(json.dumps(report, indent=2))
    return 0 if not errors else 1


if __name__ == "__main__":
    raise SystemExit(main())
