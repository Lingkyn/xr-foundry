from __future__ import annotations

import argparse
import json
import xml.etree.ElementTree as ET
from pathlib import Path


def integer_attribute(root: ET.Element, *names: str) -> int | None:
    for name in names:
        value = root.attrib.get(name)
        if value is not None:
            try:
                return int(value)
            except ValueError:
                return None
    return None


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Reject missing, empty, failed, or malformed Unity NUnit results."
    )
    parser.add_argument("result", type=Path)
    parser.add_argument("--mode", choices=("EditMode", "PlayMode"), required=True)
    args = parser.parse_args()

    result_path = args.result.resolve()
    errors: list[str] = []
    if not result_path.is_file():
        errors.append("result file is missing")
        root = None
    else:
        try:
            root = ET.parse(result_path).getroot()
        except (ET.ParseError, OSError) as error:
            errors.append(f"result XML is invalid: {error}")
            root = None

    total = failed = skipped = None
    if root is not None:
        total = integer_attribute(root, "testcasecount", "total")
        failed = integer_attribute(root, "failed")
        skipped = integer_attribute(root, "skipped")
        if total is None or total <= 0:
            errors.append("result must contain at least one executed test")
        if failed is None:
            errors.append("result is missing a numeric failed count")
        elif failed != 0:
            errors.append(f"result contains {failed} failed tests")
        result_value = root.attrib.get("result")
        if result_value not in ("Passed", "Success"):
            errors.append(f"suite result is not passing: {result_value!r}")

    payload = {
        "schema": "xr-foundry.unity_test_result_check.v1",
        "status": "pass" if not errors else "fail",
        "mode": args.mode,
        "result": result_path.as_posix(),
        "total": total,
        "failed": failed,
        "skipped": skipped,
        "errors": errors,
    }
    print(json.dumps(payload, indent=2))
    return 0 if not errors else 1


if __name__ == "__main__":
    raise SystemExit(main())

