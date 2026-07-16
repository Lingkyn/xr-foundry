# Lingkyn Settings Core

Engine-light typed settings core with no `UnityEngine` dependency.

## Public surface

- `SettingKey` / `SettingValue`: closed typed value union (boolean, integer, float, string, option id).
- `SettingDefinition` + `SettingDefinitionValidator`: defaults, scopes, numeric/string/option constraints, and accessibility discoverability metadata.
- `SettingsRegistry` / `SettingsProfile`: immutable definitions and ordered profile override layers.
- `SettingsSnapshot` / `SettingsTransaction`: revisioned snapshots, staged set/reset/profile commands, and stale revision rejection.
- `ISettingsConstraint`: whole-snapshot cross-setting validation before applicators run.
- `ISettingApplicator` + `SettingsCoordinator`: deterministic key-sorted change sets, ordered apply, reverse rollback, and no notifications on no-op or failure.
- `ISettingsSnapshotRepository`: optional typed persistence port with `applied_not_persisted` reporting.

## Non-goals

- No Unity API, file formats, PlayerPrefs, renderer, graphics/audio/input policy, or compliance claims.
- No built-in persistence codec; consumers provide `ISettingsSnapshotRepository`.

## Sample

Import the `TransactionalSettings` sample for staged apply, profile layering, rollback, and persistence-result handling without a renderer.
