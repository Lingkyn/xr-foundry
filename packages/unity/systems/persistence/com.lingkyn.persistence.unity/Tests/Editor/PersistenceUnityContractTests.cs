using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Lingkyn.Persistence.Core;
using NUnit.Framework;
using UnityEngine;
#if !UNITY_INCLUDE_TESTS
using NUnit.Framework.Legacy;
#endif

namespace Lingkyn.Persistence.Unity.Editor.Tests
{
    public sealed class PersistenceUnityContractTests
    {
        private string _tempRoot;

        [SetUp]
        public void SetUp()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "lingkyn-persistence-unity-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(_tempRoot) && Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }

        [Test]
        public void ConfigValidationRejectsUnsafeExtension()
        {
            var extensionValidation = SavePathPolicy.ValidateFileExtension("../evil.save");
            Assert.That(extensionValidation.Succeeded, Is.False);
            Assert.That(extensionValidation.Error.Code, Is.EqualTo(SaveErrorCode.UnsupportedFormat));
        }

        [Test]
        public void ConfigValidationRejectsDuplicateMigrationEdges()
        {
            var config = ScriptableObject.CreateInstance<PersistenceUnityConfig>();
            var serialized = new SerializedConfig
            {
                schemaId = "lingkyn.state",
                currentSchemaVersion = 1,
                commitId = "test",
                fileExtension = ".save",
                storageSubdirectory = "saves",
                commitStrategy = LocalFileCommitStrategy.AtomicFileReplace,
                requiredCommitCapability = SaveCommitCapabilities.RecoverableReplace,
                recoveryPolicy = SaveRecoveryPolicy.PrimaryThenBackup,
                integrityAlgorithm = "sha-256",
                migrationEdges = new[]
                {
                    new SaveMigrationEdgeDefinition(0, 1),
                    new SaveMigrationEdgeDefinition(0, 2)
                }
            };
            ApplyConfig(config, serialized);

            var result = config.ValidateAuthoring();
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(SaveErrorCode.AmbiguousMigration));
        }

        [Test]
        public void ConfigRejectsAtomicRequiredCapabilityWithoutAtomicStrategy()
        {
            var validation = PersistenceUnityConfig.ValidateCapabilityContract(
                LocalFileCommitStrategy.BestEffortDirectWrite,
                SaveCommitCapabilities.AtomicReplace);

            Assert.That(validation.Succeeded, Is.False);
            Assert.That(validation.Error.Code, Is.EqualTo(SaveErrorCode.UnsupportedCommitCapability));
        }

        [Test]
        public void JsonUtilityCodecRejectsUnsupportedDtoShapes()
        {
            Assert.That(JsonUtilityDtoSupport.ValidateDtoType<UnsupportedUnityObjectDto>().Succeeded, Is.False);
            Assert.That(JsonUtilityDtoSupport.ValidateDtoType<UnsupportedDictionaryDto>().Succeeded, Is.False);
            Assert.That(JsonUtilityDtoSupport.ValidateDtoType<SupportedPlainDto>().Succeeded, Is.True);
        }

        [Test]
        public void JsonUtilityCodecRejectsNonSerializableDto()
        {
            Assert.That(JsonUtilityDtoSupport.ValidateDtoType<NonSerializableDto>().Succeeded, Is.False);
        }

        [Test]
        public void JsonUtilityCodecRejectsReadonlySerializedField()
        {
            Assert.That(JsonUtilityDtoSupport.ValidateDtoType<ReadonlySerializedDto>().Succeeded, Is.False);
        }

        [Test]
        public void JsonUtilityCodecRejectsInvalidUtf8Payload()
        {
            var codec = new JsonUtilitySaveCodec<SupportedPlainDto>();
            var invalidUtf8 = new byte[] { 0xFF, 0xFE, 0xFD };
            var decoded = codec.Decode(1, invalidUtf8);
            Assert.That(decoded.Succeeded, Is.False);
            Assert.That(decoded.Error.Code, Is.EqualTo(SaveErrorCode.UnsupportedFormat));
        }

        [Test]
        public void PathPolicyKeepsDerivedPathsInsideRoot()
        {
            var slot = MustSlot("slot_a");
            var resolved = SavePathPolicy.ResolveSlotPaths(_tempRoot, "saves", slot, ".save");
            Assert.That(resolved.Succeeded, Is.True);
            Assert.That(
                SavePathPolicy.IsPathContained(_tempRoot, resolved.Value.PrimaryPath),
                Is.True);
            Assert.That(
                SavePathPolicy.IsPathContained(_tempRoot, resolved.Value.BackupPath),
                Is.True);
        }

        [Test]
        public void RoundTripPersistsPlainDtoSnapshot()
        {
            var coordinator = CreateCoordinator(LocalFileCommitStrategy.RecoverableCopyReplace, SaveCommitCapabilities.RecoverableReplace);
            var slot = MustSlot("round_trip");
            var snapshot = new SupportedPlainDto { score = 42, label = "hello" };

            var save = coordinator.Save(slot, snapshot);
            Assert.That(save.Committed, Is.True);

            var loaded = coordinator.LoadValidated(slot, _ => SaveResult.Success());
            Assert.That(loaded.Succeeded, Is.True);
            Assert.That(loaded.Value.State.score, Is.EqualTo(42));
            Assert.That(loaded.Value.State.label, Is.EqualTo("hello"));
        }

        [Test]
        public void InitialCommitCreatesPrimaryWithoutBackup()
        {
            var store = CreateStore(LocalFileCommitStrategy.AtomicFileReplace);
            var slot = MustSlot("initial");
            var paths = MustPaths(slot);
            var commit = store.Commit(slot, SampleEnvelopeBytes("initial"), SaveCommitCapabilities.RecoverableReplace);

            Assert.That(commit.Committed, Is.True);
            Assert.That(File.Exists(paths.PrimaryPath), Is.True);
            Assert.That(File.Exists(paths.BackupPath), Is.False);
        }

        [Test]
        public void AtomicInitialPreconditionFailsWhenAtomicRequired()
        {
            var store = CreateStore(LocalFileCommitStrategy.AtomicFileReplace);
            var slot = MustSlot("atomic_initial");
            var paths = MustPaths(slot);

            var commit = store.Commit(slot, SampleEnvelopeBytes("initial"), SaveCommitCapabilities.AtomicReplace);

            Assert.That(commit.Committed, Is.False);
            Assert.That(commit.Error.Code, Is.EqualTo(SaveErrorCode.UnsupportedCommitCapability));
            Assert.That(File.Exists(paths.PrimaryPath), Is.False);
        }

        [Test]
        public void AtomicStrategyUsesReplaceEvenWhenLowerMinimumRequired()
        {
            var store = CreateStore(LocalFileCommitStrategy.AtomicFileReplace);
            var slot = MustSlot("atomic_replace");
            var paths = MustPaths(slot);

            Assert.That(store.Commit(slot, SampleEnvelopeBytes("first"), SaveCommitCapabilities.RecoverableReplace).Committed, Is.True);
            var priorPrimary = File.ReadAllBytes(paths.PrimaryPath);

            Assert.That(store.Commit(slot, SampleEnvelopeBytes("second"), SaveCommitCapabilities.RecoverableReplace).Committed, Is.True);
            Assert.That(File.Exists(paths.BackupPath), Is.True);
            Assert.That(File.ReadAllBytes(paths.BackupPath), Is.EqualTo(priorPrimary));
        }

        [Test]
        public void ReplacementCreatesBackupAndPreservesPriorOnFailure()
        {
            var inner = new DefaultFileOperationSeam();
            var seam = new FaultInjectingFileOperationSeam(inner);
            var store = CreateStore(LocalFileCommitStrategy.AtomicFileReplace, seam);
            var slot = MustSlot("replace");
            var paths = MustPaths(slot);

            var first = store.Commit(slot, SampleEnvelopeBytes("first"), SaveCommitCapabilities.RecoverableReplace);
            Assert.That(first.Committed, Is.True);
            var priorBytes = File.ReadAllBytes(paths.PrimaryPath);

            seam.ConfigureFault(FileOperationStage.Replace, SaveErrorCode.IoDenied, "replace denied");
            var second = store.Commit(slot, SampleEnvelopeBytes("second"), SaveCommitCapabilities.AtomicReplace);

            Assert.That(second.Committed, Is.False);
            Assert.That(second.PriorCommittedRecordPreserved, Is.True);
            Assert.That(second.Error.Stage, Is.EqualTo(SaveStage.Commit));
            Assert.That(second.Error.Code, Is.EqualTo(SaveErrorCode.IoDenied));
            Assert.That(File.ReadAllBytes(paths.PrimaryPath), Is.EqualTo(priorBytes));
        }

        [Test]
        public void ReadCandidatesExposePrimaryBackupAndStagingWithoutPromotingStaging()
        {
            var store = CreateStore(LocalFileCommitStrategy.RecoverableCopyReplace);
            var slot = MustSlot("inspect");
            var paths = MustPaths(slot);
            Directory.CreateDirectory(paths.SlotDirectory);

            File.WriteAllBytes(paths.PrimaryPath, SampleDtoEnvelopeBytes(new SupportedPlainDto { score = 1, label = "primary" }, 1));
            File.WriteAllBytes(paths.BackupPath, SampleDtoEnvelopeBytes(new SupportedPlainDto { score = 2, label = "backup" }, 1));
            var stagingPath = Path.Combine(paths.SlotDirectory, paths.SlotIdStem + ".staging.token.save");
            File.WriteAllBytes(stagingPath, SampleDtoEnvelopeBytes(new SupportedPlainDto { score = 3, label = "staging" }, 1));

            var read = store.ReadCandidates(slot);
            Assert.That(read.Succeeded, Is.True);
            Assert.That(read.Value.Candidates.Count, Is.EqualTo(3));

            var coordinator = CreateCoordinator(LocalFileCommitStrategy.RecoverableCopyReplace, SaveCommitCapabilities.RecoverableReplace);
            var loaded = coordinator.LoadValidated(slot, _ => SaveResult.Success());
            Assert.That(loaded.Succeeded, Is.True);
            Assert.That(loaded.Value.SelectedCandidateKind, Is.EqualTo(SaveCandidateKind.Primary));
            Assert.That(loaded.Value.State.label, Is.EqualTo("primary"));
            Assert.That(loaded.Value.RecoveryOccurred, Is.False);
        }

        [Test]
        public void StagingCandidatesAreEnumeratedInDeterministicOrder()
        {
            var store = CreateStore(LocalFileCommitStrategy.RecoverableCopyReplace);
            var slot = MustSlot("staging_order");
            var paths = MustPaths(slot);
            Directory.CreateDirectory(paths.SlotDirectory);

            File.WriteAllBytes(
                Path.Combine(paths.SlotDirectory, paths.SlotIdStem + ".staging.ztoken.save"),
                SampleEnvelopeBytes("z"));
            File.WriteAllBytes(
                Path.Combine(paths.SlotDirectory, paths.SlotIdStem + ".staging.atoken.save"),
                SampleEnvelopeBytes("a"));

            var read = store.ReadCandidates(slot);
            Assert.That(read.Succeeded, Is.True);
            Assert.That(read.Value.Candidates.Count, Is.EqualTo(2));
            Assert.That(read.Value.Candidates[0].Id.Value, Is.EqualTo("staging-atoken"));
            Assert.That(read.Value.Candidates[1].Id.Value, Is.EqualTo("staging-ztoken"));
        }

        [Test]
        public void EnumerationFaultMapsToReadStage()
        {
            var seam = new FaultInjectingFileOperationSeam(new DefaultFileOperationSeam());
            var store = CreateStore(LocalFileCommitStrategy.RecoverableCopyReplace, seam);
            var slot = MustSlot("enum_fail");
            var paths = MustPaths(slot);
            Directory.CreateDirectory(paths.SlotDirectory);
            File.WriteAllBytes(paths.PrimaryPath, SampleEnvelopeBytes("primary"));
            File.WriteAllBytes(
                Path.Combine(paths.SlotDirectory, paths.SlotIdStem + ".staging.token.save"),
                SampleEnvelopeBytes("staging"));

            seam.ConfigureFault(FileOperationStage.Enumerate, SaveErrorCode.IoDenied, "enumerate denied");
            var read = store.ReadCandidates(slot);
            Assert.That(read.Succeeded, Is.False);
            Assert.That(read.Error.Stage, Is.EqualTo(SaveStage.Read));
            Assert.That(read.Error.Code, Is.EqualTo(SaveErrorCode.IoDenied));
        }

        [Test]
        public void BackupFailureMapsToCommitAndPreservesPrimary()
        {
            var seam = new FaultInjectingFileOperationSeam(new DefaultFileOperationSeam());
            var store = CreateStore(LocalFileCommitStrategy.RecoverableCopyReplace, seam);
            var slot = MustSlot("backup_fail");
            var paths = MustPaths(slot);

            Assert.That(store.Commit(slot, SampleEnvelopeBytes("first"), SaveCommitCapabilities.RecoverableReplace).Committed, Is.True);
            var priorBytes = File.ReadAllBytes(paths.PrimaryPath);

            seam.ConfigureFault(FileOperationStage.Backup, SaveErrorCode.IoDenied, "backup denied");
            var failed = store.Commit(slot, SampleEnvelopeBytes("second"), SaveCommitCapabilities.RecoverableReplace);

            Assert.That(failed.Committed, Is.False);
            Assert.That(failed.Error.Stage, Is.EqualTo(SaveStage.Commit));
            Assert.That(failed.Error.Code, Is.EqualTo(SaveErrorCode.IoDenied));
            Assert.That(failed.PriorCommittedRecordPreserved, Is.True);
            Assert.That(File.ReadAllBytes(paths.PrimaryPath), Is.EqualTo(priorBytes));
        }

        [Test]
        public void RecoverableBackupCorruptionPreservesPrimaryWhenPrimaryUnchanged()
        {
            var store = CreateStore(
                LocalFileCommitStrategy.RecoverableCopyReplace,
                new CorruptingBackupFileOperationSeam(new DefaultFileOperationSeam()));
            var slot = MustSlot("backup_corrupt");
            var paths = MustPaths(slot);

            Assert.That(store.Commit(slot, SampleEnvelopeBytes("first"), SaveCommitCapabilities.RecoverableReplace).Committed, Is.True);
            var priorBytes = File.ReadAllBytes(paths.PrimaryPath);
            File.WriteAllBytes(paths.BackupPath, SampleEnvelopeBytes("backup-seed"));

            var failed = store.Commit(slot, SampleEnvelopeBytes("second"), SaveCommitCapabilities.RecoverableReplace);

            Assert.That(failed.Committed, Is.False);
            Assert.That(failed.PriorCommittedRecordPreserved, Is.True);
            Assert.That(File.ReadAllBytes(paths.PrimaryPath), Is.EqualTo(priorBytes));
            Assert.That(File.Exists(paths.BackupPath), Is.False);
        }

        [Test]
        public void RecoverableReplacePreservesPriorPrimaryInBackupWhenPrimaryLostAfterBackup()
        {
            var seam = new PrimaryLossAfterBackupFileOperationSeam(new DefaultFileOperationSeam());
            var store = CreateStore(LocalFileCommitStrategy.RecoverableCopyReplace, seam);
            var slot = MustSlot("backup_survives");
            var paths = MustPaths(slot);

            Assert.That(store.Commit(slot, SampleEnvelopeBytes("first"), SaveCommitCapabilities.RecoverableReplace).Committed, Is.True);
            var priorBytes = File.ReadAllBytes(paths.PrimaryPath);

            seam.ArmPrimaryLossAfterBackup();
            var failed = store.Commit(slot, SampleEnvelopeBytes("second"), SaveCommitCapabilities.RecoverableReplace);

            Assert.That(failed.Committed, Is.False);
            Assert.That(failed.PriorCommittedRecordPreserved, Is.True);
            Assert.That(File.Exists(paths.PrimaryPath), Is.False);
            Assert.That(File.ReadAllBytes(paths.BackupPath), Is.EqualTo(priorBytes));
        }

        [Test]
        public void StageWriteFailureMapsToStageWriteAndPreservesPrimary()
        {
            var seam = new FaultInjectingFileOperationSeam(new DefaultFileOperationSeam());
            var store = CreateStore(LocalFileCommitStrategy.RecoverableCopyReplace, seam);
            var slot = MustSlot("stage_fail");
            var paths = MustPaths(slot);

            Assert.That(store.Commit(slot, SampleEnvelopeBytes("first"), SaveCommitCapabilities.RecoverableReplace).Committed, Is.True);
            var priorBytes = File.ReadAllBytes(paths.PrimaryPath);

            seam.ConfigureFault(FileOperationStage.StageWrite, SaveErrorCode.IoDenied, "stage denied");
            var failed = store.Commit(slot, SampleEnvelopeBytes("second"), SaveCommitCapabilities.RecoverableReplace);
            Assert.That(failed.Committed, Is.False);
            Assert.That(failed.Error.Stage, Is.EqualTo(SaveStage.StageWrite));
            Assert.That(failed.PriorCommittedRecordPreserved, Is.True);
            Assert.That(File.ReadAllBytes(paths.PrimaryPath), Is.EqualTo(priorBytes));
        }

        [Test]
        public void BestEffortFailurePreservationFalseWhenPrimaryLost()
        {
            var destroying = new ArmedDestroyingMoveFileOperationSeam(new DefaultFileOperationSeam());
            var store = CreateStore(LocalFileCommitStrategy.BestEffortDirectWrite, destroying);
            var slot = MustSlot("best_effort_loss");
            var paths = MustPaths(slot);

            Assert.That(store.Commit(slot, SampleEnvelopeBytes("first"), SaveCommitCapabilities.BestEffortWrite).Committed, Is.True);
            destroying.Arm();

            var failed = store.Commit(slot, SampleEnvelopeBytes("second"), SaveCommitCapabilities.BestEffortWrite);
            Assert.That(failed.Committed, Is.False);
            Assert.That(failed.PriorCommittedRecordPreserved, Is.False);
            Assert.That(File.Exists(paths.PrimaryPath), Is.False);
        }

        [Test]
        public void FlushFailureMapsToFlushAndPreservesPrimary()
        {
            var seam = new FaultInjectingFileOperationSeam(new DefaultFileOperationSeam());
            var store = CreateStore(LocalFileCommitStrategy.RecoverableCopyReplace, seam);
            var slot = MustSlot("flush_fail");
            var paths = MustPaths(slot);

            Assert.That(store.Commit(slot, SampleEnvelopeBytes("first"), SaveCommitCapabilities.RecoverableReplace).Committed, Is.True);
            var priorBytes = File.ReadAllBytes(paths.PrimaryPath);

            seam.ConfigureFault(FileOperationStage.Flush, SaveErrorCode.OutOfSpace, "disk full");
            var failed = store.Commit(slot, SampleEnvelopeBytes("second"), SaveCommitCapabilities.RecoverableReplace);

            Assert.That(failed.Committed, Is.False);
            Assert.That(failed.Error.Stage, Is.EqualTo(SaveStage.Flush));
            Assert.That(failed.Error.Code, Is.EqualTo(SaveErrorCode.OutOfSpace));
            Assert.That(failed.PriorCommittedRecordPreserved, Is.True);
            Assert.That(File.ReadAllBytes(paths.PrimaryPath), Is.EqualTo(priorBytes));
        }

        [Test]
        public void ReadFailureMapsToReadStage()
        {
            var seam = new FaultInjectingFileOperationSeam(new DefaultFileOperationSeam());
            seam.ConfigureFault(FileOperationStage.Read, SaveErrorCode.IoDenied, "read denied");
            var store = CreateStore(LocalFileCommitStrategy.RecoverableCopyReplace, seam);
            var slot = MustSlot("read_fail");
            var paths = MustPaths(slot);
            Directory.CreateDirectory(paths.SlotDirectory);
            File.WriteAllBytes(paths.PrimaryPath, SampleEnvelopeBytes("primary"));

            var read = store.ReadCandidates(slot);
            Assert.That(read.Succeeded, Is.False);
            Assert.That(read.Error.Stage, Is.EqualTo(SaveStage.Read));
            Assert.That(read.Error.Code, Is.EqualTo(SaveErrorCode.IoDenied));
        }

        [Test]
        public void AtomicReplaceCapabilityAdvertisedOnlyForAtomicStrategy()
        {
            Assert.That(
                (LocalFileSaveStore.ResolveCapabilities(LocalFileCommitStrategy.AtomicFileReplace) & SaveCommitCapabilities.AtomicReplace) != 0,
                Is.True);
            Assert.That(
                (LocalFileSaveStore.ResolveCapabilities(LocalFileCommitStrategy.RecoverableCopyReplace) & SaveCommitCapabilities.AtomicReplace) != 0,
                Is.False);
        }

        [Test]
        public void CoordinatorRejectsCapabilityMismatchBeforeCommit()
        {
            var config = CreateValidConfig(
                LocalFileCommitStrategy.BestEffortDirectWrite,
                SaveCommitCapabilities.AtomicReplace);
            var created = PersistenceUnityFactory.CreateCoordinator(
                config,
                new InjectedPersistentDataRootProvider(_tempRoot),
                new JsonUtilitySaveCodec<SupportedPlainDto>());
            Assert.That(created.Succeeded, Is.False);
            Assert.That(created.Error.Code, Is.EqualTo(SaveErrorCode.UnsupportedCommitCapability));
        }

        [Test]
        public void DirectStoreBoundaryRejectsDefaultSlot()
        {
            var store = CreateStore(LocalFileCommitStrategy.RecoverableCopyReplace);
            var read = store.ReadCandidates(default);
            Assert.That(read.Succeeded, Is.False);
            Assert.That(read.Error.Code, Is.EqualTo(SaveErrorCode.InvalidSlot));

            var commit = store.Commit(default, SampleEnvelopeBytes("payload"), SaveCommitCapabilities.RecoverableReplace);
            Assert.That(commit.Committed, Is.False);
            Assert.That(commit.Error.Code, Is.EqualTo(SaveErrorCode.InvalidSlot));
        }

        [Test]
        public void DirectStoreBoundaryRejectsUnsafeExtension()
        {
            Assert.That(
                () => new LocalFileSaveStore(
                    new InjectedPersistentDataRootProvider(_tempRoot),
                    "saves",
                    "../evil.save",
                    LocalFileCommitStrategy.RecoverableCopyReplace),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void DirectStoreBoundaryRejectsUnsafeSubdirectory()
        {
            var validation = LocalFileSaveStore.ValidateStoreConfiguration("..", ".save", LocalFileCommitStrategy.RecoverableCopyReplace);
            Assert.That(validation.Succeeded, Is.False);
            Assert.That(validation.Error.Code, Is.EqualTo(SaveErrorCode.UnsupportedFormat));
        }

        [Test]
        public void DirectStoreBoundaryRejectsUndefinedStrategy()
        {
            var validation = LocalFileSaveStore.ValidateStoreConfiguration("saves", ".save", (LocalFileCommitStrategy)99);
            Assert.That(validation.Succeeded, Is.False);
            Assert.That(validation.Error.Code, Is.EqualTo(SaveErrorCode.UnsupportedFormat));
        }

        [Test]
        public void TestsNeverWriteToProductionPersistentDataPath()
        {
            var productionPath = Application.persistentDataPath;
            Assert.That(_tempRoot, Is.Not.EqualTo(productionPath));
            Assert.That(Path.GetFullPath(_tempRoot), Is.Not.EqualTo(Path.GetFullPath(productionPath)));
        }

        [Serializable]
        private sealed class SupportedPlainDto
        {
            public int score;
            public string label;
        }

        private sealed class NonSerializableDto
        {
            public int score;
        }

        [Serializable]
        private sealed class ReadonlySerializedDto
        {
            public readonly int score;
        }

        [Serializable]
        private sealed class UnsupportedUnityObjectDto
        {
            public UnityEngine.Object reference;
        }

        [Serializable]
        private sealed class UnsupportedDictionaryDto
        {
            public Dictionary<string, int> values;
        }

        private sealed class DelegateMigration : ISaveMigration<SupportedPlainDto>
        {
            private readonly Func<SupportedPlainDto, SupportedPlainDto> _apply;

            public DelegateMigration(int fromVersion, int toVersion, Func<SupportedPlainDto, SupportedPlainDto> apply)
            {
                FromVersion = fromVersion;
                ToVersion = toVersion;
                _apply = apply;
            }

            public int FromVersion { get; }
            public int ToVersion { get; }

            public SupportedPlainDto Migrate(SupportedPlainDto state) => _apply(state);
        }

        private sealed class ArmedDestroyingMoveFileOperationSeam : IFileOperationSeam
        {
            private readonly IFileOperationSeam _inner;
            private bool _armed;

            public ArmedDestroyingMoveFileOperationSeam(IFileOperationSeam inner)
            {
                _inner = inner;
            }

            public void Arm() => _armed = true;

            public Stream CreateWriteStream(string path) => _inner.CreateWriteStream(path);
            public void FlushToDisk(Stream stream) => _inner.FlushToDisk(stream);
            public bool FileExists(string path) => _inner.FileExists(path);
            public byte[] ReadAllBytes(string path) => _inner.ReadAllBytes(path);
            public string[] EnumerateFiles(string directoryPath, string searchPattern, SearchOption searchOption)
                => _inner.EnumerateFiles(directoryPath, searchPattern, searchOption);
            public void DeleteFile(string path) => _inner.DeleteFile(path);
            public void CopyFile(string sourcePath, string destinationPath, bool overwrite)
                => _inner.CopyFile(sourcePath, destinationPath, overwrite);
            public void ReplaceFile(string sourcePath, string destinationPath, string destinationBackupPath)
                => _inner.ReplaceFile(sourcePath, destinationPath, destinationBackupPath);
            public void EnsureDirectory(string directoryPath) => _inner.EnsureDirectory(directoryPath);

            public void MoveFile(string sourcePath, string destinationPath, bool overwrite)
            {
                if (!_armed)
                {
                    _inner.MoveFile(sourcePath, destinationPath, overwrite);
                    return;
                }

                if (overwrite && _inner.FileExists(destinationPath))
                {
                    _inner.DeleteFile(destinationPath);
                }

                throw new IOException("Simulated destructive move failure.");
            }
        }

        private sealed class PrimaryLossAfterBackupFileOperationSeam : IFileOperationSeam
        {
            private readonly IFileOperationSeam _inner;
            private bool _armed;

            public PrimaryLossAfterBackupFileOperationSeam(IFileOperationSeam inner)
            {
                _inner = inner;
            }

            public void ArmPrimaryLossAfterBackup() => _armed = true;

            public Stream CreateWriteStream(string path) => _inner.CreateWriteStream(path);
            public void FlushToDisk(Stream stream) => _inner.FlushToDisk(stream);
            public bool FileExists(string path) => _inner.FileExists(path);
            public byte[] ReadAllBytes(string path) => _inner.ReadAllBytes(path);
            public string[] EnumerateFiles(string directoryPath, string searchPattern, SearchOption searchOption)
                => _inner.EnumerateFiles(directoryPath, searchPattern, searchOption);
            public void DeleteFile(string path) => _inner.DeleteFile(path);
            public void CopyFile(string sourcePath, string destinationPath, bool overwrite)
                => _inner.CopyFile(sourcePath, destinationPath, overwrite);
            public void ReplaceFile(string sourcePath, string destinationPath, string destinationBackupPath)
                => _inner.ReplaceFile(sourcePath, destinationPath, destinationBackupPath);
            public void EnsureDirectory(string directoryPath) => _inner.EnsureDirectory(directoryPath);

            public void MoveFile(string sourcePath, string destinationPath, bool overwrite)
            {
                if (_armed && overwrite && _inner.FileExists(destinationPath))
                {
                    _inner.DeleteFile(destinationPath);
                    throw new IOException("Simulated primary loss after backup.");
                }

                _inner.MoveFile(sourcePath, destinationPath, overwrite);
            }
        }

        private sealed class CorruptingBackupFileOperationSeam : IFileOperationSeam
        {
            private readonly IFileOperationSeam _inner;

            public CorruptingBackupFileOperationSeam(IFileOperationSeam inner)
            {
                _inner = inner;
            }

            public Stream CreateWriteStream(string path) => _inner.CreateWriteStream(path);
            public void FlushToDisk(Stream stream) => _inner.FlushToDisk(stream);
            public bool FileExists(string path) => _inner.FileExists(path);
            public byte[] ReadAllBytes(string path) => _inner.ReadAllBytes(path);
            public string[] EnumerateFiles(string directoryPath, string searchPattern, SearchOption searchOption)
                => _inner.EnumerateFiles(directoryPath, searchPattern, searchOption);
            public void DeleteFile(string path) => _inner.DeleteFile(path);
            public void MoveFile(string sourcePath, string destinationPath, bool overwrite)
                => _inner.MoveFile(sourcePath, destinationPath, overwrite);
            public void ReplaceFile(string sourcePath, string destinationPath, string destinationBackupPath)
                => _inner.ReplaceFile(sourcePath, destinationPath, destinationBackupPath);
            public void EnsureDirectory(string directoryPath) => _inner.EnsureDirectory(directoryPath);

            public void CopyFile(string sourcePath, string destinationPath, bool overwrite)
            {
                _inner.CopyFile(sourcePath, destinationPath, overwrite);
                _inner.DeleteFile(destinationPath);
                throw new IOException("Simulated backup corruption.");
            }
        }

        private struct SerializedConfig
        {
            public string schemaId;
            public int currentSchemaVersion;
            public string commitId;
            public string fileExtension;
            public string storageSubdirectory;
            public LocalFileCommitStrategy commitStrategy;
            public SaveCommitCapabilities requiredCommitCapability;
            public SaveRecoveryPolicy recoveryPolicy;
            public string integrityAlgorithm;
            public SaveMigrationEdgeDefinition[] migrationEdges;
        }

        private SaveCoordinator<SupportedPlainDto> CreateCoordinator(
            LocalFileCommitStrategy strategy,
            SaveCommitCapabilities requiredCapability)
        {
            var config = CreateValidConfig(strategy, requiredCapability);
            var created = PersistenceUnityFactory.CreateCoordinator(
                config,
                new InjectedPersistentDataRootProvider(_tempRoot),
                new JsonUtilitySaveCodec<SupportedPlainDto>(),
                new ISaveMigration<SupportedPlainDto>[]
                {
                    new DelegateMigration(0, 1, dto =>
                    {
                        dto.label += "|migrated";
                        return dto;
                    })
                });
            Assert.That(created.Succeeded, Is.True, created.Error.Message);
            return created.Value;
        }

        private LocalFileSaveStore CreateStore(LocalFileCommitStrategy strategy, IFileOperationSeam seam = null)
        {
            return new LocalFileSaveStore(
                new InjectedPersistentDataRootProvider(_tempRoot),
                "saves",
                ".save",
                strategy,
                seam ?? new DefaultFileOperationSeam());
        }

        private SlotPaths MustPaths(SaveSlotId slot)
        {
            var resolved = SavePathPolicy.ResolveSlotPaths(_tempRoot, "saves", slot, ".save");
            Assert.That(resolved.Succeeded, Is.True);
            return resolved.Value;
        }

        private static PersistenceUnityConfig CreateValidConfig(
            LocalFileCommitStrategy strategy,
            SaveCommitCapabilities requiredCapability)
        {
            var config = ScriptableObject.CreateInstance<PersistenceUnityConfig>();
            ApplyConfig(config, new SerializedConfig
            {
                schemaId = "lingkyn.state",
                currentSchemaVersion = 1,
                commitId = "unity-test",
                fileExtension = ".save",
                storageSubdirectory = "saves",
                commitStrategy = strategy,
                requiredCommitCapability = requiredCapability,
                recoveryPolicy = SaveRecoveryPolicy.PrimaryThenBackup,
                integrityAlgorithm = "sha-256",
                migrationEdges = new[] { new SaveMigrationEdgeDefinition(0, 1) }
            });
            return config;
        }

        private static void ApplyConfig(PersistenceUnityConfig config, SerializedConfig values)
        {
            var type = typeof(PersistenceUnityConfig);
            SetField(type, config, "schemaId", values.schemaId);
            SetField(type, config, "currentSchemaVersion", values.currentSchemaVersion);
            SetField(type, config, "commitId", values.commitId);
            SetField(type, config, "fileExtension", values.fileExtension);
            SetField(type, config, "storageSubdirectory", values.storageSubdirectory);
            SetField(type, config, "commitStrategy", values.commitStrategy);
            SetField(type, config, "requiredCommitCapability", values.requiredCommitCapability);
            SetField(type, config, "recoveryPolicy", values.recoveryPolicy);
            SetField(type, config, "integrityAlgorithm", values.integrityAlgorithm);
            SetField(type, config, "migrationEdges", values.migrationEdges ?? Array.Empty<SaveMigrationEdgeDefinition>());
        }

        private static void SetField(System.Type type, object target, string fieldName, object value)
        {
            var field = type.GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Missing field " + fieldName);
            field.SetValue(target, value);
        }

        private static SaveSlotId MustSlot(string value)
        {
            var slot = SaveSlotId.TryCreate(value);
            Assert.That(slot.Succeeded, Is.True);
            return slot.Value;
        }

        private static byte[] SampleDtoEnvelopeBytes(SupportedPlainDto dto, int schemaVersion)
        {
            var codec = new JsonUtilitySaveCodec<SupportedPlainDto>();
            var payload = codec.Encode(dto).Value;
            var digest = new Sha256IntegrityProvider().ComputeDigest(payload).Value;
            var envelope = new SaveEnvelope(
                "lingkyn.state",
                schemaVersion,
                "unity-test",
                DateTime.UtcNow.Ticks,
                "sha-256",
                digest,
                payload);
            return SaveEnvelopeBinaryCodec.Encode(envelope).Value;
        }

        private static byte[] SampleEnvelopeBytes(string payloadText)
        {
            var payload = Encoding.UTF8.GetBytes(payloadText);
            var digest = new Sha256IntegrityProvider().ComputeDigest(payload).Value;
            var envelope = new SaveEnvelope(
                "lingkyn.state",
                0,
                "unity-test",
                DateTime.UtcNow.Ticks,
                "sha-256",
                digest,
                payload);
            return SaveEnvelopeBinaryCodec.Encode(envelope).Value;
        }
    }
}
