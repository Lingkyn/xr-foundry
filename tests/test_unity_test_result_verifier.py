from __future__ import annotations

import importlib.util
import json
import math
import os
import subprocess
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path
from unittest import mock


ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "scripts" / "verify_unity_test_results.py"
SPEC = importlib.util.spec_from_file_location("verify_unity_test_results", SCRIPT)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


def result_tree(
    mode: str,
    results: tuple[str, ...] = ("Passed", "Passed"),
    assembly_name: str | None = None,
) -> ET.Element:
    counts = {
        "total": len(results),
        "passed": results.count("Passed"),
        "failed": results.count("Failed"),
        "inconclusive": results.count("Inconclusive"),
        "skipped": results.count("Skipped"),
    }
    attributes = {name: str(value) for name, value in counts.items()}
    attributes.update(
        {
            "testcasecount": str(len(results)),
            "result": "Passed" if counts["passed"] == len(results) else "Failed",
        }
    )
    root = ET.Element("test-run", attributes)
    project = ET.SubElement(
        root,
        "test-suite",
        {
            "type": "TestSuite",
            "name": "reference-consumer",
            "testcasecount": str(len(results)),
            "result": attributes["result"],
            **{name: str(value) for name, value in counts.items()},
        },
    )
    project_properties = ET.SubElement(project, "properties")
    ET.SubElement(
        project_properties, "property", {"name": "platform", "value": mode}
    )
    assembly = ET.SubElement(
        project,
        "test-suite",
        {
            "type": "Assembly",
            "name": assembly_name
            or f"XRFoundry.ReferenceSystem.{mode}.Tests.dll",
            "testcasecount": str(len(results)),
            "result": attributes["result"],
            **{name: str(value) for name, value in counts.items()},
        },
    )
    assembly_properties = ET.SubElement(assembly, "properties")
    ET.SubElement(
        assembly_properties, "property", {"name": "platform", "value": mode}
    )
    fixture = ET.SubElement(
        assembly, "test-suite", {"type": "TestFixture", "name": "Fixture"}
    )
    for index, result in enumerate(results):
        ET.SubElement(
            fixture,
            "test-case",
            {
                "name": f"Case{index}",
                "fullname": f"Fixture.Case{index}",
                "result": result,
            },
        )
    return root


def first(element: ET.Element, xpath: str) -> ET.Element:
    result = element.find(xpath)
    if result is None:
        raise AssertionError(f"fixture element missing: {xpath}")
    return result


class UnityTestResultVerifierTests(unittest.TestCase):
    def verify_tree(
        self,
        root: ET.Element,
        mode: str,
        expected_assembly: str | None = None,
        expected_total: int | None = None,
        not_before_epoch: float = 0.0,
    ) -> dict:
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "results.xml"
            ET.ElementTree(root).write(path, encoding="utf-8", xml_declaration=True)
            if expected_total is None:
                expected_total = max(1, len(root.findall(".//test-case")))
            return MODULE.verify_unity_test_result(
                path,
                mode,
                expected_total,
                not_before_epoch,
                expected_assembly,
            )

    def assert_failure_contains(self, payload: dict, text: str) -> None:
        self.assertEqual("fail", payload["status"])
        self.assertTrue(
            any(text in error for error in payload["errors"]), payload["errors"]
        )

    def test_accepts_complete_editmode_result(self) -> None:
        payload = self.verify_tree(result_tree("EditMode"), "EditMode")

        self.assertEqual("pass", payload["status"])
        self.assertEqual(2, payload["actual_test_cases"])
        self.assertEqual(
            "XRFoundry.ReferenceSystem.EditMode.Tests.dll", payload["assembly"]
        )

    def test_accepts_complete_playmode_result(self) -> None:
        payload = self.verify_tree(result_tree("PlayMode"), "PlayMode")

        self.assertEqual("pass", payload["status"])
        self.assertEqual(2, payload["passed"])

    def test_accepts_project_discovery_count_above_expected_executed_total(self) -> None:
        root = result_tree("EditMode")
        first(root, "./test-suite").set("testcasecount", "173")

        payload = self.verify_tree(root, "EditMode", expected_total=2)

        self.assertEqual("pass", payload["status"], payload["errors"])
        self.assertEqual(173, payload["project_testcasecount"])

    def test_accepts_result_at_not_before_boundary(self) -> None:
        root = result_tree("EditMode")
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "results.xml"
            ET.ElementTree(root).write(path, encoding="utf-8", xml_declaration=True)
            boundary = path.stat().st_mtime_ns / 1_000_000_000

            payload = MODULE.verify_unity_test_result(
                path, "EditMode", 2, boundary
            )

        self.assertEqual("pass", payload["status"], payload["errors"])

    def test_rejects_expected_total_mismatch_at_every_executed_layer(self) -> None:
        payload = self.verify_tree(
            result_tree("EditMode"), "EditMode", expected_total=3
        )

        for expected_error in (
            "test-run total must equal expected total",
            "test-run testcasecount must equal expected total",
            "Assembly total must equal expected total",
            "Assembly testcasecount must equal expected total",
            "actual test-case count must equal expected total",
        ):
            self.assert_failure_contains(payload, expected_error)

    def test_rejects_nonpositive_or_noninteger_expected_total(self) -> None:
        for value in (0, -1, False, 1.5, 10**13):
            with self.subTest(value=value):
                payload = self.verify_tree(
                    result_tree("EditMode"),
                    "EditMode",
                    expected_total=value,
                )

                self.assert_failure_contains(payload, "expected total must be a positive")
                json.dumps(payload, allow_nan=False)

    def test_rejects_stale_result_mtime(self) -> None:
        root = result_tree("EditMode")
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "results.xml"
            ET.ElementTree(root).write(path, encoding="utf-8", xml_declaration=True)
            boundary = (path.stat().st_mtime_ns / 1_000_000_000) + 1.0

            payload = MODULE.verify_unity_test_result(
                path, "EditMode", 2, boundary
            )

        self.assert_failure_contains(payload, "predates the required run boundary")

    def test_freshness_uses_same_open_file_descriptor_as_xml_read(self) -> None:
        root = result_tree("EditMode")
        boundary = 1_700_000_000.0
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "results.xml"
            replacement = Path(directory) / "stale.xml"
            ET.ElementTree(root).write(path, encoding="utf-8", xml_declaration=True)
            ET.ElementTree(root).write(
                replacement, encoding="utf-8", xml_declaration=True
            )
            os.utime(path, (boundary + 10, boundary + 10))
            os.utime(replacement, (boundary - 10, boundary - 10))
            original_open = MODULE.os.open

            def swap_then_open(target: Path, flags: int) -> int:
                os.replace(replacement, path)
                return original_open(target, flags)

            with mock.patch.object(MODULE.os, "open", side_effect=swap_then_open):
                payload = MODULE.verify_unity_test_result(
                    path, "EditMode", 2, boundary
                )

        self.assert_failure_contains(payload, "predates the required run boundary")

    def test_rejects_nonfinite_negative_or_nonnumeric_run_boundary(self) -> None:
        for value in (math.nan, math.inf, -1.0, True, "0"):
            with self.subTest(value=value):
                payload = self.verify_tree(
                    result_tree("EditMode"),
                    "EditMode",
                    expected_total=2,
                    not_before_epoch=value,
                )

                self.assert_failure_contains(payload, "epoch must be finite and nonnegative")
                json.dumps(payload, allow_nan=False)

    def test_accepts_exact_custom_assembly(self) -> None:
        assembly = "Lingkyn.Interaction.Core.Editor.Tests.dll"
        payload = self.verify_tree(
            result_tree("EditMode", assembly_name=assembly),
            "EditMode",
            assembly,
        )

        self.assertEqual("pass", payload["status"])
        self.assertEqual(assembly, payload["expected_assembly"])

    def test_rejects_custom_assembly_name_mismatch(self) -> None:
        payload = self.verify_tree(
            result_tree(
                "EditMode",
                assembly_name="Lingkyn.Persistence.Unity.Editor.Tests.dll",
            ),
            "EditMode",
            "Lingkyn.Interaction.Core.Editor.Tests.dll",
        )

        self.assert_failure_contains(payload, "Assembly test-suite name must exactly equal")

    def test_rejects_invalid_expected_assembly_name(self) -> None:
        payload = self.verify_tree(
            result_tree("EditMode"), "EditMode", "../Unrelated.Tests.dll"
        )

        self.assert_failure_contains(payload, "one non-path .dll assembly name")

    def test_rejects_nonstring_expected_assembly_without_raising(self) -> None:
        payload = self.verify_tree(
            result_tree("EditMode"), "EditMode", expected_assembly=42
        )

        self.assert_failure_contains(payload, "one non-path .dll assembly name")
        json.dumps(payload, allow_nan=False)

    def test_rejects_wrong_platform_at_project_or_assembly(self) -> None:
        for xpath in (
            "./test-suite/properties/property",
            "./test-suite/test-suite/properties/property",
        ):
            with self.subTest(xpath=xpath):
                root = result_tree("EditMode")
                first(root, xpath).set("value", "PlayMode")

                payload = self.verify_tree(root, "EditMode")

                self.assert_failure_contains(payload, "platform property must equal EditMode")

    def test_rejects_wrong_assembly(self) -> None:
        root = result_tree("EditMode")
        first(root, "./test-suite/test-suite").set("name", "Unrelated.Tests.dll")

        payload = self.verify_tree(root, "EditMode")

        self.assert_failure_contains(payload, "Assembly test-suite name must exactly equal")

    def test_rejects_extra_assembly_even_when_it_passes(self) -> None:
        root = result_tree("EditMode")
        project = first(root, "./test-suite")
        ET.SubElement(
            project,
            "test-suite",
            {
                "type": "Assembly",
                "name": "Unrelated.Tests.dll",
                "testcasecount": "1",
                "result": "Passed",
                "total": "1",
                "passed": "1",
                "failed": "0",
                "inconclusive": "0",
                "skipped": "0",
            },
        )

        payload = self.verify_tree(root, "EditMode")

        self.assert_failure_contains(payload, "exactly one Assembly")

    def test_rejects_zero_tests(self) -> None:
        payload = self.verify_tree(result_tree("EditMode", ()), "EditMode")

        self.assert_failure_contains(payload, "at least one executed test")
        self.assert_failure_contains(payload, "actual test-case elements")

    def test_rejects_skipped_test(self) -> None:
        payload = self.verify_tree(
            result_tree("EditMode", ("Passed", "Skipped")), "EditMode"
        )

        self.assert_failure_contains(payload, "skipped must equal zero")
        self.assert_failure_contains(payload, "every target Assembly test-case must be Passed")

    def test_rejects_inconclusive_test(self) -> None:
        payload = self.verify_tree(
            result_tree("EditMode", ("Passed", "Inconclusive")), "EditMode"
        )

        self.assert_failure_contains(payload, "inconclusive must equal zero")

    def test_rejects_failed_test(self) -> None:
        payload = self.verify_tree(
            result_tree("EditMode", ("Passed", "Failed")), "EditMode"
        )

        self.assert_failure_contains(payload, "failed must equal zero")

    def test_rejects_nonpassing_case_when_aggregates_claim_pass(self) -> None:
        root = result_tree("EditMode")
        first(root, ".//test-case").set("result", "Failed")

        payload = self.verify_tree(root, "EditMode")

        self.assert_failure_contains(payload, "every target Assembly test-case must be Passed")

    def test_rejects_missing_or_duplicate_test_case_fullnames(self) -> None:
        for mutation in ("missing", "duplicate"):
            with self.subTest(mutation=mutation):
                root = result_tree("EditMode")
                cases = root.findall(".//test-case")
                if mutation == "missing":
                    del cases[0].attrib["fullname"]
                    expected = "must have a fullname"
                else:
                    cases[1].set("fullname", cases[0].attrib["fullname"])
                    expected = "fullnames must be unique"

                payload = self.verify_tree(root, "EditMode", expected_total=2)

                self.assert_failure_contains(payload, expected)

    def test_rejects_root_to_assembly_aggregate_mismatch(self) -> None:
        root = result_tree("EditMode")
        root.set("total", "3")
        root.set("passed", "3")

        payload = self.verify_tree(root, "EditMode")

        self.assert_failure_contains(
            payload, "Assembly test-suite counts must exactly equal test-run counts"
        )

    def test_rejects_declared_to_actual_test_case_mismatch(self) -> None:
        root = result_tree("EditMode")
        assembly = first(root, "./test-suite/test-suite")
        fixture = first(assembly, "./test-suite")
        fixture.remove(first(fixture, "./test-case"))

        payload = self.verify_tree(root, "EditMode")

        self.assert_failure_contains(payload, "test-run testcasecount must equal actual")
        self.assert_failure_contains(payload, "Assembly testcasecount must equal actual")

    def test_rejects_malformed_numeric_counts_at_root_or_assembly(self) -> None:
        for xpath in (".", "./test-suite/test-suite"):
            for field in (*MODULE.COUNT_FIELDS, "testcasecount"):
                with self.subTest(xpath=xpath, field=field):
                    root = result_tree("EditMode")
                    first(root, xpath).set(field, "two")

                    payload = self.verify_tree(root, "EditMode")

                    self.assert_failure_contains(payload, f"malformed numeric {field}")

    def test_rejects_missing_numeric_counts_at_root_or_assembly(self) -> None:
        for xpath in (".", "./test-suite/test-suite"):
            for field in (*MODULE.COUNT_FIELDS, "testcasecount"):
                with self.subTest(xpath=xpath, field=field):
                    root = result_tree("EditMode")
                    del first(root, xpath).attrib[field]

                    payload = self.verify_tree(root, "EditMode")

                    self.assert_failure_contains(payload, f"missing numeric {field}")

    def test_rejects_missing_numeric_project_count(self) -> None:
        root = result_tree("EditMode")
        del first(root, "./test-suite").attrib["passed"]

        payload = self.verify_tree(root, "EditMode")

        self.assert_failure_contains(payload, "project TestSuite is missing numeric passed")

    def test_rejects_missing_malformed_or_impossible_project_testcasecount(self) -> None:
        for value in (None, "two", "1"):
            with self.subTest(value=value):
                root = result_tree("EditMode")
                project = first(root, "./test-suite")
                if value is None:
                    del project.attrib["testcasecount"]
                    expected = "missing numeric testcasecount"
                else:
                    project.set("testcasecount", value)
                    expected = (
                        "malformed numeric testcasecount"
                        if value == "two"
                        else "testcasecount must be at least its executed total"
                    )

                payload = self.verify_tree(root, "EditMode")

                self.assert_failure_contains(payload, expected)

    def test_rejects_malformed_xml_and_non_test_run_root(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "results.xml"
            path.write_text("<test-run>", encoding="utf-8")
            malformed = MODULE.verify_unity_test_result(path, "EditMode", 2, 0.0)
            path.write_text("<test-suite />", encoding="utf-8")
            wrong_root = MODULE.verify_unity_test_result(path, "EditMode", 2, 0.0)

        self.assert_failure_contains(malformed, "result XML is invalid")
        self.assert_failure_contains(wrong_root, "root must be test-run")

    def test_rejects_utf16_dtd_and_entity_declarations(self) -> None:
        xml = """<?xml version="1.0" encoding="UTF-16"?>
<!DOCTYPE test-run [
  <!ENTITY count "1">
  <!ENTITY passed "Passed">
]>
<test-run testcasecount="&count;" result="&passed;" total="&count;"
          passed="&count;" failed="0" inconclusive="0" skipped="0" />
"""
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "results.xml"
            path.write_bytes(xml.encode("utf-16"))

            payload = MODULE.verify_unity_test_result(path, "EditMode", 2, 0.0)

        self.assert_failure_contains(payload, "DTD and entity declarations are forbidden")

    def test_accepts_doctype_and_entity_literals_inside_case_cdata(self) -> None:
        root = result_tree("EditMode")
        case = first(root, ".//test-case")
        output = ET.SubElement(case, "output")
        output.text = "CDATA_SENTINEL"
        xml = ET.tostring(root, encoding="unicode").replace(
            "CDATA_SENTINEL",
            "<![CDATA[diagnostic: <!DOCTYPE html> and <!ENTITY harmless>]]>",
        )
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "results.xml"
            path.write_text(xml, encoding="utf-8")

            payload = MODULE.verify_unity_test_result(path, "EditMode", 2, 0.0)

        self.assertEqual("pass", payload["status"], payload["errors"])

    def test_rejects_oversized_numeric_without_raising(self) -> None:
        root = result_tree("EditMode")
        root.set("total", "9" * 5000)

        payload = self.verify_tree(root, "EditMode")

        self.assert_failure_contains(payload, "numeric total exceeds the accepted")

    def test_rejects_oversized_sparse_xml_before_parsing(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "oversized.xml"
            with path.open("wb") as stream:
                stream.truncate(MODULE.MAX_XML_BYTES + 1)

            payload = MODULE.verify_unity_test_result(path, "EditMode", 2, 0.0)

        self.assert_failure_contains(payload, "XML size is outside")

    def test_cli_emits_json_and_uses_exit_status(self) -> None:
        assembly = "Lingkyn.Persistence.Unity.Editor.Tests.dll"
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "results.xml"
            ET.ElementTree(result_tree("EditMode", assembly_name=assembly)).write(
                path, encoding="utf-8", xml_declaration=True
            )
            completed = subprocess.run(
                [
                    sys.executable,
                    str(SCRIPT),
                    "--mode",
                    "EditMode",
                    "--expected-total",
                    "2",
                    "--not-before-epoch",
                    "0",
                    "--assembly",
                    assembly,
                    str(path),
                ],
                cwd=ROOT,
                capture_output=True,
                text=True,
                check=False,
            )

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertEqual("pass", json.loads(completed.stdout)["status"])

    def test_cli_rejects_nonpositive_total_and_invalid_run_boundaries(self) -> None:
        invalid_arguments = (
            ("--expected-total", "0"),
            ("--not-before-epoch", "nan"),
            ("--not-before-epoch", "inf"),
            ("--not-before-epoch", "-1"),
        )
        for option, value in invalid_arguments:
            with self.subTest(option=option, value=value):
                arguments = [
                    sys.executable,
                    str(SCRIPT),
                    "--mode",
                    "EditMode",
                    "--expected-total",
                    "2",
                    "--not-before-epoch",
                    "0",
                ]
                option_index = arguments.index(option)
                arguments[option_index + 1] = value
                arguments.append("missing.xml")

                completed = subprocess.run(
                    arguments,
                    cwd=ROOT,
                    capture_output=True,
                    text=True,
                    check=False,
                )

                self.assertEqual(2, completed.returncode)
                self.assertIn("error:", completed.stderr)

    def test_symlink_loop_fails_as_json_in_api_and_cli(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "loop.xml"
            try:
                path.symlink_to(path.name)
            except OSError as error:
                self.skipTest(f"symlink creation is unavailable: {error}")

            payload = MODULE.verify_unity_test_result(path, "EditMode", 2, 0.0)
            completed = subprocess.run(
                [
                    sys.executable,
                    str(SCRIPT),
                    "--mode",
                    "EditMode",
                    "--expected-total",
                    "2",
                    "--not-before-epoch",
                    "0",
                    str(path),
                ],
                cwd=ROOT,
                capture_output=True,
                text=True,
                check=False,
            )

        self.assert_failure_contains(payload, "result path cannot be resolved")
        self.assertEqual(1, completed.returncode, completed.stderr)
        self.assertEqual("fail", json.loads(completed.stdout)["status"])


if __name__ == "__main__":
    unittest.main()
