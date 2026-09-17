# Settings public API surface

Status: inventory for the public API compatibility review. The family is
incubating; nothing in this file is a stability promise. Every entry is derived
from the `Runtime/` sources at the revision that carries this file. Internal and
private members are omitted.

Packages inventoried:

| Package | Version | Assembly (asmdef) | References | Engine references | Dependencies |
| --- | --- | --- | --- | --- | --- |
| `com.lingkyn.settings.core` | 0.1.0 | `Lingkyn.Settings.Core` | none | `noEngineReferences: true` | none |
| `com.lingkyn.settings.unity` | 0.1.0 | `Lingkyn.Settings.Unity` | `Lingkyn.Settings.Core` | `noEngineReferences: false` | `com.lingkyn.settings.core` 0.1.0 |

Both assemblies declare `InternalsVisibleTo` for their own `*.Editor.Tests`
assembly only.

Consumer-facing: `yes` means a consumer calls or reads it directly; `seam` means
a consumer implements or extends it; `no` means it exists to support another
public member.

## `com.lingkyn.settings.core` (namespace `Lingkyn.Settings.Core`)

35 public types. `SettingsReadOnly` is internal and not part of the surface.

| Type | Kind | Public members | Consumer-facing |
| --- | --- | --- | --- |
| `SettingValueKind` | enum | `Boolean = 0`, `Integer = 1`, `Float = 2`, `String = 3`, `Option = 4` | yes |
| `SettingScope` | enum | `Global = 0`, `User = 1`, `Profile = 2`, `Session = 3` | yes |
| `SettingsApplyOutcome` | enum | `Applied = 0`, `NoOp = 1`, `ValidationFailed = 2`, `ApplicatorFailed = 3`, `RollbackFailed = 4`, `StaleTransaction = 5`, `AppliedNotPersisted = 6` | yes |
| `SettingsValidationCode` | enum | `None = 0`, `InvalidKey`, `DuplicateDefinition`, `KindMismatch`, `InvalidDefault`, `NonFiniteFloat`, `OutOfRange`, `InvalidStep`, `UnknownOption`, `StringTooLong`, `DuplicateProfileOverride`, `CrossConstraintViolation`, `InvalidProfileLayer`, `InvalidScope` | yes |
| `SettingKey` | readonly struct, `IEquatable`, `IComparable` | prop `Value`; static `TryCreate(string)` returning `SettingsResult<SettingKey>`; `Equals`, `GetHashCode`, `CompareTo`, `ToString` | yes |
| `OptionId` | readonly struct, `IEquatable`, `IComparable` | prop `Value`; static `TryCreate(string)`; `Equals`, `GetHashCode`, `CompareTo`, `ToString` | yes |
| `SettingValue` | readonly struct, `IEquatable` | props `Kind`, `BooleanValue`, `IntegerValue`, `FloatValue`, `StringValue`, `OptionValue`; static `FromBoolean(bool)`, `FromInteger(long)`, `FromFloat(double)`, `FromString(string)`, `FromOption(OptionId)`; `Equals`, `GetHashCode` | yes |
| `SettingScopeValidator` | static class | `Validate(SettingScope)` returning `SettingsResult` | no |
| `ScopedSettingKey` | readonly struct, `IEquatable`, `IComparable` | ctor `(SettingKey, SettingScope)` (unvalidated); props `Key`, `Scope`; static `TryCreate(SettingKey, SettingScope)`; `Equals`, `GetHashCode`, `CompareTo` | yes |
| `SettingsValidationError` | readonly struct | ctor `(SettingsValidationCode, string, SettingKey = default)`; props `Code`, `Message`, `Key` | yes |
| `SettingsResult` | readonly struct | props `Succeeded`, `Error`; static `Success()`, `Fail(SettingsValidationCode, string, SettingKey = default)` | yes |
| `SettingsResult<T>` | readonly struct | props `Succeeded`, `Value`, `Error`; static `Success(T)`, `Fail(SettingsValidationCode, string, SettingKey = default)` | yes |
| `NumericConstraint` | readonly struct | ctor `(double minInclusive, double maxInclusive, double step)`; props `MinInclusive`, `MaxInclusive`, `Step`, `HasStep` | yes |
| `StringConstraint` | readonly struct | ctor `(int maxLength)`; prop `MaxLength` | yes |
| `OptionConstraint` | sealed class | ctor `(IEnumerable<OptionId>)` (throws `ArgumentException` on empty, invalid or duplicate ids); prop `Allowed`; `IsAllowed(OptionId)` | yes |
| `AccessibilityMetadata` | readonly struct | ctor `(string category, string featureId, string titleKey, string descriptionKey, bool previewSupported, bool availableBeforeGameplay, string documentationKey)`; props of the same names plus `IsEmpty` | yes |
| `SettingDefinition` | sealed class (internal ctor) | props `Key`, `Kind`, `DefaultValue`, `DefaultScope`, `ApplicationOrder`, `RequiresRestart`, `NumericConstraint` (nullable), `StringConstraint` (nullable), `OptionConstraint`, `Accessibility` | yes (read) |
| `SettingDefinitionValidator` | static class | `ValidateBuilt(SettingKey, SettingValueKind, SettingValue, SettingScope, int applicationOrder, bool requiresRestart, NumericConstraint?, StringConstraint?, OptionConstraint, AccessibilityMetadata)` returning `SettingsResult<SettingDefinition>`; `ValidateValue(SettingDefinition, SettingValue)` returning `SettingsResult`; `ValidateNumericConstraint(NumericConstraint, SettingKey = default)` returning `SettingsResult` | yes (`ValidateBuilt` is the only way to obtain a `SettingDefinition`) |
| `SettingsRegistry` | sealed class (private ctor) | prop `Definitions`; `TryGetDefinition(SettingKey, out SettingDefinition)`; static `Create(IEnumerable<SettingDefinition>)` returning `SettingsResult<SettingsRegistry>` | yes |
| `SettingsProfileLayer` | sealed class | ctor `(string layerId, IReadOnlyDictionary<SettingKey, SettingValue>)` (throws `ArgumentException` on empty id, invalid or duplicate key); prop `LayerId`, `Overrides`; `TryGetOverride(SettingKey, out SettingValue)` | yes |
| `SettingsProfile` | sealed class (private ctor) | props `ProfileId`, `Layers`; static `Create(string profileId, IEnumerable<SettingsProfileLayer>)` returning `SettingsResult<SettingsProfile>` | yes |
| `SettingsSnapshot` | sealed class | ctor `(long revision, IReadOnlyDictionary<ScopedSettingKey, SettingValue> knownValues, IReadOnlyDictionary<string, SettingValue> unknownValues)` (throws on empty unknown key); prop `Revision`; `TryGetKnownValue(ScopedSettingKey, out SettingValue)`; `TryGetUnknownValue(string, out SettingValue)`; props `KnownValues`, `UnknownValues` (each getter returns a fresh sorted copy); static `CreateInitial(SettingsRegistry, long revision = 0)` | yes |
| `SettingsSnapshotValidator` | static class | `ValidateLoaded(SettingsRegistry, SettingsSnapshot)`; `ValidateLoaded(SettingsRegistry, SettingsSnapshot, IEnumerable<ISettingsConstraint>)`; both return `SettingsResult<SettingsSnapshot>` | yes (repository implementers) |
| `SettingChange` | readonly struct | ctor `(ScopedSettingKey, bool hadOldValue, SettingValue oldValue, bool hasNewValue, SettingValue newValue, SettingDefinition)`; props `ScopedKey`, `Key`, `Scope`, `HadOldValue`, `OldValue`, `HasNewValue`, `NewValue`, `Definition` | yes (applicator input) |
| `SettingsTransactionCommandKind` | enum | `Set = 0`, `ResetScope = 1`, `ApplyProfile = 2` | no |
| `SettingsTransactionCommand` | readonly struct | ctor `(SettingsTransactionCommandKind, ScopedSettingKey, SettingValue, SettingScope resetScope, SettingsProfile)`; props `Kind`, `ScopedKey`, `Value`, `ResetScope`, `Profile`; static `Set(ScopedSettingKey, SettingValue)`, `CreateResetScope(SettingScope)`, `ApplyProfile(SettingsProfile)` | no |
| `SettingsTransaction` | sealed class (internal ctor) | props `BaseRevision`, `Commands`; `StageSet(ScopedSettingKey, SettingValue)`; `StageReset(SettingScope)`; `StageProfile(SettingsProfile)` (throws on null) | yes |
| `SettingsApplicatorDiagnostic` | readonly struct | ctor `(string applicatorId, string message)`; props `ApplicatorId`, `Message` | yes |
| `SettingsApplicatorStepResult` | readonly struct | props `Succeeded`, `Diagnostic`; static `Success()`, `Fail(string applicatorId, string message)` | seam (applicator output) |
| `ISettingApplicator` | interface | props `ApplicatorId`, `Order`; `CanApply(SettingKey)`; `Apply(IReadOnlyList<SettingChange>)` and `Rollback(IReadOnlyList<SettingChange>)` returning `SettingsApplicatorStepResult` | seam |
| `ISettingsConstraint` | interface | prop `ConstraintId`; `Validate(SettingsRegistry, SettingsSnapshot candidate)` returning `SettingsResult` | seam |
| `SettingsPersistResult` | readonly struct | props `Succeeded`, `Message`; static `Success()`, `Fail(string)` | seam (repository output) |
| `ISettingsSnapshotRepository` | interface | `Load()` returning `SettingsResult<SettingsSnapshot>`; `Save(SettingsSnapshot)` returning `SettingsPersistResult` | seam |
| `SettingsApplyResult` | readonly struct | props `Outcome`, `CommittedRevision`, `Changes`, `ValidationError`, `PrimaryFailure`, `RollbackDiagnostics`, `PersistenceMessage`; static `NoOp(long)`, `Stale(long)`, `ValidationFailed(long, SettingsValidationError)`, `ApplicatorFailed(long, SettingsApplicatorDiagnostic, IReadOnlyList<SettingsApplicatorDiagnostic>)`, `RollbackFailed(...)` (same shape), `Applied(long, IReadOnlyList<SettingChange>)`, `AppliedNotPersisted(long, IReadOnlyList<SettingChange>, string)` | yes (read); factories: no |
| `SettingsCoordinator` | sealed class | ctor `(SettingsRegistry, SettingsSnapshot initialSnapshot, IEnumerable<ISettingApplicator> = null, IEnumerable<ISettingsConstraint> = null, ISettingsSnapshotRepository = null)` (throws on null registry or snapshot); props `CommittedSnapshot`, `Registry`; event `ChangesApplied` (`Action<IReadOnlyList<SettingChange>>`); `BeginTransaction()` returning `SettingsTransaction`; `Cancel(SettingsTransaction)`; `Apply(SettingsTransaction)` returning `SettingsApplyResult`; `LoadFromRepository()` returning `SettingsResult<SettingsSnapshot>` | yes |

## `com.lingkyn.settings.unity` (namespace `Lingkyn.Settings.Unity`)

17 public types. The authoring records and assets expose public serialized
fields (lower-camel-case) rather than properties.

| Type | Kind | Public members | Consumer-facing |
| --- | --- | --- | --- |
| `SettingValueKindRecord` | enum | `Boolean = 0`, `Integer = 1`, `Float = 2`, `String = 3`, `Option = 4` (converted to `SettingValueKind` by integer cast) | yes (authoring) |
| `SettingScopeRecord` | enum | `Global = 0`, `User = 1`, `Profile = 2`, `Session = 3` (converted to `SettingScope` by integer cast) | yes (authoring) |
| `AccessibilityMetadataRecord` | `[Serializable]` sealed class | fields `category`, `featureId`, `titleKey`, `descriptionKey`, `previewSupported`, `availableBeforeGameplay`, `documentationKey` | yes (authoring) |
| `NumericConstraintRecord` | `[Serializable]` sealed class | fields `enabled`, `minInclusive`, `maxInclusive`, `step`, `hasStep` | yes (authoring) |
| `StringConstraintRecord` | `[Serializable]` sealed class | fields `enabled`, `maxLength` | yes (authoring) |
| `OptionEntryRecord` | `[Serializable]` sealed class | field `optionId` | yes (authoring) |
| `OptionConstraintRecord` | `[Serializable]` sealed class | field `options` (`OptionEntryRecord[]`) | yes (authoring) |
| `SettingDefinitionAsset` | sealed class, `ScriptableObject`, `[CreateAssetMenu]` | fields `key`, `kind`, `defaultBoolean`, `defaultInteger`, `defaultFloat`, `defaultString`, `defaultOptionId`, `defaultScope`, `applicationOrder`, `requiresRestart`, `numericConstraint`, `stringConstraint`, `optionConstraint`, `accessibility` | yes (authoring) |
| `SettingsCatalogAsset` | sealed class, `ScriptableObject`, `[CreateAssetMenu]` | field `definitions` (`SettingDefinitionAsset[]`) | yes (authoring) |
| `ProfileOverrideRecord` | `[Serializable]` sealed class | fields `definition`, `kind`, `booleanValue`, `integerValue`, `floatValue`, `stringValue`, `optionId` | yes (authoring) |
| `ProfileLayerRecord` | `[Serializable]` sealed class | fields `layerId`, `overrides` | yes (authoring) |
| `SettingsProfileAsset` | sealed class, `ScriptableObject`, `[CreateAssetMenu]` | fields `profileId`, `layers` | yes (authoring) |
| `SettingsUnityValidationIssue` | readonly struct | ctor `(string assetPath, int index, string key, string message)`; props `AssetPath`, `Index`, `Key`, `Message` | yes (read) |
| `SettingsUnityConverter` | static class | `ConvertCatalog(SettingsCatalogAsset)` returning `SettingsResult<SettingsRegistry>`; `ConvertDefinition(SettingDefinitionAsset, string assetPath = null, int index = -1)` returning `SettingsResult<SettingDefinition>`; `ConvertProfile(SettingsProfileAsset, SettingsRegistry, string assetPath = null)` returning `SettingsResult<SettingsProfile>`; `ConvertAccessibility(AccessibilityMetadataRecord)` returning `AccessibilityMetadata` | yes |
| `SettingsUnityValidator` | static class | `ValidateCatalog(SettingsCatalogAsset)` returning `IReadOnlyList<SettingsUnityValidationIssue>` | yes |
| `SettingsUnityFactoryConfig` | sealed class | settable props `Catalog`, `Applicators`, `Constraints`, `Repository`, `InitialRevision`, `UseDefaultsOnRepositoryLoadFailure` | yes |
| `SettingsUnityFactory` | static class | `CreateCoordinator(SettingsUnityFactoryConfig)` returning `SettingsResult<SettingsCoordinator>` | yes |

## Seams a consumer extends

All seams are interfaces; the family exposes no abstract classes.

| Seam | Implemented by a consumer when | Shipped implementations |
| --- | --- | --- |
| `ISettingApplicator` | any setting must take effect on an engine, audio, graphics, input or product system | none (the Unity adapter registers consumer-provided applicators) |
| `ISettingsConstraint` | a rule spans more than one setting | none |
| `ISettingsSnapshotRepository` | committed snapshots must be loaded or persisted (for example through a Persistence-family adapter) | none |

`SettingsCoordinator.ChangesApplied` is the only event; it is raised after the
committed snapshot is replaced and before the repository is asked to save.

## Types that must stay binary-compatible across a release

| Type | Reason |
| --- | --- |
| `SettingKey`, `OptionId`, `SettingValue`, `SettingValueKind`, `SettingScope`, `ScopedSettingKey` | The typed vocabulary every consumer, applicator, constraint and repository handles; also the values a repository serializes. |
| `SettingsResult`, `SettingsResult<T>`, `SettingsValidationError`, `SettingsValidationCode` | Returned by every public operation and crossed by every seam. |
| `ISettingApplicator`, `ISettingsConstraint`, `ISettingsSnapshotRepository` | Consumer implementations break on any added or changed member. |
| `SettingChange`, `SettingsApplicatorStepResult`, `SettingsApplicatorDiagnostic`, `SettingsPersistResult` | Appear in seam signatures. |
| `SettingsSnapshot`, `SettingsRegistry`, `SettingDefinition` | Cross the constraint and repository seams; `SettingsSnapshot` has a public constructor that repository implementers must call. |
| `SettingsApplyResult`, `SettingsApplyOutcome` | The read side is the outcome contract of `Apply`; consumers branch on `Outcome`. |
| `SettingsCoordinator`, `SettingsTransaction` | Primary consumer entry points. |
| `NumericConstraint`, `StringConstraint`, `OptionConstraint`, `AccessibilityMetadata`, `SettingDefinitionValidator.ValidateBuilt` | The only path to a `SettingDefinition`; used by the Unity converter and by engine-light consumers. |
| `SettingsProfile`, `SettingsProfileLayer` | Staged through `SettingsTransaction.StageProfile`. |
| Unity asset and record field names, `SettingValueKindRecord`, `SettingScopeRecord` | Renaming, reordering or renumbering breaks authored `.asset` files and the integer-cast conversion to the Core enums. |
| `SettingsUnityFactoryConfig`, `SettingsUnityFactory`, `SettingsUnityConverter`, `SettingsUnityValidator` | Documented Unity entry points. |

## Candidates for internal or sealed before the first release

| Type or member | Observation |
| --- | --- |
| `SettingsTransactionCommand` public constructor | Any kind/payload combination can be built, but only `SettingsTransaction.Stage*` can add a command to a transaction. Nothing outside `Runtime/` constructs one. The struct can stay readable with an internal constructor and internal factories. |
| `SettingsTransactionCommandKind` | Only meaningful with the command struct; follows it. |
| `SettingsApplyResult` static factories | Only `SettingsCoordinator` produces results; nothing outside `Runtime/` calls a factory. Making them internal keeps the read side public. |
| `SettingScopeValidator` | One-line `Enum.IsDefined` wrapper used by the coordinator and `ScopedSettingKey.TryCreate`; nothing outside `Runtime/` references it. |
| `ScopedSettingKey` public constructor | Bypasses `TryCreate`; the coordinator re-validates scope on every candidate because of it. |
| `SettingsSnapshotValidator.ValidateLoaded(registry, snapshot)` two-argument overload | Redundant with the three-argument overload plus `null`; two overloads with optional tails complicate future additions. |
| `SettingDefinitionValidator.ValidateNumericConstraint` | Public because the Unity converter (a separate assembly) calls it; could become internal with `InternalsVisibleTo` if the Unity adapter is treated as a sibling. |

All classes are already `sealed`; all structs are `readonly`.

## Open questions for the review

- `SettingsCoordinator.Cancel(SettingsTransaction)` only null-checks its argument; a cancelled transaction can still be passed to `Apply`. Decide whether `Cancel` should invalidate the transaction, or whether it should be removed and cancellation defined as "drop the object".
- `ChangesApplied` handlers run inside `Apply` with no exception guard, after the committed snapshot has been replaced and before `Save` is called. A throwing handler leaves the coordinator committed but returns no `SettingsApplyResult` to the caller.
- Failure model is mixed: `OptionConstraint`, `SettingsProfileLayer`, `SettingsSnapshot`, `SettingsTransaction.StageProfile` and the coordinator constructor throw `ArgumentException` or `ArgumentNullException`, while every operation returns `SettingsResult`. Decide whether constructor throws are admitted.
- `SettingDefinition` is obtainable only through `SettingDefinitionValidator.ValidateBuilt`. Confirm whether a validator type is the intended factory, or whether a `SettingDefinition.Create` mirrors the `SettingsRegistry.Create` / `SettingsProfile.Create` pattern.
- `SettingValueKindRecord` and `SettingScopeRecord` duplicate the Core enums and are converted by integer cast. A member added to one but not the other converts silently. Decide whether the assets should serialize the Core enums directly.
- `SettingsValidationCode` has implicit numeric values after `None`; `SettingsApplyOutcome` has explicit values. If codes are ever persisted or logged as integers, confirm an append-only rule.
- `SettingsCoordinator.LoadFromRepository` reports "no repository configured" with `InvalidKey`, and `SettingsUnityFactory` reports a missing config or catalog with `InvalidKey`. Confirm whether a dedicated code is wanted before the enum freezes.
- `SettingsSnapshot.KnownValues` and `UnknownValues` allocate a sorted copy on every read. Confirm that consumers are expected to call `TryGetKnownValue` in hot paths and that the copying getters are the intended contract.
- `SettingsUnityFactoryConfig` is a mutable class with public setters, unlike every other public type in the family. Confirm the pattern for factory inputs.
- The coordinator is not documented as thread-affine or thread-safe; `Apply` mutates `_committed` without synchronization.
- Accessibility `Category` and `FeatureId` are open strings (already recorded as `LESSON-001` in the [lessons register](../lessons/README.md)); the review should decide whether any closed vocabulary lands before the surface freezes.
