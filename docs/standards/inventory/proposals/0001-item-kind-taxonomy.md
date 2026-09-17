# Proposal 0001: Item Kind as a closed classification beside open Tags

Status: **proposed, awaiting maintainer decision**
Resolves: [Issue #85](https://github.com/Lingkyn/xr-foundry/issues/85),
[`LESSON-001`](../../lessons/README.md)
Affects: `com.lingkyn.inventory.core`, `com.lingkyn.inventory.presentation`,
`docs/standards/inventory/architecture-contract.md`

## Problem

A consumer building a creation-library backpack needed a stable, closed item
classification (for example `model`, `audio`, `light`, `motion`, `interaction`) to
drive presentation filters, grouping, and icon routing. Inventory Core offers only:

- `ItemDefinition.Tags`, an open list of strings with no registry, ordering, or
  uniqueness contract; and
- the `category_or_tag` policy seam, which governs whether a placement is accepted,
  not how items are classified for display.

The consumer therefore invented a parallel kind system outside the package, which is
the outcome the family exists to prevent. The public API compatibility review for
Core cannot proceed while this gap is undecided, because the decision changes the
shape of `ItemDefinition`.

## Options

### A. Core kind identity plus optional profile (recommended)

- Core adds `ItemKindId`, a validated identity with the same character and length
  rules as `ItemDefinitionId`, and an optional single `Kind` on `ItemDefinition`.
  `Tags` stays open metadata. A definition may have a kind, tags, both, or neither.
- Core adds an `IItemKindRegistry` port: a consumer-supplied closed set that
  catalogs validate against when present. Core ships no registry content.
- Presentation adds read-only `FilterByKind(ItemKindId)` and `ClearKindFilter`
  intents and a `Kind` field on `InventorySlotViewModel`. Chrome (tabs, rails,
  icons) stays in renderer adapters and the design language.
- The architecture contract gains one paragraph distinguishing kind (closed
  classification), tags (open metadata), and `category_or_tag` (placement policy).
- An optional profile document under `docs/standards/inventory/profiles/` records a
  recommended starter set for creation libraries. Consumers extend through the
  registry, not by forking Core.

Cost: Core `0.1.1` to `0.2.0` (additive, nullable field, no removed member),
Presentation `0.1.0` to `0.2.0`, Unity authoring gains one optional field on the
item asset, and the coverage matrix gains one row. UGUI and UI Toolkit adapters are
not required to change until they adopt the filter intents.

### B. Presentation-only filter contract

Presentation derives kind from a consumer-supplied `Func<ItemDefinition, string>`
and exposes the same filter intents; Core stays unchanged.

Cost is smaller, but the classification then lives outside the persisted domain
and cannot be validated at catalog conversion time, so two consumers still spell
kinds differently and persistence migrations cannot reason about kind.

### C. Defer

Record the gap in the coverage matrix and revisit after the candidate release.

Cost: the candidate API review would freeze `ItemDefinition` without the field,
and adding it later forces the breaking change the family is trying to avoid.

## Recommendation

Adopt option A in two steps. Step one lands Core `ItemKindId`, the optional `Kind`,
and the registry port with tests for validation, catalog conversion, persistence
round trip, and migration from a kind-less envelope. Step two lands the
Presentation intents and the profile document. Neither step admits any consumer's
kind set into Core.

## Source check required before admission

The coverage matrix already cites category and tag acceptance from the admitted
professional benchmarks in the "Restrictions and placement" row. Before option A
is admitted, the source manifest must record which admitted positive sources
distinguish a closed item category from open tags, so the closed-kind decision
rests on positive public evidence rather than on one consumer's request. If no
admitted source supports a closed classification, option B is the fallback.

## Decision record

| Field | Value |
| --- | --- |
| Decision | pending |
| Decided by | maintainer |
| Decided at | pending |
| Follow-up checkpoints | pending |
