from __future__ import annotations

import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "scripts" / "audit_unity_test_inventory.py"
SPEC = importlib.util.spec_from_file_location("audit_unity_test_inventory", SCRIPT)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)

CONSUMER_TEMPLATE = ROOT / "compositions" / "unity" / "reference-system" / "consumer"


def _write(root: Path, relative: str, text: str) -> None:
    path = root / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


def _asmdef(name: str, *, editor: bool, tests: bool = True) -> str:
    payload = {
        "name": name,
        "references": [],
        "optionalUnityReferences": ["TestAssemblies"] if tests else [],
        "includePlatforms": ["Editor"] if editor else [],
    }
    return json.dumps(payload)


class AuditUnityTestInventoryTests(unittest.TestCase):
    def test_reference_consumer_template_matches_recorded_source_audit(self) -> None:
        report = MODULE.audit_project(CONSUMER_TEMPLATE)
        self.assertEqual([], report["errors"])
        counts = {item["name"]: (item["mode"], item["cases"]) for item in report["assemblies"]}
        self.assertEqual(("EditMode", 71), counts["XRFoundry.ReferenceSystem.EditMode.Tests"])
        self.assertEqual(("PlayMode", 4), counts["XRFoundry.ReferenceSystem.PlayMode.Tests"])

    def test_live_package_test_assemblies_match_recorded_evidence_counts(self) -> None:
        packages = ROOT / "packages" / "unity" / "systems"
        report = MODULE.audit_project(packages)
        self.assertEqual([], report["errors"])
        counts = {item["name"]: item["cases"] for item in report["assemblies"]}
        # Counts recorded by prior Unity Editor runs in docs/validation and the XAG-INV-01 receipt.
        self.assertEqual(22, counts["Lingkyn.Inventory.Core.Editor.Tests"])
        self.assertEqual(16, counts["Lingkyn.Interaction.Core.Editor.Tests"])
        self.assertEqual(17, counts["Lingkyn.Interaction.Unity.Editor.Tests"])
        self.assertEqual(29, counts["Lingkyn.Settings.Core.Editor.Tests"])
        self.assertEqual(8, counts["Lingkyn.Settings.Unity.Editor.Tests"])
        self.assertEqual(5, counts["Lingkyn.Inventory.Presentation.Editor.Tests"])

    def test_counts_test_cases_and_ignores_samples_and_non_test_assemblies(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            _write(root, "Packages/pkg/Tests/Editor/Pkg.Editor.Tests.asmdef", _asmdef("Pkg.Editor.Tests", editor=True))
            _write(
                root,
                "Packages/pkg/Tests/Editor/PkgTests.cs",
                """
                using NUnit.Framework;
                namespace Lingkyn.Pkg.Tests
                {
                    public sealed class PkgTests
                    {
                        [Test]
                        public void One() { }

                        [TestCase(1)]
                        [TestCase(2)]
                        public void Parameterized(int value) { }

                        [Test, Timeout(1000)] public void Inline() { }
                        // [Test] public void Commented() { }
                        private const string Sample = "[Test] inside a string";
                    }
                }
                """,
            )
            _write(root, "Packages/pkg/Tests/Runtime/Pkg.PlayMode.Tests.asmdef", _asmdef("Pkg.PlayMode.Tests", editor=False))
            _write(
                root,
                "Packages/pkg/Tests/Runtime/PkgPlayModeTests.cs",
                "using UnityEngine.TestTools;\npublic class P { [UnityTest] public System.Collections.IEnumerator Runs() { yield break; } }\n",
            )
            _write(root, "Packages/pkg/Runtime/Pkg.asmdef", _asmdef("Pkg", editor=False, tests=False))
            _write(root, "Packages/pkg/Runtime/Runtime.cs", "public class R { [Test] public void NotATestAssembly() { } }\n")
            _write(root, "Packages/pkg/Samples~/Demo/Demo.Tests.asmdef", _asmdef("Demo.Tests", editor=True))
            _write(root, "Packages/pkg/Samples~/Demo/DemoTests.cs", "public class D { [Test] public void Skipped() { } }\n")

            report = MODULE.audit_project(root)

        self.assertEqual([], report["errors"])
        counts = {item["name"]: (item["mode"], item["cases"]) for item in report["assemblies"]}
        self.assertEqual({"Pkg.Editor.Tests": ("EditMode", 4), "Pkg.PlayMode.Tests": ("PlayMode", 1)}, counts)
        self.assertEqual({"EditMode": 4, "PlayMode": 1}, report["totals"])

    def test_dynamic_or_skipped_cases_fail_closed(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            _write(root, "Assets/Tests/Editor/Dyn.Tests.asmdef", _asmdef("Dyn.Tests", editor=True))
            _write(
                root,
                "Assets/Tests/Editor/DynTests.cs",
                "public class D { [TestCaseSource(nameof(Cases))] public void A(int v) { } [Test, Ignore(\"x\")] public void B() { } }\n",
            )
            report = MODULE.audit_project(root)

        self.assertTrue(any("[TestCaseSource]" in error for error in report["errors"]), report["errors"])
        self.assertTrue(any("[Ignore]" in error for error in report["errors"]), report["errors"])


if __name__ == "__main__":
    unittest.main()
