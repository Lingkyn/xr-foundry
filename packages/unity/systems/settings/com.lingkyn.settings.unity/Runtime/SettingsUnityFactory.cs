using System;
using System.Collections.Generic;
using Lingkyn.Settings.Core;

namespace Lingkyn.Settings.Unity
{
    public sealed class SettingsUnityFactoryConfig
    {
        public SettingsCatalogAsset Catalog { get; set; }
        public IReadOnlyList<ISettingApplicator> Applicators { get; set; }
        public IReadOnlyList<ISettingsConstraint> Constraints { get; set; }
        public ISettingsSnapshotRepository Repository { get; set; }
        public long InitialRevision { get; set; }
        public bool UseDefaultsOnRepositoryLoadFailure { get; set; }
    }

    public static class SettingsUnityFactory
    {
        public static SettingsResult<SettingsCoordinator> CreateCoordinator(SettingsUnityFactoryConfig config)
        {
            if (config == null)
            {
                return SettingsResult<SettingsCoordinator>.Fail(
                    SettingsValidationCode.InvalidKey,
                    "Factory config is required.");
            }

            if (config.Catalog == null)
            {
                return SettingsResult<SettingsCoordinator>.Fail(
                    SettingsValidationCode.InvalidKey,
                    "Catalog asset is required.");
            }

            var registryResult = SettingsUnityConverter.ConvertCatalog(config.Catalog);
            if (!registryResult.Succeeded)
            {
                return SettingsResult<SettingsCoordinator>.Fail(
                    registryResult.Error.Code,
                    registryResult.Error.Message,
                    registryResult.Error.Key);
            }

            SettingsSnapshot initialSnapshot;
            if (config.Repository != null)
            {
                var loaded = config.Repository.Load();
                if (loaded.Succeeded)
                {
                    var validated = SettingsSnapshotValidator.ValidateLoaded(registryResult.Value, loaded.Value);
                    if (!validated.Succeeded)
                    {
                        return SettingsResult<SettingsCoordinator>.Fail(
                            validated.Error.Code,
                            validated.Error.Message,
                            validated.Error.Key);
                    }

                    initialSnapshot = validated.Value;
                }
                else if (config.UseDefaultsOnRepositoryLoadFailure)
                {
                    initialSnapshot = SettingsSnapshot.CreateInitial(registryResult.Value, config.InitialRevision);
                }
                else
                {
                    return SettingsResult<SettingsCoordinator>.Fail(
                        loaded.Error.Code,
                        loaded.Error.Message,
                        loaded.Error.Key);
                }
            }
            else
            {
                initialSnapshot = SettingsSnapshot.CreateInitial(registryResult.Value, config.InitialRevision);
            }

            var coordinator = new SettingsCoordinator(
                registryResult.Value,
                initialSnapshot,
                config.Applicators,
                config.Constraints,
                config.Repository);

            return SettingsResult<SettingsCoordinator>.Success(coordinator);
        }
    }
}
