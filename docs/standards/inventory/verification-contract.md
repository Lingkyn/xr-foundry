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

- Renderer-neutral presentation state, view models, intents, and presenter ports
  compile without UGUI, UI Toolkit, XRI, scene, or device dependencies.
- Presenter tests prove that views cannot mutate storage directly.
- UGUI prefab tests verify independent panel, grid, slot, item, details, and action roles.
- UI Toolkit tests verify equivalent semantic roles in the UXML/USS visual tree.
- Nested-prefab links and variants survive import and package updates.
- Empty, partial, full, rejected, selected, disabled, and loading/error states are
  demonstrated by both renderer samples.
- Keyboard/pointer presentation tests do not require XR packages, and each renderer
  emits the same semantic slot intents without mutating Core.

## 6. Package and consumer gate

- Repository validation and package tests pass.
- Packages resolve from a pinned Git revision in a clean Unity consumer.
- The clean consumer compiles Editor and player assemblies and runs package tests.
- Upgrade and rollback from the previous released package revision are recorded.
- Public API compatibility and migration notes match the release.

## 7. XR gate

- XR UGUI uses a world-space Canvas and the supported tracked-device UI path;
  automated tests verify its raycaster/input-module configuration.
- XR UI Toolkit uses the supported world-space panel and XRI UI Toolkit path;
  automated tests verify its panel/input configuration without Canvas assumptions.
- Each sample can be placed independently of the camera rig and is not head-locked
  by default; one renderer's automated or device evidence cannot promote the other.
- A real headset test records readability, scale, angle, reach, occlusion, stable
  left/center/right targeting, interaction states, and comfort.
- The receipt identifies the exact package revision, APK SHA-256, renderer adapter,
  XR adapter, Unity/XRI/XR provider versions, named device and firmware,
  controller/input mode, tester, date, posture, sample, and duration.
- The `pico_tracked_controller_v1` profile exercises both controllers against
  left/center/right targets, verifies target isolation and disabled-state
  immutability, checks world anchoring through head turns and lateral lean, and
  runs for at least two minutes.
- A completed receipt passes
  [`inventory-xr-device-receipt-template.md`](../../validation/inventory-xr-device-receipt-template.md)
  and the repository validator's `--device-receipt` gate.

No headset, controller, world-space usability, or comfort claim may be promoted
without the final device evidence. `partial`, `fail`, and `not_tested` all block
promotion. Direct poke remains a separate optional device claim even when its
automated PlayMode route passes.

## Current evidence ledger

This ledger records the earliest unsatisfied gate for each layer. Later gates may
be prepared, but they cannot promote a package around an earlier failure.

| Layer | Satisfied evidence | Earliest unsatisfied gate | Claim allowed now |
| --- | --- | --- | --- |
| Core | Source/architecture gates; atomic mutation/invariant tests; transactional persistence/migration; typed mutable instance state; local consumer and public API review | Immutable Git consumer at the canonical nested path | Incubating Core only; not the complete Inventory family |
| Unity authoring | ScriptableObject assets; stable IDs; deterministic conversion; actionable diagnostics; asset immutability; local clean-consumer EditMode tests | Immutable Git consumer at the canonical nested path | Incubating Unity authoring only; no presentation or XR claim |
| Presentation | Renderer-neutral API extracted from the proven presenter seam; engine-light assembly contract and unit tests prepared | Fresh clean-consumer compile and immutable Git install | Incubating neutral presentation contract only |
| UGUI | `0.2.0` consumes the neutral presentation assembly and retains nested renderer roles/tests | Fresh local and immutable Git consumer | Incubating UGUI renderer; no XR claim |
| UI Toolkit | Working VisualElement/UXML/USS route, semantic state/input tests, and sample prepared | Fresh local and immutable Git consumer plus renderer acceptance | Incubating UI Toolkit adapter only |
| XR UGUI | Renderer-explicit world-space Canvas, fail-closed validation, placement, and real-XRI route tests are authored | Fresh install/build and Unity Test Runner evidence, then named-device evidence | Incubating XR UGUI adapter only; no headset usability claim |
| XR UI Toolkit | Renderer-specific world-space panel, XRI input route, fail-closed validation, placement, sample, and tests prepared | Fresh install/build, renderer tests, then independent named-device evidence | Incubating XR UI Toolkit adapter only; no headset usability claim |

Candidate promotion updates this ledger, `inventory-standard.json`,
`package-catalog.json`, `reference-catalog.json`, package documentation, and the
public work item in the same reviewed change. A passing later-layer demo cannot
override an earlier unsatisfied gate.
