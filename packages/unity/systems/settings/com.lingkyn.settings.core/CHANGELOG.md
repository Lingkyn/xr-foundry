# Changelog

## [Unreleased]

### Documentation

- Expanded `Documentation~/index.md` with the define/layer/snapshot/stage/apply
  lifecycle, the `SettingsApplyOutcome` table, applicator filtering and rollback
  semantics, and the persistence port. No API, version, or evidence change.

## [0.1.0] - 2026-07-16

### Added

- Typed settings registry, snapshots, transactions, constraints, applicators, rollback, and optional persistence port.
- EditMode contract tests for validation, profiles, reset scopes, stale transactions, rollback boundaries, and persistence outcomes.

### Changed

- `SettingChange` reports explicit presence via `HadOldValue` / `HasNewValue` for accurate reset removals.
- `SettingsTransactionCommand.CreateResetScope` replaces the conflicting `ResetScope` factory name.
