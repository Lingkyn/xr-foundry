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
| Find or claim bounded public work | [`Public Task Hall V1`](docs/contributing/task-hall.md) and the [live Project](https://github.com/users/Lingkyn/projects/2) |
| Build the next reusable package family | [`Foundry V1 production line`](docs/foundry/README.md), [first batch](docs/foundry/batches/unity-first-batch.v1.json), and [next source-gate queue](docs/foundry/queue/next-batch.json) |
| Discuss a public RFC | [Discussion #22](https://github.com/Lingkyn/xr-foundry/discussions/22) and the Ideas RFC form |
| Contribute hardware evidence | [`Public Device Lab V1`](docs/device-lab/README.md) |
| See how contributions are recognized | [`Recognition policy`](docs/contributing/recognition-policy.md) and [`CONTRIBUTORS.md`](CONTRIBUTORS.md) |
| Understand repository workflow | [`PROJECT_GITHUB_PLAYBOOK.md`](PROJECT_GITHUB_PLAYBOOK.md) |
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
| [`Inventory Package Family`](docs/standards/inventory/README.md) | Incubating | One reusable Inventory system with optional Core, Unity authoring, renderer-neutral Presentation, UGUI, UI Toolkit, and renderer-specific XR modules |
| [`Persistence Package Family`](docs/standards/persistence/README.md) | Incubating | Engine-light save/recovery contracts plus an optional Unity local-file, ScriptableObject, and JsonUtility adapter |
| [`Settings Package Family`](docs/standards/settings/README.md) | Incubating | Engine-light typed settings, profiles, transactional apply/rollback, accessibility discoverability metadata, and optional Unity authoring |

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
retain their public version/API history, while current-revision execution evidence
is pending. The canonical renderer-neutral architecture contains
Core `0.1.1`, Unity authoring `0.1.1`, Presentation `0.1.0`, UGUI `0.2.0`, UI
Toolkit `0.1.0`, XR UGUI `0.1.0`, and XR UI Toolkit `0.1.0` as incubating packages.
Each current package graph needs its own
consumer evidence, and each XR renderer/device tuple needs its own real-device
receipt. No old package path or renderer-ambiguous XR compatibility layer is part
of the active repository surface.

These nine packages form the
[`Unity first batch`](docs/foundry/batches/unity-first-batch.v1.json). A batch
release is an immutable discovery/install surface; it does not promote package
maturity or inherit device claims. The
[`Foundry V1 production line`](docs/foundry/README.md) governs how later package
families move from positive-source proposal to independently reviewed release.
The exact named-device handoff uses the generic
[`Public Device Lab V1`](docs/device-lab/README.md), its
[`Inventory world-space UI plan`](docs/device-lab/test-plans/inventory-world-space-ui-v1.json),
and the machine-validatable
[`execution receipt`](docs/device-lab/device-receipt.template.json). This admits a
PICO tracked-controller profile without adding a vendor dependency to the package,
while keeping the same route available for other reviewed device profiles.

`incubating` means a package is available for evaluation but does not yet promise
API compatibility. Candidate promotion requires repository validation, tests, and
a clean independent Unity consumer compile. XR behavior additionally needs real
device evidence before a stable claim.

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
    "com.lingkyn.inventory.xr.uitoolkit": "https://github.com/Lingkyn/xr-foundry.git?path=/packages/unity/systems/inventory/com.lingkyn.inventory.xr.uitoolkit#<same-full-40-character-commit-sha>",
    "com.lingkyn.persistence.core": "https://github.com/Lingkyn/xr-foundry.git?path=/packages/unity/systems/persistence/com.lingkyn.persistence.core#<same-full-40-character-commit-sha>",
    "com.lingkyn.persistence.unity": "https://github.com/Lingkyn/xr-foundry.git?path=/packages/unity/systems/persistence/com.lingkyn.persistence.unity#<same-full-40-character-commit-sha>",
    "com.lingkyn.settings.core": "https://github.com/Lingkyn/xr-foundry.git?path=/packages/unity/systems/settings/com.lingkyn.settings.core#<same-full-40-character-commit-sha>",
    "com.lingkyn.settings.unity": "https://github.com/Lingkyn/xr-foundry.git?path=/packages/unity/systems/settings/com.lingkyn.settings.unity#<same-full-40-character-commit-sha>"
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

## Version-adaptive references and current implementation profile

The repository's standards, tests, and samples are version-adaptive raw material;
they are not limited to the Editor version used for the current implementation.
An Agent may generate a target-specific candidate for another Unity, UI Toolkit,
XRI, or future engine version. Each installable package revision still declares a
concrete manifest and may claim only its own verified profile. See the
[`Version-Adaptive Reference Model`](docs/architecture/version-adaptive-reference-model.md)
and [`compatibility-profiles.json`](compatibility-profiles.json).

The first immutable automated validation target is one concrete profile: Unity
`6000.3.19f1`, URP `17.3.0`, Input System `1.19.0`, UGUI `2.0.0`, XRI `3.5.1`,
XR Plug-in Management `4.5.3`, and OpenXR `1.16.0`. Machine-readable receipts now
verify the named automated profiles at their exact evidence commit. Later package
or release commits do not inherit those results; `compatibility-profiles.json` is
authoritative for the exact state, tuple, revision, and evidence.

That tuple is a reproducibility boundary, not a minimum-version declaration or a
claim that other versions cannot be generated. Unlisted tuples begin as adaptation
candidates and become installable compatibility claims only after their own
validation. Unity is the only implemented engine collection in this foundation.
Unreal Engine and Godot are roadmap directions, not current support claims.

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
python -m pip install -r scripts/contract-requirements.txt
python scripts/validate_repository.py --json --fast-structure
python scripts/validate_repository.py --json --run-contract-tests
```

The fast structure command is iteration feedback and cannot support promotion or
release. The full command runs repository validation first and skips the test
suite if that first stage fails.

Unity package tests run from a Unity consumer through the Test Framework.

## Contributing and license

Start with [`CONTRIBUTING.md`](CONTRIBUTING.md), [`SUPPORT.md`](SUPPORT.md), and
[`SECURITY.md`](SECURITY.md). Package proposals begin as `incubating`; mature game
systems require a positive-source and coverage bake-off before code is admitted.

The [Task Hall](docs/contributing/task-hall.md) publishes bounded research, build,
review, and integration work. The [Device Lab](docs/device-lab/README.md) lets
contributors submit revision-bound headset evidence without code or repository
write access. Claiming work coordinates a lease only; it never grants GitHub
permissions or merge authority.

The repository is MIT licensed. See [`LICENSE`](LICENSE). Third-party dependencies
keep their own licenses.

## Public workbench for people and Agents

XR Foundry treats GitHub as durable shared state, not merely a place to upload the
final code. Umbrella Issues keep a system understandable as one outcome; child
Issues and named checkpoints expose independently valuable work. Each checkpoint
states its dependencies, allowed paths, non-goals, acceptance, verification,
evidence, device/review gates, and exact next safe action.

This lets a contributor finish one unit without pretending the whole system is
done. If a person, Cursor, Codex, Claude Code, or another tool stops midstream, a
continuation receipt preserves completed checkpoints, the current revision,
evidence, remaining work, blockers, and handoff boundary for the next contributor.
If a process stops too abruptly to publish that receipt, work resumes from the last
public checkpoint boundary; local-only output is never assumed complete.

Contribution is not limited to code. Research, documentation, design, review,
tests, device/user testing, and infrastructure can all be acknowledged through
accepted evidence. They remain separate categories rather than a total points
ranking, and no activity score grants repository permission. Start with the
[Task Hall](docs/contributing/task-hall.md), choose one certified checkpoint, and
use a fork pull request unless you already hold an appropriate repository role.
