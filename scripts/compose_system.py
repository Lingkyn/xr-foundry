from __future__ import annotations

import argparse
import importlib.util
import json
import os
import tempfile
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
VALIDATOR_PATH = ROOT / "scripts" / "validate_repository.py"
SPEC = importlib.util.spec_from_file_location("xr_foundry_repository_validator", VALIDATOR_PATH)
if SPEC is None or SPEC.loader is None:  # pragma: no cover - import failure is terminal
    raise RuntimeError(f"Cannot load repository validator from {VALIDATOR_PATH}")
VALIDATOR = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(VALIDATOR)


def report_payload(status: str, **values: Any) -> dict[str, Any]:
    return {
        "schema": "xr-foundry.composition_result.v1",
        "status": status,
        **values,
    }


def write_json_atomically(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary_name = tempfile.mkstemp(
        prefix=f".{path.name}.", suffix=".tmp", dir=path.parent
    )
    temporary_path = Path(temporary_name)
    try:
        with os.fdopen(descriptor, "w", encoding="utf-8") as stream:
            json.dump(payload, stream, indent=2)
            stream.write("\n")
            stream.flush()
            os.fsync(stream.fileno())
        temporary_path.replace(path)
    except Exception:
        temporary_path.unlink(missing_ok=True)
        raise


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Resolve an XR Foundry composition into a deterministic lock."
    )
    parser.add_argument(
        "composition",
        nargs="?",
        type=Path,
        default=VALIDATOR.REFERENCE_COMPOSITION_PATH,
    )
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument(
        "--check",
        action="store_true",
        help="fail when the committed lock differs from deterministic resolution",
    )
    mode.add_argument(
        "--write-lock",
        action="store_true",
        help="atomically replace the declared lock after successful resolution",
    )
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()

    composition_path = args.composition
    if not composition_path.is_absolute():
        composition_path = ROOT / composition_path
    composition_path = composition_path.resolve()
    try:
        composition_path.relative_to(ROOT)
    except ValueError:
        report = report_payload(
            "fail", errors=["composition manifest must stay inside the repository"]
        )
        print(json.dumps(report, indent=2))
        return 1
    if not composition_path.exists():
        report = report_payload(
            "fail", errors=[f"composition manifest is missing: {composition_path}"]
        )
        print(json.dumps(report, indent=2))
        return 1

    try:
        composition = VALIDATOR.load_json(composition_path)
    except (json.JSONDecodeError, UnicodeDecodeError) as error:
        report = report_payload("fail", errors=[f"invalid composition JSON: {error}"])
        print(json.dumps(report, indent=2))
        return 1
    manifest_schema_path, dispatch_errors = (
        VALIDATOR.composition_manifest_schema_path(composition)
    )
    schema_errors = list(dispatch_errors)
    if manifest_schema_path is not None:
        schema_errors.extend(
            VALIDATOR.validate_json_schema_instance(
                composition,
                ROOT / manifest_schema_path,
                "composition manifest",
            )
        )
    lock, build_errors = VALIDATOR.build_composition_lock(
        ROOT, composition_path, composition
    )
    errors = schema_errors + build_errors
    if lock is not None:
        lock_schema_path, lock_dispatch_errors = (
            VALIDATOR.composition_lock_schema_path(lock)
        )
        errors.extend(lock_dispatch_errors)
        if lock_schema_path is not None:
            errors.extend(
                VALIDATOR.validate_json_schema_instance(
                    lock,
                    ROOT / lock_schema_path,
                    "generated composition lock",
                )
            )
    if errors or lock is None:
        report = report_payload("fail", errors=errors)
        print(json.dumps(report, indent=2))
        return 1

    if composition.get("schema") == "xr-foundry.composition_manifest.v2":
        lock_path = composition_path.parent / "foundry.lock.json"
    else:
        lock_path = VALIDATOR.safe_repository_path(
            ROOT, composition.get("lock_path")
        )
    if lock_path is None:
        report = report_payload("fail", errors=["composition lock path is unsafe"])
        print(json.dumps(report, indent=2))
        return 1

    if args.check:
        if not lock_path.exists():
            report = report_payload(
                "fail",
                composition=composition.get("id"),
                lock_path=lock_path.relative_to(ROOT).as_posix(),
                errors=["composition lock is missing"],
            )
            print(json.dumps(report, indent=2))
            return 1
        try:
            actual_lock = VALIDATOR.load_json(lock_path)
        except (json.JSONDecodeError, UnicodeDecodeError) as error:
            report = report_payload(
                "fail",
                composition=composition.get("id"),
                lock_path=lock_path.relative_to(ROOT).as_posix(),
                errors=[f"existing composition lock is invalid JSON: {error}"],
            )
            print(json.dumps(report, indent=2))
            return 1
        if actual_lock != lock:
            report = report_payload(
                "fail",
                composition=composition.get("id"),
                lock_path=lock_path.relative_to(ROOT).as_posix(),
                errors=["composition lock is stale"],
            )
            print(json.dumps(report, indent=2))
            return 1
        report_values = {
            "composition": composition.get("id"),
            "lock_path": lock_path.relative_to(ROOT).as_posix(),
            "component_count": len(lock["resolution"]["components"]),
            "runtime_ready": lock["claims"]["runtime_ready"],
        }
        if "bindings_implemented" in lock["claims"]:
            report_values["bindings_implemented"] = lock["claims"][
                "bindings_implemented"
            ]
        report = report_payload("pass", **report_values)
    elif args.write_lock:
        write_json_atomically(lock_path, lock)
        report_values = {
            "action": "lock_written",
            "composition": composition.get("id"),
            "lock_path": lock_path.relative_to(ROOT).as_posix(),
            "component_count": len(lock["resolution"]["components"]),
            "runtime_ready": lock["claims"]["runtime_ready"],
        }
        if "bindings_implemented" in lock["claims"]:
            report_values["bindings_implemented"] = lock["claims"][
                "bindings_implemented"
            ]
        report = report_payload("pass", **report_values)
    else:
        report = lock

    if args.json or not args.write_lock:
        print(json.dumps(report, indent=2))
    else:
        print(
            f"Resolved {report['component_count']} components into "
            f"{report['lock_path']} (runtime_ready={str(report['runtime_ready']).lower()})."
        )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
