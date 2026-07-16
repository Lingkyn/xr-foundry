using System;
using System.Collections.Generic;
using Lingkyn.Persistence.Core;

namespace Lingkyn.Persistence.Unity
{
    public static class PersistenceUnityFactory
    {
        public static SaveResult<SaveCoordinator<TState>> CreateCoordinator<TState>(
            PersistenceUnityConfig config,
            IPersistentDataRootProvider rootProvider,
            ISaveCodec<TState> codec,
            IEnumerable<ISaveMigration<TState>> migrations = null,
            IEnumerable<ISaveCommitObserver<TState>> postCommitObservers = null,
            IFileOperationSeam fileOperations = null)
        {
            if (config == null)
            {
                return SaveResult<SaveCoordinator<TState>>.Fail(SaveStage.Snapshot, SaveErrorCode.ProviderFailure, "Persistence config is required.");
            }

            var validation = config.ValidateAuthoring();
            if (!validation.Succeeded)
            {
                return SaveResult<SaveCoordinator<TState>>.Fail(validation.Error.Stage, validation.Error.Code, validation.Error.Message);
            }

            if (rootProvider == null)
            {
                return SaveResult<SaveCoordinator<TState>>.Fail(SaveStage.Snapshot, SaveErrorCode.ProviderFailure, "Persistent data root provider is required.");
            }

            if (codec == null)
            {
                return SaveResult<SaveCoordinator<TState>>.Fail(SaveStage.Snapshot, SaveErrorCode.ProviderFailure, "Codec is required.");
            }

            var integrityProvider = CreateIntegrityProvider(config.IntegrityAlgorithm);
            if (integrityProvider == null)
            {
                return SaveResult<SaveCoordinator<TState>>.Fail(
                    SaveStage.Snapshot,
                    SaveErrorCode.UnsupportedFormat,
                    "Configured integrity provider is unsupported.");
            }

            var migrationPipeline = new MigrationPipeline<TState>(migrations ?? Array.Empty<ISaveMigration<TState>>());
            var store = new LocalFileSaveStore(
                rootProvider,
                config.StorageSubdirectory,
                config.FileExtension,
                config.CommitStrategy,
                fileOperations);

            var coordinator = new SaveCoordinator<TState>(
                config.SchemaId,
                config.CurrentSchemaVersion,
                config.CommitId,
                codec,
                integrityProvider,
                migrationPipeline,
                store,
                config.RequiredCommitCapability,
                postCommitObservers,
                config.RecoveryPolicy);

            return SaveResult<SaveCoordinator<TState>>.Success(coordinator);
        }

        public static IIntegrityProvider CreateIntegrityProvider(string algorithmName)
        {
            if (string.Equals(algorithmName, "sha-256", StringComparison.Ordinal))
            {
                return new Sha256IntegrityProvider();
            }

            return null;
        }
    }
}
