from __future__ import annotations

import argparse
import json
import shutil
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
TEMPLATE = ROOT / "compositions" / "unity" / "reference-system" / "consumer"

EMBEDDED_PACKAGES = (
    "packages/unity/systems/interaction/com.lingkyn.interaction.core",
    "packages/unity/systems/interaction/com.lingkyn.interaction.unity",
    "packages/unity/systems/inventory/com.lingkyn.inventory.core",
    "packages/unity/systems/inventory/com.lingkyn.inventory.presentation",
    "packages/unity/systems/inventory/com.lingkyn.inventory.ugui",
    "packages/unity/systems/inventory/com.lingkyn.inventory.unity",
    "packages/unity/systems/inventory/com.lingkyn.inventory.xr.ugui",
    "packages/unity/systems/persistence/com.lingkyn.persistence.core",
    "packages/unity/systems/persistence/com.lingkyn.persistence.unity",
    "packages/unity/systems/settings/com.lingkyn.settings.core",
    "packages/unity/systems/settings/com.lingkyn.settings.unity",
)


def fail(message: str) -> int:
    print(json.dumps({"schema": "xr-foundry.consumer_materialization.v1", "status": "fail", "error": message}, indent=2))
    return 1


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Materialize the reference binding consumer into a clean, disposable Unity project."
    )
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()

    output = args.output.expanduser().resolve()
    try:
        output.relative_to(ROOT)
    except ValueError:
        pass
    else:
        return fail("output must be outside the XR Foundry repository")

    if output.exists():
        return fail("output already exists; choose a new disposable directory")
    if not TEMPLATE.is_dir():
        return fail("reference consumer template is missing")

    for relative in EMBEDDED_PACKAGES:
        source = ROOT / relative
        if not source.is_dir() or not (source / "package.json").is_file():
            return fail(f"embedded package source is incomplete: {relative}")

    shutil.copytree(TEMPLATE, output)
    packages_root = output / "Packages"
    copied = []
    for relative in EMBEDDED_PACKAGES:
        source = ROOT / relative
        destination = packages_root / source.name
        shutil.copytree(source, destination)
        copied.append(source.name)

    print(
        json.dumps(
            {
                "schema": "xr-foundry.consumer_materialization.v1",
                "status": "pass",
                "source": TEMPLATE.relative_to(ROOT).as_posix(),
                "output": output.as_posix(),
                "embedded_packages": copied,
            },
            indent=2,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
