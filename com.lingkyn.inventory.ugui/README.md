# Lingkyn Inventory UGUI

Maturity and exact evidence are recorded in the repository package catalog. Version
`0.1.1` is the corrective functional-prefab release line. This package presents Inventory Core snapshots through
replaceable presenters and UGUI views. It contains no XR Interaction Toolkit or
consumer-specific styling, content, localization, scene, or item database.

The shipped composition is a real nested-prefab chain, not one irreducible root:

```text
InventoryShell
`- InventoryPanel
   |- InventoryGrid
   |  `- InventorySlot
   |     `- ItemView
   |- ItemDetails
   `- ActionMenu
```

Every named role is a separate prefab asset. The slot variant and item view can be
replaced independently while the parent prefab retains its nested links. Views receive
immutable view models and emit intent; only `InventoryPresenter` can send mutations
to the aggregate.

The shipped `0.1.1` chain is functional rather than structure-only: serialized view
references are wired, the grid materializes snapshot slots, labels and UI states are
visible, large inventories stay inside a masked vertical scroll view, slot and action
targets use standard UGUI raycasting/selection, and disabled targets suppress activation.
Slot intents carry both a stable Core `SlotAddress` and a transient display index; use
the address when routing selection or commands. The shell intentionally contains no Canvas or EventSystem,
so a consumer can place the same composition under screen-space or optional XR surfaces.

When replacing a slot template on an instantiated grid, assign the replacement before
the first render (or call `ConfigureTemplate`). Missing or detached templates fail fast
instead of reporting a false active-slot count. The grid adopts the template's preferred
size, so the shipped compact prefab variant remains observably compact under layout.
Pooled slots are unbound while hidden and expose no stale address or item content.
`InventoryPresenter.Execute` also fails
explicitly while the presenter is disabled; it never returns a null failure result.
Disabled state remains sticky across refreshes and external aggregate events, and selection
or replay requests fail explicitly until the presenter is enabled again.

The package demonstrates empty, partial, full, rejected, selected, disabled,
loading, and error states. Pointer and keyboard submit use standard EventSystem
interfaces and do not require XR packages. Candidate promotion requires prefab-backed
EditMode and PlayMode tests plus an immutable Git consumer whose manifest contains no
XR package.
