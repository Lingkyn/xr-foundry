using System.Collections.Generic;
using Lingkyn.Settings.Core;
using Lingkyn.Settings.Unity;
using UnityEngine;

namespace Lingkyn.Settings.Unity.Samples
{
    public static class SettingsAuthoringExample
    {
        public static SettingsApplyOutcome Run(SettingsCatalogAsset catalog)
        {
            var created = SettingsUnityFactory.CreateCoordinator(new SettingsUnityFactoryConfig
            {
                Catalog = catalog,
                Applicators = new List<ISettingApplicator>
                {
                    new DelegateApplicator("mute", 0, "audio.mute"),
                },
            });

            if (!created.Succeeded)
            {
                return SettingsApplyOutcome.ValidationFailed;
            }

            var tx = created.Value.BeginTransaction();
            tx.StageSet(new ScopedSettingKey(MustKey("audio.mute"), SettingScope.User), SettingValue.FromBoolean(true));
            return created.Value.Apply(tx).Outcome;
        }

        public static SettingsCatalogAsset CreateSampleCatalog()
        {
            var catalog = ScriptableObject.CreateInstance<SettingsCatalogAsset>();
            var mute = ScriptableObject.CreateInstance<SettingDefinitionAsset>();
            mute.key = "audio.mute";
            mute.kind = SettingValueKindRecord.Boolean;
            mute.defaultScope = SettingScopeRecord.User;
            catalog.definitions = new[] { mute };
            return catalog;
        }

        private static SettingKey MustKey(string value) => SettingKey.TryCreate(value).Value;

        private sealed class DelegateApplicator : ISettingApplicator
        {
            private readonly string _key;

            public DelegateApplicator(string id, int order, string key)
            {
                ApplicatorId = id;
                Order = order;
                _key = key;
            }

            public string ApplicatorId { get; }
            public int Order { get; }
            public bool CanApply(SettingKey key) => key.Value == _key;
            public SettingsApplicatorStepResult Apply(IReadOnlyList<SettingChange> changes) => SettingsApplicatorStepResult.Success();
            public SettingsApplicatorStepResult Rollback(IReadOnlyList<SettingChange> changes) => SettingsApplicatorStepResult.Success();
        }
    }
}
