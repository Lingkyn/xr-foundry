# Lingkyn Inventory Unity Authoring

Status: **incubating**. This package is the designer-facing ScriptableObject adapter
for Inventory Core. Its canonical nested Git install and automated consumer tests
pass; candidate promotion now waits for formal release review. It contains no
UGUI, XR, scene, service, save-provider, or product-content dependency.

## Assets

- `ItemDefinitionAsset`: explicit stable ID, stack limit, instance mode, and tags;
- `ItemCatalogAsset`: deterministic item-definition collection and Core catalog conversion;
- `ContainerDefinitionAsset`: explicit stable container ID and capacity; and
- `InventoryDefinitionAsset`: inventory ID plus referenced container assets.

IDs are serialized fields, not asset names or paths. Renaming or moving an asset
does not change persistence identity. Conversion creates new immutable Core domain
objects, so runtime mutations cannot write back into authored assets.

Use the custom inspectors or `Tools/XR Foundry/Inventory/Validate All Authoring
Assets` for diagnostics that include the asset path, field, code, and corrective
message. No Resources folder, scene singleton, service locator, or mandatory
project folder is required. Local and canonical nested Git consumer evidence
covers the current content on Unity `6000.3.19f1` at revision
`b3d4b8dfd3ae9f6025026bc6737eb10cacbd894f`.

When installing from this Git monorepo, pin both `com.lingkyn.inventory.core` and
`com.lingkyn.inventory.unity` to the same reviewed revision in the consumer
manifest. Unity package manifests cannot fetch another Git package dependency by
repository URL on the package's behalf.
