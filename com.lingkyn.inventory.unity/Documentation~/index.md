# Inventory Unity Authoring

Create explicit stable IDs before adding assets to catalogs. Validate the assets in
their custom inspectors or from the XR Foundry Inventory validation menu. Conversion
is deliberate: `ItemCatalogAsset.ToDomain()` and `InventoryDefinitionAsset.ToDomain()`
produce new Core values and never bind the aggregate to mutable authoring assets.

Diagnostics identify the source asset, serialized field path, stable machine code,
and corrective message. Treat error codes as automation contracts; display text may
improve between compatible releases.

This package does not store player state in ScriptableObjects. Persist snapshots
through the Core persistence contract and a consumer-selected storage adapter.
