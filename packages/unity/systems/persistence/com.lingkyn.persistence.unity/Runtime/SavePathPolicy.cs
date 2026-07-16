using System;
using System.IO;
using Lingkyn.Persistence.Core;

namespace Lingkyn.Persistence.Unity
{
    internal static class SavePathPolicy
    {
        internal const string PrimaryCandidateId = "primary";
        internal const string BackupCandidateId = "backup";
        internal const string StagingCandidateIdPrefix = "staging-";

        public static SaveResult ValidateFileExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                return SaveResult.Fail(SaveStage.Snapshot, SaveErrorCode.UnsupportedFormat, "File extension is required.");
            }

            if (!extension.StartsWith(".", StringComparison.Ordinal))
            {
                return SaveResult.Fail(SaveStage.Snapshot, SaveErrorCode.UnsupportedFormat, "File extension must start with '.'.");
            }

            if (extension.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || extension.Contains("/", StringComparison.Ordinal)
                || extension.Contains("\\", StringComparison.Ordinal))
            {
                return SaveResult.Fail(SaveStage.Snapshot, SaveErrorCode.UnsupportedFormat, "File extension contains unsafe path characters.");
            }

            for (var index = 1; index < extension.Length; index++)
            {
                var current = extension[index];
                if (!(char.IsLetterOrDigit(current) || current == '-' || current == '_'))
                {
                    return SaveResult.Fail(SaveStage.Snapshot, SaveErrorCode.UnsupportedFormat, "File extension contains unsupported characters.");
                }
            }

            return SaveResult.Success();
        }

        public static SaveResult ValidateStorageSubdirectory(string subdirectory)
        {
            if (string.IsNullOrWhiteSpace(subdirectory))
            {
                return SaveResult.Success();
            }

            if (Path.IsPathRooted(subdirectory)
                || subdirectory.Contains("..", StringComparison.Ordinal)
                || subdirectory.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                return SaveResult.Fail(SaveStage.Snapshot, SaveErrorCode.UnsupportedFormat, "Storage subdirectory is unsafe.");
            }

            var segments = subdirectory.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            for (var index = 0; index < segments.Length; index++)
            {
                var segment = segments[index];
                if (segment == "." || segment == "..")
                {
                    return SaveResult.Fail(SaveStage.Snapshot, SaveErrorCode.UnsupportedFormat, "Storage subdirectory contains relative traversal.");
                }

                for (var charIndex = 0; charIndex < segment.Length; charIndex++)
                {
                    var current = segment[charIndex];
                    if (!(char.IsLetterOrDigit(current) || current == '-' || current == '_'))
                    {
                        return SaveResult.Fail(SaveStage.Snapshot, SaveErrorCode.UnsupportedFormat, "Storage subdirectory contains unsupported characters.");
                    }
                }
            }

            return SaveResult.Success();
        }

        public static SaveResult<string> ResolveSlotDirectory(string rootPath, string storageSubdirectory)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                return SaveResult<string>.Fail(SaveStage.Snapshot, SaveErrorCode.InvalidSlot, "Persistent data root is required.");
            }

            var rootFullPath = Path.GetFullPath(rootPath);
            var slotDirectory = string.IsNullOrWhiteSpace(storageSubdirectory)
                ? rootFullPath
                : Path.GetFullPath(Path.Combine(rootFullPath, storageSubdirectory));

            if (!IsPathContained(rootFullPath, slotDirectory))
            {
                return SaveResult<string>.Fail(SaveStage.Snapshot, SaveErrorCode.InvalidSlot, "Resolved slot directory escapes persistent data root.");
            }

            return SaveResult<string>.Success(slotDirectory);
        }

        public static SaveResult<SlotPaths> ResolveSlotPaths(
            string rootPath,
            string storageSubdirectory,
            SaveSlotId slotId,
            string fileExtension)
        {
            var directoryResult = ResolveSlotDirectory(rootPath, storageSubdirectory);
            if (!directoryResult.Succeeded)
            {
                return SaveResult<SlotPaths>.Fail(directoryResult.Error.Stage, directoryResult.Error.Code, directoryResult.Error.Message);
            }

            var slotDirectory = directoryResult.Value;
            var primaryFileName = slotId.Value + fileExtension;
            var backupFileName = slotId.Value + ".backup" + fileExtension;

            var primaryPath = Path.GetFullPath(Path.Combine(slotDirectory, primaryFileName));
            var backupPath = Path.GetFullPath(Path.Combine(slotDirectory, backupFileName));

            if (!IsPathContained(slotDirectory, primaryPath) || !IsPathContained(slotDirectory, backupPath))
            {
                return SaveResult<SlotPaths>.Fail(SaveStage.Snapshot, SaveErrorCode.InvalidSlot, "Resolved slot file paths escape slot directory.");
            }

            return SaveResult<SlotPaths>.Success(new SlotPaths(slotDirectory, primaryPath, backupPath, fileExtension));
        }

        public static SaveResult<string> ResolveStagingPath(SlotPaths paths, string stagingToken)
        {
            if (string.IsNullOrWhiteSpace(stagingToken))
            {
                return SaveResult<string>.Fail(SaveStage.StageWrite, SaveErrorCode.ProviderFailure, "Staging token is required.");
            }

            var stagingFileName = paths.SlotIdStem + ".staging." + stagingToken + paths.FileExtension;
            var stagingPath = Path.GetFullPath(Path.Combine(paths.SlotDirectory, stagingFileName));
            if (!IsPathContained(paths.SlotDirectory, stagingPath))
            {
                return SaveResult<string>.Fail(SaveStage.StageWrite, SaveErrorCode.InvalidSlot, "Resolved staging path escapes slot directory.");
            }

            return SaveResult<string>.Success(stagingPath);
        }

        public static bool IsPathContained(string rootFullPath, string candidateFullPath)
        {
            var normalizedRoot = EnsureTrailingSeparator(Path.GetFullPath(rootFullPath));
            var normalizedCandidate = Path.GetFullPath(candidateFullPath);
            return normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static string EnsureTrailingSeparator(string path)
        {
            if (!path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                && !path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                return path + Path.DirectorySeparatorChar;
            }

            return path;
        }
    }

    internal readonly struct SlotPaths
    {
        public SlotPaths(string slotDirectory, string primaryPath, string backupPath, string fileExtension)
        {
            SlotDirectory = slotDirectory;
            PrimaryPath = primaryPath;
            BackupPath = backupPath;
            FileExtension = fileExtension;
            SlotIdStem = Path.GetFileNameWithoutExtension(primaryPath);
        }

        public string SlotDirectory { get; }
        public string PrimaryPath { get; }
        public string BackupPath { get; }
        public string FileExtension { get; }
        public string SlotIdStem { get; }
    }
}
