using System;
using System.Threading;
using Lingkyn.Persistence.Core;
using UnityEngine;

namespace Lingkyn.Persistence.Unity
{
    public interface IPersistentDataRootProvider
    {
        SaveResult<string> ResolveRoot();
    }

    public sealed class PersistentDataRootProvider : IPersistentDataRootProvider
    {
        private static int s_mainThreadId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CaptureMainThread()
        {
            s_mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        public SaveResult<string> ResolveRoot()
        {
            if (s_mainThreadId != 0 && Thread.CurrentThread.ManagedThreadId != s_mainThreadId)
            {
                return SaveResult<string>.Fail(
                    SaveStage.Snapshot,
                    SaveErrorCode.ProviderFailure,
                    "Application.persistentDataPath must be resolved on the Unity main thread.");
            }

            var path = Application.persistentDataPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                return SaveResult<string>.Fail(
                    SaveStage.Snapshot,
                    SaveErrorCode.ProviderFailure,
                    "Application.persistentDataPath is empty on this platform.");
            }

            return SaveResult<string>.Success(path);
        }
    }

    public sealed class InjectedPersistentDataRootProvider : IPersistentDataRootProvider
    {
        private readonly string _rootPath;

        public InjectedPersistentDataRootProvider(string rootPath)
        {
            _rootPath = rootPath ?? throw new ArgumentNullException(nameof(rootPath));
        }

        public SaveResult<string> ResolveRoot()
        {
            if (string.IsNullOrWhiteSpace(_rootPath))
            {
                return SaveResult<string>.Fail(SaveStage.Snapshot, SaveErrorCode.ProviderFailure, "Injected persistent data root is empty.");
            }

            return SaveResult<string>.Success(_rootPath);
        }
    }
}
