# Changelog

## Unreleased

- Added seven EditMode tests named by the Core coverage map: empty-slot and
  over-quantity removal, non-positive stack quantities, zero-quantity Remove, Move,
  Split, Merge, and Transfer requests rejected as `InvalidRequest` without
  mutation, exact stack and capacity boundaries, unknown container and invalid
  slot, occupied transfer destination, and multi-seed invariant sequences with a
  unique instance. No runtime change. Editor execution is pending.

## 0.1.1 - 2026-07-15

- Aligned package guidance with the canonical nested layout and version-adaptive
  evidence contract.

## 0.1.0 - 2026-07-15

- Promoted the first Core candidate after immutable Git install, upgrade from
  `0.1.0-pre.1`, rollback, and clean-consumer tests.

## 0.1.0-pre.1 - 2026-07-15

- Added the independently authored Inventory domain foundation and invariant tests.
- Added provider-neutral persistence state, schema migration contracts, and
  transactional restore validation with structured failure results.
- Added registered typed state-fragment codecs, explicit instance-state mutations,
  fragment-schema normalization, and persistence coverage for unique items.
