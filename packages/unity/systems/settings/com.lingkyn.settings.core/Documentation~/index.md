# Lingkyn Settings Core

`com.lingkyn.settings.core` provides engine-light typed settings, profile
layering, transactional apply with rollback, and an optional persistence port. It
has no `UnityEngine` dependency, and its namespaces start with
`Lingkyn.Settings.Core`.

Accessibility metadata is discoverability-only and makes no compliance or device
claims. See the package `README.md` for the public surface and non-goals, and the
Settings family standard under `docs/standards/settings/` for the source-derived
boundary.

## Lifecycle

1. **Define.** Build `SettingDefinition` values (key, value kind, typed default,
   default scope, application order, restart flag, numeric/string/option
   constraint, accessibility metadata) and freeze them with
   `SettingsRegistry.Create(definitions)`. Validation rejects invalid keys,
   duplicate definitions, kind mismatches, non-finite floats, out-of-range
   defaults, invalid steps, unknown options, and over-long strings with a
   `SettingsValidationCode`.
2. **Layer.** A `SettingsProfile` is an ordered list of `SettingsProfileLayer`
   overrides keyed by `SettingKey`. A layer rejects duplicate overrides at
   construction. Profiles are data and change nothing until staged.
3. **Snapshot.** `SettingsSnapshot.CreateInitial(registry, revision)` yields the
   revisioned committed state. Snapshots are immutable and keyed by
   `ScopedSettingKey`; `SettingScope` is `Global`, `User`, `Profile`, or
   `Session`.
4. **Stage.** `SettingsCoordinator.BeginTransaction()` captures the committed
   revision. `StageSet`, `StageReset(scope)`, and `StageProfile(profile)` append
   commands in order. Nothing is applied yet, and a transaction that is never
   applied has no effect.
5. **Apply.** `SettingsCoordinator.Apply(transaction)` materializes the candidate
   snapshot, validates it against the registry and every `ISettingsConstraint`,
   computes a key-sorted `SettingChange` list, runs applicators in ascending
   `Order`, increments the revision, raises `ChangesApplied`, then persists
   through the optional repository.

`StageReset(scope)` removes every known value in that scope and restores the
registry defaults whose default scope matches. `StageProfile` applies the
profile's layers in order on top of the staged state.

## Apply outcomes

| `SettingsApplyOutcome` | Meaning | Committed snapshot |
| --- | --- | --- |
| `Applied` | Applicators succeeded and the repository saved, or no repository is configured | Advanced |
| `AppliedNotPersisted` | Applicators succeeded; repository `Save` failed and `PersistenceMessage` explains why | Advanced |
| `NoOp` | The candidate equals the committed snapshot | Unchanged, no notification |
| `StaleTransaction` | The transaction `BaseRevision` no longer matches the committed revision | Unchanged |
| `ValidationFailed` | A key, scope, definition, or cross-setting constraint rejected the candidate; see `ValidationError` | Unchanged |
| `ApplicatorFailed` | One applicator failed and every earlier applicator rolled back cleanly in reverse order; see `PrimaryFailure` | Unchanged |
| `RollbackFailed` | An applicator failed and at least one rollback step also failed; `RollbackDiagnostics` lists each failure | Unchanged in Core; live consumer state needs inspection |

Each applicator receives only the changes whose keys pass its `CanApply`, and an
applicator with no matching changes is skipped. A failed apply or rollback never
raises `ChangesApplied` and never advances the revision.

## Persistence port

`ISettingsSnapshotRepository` is an optional typed port with `Load()` and
`Save(snapshot)`. `SettingsCoordinator.LoadFromRepository()` validates the loaded
snapshot against the registry and constraints before it replaces the committed
state; an invalid stored snapshot is rejected rather than partially adopted. Core
ships no codec, file format, or PlayerPrefs binding. Consumers supply the
repository and own its storage semantics.

## Sample

Import the `TransactionalSettings` sample and call
`TransactionalSettingsExample.Run()` from an EditMode test or bootstrap harness.
It demonstrates staged apply, profile layering, rollback, and persistence-result
handling without a renderer.
