using System;
using System.IO;
using Lingkyn.Persistence.Core;

namespace Lingkyn.Persistence.Unity
{
    internal enum FileOperationStage
    {
        None = 0,
        StageWrite = 1,
        Flush = 2,
        Backup = 3,
        Replace = 4,
        Cleanup = 5,
        Read = 6,
        Enumerate = 7
    }

    internal interface IFileOperationSeam
    {
        Stream CreateWriteStream(string path);
        void FlushToDisk(Stream stream);
        bool FileExists(string path);
        byte[] ReadAllBytes(string path);
        string[] EnumerateFiles(string directoryPath, string searchPattern, SearchOption searchOption);
        void DeleteFile(string path);
        void MoveFile(string sourcePath, string destinationPath, bool overwrite);
        void CopyFile(string sourcePath, string destinationPath, bool overwrite);
        void ReplaceFile(string sourcePath, string destinationPath, string destinationBackupPath);
        void EnsureDirectory(string directoryPath);
    }

    internal sealed class DefaultFileOperationSeam : IFileOperationSeam
    {
        public Stream CreateWriteStream(string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            return new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        }

        public void FlushToDisk(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            stream.Flush();
            if (stream is FileStream fileStream)
            {
                fileStream.Flush(true);
            }
        }

        public bool FileExists(string path) => File.Exists(path);

        public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);

        public string[] EnumerateFiles(string directoryPath, string searchPattern, SearchOption searchOption)
        {
            if (!Directory.Exists(directoryPath))
            {
                return Array.Empty<string>();
            }

            return Directory.GetFiles(directoryPath, searchPattern, searchOption);
        }

        public void DeleteFile(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        public void MoveFile(string sourcePath, string destinationPath, bool overwrite)
        {
            if (overwrite)
            {
                File.Move(sourcePath, destinationPath, overwrite: true);
                return;
            }

            File.Move(sourcePath, destinationPath);
        }

        public void CopyFile(string sourcePath, string destinationPath, bool overwrite)
        {
            File.Copy(sourcePath, destinationPath, overwrite);
        }

        public void ReplaceFile(string sourcePath, string destinationPath, string destinationBackupPath)
        {
            File.Replace(sourcePath, destinationPath, destinationBackupPath, ignoreMetadataErrors: true);
        }

        public void EnsureDirectory(string directoryPath)
        {
            Directory.CreateDirectory(directoryPath);
        }
    }

    internal sealed class FaultInjectingFileOperationSeam : IFileOperationSeam
    {
        private readonly IFileOperationSeam _inner;
        private FileOperationStage _faultStage = FileOperationStage.None;
        private SaveErrorCode _faultCode = SaveErrorCode.ProviderFailure;
        private string _faultMessage = "Injected file operation failure.";

        public FaultInjectingFileOperationSeam(IFileOperationSeam inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public void ConfigureFault(FileOperationStage stage, SaveErrorCode code, string message)
        {
            _faultStage = stage;
            _faultCode = code;
            _faultMessage = message ?? "Injected file operation failure.";
        }

        public void ClearFault()
        {
            _faultStage = FileOperationStage.None;
        }

        public Stream CreateWriteStream(string path)
        {
            ThrowIfFault(FileOperationStage.StageWrite);
            return _inner.CreateWriteStream(path);
        }

        public void FlushToDisk(Stream stream)
        {
            ThrowIfFault(FileOperationStage.Flush);
            _inner.FlushToDisk(stream);
        }

        public bool FileExists(string path) => _inner.FileExists(path);

        public byte[] ReadAllBytes(string path)
        {
            ThrowIfFault(FileOperationStage.Read);
            return _inner.ReadAllBytes(path);
        }

        public string[] EnumerateFiles(string directoryPath, string searchPattern, SearchOption searchOption)
        {
            ThrowIfFault(FileOperationStage.Enumerate);
            return _inner.EnumerateFiles(directoryPath, searchPattern, searchOption);
        }

        public void DeleteFile(string path)
        {
            ThrowIfFault(FileOperationStage.Cleanup);
            _inner.DeleteFile(path);
        }

        public void MoveFile(string sourcePath, string destinationPath, bool overwrite)
        {
            ThrowIfFault(FileOperationStage.Replace);
            _inner.MoveFile(sourcePath, destinationPath, overwrite);
        }

        public void CopyFile(string sourcePath, string destinationPath, bool overwrite)
        {
            ThrowIfFault(FileOperationStage.Backup);
            _inner.CopyFile(sourcePath, destinationPath, overwrite);
        }

        public void ReplaceFile(string sourcePath, string destinationPath, string destinationBackupPath)
        {
            ThrowIfFault(FileOperationStage.Replace);
            _inner.ReplaceFile(sourcePath, destinationPath, destinationBackupPath);
        }

        public void EnsureDirectory(string directoryPath)
        {
            _inner.EnsureDirectory(directoryPath);
        }

        private void ThrowIfFault(FileOperationStage stage)
        {
            if (_faultStage != stage)
            {
                return;
            }

            switch (_faultCode)
            {
                case SaveErrorCode.IoDenied:
                    throw new UnauthorizedAccessException(_faultMessage);
                case SaveErrorCode.OutOfSpace:
                    throw new IOException(_faultMessage) { HResult = unchecked((int)0x80070070) };
                default:
                    throw new IOException(_faultMessage);
            }
        }
    }

    internal static class FileIoErrorMapper
    {
        public static SaveCommitResult MapCommitFailure(
            Exception exception,
            SaveStage stage,
            bool priorCommittedRecordPreserved)
        {
            return SaveCommitResult.NotCommitted(
                stage,
                MapCode(exception),
                exception.Message,
                priorCommittedRecordPreserved);
        }

        public static SaveResult<T> MapReadFailure<T>(Exception exception)
        {
            return SaveResult<T>.Fail(SaveStage.Read, MapCode(exception), exception.Message);
        }

        private static SaveErrorCode MapCode(Exception exception)
        {
            if (exception is UnauthorizedAccessException)
            {
                return SaveErrorCode.IoDenied;
            }

            if (exception is IOException ioException)
            {
                const int ERROR_DISK_FULL = unchecked((int)0x80070070);
                const int ERROR_HANDLE_DISK_FULL = unchecked((int)0x80070027);
                if (ioException.HResult == ERROR_DISK_FULL || ioException.HResult == ERROR_HANDLE_DISK_FULL)
                {
                    return SaveErrorCode.OutOfSpace;
                }
            }

            return SaveErrorCode.ProviderFailure;
        }
    }
}
