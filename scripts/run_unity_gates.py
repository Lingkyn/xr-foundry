from __future__ import annotations

"""Run every automated Unity gate of XR Foundry with one command.

Anyone with a Unity Editor and Python can execute this script; it needs no
hand-maintained Unity project. It

1. locates a Unity Editor (``--unity``, ``UNITY_EDITOR``, or the Unity Hub default
   install locations), preferring the version the reference consumer records;
2. generates a disposable host project for the selected packages: the
   reference-system binding consumer, every live package, or a minimal host for the
   named packages plus their ``com.lingkyn.*`` dependencies;
3. audits the exact test-case count of every test assembly from source;
4. runs each assembly in its own Unity batchmode process with an
   ``-assemblyNames`` filter and verifies the NUnit result through
   ``verify_unity_test_results.py`` against that count and the recorded run boundary;
5. writes a machine-readable run receipt with the commit, host manifest, resolved
   dependency lock digest, editor, and per-assembly verification.

A passing receipt is Editor evidence for one exact tuple. It is not a player,
controller, headset, comfort, or named-device claim; those need the Device Lab.
"""

import argparse
import datetime as _dt
import glob
import hashlib
import importlib.util
import json
import os
import platform
import shutil
import subprocess
import sys
import tempfile
import time
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
SCRIPTS = ROOT / "scripts"
CONSUMER_TEMPLATE = ROOT / "compositions" / "unity" / "reference-system" / "consumer"
PACKAGE_CATALOG = ROOT / "package-catalog.json"
RECEIPT_SCHEMA = "xr-foundry.unity_gate_run.v1"
DEFAULT_OUTPUT_ROOT = ROOT / ".unity-gates"
TEST_FRAMEWORK_VERSION = "1.6.0"
# The exact XR tuple recorded by the current editor_automated compatibility profiles.
XR_HOST_PINS = {
    "com.unity.inputsystem": "1.19.0",
    "com.unity.xr.management": "4.5.3",
    "com.unity.xr.openxr": "1.16.0",
}
MODES = ("EditMode", "PlayMode")


def _load_module(name: str, filename: str):
    spec = importlib.util.spec_from_file_location(name, SCRIPTS / filename)
    assert spec and spec.loader
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


AUDIT = _load_module("audit_unity_test_inventory", "audit_unity_test_inventory.py")
VERIFY = _load_module("verify_unity_test_results", "verify_unity_test_results.py")


def utc_now() -> str:
    return _dt.datetime.now(_dt.timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def sha256_of(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def git_output(*args: str) -> str | None:
    try:
        result = subprocess.run(["git", "-C", str(ROOT), *args], capture_output=True, text=True, check=False)
    except OSError:
        return None
    return result.stdout.strip() if result.returncode == 0 else None


def load_catalog_packages() -> dict[str, dict[str, Any]]:
    catalog = json.loads(PACKAGE_CATALOG.read_text(encoding="utf-8"))
    packages: dict[str, dict[str, Any]] = {}
    for item in catalog.get("packages", []):
        if isinstance(item, dict) and isinstance(item.get("id"), str):
            packages[item["id"]] = item
    return packages


def project_editor_version() -> str | None:
    version_file = CONSUMER_TEMPLATE / "ProjectSettings" / "ProjectVersion.txt"
    if not version_file.exists():
        return None
    for line in version_file.read_text(encoding="utf-8").splitlines():
        if line.startswith("m_EditorVersion:"):
            return line.split(":", 1)[1].strip()
    return None


def hub_editor_candidates() -> list[Path]:
    system = platform.system()
    patterns: list[str] = []
    if system == "Windows":
        program_files = os.environ.get("ProgramFiles") or os.environ.get("PROGRAMFILES") or ""
        if program_files:
            patterns.append(str(Path(program_files) / "Unity" / "Hub" / "Editor" / "*" / "Editor" / "Unity.exe"))
    elif system == "Darwin":
        patterns.append("/Applications/Unity/Hub/Editor/*/Unity.app/Contents/MacOS/Unity")
    else:
        patterns.append(str(Path.home() / "Unity" / "Hub" / "Editor" / "*" / "Editor" / "Unity"))
        patterns.append("/opt/unity/editors/*/Editor/Unity")
    candidates: list[Path] = []
    for pattern in patterns:
        candidates.extend(Path(match) for match in sorted(glob.glob(pattern)))
    return [candidate for candidate in candidates if candidate.is_file()]


def locate_unity(explicit: str | None) -> tuple[Path | None, str]:
    if explicit:
        path = Path(explicit).expanduser()
        return (path, "explicit") if path.exists() else (None, f"explicit path does not exist: {path}")
    environment = os.environ.get("UNITY_EDITOR")
    if environment:
        path = Path(environment).expanduser()
        return (path, "UNITY_EDITOR") if path.exists() else (None, f"UNITY_EDITOR does not exist: {path}")
    candidates = hub_editor_candidates()
    if not candidates:
        return None, "no Unity Hub editor found; pass --unity or set UNITY_EDITOR"
    wanted = project_editor_version() or ""
    for candidate in candidates:
        if wanted and wanted in candidate.as_posix():
            return candidate, f"Unity Hub install matching {wanted}"
    major = wanted.split("f")[0].rsplit(".", 1)[0] if wanted else ""
    for candidate in candidates:
        if major and major in candidate.as_posix():
            return candidate, f"Unity Hub install of the {major} stream (profile records {wanted})"
    return candidates[-1], f"newest Unity Hub install (profile records {wanted or 'no version'})"


def resolve_host_packages(selection: str, catalog: dict[str, dict[str, Any]]) -> tuple[str, list[str]]:
    if selection == "reference-system":
        return "reference-system", []
    if selection == "all":
        return "all-packages", sorted(catalog)
    requested = [item.strip() for item in selection.split(",") if item.strip()]
    unknown = [item for item in requested if item not in catalog]
    if unknown:
        raise SystemExit(f"unknown package id(s): {unknown}; known: {sorted(catalog)}")
    closure: set[str] = set()
    pending = list(requested)
    while pending:
        package_id = pending.pop()
        if package_id in closure:
            continue
        closure.add(package_id)
        manifest = json.loads((ROOT / catalog[package_id]["path"] / "package.json").read_text(encoding="utf-8"))
        for dependency in manifest.get("dependencies", {}):
            if dependency.startswith("com.lingkyn.") and dependency in catalog:
                pending.append(dependency)
    return "minimal:" + ",".join(requested), sorted(closure)


def materialize_reference_system(host: Path) -> dict[str, Any]:
    result = subprocess.run(
        [sys.executable, str(SCRIPTS / "materialize_reference_consumer.py"), "--output", str(host)],
        capture_output=True,
        text=True,
        check=False,
    )
    if result.returncode != 0:
        raise SystemExit(f"reference consumer materialization failed:\n{result.stdout}\n{result.stderr}")
    return json.loads(result.stdout)


def generate_host(host: Path, package_ids: list[str], catalog: dict[str, dict[str, Any]]) -> dict[str, Any]:
    host.mkdir(parents=True, exist_ok=False)
    (host / "Assets").mkdir()
    (host / "ProjectSettings").mkdir()
    shutil.copy2(CONSUMER_TEMPLATE / "ProjectSettings" / "ProjectVersion.txt", host / "ProjectSettings" / "ProjectVersion.txt")
    packages_root = host / "Packages"
    packages_root.mkdir()
    needs_xr = False
    for package_id in package_ids:
        source = ROOT / catalog[package_id]["path"]
        shutil.copytree(source, packages_root / package_id)
        manifest = json.loads((source / "package.json").read_text(encoding="utf-8"))
        if "com.unity.xr.interaction.toolkit" in manifest.get("dependencies", {}):
            needs_xr = True
    dependencies = {"com.unity.test-framework": TEST_FRAMEWORK_VERSION}
    if needs_xr:
        dependencies.update(XR_HOST_PINS)
    manifest_payload = {"dependencies": dict(sorted(dependencies.items())), "testables": list(package_ids)}
    (packages_root / "manifest.json").write_text(json.dumps(manifest_payload, indent=2) + "\n", encoding="utf-8")
    return {"embedded_packages": list(package_ids), "requested_dependencies": manifest_payload["dependencies"]}


def describe_host_packages(host: Path) -> list[dict[str, Any]]:
    described: list[dict[str, Any]] = []
    for manifest in sorted((host / "Packages").glob("com.lingkyn.*/package.json")):
        payload = json.loads(manifest.read_text(encoding="utf-8"))
        described.append({"id": payload.get("name"), "version": payload.get("version"), "package_json_sha256": sha256_of(manifest)})
    return described


def unity_command(unity: Path, host: Path, mode: str, assembly: str, result_path: Path, log_path: Path) -> list[str]:
    return [
        str(unity),
        "-batchmode",
        "-nographics",
        "-projectPath",
        str(host),
        "-runTests",
        "-testPlatform",
        mode,
        "-assemblyNames",
        assembly,
        "-testResults",
        str(result_path),
        "-logFile",
        str(log_path),
    ]


def launch_unity(command: list[str], timeout_seconds: int) -> int:
    """Run one Unity batchmode process. Tests replace this function with a fake."""

    completed = subprocess.run(command, check=False, timeout=timeout_seconds)
    return completed.returncode


def run_gates(args: argparse.Namespace) -> tuple[dict[str, Any], int]:
    catalog = load_catalog_packages()
    started_at = utc_now()
    output = Path(args.output).expanduser().resolve() if args.output else DEFAULT_OUTPUT_ROOT / time.strftime("%Y%m%dT%H%M%SZ", time.gmtime())
    output.mkdir(parents=True, exist_ok=True)

    host_root = Path(args.host_dir).expanduser().resolve() if args.host_dir else Path(tempfile.mkdtemp(prefix="xr-foundry-gates-"))
    host = host_root / "host"
    profile, package_ids = resolve_host_packages(args.host, catalog)
    if profile == "reference-system":
        materialization = materialize_reference_system(host)
    else:
        materialization = generate_host(host, package_ids, catalog)

    inventory = AUDIT.audit_project(host)
    assemblies = [item for item in inventory["assemblies"] if args.mode == "all" or item["mode"] == args.mode]
    if args.assembly:
        wanted = set(args.assembly)
        assemblies = [item for item in assemblies if item["name"] in wanted]

    unity, unity_reason = (None, "dry run") if args.dry_run else locate_unity(args.unity)
    receipt: dict[str, Any] = {
        "schema": RECEIPT_SCHEMA,
        "status": "planned" if args.dry_run else "running",
        "claim_ceiling": "editor_automated for the recorded tuple only; no player, runtime, controller, headset, comfort, or named-device claim",
        "started_at": started_at,
        "repository": {
            "commit_sha": git_output("rev-parse", "HEAD"),
            "worktree_dirty": bool(git_output("status", "--porcelain")),
            "branch": git_output("rev-parse", "--abbrev-ref", "HEAD"),
        },
        "host": {
            "profile": profile,
            "path": host.as_posix(),
            "packages": describe_host_packages(host),
            "manifest_sha256": sha256_of(host / "Packages" / "manifest.json"),
            "materialization": materialization,
        },
        "editor": {
            "path": unity.as_posix() if unity else None,
            "selection": unity_reason,
            "profile_version": project_editor_version(),
            "os": f"{platform.system()} {platform.release()}",
            "machine": platform.machine(),
        },
        "inventory_errors": inventory["errors"],
        "runs": [],
    }

    exit_code = 0
    if inventory["errors"]:
        receipt["status"] = "fail"
        exit_code = 1
    elif not assemblies:
        receipt["status"] = "fail"
        receipt["inventory_errors"].append("no test assembly matched the requested host, mode, and assembly filters")
        exit_code = 1
    elif args.dry_run:
        for item in assemblies:
            result_path = output / f"{item['name']}.{item['mode']}.xml"
            log_path = output / f"{item['name']}.{item['mode']}.log"
            receipt["runs"].append(
                {
                    "assembly": item["name"],
                    "mode": item["mode"],
                    "expected_cases": item["cases"],
                    "status": "planned",
                    "command": unity_command(Path("<unity>"), host, item["mode"], item["name"], result_path, log_path),
                }
            )
    elif unity is None:
        receipt["status"] = "fail"
        receipt["inventory_errors"].append(f"Unity Editor not found: {unity_reason}")
        exit_code = 1
    else:
        failures = 0
        for item in assemblies:
            result_path = output / f"{item['name']}.{item['mode']}.xml"
            log_path = output / f"{item['name']}.{item['mode']}.log"
            if result_path.exists():
                result_path.unlink()
            boundary = time.time()
            command = unity_command(unity, host, item["mode"], item["name"], result_path, log_path)
            try:
                returncode = launch_unity(command, args.timeout_minutes * 60)
            except subprocess.TimeoutExpired:
                returncode = -1
            verification = VERIFY.verify_unity_test_result(
                result_path, item["mode"], item["cases"], boundary, f"{item['name']}.dll"
            )
            passed = returncode == 0 and verification["status"] == "pass"
            failures += 0 if passed else 1
            receipt["runs"].append(
                {
                    "assembly": item["name"],
                    "mode": item["mode"],
                    "expected_cases": item["cases"],
                    "status": "pass" if passed else "fail",
                    "unity_exit_code": returncode,
                    "run_boundary_epoch": boundary,
                    "result_path": result_path.as_posix(),
                    "result_sha256": sha256_of(result_path) if result_path.exists() else None,
                    "log_path": log_path.as_posix(),
                    "verification": verification,
                }
            )
            print(f"[{'PASS' if passed else 'FAIL'}] {item['mode']:8} {item['name']} ({item['cases']} cases)", flush=True)
        lock = host / "Packages" / "packages-lock.json"
        if lock.exists():
            shutil.copy2(lock, output / "packages-lock.json")
            receipt["host"]["resolved_lock_sha256"] = sha256_of(lock)
        receipt["status"] = "pass" if failures == 0 else "fail"
        exit_code = 0 if failures == 0 else 1

    shutil.copy2(host / "Packages" / "manifest.json", output / "host-manifest.json")
    receipt["finished_at"] = utc_now()
    receipt["summary"] = {
        "assemblies": len(receipt["runs"]),
        "passed": sum(1 for run in receipt["runs"] if run["status"] == "pass"),
        "failed": sum(1 for run in receipt["runs"] if run["status"] == "fail"),
        "planned": sum(1 for run in receipt["runs"] if run["status"] == "planned"),
    }
    receipt["output"] = output.as_posix()
    (output / "receipt.json").write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    if not args.keep_host and not args.host_dir:
        shutil.rmtree(host_root, ignore_errors=True)
        receipt["host"]["path"] = None
    return receipt, exit_code


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Run every automated Unity gate of XR Foundry with one command.")
    parser.add_argument("--unity", help="Path to the Unity Editor executable. Defaults to UNITY_EDITOR or the Unity Hub install.")
    parser.add_argument(
        "--host",
        default="reference-system",
        help="Host to generate: reference-system (default), all, or a comma-separated list of package ids whose com.lingkyn dependencies are embedded automatically.",
    )
    parser.add_argument("--mode", choices=("all", *MODES), default="all")
    parser.add_argument("--assembly", action="append", help="Run only this test assembly (repeatable).")
    parser.add_argument("--output", help="Directory for results and the receipt. Defaults to .unity-gates/<timestamp> in the repository (ignored by Git).")
    parser.add_argument("--host-dir", help="Reuse or create the host project here instead of a temporary directory (must not exist inside the repository).")
    parser.add_argument("--keep-host", action="store_true", help="Keep the temporary host project after the run.")
    parser.add_argument("--timeout-minutes", type=int, default=60)
    parser.add_argument("--dry-run", action="store_true", help="Generate the host, audit the inventory, and write the plan without launching Unity.")
    parser.add_argument("--json", action="store_true", help="Print the receipt as JSON.")
    return parser


def main() -> int:
    args = build_parser().parse_args()
    receipt, exit_code = run_gates(args)
    if args.json:
        print(json.dumps(receipt, indent=2))
    else:
        summary = receipt["summary"]
        print(
            f"{receipt['status'].upper()}: {summary['passed']} passed, {summary['failed']} failed, "
            f"{summary['planned']} planned; receipt at {receipt['output']}/receipt.json"
        )
        for error in receipt["inventory_errors"]:
            print(f"  error: {error}")
    return exit_code


if __name__ == "__main__":
    raise SystemExit(main())
