from __future__ import annotations

import argparse
import json
import math
import os
import stat
import xml.parsers.expat as expat
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Any


MODES = ("EditMode", "PlayMode")
COUNT_FIELDS = ("total", "passed", "failed", "inconclusive", "skipped")
MAX_XML_BYTES = 128 * 1024 * 1024
MAX_COUNT_DIGITS = 12
MAX_COUNT_VALUE = (10**MAX_COUNT_DIGITS) - 1


class UnsafeXmlDeclaration(ValueError):
    pass


def _expanded_name(name: str) -> str:
    if "}" not in name:
        return name
    namespace, local_name = name.split("}", 1)
    return f"{{{namespace}}}{local_name}"


def _parse_xml(xml_bytes: bytes) -> ET.Element:
    """Build an ElementTree while rejecting DTD/entity declarations by event."""

    builder = ET.TreeBuilder()
    parser = expat.ParserCreate(namespace_separator="}")
    parser.buffer_text = True
    parser.StartElementHandler = lambda name, attributes: builder.start(
        _expanded_name(name),
        {_expanded_name(key): value for key, value in attributes.items()},
    )
    parser.EndElementHandler = lambda name: builder.end(_expanded_name(name))
    parser.CharacterDataHandler = builder.data

    def reject_declaration(*_args: object) -> None:
        raise UnsafeXmlDeclaration("DTD and entity declarations are forbidden")

    parser.StartDoctypeDeclHandler = reject_declaration
    parser.EntityDeclHandler = reject_declaration
    parser.Parse(xml_bytes, True)
    return builder.close()


def _local_name(element: ET.Element) -> str:
    return element.tag.rsplit("}", 1)[-1]


def _direct_children(element: ET.Element, tag: str) -> list[ET.Element]:
    return [child for child in element if _local_name(child) == tag]


def _properties(element: ET.Element) -> dict[str, list[str]]:
    properties: dict[str, list[str]] = {}
    for container in _direct_children(element, "properties"):
        for item in _direct_children(container, "property"):
            name = item.attrib.get("name")
            value = item.attrib.get("value")
            if isinstance(name, str) and isinstance(value, str):
                properties.setdefault(name, []).append(value)
    return properties


def _required_count(
    element: ET.Element,
    field: str,
    label: str,
    errors: list[str],
) -> int | None:
    value = element.attrib.get(field)
    if value is None:
        errors.append(f"{label} is missing numeric {field}")
        return None
    if not value.isascii() or not value.isdigit():
        errors.append(f"{label} has malformed numeric {field}: {value!r}")
        return None
    if len(value) > MAX_COUNT_DIGITS:
        errors.append(
            f"{label} numeric {field} exceeds the accepted {MAX_COUNT_DIGITS}-digit bound"
        )
        return None
    try:
        return int(value)
    except ValueError:
        errors.append(f"{label} has malformed numeric {field}: {value!r}")
        return None


def _required_counts(
    element: ET.Element,
    label: str,
    errors: list[str],
) -> dict[str, int | None]:
    return {
        field: _required_count(element, field, label, errors)
        for field in COUNT_FIELDS
    }


def _validate_passing_counts(
    counts: dict[str, int | None],
    label: str,
    errors: list[str],
) -> None:
    total = counts["total"]
    passed = counts["passed"]
    if total is not None and total < 1:
        errors.append(f"{label} must contain at least one executed test")
    if total is not None and passed is not None and passed != total:
        errors.append(f"{label} passed must equal total")
    for field in ("failed", "inconclusive", "skipped"):
        value = counts[field]
        if value is not None and value != 0:
            errors.append(f"{label} {field} must equal zero, got {value}")


def _default_assembly(mode: str) -> str:
    return f"XRFoundry.ReferenceSystem.{mode}.Tests.dll"


def _valid_assembly_name(value: object) -> bool:
    return (
        isinstance(value, str)
        and bool(value)
        and value.endswith(".dll")
        and "/" not in value
        and "\\" not in value
        and not any(ord(character) < 32 or ord(character) == 127 for character in value)
    )


def _positive_integer(value: str) -> int:
    if not value.isascii() or not value.isdigit():
        raise argparse.ArgumentTypeError("must be a positive integer")
    if len(value) > MAX_COUNT_DIGITS:
        raise argparse.ArgumentTypeError(
            f"must not exceed the {MAX_COUNT_DIGITS}-digit count bound"
        )
    parsed = int(value)
    if parsed < 1:
        raise argparse.ArgumentTypeError("must be a positive integer")
    return parsed


def _finite_nonnegative_epoch(value: str) -> float:
    try:
        parsed = float(value)
    except (OverflowError, ValueError) as error:
        raise argparse.ArgumentTypeError(
            "must be a finite nonnegative Unix epoch"
        ) from error
    if not math.isfinite(parsed) or parsed < 0:
        raise argparse.ArgumentTypeError("must be a finite nonnegative Unix epoch")
    return parsed


def verify_unity_test_result(
    result: Path,
    mode: str,
    expected_total: int,
    not_before_epoch: float,
    expected_assembly: str | None = None,
) -> dict[str, Any]:
    """Validate one filtered Unity NUnit Assembly result, failing closed."""

    errors: list[str] = []
    try:
        result_path = result.resolve()
    except (OSError, RuntimeError, ValueError) as error:
        result_path = result.absolute()
        errors.append(f"result path cannot be resolved: {error}")
    if mode not in MODES:
        errors.append(f"mode must be one of {MODES}, got {mode!r}")
    if expected_assembly is None:
        expected_assembly = _default_assembly(mode)
    if not _valid_assembly_name(expected_assembly):
        errors.append(
            "expected assembly must be one non-path .dll assembly name"
        )
        normalized_expected_assembly: str | None = None
    else:
        normalized_expected_assembly = expected_assembly
    valid_expected_total = (
        isinstance(expected_total, int)
        and not isinstance(expected_total, bool)
        and 0 < expected_total <= MAX_COUNT_VALUE
    )
    if not valid_expected_total:
        errors.append(
            "expected total must be a positive integer within the accepted "
            f"{MAX_COUNT_DIGITS}-digit bound"
        )
    normalized_not_before: float | None = None
    if isinstance(not_before_epoch, (int, float)) and not isinstance(
        not_before_epoch, bool
    ):
        try:
            candidate_epoch = float(not_before_epoch)
        except (OverflowError, ValueError):
            candidate_epoch = math.nan
        if math.isfinite(candidate_epoch) and candidate_epoch >= 0:
            normalized_not_before = candidate_epoch
    valid_not_before = normalized_not_before is not None
    if not valid_not_before:
        errors.append("not-before epoch must be finite and nonnegative")

    root: ET.Element | None = None
    result_mtime: float | None = None
    try:
        open_flags = os.O_RDONLY | getattr(os, "O_NONBLOCK", 0) | getattr(
            os, "O_CLOEXEC", 0
        )
        open_flags |= getattr(os, "O_NOFOLLOW", 0)
        descriptor = os.open(result_path, open_flags)
        with os.fdopen(descriptor, "rb") as stream:
            result_stat = os.fstat(stream.fileno())
            if not stat.S_ISREG(result_stat.st_mode):
                errors.append("result path must identify a regular file")
            declared_size = result_stat.st_size
            result_mtime = result_stat.st_mtime_ns / 1_000_000_000
            if valid_not_before and result_mtime < normalized_not_before:
                errors.append(
                    "result file predates the required run boundary: "
                    f"mtime={result_mtime}, not_before_epoch={normalized_not_before}"
                )
            if declared_size < 1 or declared_size > MAX_XML_BYTES:
                errors.append("result XML size is outside the accepted evidence bound")
            else:
                xml_bytes = stream.read(MAX_XML_BYTES + 1)
                final_stat = os.fstat(stream.fileno())
                stable_identity = (
                    result_stat.st_dev,
                    result_stat.st_ino,
                    result_stat.st_size,
                    result_stat.st_mtime_ns,
                ) == (
                    final_stat.st_dev,
                    final_stat.st_ino,
                    final_stat.st_size,
                    final_stat.st_mtime_ns,
                )
                if not stable_identity:
                    errors.append("result file changed while it was being read")
                if not xml_bytes or len(xml_bytes) > MAX_XML_BYTES:
                    errors.append("result XML size is outside the accepted evidence bound")
                else:
                    root = _parse_xml(xml_bytes)
    except FileNotFoundError:
        errors.append("result file is missing")
    except (ET.ParseError, expat.ExpatError, OSError, ValueError) as error:
        errors.append(f"result XML is invalid: {error}")

    root_counts: dict[str, int | None] = {field: None for field in COUNT_FIELDS}
    root_testcasecount: int | None = None
    project_testcasecount: int | None = None
    assembly_name: str | None = None
    actual_test_cases: int | None = None

    if root is not None:
        if _local_name(root) != "test-run":
            errors.append("result XML root must be test-run")
        else:
            root_counts = _required_counts(root, "test-run", errors)
            root_testcasecount = _required_count(
                root, "testcasecount", "test-run", errors
            )
            _validate_passing_counts(root_counts, "test-run", errors)
            if valid_expected_total and root_counts["total"] != expected_total:
                errors.append(
                    "test-run total must equal expected total "
                    f"{expected_total}, got {root_counts['total']}"
                )
            if valid_expected_total and root_testcasecount != expected_total:
                errors.append(
                    "test-run testcasecount must equal expected total "
                    f"{expected_total}, got {root_testcasecount}"
                )
            if root.attrib.get("result") != "Passed":
                errors.append(
                    f"test-run result must be Passed, got {root.attrib.get('result')!r}"
                )

            project_suites = [
                suite
                for suite in _direct_children(root, "test-suite")
                if suite.attrib.get("type") == "TestSuite"
            ]
            if len(project_suites) != 1:
                errors.append("test-run must contain exactly one project TestSuite")
                project_suite = None
            else:
                project_suite = project_suites[0]
                project_counts = _required_counts(
                    project_suite, "project TestSuite", errors
                )
                project_testcasecount = _required_count(
                    project_suite,
                    "testcasecount",
                    "project TestSuite",
                    errors,
                )
                _validate_passing_counts(
                    project_counts, "project TestSuite", errors
                )
                if project_suite.attrib.get("result") != "Passed":
                    errors.append("project TestSuite result must be Passed")
                if project_counts != root_counts:
                    errors.append(
                        "project TestSuite counts must exactly equal test-run counts"
                    )
                if (
                    project_testcasecount is not None
                    and project_counts["total"] is not None
                    and project_testcasecount < project_counts["total"]
                ):
                    errors.append(
                        "project TestSuite testcasecount must be at least its executed total"
                    )
                if _properties(project_suite).get("platform") != [mode]:
                    errors.append(
                        f"project TestSuite platform property must equal {mode}"
                    )

            all_assemblies = [
                suite
                for suite in root.iter()
                if _local_name(suite) == "test-suite"
                and suite.attrib.get("type") == "Assembly"
            ]
            if len(all_assemblies) != 1:
                errors.append("result must contain exactly one Assembly test-suite")
                assembly_suite = None
            else:
                assembly_suite = all_assemblies[0]
                if (
                    project_suite is not None
                    and assembly_suite
                    not in [
                        suite
                        for suite in _direct_children(project_suite, "test-suite")
                        if suite.attrib.get("type") == "Assembly"
                    ]
                ):
                    errors.append("Assembly test-suite must be a direct project child")

            if assembly_suite is not None:
                assembly_name = assembly_suite.attrib.get("name")
                if assembly_name != normalized_expected_assembly:
                    errors.append(
                        "Assembly test-suite name must exactly equal "
                        f"{normalized_expected_assembly!r}, got {assembly_name!r}"
                    )
                if _properties(assembly_suite).get("platform") != [mode]:
                    errors.append(
                        f"Assembly test-suite platform property must equal {mode}"
                    )
                assembly_counts = _required_counts(
                    assembly_suite, "Assembly test-suite", errors
                )
                assembly_testcasecount = _required_count(
                    assembly_suite,
                    "testcasecount",
                    "Assembly test-suite",
                    errors,
                )
                _validate_passing_counts(
                    assembly_counts, "Assembly test-suite", errors
                )
                if valid_expected_total and assembly_counts["total"] != expected_total:
                    errors.append(
                        "Assembly total must equal expected total "
                        f"{expected_total}, got {assembly_counts['total']}"
                    )
                if (
                    valid_expected_total
                    and assembly_testcasecount != expected_total
                ):
                    errors.append(
                        "Assembly testcasecount must equal expected total "
                        f"{expected_total}, got {assembly_testcasecount}"
                    )
                if assembly_suite.attrib.get("result") != "Passed":
                    errors.append("Assembly test-suite result must be Passed")
                if assembly_counts != root_counts:
                    errors.append(
                        "Assembly test-suite counts must exactly equal test-run counts"
                    )

                assembly_cases = [
                    case
                    for case in assembly_suite.iter()
                    if _local_name(case) == "test-case"
                ]
                root_cases = [
                    case for case in root.iter() if _local_name(case) == "test-case"
                ]
                actual_test_cases = len(assembly_cases)
                if valid_expected_total and actual_test_cases != expected_total:
                    errors.append(
                        "actual test-case count must equal expected total "
                        f"{expected_total}, got {actual_test_cases}"
                    )
                if actual_test_cases < 1:
                    errors.append("Assembly test-suite must contain actual test-case elements")
                if len(root_cases) != actual_test_cases:
                    errors.append(
                        "every test-case in the result must belong to the target Assembly"
                    )
                case_fullnames = [case.attrib.get("fullname") for case in assembly_cases]
                if any(not isinstance(name, str) or not name for name in case_fullnames):
                    errors.append("every target Assembly test-case must have a fullname")
                elif len(case_fullnames) != len(set(case_fullnames)):
                    errors.append("target Assembly test-case fullnames must be unique")
                if (
                    root_testcasecount is not None
                    and root_testcasecount != actual_test_cases
                ):
                    errors.append("test-run testcasecount must equal actual test-case count")
                if (
                    assembly_testcasecount is not None
                    and assembly_testcasecount != actual_test_cases
                ):
                    errors.append(
                        "Assembly testcasecount must equal actual test-case count"
                    )
                if (
                    root_counts["total"] is not None
                    and root_counts["total"] != actual_test_cases
                ):
                    errors.append("test-run total must equal actual test-case count")
                if any(case.attrib.get("result") != "Passed" for case in assembly_cases):
                    errors.append("every target Assembly test-case must be Passed")

    payload: dict[str, Any] = {
        "schema": "xr-foundry.unity_test_result_check.v1",
        "status": "pass" if not errors else "fail",
        "mode": mode,
        "result": result_path.as_posix(),
        "result_mtime": result_mtime,
        "not_before_epoch": normalized_not_before,
        "expected_total": expected_total if valid_expected_total else None,
        "expected_assembly": normalized_expected_assembly,
        "assembly": assembly_name,
        "project_testcasecount": project_testcasecount,
        "total": root_counts["total"],
        "passed": root_counts["passed"],
        "failed": root_counts["failed"],
        "inconclusive": root_counts["inconclusive"],
        "skipped": root_counts["skipped"],
        "actual_test_cases": actual_test_cases,
        "errors": errors,
    }
    return payload


def main() -> int:
    parser = argparse.ArgumentParser(
        description=(
            "Reject Unity NUnit results that do not prove the requested single "
            "test Assembly passed completely."
        )
    )
    parser.add_argument("result", type=Path)
    parser.add_argument("--mode", choices=MODES, required=True)
    parser.add_argument(
        "--expected-total",
        type=_positive_integer,
        required=True,
        help="Exact number of executed test-case elements required in the result.",
    )
    parser.add_argument(
        "--not-before-epoch",
        type=_finite_nonnegative_epoch,
        required=True,
        help="Reject a result whose filesystem mtime predates this Unix epoch.",
    )
    parser.add_argument(
        "--assembly",
        help=(
            "Exact .dll Assembly name expected in the result; defaults to the "
            "reference-system Assembly for --mode."
        ),
    )
    args = parser.parse_args()

    payload = verify_unity_test_result(
        args.result,
        args.mode,
        args.expected_total,
        args.not_before_epoch,
        args.assembly,
    )
    print(json.dumps(payload, indent=2))
    return 0 if payload["status"] == "pass" else 1


if __name__ == "__main__":
    raise SystemExit(main())
