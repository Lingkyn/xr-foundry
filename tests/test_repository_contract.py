from __future__ import annotations

import importlib.util
import hashlib
import json
import os
import struct
import subprocess
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path
from unittest import mock


ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "scripts/validate_repository.py"
SPEC = importlib.util.spec_from_file_location("validate_repository", SCRIPT)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


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


def current_compatibility_profiles() -> dict:
    return json.loads((ROOT / "compatibility-profiles.json").read_text(encoding="utf-8"))


def compatibility_evidence_test_root() -> Path:
    path = ROOT / "docs" / "validation" / "evidence"
    path.mkdir(parents=True, exist_ok=True)
    return path


def public_fixture_commit() -> str:
    catalog = MODULE.load_json(ROOT / "package-catalog.json")
    refs = subprocess.check_output(
        [
            "git",
            "for-each-ref",
            "--sort=-committerdate",
            "--format=%(refname)",
            "refs/remotes/origin/codex",
            "refs/remotes/origin/main",
        ],
        cwd=ROOT,
        text=True,
    ).splitlines()
    for ref in refs:
        commit_sha = subprocess.check_output(
            ["git", "rev-parse", ref], cwd=ROOT, text=True
        ).strip()
        if not MODULE.commit_is_public_origin_reachable(ROOT, commit_sha):
            continue
        if all(
            subprocess.run(
                ["git", "cat-file", "-e", f"{commit_sha}:{item['path']}/package.json"],
                cwd=ROOT,
                capture_output=True,
                check=False,
            ).returncode
            == 0
            for item in catalog["packages"]
        ):
            return commit_sha
    raise AssertionError("No public origin revision contains the current canonical package tree")


_DEVICE_EVIDENCE_DIRECTORIES: list[tempfile.TemporaryDirectory] = []


def device_evidence_test_root() -> Path:
    holder = tempfile.TemporaryDirectory(
        prefix="device-receipt-test-",
        dir=compatibility_evidence_test_root(),
    )
    _DEVICE_EVIDENCE_DIRECTORIES.append(holder)
    return Path(holder.name)


def binary_android_manifest(application_id: str) -> bytes:
    strings = [
        "manifest",
        "package",
        application_id,
        "http://schemas.android.com/apk/res/android",
        "versionCode",
    ]
    string_offsets: list[int] = []
    string_data = bytearray()
    for value in strings:
        encoded = value.encode("utf-8")
        if len(value) >= 128 or len(encoded) >= 128:
            raise ValueError("test fixture strings must use one-byte AXML lengths")
        string_offsets.append(len(string_data))
        string_data.extend((len(value), len(encoded)))
        string_data.extend(encoded)
        string_data.append(0)
    while len(string_data) % 4:
        string_data.append(0)
    strings_start = 28 + len(strings) * 4
    string_pool_size = strings_start + len(string_data)
    string_pool = (
        struct.pack("<HHI", 0x0001, 28, string_pool_size)
        + struct.pack("<IIIII", len(strings), 0, 0x00000100, strings_start, 0)
        + b"".join(struct.pack("<I", offset) for offset in string_offsets)
        + bytes(string_data)
    )
    start_element = (
        struct.pack("<HHI", 0x0102, 16, 76)
        + struct.pack("<II", 1, 0xFFFFFFFF)
        + struct.pack("<IIHHHHHH", 0xFFFFFFFF, 0, 20, 20, 2, 0, 0, 0)
        + struct.pack("<IIIHBBI", 0xFFFFFFFF, 1, 2, 8, 0, 0x03, 2)
        + struct.pack("<IIIHBBI", 3, 4, 0xFFFFFFFF, 8, 0, 0x10, 1)
    )
    end_element = (
        struct.pack("<HHI", 0x0103, 16, 24)
        + struct.pack("<II", 1, 0xFFFFFFFF)
        + struct.pack("<II", 0xFFFFFFFF, 0)
    )
    body = string_pool + start_element + end_element
    return struct.pack("<HHI", 0x0003, 8, 8 + len(body)) + body


def write_minimal_unity_apk(path: Path, application_id: str) -> None:
    with zipfile.ZipFile(path, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        archive.writestr("AndroidManifest.xml", binary_android_manifest(application_id))
        archive.writestr("classes.dex", b"dex\n035\x00" + b"\x00" * 112)
        archive.writestr("lib/arm64-v8a/libunity.so", b"\x7fELF" + b"unity")
        archive.writestr("lib/arm64-v8a/libil2cpp.so", b"\x7fELF" + b"il2cpp")
        archive.writestr("assets/bin/Data/globalgamemanagers", b"unity-player-data")


def attach_compatibility_receipt(
    payload: dict,
    directory: str | Path,
    *,
    profile_id: str = "unity-6000.3-inventory-ugui-non-xr-windows-editor",
) -> tuple[dict, dict, Path]:
    profile = next(item for item in payload["profiles"] if item["id"] == profile_id)
    commit_sha = public_fixture_commit()
    catalog = MODULE.load_json(ROOT / "package-catalog.json")
    catalog_paths = {
        item["id"]: item["path"] for item in catalog["packages"]
    }
    if profile_id in payload["current_profile_ids"]:
        profile = json.loads(json.dumps(profile))
        profile["id"] = f"{profile_id}-verified-fixture"
        for package_id in profile["package_versions"]:
            manifest_at_commit = json.loads(
                subprocess.check_output(
                    [
                        "git",
                        "show",
                        f"{commit_sha}:{catalog_paths[package_id]}/package.json",
                    ],
                    cwd=ROOT,
                    text=True,
                )
            )
            profile["package_versions"][package_id] = manifest_at_commit["version"]
        payload["profiles"].append(profile)
    profile["state"] = "verified"
    profile["verified_claims"] = ["editor_compile", "editmode_tests", "playmode_tests"]
    evidence_directory = Path(directory)
    receipt_path = evidence_directory / "compatibility-receipt.json"
    manifest_path = evidence_directory / "consumer-manifest.json"
    lock_path = evidence_directory / "consumer-packages-lock.json"
    selector = lambda package_id: (
        "https://github.com/Lingkyn/xr-foundry.git?path=/"
        f"{catalog_paths[package_id]}#{commit_sha}"
    )
    manifest_dependencies = dict(profile["target"]["requested_dependencies"])
    manifest_dependencies.update(
        {package_id: selector(package_id) for package_id in profile["package_versions"]}
    )
    lock_dependencies: dict[str, dict] = {}
    for package_id in profile["package_versions"]:
        package_manifest = json.loads(
            subprocess.check_output(
                ["git", "show", f"{commit_sha}:{catalog_paths[package_id]}/package.json"],
                cwd=ROOT,
                text=True,
            )
        )
        lock_dependencies[package_id] = {
            "version": selector(package_id),
            "depth": 0,
            "source": "git",
            "dependencies": package_manifest.get("dependencies", {}),
            "hash": commit_sha,
        }
    for dependency_id, version in profile["target"]["resolved_dependencies"].items():
        lock_dependencies[dependency_id] = {
            "version": version,
            "depth": 0 if dependency_id in manifest_dependencies else 1,
            "source": (
                "builtin"
                if dependency_id.startswith("com.unity.modules.")
                or dependency_id == "com.unity.ugui"
                else "registry"
            ),
            "dependencies": {},
        }
    known_unity_edges = {
        "com.unity.test-framework": {
            "com.unity.ext.nunit": "2.0.3",
            "com.unity.modules.imgui": "1.0.0",
            "com.unity.modules.jsonserialize": "1.0.0",
        },
        "com.unity.ugui": {
            "com.unity.modules.ui": "1.0.0",
            "com.unity.modules.imgui": "1.0.0",
        },
    }
    for dependency_id, edges in known_unity_edges.items():
        if dependency_id in lock_dependencies:
            lock_dependencies[dependency_id]["dependencies"] = {
                child_id: requested_version
                for child_id, requested_version in edges.items()
                if child_id in lock_dependencies
            }
    shortest_depths: dict[str, int] = {}
    pending_depths = [(dependency_id, 0) for dependency_id in manifest_dependencies]
    while pending_depths:
        dependency_id, depth = pending_depths.pop(0)
        if dependency_id in shortest_depths and shortest_depths[dependency_id] <= depth:
            continue
        shortest_depths[dependency_id] = depth
        entry = lock_dependencies.get(dependency_id, {})
        pending_depths.extend(
            (child_id, depth + 1)
            for child_id in entry.get("dependencies", {})
        )
    for dependency_id, depth in shortest_depths.items():
        if dependency_id in lock_dependencies:
            lock_dependencies[dependency_id]["depth"] = depth
    manifest_path.write_text(
        json.dumps(
            {
                "dependencies": manifest_dependencies,
                "testables": sorted(profile["package_versions"]),
            },
            indent=2,
            sort_keys=True,
        ),
        encoding="utf-8",
    )
    lock_path.write_text(
        json.dumps({"dependencies": lock_dependencies}, indent=2, sort_keys=True),
        encoding="utf-8",
    )
    manifest_digest = hashlib.sha256(manifest_path.read_bytes()).hexdigest()
    lock_digest = hashlib.sha256(lock_path.read_bytes()).hexdigest()
    check_payloads = []
    for check_id in ("editor_compile", "editmode_tests", "playmode_tests"):
        if check_id == "editor_compile":
            result_path = evidence_directory / "unity-compile-result.json"
            result_path.write_text(
                json.dumps(
                    {
                        "$schema": "docs/validation/unity-compile-result.schema.json",
                        "schema": "xr-foundry.unity_compile_result.v1",
                        "profile_id": profile["id"],
                        "commit_sha": commit_sha,
                        "unity_version": profile["target"]["editor"]["version"],
                        "build_target": profile["target"]["build_target"],
                        "graphics_api": profile["target"]["graphics_api"],
                        "scripting_backend": profile["target"]["scripting_backend"],
                        "architecture": profile["target"]["architecture"],
                        "manifest_sha256": manifest_digest,
                        "lock_sha256": lock_digest,
                        "batchmode": True,
                        "result": "pass",
                        "error_count": 0,
                        "warning_count": 0,
                        "started_at": "2026-07-15T10:00:00Z",
                        "completed_at": "2026-07-15T10:00:05Z",
                    },
                    indent=2,
                ),
                encoding="utf-8",
            )
            kind = "compile_result"
        else:
            result_path = evidence_directory / f"{check_id}-results.xml"
            mode = "EditMode" if check_id == "editmode_tests" else "PlayMode"
            expected_assemblies, derivation_errors = MODULE.derive_expected_test_assemblies(
                ROOT,
                commit_sha,
                catalog_paths,
                set(profile["package_versions"]),
                mode,
            )
            if derivation_errors or not expected_assemblies:
                raise AssertionError((derivation_errors, expected_assemblies))
            assembly_xml = "".join(
                (
                    f'<test-suite type="Assembly" name="{assembly}" result="Passed" '
                    'total="1" passed="1" failed="0" inconclusive="0" skipped="0">'
                    '<properties>'
                    f'<property name="platform" value="{mode}" />'
                    f'<property name="EditorOnly" value="'
                    f'{"True" if mode == "EditMode" else "False"}" />'
                    '</properties>'
                    f'<test-suite type="TestSuite" name="{assembly}.Tests" result="Passed" '
                    'total="1" passed="1" failed="0" inconclusive="0" skipped="0">'
                    f'<test-suite type="TestFixture" name="{assembly}.RequiredFixture" '
                    'result="Passed" total="1" passed="1" failed="0" '
                    'inconclusive="0" skipped="0">'
                    f'<test-case name="{assembly}.RequiredFixture.Passes" result="Passed" />'
                    '</test-suite></test-suite>'
                    '</test-suite>'
                )
                for assembly in sorted(expected_assemblies)
            )
            total = len(expected_assemblies)
            result_path.write_text(
                '<?xml version="1.0" encoding="utf-8"?>\n'
                f'<test-run testcasecount="{total}" result="Passed" total="{total}" '
                f'passed="{total}" failed="0" inconclusive="0" skipped="0" asserts="{total}">'
                f'<test-suite type="TestSuite" name="xr-foundry-consumer" result="Passed" '
                f'total="{total}" passed="{total}" failed="0" inconclusive="0" '
                f'skipped="0"><properties><property name="platform" value="{mode}" />'
                f'</properties>{assembly_xml}</test-suite></test-run>\n',
                encoding="utf-8",
            )
            kind = "nunit_result"
        result_digest = hashlib.sha256(result_path.read_bytes()).hexdigest()
        check_payloads.append(
            {
                "id": check_id,
                "status": "pass",
                "evidence_refs": [
                    {
                        "kind": kind,
                        "ref": result_path.relative_to(ROOT).as_posix(),
                        "sha256": result_digest,
                    }
                ],
            }
        )
    receipt = {
        "$schema": "docs/validation/compatibility-evidence.schema.json",
        "schema": "xr-foundry.compatibility_evidence.v1",
        "kind": "editor_automated",
        "profile_id": profile["id"],
        "commit_sha": commit_sha,
        "target": json.loads(json.dumps(profile["target"])),
        "package_versions": json.loads(json.dumps(profile["package_versions"])),
        "resolved_dependencies": json.loads(
            json.dumps(profile["target"]["resolved_dependencies"])
        ),
        "manifest": {
            "path": manifest_path.relative_to(ROOT).as_posix(),
            "sha256": manifest_digest,
        },
        "lock": {
            "path": lock_path.relative_to(ROOT).as_posix(),
            "sha256": lock_digest,
        },
        "checks": check_payloads,
    }
    receipt_path.write_text(json.dumps(receipt, indent=2), encoding="utf-8")
    profile["evidence"] = [
        {
            "kind": "editor_automated",
            "profile_id": profile["id"],
            "commit_sha": commit_sha,
            "receipt_path": receipt_path.relative_to(ROOT).as_posix(),
            "manifest_sha256": manifest_digest,
            "lock_sha256": lock_digest,
            "checks": ["editor_compile", "editmode_tests", "playmode_tests"],
        }
    ]
    return profile, receipt, receipt_path


def completed_device_lab_receipt() -> dict:
    payload = json.loads(
        (ROOT / "docs" / "device-lab" / "device-receipt.template.json").read_text(
            encoding="utf-8"
        )
    )
    payload["receipt_id"] = "inventory-world-ui-pico-pass"
    payload["compatibility_profile_id"] = (
        "unity-6000.3-inventory-xr-ugui-openxr-pico-4"
    )
    payload["task_url"] = "https://github.com/Lingkyn/xr-foundry/issues/1"
    commit_sha = public_fixture_commit()
    payload["revision"]["commit_sha"] = commit_sha
    catalog_at_commit = json.loads(
        subprocess.check_output(
            ["git", "show", f"{commit_sha}:package-catalog.json"],
            cwd=ROOT,
            text=True,
        )
    )
    catalog_paths = {
        item["id"]: item["path"] for item in catalog_at_commit["packages"]
    }
    custom_manifests: dict[str, dict] = {}
    for role in ("domain", "presentation", "renderer_adapter", "xr_adapter"):
        package_id = payload["package_tuple"][role]["id"]
        package_path = catalog_paths[package_id]
        manifest = json.loads(
            subprocess.check_output(
                ["git", "show", f"{commit_sha}:{package_path}/package.json"],
                cwd=ROOT,
                text=True,
            )
        )
        custom_manifests[package_id] = manifest
        payload["package_tuple"][role]["version"] = manifest["version"]

    selector = lambda package_id: (
        "https://github.com/Lingkyn/xr-foundry.git?path=/"
        f"{catalog_paths[package_id]}#{commit_sha}"
    )
    lock_dependencies: dict[str, dict] = {
        package_id: {
            "version": selector(package_id),
            "depth": 0,
            "source": "git",
            "dependencies": manifest.get("dependencies", {}),
            "hash": commit_sha,
        }
        for package_id, manifest in custom_manifests.items()
    }
    external_versions = {
        "com.unity.xr.interaction.toolkit": "3.5.1",
        "com.unity.xr.openxr": "1.16.0",
        "com.unity.xr.management": "4.5.3",
        "com.unity.inputsystem": "1.19.0",
    }
    for manifest in custom_manifests.values():
        for dependency_id, version in manifest.get("dependencies", {}).items():
            if dependency_id not in catalog_paths:
                external_versions[dependency_id] = version
    manifest_dependencies = {
        package_id: selector(package_id) for package_id in custom_manifests
    }
    for package_id in (
        "com.unity.inputsystem",
        "com.unity.xr.management",
        "com.unity.xr.openxr",
    ):
        manifest_dependencies[package_id] = external_versions[package_id]
    for package_id, version in external_versions.items():
        lock_dependencies[package_id] = {
            "version": version,
            "depth": 0 if package_id in manifest_dependencies else 1,
            "source": "builtin" if package_id == "com.unity.ugui" else "registry",
            "dependencies": {},
        }

    evidence_directory = device_evidence_test_root()
    artifact_path = evidence_directory / "inventory-world-ui.apk"
    write_minimal_unity_apk(artifact_path, "com.example.inventoryworldui")
    payload["artifact"] = {
        "kind": "android-apk",
        "file_name": artifact_path.name,
        "sha256": hashlib.sha256(artifact_path.read_bytes()).hexdigest(),
        "repository_path": artifact_path.relative_to(ROOT).as_posix(),
        "application_id": "com.example.inventoryworldui",
    }
    manifest_path = evidence_directory / "manifest.json"
    manifest_path.write_text(
        json.dumps({"dependencies": manifest_dependencies}, indent=2, sort_keys=True),
        encoding="utf-8",
    )
    lock_path = evidence_directory / "packages-lock.json"
    lock_path.write_text(
        json.dumps({"dependencies": lock_dependencies}, indent=2, sort_keys=True),
        encoding="utf-8",
    )
    lock_digest = hashlib.sha256(lock_path.read_bytes()).hexdigest()
    payload["dependency_resolution"] = {
        "manifest": {
            "format": "unity-manifest-v1",
            "kind": "repository_file",
            "ref": manifest_path.relative_to(ROOT).as_posix(),
            "sha256": hashlib.sha256(manifest_path.read_bytes()).hexdigest(),
        },
        "lock": {
            "format": "unity-packages-lock-v1",
            "kind": "repository_file",
            "ref": lock_path.relative_to(ROOT).as_posix(),
            "sha256": lock_digest,
        },
        "resolved_packages": [
            {"id": package_id, "version": version}
            for package_id, version in sorted(external_versions.items())
        ],
    }
    payload["software"].update(
        {
            "engine_version": "6000.3.19f1",
            "editor_version": "6000.3.19f1",
            "runtime_version": "1.0.31",
        }
    )
    payload["device"].update(
        {
            "model": "PICO 4",
            "os_version": "5.11.2",
        }
    )
    payload["build"] = {
        "target": "Android",
        "graphics_api": "OpenGLES3",
        "scripting_backend": "IL2CPP",
        "architecture": "ARM64",
    }
    for source in payload["input"]["sources"]:
        source["description"] = f"Recorded {source['id']} tracked controller"
    payload["input"]["device_description"] = "Recorded left and right tracked controllers"
    payload["execution_context"] = {"posture": "seated", "duration_seconds": 120}
    observation_path = evidence_directory / "headset-observation.json"
    observation_path.write_text(
        json.dumps(
            {
                "receipt_id": payload["receipt_id"],
                "commit_sha": commit_sha,
                "device": "PICO 4",
                "result": "recorded test fixture",
            },
            indent=2,
            sort_keys=True,
        ),
        encoding="utf-8",
    )
    observation_digest = hashlib.sha256(observation_path.read_bytes()).hexdigest()
    evidence_ref = observation_path.relative_to(ROOT).as_posix()
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
                            "ref": evidence_ref,
                            "sha256": observation_digest,
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


def activate_checkpoint_fixture(checkpoint: dict) -> None:
    """Make lifecycle-sensitive negative fixtures independent of live task state."""
    checkpoint["status"] = "in_progress"
    checkpoint["waiting"] = None
    checkpoint["claim"].update(
        {
            "status": "active",
            "adoptable": False,
            "ended_at": None,
            "transition_reason": None,
        }
    )
    checkpoint["exact_next_action"] = "Continue the bounded test fixture."
    checkpoint["completed_at"] = None


def attach_device_runtime_receipt(
    payload: dict,
    directory: str | Path,
) -> tuple[dict, dict, Path, Path]:
    device_receipt = completed_device_lab_receipt()
    profile_id = device_receipt["compatibility_profile_id"]
    base = next(
        item
        for item in payload["profiles"]
        if item["install_artifact"] == "com.lingkyn.inventory.xr.ugui"
    )
    profile = json.loads(json.dumps(base))
    profile["id"] = profile_id
    package_versions = {
        package["id"]: package["version"]
        for role in ("domain", "presentation", "renderer_adapter", "xr_adapter")
        if isinstance(package := device_receipt["package_tuple"].get(role), dict)
    }
    resolved_dependencies = {
        package["id"]: package["version"]
        for package in device_receipt["dependency_resolution"]["resolved_packages"]
    }
    manifest_path = ROOT / device_receipt["dependency_resolution"]["manifest"]["ref"]
    manifest_dependencies = json.loads(manifest_path.read_text(encoding="utf-8"))[
        "dependencies"
    ]
    profile["target"] = {
        "engine": {"id": "unity", "version": device_receipt["software"]["engine_version"]},
        "editor": {
            "id": "unity-editor",
            "version": device_receipt["software"]["editor_version"],
        },
        "renderer": {"id": "com.unity.ugui", "version": resolved_dependencies["com.unity.ugui"]},
        "requested_dependencies": {
            package_id: version
            for package_id, version in manifest_dependencies.items()
            if package_id not in package_versions
        },
        "resolved_dependencies": resolved_dependencies,
        "build_target": device_receipt["build"]["target"],
        "graphics_api": device_receipt["build"]["graphics_api"],
        "scripting_backend": device_receipt["build"]["scripting_backend"],
        "architecture": device_receipt["build"]["architecture"],
        "xr_provider": {
            "id": "com.unity.xr.openxr",
            "version": resolved_dependencies["com.unity.xr.openxr"],
        },
        "runtime": {
            "id": device_receipt["software"]["runtime_id"],
            "version": device_receipt["software"]["runtime_version"],
        },
        "input_routes": sorted(device_receipt["input"]["routes"]),
        "device": {
            "id": device_receipt["device"]["family_id"],
            "version": device_receipt["device"]["os_version"],
        },
    }
    profile["package_versions"] = package_versions
    profile["state"] = "verified"
    profile["verified_claims"] = ["device_runtime"]
    payload["profiles"].append(profile)

    receipts_root = ROOT / "docs" / "device-lab" / "receipts"
    with tempfile.NamedTemporaryFile(
        mode="w",
        encoding="utf-8",
        suffix=".json",
        prefix="device-runtime-fixture-",
        dir=receipts_root,
        delete=False,
    ) as handle:
        json.dump(device_receipt, handle, indent=2)
        device_path = Path(handle.name)
    device_digest = hashlib.sha256(device_path.read_bytes()).hexdigest()
    evidence_directory = Path(directory)
    receipt_path = evidence_directory / "device-compatibility-receipt.json"
    manifest = device_receipt["dependency_resolution"]["manifest"]
    lock = device_receipt["dependency_resolution"]["lock"]
    compatibility_receipt = {
        "$schema": "docs/validation/compatibility-evidence.schema.json",
        "schema": "xr-foundry.compatibility_evidence.v1",
        "kind": "device_runtime",
        "profile_id": profile_id,
        "commit_sha": device_receipt["revision"]["commit_sha"],
        "target": json.loads(json.dumps(profile["target"])),
        "package_versions": json.loads(json.dumps(package_versions)),
        "resolved_dependencies": json.loads(json.dumps(resolved_dependencies)),
        "manifest": {"path": manifest["ref"], "sha256": manifest["sha256"]},
        "lock": {"path": lock["ref"], "sha256": lock["sha256"]},
        "device_lab_receipt": {
            "path": device_path.relative_to(ROOT).as_posix(),
            "sha256": device_digest,
        },
        "checks": [
            {
                "id": "device_runtime",
                "status": "pass",
                "evidence_refs": [
                    {
                        "kind": "device_lab_receipt",
                        "ref": device_path.relative_to(ROOT).as_posix(),
                        "sha256": device_digest,
                    }
                ],
            }
        ],
    }
    receipt_path.write_text(json.dumps(compatibility_receipt, indent=2), encoding="utf-8")
    profile["evidence"] = [
        {
            "kind": "device_runtime",
            "profile_id": profile_id,
            "commit_sha": device_receipt["revision"]["commit_sha"],
            "receipt_path": receipt_path.relative_to(ROOT).as_posix(),
            "manifest_sha256": manifest["sha256"],
            "lock_sha256": lock["sha256"],
            "checks": ["device_runtime"],
        }
    ]
    return profile, compatibility_receipt, receipt_path, device_path


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

    def test_current_compatibility_profiles_are_adaptive_and_exact_verified(self) -> None:
        payload = current_compatibility_profiles()

        self.assertEqual(
            "version_adaptive",
            payload["capability"]["reference_and_generation"],
        )
        self.assertEqual(
            "exact_verified_profile_only",
            payload["capability"]["support_claim_source"],
        )
        self.assertEqual("exact_profile", payload["capability"]["engine_version_binding"])
        self.assertEqual(
            {
                "route": "raw_material_regeneration",
                "result_state": "candidate",
                "support_claim_allowed": False,
            },
            payload["evidence_policy"]["unmatched_target"],
        )
        non_xr = next(
            profile
            for profile in payload["profiles"]
            if profile["id"] == "unity-6000.3-inventory-ugui-non-xr-windows-editor"
        )
        not_applicable = {"id": "not_applicable", "version": "not_applicable"}
        self.assertEqual(not_applicable, non_xr["target"]["xr_provider"])
        self.assertEqual(not_applicable, non_xr["target"]["runtime"])
        self.assertNotIn("com.lingkyn.inventory.xr.ugui", non_xr["package_versions"])
        self.assertEqual(
            {item["id"] for item in MODULE.load_json(ROOT / "package-catalog.json")["packages"]},
            {profile["install_artifact"] for profile in payload["profiles"]},
        )
        self.assertEqual(9, len(payload["profiles"]))
        for profile in payload["profiles"]:
            self.assertEqual("verified", profile["state"])
            self.assertEqual("6000.3.19f1", profile["target"]["engine"]["version"])
            self.assertEqual("6000.3.19f1", profile["target"]["editor"]["version"])
            self.assertEqual(1, len(profile["evidence"]))
            evidence = profile["evidence"][0]
            self.assertEqual(profile["id"], evidence["profile_id"])
            self.assertRegex(evidence["commit_sha"], r"^[0-9a-f]{40}$")
            self.assertNotEqual("0" * 40, evidence["commit_sha"])
            self.assertEqual(set(profile["verified_claims"]), set(evidence["checks"]))
            self.assertTrue(profile["verified_claims"])
        self.assertEqual(
            [],
            MODULE.validate_compatibility_profile_payload(
                payload,
                ROOT,
                MODULE.load_json(ROOT / "package-catalog.json"),
            ),
        )

    def test_unity_external_lock_edge_accepts_resolver_upgrade_and_rejects_incompatible(self) -> None:
        self.assertTrue(MODULE.unity_external_dependency_is_satisfied("1.2.0", "1.3.4"))
        self.assertTrue(MODULE.unity_external_dependency_is_satisfied("1.2.0-pre.1", "1.2.0"))
        self.assertFalse(MODULE.unity_external_dependency_is_satisfied("1.2.0", "1.2.0-pre.1"))
        self.assertFalse(MODULE.unity_external_dependency_is_satisfied("2.0.0", "1.9.9"))
        self.assertFalse(MODULE.unity_external_dependency_is_satisfied("not-semver", "1.0.0"))

    def test_contribution_credit_never_grants_authority_or_accepts_empty_evidence(self) -> None:
        schema = ROOT / "docs" / "contributing" / "contribution-credit.schema.json"
        example = MODULE.load_json(
            ROOT / "docs" / "contributing" / "contribution-credit.example.json"
        )
        self.assertEqual(
            [],
            MODULE.validate_json_schema_instance(example, schema, "credit example"),
        )

        escalated = json.loads(json.dumps(example))
        escalated["authority"]["grants_merge_authority"] = True
        self.assertTrue(
            MODULE.validate_json_schema_instance(escalated, schema, "credit escalation")
        )

        unsupported = json.loads(json.dumps(example))
        unsupported["record_status"] = "accepted"
        unsupported["credited_at"] = "2026-07-16T12:00:00Z"
        unsupported["recorded_by"] = "@maintainer"
        self.assertTrue(
            MODULE.validate_json_schema_instance(unsupported, schema, "credit without evidence")
        )

    def test_pending_compatibility_profile_rejects_verified_overclaim(self) -> None:
        payload = current_compatibility_profiles()
        profile = payload["profiles"][0]
        profile["state"] = "pending_automated_validation"
        profile["verified_claims"] = ["editor_compile"]
        profile["evidence"] = []

        errors = MODULE.validate_compatibility_profile_payload(
            payload,
            ROOT,
            MODULE.load_json(ROOT / "package-catalog.json"),
        )

        self.assertTrue(any("unverified profile must not publish" in error for error in errors))

    def test_verified_compatibility_profile_accepts_only_exact_bound_evidence(self) -> None:
        payload = current_compatibility_profiles()
        self.assertEqual(
            [],
            MODULE.validate_compatibility_profile_payload(
                payload,
                ROOT,
                MODULE.load_json(ROOT / "package-catalog.json"),
            ),
        )

    def test_device_runtime_compatibility_requires_exact_device_lab_pass(self) -> None:
        payload = current_compatibility_profiles()
        with tempfile.TemporaryDirectory(dir=compatibility_evidence_test_root()) as directory:
            _, _, _, device_path = attach_device_runtime_receipt(payload, directory)
            try:
                self.assertEqual(
                    [],
                    MODULE.validate_compatibility_profile_payload(
                        payload,
                        ROOT,
                        MODULE.load_json(ROOT / "package-catalog.json"),
                    ),
                )
            finally:
                device_path.unlink(missing_ok=True)

    def test_device_runtime_rejects_host_profile_or_cross_tuple_receipt(self) -> None:
        payload = current_compatibility_profiles()
        with tempfile.TemporaryDirectory(dir=compatibility_evidence_test_root()) as directory:
            profile, receipt, receipt_path, device_path = attach_device_runtime_receipt(
                payload, directory
            )
            try:
                host_target = next(
                    item["target"]
                    for item in payload["profiles"]
                    if item["id"] == "unity-6000.3-inventory-xr-ugui-openxr-windows-editor"
                )
                profile["target"] = json.loads(json.dumps(host_target))
                receipt["target"] = json.loads(json.dumps(host_target))
                receipt_path.write_text(json.dumps(receipt), encoding="utf-8")
                errors = MODULE.validate_compatibility_profile_payload(
                    payload, ROOT, MODULE.load_json(ROOT / "package-catalog.json")
                )
            finally:
                device_path.unlink(missing_ok=True)
        self.assertTrue(any("Device Lab runtime tuple must match" in error for error in errors))
        self.assertTrue(any("Device Lab device tuple must match" in error for error in errors))

    def test_device_runtime_rejects_device_receipt_that_does_not_pass_full_validator(self) -> None:
        payload = current_compatibility_profiles()
        with tempfile.TemporaryDirectory(dir=compatibility_evidence_test_root()) as directory:
            profile, receipt, receipt_path, device_path = attach_device_runtime_receipt(
                payload, directory
            )
            try:
                device_receipt = json.loads(device_path.read_text(encoding="utf-8"))
                required = next(
                    check
                    for check in device_receipt["checks"]
                    if check["id"] == "artifact-install"
                )
                required.update(
                    {"status": "not_tested", "observation": "", "evidence_refs": []}
                )
                device_path.write_text(json.dumps(device_receipt), encoding="utf-8")
                digest = hashlib.sha256(device_path.read_bytes()).hexdigest()
                receipt["device_lab_receipt"]["sha256"] = digest
                receipt["checks"][0]["evidence_refs"][0]["sha256"] = digest
                receipt_path.write_text(json.dumps(receipt), encoding="utf-8")
                profile["evidence"][0]["checks"] = ["device_runtime"]
                errors = MODULE.validate_compatibility_profile_payload(
                    payload, ROOT, MODULE.load_json(ROOT / "package-catalog.json")
                )
            finally:
                device_path.unlink(missing_ok=True)
        self.assertTrue(any("required check cannot remain not_tested" in error for error in errors))

    def test_device_runtime_rejects_receipt_outside_canonical_device_lab_route(self) -> None:
        payload = current_compatibility_profiles()
        with tempfile.TemporaryDirectory(dir=compatibility_evidence_test_root()) as directory:
            profile, receipt, receipt_path, device_path = attach_device_runtime_receipt(
                payload, directory
            )
            copied_path = Path(directory) / "copied-device-receipt.json"
            copied_path.write_bytes(device_path.read_bytes())
            try:
                digest = hashlib.sha256(copied_path.read_bytes()).hexdigest()
                receipt["device_lab_receipt"] = {
                    "path": copied_path.relative_to(ROOT).as_posix(),
                    "sha256": digest,
                }
                receipt["checks"][0]["evidence_refs"][0].update(
                    {"ref": copied_path.relative_to(ROOT).as_posix(), "sha256": digest}
                )
                receipt_path.write_text(json.dumps(receipt), encoding="utf-8")
                errors = MODULE.validate_compatibility_profile_payload(
                    payload, ROOT, MODULE.load_json(ROOT / "package-catalog.json")
                )
            finally:
                device_path.unlink(missing_ok=True)
        self.assertTrue(any("docs/device-lab/receipts/*.json" in error for error in errors))

    def test_historical_verified_profile_keeps_its_exact_manifest_versions(self) -> None:
        payload = current_compatibility_profiles()
        current = next(
            item
            for item in payload["profiles"]
            if item["id"] == "unity-6000.3-inventory-ugui-non-xr-windows-editor"
        )
        historical = json.loads(json.dumps(current))
        historical["id"] = "unity-6000.3-inventory-ugui-0.1.0-historical"
        fixture_commit = public_fixture_commit()
        catalog_paths = {
            item["id"]: item["path"]
            for item in MODULE.load_json(ROOT / "package-catalog.json")["packages"]
        }
        for package_id in historical["package_versions"]:
            manifest = json.loads(
                subprocess.check_output(
                    [
                        "git",
                        "show",
                        f"{fixture_commit}:{catalog_paths[package_id]}/package.json",
                    ],
                    cwd=ROOT,
                    text=True,
                )
            )
            historical["package_versions"][package_id] = manifest["version"]
        payload["profiles"].append(historical)
        with tempfile.TemporaryDirectory(dir=compatibility_evidence_test_root()) as directory:
            attach_compatibility_receipt(payload, directory, profile_id=historical["id"])
            self.assertEqual(
                [],
                MODULE.validate_compatibility_profile_payload(
                    payload,
                    ROOT,
                    MODULE.load_json(ROOT / "package-catalog.json"),
                ),
            )

    def test_compatibility_receipt_rejects_pseudo_revision_and_hashes(self) -> None:
        payload = current_compatibility_profiles()
        with tempfile.TemporaryDirectory(dir=compatibility_evidence_test_root()) as directory:
            profile, receipt, receipt_path = attach_compatibility_receipt(payload, directory)
            profile["evidence"][0]["commit_sha"] = "0" * 40
            profile["evidence"][0]["lock_sha256"] = "0" * 64
            receipt["commit_sha"] = "0" * 40
            receipt["lock"]["sha256"] = "0" * 64
            receipt_path.write_text(json.dumps(receipt), encoding="utf-8")
            errors = MODULE.validate_compatibility_profile_payload(
                payload, ROOT, MODULE.load_json(ROOT / "package-catalog.json")
            )
        self.assertTrue(any("non-zero full commit SHA" in error for error in errors))
        self.assertTrue(any("non-zero lock_sha256" in error for error in errors))

    def test_compatibility_receipt_rejects_nonexistent_commit(self) -> None:
        payload = current_compatibility_profiles()
        with tempfile.TemporaryDirectory(dir=compatibility_evidence_test_root()) as directory:
            profile, receipt, receipt_path = attach_compatibility_receipt(payload, directory)
            profile["evidence"][0]["commit_sha"] = "f" * 40
            receipt["commit_sha"] = "f" * 40
            receipt_path.write_text(json.dumps(receipt), encoding="utf-8")
            errors = MODULE.validate_compatibility_profile_payload(
                payload, ROOT, MODULE.load_json(ROOT / "package-catalog.json")
            )
        self.assertTrue(any("must resolve and be reachable" in error for error in errors))

    def test_compatibility_receipt_rejects_local_only_commit_with_public_tree(self) -> None:
        public_commit = public_fixture_commit()
        tree = subprocess.check_output(
            ["git", "rev-parse", f"{public_commit}^{{tree}}"], cwd=ROOT, text=True
        ).strip()
        local_only = subprocess.check_output(
            [
                "git",
                "-c",
                "user.name=XR Foundry Contract Test",
                "-c",
                "user.email=xr-foundry-contract-test@example.invalid",
                "commit-tree",
                tree,
            ],
            cwd=ROOT,
            input="local-only evidence object\n",
            text=True,
        ).strip()
        self.assertFalse(MODULE.commit_is_public_origin_reachable(ROOT, local_only))
        payload = current_compatibility_profiles()
        with tempfile.TemporaryDirectory(dir=compatibility_evidence_test_root()) as directory:
            profile, receipt, receipt_path = attach_compatibility_receipt(payload, directory)
            profile["evidence"][0]["commit_sha"] = local_only
            receipt["commit_sha"] = local_only
            receipt_path.write_text(json.dumps(receipt), encoding="utf-8")
            errors = MODULE.validate_compatibility_profile_payload(
                payload, ROOT, MODULE.load_json(ROOT / "package-catalog.json")
            )
        self.assertTrue(any("fetched public origin ref" in error for error in errors))

    def test_compatibility_receipt_rejects_missing_files_and_wrong_real_hash(self) -> None:
        payload = current_compatibility_profiles()
        with tempfile.TemporaryDirectory(dir=compatibility_evidence_test_root()) as directory:
            _, receipt, receipt_path = attach_compatibility_receipt(payload, directory)
            receipt["manifest"]["path"] = (
                "docs/validation/evidence/nonexistent-consumer-manifest.json"
            )
            receipt["checks"][0]["evidence_refs"][0]["sha256"] = "e" * 64
            receipt_path.write_text(json.dumps(receipt), encoding="utf-8")
            errors = MODULE.validate_compatibility_profile_payload(
                payload, ROOT, MODULE.load_json(ROOT / "package-catalog.json")
            )
        self.assertTrue(any("repository evidence file does not exist" in error for error in errors))
        self.assertTrue(any("SHA-256 does not match repository file" in error for error in errors))

    def test_compatibility_receipt_rejects_simplified_or_drifted_unity_lock(self) -> None:
        mutations = {
            "simplified": lambda lock, root: lock["dependencies"].__setitem__(root, "0.1.0"),
            "wrong_source": lambda lock, root: lock["dependencies"][root].__setitem__(
                "source", "registry"
            ),
            "wrong_hash": lambda lock, root: lock["dependencies"][root].__setitem__(
                "hash", "f" * 40
            ),
            "wrong_depth": lambda lock, root: lock["dependencies"][root].__setitem__(
                "depth", 7
            ),
            "wrong_path": lambda lock, root: lock["dependencies"][root].__setitem__(
                "version", lock["dependencies"][root]["version"].replace(
                    "com.lingkyn.inventory.ugui", "com.lingkyn.inventory.core"
                )
            ),
            "wrong_transitive": lambda lock, root: lock["dependencies"][root][
                "dependencies"
            ].__setitem__("com.unity.fake-transitive", "9.9.9"),
        }
        for label, mutate in mutations.items():
            with self.subTest(label=label):
                payload = current_compatibility_profiles()
                with tempfile.TemporaryDirectory(
                    dir=compatibility_evidence_test_root()
                ) as directory:
                    profile, receipt, receipt_path = attach_compatibility_receipt(
                        payload, directory
                    )
                    lock_path = ROOT / receipt["lock"]["path"]
                    lock = json.loads(lock_path.read_text(encoding="utf-8"))
                    mutate(lock, profile["install_artifact"])
                    lock_path.write_text(json.dumps(lock), encoding="utf-8")
                    digest = hashlib.sha256(lock_path.read_bytes()).hexdigest()
                    receipt["lock"]["sha256"] = digest
                    profile["evidence"][0]["lock_sha256"] = digest
                    receipt_path.write_text(json.dumps(receipt), encoding="utf-8")
                    errors = MODULE.validate_compatibility_profile_payload(
                        payload, ROOT, MODULE.load_json(ROOT / "package-catalog.json")
                    )
                self.assertTrue(
                    any(
                        marker in error
                        for error in errors
                        for marker in (
                            "lock dependency entry must be an object",
                            "custom lock package source must be git",
                            "custom lock package hash must equal",
                            "custom lock selector must bind canonical path",
                            "custom lock dependency edges drift",
                            "direct manifest dependency must have depth=0",
                            "depth must equal the shortest manifest path",
                        )
                    )
                )

    def test_compatibility_receipt_rejects_exact_tuple_mismatches(self) -> None:
        mutations = {
            "build_target": lambda receipt: receipt["target"].__setitem__(
                "build_target", "Android"
            ),
            "provider": lambda receipt: receipt["target"]["xr_provider"].update(
                {"id": "com.unity.xr.openxr", "version": "1.16.0"}
            ),
            "input": lambda receipt: receipt["target"].__setitem__(
                "input_routes", ["xri-ray"]
            ),
            "renderer": lambda receipt: receipt["target"]["renderer"].update(
                {"id": "com.unity.modules.uielements", "version": "1.0.0"}
            ),
            "scripting_backend": lambda receipt: receipt["target"].__setitem__(
                "scripting_backend", "IL2CPP"
            ),
            "architecture": lambda receipt: receipt["target"].__setitem__(
                "architecture", "ARM64"
            ),
        }
        for label, mutate in mutations.items():
            with self.subTest(label=label):
                payload = current_compatibility_profiles()
                with tempfile.TemporaryDirectory(
                    dir=compatibility_evidence_test_root()
                ) as directory:
                    _, receipt, receipt_path = attach_compatibility_receipt(payload, directory)
                    mutate(receipt)
                    receipt_path.write_text(json.dumps(receipt), encoding="utf-8")
                    errors = MODULE.validate_compatibility_profile_payload(
                        payload, ROOT, MODULE.load_json(ROOT / "package-catalog.json")
                    )
                self.assertTrue(any("receipt target must exactly match" in error for error in errors))

    def test_compatibility_receipt_rejects_package_dependency_and_lock_mismatch(self) -> None:
        payload = current_compatibility_profiles()
        with tempfile.TemporaryDirectory(dir=compatibility_evidence_test_root()) as directory:
            profile, receipt, receipt_path = attach_compatibility_receipt(payload, directory)
            receipt["package_versions"]["com.lingkyn.inventory.core"] = "9.9.9"
            receipt["resolved_dependencies"]["com.unity.ugui"] = "9.9.9"
            receipt["lock"]["sha256"] = "e" * 64
            receipt_path.write_text(json.dumps(receipt), encoding="utf-8")
            errors = MODULE.validate_compatibility_profile_payload(
                payload, ROOT, MODULE.load_json(ROOT / "package-catalog.json")
            )
        self.assertTrue(any("receipt package_versions must exactly match" in error for error in errors))
        self.assertTrue(any("receipt resolved_dependencies must exactly match" in error for error in errors))
        self.assertTrue(any("receipt lock hash must match" in error for error in errors))

    def test_compatibility_receipt_rejects_check_mismatch(self) -> None:
        payload = current_compatibility_profiles()
        with tempfile.TemporaryDirectory(dir=compatibility_evidence_test_root()) as directory:
            profile, receipt, receipt_path = attach_compatibility_receipt(payload, directory)
            receipt["checks"][0]["status"] = "fail"
            receipt_path.write_text(json.dumps(receipt), encoding="utf-8")
            errors = MODULE.validate_compatibility_profile_payload(
                payload, ROOT, MODULE.load_json(ROOT / "package-catalog.json")
            )
        self.assertTrue(any("checks must exactly equal passed receipt checks" in error for error in errors))
        self.assertTrue(any("lacks a passed receipt check" in error for error in errors))

    def test_automated_compatibility_rejects_text_compile_and_fake_nunit(self) -> None:
        payload = current_compatibility_profiles()
        with tempfile.TemporaryDirectory(dir=compatibility_evidence_test_root()) as directory:
            _, receipt, receipt_path = attach_compatibility_receipt(payload, directory)
            compile_ref = receipt["checks"][0]["evidence_refs"][0]
            compile_path = ROOT / compile_ref["ref"]
            compile_path.write_text("compile passed", encoding="utf-8")
            compile_ref["sha256"] = hashlib.sha256(compile_path.read_bytes()).hexdigest()
            nunit_ref = receipt["checks"][1]["evidence_refs"][0]
            nunit_path = ROOT / nunit_ref["ref"]
            nunit_path.write_text("all tests passed", encoding="utf-8")
            nunit_ref["sha256"] = hashlib.sha256(nunit_path.read_bytes()).hexdigest()
            receipt_path.write_text(json.dumps(receipt), encoding="utf-8")
            errors = MODULE.validate_compatibility_profile_payload(
                payload, ROOT, MODULE.load_json(ROOT / "package-catalog.json")
            )
        self.assertTrue(any("compile result must be structured JSON" in error for error in errors))
        self.assertTrue(any("parseable NUnit XML" in error for error in errors))

    def test_nunit_result_rejects_wrong_mode_missing_and_unrelated_assemblies(self) -> None:
        cases = {
            "wrong-mode": "platform property must equal EditMode",
            "missing-required": "omits commit-required EditMode assemblies",
            "unrelated-assembly": "manifest-testables-derived EditMode assemblies",
        }
        for mutation, expected_error in cases.items():
            with self.subTest(mutation=mutation):
                payload = current_compatibility_profiles()
                with tempfile.TemporaryDirectory(
                    dir=compatibility_evidence_test_root()
                ) as directory:
                    _, receipt, receipt_path = attach_compatibility_receipt(
                        payload, directory
                    )
                    nunit_ref = receipt["checks"][1]["evidence_refs"][0]
                    nunit_path = ROOT / nunit_ref["ref"]
                    tree = ET.parse(nunit_path)
                    test_run = tree.getroot()
                    project_suite = next(
                        item
                        for item in test_run
                        if item.tag == "test-suite"
                        and item.attrib.get("type") == "TestSuite"
                    )
                    assembly_suites = [
                        item
                        for item in project_suite
                        if item.tag == "test-suite"
                        and item.attrib.get("type") == "Assembly"
                    ]
                    if mutation == "wrong-mode":
                        platform = next(
                            item
                            for properties in project_suite
                            if properties.tag == "properties"
                            for item in properties
                            if item.attrib.get("name") == "platform"
                        )
                        platform.attrib["value"] = "PlayMode"
                    elif mutation == "missing-required":
                        project_suite.remove(assembly_suites[0])
                    else:
                        assembly_suites[0].attrib["name"] = "Unrelated.Tests.dll"
                    tree.write(nunit_path, encoding="utf-8", xml_declaration=True)
                    nunit_ref["sha256"] = hashlib.sha256(
                        nunit_path.read_bytes()
                    ).hexdigest()
                    receipt_path.write_text(json.dumps(receipt), encoding="utf-8")
                    errors = MODULE.validate_compatibility_profile_payload(
                        payload, ROOT, MODULE.load_json(ROOT / "package-catalog.json")
                    )
                self.assertTrue(
                    any(expected_error in error for error in errors), errors
                )

    def test_structured_compile_result_must_cross_bind_and_pass(self) -> None:
        payload = current_compatibility_profiles()
        with tempfile.TemporaryDirectory(dir=compatibility_evidence_test_root()) as directory:
            _, receipt, receipt_path = attach_compatibility_receipt(payload, directory)
            compile_ref = receipt["checks"][0]["evidence_refs"][0]
            compile_path = ROOT / compile_ref["ref"]
            compile_result = json.loads(compile_path.read_text(encoding="utf-8"))
            compile_result["profile_id"] = "another-profile"
            compile_result["result"] = "fail"
            compile_result["error_count"] = 1
            compile_path.write_text(json.dumps(compile_result), encoding="utf-8")
            compile_ref["sha256"] = hashlib.sha256(compile_path.read_bytes()).hexdigest()
            receipt_path.write_text(json.dumps(receipt), encoding="utf-8")
            errors = MODULE.validate_compatibility_profile_payload(
                payload, ROOT, MODULE.load_json(ROOT / "package-catalog.json")
            )
        self.assertTrue(any("profile_id must match the evidence tuple" in error for error in errors))
        self.assertTrue(any("pass with error_count=0" in error for error in errors))

    def test_compatibility_profiles_reject_vague_target_and_unsupported_match(self) -> None:
        payload = current_compatibility_profiles()
        payload["profiles"][0]["target"]["engine"]["version"] = "latest"
        payload["evidence_policy"]["unmatched_target"]["support_claim_allowed"] = True

        errors = MODULE.validate_compatibility_profile_payload(
            payload,
            ROOT,
            MODULE.load_json(ROOT / "package-catalog.json"),
        )

        self.assertTrue(any("concrete profile value" in error for error in errors))
        self.assertTrue(any("unsupported candidates" in error for error in errors))

    def test_compatibility_profile_rejects_package_version_drift(self) -> None:
        payload = current_compatibility_profiles()
        profile = next(
            item
            for item in payload["profiles"]
            if item["install_artifact"] == "com.lingkyn.inventory.core"
        )
        profile["package_versions"]["com.lingkyn.inventory.core"] = "9.9.9"

        errors = MODULE.validate_compatibility_profile_payload(
            payload,
            ROOT,
            MODULE.load_json(ROOT / "package-catalog.json"),
        )

        self.assertTrue(any("match catalog and installable manifest" in error for error in errors))

    def test_installable_manifest_rejects_dependency_range(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            package_root = root / "packages" / "unity" / "com.lingkyn.example"
            package_root.mkdir(parents=True)
            (package_root / "package.json").write_text(
                json.dumps(
                    {
                        "name": "com.lingkyn.example",
                        "version": "0.1.0",
                        "unity": "6000.3",
                        "dependencies": {"com.unity.example": ">=1.0.0"},
                    }
                ),
                encoding="utf-8",
            )
            catalog = {
                "packages": [
                    {
                        "id": "com.lingkyn.example",
                        "path": "packages/unity/com.lingkyn.example",
                        "version": "0.1.0",
                    }
                ]
            }

            errors = MODULE.validate_concrete_package_manifests(root, catalog)

            self.assertTrue(any("dependency com.unity.example" in error for error in errors))

    def test_asmdef_filename_stem_must_equal_declared_name(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            package_root = Path(directory)
            asmdef = package_root / "LegacyName.asmdef"
            asmdef.write_text(
                json.dumps({"name": "Lingkyn.Inventory.Runtime"}), encoding="utf-8"
            )
            asmdef.with_name(asmdef.name + ".meta").write_text(
                "fileFormatVersion: 2\n", encoding="utf-8"
            )

            errors = MODULE.validate_asmdef_identity(package_root)

        self.assertTrue(any("filename stem must equal declared name" in error for error in errors))

    def test_installable_reference_requires_raw_material_use_mode(self) -> None:
        catalog = MODULE.load_json(ROOT / "reference-catalog.json")
        package_artifact = next(
            item for item in catalog["artifacts"] if item.get("package_id")
        )
        package_artifact["use_modes"].remove("raw_material")

        errors = MODULE.validate_reference_package_use_modes(catalog)

        self.assertTrue(any("must include raw_material" in error for error in errors))

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
                                "version": "0.1.0",
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
            suffixes = [
                ".py",
                ".xml",
                ".uxml",
                ".uss",
                ".prefab",
                ".asset",
                ".meta",
                ".custom",
            ]
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

        self.assertTrue(any("write_permission_not_inferred" in error for error in errors))

    def test_task_hall_positive_fixtures_pass(self) -> None:
        self.assertEqual([], MODULE.validate_task_hall_contract(ROOT))
        example = json.loads(
            (ROOT / "docs" / "contributing" / "task-contract.example.json").read_text(
                encoding="utf-8"
            )
        )
        continuation = json.loads(
            (ROOT / "docs" / "contributing" / "work-continuation.example.json").read_text(
                encoding="utf-8"
            )
        )
        registry = json.loads(
            (ROOT / "docs" / "contributing" / "tasks" / "task-registry.json").read_text(
                encoding="utf-8"
            )
        )
        live_task = json.loads(
            (
                ROOT
                / "docs"
                / "contributing"
                / "tasks"
                / "agent-commons-public-workbench.task.json"
            ).read_text(encoding="utf-8")
        )
        authority = json.loads(
            (ROOT / "docs" / "contributing" / "task-hall.v1.json").read_text(encoding="utf-8")
        )

        self.assertEqual("0.3.0", authority["version"])
        self.assertEqual([], MODULE.validate_task_hall_authority(authority))
        self.assertEqual([], MODULE.validate_task_contract(example, "Task Hall example"))
        self.assertEqual(
            [], MODULE.validate_work_continuation(continuation, "Task Hall continuation")
        )
        self.assertEqual([], MODULE.validate_task_registry(ROOT, registry))
        self.assertEqual(
            [],
            MODULE.validate_task_contract(
                live_task,
                "registered live task",
                require_canonical_repository=True,
            ),
        )

    def test_task_contract_rejects_competing_umbrella_lifecycle(self) -> None:
        payload = json.loads(
            (ROOT / "docs" / "contributing" / "task-contract.example.json").read_text(
                encoding="utf-8"
            )
        )
        payload["state"] = "in_progress"

        errors = MODULE.validate_task_contract(payload, "stale lifecycle task")

        self.assertTrue(any("canonical Task Hall umbrella lifecycle" in error for error in errors))

    def test_task_contract_rejects_missing_and_duplicate_issue_projections(self) -> None:
        payload = json.loads(
            (ROOT / "docs" / "contributing" / "task-contract.example.json").read_text(
                encoding="utf-8"
            )
        )
        missing = json.loads(json.dumps(payload))
        missing["public_projection"]["checkpoint_issues"] = missing["public_projection"][
            "checkpoint_issues"
        ][:-1]
        missing_errors = MODULE.validate_task_contract(missing, "missing projection")
        self.assertTrue(any("missing checkpoint Issue projections" in error for error in missing_errors))

        duplicate = json.loads(json.dumps(payload))
        duplicate["public_projection"]["checkpoint_issues"].append(
            {
                "checkpoint_id": "CP-01",
                "issue": "https://github.com/example-org/example-repo/issues/199",
            }
        )
        duplicate_id_errors = MODULE.validate_task_contract(duplicate, "duplicate checkpoint projection")
        self.assertTrue(
            any("duplicate checkpoint Issue projections" in error for error in duplicate_id_errors)
        )

        duplicate_issue = json.loads(json.dumps(payload))
        duplicate_issue["public_projection"]["checkpoint_issues"][1]["issue"] = duplicate_issue[
            "public_projection"
        ]["checkpoint_issues"][0]["issue"]
        duplicate_issue_errors = MODULE.validate_task_contract(
            duplicate_issue, "duplicate issue projection"
        )
        self.assertTrue(any("duplicate Issue projections" in error for error in duplicate_issue_errors))

        umbrella_collision = json.loads(json.dumps(payload))
        umbrella_collision["public_projection"]["checkpoint_issues"][0]["issue"] = (
            umbrella_collision["public_projection"]["umbrella_issue"]
        )
        umbrella_errors = MODULE.validate_task_contract(
            umbrella_collision, "checkpoint equals umbrella"
        )
        self.assertTrue(
            any("checkpoint Issue cannot equal umbrella Issue" in error for error in umbrella_errors)
        )
        self.assertTrue(
            any("unique role-separated set" in error for error in umbrella_errors)
        )

    def test_task_contract_rejects_foreign_repository_projections(self) -> None:
        payload = json.loads(
            (ROOT / "docs" / "contributing" / "task-contract.example.json").read_text(
                encoding="utf-8"
            )
        )
        payload["public_projection"]["checkpoint_issues"][0]["issue"] = (
            "https://github.com/other-org/other-repo/issues/101"
        )

        errors = MODULE.validate_task_contract(payload, "foreign projection")

        self.assertTrue(any("foreign repository" in error for error in errors))

    def test_registered_task_requires_canonical_task_hall_project(self) -> None:
        payload = json.loads(
            (
                ROOT
                / "docs"
                / "contributing"
                / "tasks"
                / "agent-commons-public-workbench.task.json"
            ).read_text(encoding="utf-8")
        )
        payload["public_projection"]["project"] = "https://github.com/orgs/other-org/projects/9"

        errors = MODULE.validate_task_contract(
            payload,
            "non-canonical project",
            require_canonical_repository=True,
        )

        self.assertTrue(
            any("canonical Task Hall Project" in error for error in errors)
        )

    def test_task_contract_rejects_self_authorizing_and_unqualified_high_risk_routing(
        self,
    ) -> None:
        payload = json.loads(
            (ROOT / "docs" / "contributing" / "task-contract.example.json").read_text(
                encoding="utf-8"
            )
        )
        self_auth = json.loads(json.dumps(payload))
        self_auth["checkpoints"][0]["routing"]["self_report_grants_authority"] = True
        self_auth_errors = MODULE.validate_task_contract(self_auth, "self-authorizing")
        self.assertTrue(any("self-authorizing routing" in error for error in self_auth_errors))
        self.assertTrue(any("JSON Schema violation" in error for error in self_auth_errors))

        unqualified = json.loads(json.dumps(payload))
        routing = unqualified["checkpoints"][2]["routing"]
        routing["judgment_level"] = "security_or_release"
        routing["required_capabilities"] = []
        routing["qualification_evidence"] = []
        routing["independent_review"] = "not_required"
        unqualified_errors = MODULE.validate_task_contract(unqualified, "unqualified high-risk")
        self.assertTrue(
            any("unqualified high-risk execution" in error for error in unqualified_errors)
        )
        self.assertTrue(
            any("high-risk routing requires required_capabilities" in error for error in unqualified_errors)
        )

    def test_required_device_review_requires_device_capability_gate_and_evidence(self) -> None:
        payload = json.loads(
            (ROOT / "docs" / "contributing" / "task-contract.example.json").read_text(
                encoding="utf-8"
            )
        )
        incomplete = json.loads(json.dumps(payload))
        routing = incomplete["checkpoints"][0]["routing"]
        routing["independent_review"] = "required_device"
        routing["required_devices"] = []
        incomplete["checkpoints"][0]["device"] = {
            "required": False,
            "profiles": [],
            "acceptance": [],
            "evidence": [],
        }
        incomplete_errors = MODULE.validate_task_contract(incomplete, "incomplete device review")
        self.assertTrue(
            any("non-empty required_devices" in error for error in incomplete_errors)
        )
        self.assertTrue(
            any("coherent device gate" in error for error in incomplete_errors)
        )

        completed = json.loads(json.dumps(payload))
        completed_routing = completed["checkpoints"][0]["routing"]
        completed_routing["independent_review"] = "required_device"
        completed_routing["required_devices"] = ["pico-openxr-controller"]
        completed["checkpoints"][0]["device"] = {
            "required": True,
            "profiles": ["pico-openxr-controller"],
            "acceptance": ["Confirm world-space UI comfort on device"],
            "evidence": [],
        }
        completed["checkpoints"][0]["status"] = "completed"
        completed["checkpoints"][0]["evidence"] = [
            {
                "id": "E-NON-DEVICE",
                "kind": "test",
                "location": "docs/device-lab/README.md",
                "commit": "0000000000000000000000000000000000000000",
                "summary": "Not device evidence",
            }
        ]
        completed_errors = MODULE.validate_task_contract(completed, "completed without device evidence")
        self.assertTrue(
            any(
                "completed required_device checkpoint requires device evidence" in error
                for error in completed_errors
            )
        )

    def test_task_contract_rejects_duplicate_checkpoint_local_ids(self) -> None:
        payload = json.loads(
            (ROOT / "docs" / "contributing" / "task-contract.example.json").read_text(
                encoding="utf-8"
            )
        )
        duplicate_acceptance = json.loads(json.dumps(payload))
        duplicate_acceptance["checkpoints"][0]["acceptance"].append(
            {
                "id": "AC-01",
                "criterion": "Duplicate local acceptance id must fail closed.",
            }
        )
        acceptance_errors = MODULE.validate_task_contract(
            duplicate_acceptance, "duplicate acceptance ids"
        )
        self.assertTrue(
            any("duplicate acceptance id AC-01" in error for error in acceptance_errors)
        )

        duplicate_verification = json.loads(json.dumps(payload))
        duplicate_verification["checkpoints"][0]["verification"].append(
            {
                "id": "V-01",
                "procedure": "Repeat the same verification id.",
                "expected": "Validation fails closed.",
                "evidence_required": True,
            }
        )
        verification_errors = MODULE.validate_task_contract(
            duplicate_verification, "duplicate verification ids"
        )
        self.assertTrue(
            any("duplicate verification id V-01" in error for error in verification_errors)
        )

        duplicate_evidence = json.loads(json.dumps(payload))
        duplicate_evidence["checkpoints"][0]["evidence"].append(
            {
                "id": "E-CP01",
                "kind": "test",
                "location": "docs/architecture/example-duplicate.md",
                "commit": "0000000000000000000000000000000000000000",
                "summary": "Duplicate evidence id must fail closed.",
            }
        )
        evidence_errors = MODULE.validate_task_contract(
            duplicate_evidence, "duplicate evidence ids"
        )
        self.assertTrue(
            any("duplicate evidence id E-CP01" in error for error in evidence_errors)
        )

    def test_task_contract_rejects_unresolved_device_evidence_references(self) -> None:
        payload = json.loads(
            (ROOT / "docs" / "contributing" / "task-contract.example.json").read_text(
                encoding="utf-8"
            )
        )
        payload["checkpoints"][1]["device"] = {
            "required": True,
            "profiles": ["pico-openxr-controller"],
            "acceptance": ["Confirm the device evidence reference resolves locally"],
            "evidence": ["E-MISSING-DEVICE"],
        }

        errors = MODULE.validate_task_contract(payload, "unresolved device evidence")

        self.assertTrue(
            any(
                "device evidence reference 'E-MISSING-DEVICE' does not resolve within the checkpoint"
                in error
                for error in errors
            )
        )

    def test_task_contract_rejects_overlapping_concurrent_allowed_paths(self) -> None:
        payload = json.loads(
            (
                ROOT
                / "docs"
                / "contributing"
                / "tasks"
                / "agent-commons-public-workbench.task.json"
            ).read_text(encoding="utf-8")
        )
        for checkpoint in payload["checkpoints"]:
            if checkpoint["id"] == "WB-01V":
                activate_checkpoint_fixture(checkpoint)
            elif checkpoint["id"] == "WB-05":
                activate_checkpoint_fixture(checkpoint)
                checkpoint["allowed_paths"] = [
                    "scripts/validate_repository.py",
                    "docs/contributing/deliberation-protocol.md",
                ]
                break

        errors = MODULE.validate_task_contract(
            payload,
            "overlapping concurrent writes",
            require_canonical_repository=True,
        )

        self.assertTrue(
            any(
                "concurrent checkpoints WB-01V and WB-05 claim overlapping allowed_paths"
                in error
                for error in errors
            )
        )

    def test_allowed_paths_overlap_detects_docs_glob_starstar_witness(self) -> None:
        self.assertTrue(MODULE.allowed_paths_overlap("docs/**/foo", "docs/bar/**"))
        self.assertTrue(MODULE.allowed_path_matches("docs/bar/foo", "docs/**/foo"))
        self.assertTrue(MODULE.allowed_path_matches("docs/bar/foo", "docs/bar/**"))
        self.assertFalse(MODULE.allowed_paths_overlap("packages/a/**", "packages/b/**"))

        payload = json.loads(
            (
                ROOT
                / "docs"
                / "contributing"
                / "tasks"
                / "agent-commons-public-workbench.task.json"
            ).read_text(encoding="utf-8")
        )
        for checkpoint in payload["checkpoints"]:
            if checkpoint["id"] == "WB-01V":
                activate_checkpoint_fixture(checkpoint)
                checkpoint["allowed_paths"] = ["docs/**/foo"]
            elif checkpoint["id"] == "WB-05":
                activate_checkpoint_fixture(checkpoint)
                checkpoint["allowed_paths"] = ["docs/bar/**"]

        errors = MODULE.validate_task_contract(
            payload,
            "glob intersection witness",
            require_canonical_repository=True,
        )
        self.assertTrue(
            any(
                "concurrent checkpoints WB-01V and WB-05 claim overlapping allowed_paths"
                in error
                for error in errors
            )
        )

    def test_allowed_paths_unify_starstar_segment_grammar_docs_a_starstar_foo(self) -> None:
        # Embedded ** is illegal; previously matching treated docs/a**/foo as crossing
        # slash so it shared witness docs/a/x/foo with docs/a/x/foo while intersection
        # treated the patterns as disjoint.
        self.assertFalse(
            MODULE.is_safe_repository_relative_allowed_path("docs/a**/foo")
        )
        self.assertFalse(MODULE.allowed_path_matches("docs/a/x/foo", "docs/a**/foo"))
        self.assertTrue(MODULE.allowed_paths_overlap("docs/a**/foo", "docs/a/x/foo"))

        payload = json.loads(
            (
                ROOT
                / "docs"
                / "contributing"
                / "tasks"
                / "agent-commons-public-workbench.task.json"
            ).read_text(encoding="utf-8")
        )
        for checkpoint in payload["checkpoints"]:
            if checkpoint["id"] == "WB-01V":
                activate_checkpoint_fixture(checkpoint)
                checkpoint["allowed_paths"] = ["docs/a**/foo"]
            elif checkpoint["id"] == "WB-05":
                activate_checkpoint_fixture(checkpoint)
                checkpoint["allowed_paths"] = ["docs/a/x/foo"]

        errors = MODULE.validate_task_contract(
            payload,
            "embedded starstar grammar regression",
            require_canonical_repository=True,
        )
        self.assertTrue(
            any(
                "allowed_path 'docs/a**/foo' is not a safe repository-relative path" in error
                for error in errors
            ),
            errors,
        )
        self.assertTrue(
            any(
                "concurrent checkpoints WB-01V and WB-05 claim overlapping allowed_paths"
                in error
                for error in errors
            ),
            errors,
        )

    def test_allowed_paths_reject_unsafe_repository_relative_forms(self) -> None:
        unsafe_paths = [
            "../outside/**",
            "/tmp/**",
            "C:/temp/**",
            "c:\\temp\\**",
            "//server/share/**",
            "\\\\server\\share\\**",
            "docs/../scripts/**",
            "docs/./foo",
            "docs//foo",
            "docs/a**/foo",
            "",
            ".",
            "..",
            "**/../secret",
        ]
        for pattern in unsafe_paths:
            self.assertFalse(
                MODULE.is_safe_repository_relative_allowed_path(pattern),
                pattern,
            )

        self.assertTrue(
            MODULE.is_safe_repository_relative_allowed_path("docs/contributing/tasks/**")
        )
        self.assertTrue(
            MODULE.is_safe_repository_relative_allowed_path("docs/**/foo")
        )
        self.assertTrue(
            MODULE.is_safe_repository_relative_allowed_path("scripts/validate_repository.py")
        )

        payload = json.loads(
            (
                ROOT
                / "docs"
                / "contributing"
                / "tasks"
                / "agent-commons-public-workbench.task.json"
            ).read_text(encoding="utf-8")
        )
        for checkpoint in payload["checkpoints"]:
            if checkpoint["id"] == "WB-01V":
                checkpoint["allowed_paths"] = ["docs/../scripts/**"]
                break

        errors = MODULE.validate_task_contract(
            payload,
            "unsafe relative allowed_paths",
            require_canonical_repository=True,
        )
        self.assertTrue(
            any(
                "allowed_path 'docs/../scripts/**' is not a safe repository-relative path"
                in error
                for error in errors
            ),
            errors,
        )

        absolute_payload = json.loads(json.dumps(payload))
        for checkpoint in absolute_payload["checkpoints"]:
            if checkpoint["id"] == "WB-01V":
                checkpoint["allowed_paths"] = ["/tmp/**"]
                break
        absolute_errors = MODULE.validate_task_contract(
            absolute_payload,
            "absolute allowed_paths",
            require_canonical_repository=True,
        )
        self.assertTrue(
            any(
                "allowed_path '/tmp/**' is not a safe repository-relative path" in error
                for error in absolute_errors
            ),
            absolute_errors,
        )

        drive_payload = json.loads(json.dumps(payload))
        for checkpoint in drive_payload["checkpoints"]:
            if checkpoint["id"] == "WB-01V":
                checkpoint["allowed_paths"] = ["C:/temp/**"]
                break
        drive_errors = MODULE.validate_task_contract(
            drive_payload,
            "drive allowed_paths",
            require_canonical_repository=True,
        )
        self.assertTrue(
            any(
                "allowed_path 'C:/temp/**' is not a safe repository-relative path" in error
                for error in drive_errors
            ),
            drive_errors,
        )

        parent_payload = json.loads(json.dumps(payload))
        for checkpoint in parent_payload["checkpoints"]:
            if checkpoint["id"] == "WB-01V":
                checkpoint["allowed_paths"] = ["../outside/**"]
                break
        parent_errors = MODULE.validate_task_contract(
            parent_payload,
            "parent escape allowed_paths",
            require_canonical_repository=True,
        )
        self.assertTrue(
            any(
                "allowed_path '../outside/**' is not a safe repository-relative path"
                in error
                for error in parent_errors
            ),
            parent_errors,
        )

    def test_allowed_paths_casefold_ownership_aliases_overlap(self) -> None:
        self.assertTrue(MODULE.allowed_paths_overlap("README.md", "readme.md"))
        self.assertTrue(MODULE.allowed_paths_overlap("Docs/**", "docs/foo"))
        self.assertFalse(MODULE.allowed_paths_overlap("README.md", "LICENSE.md"))

        payload = json.loads(
            (
                ROOT
                / "docs"
                / "contributing"
                / "tasks"
                / "agent-commons-public-workbench.task.json"
            ).read_text(encoding="utf-8")
        )
        for checkpoint in payload["checkpoints"]:
            if checkpoint["id"] == "WB-01V":
                activate_checkpoint_fixture(checkpoint)
                checkpoint["allowed_paths"] = ["README.md"]
            elif checkpoint["id"] == "WB-05":
                activate_checkpoint_fixture(checkpoint)
                checkpoint["allowed_paths"] = ["readme.md"]

        errors = MODULE.validate_task_contract(
            payload,
            "casefold ownership alias",
            require_canonical_repository=True,
        )
        self.assertTrue(
            any(
                "concurrent checkpoints WB-01V and WB-05 claim overlapping allowed_paths"
                in error
                for error in errors
            ),
            errors,
        )

        same = json.loads(
            (
                ROOT
                / "docs"
                / "contributing"
                / "tasks"
                / "agent-commons-public-workbench.task.json"
            ).read_text(encoding="utf-8")
        )
        for checkpoint in same["checkpoints"]:
            if checkpoint["id"] == "WB-01V":
                checkpoint["allowed_paths"] = ["README.md", "readme.md"]
                break
        same_errors = MODULE.validate_task_contract(
            same,
            "casefold duplicate within checkpoint",
            require_canonical_repository=True,
        )
        self.assertTrue(
            any(
                "collides under portable ownership-key aliasing" in error
                for error in same_errors
            ),
            same_errors,
        )

    def test_allowed_paths_portable_ownership_key_rejects_windows_aliases_and_controls(
        self,
    ) -> None:
        import unicodedata

        nfd_readme = "README" + unicodedata.normalize("NFD", "é") + ".md"
        nfc_readme = unicodedata.normalize("NFC", nfd_readme)
        self.assertNotEqual(nfd_readme, nfc_readme)

        next_line = "docs/foo\u0085bar"
        zero_width = "docs/foo\u200bbar"
        bidi_cf = "docs/foo\u202ebar"
        self.assertEqual(unicodedata.category("\u0085"), "Cc")
        self.assertEqual(unicodedata.category("\u200b"), "Cf")
        self.assertEqual(unicodedata.category("\u202e"), "Cf")
        unsafe_aliases = [
            "README.md.",
            "README.md::",
            "README.md::$DATA",
            "README.md\x00",
            "README.md\x1f",
            "README.md\x7f",
            next_line,
            zero_width,
            bidi_cf,
            " CON",
            "CON ",
            "docs\\readme.md",
            " docs/foo",
            "docs/foo ",
            "NUL",
            "nul.txt",
            "COM1",
            "COM¹",
            "COM²",
            "COM³",
            "lpt9.log",
            "LPT¹",
            "LPT².txt",
            "LPT³.log",
            "AUX.cache",
            "PRN.md",
            "docs/foo.",
            "docs/foo /bar",
            nfd_readme,
        ]
        for pattern in unsafe_aliases:
            self.assertIsNone(
                MODULE.allowed_path_ownership_key(pattern),
                pattern,
            )
            self.assertFalse(
                MODULE.is_safe_repository_relative_allowed_path(pattern),
                pattern,
            )

        self.assertEqual(
            MODULE.allowed_path_ownership_key("README.md"),
            "readme.md",
        )
        self.assertEqual(
            MODULE.allowed_path_ownership_key(nfc_readme),
            nfc_readme.casefold(),
        )
        self.assertTrue(
            MODULE.is_safe_repository_relative_allowed_path("docs/**/foo")
        )
        self.assertTrue(
            MODULE.is_safe_repository_relative_allowed_path("docs/*/?.md")
        )

        # Unsafe aliases fail closed for overlap and never match canonically.
        self.assertTrue(MODULE.allowed_paths_overlap("README.md", "README.md."))
        self.assertTrue(MODULE.allowed_paths_overlap("README.md", "README.md::"))
        self.assertTrue(
            MODULE.allowed_paths_overlap("README.md", "README.md::$DATA")
        )
        self.assertTrue(MODULE.allowed_paths_overlap("README.md", nfd_readme))
        self.assertFalse(MODULE.allowed_path_matches("README.md.", "README.md."))
        self.assertFalse(
            MODULE.allowed_path_matches("README.md::$DATA", "README.md::$DATA")
        )
        self.assertTrue(MODULE.allowed_path_matches("README.md", "README.md"))
        self.assertTrue(MODULE.allowed_path_matches("readme.md", "README.md"))
        self.assertTrue(MODULE.allowed_path_matches(nfc_readme, nfc_readme))

        workbench = (
            ROOT
            / "docs"
            / "contributing"
            / "tasks"
            / "agent-commons-public-workbench.task.json"
        )

        def contract_errors(left: str, right: str, label: str) -> list[str]:
            payload = json.loads(workbench.read_text(encoding="utf-8"))
            for checkpoint in payload["checkpoints"]:
                if checkpoint["id"] == "WB-01V":
                    activate_checkpoint_fixture(checkpoint)
                    checkpoint["allowed_paths"] = [left]
                elif checkpoint["id"] == "WB-05":
                    activate_checkpoint_fixture(checkpoint)
                    checkpoint["allowed_paths"] = [right]
            return MODULE.validate_task_contract(
                payload,
                label,
                require_canonical_repository=True,
            )

        attack_pairs = [
            ("README.md.", "README.md", "trailing-dot ownership alias"),
            ("README.md::", "README.md", "ads-colon ownership alias"),
            ("README.md::$DATA", "README.md", "ads-data ownership alias"),
            ("README.md\x00", "README.md", "c0-control ownership alias"),
            (next_line, "README.md", "u+0085-cc ownership alias"),
            (zero_width, "README.md", "u+200b-cf ownership alias"),
            (bidi_cf, "README.md", "bidi-cf ownership alias"),
            ("NUL.txt", "README.md", "reserved-device ownership"),
            ("COM¹", "README.md", "superscript-com1 ownership"),
            ("LPT².txt", "README.md", "superscript-lpt2 ownership"),
            ("docs\\readme.md", "README.md", "backslash ownership alias"),
            (" README.md", "README.md", "outer-whitespace ownership alias"),
            (nfd_readme, nfc_readme, "nfd-nfc ownership alias"),
        ]
        for left, right, label in attack_pairs:
            errors = contract_errors(left, right, label)
            self.assertTrue(
                any(
                    f"allowed_path {left!r} is not a safe repository-relative path"
                    in error
                    for error in errors
                ),
                (label, errors),
            )
            self.assertTrue(
                any(
                    "concurrent checkpoints WB-01V and WB-05 claim overlapping allowed_paths"
                    in error
                    for error in errors
                ),
                (label, errors),
            )

        duplicate = json.loads(workbench.read_text(encoding="utf-8"))
        for checkpoint in duplicate["checkpoints"]:
            if checkpoint["id"] == "WB-01V":
                checkpoint["allowed_paths"] = [nfc_readme, nfc_readme]
                break
        duplicate_errors = MODULE.validate_task_contract(
            duplicate,
            "nfc ownership duplicate",
            require_canonical_repository=True,
        )
        self.assertTrue(
            any(
                "collides under portable ownership-key aliasing" in error
                for error in duplicate_errors
            ),
            duplicate_errors,
        )

    def test_task_contract_rejects_non_downstream_integration_fan_in(self) -> None:
        payload = json.loads(
            (ROOT / "docs" / "contributing" / "task-contract.example.json").read_text(
                encoding="utf-8"
            )
        )
        payload["integration"]["checkpoint_id"] = "CP-01"

        errors = MODULE.validate_task_contract(payload, "non-downstream fan-in")

        self.assertTrue(
            any(
                "integration checkpoint CP-01 must be downstream of CP-02" in error
                for error in errors
            )
        )
        self.assertTrue(
            any(
                "integration checkpoint CP-01 must be downstream of CP-03" in error
                for error in errors
            )
        )

    def test_task_registry_enforces_safe_unique_and_agreeing_entries(self) -> None:
        registry = json.loads(
            (ROOT / "docs" / "contributing" / "tasks" / "task-registry.json").read_text(
                encoding="utf-8"
            )
        )
        self.assertEqual([], MODULE.validate_task_registry(ROOT, registry))

        unsafe = json.loads(json.dumps(registry))
        unsafe["tasks"][0]["contract"] = "docs/contributing/tasks/../task-hall.v1.json"
        unsafe_errors = MODULE.validate_task_registry(ROOT, unsafe, "unsafe registry")
        self.assertTrue(any("contract path is unsafe" in error for error in unsafe_errors))

        missing = json.loads(json.dumps(registry))
        missing["tasks"][0]["contract"] = "docs/contributing/tasks/missing-task.task.json"
        missing_errors = MODULE.validate_task_registry(ROOT, missing, "missing registry")
        self.assertTrue(any("does not exist" in error for error in missing_errors))

        duplicate = json.loads(json.dumps(registry))
        duplicate["tasks"].append(json.loads(json.dumps(duplicate["tasks"][0])))
        duplicate_errors = MODULE.validate_task_registry(ROOT, duplicate, "duplicate registry")
        self.assertTrue(any("duplicate task id" in error for error in duplicate_errors))

        mismatched = json.loads(json.dumps(registry))
        mismatched["tasks"][0]["state"] = "closed"
        mismatched["tasks"][0]["umbrella_issue"] = "https://github.com/Lingkyn/xr-foundry/issues/999"
        mismatch_errors = MODULE.validate_task_registry(ROOT, mismatched, "mismatched registry")
        self.assertTrue(any("contract state must equal" in error for error in mismatch_errors))
        self.assertTrue(any("umbrella Issue must equal" in error for error in mismatch_errors))

        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            contract_rel = "docs/contributing/tasks/agent-commons-public-workbench.task.json"
            source = ROOT / contract_rel
            target = root / contract_rel
            target.parent.mkdir(parents=True, exist_ok=True)
            task = json.loads(source.read_text(encoding="utf-8"))
            task["public_projection"]["repository"] = "https://github.com/other-org/other-repo"
            task["public_projection"]["umbrella_issue"] = (
                "https://github.com/other-org/other-repo/issues/34"
            )
            task["public_projection"]["project"] = "https://github.com/orgs/other-org/projects/1"
            for entry in task["public_projection"]["checkpoint_issues"]:
                number = entry["issue"].rsplit("/", 1)[-1]
                entry["issue"] = f"https://github.com/other-org/other-repo/issues/{number}"
            target.write_text(json.dumps(task), encoding="utf-8")
            for schema_rel in (
                "docs/contributing/tasks/task-registry.schema.json",
                "docs/contributing/task-contract.schema.json",
            ):
                schema_path = root / schema_rel
                schema_path.parent.mkdir(parents=True, exist_ok=True)
                schema_path.write_bytes((ROOT / schema_rel).read_bytes())
            foreign = json.loads(json.dumps(registry))
            foreign["tasks"][0]["umbrella_issue"] = task["public_projection"]["umbrella_issue"]
            foreign_errors = MODULE.validate_task_registry(root, foreign, "foreign registry")
            self.assertTrue(any("canonical repository" in error for error in foreign_errors))
            self.assertTrue(
                any("canonical Task Hall Project" in error for error in foreign_errors)
            )

    def test_task_registry_rejects_duplicate_paths_and_projection_urls(self) -> None:
        registry = json.loads(
            (ROOT / "docs" / "contributing" / "tasks" / "task-registry.json").read_text(
                encoding="utf-8"
            )
        )
        live = json.loads(
            (
                ROOT
                / "docs"
                / "contributing"
                / "tasks"
                / "agent-commons-public-workbench.task.json"
            ).read_text(encoding="utf-8")
        )
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            first_rel = "docs/contributing/tasks/agent-commons-public-workbench.task.json"
            second_rel = "docs/contributing/tasks/second-workbench.task.json"
            for schema_rel in (
                "docs/contributing/tasks/task-registry.schema.json",
                "docs/contributing/task-contract.schema.json",
            ):
                schema_path = root / schema_rel
                schema_path.parent.mkdir(parents=True, exist_ok=True)
                schema_path.write_bytes((ROOT / schema_rel).read_bytes())

            first = json.loads(json.dumps(live))
            second = json.loads(json.dumps(live))
            second["id"] = "second-workbench-task"
            second["public_projection"]["umbrella_issue"] = (
                "https://github.com/Lingkyn/xr-foundry/issues/134"
            )
            for index, entry in enumerate(second["public_projection"]["checkpoint_issues"]):
                entry["issue"] = f"https://github.com/Lingkyn/xr-foundry/issues/{200 + index}"
            (root / first_rel).parent.mkdir(parents=True, exist_ok=True)
            (root / first_rel).write_text(json.dumps(first), encoding="utf-8")
            (root / second_rel).write_text(json.dumps(second), encoding="utf-8")

            duplicate_contract = {
                "schema": "xr-foundry.task_registry.v1",
                "coverage": {"mode": "explicit_registration"},
                "authority": registry["authority"],
                "tasks": [
                    {
                        "task_id": first["id"],
                        "contract": first_rel,
                        "umbrella_issue": first["public_projection"]["umbrella_issue"],
                        "state": first["state"],
                    },
                    {
                        "task_id": second["id"],
                        "contract": first_rel,
                        "umbrella_issue": second["public_projection"]["umbrella_issue"],
                        "state": second["state"],
                    },
                ],
            }
            contract_errors = MODULE.validate_task_registry(
                root, duplicate_contract, "duplicate contract path registry"
            )
            self.assertTrue(any("duplicate contract path" in error for error in contract_errors))

            duplicate_umbrella = json.loads(json.dumps(duplicate_contract))
            duplicate_umbrella["tasks"][1]["contract"] = second_rel
            duplicate_umbrella["tasks"][1]["umbrella_issue"] = first["public_projection"][
                "umbrella_issue"
            ]
            second_collide = json.loads(json.dumps(second))
            second_collide["public_projection"]["umbrella_issue"] = first["public_projection"][
                "umbrella_issue"
            ]
            (root / second_rel).write_text(json.dumps(second_collide), encoding="utf-8")
            umbrella_errors = MODULE.validate_task_registry(
                root, duplicate_umbrella, "duplicate umbrella registry"
            )
            self.assertTrue(any("duplicate umbrella Issue" in error for error in umbrella_errors))

            second_ok = json.loads(json.dumps(second))
            second_ok["public_projection"]["checkpoint_issues"][0]["issue"] = first[
                "public_projection"
            ]["checkpoint_issues"][0]["issue"]
            (root / second_rel).write_text(json.dumps(second_ok), encoding="utf-8")
            duplicate_checkpoint = {
                "schema": "xr-foundry.task_registry.v1",
                "coverage": {"mode": "explicit_registration"},
                "authority": registry["authority"],
                "tasks": [
                    {
                        "task_id": first["id"],
                        "contract": first_rel,
                        "umbrella_issue": first["public_projection"]["umbrella_issue"],
                        "state": first["state"],
                    },
                    {
                        "task_id": second["id"],
                        "contract": second_rel,
                        "umbrella_issue": second["public_projection"]["umbrella_issue"],
                        "state": second["state"],
                    },
                ],
            }
            checkpoint_errors = MODULE.validate_task_registry(
                root, duplicate_checkpoint, "duplicate checkpoint issue registry"
            )
            self.assertTrue(
                any("duplicate checkpoint Issue URL" in error for error in checkpoint_errors)
            )

    def test_task_registry_rejects_symlink_contracts(self) -> None:
        registry = json.loads(
            (ROOT / "docs" / "contributing" / "tasks" / "task-registry.json").read_text(
                encoding="utf-8"
            )
        )
        live = (
            ROOT
            / "docs"
            / "contributing"
            / "tasks"
            / "agent-commons-public-workbench.task.json"
        ).read_bytes()
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            contract_rel = "docs/contributing/tasks/agent-commons-public-workbench.task.json"
            for schema_rel in (
                "docs/contributing/tasks/task-registry.schema.json",
                "docs/contributing/task-contract.schema.json",
            ):
                schema_path = root / schema_rel
                schema_path.parent.mkdir(parents=True, exist_ok=True)
                schema_path.write_bytes((ROOT / schema_rel).read_bytes())
            target = root / contract_rel
            target.parent.mkdir(parents=True, exist_ok=True)
            real_file = root / "docs" / "contributing" / "tasks" / "real-source.task.json"
            real_file.write_bytes(live)
            try:
                target.symlink_to(real_file)
            except (OSError, NotImplementedError):
                self.skipTest("symlinks unavailable in this environment")
            symlink_errors = MODULE.validate_task_registry(
                root, registry, "symlink registry"
            )
            self.assertTrue(any("must not be a symlink" in error for error in symlink_errors))

    def test_task_registry_rejects_hardlink_contracts(self) -> None:
        registry = json.loads(
            (ROOT / "docs" / "contributing" / "tasks" / "task-registry.json").read_text(
                encoding="utf-8"
            )
        )
        live = (
            ROOT
            / "docs"
            / "contributing"
            / "tasks"
            / "agent-commons-public-workbench.task.json"
        ).read_bytes()
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            contract_rel = "docs/contributing/tasks/agent-commons-public-workbench.task.json"
            for schema_rel in (
                "docs/contributing/tasks/task-registry.schema.json",
                "docs/contributing/task-contract.schema.json",
            ):
                schema_path = root / schema_rel
                schema_path.parent.mkdir(parents=True, exist_ok=True)
                schema_path.write_bytes((ROOT / schema_rel).read_bytes())
            target = root / contract_rel
            target.parent.mkdir(parents=True, exist_ok=True)
            hardlink_source = root / "docs" / "contributing" / "tasks" / "hardlink-source.task.json"
            hardlink_source.write_bytes(live)
            try:
                os.link(hardlink_source, target)
            except (OSError, NotImplementedError, AttributeError):
                self.skipTest("hardlinks unavailable in this environment")
            hardlink_errors = MODULE.validate_task_registry(
                root, registry, "hardlink registry"
            )
            self.assertTrue(any("must not be a hardlink" in error for error in hardlink_errors))

    def test_task_registry_rejects_tasks_directory_parent_link_escape(self) -> None:
        registry = json.loads(
            (ROOT / "docs" / "contributing" / "tasks" / "task-registry.json").read_text(
                encoding="utf-8"
            )
        )
        live = (
            ROOT
            / "docs"
            / "contributing"
            / "tasks"
            / "agent-commons-public-workbench.task.json"
        ).read_bytes()
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            external = Path(directory) / "external-tasks"
            external.mkdir(parents=True, exist_ok=True)
            (external / "agent-commons-public-workbench.task.json").write_bytes(live)
            contributing = root / "docs" / "contributing"
            contributing.mkdir(parents=True, exist_ok=True)
            tasks_link = contributing / "tasks"
            linked = False
            if os.name == "nt":
                completed = subprocess.run(
                    ["cmd", "/c", "mklink", "/J", str(tasks_link), str(external)],
                    capture_output=True,
                    text=True,
                    check=False,
                )
                linked = completed.returncode == 0 and tasks_link.exists()
            if not linked:
                try:
                    tasks_link.symlink_to(external, target_is_directory=True)
                    linked = True
                except (OSError, NotImplementedError):
                    self.skipTest("parent directory junction/symlink unavailable")

            for schema_rel in (
                "docs/contributing/tasks/task-registry.schema.json",
                "docs/contributing/task-contract.schema.json",
            ):
                schema_path = root / schema_rel
                schema_path.parent.mkdir(parents=True, exist_ok=True)
                schema_path.write_bytes((ROOT / schema_rel).read_bytes())

            errors = MODULE.validate_task_registry(root, registry, "parent-link registry")
            self.assertTrue(
                any(
                    "must not be a symlink, junction, or reparse point" in error
                    or "resolves outside the repository root" in error
                    for error in errors
                ),
                errors,
            )

    def test_task_registry_resolution_errors_fail_closed(self) -> None:
        registry = json.loads(
            (ROOT / "docs" / "contributing" / "tasks" / "task-registry.json").read_text(
                encoding="utf-8"
            )
        )
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            contract_rel = "docs/contributing/tasks/agent-commons-public-workbench.task.json"
            for schema_rel in (
                "docs/contributing/tasks/task-registry.schema.json",
                "docs/contributing/task-contract.schema.json",
            ):
                schema_path = root / schema_rel
                schema_path.parent.mkdir(parents=True, exist_ok=True)
                schema_path.write_bytes((ROOT / schema_rel).read_bytes())
            target = root / contract_rel
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(
                (
                    ROOT
                    / "docs"
                    / "contributing"
                    / "tasks"
                    / "agent-commons-public-workbench.task.json"
                ).read_bytes()
            )
            original_resolve = Path.resolve

            def boom(self: Path, *args: object, **kwargs: object):
                text = self.as_posix().replace("\\", "/")
                if text.endswith("/docs/contributing/tasks") or text.endswith(contract_rel):
                    raise OSError("simulated resolution failure")
                return original_resolve(self, *args, **kwargs)

            with mock.patch.object(Path, "resolve", boom):
                errors = MODULE.validate_task_registry(
                    root, registry, "resolution-error registry"
                )
            self.assertTrue(
                any("unreadable" in error for error in errors),
                errors,
            )

    def test_resolution_runtime_error_fail_closed_at_three_sites(self) -> None:
        registry = json.loads(
            (ROOT / "docs" / "contributing" / "tasks" / "task-registry.json").read_text(
                encoding="utf-8"
            )
        )
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            contract_rel = "docs/contributing/tasks/agent-commons-public-workbench.task.json"
            for schema_rel in (
                "docs/contributing/tasks/task-registry.schema.json",
                "docs/contributing/task-contract.schema.json",
            ):
                schema_path = root / schema_rel
                schema_path.parent.mkdir(parents=True, exist_ok=True)
                schema_path.write_bytes((ROOT / schema_rel).read_bytes())
            target = root / contract_rel
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(
                (
                    ROOT
                    / "docs"
                    / "contributing"
                    / "tasks"
                    / "agent-commons-public-workbench.task.json"
                ).read_bytes()
            )
            original_resolve = Path.resolve
            root_resolved = root.resolve()
            tasks_resolved = (root / "docs" / "contributing" / "tasks").resolve()
            contract_resolved = target.resolve()

            def boom_repo_root(self: Path, *args: object, **kwargs: object):
                candidate = original_resolve(self, *args, **kwargs)
                if candidate == root_resolved or self == root:
                    raise RuntimeError("simulated repo-root symlink loop")
                return candidate

            with mock.patch.object(Path, "resolve", boom_repo_root):
                repo_errors = MODULE.resolve_controlled_tasks_root(
                    root, "runtime-error repo root"
                )[2]
            self.assertTrue(
                any("repository root is unreadable" in error for error in repo_errors),
                repo_errors,
            )
            with mock.patch.object(Path, "resolve", boom_repo_root):
                registry_repo_errors = MODULE.validate_task_registry(
                    root, registry, "runtime-error repo root registry"
                )
            self.assertTrue(
                any("repository root is unreadable" in error for error in registry_repo_errors),
                registry_repo_errors,
            )

            def boom_tasks_root(self: Path, *args: object, **kwargs: object):
                text = self.as_posix().replace("\\", "/")
                if text.endswith("/docs/contributing/tasks"):
                    raise RuntimeError("simulated tasks-root symlink loop")
                candidate = original_resolve(self, *args, **kwargs)
                if candidate == tasks_resolved:
                    raise RuntimeError("simulated tasks-root symlink loop")
                return candidate

            with mock.patch.object(Path, "resolve", boom_tasks_root):
                tasks_errors = MODULE.resolve_controlled_tasks_root(
                    root, "runtime-error tasks root"
                )[2]
            self.assertTrue(
                any(
                    "controlled tasks directory is unreadable" in error
                    for error in tasks_errors
                ),
                tasks_errors,
            )
            with mock.patch.object(Path, "resolve", boom_tasks_root):
                registry_tasks_errors = MODULE.validate_task_registry(
                    root, registry, "runtime-error tasks root registry"
                )
            self.assertTrue(
                any(
                    "controlled tasks directory is unreadable" in error
                    for error in registry_tasks_errors
                ),
                registry_tasks_errors,
            )

            def boom_contract(self: Path, *args: object, **kwargs: object):
                text = self.as_posix().replace("\\", "/")
                if text.endswith(contract_rel):
                    raise RuntimeError("simulated contract symlink loop")
                candidate = original_resolve(self, *args, **kwargs)
                if candidate == contract_resolved:
                    raise RuntimeError("simulated contract symlink loop")
                return candidate

            with mock.patch.object(Path, "resolve", boom_contract):
                contract_path, contract_errors = MODULE.inspect_registered_contract_path(
                    root, contract_rel, "runtime-error contract"
                )
            self.assertIsNone(contract_path)
            self.assertTrue(
                any("registered contract is unreadable" in error for error in contract_errors),
                contract_errors,
            )
            with mock.patch.object(Path, "resolve", boom_contract):
                registry_contract_errors = MODULE.validate_task_registry(
                    root, registry, "runtime-error contract registry"
                )
            self.assertTrue(
                any(
                    "registered contract is unreadable" in error
                    for error in registry_contract_errors
                ),
                registry_contract_errors,
            )

    def test_task_contract_validation_uses_target_root_schema(self) -> None:
        payload = json.loads(
            (ROOT / "docs" / "contributing" / "task-contract.example.json").read_text(
                encoding="utf-8"
            )
        )
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            schema_path = root / "docs" / "contributing" / "task-contract.schema.json"
            schema_path.parent.mkdir(parents=True, exist_ok=True)
            schema_path.write_text("{", encoding="utf-8")
            errors = MODULE.validate_task_contract(
                payload, "foreign-root schema", root=root
            )
            self.assertTrue(any("invalid JSON" in error or "JSON" in error for error in errors))
            self.assertFalse(
                any("canonical Task Hall umbrella lifecycle" in error for error in errors)
            )

    def test_task_hall_lifecycle_and_policies_are_machine_enforced(self) -> None:
        payload = json.loads(
            (ROOT / "docs" / "contributing" / "task-hall.v1.json").read_text(
                encoding="utf-8"
            )
        )
        lifecycle = json.loads(json.dumps(payload))
        lifecycle["lifecycle"]["umbrella_states"][2:4] = ["active", "ready"]
        lifecycle_errors = MODULE.validate_task_hall_authority(lifecycle)
        self.assertTrue(any("umbrella lifecycle" in error for error in lifecycle_errors))

        durability = json.loads(json.dumps(payload))
        durability["durability_policy"]["local_only_progress_is_non_transferable"] = False
        durability_errors = MODULE.validate_task_hall_authority(durability)
        self.assertTrue(
            any("local_only_progress_is_non_transferable" in error for error in durability_errors)
        )

        routing = json.loads(json.dumps(payload))
        routing["routing_policy"]["self_report_grants_authority"] = True
        routing["routing_policy"]["model_or_agent_ranking"] = True
        routing_errors = MODULE.validate_task_hall_authority(routing)
        self.assertTrue(any("self_report_grants_authority" in error for error in routing_errors))
        self.assertTrue(any("model_or_agent_ranking" in error for error in routing_errors))

        registry = json.loads(json.dumps(payload))
        registry["registry_policy"]["registry"] = "tmp/evil-registry.json"
        registry_errors = MODULE.validate_task_hall_authority(registry)
        self.assertTrue(any("registry='tmp/evil-registry.json'" in error or "must keep registry=" in error for error in registry_errors))

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

    def test_device_plan_enforces_duration_posture_sources_and_interaction_matrix(self) -> None:
        plan = json.loads(
            (ROOT / "docs" / "device-lab" / "test-plans" / "inventory-world-space-ui-v1.json").read_text(
                encoding="utf-8"
            )
        )
        plan["execution_requirements"]["minimum_duration_seconds"] = 119
        plan["execution_requirements"]["allowed_postures"] = []
        plan["allowed_targets"][0]["required_input_source_ids"] = []
        plan["required_checks"] = [
            check
            for check in plan["required_checks"]
            if check["id"] != "right-controller-disabled-no-mutation"
        ]

        errors = MODULE.validate_capability_test_plan(
            plan, current_device_profiles(), "weak device plan"
        )

        self.assertTrue(any("at least 120" in error for error in errors))
        self.assertTrue(any("allowed_postures" in error for error in errors))
        self.assertTrue(any("unique input source IDs" in error for error in errors))
        self.assertTrue(any("interaction matrix is incomplete" in error for error in errors))

    def test_device_receipt_rejects_missing_or_forged_lock_and_evidence(self) -> None:
        payload = completed_device_lab_receipt()
        payload["dependency_resolution"]["lock"]["ref"] = (
            "docs/validation/evidence/missing-lock.json"
        )
        payload["dependency_resolution"]["lock"]["sha256"] = "e" * 64
        target = next(check for check in payload["checks"] if check["status"] == "pass")
        target["evidence_refs"][0]["sha256"] = "f" * 64

        errors = MODULE.validate_device_lab_execution_receipt(
            payload, current_device_profiles(), current_device_plans(), "forged files"
        )

        self.assertTrue(any("lock repository file does not exist" in error for error in errors))
        self.assertTrue(any("evidence SHA-256 does not match file" in error for error in errors))

    def test_device_receipt_rejects_dependency_input_build_and_execution_tuple_drift(self) -> None:
        payload = completed_device_lab_receipt()
        payload["dependency_resolution"]["resolved_packages"] = [
            package
            for package in payload["dependency_resolution"]["resolved_packages"]
            if package["id"] != "com.unity.xr.openxr"
        ]
        payload["build"]["target"] = "latest"
        payload["input"]["sources"] = [
            source for source in payload["input"]["sources"] if source["id"] != "right-controller"
        ]
        payload["execution_context"] = {"posture": "prone", "duration_seconds": 119}

        errors = MODULE.validate_device_lab_execution_receipt(
            payload, current_device_profiles(), current_device_plans(), "tuple drift"
        )

        self.assertTrue(any("plan-required resolved packages are missing" in error for error in errors))
        self.assertTrue(any("build.target must be an exact" in error for error in errors))
        self.assertTrue(any("required input sources are missing" in error for error in errors))
        self.assertTrue(any("below the plan minimum" in error for error in errors))
        self.assertTrue(any("posture is not admitted" in error for error in errors))

    def test_device_receipt_rejects_placeholder_runtime_and_os_versions(self) -> None:
        payload = completed_device_lab_receipt()
        payload["software"]["runtime_version"] = "recorded-runtime-version"
        payload["device"]["os_version"] = "current"

        errors = MODULE.validate_device_lab_execution_receipt(
            payload, current_device_profiles(), current_device_plans(), "placeholder versions"
        )

        self.assertTrue(any("exact dotted runtime version" in error for error in errors))
        self.assertTrue(any("exact dotted OS version" in error for error in errors))

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

    def test_device_receipt_rejects_local_only_revision_with_public_tree(self) -> None:
        payload = completed_device_lab_receipt()
        public_commit = payload["revision"]["commit_sha"]
        tree = subprocess.check_output(
            ["git", "rev-parse", f"{public_commit}^{{tree}}"], cwd=ROOT, text=True
        ).strip()
        payload["revision"]["commit_sha"] = subprocess.check_output(
            [
                "git",
                "-c",
                "user.name=XR Foundry Contract Test",
                "-c",
                "user.email=xr-foundry-contract-test@example.invalid",
                "commit-tree",
                tree,
            ],
            cwd=ROOT,
            input="device local-only evidence object\n",
            text=True,
        ).strip()

        errors = MODULE.validate_device_lab_execution_receipt(
            payload, current_device_profiles(), current_device_plans(), "local-only device"
        )

        self.assertTrue(any("fetched public origin ref" in error for error in errors))

    def test_device_receipt_rejects_unbound_revision_and_artifact(self) -> None:
        payload = completed_device_lab_receipt()
        payload["revision"]["commit_sha"] = None
        payload["artifact"] = {
            "kind": "android-apk",
            "file_name": "missing.apk",
            "sha256": None,
            "repository_path": None,
            "application_id": None,
        }

        errors = MODULE.validate_device_lab_execution_receipt(
            payload, current_device_profiles(), current_device_plans(), "unbound execution"
        )

        self.assertTrue(any("full 40-character commit SHA" in error for error in errors))
        self.assertTrue(any("artifact SHA-256" in error for error in errors))
        self.assertTrue(any("artifact.repository_path" in error for error in errors))
        self.assertTrue(any("artifact.application_id" in error for error in errors))

    def test_device_receipt_rejects_fake_or_lfs_pointer_artifact(self) -> None:
        payload = completed_device_lab_receipt()
        artifact_path = ROOT / payload["artifact"]["repository_path"]
        artifact_path.write_text(
            "version https://git-lfs.github.com/spec/v1\n"
            "oid sha256:" + "a" * 64 + "\nsize 123\n",
            encoding="utf-8",
        )
        payload["artifact"]["sha256"] = hashlib.sha256(artifact_path.read_bytes()).hexdigest()

        errors = MODULE.validate_device_lab_execution_receipt(
            payload, current_device_profiles(), current_device_plans(), "lfs artifact"
        )

        self.assertTrue(any("materialized, not a Git LFS pointer" in error for error in errors))

    def test_device_receipt_rejects_fake_pk_bytes_and_missing_manifest(self) -> None:
        for mutation in ("fake-pk", "missing-manifest", "prefixed-zip"):
            with self.subTest(mutation=mutation):
                payload = completed_device_lab_receipt()
                artifact_path = ROOT / payload["artifact"]["repository_path"]
                if mutation == "fake-pk":
                    artifact_path.write_bytes(b"PK\x03\x04not-a-zip")
                elif mutation == "prefixed-zip":
                    original = artifact_path.read_bytes()
                    artifact_path.write_bytes(b"MZ-prefixed-polyglot" + original)
                else:
                    with zipfile.ZipFile(
                        artifact_path, "w", compression=zipfile.ZIP_DEFLATED
                    ) as archive:
                        archive.writestr("classes.dex", b"dex\n035\x00" + b"\x00" * 112)
                        archive.writestr(
                            "lib/arm64-v8a/libunity.so", b"\x7fELFunity"
                        )
                        archive.writestr(
                            "lib/arm64-v8a/libil2cpp.so", b"\x7fELFil2cpp"
                        )
                        archive.writestr(
                            "assets/bin/Data/globalgamemanagers", b"unity-player-data"
                        )
                payload["artifact"]["sha256"] = hashlib.sha256(
                    artifact_path.read_bytes()
                ).hexdigest()

                errors = MODULE.validate_device_lab_execution_receipt(
                    payload,
                    current_device_profiles(),
                    current_device_plans(),
                    f"{mutation} artifact",
                )

                marker = {
                    "fake-pk": "valid APK ZIP",
                    "missing-manifest": "must contain exactly one AndroidManifest.xml",
                    "prefixed-zip": "must begin with a ZIP local-file header",
                }[mutation]
                self.assertTrue(any(marker in error for error in errors), errors)

    def test_device_receipt_application_id_must_match_binary_manifest(self) -> None:
        payload = completed_device_lab_receipt()
        payload["artifact"]["application_id"] = "com.example.different"

        errors = MODULE.validate_device_lab_execution_receipt(
            payload, current_device_profiles(), current_device_plans(), "application drift"
        )

        self.assertTrue(any("must equal the APK manifest package ID" in error for error in errors))

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
        target = next(
            check
            for check in payload["checks"]
            if check["id"] == "left-controller-left-target-activate"
        )
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
