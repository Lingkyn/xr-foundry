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

## Typed instance state

Create a consumer-owned `ItemStateFragmentCodec<T>` for each admitted state type
and register it with `ItemStateFragmentRegistry`. Codecs own their compact payload
format and must decode every fragment schema the package still supports. Restore
normalizes successfully decoded older fragments to the codec's current schema.

Pass the registry to `InventoryAggregate`, create fragments through
`registry.Create(codec, value)`, and mutate them only with
`MutationRequest.SetInstanceState` or `RemoveInstanceState`. A fragment requires a
unique item instance; fungible stacks cannot carry instance state. Unknown codecs,
future fragment schemas, malformed payloads, and invalid ownership are rejected
without changing the aggregate.

The codec boundary is deliberately provider-neutral. JSON, MessagePack, custom
binary, cloud SDK objects, and Unity serialization adapters belong outside Core.
Core stores an immutable opaque payload only after a registered typed codec has
validated it.

See the repository Inventory standard for architecture and verification gates.
