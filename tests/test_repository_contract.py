from __future__ import annotations

import importlib.util
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "scripts/validate_repository.py"
SPEC = importlib.util.spec_from_file_location("validate_repository", SCRIPT)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class RepositoryContractTests(unittest.TestCase):
    def test_current_repository_passes(self) -> None:
        self.assertEqual([], MODULE.validate_repository(ROOT))

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

    def test_unanchored_unity_build_ignore_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / ".gitignore").write_text("Build/\n", encoding="utf-8")
            errors = MODULE.validate_ignore_scope(root)
            self.assertTrue(any("root-anchored" in error for error in errors))

    def test_missing_internal_namespace_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            package = Path(directory)
            (package / "Consumer.cs").write_text(
                "using Lingkyn.Unity.Missing; namespace Lingkyn.Unity.Consumer {}",
                encoding="utf-8",
            )
            errors = MODULE.validate_internal_namespace_links(package)
            self.assertTrue(any("no source declaration" in error for error in errors))


if __name__ == "__main__":
    unittest.main()
