# Inventory UI state gallery

After importing this sample, run **Tools > XR Foundry > Inventory > Create UGUI State
Gallery Sample**. The command creates a screen-space Canvas, an EventSystem when one
does not already exist, and an instance of the shipped `InventoryStateGallery` prefab
with `InventoryStateGalleryBootstrap` attached.

Use the bootstrap's context-menu commands or call `Replay` to show empty, partial,
full, rejected, selected, disabled, loading, and error. Replace the nested
`InventorySlot` or `ItemView` instance with a project style/variant while retaining
the parent prefab links. This sample proves neutral UGUI composition only; it makes no
XR, headset, comfort, or product-art claim.
