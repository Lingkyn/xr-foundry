# Inventory UGUI composition

Instantiate `InventoryShell.prefab` or compose the smaller role prefabs yourself.
The renderer-neutral `Lingkyn.Inventory.Presentation.InventoryPresenter` owns
aggregate commands; UGUI view components receive immutable models and emit
selection/activation intent. Replace a slot or item nested instance with a
prefab variant without unpacking the shell.

`InventoryShell` is deliberately Canvas-neutral. Put it under a consumer-owned Canvas;
its shipped nested children already include neutral text, deterministic layout,
raycastable Selectables, and visible state feedback. Do not rebuild package prefabs
inside an immutable PackageCache. The editor rebuild command is for package authors
working from a writable local package checkout only.

`InventoryGridView.ActivationRequested` and `SelectionRequested` expose
`InventorySlotIntent`. Route domain work by `InventorySlotIntent.Address`; the display
index is only presentation order. The shipped grid uses a masked `ScrollRect`, so the
three-column viewport remains bounded while its content grows. A replaced slot prefab
must be rebound before the first render; invalid configuration throws immediately.
Its preferred dimensions become the grid cell size. Hidden pooled slots are unbound,
so their address, item label, and interactable state cannot leak from an earlier model.

`InventoryStateGallery` can replay every required presentation state without a
product database or XR package. World-space Canvas and tracked-device interaction
belong to the optional XR adapter.

Presentation contracts live in `Lingkyn.Inventory.Presentation`; any assembly that
uses them directly references `Lingkyn.Inventory.Presentation`. UGUI components
remain in `Lingkyn.Inventory.UGUI`. The renderer does not depend on the optional
Unity authoring package.
