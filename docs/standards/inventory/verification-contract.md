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
  left/center/right targeting with each left and right controller, target isolation,
  disabled-target immutability, interaction states, posture, measured duration, and
  comfort.
- The receipt identifies the exact package revision, artifact SHA-256/reference,
  artifact kind/file name/application ID, renderer and XR adapters, resolved
  dependency-lock digest, XRI/OpenXR/XR Management/Input System versions,
  engine/runtime versions, build target, graphics API, scripting backend,
  architecture, named device and OS, exact input routes and sources, tester
  identity, timestamps, and immutable evidence.
- The
  [`inventory-world-space-ui-v1`](../../device-lab/test-plans/inventory-world-space-ui-v1.json)
  plan requires at least 120 seconds in the recorded seated or standing posture. It
  exercises left/center/right hover and activation independently with both
  controllers, target isolation, disabled-target no-mutation, semantic states,
  world anchoring under head turns and lateral lean, readability, scale, angle,
  reach, occlusion, sustained comfort, and the selected profile's restart or
  recenter path. Optional direct-poke, hand-ray, and gaze-and-pinch claims each
  require their own admitted route and passed check.
- A completed generic
  [`Device Lab receipt`](../../device-lab/device-receipt.template.json) passes the
  repository validator's `--device-lab-receipt` gate for the exact selected plan,
  profile, package composition, artifact, environment, and evidence set.

No headset, controller, world-space usability, or comfort claim may be promoted
without the final device evidence. `fail`, `blocked`, `inconclusive`, and
`not_tested` all block promotion. Direct poke remains a separate optional device
claim even when its automated PlayMode route passes.

This contract is version-adaptive, not version-agnostic evidence. An Agent may
generate a candidate for another Unity, renderer, XRI, OpenXR, or input-stack
version, but that candidate receives no inherited support claim. Its receipt must
record and validate its own exact resolved lock, build, runtime, device, and input
tuple.

## Unity test evidence basis

- [Unity custom package tests](https://docs.unity3d.com/Manual/cus-tests.html)
  requires a consumer project to name packages in its manifest `testables` list
  before their package tests are included. XR Foundry therefore derives the
  declared EditMode and PlayMode assembly set from the exact bound manifest and
  same-commit package test Assembly Definitions; a passing XML file cannot name an
  arbitrary or incomplete assembly set.
- [Unity Assembly Definition file format](https://docs.unity3d.com/6000.0/Documentation/Manual/assembly-definition-file-format.html)
  defines platform filters, define constraints, and optional Unity references.
  XR Foundry uses those fields to classify Editor and Runtime test assemblies and
  binds the result to Unity's project-suite and Assembly-suite mode properties.

Every profile must include all commit-required assemblies, while the XML assembly
set must exactly equal the assemblies declared by that consumer's `testables`.
This permits one full-graph Unity result to support a narrower package closure only
when the same manifest, immutable commit, and exact tested assembly graph remain
bound; it does not transfer a pass between modes, versions, or revisions.

## Current evidence ledger

This ledger records the earliest unsatisfied gate for each layer. Later gates may
be prepared, but they cannot promote a package around an earlier failure.

| Layer | Satisfied evidence | Earliest unsatisfied gate | Claim allowed now |
| --- | --- | --- | --- |
| Core | Positive-source and architecture gates are satisfied | Current atomic, persistence, typed-state, API, and consumer tests, then immutable Git and review gates | Incubating Core only; not the complete Inventory family |
| Unity authoring | ScriptableObject, stable-ID, conversion, diagnostics, and immutability surfaces are present | Current clean-consumer EditMode tests, then immutable Git and review gates | Incubating Unity authoring only; no presentation or XR claim |
| Presentation | Renderer-neutral API and engine-light assembly are present | Presenter, compile, local-consumer, immutable Git, and API-review gates | Incubating neutral presentation contract only |
| UGUI | `0.2.0` neutral composition and nested renderer roles are present | Current state/raycast/local tests, then non-XR immutable Git and review gates | Incubating UGUI renderer; no XR claim |
| UI Toolkit | VisualElement/UXML/USS route and sample are present | Current semantic/local tests, then immutable Git and renderer-review gates | Incubating UI Toolkit adapter only |
| XR UGUI | Renderer-explicit Canvas, fail-closed validation, placement, and XRI test routes are authored | Current local/immutable/XRI tests, then Android/device gates | Incubating XR UGUI adapter only; no headset usability claim |
| XR UI Toolkit | Renderer-specific panel, fail-closed validation, placement, and XRI test routes are authored | Current local/immutable/XRI tests, then Android/device gates | Incubating XR UI Toolkit adapter only; no headset usability claim |

Candidate promotion updates this ledger, `inventory-standard.json`,
`package-catalog.json`, `reference-catalog.json`, package documentation, and the
public work item in the same reviewed change. A passing later-layer demo cannot
override an earlier unsatisfied gate.
