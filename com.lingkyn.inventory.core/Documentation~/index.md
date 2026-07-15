# Inventory Core 0.1

Create an `ItemDefinitionCatalog`, then construct an `InventoryAggregate` with one
or more `ContainerDefinition` values. Mutate the aggregate only through
`MutationRequest` factories and inspect `MutationResult` before updating a view or
neighboring system.

The package does not persist itself. Capture an `InventorySnapshot` in a
`PersistenceEnvelope` and let a consumer-owned adapter choose JSON, binary, cloud,
or server storage. Schema migrations belong in that adapter until a generic
migration mechanism is proven.

See the repository Inventory standard for architecture and verification gates.
