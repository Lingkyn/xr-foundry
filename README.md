# XR Foundry

Reusable XR development packages and reference material for people and coding
agents. The repository starts with Unity packages, while its catalog and quality
contracts are designed to support tools, templates, samples, validation, and future
engine-specific collections without pretending those implementations exist today.

XR Foundry is both:

- an installable source of versioned packages; and
- a reference source that agents can inspect, compare, adapt, and validate instead
  of reconstructing common systems from scratch.

The second use does not make generated changes automatically correct. Package
maturity, compatibility, license, tests, independent-consumer evidence, and device
evidence remain explicit gates.

## Start here

| Need | Entry point |
| --- | --- |
| Choose an available system or package | [`package-catalog.json`](package-catalog.json) |
| Find reusable reference material | [`reference-catalog.json`](reference-catalog.json) |
| Work with a coding agent | [`AGENTS.md`](AGENTS.md) and [`docs/for-agents.md`](docs/for-agents.md) |
| Install a Unity package | [Install for evaluation](#install-for-evaluation) |
| Propose a reusable system | [`CONTRIBUTING.md`](CONTRIBUTING.md) |
| Check evidence and maturity | [`docs/validation`](docs/validation/) and [`ROADMAP.md`](ROADMAP.md) |

Thin adapters are included for tools that discover repository instructions in
different ways: `CLAUDE.md`, `.cursor/rules/`, and `SKILL.md`. They all point back
to the same public catalog and quality contract rather than maintaining different
answers for different models.

## Unity package catalog

| Package | Maturity | Purpose |
| --- | --- | --- |
| [`com.lingkyn.project-initializer`](packages/unity/foundations/com.lingkyn.project-initializer/) | Incubating | Configurable folder/scene scaffold, baseline prefabs, validation, and editor tools |
| [`com.lingkyn.xr-baseline`](packages/unity/foundations/com.lingkyn.xr-baseline/) | Incubating | Vendor-neutral XR Sandbox assets, rig helpers, configuration, and smoke-build tools |
| [`Inventory Package Family`](docs/standards/inventory/README.md) | Mixed | One reusable Inventory system with optional Core, Unity authoring, renderer-neutral Presentation, UGUI, UI Toolkit, and renderer-specific XR modules |

The human-facing landing page groups a reusable system into one row to reduce
cognitive load. Its family page explains recommended compositions and lets a
person or Agent progressively disclose the installable modules. The machine-readable
[`package-catalog.json`](package-catalog.json) continues to record every package
separately because dependency, version, maturity, and evidence gates remain
module-specific.

## Incubating system standards

The first reusable game-system candidate is the
[`Inventory Package Family Standard`](docs/standards/inventory/README.md). Its
design inputs are restricted to admitted positive external sources. It deliberately
excludes consumer and screened-out code from derivation. Core and Unity authoring
retain candidate evidence. The canonical renderer-neutral architecture contains
Presentation `0.1.0`, UGUI `0.2.0`, UI Toolkit `0.1.0`, XR UGUI `0.1.0`, and XR UI
Toolkit `0.1.0` as incubating packages. Each current package graph needs its own
consumer evidence, and each XR renderer/device tuple needs its own real-device
receipt. No old package path or renderer-ambiguous XR compatibility layer is part
of the active repository surface.
The exact named-device handoff is the
[`Inventory XR Device Acceptance Receipt`](docs/validation/inventory-xr-device-receipt-template.md),
which includes a machine-validatable PICO tracked-controller profile without
adding a vendor dependency to the package.

`incubating` means a package is available for evaluation but does not yet promise
API compatibility. Candidate promotion requires repository validation, tests, and
a clean independent Unity consumer compile. XR behavior additionally needs real
device evidence before a stable claim.

Historical root-path foundation compile evidence (not proof of the current nested
paths):
[`docs/validation/2026-07-15-git-url-unity-smoke.md`](docs/validation/2026-07-15-git-url-unity-smoke.md).

## Install for evaluation

Pin a reviewed commit SHA rather than `main`:

```json
{
  "dependencies": {
    "com.lingkyn.project-initializer": "https://github.com/Lingkyn/xr-foundry.git?path=/packages/unity/foundations/com.lingkyn.project-initializer#<full-40-character-commit-sha>",
    "com.lingkyn.xr-baseline": "https://github.com/Lingkyn/xr-foundry.git?path=/packages/unity/foundations/com.lingkyn.xr-baseline#<same-full-40-character-commit-sha>",
    "com.lingkyn.inventory.core": "https://github.com/Lingkyn/xr-foundry.git?path=/packages/unity/systems/inventory/com.lingkyn.inventory.core#<same-full-40-character-commit-sha>",
    "com.lingkyn.inventory.unity": "https://github.com/Lingkyn/xr-foundry.git?path=/packages/unity/systems/inventory/com.lingkyn.inventory.unity#<same-full-40-character-commit-sha>",
    "com.lingkyn.inventory.presentation": "https://github.com/Lingkyn/xr-foundry.git?path=/packages/unity/systems/inventory/com.lingkyn.inventory.presentation#<same-full-40-character-commit-sha>",
    "com.lingkyn.inventory.ugui": "https://github.com/Lingkyn/xr-foundry.git?path=/packages/unity/systems/inventory/com.lingkyn.inventory.ugui#<same-full-40-character-commit-sha>",
    "com.lingkyn.inventory.uitoolkit": "https://github.com/Lingkyn/xr-foundry.git?path=/packages/unity/systems/inventory/com.lingkyn.inventory.uitoolkit#<same-full-40-character-commit-sha>",
    "com.lingkyn.inventory.xr.ugui": "https://github.com/Lingkyn/xr-foundry.git?path=/packages/unity/systems/inventory/com.lingkyn.inventory.xr.ugui#<same-full-40-character-commit-sha>",
    "com.lingkyn.inventory.xr.uitoolkit": "https://github.com/Lingkyn/xr-foundry.git?path=/packages/unity/systems/inventory/com.lingkyn.inventory.xr.uitoolkit#<same-full-40-character-commit-sha>"
  }
}
```

During package development, use `file:` dependencies from a separate Unity smoke
project. Do not create a release tag until the compatibility evidence for that
revision is recorded.

Use the full 40-character commit SHA for Git package pins. The Inventory clean
consumer gate confirmed that Unity Package Manager rejects a short SHA in this URL.
Pin every `com.lingkyn.inventory.*` package used by a Git consumer to the same
reviewed revision: custom transitive semver dependencies are not a public registry.

## Use as reference material

A coding agent should not copy the whole repository or infer a production claim
from a folder name. It should:

1. read `reference-catalog.json` and the selected package manifest;
2. inspect its README, documentation, tests, samples, changelog, and evidence;
3. decide whether to install the package, extend a public seam, or use it only as
   raw material for a consumer-owned adapter;
4. keep product-specific code in the consuming project; and
5. run the repository checks plus the consumer's own compile/tests.

See [`docs/for-agents.md`](docs/for-agents.md) for the provider-neutral workflow.

## Current compatibility target

- Unity 6000.3 LTS
- Universal Render Pipeline 17.x
- Input System 1.11+
- Repository XR baseline: XR Interaction Toolkit 3.3+, XR Management 4.5+,
  OpenXR 1.14+
- Inventory renderer adapters target the Unity 6000.3 UI systems. XR UGUI retains
  XRI 3.3.2 compatibility; the UI Toolkit XR route declares its exact minimum in
  its package manifest and does not install or claim a vendor runtime.

Compatibility is a tested target, not a promise for unlisted versions. Unity is
the only implemented engine collection in this foundation. Unreal Engine and Godot
are roadmap directions, not current support claims.

## Quality contract

Every live package must provide:

- a stable identity and matching assembly/namespace boundary;
- README, changelog, license, documentation, tests, and samples;
- no compile-time dependency on a consuming product or its assemblies;
- deterministic repository validation and CI;
- explicit maturity, compatibility, migration, deprecation, and security guidance;
- an independent consumer compile before candidate promotion; and
- device evidence before XR/controller/headset behavior is called stable.

Run the local checks:

```powershell
python scripts/validate_repository.py --json
python -m unittest discover -s tests -p "test_*.py"
```

Unity package tests run from a Unity consumer through the Test Framework.

## Contributing and license

Start with [`CONTRIBUTING.md`](CONTRIBUTING.md), [`SUPPORT.md`](SUPPORT.md), and
[`SECURITY.md`](SECURITY.md). Package proposals begin as `incubating`; mature game
systems require a positive-source and coverage bake-off before code is admitted.

The repository is MIT licensed. See [`LICENSE`](LICENSE). Third-party dependencies
keep their own licenses.
