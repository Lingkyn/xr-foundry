# Inventory Core API contract

## Baseline

`com.lingkyn.inventory.core@0.1.0` is the previous public baseline. The current
`0.1.1` package declares no public type removal or persisted-schema break from that
tag, but it receives no inherited install or test verdict. Its immutable revision
must resolve in an independent consumer, preserve or explicitly migrate public and
persisted contracts, and support a reviewed rollback path before promotion.

The machine-readable type list lives in `core-api-baseline.json`. Repository
validation compares that list with public Runtime declarations so a public type
cannot silently appear or disappear without a reviewed baseline change.

## Compatibility policy

Before `1.0`, semantic-versioning rules are applied as follows:

- patch releases preserve source and persisted-state compatibility;
- minor releases may change an incubating API only with migration notes, an
  upgrade/rollback receipt, and an updated baseline reviewed in the same change;
- candidate releases freeze the admitted surface for their release line;
- removals require a deprecation cycle unless a security or data-loss risk makes
  immediate removal necessary and that exception is documented;
- definition IDs, instance IDs, container IDs, fragment type IDs, aggregate schema
  versions, and fragment schema versions are persistence contracts, not display text;
- supported aggregate and fragment schemas must migrate deterministically or fail
  explicitly without mutating live state.

Additive overloads and new result fields still require review because they may
change overload resolution, serializers, or pattern-matching consumers. Enum values
are append-only within a compatible release line; numeric values are never reused.

## Boundary review

The prerelease public surface has four bounded groups:

1. identities, definitions, stacks, snapshots, containers, and aggregate mutations;
2. policies, structured results, revisions, and post-commit events;
3. provider-neutral persistence state, aggregate migrations, and transactional restore;
4. registered typed instance-state codecs and fragment schema normalization.

The Runtime assembly intentionally excludes UnityEngine, UnityEditor, UI, XR,
scenes, storage providers, service SDKs, equipment, crafting, commerce, networking,
and consumer namespaces. Those integrations belong in later packages or public seams.

## Current promotion gate

The current package may advance only when all of the following are recorded:

- the previous public tag and immutable commit exist;
- a clean consumer installs and tests the current immutable package revision;
- public API and persisted-state comparison against `0.1.0` is reviewed;
- rollback to the previous public reference resolves without corrupting consumer
  state or package resolution;
- public API and persistence migration notes match the actual delta; and
- catalogs, package docs, changelog, and the verification ledger agree.
