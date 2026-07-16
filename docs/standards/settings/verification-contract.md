# Player Settings and Accessibility verification contract

## Source gate

- Derivation inputs are positive, public, role-bounded, and license/terms aware.
- Accessibility guidance supplies barrier and discoverability coverage, not a
  compliance claim or a mandatory universal feature set.
- Engine/platform documentation constrains only the matching adapter.

## Core gate

Deterministic tests cover:

- invalid keys, duplicate definitions, kind mismatch, invalid defaults, non-finite
  floats, ranges, steps, options, and string bounds;
- profile layering, duplicate overrides, reset by scope, and stale transactions;
- complete-snapshot cross-setting constraints;
- stable change ordering and no notification for no-op or failed apply;
- partial applicator failure, reverse rollback, and rollback diagnostics;
- persistence success, absence, and `applied_not_persisted` failure;
- unknown stored keys preserved but never treated as registered definitions; and
- accessibility metadata round trip without promoting feature/compliance claims.

The rollback tests must inject failure before the first applicator, after each
intermediate applicator, during reverse rollback, and during persistence. They
assert committed-state revision, effect state, change events, diagnostics and
persistence calls independently; one boolean success assertion is insufficient.

## Unity adapter gate

- ScriptableObject catalogs and profiles convert deterministically to Core.
- Validation reports asset/index/key-specific errors for duplicate IDs, invalid
  value kinds, defaults, ranges, options, metadata, and profile overrides.
- Runtime snapshots do not mutate source assets.
- Consumer-provided applicators are explicit; no scene search or renderer is
  required.

## Exact-consumer gate

- Install Core and Unity packages from the same immutable full commit SHA.
- Record Unity, OS, dependency lock, package versions, testable assemblies, and
  machine-readable results.
- Compile both packages and run all applicable tests in a clean consumer.

## Claim ceiling

Passing automated tests proves only the named data/transaction/authoring tuple. It
does not prove a settings screen, localized copy, remapping, caption rendering,
contrast ratio, audio mix, comfort outcome, platform certification, legal
compliance, or named-device accessibility.
