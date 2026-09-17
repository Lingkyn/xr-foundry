from __future__ import annotations

"""Derive the exact Unity test-case inventory of a consumer project from source.

The repository's result verifier (``verify_unity_test_results.py``) rejects a Unity
NUnit result unless it proves exactly one test Assembly with an exact, caller-supplied
case count. That count must come from a source audit, never from the result under
test. This script performs that audit deterministically:

* every ``*.asmdef`` that opts into the Unity Test Framework is a test assembly;
* an assembly restricted to the ``Editor`` platform runs in EditMode, any other test
  assembly runs in PlayMode;
* each ``[Test]`` or ``[UnityTest]`` method counts once, and a method carrying
  ``[TestCase]`` attributes counts once per ``[TestCase]``;
* ``[TestCaseSource]``, ``[ValueSource]``, ``[Ignore]``, and ``[Explicit]`` cannot be
  counted statically or would produce skipped cases, so they fail the audit closed.

Directories whose name ends with ``~`` are not imported by Unity and are skipped,
as are ``Library``, ``Temp``, ``Logs``, and hidden directories.
"""

import argparse
import json
import re
from pathlib import Path
from typing import Any


SCHEMA = "xr-foundry.unity_test_inventory.v1"
MODES = ("EditMode", "PlayMode")
SKIPPED_DIRECTORY_NAMES = {"Library", "Temp", "Logs", "obj", "Build", "Builds"}
TEST_FRAMEWORK_REFERENCES = {"UnityEngine.TestRunner", "UnityEditor.TestRunner"}
UNSUPPORTED_ATTRIBUTES = ("TestCaseSource", "ValueSource", "Ignore", "Explicit")

ATTRIBUTE_BLOCK = re.compile(r"\[([^\[\]]*)\]")
ATTRIBUTE_NAME = re.compile(
    r"(?<![\w.])(Test|UnityTest|TestCase|TestCaseSource|ValueSource|Ignore|Explicit)"
    r"(?:Attribute)?\s*(?=\(|,|$)"
)
METHOD_DECLARATION = re.compile(
    r"\b(?:public|internal|protected|private)\b[^;{=]*?\b[A-Za-z_][A-Za-z0-9_]*\s*\("
)
BLOCK_COMMENT = re.compile(r"/\*.*?\*/", re.DOTALL)
LINE_COMMENT = re.compile(r"//[^\n]*")
STRING_LITERAL = re.compile(r'@"(?:[^"]|"")*"|"(?:\\.|[^"\\\n])*"')


def _is_skipped_directory(name: str) -> bool:
    return name.endswith("~") or name.startswith(".") or name in SKIPPED_DIRECTORY_NAMES


def _walk_files(root: Path, suffix: str) -> list[Path]:
    matches: list[Path] = []
    for path in sorted(root.rglob(f"*{suffix}")):
        if not path.is_file():
            continue
        relative_parts = path.relative_to(root).parts[:-1]
        if any(_is_skipped_directory(part) for part in relative_parts):
            continue
        matches.append(path)
    return matches


def _strip_non_code(text: str) -> str:
    text = BLOCK_COMMENT.sub(" ", text)
    text = STRING_LITERAL.sub('""', text)
    return LINE_COMMENT.sub(" ", text)


def _is_test_assembly(definition: dict[str, Any]) -> bool:
    optional = definition.get("optionalUnityReferences")
    if isinstance(optional, list) and "TestAssemblies" in optional:
        return True
    references = definition.get("references")
    if isinstance(references, list) and TEST_FRAMEWORK_REFERENCES & {str(item) for item in references}:
        return True
    constraints = definition.get("defineConstraints")
    return isinstance(constraints, list) and "UNITY_INCLUDE_TESTS" in constraints


def _assembly_mode(definition: dict[str, Any]) -> str:
    platforms = definition.get("includePlatforms")
    if isinstance(platforms, list) and platforms == ["Editor"]:
        return "EditMode"
    return "PlayMode"


def count_test_cases(source: str, label: str, errors: list[str]) -> int:
    """Count NUnit cases declared in one C# source file, failing closed on dynamic sources."""

    cases = 0
    pending_test = False
    pending_test_cases = 0
    for line_number, raw_line in enumerate(_strip_non_code(source).splitlines(), start=1):
        line = raw_line.strip()
        if not line:
            continue
        for block in ATTRIBUTE_BLOCK.findall(line):
            for name in ATTRIBUTE_NAME.findall(block):
                if name in UNSUPPORTED_ATTRIBUTES:
                    errors.append(
                        f"{label}:{line_number}: [{name}] cannot be audited statically; "
                        "replace it with explicit [Test] or [TestCase] declarations"
                    )
                elif name == "TestCase":
                    pending_test_cases += 1
                else:
                    pending_test = True
        declaration = ATTRIBUTE_BLOCK.sub(" ", line)
        if METHOD_DECLARATION.search(declaration) and (pending_test or pending_test_cases):
            cases += pending_test_cases if pending_test_cases else 1
            pending_test = False
            pending_test_cases = 0
    if pending_test or pending_test_cases:
        errors.append(f"{label}: test attribute is not followed by a method declaration")
    return cases


def audit_project(project_root: Path) -> dict[str, Any]:
    errors: list[str] = []
    root = project_root.resolve()
    if not root.is_dir():
        return {"schema": SCHEMA, "project": root.as_posix(), "assemblies": [], "errors": ["project root is not a directory"]}

    definitions: list[tuple[Path, dict[str, Any]]] = []
    for asmdef_path in _walk_files(root, ".asmdef"):
        try:
            payload = json.loads(asmdef_path.read_text(encoding="utf-8"))
        except (json.JSONDecodeError, UnicodeDecodeError) as error:
            errors.append(f"{asmdef_path.relative_to(root).as_posix()}: invalid asmdef JSON: {error}")
            continue
        if isinstance(payload, dict) and isinstance(payload.get("name"), str):
            definitions.append((asmdef_path, payload))
        else:
            errors.append(f"{asmdef_path.relative_to(root).as_posix()}: asmdef must declare a name")

    owners = sorted(((path.parent, payload) for path, payload in definitions), key=lambda item: len(item[0].parts), reverse=True)
    sources_by_assembly: dict[str, list[Path]] = {payload["name"]: [] for _, payload in definitions}
    for source in _walk_files(root, ".cs"):
        for directory, payload in owners:
            if directory == source.parent or directory in source.parents:
                sources_by_assembly[payload["name"]].append(source)
                break

    assemblies: list[dict[str, Any]] = []
    seen_names: set[str] = set()
    for asmdef_path, payload in sorted(definitions, key=lambda item: item[1]["name"]):
        name = payload["name"]
        if name in seen_names:
            errors.append(f"duplicate assembly definition name: {name}")
            continue
        seen_names.add(name)
        if not _is_test_assembly(payload):
            continue
        cases = 0
        for source in sources_by_assembly.get(name, []):
            cases += count_test_cases(
                source.read_text(encoding="utf-8"),
                source.relative_to(root).as_posix(),
                errors,
            )
        if cases < 1:
            errors.append(f"{name}: test assembly declares no statically countable test cases")
        assemblies.append(
            {
                "name": name,
                "dll": f"{name}.dll",
                "mode": _assembly_mode(payload),
                "asmdef": asmdef_path.relative_to(root).as_posix(),
                "source_files": len(sources_by_assembly.get(name, [])),
                "cases": cases,
            }
        )

    return {
        "schema": SCHEMA,
        "project": root.as_posix(),
        "assemblies": assemblies,
        "totals": {mode: sum(item["cases"] for item in assemblies if item["mode"] == mode) for mode in MODES},
        "errors": errors,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Audit the exact Unity test-case inventory of a project from source.")
    parser.add_argument("project_root", type=Path)
    parser.add_argument("--mode", choices=MODES, help="Limit the reported assemblies to one test mode.")
    parser.add_argument(
        "--github-matrix",
        action="store_true",
        help="Print a GitHub Actions matrix include list instead of the full report.",
    )
    args = parser.parse_args()

    report = audit_project(args.project_root)
    assemblies = report["assemblies"]
    if args.mode:
        assemblies = [item for item in assemblies if item["mode"] == args.mode]
    if args.github_matrix:
        print(json.dumps({"include": [{"assembly": item["name"], "mode": item["mode"], "cases": item["cases"]} for item in assemblies]}))
    else:
        report["assemblies"] = assemblies
        print(json.dumps(report, indent=2))
    return 0 if not report["errors"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
