using System;
using System.Collections.Generic;
using System.Text;
using Lingkyn.Persistence.Core;
using Lingkyn.Persistence.Unity;
using Lingkyn.Settings.Core;
using NUnit.Framework;
using XRFoundry.ReferenceSystem.Bindings;

namespace XRFoundry.ReferenceSystem.Tests
{
    public sealed class SettingsPersistenceRehydrationAdapterTests
    {
        [Test]
        public void PersistAndCoordinatorRehydrateRoundTripIsDeterministicAndPreservesUnknownValues()
        {
            var registry = CreateRegistry();
            var muteKey = MustKey("audio.mute");
            var initial = new SettingsSnapshot(
                7,
                new Dictionary<ScopedSettingKey, SettingValue>
                {
                    { new ScopedSettingKey(muteKey, SettingScope.User), SettingValue.FromBoolean(false) },
                },
                new Dictionary<string, SettingValue>
                {
                    { "future.zeta", SettingValue.FromString("preserve-me") },
                    { "future.alpha", SettingValue.FromInteger(42) },
                    { "future.float", SettingValue.FromFloat(0.625) },
                    { "future.option", SettingValue.FromOption(MustOption("immersive")) },
                    { "future.boolean", SettingValue.FromBoolean(true) },
                });
            var codec = new RecordingJsonSettingsCodec();
            var store = new InMemorySettingsSaveStore();
            var adapter = CreateAdapter(codec, store);
            var source = adapter.CreateCoordinator(registry, initial);

            var firstDto = SettingsSnapshotPersistenceDtoMapper.FromSnapshot(initial);
            var secondDto = SettingsSnapshotPersistenceDtoMapper.FromSnapshot(initial);
            Assert.That(firstDto.Succeeded, Is.True, firstDto.Error.Message);
            Assert.That(secondDto.Succeeded, Is.True, secondDto.Error.Message);
            Assert.That(firstDto.Value.UnknownValues[0].RawKey, Is.EqualTo("future.alpha"));
            Assert.That(firstDto.Value.UnknownValues[4].RawKey, Is.EqualTo("future.zeta"));
            var firstBytes = codec.Encode(firstDto.Value);
            var secondBytes = codec.Encode(secondDto.Value);
            Assert.That(firstBytes.Succeeded, Is.True, firstBytes.Error.Message);
            Assert.That(secondBytes.Succeeded, Is.True, secondBytes.Error.Message);
            Assert.That(secondBytes.Value, Is.EqualTo(firstBytes.Value));

            var transaction = source.BeginTransaction();
            transaction.StageSet(
                new ScopedSettingKey(muteKey, SettingScope.User),
                SettingValue.FromBoolean(true));
            var applied = source.Apply(transaction);
            Assert.That(applied.Outcome, Is.EqualTo(SettingsApplyOutcome.Applied));
            Assert.That(source.CommittedSnapshot.Revision, Is.EqualTo(8));
            var firstPersistedPayload = ExtractPayload(store.CopyLastCommittedEnvelopeBytes());

            var restored = adapter.CreateCoordinator(
                registry,
                SettingsSnapshot.CreateInitial(registry));
            var loaded = restored.LoadFromRepository();

            Assert.That(loaded.Succeeded, Is.True, loaded.Error.Message);
            Assert.That(restored.CommittedSnapshot.Revision, Is.EqualTo(8));
            Assert.That(
                restored.CommittedSnapshot.TryGetKnownValue(
                    new ScopedSettingKey(muteKey, SettingScope.User),
                    out var restoredMute),
                Is.True);
            Assert.That(restoredMute.BooleanValue, Is.True);
            AssertUnknownInteger(restored.CommittedSnapshot, "future.alpha", 42);
            AssertUnknownBoolean(restored.CommittedSnapshot, "future.boolean", true);
            AssertUnknownFloat(restored.CommittedSnapshot, "future.float", 0.625);
            AssertUnknownOption(restored.CommittedSnapshot, "future.option", "immersive");
            AssertUnknownString(restored.CommittedSnapshot, "future.zeta", "preserve-me");
            Assert.That(codec.LastDecodeSchemaVersion, Is.EqualTo(1));
            Assert.That(codec.LastDecodeSchemaVersion, Is.Not.EqualTo(restored.CommittedSnapshot.Revision));
            Assert.That(adapter.LastSelectedCandidateKind, Is.EqualTo(SaveCandidateKind.Primary));
            Assert.That(adapter.LastSelectedCandidateId.HasValue, Is.True);
            Assert.That(adapter.LastSelectedCandidateId.Value.Value, Is.EqualTo("primary"));
            Assert.That(adapter.LastLoadRecoveryOccurred, Is.False);
            Assert.That(adapter.LastPrimaryFailureDiagnostic, Is.Null);

            var savedAgain = adapter.Persist(restored.CommittedSnapshot);
            Assert.That(savedAgain.Committed, Is.True, savedAgain.Error.Message);
            var secondPersistedPayload = ExtractPayload(store.CopyLastCommittedEnvelopeBytes());
            Assert.That(secondPersistedPayload, Is.EqualTo(firstPersistedPayload));
        }

        [Test]
        public void MissingRequiredWireFieldsAreRejectedAfterJsonUtilityDecode()
        {
            AssertWireRejected(
                "{\"KnownValues\":[],\"UnknownValues\":[]}",
                "revision is required");
            AssertWireRejected(
                "{\"Revision\":\"0\",\"KnownValues\":[{\"Key\":\"audio.mute\",\"Value\":{\"Kind\":\"boolean\",\"Payload\":\"false\"}}],\"UnknownValues\":[]}",
                "scope is required");
            AssertWireRejected(
                "{\"Revision\":\"0\",\"KnownValues\":[{\"Key\":\"audio.mute\",\"Scope\":\"user\",\"Value\":{\"Payload\":\"false\"}}],\"UnknownValues\":[]}",
                "kind is required");
            AssertWireRejected(
                "{\"Revision\":\"0\",\"KnownValues\":[{\"Key\":\"audio.mute\",\"Scope\":\"user\",\"Value\":{\"Kind\":\"boolean\"}}],\"UnknownValues\":[]}",
                "boolean payload is required");
            AssertWireRejected(
                "{\"Revision\":\"0\",\"KnownValues\":[{\"Key\":\"future.integer\",\"Scope\":\"user\",\"Value\":{\"Kind\":\"integer\"}}],\"UnknownValues\":[]}",
                "integer payload is required");
            AssertWireRejected(
                "{\"Revision\":\"0\",\"KnownValues\":[{\"Key\":\"future.float\",\"Scope\":\"user\",\"Value\":{\"Kind\":\"float\"}}],\"UnknownValues\":[]}",
                "float payload is required");
            AssertWireRejected(
                "{\"Revision\":\"0\",\"KnownValues\":[{\"Key\":\"future.string\",\"Scope\":\"user\",\"Value\":{\"Kind\":\"string\"}}],\"UnknownValues\":[]}",
                "string payload is required");
            AssertWireRejected(
                "{\"Revision\":\"0\",\"KnownValues\":[{\"Key\":\"future.option\",\"Scope\":\"user\",\"Value\":{\"Kind\":\"option\"}}],\"UnknownValues\":[]}",
                "option payload is required");
        }

        [Test]
        public void NonCanonicalWireScalarsAndUnknownUnionKindAreRejected()
        {
            AssertWireRejected(
                "{\"Revision\":\"00\",\"KnownValues\":[],\"UnknownValues\":[]}",
                "revision '00' is not canonical");
            AssertWireRejected(
                "{\"Revision\":\"0\",\"KnownValues\":[{\"Key\":\"audio.mute\",\"Scope\":\"User\",\"Value\":{\"Kind\":\"boolean\",\"Payload\":\"false\"}}],\"UnknownValues\":[]}",
                "scope 'User' is unsupported");
            AssertWireRejected(
                "{\"Revision\":\"0\",\"KnownValues\":[{\"Key\":\"audio.mute\",\"Scope\":\"user\",\"Value\":{\"Kind\":\"Boolean\",\"Payload\":\"false\"}}],\"UnknownValues\":[]}",
                "kind 'Boolean' is unsupported");
            AssertWireRejected(
                "{\"Revision\":\"0\",\"KnownValues\":[{\"Key\":\"audio.mute\",\"Scope\":\"user\",\"Value\":{\"Kind\":\"boolean\",\"Payload\":\"False\"}}],\"UnknownValues\":[]}",
                "boolean payload 'False' is not canonical");
            AssertWireRejected(
                "{\"Revision\":\"0\",\"KnownValues\":[{\"Key\":\"future.integer\",\"Scope\":\"user\",\"Value\":{\"Kind\":\"integer\",\"Payload\":\"+1\"}}],\"UnknownValues\":[]}",
                "integer payload '+1' is not canonical");
            AssertWireRejected(
                "{\"Revision\":\"0\",\"KnownValues\":[{\"Key\":\"future.float\",\"Scope\":\"user\",\"Value\":{\"Kind\":\"float\",\"Payload\":\"1.0\"}}],\"UnknownValues\":[]}",
                "float payload '1.0' is not canonical");
            AssertWireRejected(
                "{\"Revision\":\"0\",\"KnownValues\":[{\"Key\":\"future.value\",\"Scope\":\"user\",\"Value\":{\"Kind\":\"future-kind\",\"Payload\":\"opaque\"}}],\"UnknownValues\":[]}",
                "kind 'future-kind' is unsupported");
        }

        [Test]
        public void NotFoundFlowsThroughRepositoryAndLeavesCoordinatorStateUntouched()
        {
            var registry = CreateRegistry();
            var initial = SnapshotWithMute(registry, revision: 5, muted: true);
            var adapter = CreateAdapter(
                new RecordingJsonSettingsCodec(),
                new InMemorySettingsSaveStore());
            var coordinator = adapter.CreateCoordinator(registry, initial);

            var loaded = coordinator.LoadFromRepository();

            Assert.That(loaded.Succeeded, Is.False);
            Assert.That(loaded.Error.Code, Is.EqualTo(SettingsValidationCode.InvalidKey));
            StringAssert.Contains("Read:NotFound", loaded.Error.Message);
            Assert.That(coordinator.CommittedSnapshot, Is.SameAs(initial));
            Assert.That(adapter.LastSelectedCandidateKind, Is.Null);
            Assert.That(adapter.LastSelectedCandidateId, Is.Null);
            Assert.That(adapter.LastLoadError.HasValue, Is.True);
            Assert.That(adapter.LastLoadError.Value.Stage, Is.EqualTo(SaveStage.Read));
            Assert.That(adapter.LastLoadError.Value.Code, Is.EqualTo(SaveErrorCode.NotFound));
        }

        [Test]
        public void CorruptEnvelopeIsRejectedBeforeRehydrationAndLeavesCoordinatorStateUntouched()
        {
            var registry = CreateRegistry();
            var store = new InMemorySettingsSaveStore();
            var adapter = CreateAdapter(new RecordingJsonSettingsCodec(), store);
            var persisted = SnapshotWithMute(registry, revision: 9, muted: false);
            Assert.That(adapter.Persist(persisted).Committed, Is.True);

            var corrupt = store.CopyLastCommittedEnvelopeBytes();
            corrupt[corrupt.Length - 1] ^= 0x20;
            store.ReplaceCandidates(Candidate(SaveCandidateKind.Primary, "primary", corrupt));
            var initial = SnapshotWithMute(registry, revision: 3, muted: true);
            var coordinator = adapter.CreateCoordinator(registry, initial);

            var loaded = coordinator.LoadFromRepository();

            Assert.That(loaded.Succeeded, Is.False);
            StringAssert.Contains("Verify:CorruptPayload", loaded.Error.Message);
            Assert.That(coordinator.CommittedSnapshot, Is.SameAs(initial));
            Assert.That(adapter.LastSelectedCandidateKind, Is.Null);
            Assert.That(adapter.LastSelectedCandidateId, Is.Null);
            Assert.That(adapter.LastLoadError.HasValue, Is.True);
            Assert.That(adapter.LastLoadError.Value.Stage, Is.EqualTo(SaveStage.Verify));
            Assert.That(adapter.LastLoadError.Value.Code, Is.EqualTo(SaveErrorCode.CorruptPayload));
        }

        [Test]
        public void CommitFailureKeepsSettingsAppliedButReportsExactPersistenceFailure()
        {
            var registry = CreateRegistry();
            var store = new InMemorySettingsSaveStore
            {
                ForcedCommitResult = SaveCommitResult.NotCommitted(
                    SaveStage.Commit,
                    SaveErrorCode.OutOfSpace,
                    "capacity exhausted",
                    priorCommittedRecordPreserved: true),
            };
            var adapter = CreateAdapter(new RecordingJsonSettingsCodec(), store);
            var coordinator = adapter.CreateCoordinator(
                registry,
                SettingsSnapshot.CreateInitial(registry));
            var muteKey = MustKey("audio.mute");
            var transaction = coordinator.BeginTransaction();
            transaction.StageSet(
                new ScopedSettingKey(muteKey, SettingScope.User),
                SettingValue.FromBoolean(true));

            var applied = coordinator.Apply(transaction);

            Assert.That(applied.Outcome, Is.EqualTo(SettingsApplyOutcome.AppliedNotPersisted));
            Assert.That(applied.CommittedRevision, Is.EqualTo(1));
            Assert.That(applied.PersistenceMessage, Is.EqualTo("Commit:OutOfSpace capacity exhausted"));
            Assert.That(
                coordinator.CommittedSnapshot.TryGetKnownValue(
                    new ScopedSettingKey(muteKey, SettingScope.User),
                    out var committedMute),
                Is.True);
            Assert.That(committedMute.BooleanValue, Is.True);
            Assert.That(adapter.LastSaveCommitResult.HasValue, Is.True);
            Assert.That(adapter.LastSaveCommitResult.Value.Committed, Is.False);
            Assert.That(adapter.LastSaveCommitResult.Value.PriorCommittedRecordPreserved, Is.True);
            Assert.That(adapter.LastSaveCommitResult.Value.Error.Stage, Is.EqualTo(SaveStage.Commit));
            Assert.That(adapter.LastSaveCommitResult.Value.Error.Code, Is.EqualTo(SaveErrorCode.OutOfSpace));
        }

        [Test]
        public void PersistRejectsInvalidKnownKeyAndScopeBeforeCommitAndPreservesPriorBytes()
        {
            var registry = CreateRegistry();
            var store = new InMemorySettingsSaveStore();
            var adapter = CreateAdapter(new RecordingJsonSettingsCodec(), store);
            Assert.That(
                adapter.Persist(
                    SnapshotWithMute(registry, revision: 3, muted: true)).Committed,
                Is.True);
            var priorEnvelope = store.CopyLastCommittedEnvelopeBytes();
            var priorCommitCount = store.CommitCallCount;

            var invalidKey = new SettingsSnapshot(
                4,
                new Dictionary<ScopedSettingKey, SettingValue>
                {
                    { new ScopedSettingKey(default, SettingScope.User), SettingValue.FromBoolean(false) },
                },
                new Dictionary<string, SettingValue>());
            var invalidKeyResult = adapter.Persist(invalidKey);

            Assert.That(invalidKeyResult.Committed, Is.False);
            Assert.That(invalidKeyResult.PriorCommittedRecordPreserved, Is.True);
            Assert.That(invalidKeyResult.Error.Stage, Is.EqualTo(SaveStage.Snapshot));
            Assert.That(invalidKeyResult.Error.Code, Is.EqualTo(SaveErrorCode.ValidateRejected));
            StringAssert.Contains("key is invalid", invalidKeyResult.Error.Message);
            Assert.That(store.CommitCallCount, Is.EqualTo(priorCommitCount));
            Assert.That(store.CopyLastCommittedEnvelopeBytes(), Is.EqualTo(priorEnvelope));

            var invalidScope = new SettingsSnapshot(
                4,
                new Dictionary<ScopedSettingKey, SettingValue>
                {
                    {
                        new ScopedSettingKey(MustKey("audio.mute"), (SettingScope)999),
                        SettingValue.FromBoolean(false)
                    },
                },
                new Dictionary<string, SettingValue>());
            var invalidScopeResult = adapter.Persist(invalidScope);

            Assert.That(invalidScopeResult.Committed, Is.False);
            Assert.That(invalidScopeResult.PriorCommittedRecordPreserved, Is.True);
            Assert.That(invalidScopeResult.Error.Stage, Is.EqualTo(SaveStage.Snapshot));
            Assert.That(invalidScopeResult.Error.Code, Is.EqualTo(SaveErrorCode.ValidateRejected));
            StringAssert.Contains("scope is invalid", invalidScopeResult.Error.Message);
            Assert.That(store.CommitCallCount, Is.EqualTo(priorCommitCount));
            Assert.That(store.CopyLastCommittedEnvelopeBytes(), Is.EqualTo(priorEnvelope));
        }

        [Test]
        public void EmptyUnknownRawKeyCannotReachPersistenceOrReplacePriorBytes()
        {
            var registry = CreateRegistry();
            var store = new InMemorySettingsSaveStore();
            var adapter = CreateAdapter(new RecordingJsonSettingsCodec(), store);
            Assert.That(
                adapter.Persist(SnapshotWithMute(registry, revision: 3, muted: true)).Committed,
                Is.True);
            var priorEnvelope = store.CopyLastCommittedEnvelopeBytes();
            var priorCommitCount = store.CommitCallCount;

            Assert.Throws<ArgumentException>(() => adapter.Persist(new SettingsSnapshot(
                4,
                SettingsSnapshot.CreateInitial(registry).KnownValues,
                new Dictionary<string, SettingValue>
                {
                    { string.Empty, SettingValue.FromInteger(1) },
                })));

            Assert.That(store.CommitCallCount, Is.EqualTo(priorCommitCount));
            Assert.That(store.CopyLastCommittedEnvelopeBytes(), Is.EqualTo(priorEnvelope));
        }

        [Test]
        public void MaxRevisionIsRejectedOnPersistAndLoadWhileSubsequentSafeApplyStillSucceeds()
        {
            var registry = CreateRegistry();
            var codec = new RecordingJsonSettingsCodec();
            var store = new InMemorySettingsSaveStore();
            var adapter = CreateAdapter(codec, store);
            Assert.That(
                adapter.Persist(SnapshotWithMute(registry, revision: 2, muted: false)).Committed,
                Is.True);
            var priorEnvelope = store.CopyLastCommittedEnvelopeBytes();
            var priorCommitCount = store.CommitCallCount;

            var persistMax = adapter.Persist(
                SnapshotWithMute(registry, revision: long.MaxValue, muted: true));

            Assert.That(persistMax.Committed, Is.False);
            Assert.That(persistMax.PriorCommittedRecordPreserved, Is.True);
            Assert.That(persistMax.Error.Stage, Is.EqualTo(SaveStage.Snapshot));
            Assert.That(persistMax.Error.Code, Is.EqualTo(SaveErrorCode.ValidateRejected));
            Assert.That(store.CommitCallCount, Is.EqualTo(priorCommitCount));
            Assert.That(store.CopyLastCommittedEnvelopeBytes(), Is.EqualTo(priorEnvelope));

            codec.DecodeOverride = new SettingsSnapshotPersistenceDto
            {
                Revision = "9223372036854775807",
                KnownValues = new[]
                {
                    new SettingsKnownValuePersistenceDto
                    {
                        Key = "audio.mute",
                        Scope = "user",
                        Value = new SettingsValuePersistenceDto
                        {
                            Kind = "boolean",
                            Payload = "true",
                        },
                    },
                },
                UnknownValues = Array.Empty<SettingsUnknownValuePersistenceDto>(),
            };
            var initial = SnapshotWithMute(registry, revision: 3, muted: false);
            var coordinator = adapter.CreateCoordinator(registry, initial);

            var loaded = coordinator.LoadFromRepository();

            Assert.That(loaded.Succeeded, Is.False);
            Assert.That(coordinator.CommittedSnapshot, Is.SameAs(initial));
            Assert.That(adapter.LastLoadError.HasValue, Is.True);
            Assert.That(adapter.LastLoadError.Value.Stage, Is.EqualTo(SaveStage.Validate));
            Assert.That(adapter.LastLoadError.Value.Code, Is.EqualTo(SaveErrorCode.ValidateRejected));
            Assert.That(store.CommitCallCount, Is.EqualTo(priorCommitCount));
            Assert.That(store.CopyLastCommittedEnvelopeBytes(), Is.EqualTo(priorEnvelope));

            codec.DecodeOverride = null;
            var transaction = coordinator.BeginTransaction();
            transaction.StageSet(
                new ScopedSettingKey(MustKey("audio.mute"), SettingScope.User),
                SettingValue.FromBoolean(true));
            var applied = coordinator.Apply(transaction);

            Assert.That(applied.Outcome, Is.EqualTo(SettingsApplyOutcome.Applied));
            Assert.That(applied.CommittedRevision, Is.EqualTo(4));
            Assert.That(store.CommitCallCount, Is.EqualTo(priorCommitCount + 1));
        }

        [Test]
        public void CoordinatorLoadCompletesMissingDefaultsAndRunsConstraintsBeforeReplacingState()
        {
            var registry = CreateRegistry();
            var codec = new RecordingJsonSettingsCodec();
            var store = new InMemorySettingsSaveStore();
            var adapter = CreateAdapter(codec, store);
            Assert.That(
                adapter.Persist(SnapshotWithMute(registry, revision: 2, muted: true)).Committed,
                Is.True);
            codec.DecodeOverride = new SettingsSnapshotPersistenceDto
            {
                Revision = "15",
                KnownValues = Array.Empty<SettingsKnownValuePersistenceDto>(),
                UnknownValues = new[]
                {
                    new SettingsUnknownValuePersistenceDto
                    {
                        RawKey = "  future/raw key  ",
                        Value = new SettingsValuePersistenceDto
                        {
                            Kind = "integer",
                            Payload = "9",
                        },
                    },
                },
            };

            var completed = adapter.CreateCoordinator(
                registry,
                SnapshotWithMute(registry, revision: 1, muted: true));
            var completedLoad = completed.LoadFromRepository();

            Assert.That(completedLoad.Succeeded, Is.True, completedLoad.Error.Message);
            Assert.That(completed.CommittedSnapshot.Revision, Is.EqualTo(15));
            Assert.That(
                completed.CommittedSnapshot.TryGetKnownValue(
                    new ScopedSettingKey(MustKey("audio.mute"), SettingScope.User),
                    out var defaultMute),
                Is.True);
            Assert.That(defaultMute.BooleanValue, Is.False);
            AssertUnknownInteger(completed.CommittedSnapshot, "  future/raw key  ", 9);

            var initial = SnapshotWithMute(registry, revision: 5, muted: true);
            var rejected = adapter.CreateCoordinator(
                registry,
                initial,
                constraints: new ISettingsConstraint[] { new MuteMustBeEnabledConstraint() });
            var rejectedLoad = rejected.LoadFromRepository();

            Assert.That(rejectedLoad.Succeeded, Is.False);
            Assert.That(rejectedLoad.Error.Code, Is.EqualTo(SettingsValidationCode.CrossConstraintViolation));
            Assert.That(rejected.CommittedSnapshot, Is.SameAs(initial));
        }

        [Test]
        public void StructurallyInvalidDtoIsRejectedBeforeCoordinatorMutation()
        {
            var registry = CreateRegistry();
            var codec = new RecordingJsonSettingsCodec();
            var store = new InMemorySettingsSaveStore();
            var adapter = CreateAdapter(codec, store);
            var seed = SettingsSnapshotPersistenceDtoMapper.FromSnapshot(
                SnapshotWithMute(registry, revision: 4, muted: false));
            Assert.That(seed.Succeeded, Is.True, seed.Error.Message);
            Assert.That(
                adapter.Persist(
                    SnapshotWithMute(registry, revision: 4, muted: false)).Committed,
                Is.True);
            codec.DecodeOverride = new SettingsSnapshotPersistenceDto
            {
                Revision = "4",
                KnownValues = new[]
                {
                    new SettingsKnownValuePersistenceDto
                    {
                        Key = "audio.mute",
                        Scope = "user",
                        Value = new SettingsValuePersistenceDto
                        {
                            Kind = "999",
                            Payload = "ignored",
                        },
                    },
                },
                UnknownValues = Array.Empty<SettingsUnknownValuePersistenceDto>(),
            };
            var initial = SnapshotWithMute(registry, revision: 2, muted: true);
            var coordinator = adapter.CreateCoordinator(registry, initial);

            var loaded = coordinator.LoadFromRepository();

            Assert.That(loaded.Succeeded, Is.False);
            StringAssert.Contains("Validate:ValidateRejected", loaded.Error.Message);
            StringAssert.Contains("kind '999' is unsupported", loaded.Error.Message);
            Assert.That(coordinator.CommittedSnapshot, Is.SameAs(initial));
            Assert.That(adapter.LastLoadError.Value.Stage, Is.EqualTo(SaveStage.Validate));
            Assert.That(adapter.LastLoadError.Value.Code, Is.EqualTo(SaveErrorCode.ValidateRejected));
        }

        [Test]
        public void CorruptPrimaryFallsBackToBackupAndEveryLoadAttemptResetsDiagnostics()
        {
            var registry = CreateRegistry();
            var store = new InMemorySettingsSaveStore();
            var adapter = CreateAdapter(
                new RecordingJsonSettingsCodec(),
                store,
                SaveRecoveryPolicy.PrimaryThenBackup);
            Assert.That(
                adapter.Persist(
                    SnapshotWithMute(registry, revision: 6, muted: false)).Committed,
                Is.True);
            var backupEnvelope = store.CopyLastCommittedEnvelopeBytes();
            Assert.That(
                adapter.Persist(
                    SnapshotWithMute(registry, revision: 7, muted: true)).Committed,
                Is.True);
            var corruptPrimaryEnvelope = store.CopyLastCommittedEnvelopeBytes();
            corruptPrimaryEnvelope[corruptPrimaryEnvelope.Length - 1] ^= 0x20;
            store.ReplaceCandidates(
                Candidate(SaveCandidateKind.Primary, "primary", corruptPrimaryEnvelope),
                Candidate(SaveCandidateKind.Backup, "backup", backupEnvelope));

            var recovered = adapter.LoadSnapshot();

            Assert.That(recovered.Succeeded, Is.True, recovered.Error.Message);
            Assert.That(recovered.Value.Snapshot.Revision, Is.EqualTo(6));
            Assert.That(recovered.Value.SelectedCandidateKind, Is.EqualTo(SaveCandidateKind.Backup));
            Assert.That(recovered.Value.RecoveryOccurred, Is.True);
            Assert.That(recovered.Value.PrimaryFailureDiagnostic.HasValue, Is.True);
            Assert.That(
                recovered.Value.PrimaryFailureDiagnostic.Value.Code,
                Is.EqualTo(SaveErrorCode.CorruptPayload));
            Assert.That(adapter.LastLoadError, Is.Null);
            Assert.That(adapter.LastSelectedCandidateKind, Is.EqualTo(SaveCandidateKind.Backup));
            Assert.That(adapter.LastLoadRecoveryOccurred, Is.True);
            Assert.That(adapter.LastPrimaryFailureDiagnostic.HasValue, Is.True);

            store.ReplaceCandidates();
            var failedRetry = adapter.LoadSnapshot();

            Assert.That(failedRetry.Succeeded, Is.False);
            Assert.That(failedRetry.Error.Stage, Is.EqualTo(SaveStage.Read));
            Assert.That(failedRetry.Error.Code, Is.EqualTo(SaveErrorCode.NotFound));
            Assert.That(adapter.LastLoadError.HasValue, Is.True);
            Assert.That(adapter.LastSelectedCandidateKind, Is.Null);
            Assert.That(adapter.LastSelectedCandidateId, Is.Null);
            Assert.That(adapter.LastLoadRecoveryOccurred, Is.False);
            Assert.That(adapter.LastPrimaryFailureDiagnostic, Is.Null);

            store.ReplaceCandidates(Candidate(SaveCandidateKind.Primary, "primary", backupEnvelope));
            var primaryRetry = adapter.LoadSnapshot();

            Assert.That(primaryRetry.Succeeded, Is.True, primaryRetry.Error.Message);
            Assert.That(adapter.LastLoadError, Is.Null);
            Assert.That(adapter.LastSelectedCandidateKind, Is.EqualTo(SaveCandidateKind.Primary));
            Assert.That(adapter.LastLoadRecoveryOccurred, Is.False);
            Assert.That(adapter.LastPrimaryFailureDiagnostic, Is.Null);
        }

        private static SettingsPersistenceRehydrationAdapter CreateAdapter(
            RecordingJsonSettingsCodec codec,
            InMemorySettingsSaveStore store,
            SaveRecoveryPolicy recoveryPolicy = SaveRecoveryPolicy.PrimaryOnly)
        {
            var coordinator = new SaveCoordinator<SettingsSnapshotPersistenceDto>(
                SettingsPersistenceRehydrationAdapter.OuterPersistenceSchemaId,
                SettingsPersistenceRehydrationAdapter.OuterPersistenceSchemaVersion,
                "settings-test-build",
                codec,
                new Sha256IntegrityProvider(),
                new MigrationPipeline<SettingsSnapshotPersistenceDto>(
                    Array.Empty<ISaveMigration<SettingsSnapshotPersistenceDto>>()),
                store,
                SaveCommitCapabilities.BestEffortWrite,
                recoveryPolicy: recoveryPolicy);
            return new SettingsPersistenceRehydrationAdapter(
                coordinator,
                MustSlot("settings"));
        }

        private static SettingsRegistry CreateRegistry()
        {
            var definition = SettingDefinitionValidator.ValidateBuilt(
                MustKey("audio.mute"),
                SettingValueKind.Boolean,
                SettingValue.FromBoolean(false),
                SettingScope.User,
                0,
                false,
                null,
                null,
                null,
                default);
            Assert.That(definition.Succeeded, Is.True, definition.Error.Message);
            var registry = SettingsRegistry.Create(new[] { definition.Value });
            Assert.That(registry.Succeeded, Is.True, registry.Error.Message);
            return registry.Value;
        }

        private static SettingsSnapshot SnapshotWithMute(
            SettingsRegistry registry,
            long revision,
            bool muted)
        {
            return new SettingsSnapshot(
                revision,
                new Dictionary<ScopedSettingKey, SettingValue>
                {
                    {
                        new ScopedSettingKey(MustKey("audio.mute"), SettingScope.User),
                        SettingValue.FromBoolean(muted)
                    },
                },
                new Dictionary<string, SettingValue>());
        }

        private static SettingKey MustKey(string value)
        {
            var result = SettingKey.TryCreate(value);
            Assert.That(result.Succeeded, Is.True, result.Error.Message);
            return result.Value;
        }

        private static OptionId MustOption(string value)
        {
            var result = OptionId.TryCreate(value);
            Assert.That(result.Succeeded, Is.True, result.Error.Message);
            return result.Value;
        }

        private static SaveSlotId MustSlot(string value)
        {
            var result = SaveSlotId.TryCreate(value);
            Assert.That(result.Succeeded, Is.True, result.Error.Message);
            return result.Value;
        }

        private static SaveReadCandidate Candidate(
            SaveCandidateKind kind,
            string id,
            byte[] bytes)
        {
            var candidateId = SaveCandidateId.TryCreate(id);
            Assert.That(candidateId.Succeeded, Is.True, candidateId.Error.Message);
            return new SaveReadCandidate(kind, candidateId.Value, bytes);
        }

        private static byte[] ExtractPayload(byte[] envelopeBytes)
        {
            var decoded = SaveEnvelopeBinaryCodec.Decode(envelopeBytes);
            Assert.That(decoded.Succeeded, Is.True, decoded.Error.Message);
            return decoded.Value.Payload.ToArray();
        }

        private static void AssertWireRejected(string json, string expectedMessageFragment)
        {
            var codec = new JsonUtilitySaveCodec<SettingsSnapshotPersistenceDto>();
            var decoded = codec.Decode(
                SettingsPersistenceRehydrationAdapter.OuterPersistenceSchemaVersion,
                Encoding.UTF8.GetBytes(json));
            Assert.That(decoded.Succeeded, Is.True, decoded.Error.Message);

            var mapped = SettingsSnapshotPersistenceDtoMapper.ToSnapshot(decoded.Value);

            Assert.That(mapped.Succeeded, Is.False, json);
            Assert.That(mapped.Error.Stage, Is.EqualTo(SaveStage.Validate), json);
            Assert.That(mapped.Error.Code, Is.EqualTo(SaveErrorCode.ValidateRejected), json);
            StringAssert.Contains(expectedMessageFragment, mapped.Error.Message, json);
        }

        private static void AssertUnknownInteger(SettingsSnapshot snapshot, string key, long expected)
        {
            Assert.That(snapshot.TryGetUnknownValue(key, out var value), Is.True);
            Assert.That(value.Kind, Is.EqualTo(SettingValueKind.Integer));
            Assert.That(value.IntegerValue, Is.EqualTo(expected));
        }

        private static void AssertUnknownBoolean(SettingsSnapshot snapshot, string key, bool expected)
        {
            Assert.That(snapshot.TryGetUnknownValue(key, out var value), Is.True);
            Assert.That(value.Kind, Is.EqualTo(SettingValueKind.Boolean));
            Assert.That(value.BooleanValue, Is.EqualTo(expected));
        }

        private static void AssertUnknownFloat(SettingsSnapshot snapshot, string key, double expected)
        {
            Assert.That(snapshot.TryGetUnknownValue(key, out var value), Is.True);
            Assert.That(value.Kind, Is.EqualTo(SettingValueKind.Float));
            Assert.That(value.FloatValue, Is.EqualTo(expected).Within(1e-9));
        }

        private static void AssertUnknownString(SettingsSnapshot snapshot, string key, string expected)
        {
            Assert.That(snapshot.TryGetUnknownValue(key, out var value), Is.True);
            Assert.That(value.Kind, Is.EqualTo(SettingValueKind.String));
            Assert.That(value.StringValue, Is.EqualTo(expected));
        }

        private static void AssertUnknownOption(SettingsSnapshot snapshot, string key, string expected)
        {
            Assert.That(snapshot.TryGetUnknownValue(key, out var value), Is.True);
            Assert.That(value.Kind, Is.EqualTo(SettingValueKind.Option));
            Assert.That(value.OptionValue.Value, Is.EqualTo(expected));
        }

        private sealed class RecordingJsonSettingsCodec : ISaveCodec<SettingsSnapshotPersistenceDto>
        {
            private readonly JsonUtilitySaveCodec<SettingsSnapshotPersistenceDto> _inner =
                new JsonUtilitySaveCodec<SettingsSnapshotPersistenceDto>();

            public int LastDecodeSchemaVersion { get; private set; } = -1;
            public SettingsSnapshotPersistenceDto DecodeOverride { get; set; }

            public SaveResult<byte[]> Encode(SettingsSnapshotPersistenceDto snapshot)
            {
                return _inner.Encode(snapshot);
            }

            public SaveResult<SettingsSnapshotPersistenceDto> Decode(
                int schemaVersion,
                ReadOnlySpan<byte> bytes)
            {
                LastDecodeSchemaVersion = schemaVersion;
                return DecodeOverride == null
                    ? _inner.Decode(schemaVersion, bytes)
                    : SaveResult<SettingsSnapshotPersistenceDto>.Success(DecodeOverride);
            }
        }

        private sealed class MuteMustBeEnabledConstraint : ISettingsConstraint
        {
            public string ConstraintId => "mute-must-be-enabled";

            public SettingsResult Validate(SettingsRegistry registry, SettingsSnapshot candidate)
            {
                var hasMute = candidate.TryGetKnownValue(
                    new ScopedSettingKey(MustKey("audio.mute"), SettingScope.User),
                    out var mute);
                return hasMute && mute.BooleanValue
                    ? SettingsResult.Success()
                    : SettingsResult.Fail(
                        SettingsValidationCode.CrossConstraintViolation,
                        "Default-scope mute must be enabled.");
            }
        }

        private sealed class InMemorySettingsSaveStore : ISaveStore
        {
            private readonly List<SaveReadCandidate> _candidates = new List<SaveReadCandidate>();
            private byte[] _lastCommittedEnvelopeBytes = Array.Empty<byte>();

            public SaveCommitCapabilities Capabilities { get; set; } =
                SaveCommitCapabilities.BestEffortWrite;
            public SaveCommitResult? ForcedCommitResult { get; set; }
            public int CommitCallCount { get; private set; }

            public SaveResult<SaveReadCandidateSet> ReadCandidates(SaveSlotId slotId)
            {
                return _candidates.Count == 0
                    ? SaveResult<SaveReadCandidateSet>.Fail(
                        SaveStage.Read,
                        SaveErrorCode.NotFound,
                        "No in-memory settings save exists.")
                    : SaveResult<SaveReadCandidateSet>.Success(
                        new SaveReadCandidateSet(_candidates.ToArray()));
            }

            public SaveCommitResult Commit(
                SaveSlotId slotId,
                ReadOnlyMemory<byte> envelopeBytes,
                SaveCommitCapabilities requiredCapabilities)
            {
                CommitCallCount++;
                if (ForcedCommitResult.HasValue)
                {
                    return ForcedCommitResult.Value;
                }

                _lastCommittedEnvelopeBytes = envelopeBytes.ToArray();
                ReplaceCandidates(Candidate(
                    SaveCandidateKind.Primary,
                    "primary",
                    _lastCommittedEnvelopeBytes));
                return SaveCommitResult.Success();
            }

            public byte[] CopyLastCommittedEnvelopeBytes()
            {
                return (byte[])_lastCommittedEnvelopeBytes.Clone();
            }

            public void ReplaceCandidates(params SaveReadCandidate[] candidates)
            {
                _candidates.Clear();
                if (candidates != null)
                {
                    _candidates.AddRange(candidates);
                }
            }
        }
    }
}
