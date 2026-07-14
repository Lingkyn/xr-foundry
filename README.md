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
| Choose an available package | [`package-catalog.json`](package-catalog.json) |
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
| [`com.lingkyn.project-initializer`](com.lingkyn.project-initializer/) | Incubating | Configurable folder/scene scaffold, baseline prefabs, validation, and editor tools |
| [`com.lingkyn.xr-baseline`](com.lingkyn.xr-baseline/) | Incubating | Vendor-neutral XR Sandbox assets, rig helpers, configuration, and smoke-build tools |

`incubating` means a package is available for evaluation but does not yet promise
API compatibility. Candidate promotion requires repository validation, tests, and
a clean independent Unity consumer compile. XR behavior additionally needs real
device evidence before a stable claim.

Latest clean-foundation compile evidence:
[`docs/validation/2026-07-15-git-url-unity-smoke.md`](docs/validation/2026-07-15-git-url-unity-smoke.md).

## Install for evaluation

Pin a reviewed commit SHA rather than `main`:

```json
{
  "dependencies": {
    "com.lingkyn.project-initializer": "https://github.com/Lingkyn/xr-foundry.git?path=com.lingkyn.project-initializer#<commit-sha>",
    "com.lingkyn.xr-baseline": "https://github.com/Lingkyn/xr-foundry.git?path=com.lingkyn.xr-baseline#<commit-sha>"
  }
}
```

During package development, use `file:` dependencies from a separate Unity smoke
project. Do not create a release tag until the compatibility evidence for that
revision is recorded.

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
- XR Interaction Toolkit 3.3+, XR Management 4.5+, OpenXR 1.14+ for the XR package

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
systems such as inventory require a source and coverage bake-off before code is
admitted.

The repository is MIT licensed. See [`LICENSE`](LICENSE). Third-party dependencies
keep their own licenses.
