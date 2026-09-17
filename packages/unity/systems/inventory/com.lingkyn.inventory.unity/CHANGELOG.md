# Changelog

## Unreleased

- Added the EditMode test `NonPositiveStackLimitAndUniqueStackMismatchReportStableCodes`
  asserting the `item.maximumStack.invalid` and `item.unique.stack` diagnostics,
  their field path, and that conversion throws for both. No runtime change. Editor
  execution is pending.
- Repository validation now enforces the authoring gate's isolation clause: the
  Runtime sources must not call `Resources.Load` or any `FindObject*` scene lookup
  (`validate_inventory_isolation_rules`).

## 0.1.1 - 2026-07-15

- Aligned authoring-package guidance and its Core dependency with the canonical
  nested package family.

## 0.1.0 - 2026-07-15

- Added ScriptableObject item, catalog, container, and inventory authoring assets.
- Added deterministic Core conversion, validation diagnostics, inspectors, tests,
  documentation, and a neutral authoring sample.
