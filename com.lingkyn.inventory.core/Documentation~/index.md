# Inventory Core 0.1

Create an `ItemDefinitionCatalog`, then construct an `InventoryAggregate` with one
or more `ContainerDefinition` values. Mutate the aggregate only through
`MutationRequest` factories and inspect `MutationResult` before updating a view or
neighboring system.

The package does not choose a storage provider. Call `CreatePersistenceEnvelope()`
to obtain provider-neutral primitive state, then let a consumer-owned adapter choose
JSON, binary, cloud, or server storage. Restore through `InventoryAggregate.Restore`;
the operation validates the complete state before replacing the aggregate and leaves
the previous state unchanged on failure.

Schema changes are explicit. Supply one `IInventoryStateMigration` for each
supported forward step. Missing, ambiguous, failed, or future-schema migrations
return a structured `InventoryRestoreResult` rather than partially loading data.
Migration code may rename or reshape persisted identifiers, but it cannot bypass
definition, container, capacity, stack, or duplicate-instance validation.

See the repository Inventory standard for architecture and verification gates.
