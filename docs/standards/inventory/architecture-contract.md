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

The baseline Unity UI composition uses nested prefabs with independent roles:

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

## Package boundaries

### `com.lingkyn.inventory.core`

Engine-light domain types, policies, transactions, events, snapshots, and
persistence interfaces. Its runtime assembly has no Unity UI or XR dependency.

### `com.lingkyn.inventory.unity`

ScriptableObject authoring, definition catalogs, inspectors, conversion, Unity
serialization adapters, and validation tools.

### `com.lingkyn.inventory.ugui`

Presenters, Unity UI bindings, nested prefabs, keyboard/pointer sample, and
presentation tests. It does not depend on XR Interaction Toolkit.

### `com.lingkyn.inventory.xr`

Optional world-space UI, tracked-device interaction, XR interaction presets, and
device-validation sample. It depends on Unity UI and XR Interaction Toolkit but
does not redefine domain types.

## XR contract

The XR package defaults to world-space UI. It provides stable ray/direct targets,
visible hover/select/disabled states, predictable focus, and input-module checks.
It must support scene placement independent of head pose; head-locked behavior is
an explicit optional policy, never the default.

Editor simulation proves only configuration and interaction routing. Readability,
reach, occlusion, controller stability, scale, angle, comfort, and headset runtime
remain unproven until recorded on a real target device.

## Explicit non-goals for the first core release

- A complete RPG, crafting, shop, currency, loot, or equipment system.
- A universal network protocol or backend service.
- Product art, product-specific item definitions, or product scenes.
- Mandatory grid shapes, weight, categories, or hotbars.
- Cross-engine implementation claims before another engine has working packages and
  independent evidence.
