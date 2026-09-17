# Persistence public API surface

Status: inventory for the public API compatibility review. The family is
incubating; nothing in this file is a stability promise. Every entry is derived
from the `Runtime/` sources at the revision that carries this file. Internal and
private members are omitted.

Packages inventoried:

| Package | Version | Assembly (asmdef) | References | Engine references | Dependencies |
| --- | --- | --- | --- | --- | --- |
| `com.lingkyn.persistence.core` | 0.1.0 | `Lingkyn.Persistence.Core` | none | `noEngineReferences: true` | none |
| `com.lingkyn.persistence.unity` | 0.1.0 | `Lingkyn.Persistence.Unity` | `Lingkyn.Persistence.Core` | `noEngineReferences: false` | `com.lingkyn.persistence.core` 0.1.0 |

`Lingkyn.Persistence.Unity` declares `InternalsVisibleTo("Lingkyn.Persistence.Unity.Editor.Tests")`.
The Core assembly declares no `InternalsVisibleTo`.

Consumer-facing: `yes` means a consumer calls or reads it directly; `seam` means
a consumer implements or extends it; `no` means it exists to support another
public member (a result carrier, a helper, or an adapter-only entry).

## `com.lingkyn.persistence.core` (namespace `Lingkyn.Persistence.Core`)

29 public types.

| Type | Kind | Public members | Consumer-facing |
| --- | --- | --- | --- |
| `SaveStage` | enum | `Snapshot`, `Encode`, `Integrity`, `StageWrite`, `Flush`, `Commit`, `Read`, `Envelope`, `Verify`, `Decode`, `Migrate`, `Validate`, `Apply` | yes |
| `SaveErrorCode` | enum | `None`, `InvalidSlot`, `NotFound`, `UnsupportedFormat`, `FutureSchema`, `MissingMigration`, `AmbiguousMigration`, `CyclicMigration`, `NonMonotonicMigration`, `CorruptPayload`, `UnsupportedCommitCapability`, `IoDenied`, `OutOfSpace`, `Cancelled`, `ValidateRejected`, `ProviderFailure`, `OvershootMigration` | yes |
| `SaveError` | readonly struct, `IEquatable<SaveError>` | ctor `(SaveStage, SaveErrorCode, string)`; props `Stage`, `Code`, `Message`; `Equals`, `GetHashCode`, `ToString` | yes |
| `SaveResult` | readonly struct | props `Succeeded`, `Error`; static `Success()`, `Fail(SaveStage, SaveErrorCode, string)` | yes |
| `SaveResult<T>` | readonly struct | props `Succeeded`, `Value`, `Error`; static `Success(T)`, `Fail(SaveStage, SaveErrorCode, string)` | yes |
| `SaveDiagnosticSeverity` | enum | `Info = 0`, `Warning = 1`, `Error = 2` | yes |
| `SaveDiagnostic` | readonly struct | ctor `(SaveDiagnosticSeverity, SaveStage, SaveErrorCode, string)`; props `Severity`, `Stage`, `Code`, `Message` | yes |
| `SaveCommitResult` | readonly struct | props `Committed`, `PriorCommittedRecordPreserved`, `Error`, `Diagnostics`; static `Success(IReadOnlyList<SaveDiagnostic> = null)`, `NotCommitted(SaveStage, SaveErrorCode, string, bool priorCommittedRecordPreserved, IReadOnlyList<SaveDiagnostic> = null)`; `WithDiagnostics(IReadOnlyList<SaveDiagnostic>)`, `IsContradictory(out string)` | yes |
| `SaveSlotId` | readonly struct, `IEquatable<SaveSlotId>` | prop `Value`; static `TryCreate(string)` returning `SaveResult<SaveSlotId>`; `Equals`, `GetHashCode`, `ToString`, `==`, `!=` | yes |
| `SaveEnvelope` | sealed class | ctor `(string schemaId, int schemaVersion, string commitId, long timestampUtcTicks, string integrityAlgorithm, byte[] integrityDigest, byte[] payload)` (throws on invalid arguments); props `SchemaId`, `SchemaVersion`, `CommitId`, `TimestampUtcTicks`, `IntegrityAlgorithm`, `IntegrityDigest` (`ReadOnlyMemory<byte>`), `Payload` (`ReadOnlyMemory<byte>`) | no |
| `SaveEnvelopeBinaryCodec` | static class | consts `FormatVersion = 1`, `MaxSchemaIdBytes = 128`, `MaxCommitIdBytes = 128`, `MaxAlgorithmBytes = 32`, `MaxDigestBytes = 64`, `MaxPayloadBytes = 4 MiB`; `Encode(SaveEnvelope)` returning `SaveResult<byte[]>`; `Decode(ReadOnlySpan<byte>)` returning `SaveResult<SaveEnvelope>` | no |
| `SaveCommitCapabilities` | `[Flags]` enum | `None = 0`, `BestEffortWrite = 1`, `AtomicReplace = 2`, `RecoverableReplace = 4` | yes |
| `ISaveCodec<TState>` | interface | `Encode(TState)` returning `SaveResult<byte[]>`; `Decode(int schemaVersion, ReadOnlySpan<byte>)` returning `SaveResult<TState>` | seam |
| `IIntegrityProvider` | interface | prop `AlgorithmName`; `ComputeDigest(ReadOnlySpan<byte>)` returning `SaveResult<byte[]>`; `Verify(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> expectedDigest)` returning `SaveResult` | seam |
| `Sha256IntegrityProvider` | sealed class, `IIntegrityProvider` | `AlgorithmName` (`"sha-256"`); `ComputeDigest`, `Verify` | yes |
| `ISaveMigration<TState>` | interface | props `FromVersion`, `ToVersion`; `Migrate(TState)` returning `TState` | seam |
| `SaveCandidateKind` | enum | `Primary = 0`, `Backup = 1`, `Staging = 2` | yes |
| `SaveCandidateId` | readonly struct, `IEquatable<SaveCandidateId>` | prop `Value`; static `TryCreate(string)`; `Equals`, `GetHashCode`, `ToString`, `==`, `!=` | yes |
| `SaveReadCandidate` | sealed class | ctor `(SaveCandidateKind, SaveCandidateId, ReadOnlySpan<byte>)` (throws on undefined kind or empty id); props `Kind`, `Id`, `Bytes` (`ReadOnlyMemory<byte>`) | seam (store output) |
| `SaveReadCandidateSet` | readonly struct | ctor `(IReadOnlyList<SaveReadCandidate>)`; prop `Candidates`; static `Empty` | seam (store output) |
| `SaveRecoveryPolicy` | enum | `PrimaryOnly = 0`, `PrimaryThenBackup = 1` | yes |
| `SaveLoadReceipt<TState>` | readonly struct | ctor `(TState, SaveCandidateKind, SaveCandidateId, bool recoveryOccurred, SaveDiagnostic? = null)`; props `State`, `SelectedCandidateKind`, `SelectedCandidateId`, `RecoveryOccurred`, `PrimaryFailureDiagnostic` | yes |
| `ISaveStore` | interface | prop `Capabilities`; `ReadCandidates(SaveSlotId)` returning `SaveResult<SaveReadCandidateSet>`; `Commit(SaveSlotId, ReadOnlyMemory<byte> envelopeBytes, SaveCommitCapabilities requiredCapabilities)` returning `SaveCommitResult` | seam |
| `SaveCommitNotification<TState>` | readonly struct | ctor `(SaveSlotId, TState, ReadOnlyMemory<byte>)`; props `SlotId`, `Snapshot`, `EnvelopeBytes` | seam (observer input) |
| `ISaveCommitObserver<TState>` | interface | `OnCommitted(SaveCommitNotification<TState>)` | seam |
| `MigrationPipeline<TState>` | sealed class | ctor `(IEnumerable<ISaveMigration<TState>>)` (throws on null; skips null entries); `Apply(int storedVersion, int targetVersion, TState)` returning `SaveResult<TState>` | yes |
| `SaveRecoveryCandidateSelector` | static class | `IsEligiblePrimaryFailureForBackup(SaveError)` returning `bool`; `Select(SaveReadCandidateSet, SaveRecoveryPolicy, string expectedSchemaId, IIntegrityProvider)` returning `SaveResult<SaveRecoverySelection>` | no |
| `SaveRecoverySelection` | readonly struct | ctor `(SaveReadCandidate, bool recoveryOccurred, SaveDiagnostic?)` (throws on null candidate); props `SelectedCandidate`, `RecoveryOccurred`, `PrimaryFailureDiagnostic` | no |
| `SaveCoordinator<TState>` | sealed class | ctor `(string schemaId, int currentSchemaVersion, string commitId, ISaveCodec<TState>, IIntegrityProvider, MigrationPipeline<TState>, ISaveStore, SaveCommitCapabilities requiredCapabilities, IEnumerable<ISaveCommitObserver<TState>> = null, SaveRecoveryPolicy = PrimaryOnly)` (throws on invalid arguments); `RegisterPostCommitObserver(ISaveCommitObserver<TState>)`; `Save(SaveSlotId, TState, CancellationToken = default)` returning `SaveCommitResult`; `LoadValidated(SaveSlotId, Func<TState, SaveResult> validator)` returning `SaveResult<SaveLoadReceipt<TState>>`; `LoadAndApply(SaveSlotId, Func<TState, SaveResult> validator, Func<TState, SaveResult> apply)` returning `SaveResult` | yes |

## `com.lingkyn.persistence.unity` (namespace `Lingkyn.Persistence.Unity`)

10 public types. Internal-only types in this assembly (`IFileOperationSeam`,
`DefaultFileOperationSeam`, `FaultInjectingFileOperationSeam`,
`FileOperationStage`, `InjectedOutOfSpaceException`, `FileIoErrorMapper`,
`SavePathPolicy`, `SlotPaths`) are not part of the surface.

| Type | Kind | Public members | Consumer-facing |
| --- | --- | --- | --- |
| `LocalFileCommitStrategy` | enum | `AtomicFileReplace = 0`, `RecoverableCopyReplace = 1`, `BestEffortDirectWrite = 2` | yes |
| `SaveMigrationEdgeDefinition` | `[Serializable]` sealed class | ctor `(int fromVersion, int toVersion)`; props `FromVersion`, `ToVersion` | yes (authoring) |
| `PersistenceUnityConfig` | sealed class, `ScriptableObject`, `[CreateAssetMenu]` | props `SchemaId`, `CurrentSchemaVersion`, `CommitId`, `FileExtension`, `StorageSubdirectory`, `CommitStrategy`, `RequiredCommitCapability`, `RecoveryPolicy`, `IntegrityAlgorithm`, `MigrationEdges`; `ValidateAuthoring()` returning `SaveResult`; `ResolveAdvertisedCapabilities()` returning `SaveCommitCapabilities`. Backing fields are private `[SerializeField]` with no public setters | yes (authoring) |
| `PersistenceUnityFactory` | static class | `CreateCoordinator<TState>(PersistenceUnityConfig, IPersistentDataRootProvider, ISaveCodec<TState>, IEnumerable<ISaveMigration<TState>> = null, IEnumerable<ISaveCommitObserver<TState>> = null)` returning `SaveResult<SaveCoordinator<TState>>`; `CreateIntegrityProvider(string algorithmName)` returning `IIntegrityProvider` (null when unsupported) | yes |
| `IPersistentDataRootProvider` | interface | `ResolveRoot()` returning `SaveResult<string>` | seam |
| `PersistentDataRootProvider` | sealed class, `IPersistentDataRootProvider` | `ResolveRoot()` (fails unless on the Unity main thread; reads `Application.persistentDataPath`) | yes |
| `InjectedPersistentDataRootProvider` | sealed class, `IPersistentDataRootProvider` | ctor `(string rootPath)` (throws on null); `ResolveRoot()` | yes (tests, custom roots) |
| `LocalFileSaveStore` | sealed class, `ISaveStore` | ctor `(IPersistentDataRootProvider, string storageSubdirectory, string fileExtension, LocalFileCommitStrategy)` (throws `InvalidOperationException` on invalid configuration); `Capabilities`; `ReadCandidates`; `Commit` | yes |
| `JsonUtilityDtoSupport` | static class | `ValidateDtoType<T>()` returning `SaveResult` | yes (authoring validation) |
| `JsonUtilitySaveCodec<T>` | sealed class, `ISaveCodec<T>` | ctor `()` (throws `InvalidOperationException` when `T` is not a supported DTO); `Encode(T)`; `Decode(int, ReadOnlySpan<byte>)` | yes |

## Seams a consumer extends

All seams are interfaces; the family exposes no abstract classes.

| Seam | Implemented by a consumer when | Shipped implementations |
| --- | --- | --- |
| `ISaveCodec<TState>` | the consumer's DTO is not a plain `JsonUtility` shape or another format is required | `JsonUtilitySaveCodec<T>` |
| `IIntegrityProvider` | a digest other than SHA-256 is required (the Unity factory only resolves `"sha-256"`) | `Sha256IntegrityProvider` |
| `ISaveMigration<TState>` | every schema-version step | none (consumer-authored) |
| `ISaveStore` | storage other than local files (cloud, platform, in-memory test doubles) | `LocalFileSaveStore` |
| `ISaveCommitObserver<TState>` | post-commit side effects such as telemetry or cache invalidation | none |
| `IPersistentDataRootProvider` | a root other than `Application.persistentDataPath` | `PersistentDataRootProvider`, `InjectedPersistentDataRootProvider` |

Delegates on `SaveCoordinator<TState>` (`validator`, `apply`) are also
extension points: both are `Func<TState, SaveResult>`.

## Types that must stay binary-compatible across a release

| Type | Reason |
| --- | --- |
| `SaveResult`, `SaveResult<T>`, `SaveError` | Every public method returns them; a layout or member change breaks every caller. |
| `SaveStage`, `SaveErrorCode` | Consumers branch on them; they are also written into diagnostics and logs, so member order and names are part of the contract. |
| `SaveCommitResult`, `SaveDiagnostic`, `SaveDiagnosticSeverity` | Store implementers construct them and consumers read them across the `ISaveStore` seam. |
| `SaveSlotId`, `SaveCandidateId`, `SaveCandidateKind` | Cross the `ISaveStore` seam in both directions and name on-disk files. |
| `ISaveCodec<TState>`, `IIntegrityProvider`, `ISaveMigration<TState>`, `ISaveStore`, `ISaveCommitObserver<TState>`, `IPersistentDataRootProvider` | Consumer implementations break on any added or changed member. |
| `SaveReadCandidate`, `SaveReadCandidateSet`, `SaveCommitNotification<TState>` | Appear in seam signatures. |
| `SaveCommitCapabilities` | Flag values are serialized in `PersistenceUnityConfig` assets and compared bitwise by stores. |
| `SaveEnvelope`, `SaveEnvelopeBinaryCodec` | Define the on-disk envelope; `FormatVersion` and the `Max*` bounds are the wire format. |
| `SaveRecoveryPolicy`, `SaveLoadReceipt<TState>` | Serialized in config and returned from the primary load path. |
| `SaveCoordinator<TState>`, `MigrationPipeline<TState>` | Primary consumer entry points; the coordinator constructor already has ten parameters and no options object. |
| `PersistenceUnityConfig` serialized field names, `LocalFileCommitStrategy`, `SaveMigrationEdgeDefinition` | Renaming or reordering breaks authored `.asset` files. |
| `PersistenceUnityFactory`, `LocalFileSaveStore`, `JsonUtilitySaveCodec<T>` | Documented Unity entry points and the only shipped store and codec. |

## Candidates for internal or sealed before the first release

| Type or member | Observation |
| --- | --- |
| `SaveRecoveryCandidateSelector` | Only `SaveCoordinator<TState>.LoadValidated` and the Core contract tests call it. Its inputs are already public through `ISaveStore`, so making it internal removes a second recovery entry point without losing capability. |
| `SaveRecoverySelection` | Exists only as the `Select` result; nothing outside `Runtime/` references it. Would follow the selector. |
| `SaveEnvelope` constructor and `SaveEnvelopeBinaryCodec` | Consumers never build envelopes; the coordinator does. Keeping them public makes the envelope format a supported extension point. The `Max*` constants are useful to authoring validation and could stay public on their own. |
| `SaveLoadReceipt<TState>` constructor | Only the coordinator constructs receipts; a public constructor with an optional parameter blocks adding fields without a source-breaking change. |
| `SaveCommitResult.WithDiagnostics` | Used by the coordinator to append observer warnings; a store has no reason to call it. |
| `JsonUtilityDtoSupport` | One public method; used by the Unity contract tests and the codec constructor. Reasonable to keep for Editor validation, but it duplicates the check the codec constructor already performs. |
| `InjectedPersistentDataRootProvider` | A test and sample seam. Keeping it public is defensible because consumers need it for their own tests; the name should then be treated as stable. |

All classes are already `sealed`; all structs are `readonly`.

## Open questions for the review

- Failure model is mixed: `SaveEnvelope`, `SaveReadCandidate`, `SaveRecoverySelection`, `MigrationPipeline<TState>`, `SaveCoordinator<TState>`, `LocalFileSaveStore` and `JsonUtilitySaveCodec<T>` throw on construction, while every operation returns `SaveResult`. The architecture contract says exceptions are diagnostic causes, not public control flow. Decide whether constructor throws are admitted.
- `PersistenceUnityFactory.CreateIntegrityProvider` returns `null` for an unknown algorithm instead of a `SaveResult`; it is the only null-returning public member.
- `SaveErrorCode` and `SaveStage` have implicit numeric values after `None`. If any consumer persists or logs them as integers, inserting a member is a breaking change; confirm an append-only rule or assign explicit values.
- `IIntegrityProvider.AlgorithmName` is compared ordinally with the envelope string and stored on disk. Only `"sha-256"` is admitted by `PersistenceUnityConfig.ValidateAuthoring`. Decide whether algorithm names are a registry or free strings.
- `SaveEnvelopeBinaryCodec.FormatVersion = 1` with `MaxPayloadBytes = 4 MiB`: raising the bound or the version is a format change. Record the bump policy before release.
- `SaveCandidateKind.Staging` candidates are enumerated by `LocalFileSaveStore.ReadCandidates` but never selected by `SaveRecoveryCandidateSelector`. Confirm whether staging candidates are part of the read contract or an inspection-only detail.
- `ISaveStore.Commit` receives `requiredCapabilities` even though `SaveCoordinator<TState>.Save` already rejects a store that cannot satisfy them. Decide which side owns the check.
- `SaveCoordinator<TState>` takes ten constructor parameters and exposes `RegisterPostCommitObserver` for mutation after construction. Consider an options object before the signature freezes.
- `PersistenceUnityConfig` has no public setters or factory; runtime consumers can only author it as an asset. Confirm this is intended for a Unity adapter that targets automated tests.
- `SaveSlotId` and `SaveCandidateId` are constructible as `default` with an empty `Value`; every consumer of them re-validates for emptiness. Confirm that `default` is an admitted invalid state.
- `ISaveCommitObserver<TState>` exceptions are downgraded to `Warning` diagnostics on a committed result. Confirm that observers cannot fail a commit.
- The Unity package declares `InternalsVisibleTo` for its Editor tests, while Core declares none; confirm the intended testing boundary for each assembly.
