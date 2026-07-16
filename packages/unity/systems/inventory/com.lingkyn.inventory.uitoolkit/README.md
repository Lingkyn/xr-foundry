# Lingkyn Inventory UI Toolkit

Status: **incubating**. This package is a renderer adapter for
`com.lingkyn.inventory.presentation`; it does not own Inventory domain or
presentation policy.

`InventoryDocumentView` binds an actual `UIDocument`/`VisualElement` tree to
immutable `InventoryViewModel` state. UI Toolkit buttons emit
`InventorySlotIntent` values containing both the stable Core `SlotAddress` and
the transient display index. The adapter does not mutate an Inventory aggregate.

The shipped `InventoryDocument.uxml` and `InventoryDocument.uss` provide a
functional empty/partial/full/rejected/selected/disabled/loading/error surface,
a bounded `ScrollView`, runtime slot buttons, details, and a primary action. A
consumer can replace the document while preserving the named-element contract
defined by `InventoryDocumentContract`.

This package has no XR Interaction Toolkit dependency and makes no world-space,
headset, controller, comfort, or device claim. Use
`com.lingkyn.inventory.xr.uitoolkit` for the optional XRI world-space
composition.

The State Gallery sample creates a screen-space `UIDocument` explicitly from a
menu command and replays all neutral states. It does not install scenes or
global input objects automatically.

## Git installation

For Git evaluation, explicitly pin Core, Presentation, and UI Toolkit to the same
full repository commit SHA. Package manifest dependency versions express
compatibility; they cannot fetch sibling Git packages from this monorepo
automatically.
