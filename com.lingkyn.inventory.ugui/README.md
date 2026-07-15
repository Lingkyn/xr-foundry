# Lingkyn Inventory UGUI

Status: **incubating**. This package presents Inventory Core snapshots through
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

The package demonstrates empty, partial, full, rejected, selected, disabled,
loading, and error states. Pointer and keyboard submit use standard EventSystem
interfaces and do not require XR packages.
