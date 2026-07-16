# Inventory Authoring sample

1. Create item definitions from `Assets > Create > Lingkyn > Inventory`.
2. Give every item and container an explicit stable ID that is independent of its
   asset name and folder.
3. Create an item catalog and inventory definition, then assign the referenced assets.
4. Resolve every inspector diagnostic.
5. Convert the catalog and inventory definition at the composition root and keep the
   resulting Core aggregate in consumer-owned runtime code.

The sample intentionally contains no product art, item content, scene singleton,
Resources folder, UI, or XR dependency.
