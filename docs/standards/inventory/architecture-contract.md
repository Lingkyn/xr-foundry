# Inventory Architecture Contract

## Design invariant

Inventory is a domain system. Unity authoring, UI technology, XR interaction,
equipment visuals, persistence providers, networking, crafting, and commerce are
adapters or neighboring systems. None may become a hidden dependency of the core.

The implementation must be independently authored from the admitted public
contracts. Consumer code is not an implementation seed.

## Domain model

The core distinguishes these concepts:

- **Item definition**: immutable shared identity and authored defaults.
- **Item instance**: unique runtime identity plus mutable instance state.
- **Item stack**: a quantity of stack-compatible instances represented together.
- **Inventory**: an owner-facing aggregate containing one or more containers.
- **Container**: storage with addressing and policy; bags, equipment slots, hotbars,
  stashes, and grids are configurations or specializations.
- **Slot address**: stable logical placement; never a direct UI object reference.
- **Snapshot**: immutable read model used by persistence, presentation, diagnostics,
  and synchronization.

Definition identity and instance identity must never be interchangeable. Display
names and asset names are not persistence IDs.

## Mutation protocol

All mutations follow one path:

```text
request -> validate policies -> produce plan -> atomic commit -> result + events
```

Required operations are add, remove, move, swap, split, merge, and transfer. A
result reports success or a structured failure reason, accepted quantity, remainder,
affected addresses, and resulting revision. Partial success is allowed only when
the caller explicitly selects a partial-acceptance policy.

Validation must not mutate state. Failed operations leave state and revision
unchanged. Successful multi-container transfers are atomic within the local domain
boundary. Events are emitted only after commit and preserve operation ordering.

## Policies and extension seams

Capacity, stacking, placement, category/tag acceptance, ownership, and mutation
authority are composable policies. A custom rule can be added without subclassing
the Inventory aggregate or changing UI code.

Optional item capabilities use typed extension records or fragments. They cannot
require a universal dictionary of unvalidated objects. Equipment, use actions,
crafting, currency, shops, drops, pickups, durability, and online authority attach
through explicit integrations.

Mutable state on a unique item instance uses registered typed codecs. Each stored
fragment has a stable type ID and independent schema version; the codec validates,
decodes, and normalizes its payload. The aggregate owns atomic set/remove commands
and persistence of the immutable encoded fragment, while serializer choice remains
outside Core. Unknown codecs or invalid/future fragment schemas fail explicitly.

## Persistence

The core owns serializable state contracts, not disk or cloud I/O. A persistence
envelope includes schema version, aggregate revision, inventory/container IDs,
definition IDs, instance IDs, quantities, placement, and typed instance state.

Persistence adapters must provide:

- deterministic round trips;
- migration from every supported prior schema;
- explicit handling for missing or renamed definitions;
- duplicate-instance detection;
- transactional load or rollback; and
- diagnostics that do not silently discard state.

Online adapters map local requests to an authority boundary and expose conflict or
write-lock failures. The local core must not depend on an authentication, economy,
or cloud-save SDK.

## Unity authoring

Unity item definitions are authoring assets that compile into validated domain
definitions. Runtime mutable state must not be written back into source assets.
Catalog validation detects duplicate IDs, missing definitions, invalid stack
limits, incompatible capabilities, and broken presentation references.

The Unity package supplies inspectors and validation but does not impose a project
folder structure, scene singleton, service locator, or persistent GameObject.

## Presentation boundary

Presentation reads snapshots/view models and sends commands. Buttons, slots,
drag/drop handlers, ray targets, and panels cannot edit collections directly.

Renderer-neutral presentation owns semantic UI state, immutable view models,
selection/activation intents, and presenter ports. It has no dependency on UGUI,
UI Toolkit, scenes, XR Interaction Toolkit, or a device SDK. Renderer adapters
translate those contracts into their own visual trees and input events; they do
not redefine Inventory semantics.

The first Unity renderer uses nested UGUI prefabs with independent roles:

```text
InventoryShell
|- InventoryPanel
|  |- InventoryGrid
|  |  `- InventorySlot (repeated)
|  |     `- ItemView
|  |- ItemDetails
|  `- ActionMenu
```

`InventorySlot`, `ItemView`, `ItemDetails`, and action controls are reusable prefab
assets. A product can replace any view through presenter interfaces without
forking domain code. Prefab variants may provide styling; they cannot change domain
semantics.

The peer UI Toolkit adapter preserves the same semantic roles as UXML/USS and
`VisualElement` composition. Identical role names do not imply shared renderer
objects: a `RectTransform`, Canvas, UGUI prefab, `VisualElement`, UXML tree, and
`PanelSettings` remain inside their renderer adapters.

## Package boundaries

### `com.lingkyn.inventory.core`

Engine-light domain types, policies, transactions, events, snapshots, and
persistence interfaces. Its runtime assembly has no Unity UI or XR dependency.

### `com.lingkyn.inventory.unity`

ScriptableObject authoring, definition catalogs, inspectors, conversion, Unity
serialization adapters, and validation tools.

### `com.lingkyn.inventory.presentation`

Renderer-neutral view state, immutable view models, semantic selection/activation
intents, `IInventoryView`, and the presenter that maps Core snapshots and results
to the view contract. Its runtime assembly is engine-light and has no Unity UI or
XR dependency.

### `com.lingkyn.inventory.ugui`

UGUI bindings, nested prefabs, keyboard/pointer input, a state-gallery sample, and
renderer tests. It consumes `com.lingkyn.inventory.presentation` and does not
depend on XR Interaction Toolkit.

### `com.lingkyn.inventory.uitoolkit`

UI Toolkit bindings, UXML/USS composition, keyboard/pointer input, a state-gallery
sample, and renderer tests. It is a peer of UGUI, not a wrapper around a Canvas or
UGUI prefab.

### `com.lingkyn.inventory.xr.ugui`

Optional world-space Canvas composition, tracked-device interaction, renderer-
specific validation, and a device-validation sample for UGUI.

### `com.lingkyn.inventory.xr.uitoolkit`

Optional UI Toolkit world-space composition, XRI interaction routing, renderer-
specific validation, and a device-validation sample. It does not reuse Canvas
validators or infer evidence from the UGUI route.

## XR contract

Each XR renderer composition defaults to world-space UI. It provides stable
interaction targets, visible hover/select/disabled states, predictable focus, and
renderer-appropriate input checks. It must support scene placement independent of
head pose; head-locked behavior is an explicit optional policy, never the default.

UGUI validation owns Canvas, `TrackedDeviceGraphicRaycaster`, and
`XRUIInputModule` requirements. UI Toolkit validation owns `UIDocument`, world-
space panel configuration, panel input, and the supported XRI UI Toolkit path.
Neither validator is a renderer-neutral rule.

A shared `com.lingkyn.inventory.xr.core` package must not be created speculatively.
Common XR semantics may be extracted only after both working renderer compositions
show tested duplication that is independent of their renderer APIs.

Editor simulation proves only configuration and interaction routing. Readability,
reach, occlusion, controller stability, scale, angle, comfort, and headset runtime
remain unproven until recorded on a real target device. Evidence is scoped to the
exact revision, artifact hash, renderer adapter, XR adapter, device/runtime profile,
and input modality; it cannot be transferred across those boundaries.

## Explicit non-goals for the first core release

- A complete RPG, crafting, shop, currency, loot, or equipment system.
- A universal network protocol or backend service.
- Product art, product-specific item definitions, or product scenes.
- Mandatory grid shapes, weight, categories, or hotbars.
- Cross-engine implementation claims before another engine has working packages and
  independent evidence.
