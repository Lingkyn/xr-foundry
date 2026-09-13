from __future__ import annotations

import importlib.util
import json
import tempfile
import unittest
from pathlib import Path
from unittest import mock


ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "scripts" / "run_unity_gates.py"
SPEC = importlib.util.spec_from_file_location("run_unity_gates", SCRIPT)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


def _passing_result_xml(mode: str, assembly: str, cases: int) -> str:
    test_cases = "".join(
        f'<test-case id="{index}" name="Case{index}" fullname="{assembly}.Case{index}" result="Passed" />'
        for index in range(cases)
    )
    counts = f'testcasecount="{cases}" total="{cases}" passed="{cases}" failed="0" inconclusive="0" skipped="0" result="Passed"'
    return (
        '<?xml version="1.0" encoding="utf-8"?>'
        f'<test-run id="2" {counts}>'
        f'<test-suite type="TestSuite" id="1000" name="host" fullname="host" {counts}>'
        f'<properties><property name="platform" value="{mode}" /></properties>'
        f'<test-suite type="Assembly" id="1001" name="{assembly}.dll" fullname="{assembly}.dll" {counts}>'
        f'<properties><property name="platform" value="{mode}" /></properties>'
        f"{test_cases}</test-suite></test-suite></test-run>"
    )


def _args(**overrides):
    parser = MODULE.build_parser()
    defaults = ["--host", "com.lingkyn.inventory.core"]
    return parser.parse_args(defaults + overrides.pop("argv", []))


class RunUnityGatesTests(unittest.TestCase):
    def test_minimal_host_embeds_transitive_dependencies_and_xr_pins(self) -> None:
        catalog = MODULE.load_catalog_packages()
        profile, packages = MODULE.resolve_host_packages("com.lingkyn.inventory.xr.ugui", catalog)
        self.assertEqual("minimal:com.lingkyn.inventory.xr.ugui", profile)
        self.assertEqual(
            [
                "com.lingkyn.inventory.core",
                "com.lingkyn.inventory.presentation",
                "com.lingkyn.inventory.ugui",
                "com.lingkyn.inventory.xr.ugui",
            ],
            packages,
        )
        with tempfile.TemporaryDirectory() as directory:
            host = Path(directory) / "host"
            MODULE.generate_host(host, packages, catalog)
            manifest = json.loads((host / "Packages" / "manifest.json").read_text(encoding="utf-8"))
        self.assertEqual(packages, manifest["testables"])
        self.assertEqual("1.6.0", manifest["dependencies"]["com.unity.test-framework"])
        for pin, version in MODULE.XR_HOST_PINS.items():
            self.assertEqual(version, manifest["dependencies"][pin])

    def test_all_packages_host_covers_every_live_package(self) -> None:
        catalog = MODULE.load_catalog_packages()
        profile, packages = MODULE.resolve_host_packages("all", catalog)
        self.assertEqual("all-packages", profile)
        self.assertEqual(sorted(catalog), packages)
        self.assertEqual(15, len(packages))

    def test_dry_run_plans_every_assembly_without_unity(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            args = _args(argv=["--dry-run", "--output", directory])
            receipt, exit_code = MODULE.run_gates(args)
            written = json.loads((Path(directory) / "receipt.json").read_text(encoding="utf-8"))
        self.assertEqual(0, exit_code)
        self.assertEqual("planned", receipt["status"])
        self.assertEqual([], receipt["inventory_errors"])
        self.assertEqual(1, receipt["summary"]["planned"])
        self.assertEqual("Lingkyn.Inventory.Core.Editor.Tests", receipt["runs"][0]["assembly"])
        self.assertEqual(22, receipt["runs"][0]["expected_cases"])
        self.assertIn("-assemblyNames", receipt["runs"][0]["command"])
        self.assertEqual(receipt["schema"], written["schema"])
        self.assertIsNone(receipt["host"]["path"], "temporary host must be removed after the run")

    def test_fake_unity_run_verifies_each_assembly_and_writes_receipt(self) -> None:
        def fake_launch(command: list[str], timeout_seconds: int) -> int:
            mode = command[command.index("-testPlatform") + 1]
            assembly = command[command.index("-assemblyNames") + 1]
            result = Path(command[command.index("-testResults") + 1])
            cases = 22 if assembly == "Lingkyn.Inventory.Core.Editor.Tests" else 1
            result.write_text(_passing_result_xml(mode, assembly, cases), encoding="utf-8")
            Path(command[command.index("-logFile") + 1]).write_text("fake unity log\n", encoding="utf-8")
            return 0

        with tempfile.TemporaryDirectory() as directory, mock.patch.object(MODULE, "launch_unity", side_effect=fake_launch):
            args = _args(argv=["--unity", str(ROOT / "scripts" / "run_unity_gates.py"), "--output", directory])
            receipt, exit_code = MODULE.run_gates(args)

        self.assertEqual(0, exit_code, json.dumps(receipt["runs"], indent=2))
        self.assertEqual("pass", receipt["status"])
        self.assertEqual({"assemblies": 1, "passed": 1, "failed": 0, "planned": 0}, receipt["summary"])
        run = receipt["runs"][0]
        self.assertEqual("pass", run["verification"]["status"])
        self.assertEqual(22, run["verification"]["actual_test_cases"])
        self.assertIsNotNone(run["result_sha256"])

    def test_wrong_case_count_fails_closed(self) -> None:
        def fake_launch(command: list[str], timeout_seconds: int) -> int:
            mode = command[command.index("-testPlatform") + 1]
            assembly = command[command.index("-assemblyNames") + 1]
            result = Path(command[command.index("-testResults") + 1])
            result.write_text(_passing_result_xml(mode, assembly, 3), encoding="utf-8")
            return 0

        with tempfile.TemporaryDirectory() as directory, mock.patch.object(MODULE, "launch_unity", side_effect=fake_launch):
            args = _args(argv=["--unity", str(SCRIPT), "--output", directory])
            receipt, exit_code = MODULE.run_gates(args)

        self.assertEqual(1, exit_code)
        self.assertEqual("fail", receipt["status"])
        self.assertTrue(any("expected total" in error for error in receipt["runs"][0]["verification"]["errors"]))

    def test_missing_result_is_a_failure_not_a_pass(self) -> None:
        with tempfile.TemporaryDirectory() as directory, mock.patch.object(MODULE, "launch_unity", return_value=0):
            args = _args(argv=["--unity", str(SCRIPT), "--output", directory])
            receipt, exit_code = MODULE.run_gates(args)

        self.assertEqual(1, exit_code)
        self.assertEqual("fail", receipt["runs"][0]["status"])
        self.assertTrue(any("missing" in error for error in receipt["runs"][0]["verification"]["errors"]))


if __name__ == "__main__":
    unittest.main()
