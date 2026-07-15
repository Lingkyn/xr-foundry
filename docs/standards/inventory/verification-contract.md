# Inventory Verification Contract

An Inventory implementation cannot be admitted because it compiles or because a
sample panel is visible. Evidence accumulates in the following order.

## 1. Source gate

- Every derivation input is present in `source-manifest.json`.
- Every source is external, positive, traceable, and assigned a bounded role.
- License boundaries are explicit.
- Consumer code and screened-out candidates are absent from derivation inputs.

## 2. Architecture gate

- Package dependency direction matches `inventory-standard.json`.
- The core assembly has no Unity UI, XR, scene, consumer, or service SDK dependency.
- Definitions, instances, stacks, inventories, containers, slots, snapshots, and
  mutations remain distinct public concepts.
- UI and persistence can be replaced without changing the aggregate.

## 3. Core behavior gate

Deterministic tests cover:

- add/remove at zero, one, maximum stack, capacity edge, and overflow;
- split/merge conservation of quantity;
- move/swap/transfer across valid and invalid containers;
- unique and mutable instances;
- duplicate IDs and unresolved definitions;
- policy composition and structured rejection reasons;
- failed-operation immutability and successful-operation atomicity;
- revision increments and post-commit event ordering;
- snapshot immutability;
- persistence round trip and migrations; and
- deterministic replay of a command sequence.

Stateful randomized tests assert quantity conservation, no duplicate instance IDs,
valid placement, stack bounds, and unchanged state after rejection across long
operation sequences.

## 4. Unity authoring gate

- EditMode tests validate definition catalogs and asset conversion.
- Duplicate IDs, broken references, invalid limits, and incompatible capabilities
  fail with actionable diagnostics.
- Runtime state never modifies authored assets.
- No Resources-folder or scene-singleton dependency is required.

## 5. Presentation gate

- Presenter tests prove that views cannot mutate storage directly.
- Prefab tests verify independent panel, grid, slot, item, details, and action roles.
- Nested-prefab links and variants survive import and package updates.
- Empty, partial, full, rejected, selected, disabled, and loading/error states are
  demonstrated in samples.
- Keyboard/pointer presentation tests do not require XR packages.

## 6. Package and consumer gate

- Repository validation and package tests pass.
- Packages resolve from a pinned Git revision in a clean Unity consumer.
- The clean consumer compiles Editor and player assemblies and runs package tests.
- Upgrade and rollback from the previous released package revision are recorded.
- Public API compatibility and migration notes match the release.

## 7. XR gate

- The XR adapter uses a world-space Canvas and the supported tracked-device UI path.
- Automated tests verify required raycaster/input-module configuration and prevent
  incompatible duplicate input modules.
- The sample can be placed independently of the camera rig and is not head-locked
  by default.
- A real headset test records readability, scale, angle, reach, occlusion, stable
  left/center/right targeting, interaction states, and comfort.

No headset, controller, world-space usability, or comfort claim may be promoted
without the final device evidence.

## Current evidence ledger

This ledger records the earliest unsatisfied gate for each layer. Later gates may
be prepared, but they cannot promote a package around an earlier failure.

| Layer | Satisfied evidence | Earliest unsatisfied gate | Claim allowed now |
| --- | --- | --- | --- |
| Core | Source/architecture gates; atomic mutation/invariant tests; transactional persistence round-trip and schema migration; local and immutable Git URL clean consumers | Typed mutable instance state and public API compatibility across a release | Incubating Core evaluation only |
| Unity authoring | Package boundary defined | Authoring implementation and EditMode tests | Architecture reference only |
| UGUI | Nested composition contract defined | Presentation implementation, prefab/state coverage, and PlayMode tests | Architecture reference only |
| XR | World-space/device contract defined | XR implementation, automated configuration checks, and Pico evidence | Architecture reference only; no headset claim |

Candidate promotion updates this ledger, `inventory-standard.json`,
`package-catalog.json`, `reference-catalog.json`, package documentation, and the
public work item in the same reviewed change. A passing later-layer demo cannot
override an earlier unsatisfied gate.
