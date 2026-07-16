using System.Collections.Generic;
using Lingkyn.Settings.Core;

namespace Lingkyn.Settings.Samples
{
    public static class TransactionalSettingsExample
    {
        public static SettingsApplyOutcome Run()
        {
            var registry = SettingsRegistry.Create(new[]
            {
                MustDefinition("audio.mute", SettingValueKind.Boolean, SettingValue.FromBoolean(false), SettingScope.User, 0),
                MustDefinition("audio.volume", SettingValueKind.Float, SettingValue.FromFloat(1.0), SettingScope.User, 1, new NumericConstraint(0, 1, 0.25)),
            }).Value;

            var coordinator = new SettingsCoordinator(
                registry,
                SettingsSnapshot.CreateInitial(registry),
                new ISettingApplicator[]
                {
                    new DelegateApplicator("mute", 0, "audio.mute"),
                    new DelegateApplicator("volume", 1, "audio.volume"),
                },
                constraints: new ISettingsConstraint[] { new MuteVolumeConstraint() },
                repository: new InMemoryRepository(registry));

            var tx = coordinator.BeginTransaction();
            tx.StageSet(new ScopedSettingKey(MustKey("audio.mute"), SettingScope.User), SettingValue.FromBoolean(true));
            tx.StageSet(new ScopedSettingKey(MustKey("audio.volume"), SettingScope.User), SettingValue.FromFloat(0));
            return coordinator.Apply(tx).Outcome;
        }

        private static SettingKey MustKey(string value) => SettingKey.TryCreate(value).Value;

        private static SettingDefinition MustDefinition(
            string key,
            SettingValueKind kind,
            SettingValue defaultValue,
            SettingScope scope,
            int order,
            NumericConstraint? numeric = null)
        {
            return SettingDefinitionValidator.ValidateBuilt(
                MustKey(key),
                kind,
                defaultValue,
                scope,
                order,
                false,
                numeric,
                null,
                null,
                default).Value;
        }

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

        private sealed class MuteVolumeConstraint : ISettingsConstraint
        {
            public string ConstraintId => "mute-zeroes-volume";

            public SettingsResult Validate(SettingsRegistry registry, SettingsSnapshot candidate)
            {
                candidate.TryGetKnownValue(new ScopedSettingKey(MustKey("audio.mute"), SettingScope.User), out var mute);
                candidate.TryGetKnownValue(new ScopedSettingKey(MustKey("audio.volume"), SettingScope.User), out var volume);
                if (mute.BooleanValue && volume.FloatValue > 0)
                {
                    return SettingsResult.Fail(SettingsValidationCode.CrossConstraintViolation, "Muted audio must have zero volume.");
                }

                return SettingsResult.Success();
            }
        }

        private sealed class InMemoryRepository : ISettingsSnapshotRepository
        {
            private SettingsSnapshot _snapshot;

            public InMemoryRepository(SettingsRegistry registry)
            {
                _snapshot = SettingsSnapshot.CreateInitial(registry);
            }

            public SettingsResult<SettingsSnapshot> Load() => SettingsResult<SettingsSnapshot>.Success(_snapshot);
            public SettingsPersistResult Save(SettingsSnapshot snapshot)
            {
                _snapshot = snapshot;
                return SettingsPersistResult.Success();
            }
        }
    }
}
