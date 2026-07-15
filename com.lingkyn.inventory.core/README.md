# Lingkyn Inventory Core

Status: **candidate**. Version `0.1.0` preserves the `0.1.0-pre.1` public API and
persistence baseline and has passed the clean-consumer install, upgrade, and
rollback gate. This candidate status applies only to Inventory Core.

Inventory Core is an engine-light domain package for reusable Inventory behavior.
It contains identities, definitions, stacks, containers, atomic mutations,
policies, snapshots, events, and persistence envelopes. It contains no UI, XR,
scene, service SDK, or consumer-specific dependency.

The package is independently authored from the positive public evidence in
[`docs/standards/inventory`](../docs/standards/inventory/README.md). It is not a
copy of a consumer project or commercial Inventory implementation.

## Current scope

- fixed-capacity slot containers;
- fungible stacks and unique runtime instances;
- typed, schema-versioned state fragments for unique instances;
- atomic add, remove, move, swap, split, merge, and transfer requests;
- optional partial acceptance for add only;
- structured failures, revisions, post-commit events, and immutable snapshots;
- composable pre-commit policies; and
- provider-neutral persistence state, explicit schema migrations, and transactional
  restore with structured failures.

Persistence providers remain consumer-owned. The Core package exports immutable
primitive state and validates migrations/restores, but it does not select JSON,
disk, cloud, authentication, or server storage.

Per-instance state is created and read through registered
`ItemStateFragmentCodec<T>` implementations. The stored fragment is immutable and
contains a stable type ID, schema version, and codec-owned payload. This keeps
durability, quality, binding, ammunition, or similar data typed and migratable
without exposing a universal dictionary of arbitrary objects. State changes use
`MutationRequest.SetInstanceState` or `RemoveInstanceState`, so they follow the
same policy, atomic commit, revision, and event path as placement mutations.

Unity authoring, nested UI prefabs, and XR interaction are separate package layers.

See the [Core API contract](../docs/standards/inventory/core-api-contract.md) for
the prerelease compatibility, persistence, deprecation, and promotion policy.
