using System;
using System.Collections.Generic;
using Lingkyn.Persistence.Core;
using UnityEngine;

namespace Lingkyn.Persistence.Unity
{
    public enum LocalFileCommitStrategy
    {
        AtomicFileReplace = 0,
        RecoverableCopyReplace = 1,
        BestEffortDirectWrite = 2
    }

    [Serializable]
    public sealed class SaveMigrationEdgeDefinition
    {
        [SerializeField] private int fromVersion;
        [SerializeField] private int toVersion;

        public int FromVersion => fromVersion;
        public int ToVersion => toVersion;

        public SaveMigrationEdgeDefinition(int fromVersion, int toVersion)
        {
            this.fromVersion = fromVersion;
            this.toVersion = toVersion;
        }
    }

    [CreateAssetMenu(menuName = "Lingkyn/Persistence/Unity Config", fileName = "PersistenceUnityConfig")]
    public sealed class PersistenceUnityConfig : ScriptableObject
    {
        private const int MaxSchemaIdLength = SaveEnvelopeBinaryCodec.MaxSchemaIdBytes;
        private const int MaxCommitIdLength = SaveEnvelopeBinaryCodec.MaxCommitIdBytes;

        [SerializeField] private string schemaId = "lingkyn.state";
        [SerializeField] private int currentSchemaVersion;
        [SerializeField] private string commitId = "unity";
        [SerializeField] private string fileExtension = ".save";
        [SerializeField] private string storageSubdirectory = "saves";
        [SerializeField] private LocalFileCommitStrategy commitStrategy = LocalFileCommitStrategy.AtomicFileReplace;
        [SerializeField] private SaveCommitCapabilities requiredCommitCapability = SaveCommitCapabilities.RecoverableReplace;
        [SerializeField] private SaveRecoveryPolicy recoveryPolicy = SaveRecoveryPolicy.PrimaryThenBackup;
        [SerializeField] private string integrityAlgorithm = "sha-256";
        [SerializeField] private SaveMigrationEdgeDefinition[] migrationEdges = Array.Empty<SaveMigrationEdgeDefinition>();

        public string SchemaId => schemaId ?? string.Empty;
        public int CurrentSchemaVersion => currentSchemaVersion;
        public string CommitId => commitId ?? string.Empty;
        public string FileExtension => fileExtension ?? string.Empty;
        public string StorageSubdirectory => storageSubdirectory ?? string.Empty;
        public LocalFileCommitStrategy CommitStrategy => commitStrategy;
        public SaveCommitCapabilities RequiredCommitCapability => requiredCommitCapability;
        public SaveRecoveryPolicy RecoveryPolicy => recoveryPolicy;
        public string IntegrityAlgorithm => integrityAlgorithm ?? string.Empty;
        public IReadOnlyList<SaveMigrationEdgeDefinition> MigrationEdges => migrationEdges ?? Array.Empty<SaveMigrationEdgeDefinition>();

        public SaveResult ValidateAuthoring()
        {
            if (string.IsNullOrWhiteSpace(schemaId) || schemaId.Length > MaxSchemaIdLength)
            {
                return SaveResult.Fail(SaveStage.Snapshot, SaveErrorCode.UnsupportedFormat, "Schema id is missing or exceeds bounds.");
            }

            if (currentSchemaVersion < 0)
            {
                return SaveResult.Fail(SaveStage.Snapshot, SaveErrorCode.UnsupportedFormat, "Current schema version cannot be negative.");
            }

            if (string.IsNullOrWhiteSpace(commitId) || commitId.Length > MaxCommitIdLength)
            {
                return SaveResult.Fail(SaveStage.Snapshot, SaveErrorCode.UnsupportedFormat, "Commit id is missing or exceeds bounds.");
            }

            var extensionValidation = SavePathPolicy.ValidateFileExtension(fileExtension);
            if (!extensionValidation.Succeeded)
            {
                return extensionValidation;
            }

            var subdirectoryValidation = SavePathPolicy.ValidateStorageSubdirectory(storageSubdirectory);
            if (!subdirectoryValidation.Succeeded)
            {
                return subdirectoryValidation;
            }

            if (!string.Equals(integrityAlgorithm, "sha-256", StringComparison.Ordinal))
            {
                return SaveResult.Fail(SaveStage.Snapshot, SaveErrorCode.UnsupportedFormat, "Only sha-256 integrity is supported in this adapter.");
            }

            var migrationValidation = ValidateMigrationEdges(migrationEdges);
            if (!migrationValidation.Succeeded)
            {
                return migrationValidation;
            }

            var capabilityValidation = ValidateCapabilityContract(commitStrategy, requiredCommitCapability);
            if (!capabilityValidation.Succeeded)
            {
                return capabilityValidation;
            }

            if (recoveryPolicy != SaveRecoveryPolicy.PrimaryOnly
                && recoveryPolicy != SaveRecoveryPolicy.PrimaryThenBackup)
            {
                return SaveResult.Fail(SaveStage.Snapshot, SaveErrorCode.UnsupportedFormat, "Recovery policy is undefined.");
            }

            return SaveResult.Success();
        }

        public SaveCommitCapabilities ResolveAdvertisedCapabilities()
        {
            switch (commitStrategy)
            {
                case LocalFileCommitStrategy.AtomicFileReplace:
                    return SaveCommitCapabilities.BestEffortWrite
                        | SaveCommitCapabilities.RecoverableReplace
                        | SaveCommitCapabilities.AtomicReplace;
                case LocalFileCommitStrategy.RecoverableCopyReplace:
                    return SaveCommitCapabilities.BestEffortWrite | SaveCommitCapabilities.RecoverableReplace;
                case LocalFileCommitStrategy.BestEffortDirectWrite:
                    return SaveCommitCapabilities.BestEffortWrite;
                default:
                    return SaveCommitCapabilities.None;
            }
        }

        internal static SaveResult ValidateMigrationEdges(IReadOnlyList<SaveMigrationEdgeDefinition> edges)
        {
            if (edges == null)
            {
                return SaveResult.Success();
            }

            var seenFromVersions = new HashSet<int>();
            for (var index = 0; index < edges.Count; index++)
            {
                var edge = edges[index];
                if (edge == null)
                {
                    continue;
                }

                if (edge.ToVersion <= edge.FromVersion)
                {
                    return SaveResult.Fail(SaveStage.Migrate, SaveErrorCode.NonMonotonicMigration, "Migration edge must increase schema version.");
                }

                if (!seenFromVersions.Add(edge.FromVersion))
                {
                    return SaveResult.Fail(SaveStage.Migrate, SaveErrorCode.AmbiguousMigration, "Duplicate migration edge source version.");
                }
            }

            return SaveResult.Success();
        }

        internal static SaveResult ValidateCapabilityContract(
            LocalFileCommitStrategy strategy,
            SaveCommitCapabilities requiredCapability)
        {
            if ((requiredCapability & SaveCommitCapabilities.AtomicReplace) != 0
                && strategy != LocalFileCommitStrategy.AtomicFileReplace)
            {
                return SaveResult.Fail(
                    SaveStage.Commit,
                    SaveErrorCode.UnsupportedCommitCapability,
                    "AtomicReplace requires the AtomicFileReplace commit strategy.");
            }

            if ((requiredCapability & SaveCommitCapabilities.RecoverableReplace) != 0
                && strategy == LocalFileCommitStrategy.BestEffortDirectWrite)
            {
                return SaveResult.Fail(
                    SaveStage.Commit,
                    SaveErrorCode.UnsupportedCommitCapability,
                    "RecoverableReplace requires a recoverable commit strategy.");
            }

            var advertised = strategy switch
            {
                LocalFileCommitStrategy.AtomicFileReplace => SaveCommitCapabilities.BestEffortWrite
                    | SaveCommitCapabilities.RecoverableReplace
                    | SaveCommitCapabilities.AtomicReplace,
                LocalFileCommitStrategy.RecoverableCopyReplace => SaveCommitCapabilities.BestEffortWrite
                    | SaveCommitCapabilities.RecoverableReplace,
                LocalFileCommitStrategy.BestEffortDirectWrite => SaveCommitCapabilities.BestEffortWrite,
                _ => SaveCommitCapabilities.None
            };

            if ((advertised & requiredCapability) != requiredCapability)
            {
                return SaveResult.Fail(
                    SaveStage.Commit,
                    SaveErrorCode.UnsupportedCommitCapability,
                    "Configured commit strategy does not satisfy required commit capability.");
            }

            return SaveResult.Success();
        }
    }
}
