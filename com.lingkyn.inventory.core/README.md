# Lingkyn Inventory Core

Status: **incubating**. Public API compatibility is not yet promised.

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
- atomic add, remove, move, swap, split, merge, and transfer requests;
- optional partial acceptance for add only;
- structured failures, revisions, post-commit events, and immutable snapshots;
- composable pre-commit policies; and
- versioned persistence envelopes.

Unity authoring, nested UI prefabs, and XR interaction are separate package layers.
