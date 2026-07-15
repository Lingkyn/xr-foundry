# Inventory Presentation contract

`com.lingkyn.inventory.presentation` separates Inventory behavior from any visual
renderer. It consumes domain snapshots and mutations from Inventory Core and exposes
only immutable renderer inputs plus semantic user intent.

## Ports and ownership

- `IInventoryView.Render(InventoryViewModel)` is the renderer output port.
- `InventoryPresenter` subscribes to aggregate changes, derives view state, owns
  selection and disabled behavior, and sends requested mutations to the aggregate.
- `InventorySlotIntent` preserves the stable Core `SlotAddress`; `DisplayIndex` is
  transient presentation order and must not be treated as storage identity.
- Renderers may emit intent, but must not mutate the aggregate directly.

## State contract

The standard states are empty, partial, full, rejected, selected, disabled, loading,
and error. A renderer decides how each state looks and how supported input reaches its
controls. It must not silently reinterpret a semantic state or infer device support.

The runtime assembly is engine-free. Device scale, layout, world-space composition,
ray/poke/gaze input, accessibility, and visual styling belong to renderer and XR
adapter packages and require their own verification.

## Migration

For consumers upgrading from Inventory UGUI `0.1.x` to `0.2.0`, replace imports of
presentation types from `Lingkyn.Inventory.UGUI` with
`Lingkyn.Inventory.Presentation`, reference the `Lingkyn.Inventory.Presentation`
assembly, and keep renderer component imports in `Lingkyn.Inventory.UGUI`.
