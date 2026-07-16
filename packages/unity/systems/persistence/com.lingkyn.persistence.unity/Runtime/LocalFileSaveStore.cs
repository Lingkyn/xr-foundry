using System;
using System.Collections.Generic;
using System.IO;
using Lingkyn.Persistence.Core;

namespace Lingkyn.Persistence.Unity
{
    public sealed class LocalFileSaveStore : ISaveStore
    {
        private readonly IPersistentDataRootProvider _rootProvider;
        private readonly string _storageSubdirectory;
        private readonly string _fileExtension;
        private readonly LocalFileCommitStrategy _commitStrategy;
        private readonly IFileOperationSeam _fileOperations;

        public LocalFileSaveStore(
            IPersistentDataRootProvider rootProvider,
            string storageSubdirectory,
            string fileExtension,
            LocalFileCommitStrategy commitStrategy)
            : this(rootProvider, storageSubdirectory, fileExtension, commitStrategy, new DefaultFileOperationSeam())
        {
        }

        internal LocalFileSaveStore(
            IPersistentDataRootProvider rootProvider,
            string storageSubdirectory,
            string fileExtension,
            LocalFileCommitStrategy commitStrategy,
            IFileOperationSeam fileOperations)
        {
            _rootProvider = rootProvider ?? throw new ArgumentNullException(nameof(rootProvider));
            _storageSubdirectory = storageSubdirectory ?? string.Empty;
            _fileExtension = fileExtension ?? throw new ArgumentNullException(nameof(fileExtension));
            _commitStrategy = commitStrategy;
            _fileOperations = fileOperations ?? throw new ArgumentNullException(nameof(fileOperations));

            var validation = ValidateStoreConfiguration(_storageSubdirectory, _fileExtension, _commitStrategy);
            if (!validation.Succeeded)
            {
                throw new InvalidOperationException(validation.Error.Message);
            }
        }

        public SaveCommitCapabilities Capabilities => ResolveCapabilities(_commitStrategy);

        public SaveResult<SaveReadCandidateSet> ReadCandidates(SaveSlotId slotId)
        {
            var slotValidation = ValidateSlot(slotId);
            if (!slotValidation.Succeeded)
            {
                return SaveResult<SaveReadCandidateSet>.Fail(
                    slotValidation.Error.Stage,
                    slotValidation.Error.Code,
                    slotValidation.Error.Message);
            }

            var pathsResult = ResolvePaths(slotId);
            if (!pathsResult.Succeeded)
            {
                return SaveResult<SaveReadCandidateSet>.Fail(pathsResult.Error.Stage, pathsResult.Error.Code, pathsResult.Error.Message);
            }

            var paths = pathsResult.Value;
            var candidates = new List<SaveReadCandidate>(3);

            try
            {
                if (_fileOperations.FileExists(paths.PrimaryPath))
                {
                    var primaryBytes = _fileOperations.ReadAllBytes(paths.PrimaryPath);
                    var primaryId = MustCandidateId(SavePathPolicy.PrimaryCandidateId);
                    candidates.Add(new SaveReadCandidate(SaveCandidateKind.Primary, primaryId, primaryBytes));
                }

                if (_fileOperations.FileExists(paths.BackupPath))
                {
                    var backupBytes = _fileOperations.ReadAllBytes(paths.BackupPath);
                    var backupId = MustCandidateId(SavePathPolicy.BackupCandidateId);
                    candidates.Add(new SaveReadCandidate(SaveCandidateKind.Backup, backupId, backupBytes));
                }

                AddStagingCandidates(paths, candidates);

                if (candidates.Count == 0)
                {
                    return SaveResult<SaveReadCandidateSet>.Fail(SaveStage.Read, SaveErrorCode.NotFound, "No save candidates exist for slot.");
                }

                return SaveResult<SaveReadCandidateSet>.Success(new SaveReadCandidateSet(candidates));
            }
            catch (Exception exception)
            {
                return FileIoErrorMapper.MapReadFailure<SaveReadCandidateSet>(exception);
            }
        }

        public SaveCommitResult Commit(
            SaveSlotId slotId,
            ReadOnlyMemory<byte> envelopeBytes,
            SaveCommitCapabilities requiredCapabilities)
        {
            var slotValidation = ValidateSlot(slotId);
            if (!slotValidation.Succeeded)
            {
                return SaveCommitResult.NotCommitted(
                    slotValidation.Error.Stage,
                    slotValidation.Error.Code,
                    slotValidation.Error.Message,
                    true);
            }

            if ((Capabilities & requiredCapabilities) != requiredCapabilities)
            {
                return SaveCommitResult.NotCommitted(
                    SaveStage.Commit,
                    SaveErrorCode.UnsupportedCommitCapability,
                    $"Store capabilities {Capabilities} do not satisfy required {requiredCapabilities}.",
                    true);
            }

            var pathsResult = ResolvePaths(slotId);
            if (!pathsResult.Succeeded)
            {
                return SaveCommitResult.NotCommitted(
                    pathsResult.Error.Stage,
                    pathsResult.Error.Code,
                    pathsResult.Error.Message,
                    true);
            }

            var paths = pathsResult.Value;
            var primaryExisted = _fileOperations.FileExists(paths.PrimaryPath);
            byte[] priorPrimaryBytes = null;

            if (primaryExisted)
            {
                try
                {
                    priorPrimaryBytes = _fileOperations.ReadAllBytes(paths.PrimaryPath);
                }
                catch (Exception exception)
                {
                    return FileIoErrorMapper.MapCommitFailure(
                        exception,
                        SaveStage.Read,
                        ComputePriorCommittedRecordPreserved(paths, priorPrimaryBytes));
                }
            }

            var stagingToken = Guid.NewGuid().ToString("N");
            var stagingPathResult = SavePathPolicy.ResolveStagingPath(paths, stagingToken);
            if (!stagingPathResult.Succeeded)
            {
                return SaveCommitResult.NotCommitted(
                    stagingPathResult.Error.Stage,
                    stagingPathResult.Error.Code,
                    stagingPathResult.Error.Message,
                    ComputePriorCommittedRecordPreserved(paths, priorPrimaryBytes));
            }

            var stagingPath = stagingPathResult.Value;
            string committedStagingPath = null;
            var failureStage = SaveStage.StageWrite;

            try
            {
                _fileOperations.EnsureDirectory(paths.SlotDirectory);
                using (var stream = _fileOperations.CreateWriteStream(stagingPath))
                {
                    var bytes = envelopeBytes.ToArray();
                    stream.Write(bytes, 0, bytes.Length);
                    try
                    {
                        _fileOperations.FlushToDisk(stream);
                    }
                    catch (Exception exception)
                    {
                        failureStage = SaveStage.Flush;
                        throw;
                    }
                }

                committedStagingPath = stagingPath;
                failureStage = SaveStage.Commit;
                return CommitStagedFile(
                    paths,
                    stagingPath,
                    primaryExisted,
                    requiredCapabilities,
                    priorPrimaryBytes);
            }
            catch (Exception exception)
            {
                return FileIoErrorMapper.MapCommitFailure(
                    exception,
                    failureStage,
                    ComputePriorCommittedRecordPreserved(paths, priorPrimaryBytes));
            }
            finally
            {
                if (committedStagingPath != null && _fileOperations.FileExists(committedStagingPath))
                {
                    try
                    {
                        _fileOperations.DeleteFile(committedStagingPath);
                    }
                    catch
                    {
                        // Best-effort staging cleanup; commit result already decided.
                    }
                }
            }
        }

        internal static SaveCommitCapabilities ResolveCapabilities(LocalFileCommitStrategy strategy)
        {
            switch (strategy)
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

        internal static SaveResult ValidateStoreConfiguration(
            string storageSubdirectory,
            string fileExtension,
            LocalFileCommitStrategy commitStrategy)
        {
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

            if (!Enum.IsDefined(typeof(LocalFileCommitStrategy), commitStrategy))
            {
                return SaveResult.Fail(SaveStage.Snapshot, SaveErrorCode.UnsupportedFormat, "Commit strategy is undefined.");
            }

            try
            {
                var probeSlot = SaveSlotId.TryCreate("__path_probe__");
                if (!probeSlot.Succeeded)
                {
                    return SaveResult.Fail(probeSlot.Error.Stage, probeSlot.Error.Code, probeSlot.Error.Message);
                }

                var probeRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "lingkyn-path-probe-" + Guid.NewGuid().ToString("N")));
                var resolved = SavePathPolicy.ResolveSlotPaths(probeRoot, storageSubdirectory, probeSlot.Value, fileExtension);
                if (!resolved.Succeeded)
                {
                    return SaveResult.Fail(resolved.Error.Stage, resolved.Error.Code, resolved.Error.Message);
                }
            }
            catch (Exception exception)
            {
                return SaveResult.Fail(
                    SaveStage.Snapshot,
                    SaveErrorCode.UnsupportedFormat,
                    $"Path normalization failed: {exception.Message}");
            }

            return SaveResult.Success();
        }

        private SaveCommitResult CommitStagedFile(
            SlotPaths paths,
            string stagingPath,
            bool primaryExisted,
            SaveCommitCapabilities requiredCapabilities,
            byte[] priorPrimaryBytes)
        {
            if (!primaryExisted)
            {
                if (_commitStrategy == LocalFileCommitStrategy.AtomicFileReplace
                    && (requiredCapabilities & SaveCommitCapabilities.AtomicReplace) != 0)
                {
                    return SaveCommitResult.NotCommitted(
                        SaveStage.Commit,
                        SaveErrorCode.UnsupportedCommitCapability,
                        "AtomicReplace requires an existing primary record.",
                        ComputePriorCommittedRecordPreserved(paths, priorPrimaryBytes));
                }

                _fileOperations.MoveFile(stagingPath, paths.PrimaryPath, overwrite: false);
                return SaveCommitResult.Success();
            }

            switch (_commitStrategy)
            {
                case LocalFileCommitStrategy.AtomicFileReplace:
                    _fileOperations.ReplaceFile(stagingPath, paths.PrimaryPath, paths.BackupPath);
                    return SaveCommitResult.Success();
                case LocalFileCommitStrategy.RecoverableCopyReplace:
                    _fileOperations.CopyFile(paths.PrimaryPath, paths.BackupPath, overwrite: true);
                    _fileOperations.MoveFile(stagingPath, paths.PrimaryPath, overwrite: true);
                    return SaveCommitResult.Success();
                case LocalFileCommitStrategy.BestEffortDirectWrite:
                    _fileOperations.MoveFile(stagingPath, paths.PrimaryPath, overwrite: true);
                    return SaveCommitResult.Success();
                default:
                    return SaveCommitResult.NotCommitted(
                        SaveStage.Commit,
                        SaveErrorCode.UnsupportedCommitCapability,
                        "Commit strategy is undefined.",
                        ComputePriorCommittedRecordPreserved(paths, priorPrimaryBytes));
            }
        }

        private void AddStagingCandidates(SlotPaths paths, List<SaveReadCandidate> candidates)
        {
            var files = _fileOperations.EnumerateFiles(
                paths.SlotDirectory,
                "*" + paths.FileExtension,
                SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.Ordinal);

            var stagingPrefix = paths.SlotIdStem + ".staging.";
            for (var index = 0; index < files.Length; index++)
            {
                var fileName = Path.GetFileName(files[index]);
                if (!fileName.StartsWith(stagingPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var token = fileName.Substring(stagingPrefix.Length, fileName.Length - stagingPrefix.Length - paths.FileExtension.Length);
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                var bytes = _fileOperations.ReadAllBytes(files[index]);
                var candidateId = MustCandidateId(SavePathPolicy.StagingCandidateIdPrefix + token);
                candidates.Add(new SaveReadCandidate(SaveCandidateKind.Staging, candidateId, bytes));
            }
        }

        private SaveResult<SlotPaths> ResolvePaths(SaveSlotId slotId)
        {
            var root = _rootProvider.ResolveRoot();
            if (!root.Succeeded)
            {
                return SaveResult<SlotPaths>.Fail(root.Error.Stage, root.Error.Code, root.Error.Message);
            }

            try
            {
                return SavePathPolicy.ResolveSlotPaths(root.Value, _storageSubdirectory, slotId, _fileExtension);
            }
            catch (Exception exception)
            {
                return SaveResult<SlotPaths>.Fail(
                    SaveStage.Snapshot,
                    SaveErrorCode.UnsupportedFormat,
                    $"Path normalization failed: {exception.Message}");
            }
        }

        private bool ComputePriorCommittedRecordPreserved(
            SlotPaths paths,
            byte[] expectedPrimaryBytes)
        {
            if (expectedPrimaryBytes == null)
            {
                return true;
            }

            try
            {
                if (PathBytesEqual(paths.PrimaryPath, expectedPrimaryBytes))
                {
                    return true;
                }

                return PathBytesEqual(paths.BackupPath, expectedPrimaryBytes);
            }
            catch
            {
                return false;
            }
        }

        private bool PathBytesEqual(string path, byte[] expectedBytes)
        {
            if (!_fileOperations.FileExists(path))
            {
                return false;
            }

            var actualBytes = _fileOperations.ReadAllBytes(path);
            return ByteArraysEqual(actualBytes, expectedBytes);
        }

        private static SaveResult ValidateSlot(SaveSlotId slotId)
        {
            if (string.IsNullOrEmpty(slotId.Value))
            {
                return SaveResult.Fail(SaveStage.Snapshot, SaveErrorCode.InvalidSlot, "Save slot is invalid.");
            }

            return SaveResult.Success();
        }

        private static bool ByteArraysEqual(byte[] left, byte[] right)
        {
            if (left == null || right == null)
            {
                return left == right;
            }

            if (left.Length != right.Length)
            {
                return false;
            }

            for (var index = 0; index < left.Length; index++)
            {
                if (left[index] != right[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static SaveCandidateId MustCandidateId(string value)
        {
            var candidate = SaveCandidateId.TryCreate(value);
            if (!candidate.Succeeded)
            {
                throw new InvalidOperationException(candidate.Error.Message);
            }

            return candidate.Value;
        }
    }
}
