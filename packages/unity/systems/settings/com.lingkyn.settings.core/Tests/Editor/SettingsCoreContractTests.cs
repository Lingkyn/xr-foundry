using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Lingkyn.Settings.Core.Editor.Tests
{
    public sealed class SettingsCoreContractTests
    {
        [Test]
        public void SettingKeyRejectsInvalidInputs()
        {
            Assert.That(SettingKey.TryCreate(string.Empty).Succeeded, Is.False);
            Assert.That(SettingKey.TryCreate(" ").Succeeded, Is.False);
            Assert.That(SettingKey.TryCreate("audio/master").Succeeded, Is.False);
            Assert.That(SettingKey.TryCreate("valid.key_1").Succeeded, Is.True);
        }

        [Test]
        public void RegistryRejectsDuplicateDefinitions()
        {
            var boolDef = MustDefinition("audio.mute", SettingValueKind.Boolean, SettingValue.FromBoolean(false), SettingScope.User);
            var result = SettingsRegistry.Create(new[] { boolDef, boolDef });
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(SettingsValidationCode.DuplicateDefinition));
        }

        [Test]
        public void DefinitionRejectsKindMismatchAndInvalidDefaults()
        {
            var key = MustKey("graphics.quality");
            var mismatch = SettingDefinitionValidator.ValidateBuilt(
                key,
                SettingValueKind.Integer,
                SettingValue.FromBoolean(true),
                SettingScope.User,
                0,
                false,
                new NumericConstraint(0, 10, 1),
                null,
                null,
                default);
            Assert.That(mismatch.Succeeded, Is.False);
            Assert.That(mismatch.Error.Code, Is.EqualTo(SettingsValidationCode.KindMismatch));

            var nonFinite = SettingDefinitionValidator.ValidateBuilt(
                key,
                SettingValueKind.Float,
                SettingValue.FromFloat(double.NaN),
                SettingScope.User,
                0,
                false,
                new NumericConstraint(0, 1, 0),
                null,
                null,
                default);
            Assert.That(nonFinite.Succeeded, Is.False);
        }

        [Test]
        public void ValueValidationRejectsRangeStepOptionAndStringBounds()
        {
            var intDef = MustDefinition(
                "player.level",
                SettingValueKind.Integer,
                SettingValue.FromInteger(5),
                SettingScope.User,
                numeric: new NumericConstraint(0, 10, 2));
            Assert.That(SettingDefinitionValidator.ValidateValue(intDef, SettingValue.FromInteger(11)).Error.Code, Is.EqualTo(SettingsValidationCode.OutOfRange));
            Assert.That(SettingDefinitionValidator.ValidateValue(intDef, SettingValue.FromInteger(3)).Error.Code, Is.EqualTo(SettingsValidationCode.InvalidStep));

            var floatDef = MustDefinition(
                "audio.volume",
                SettingValueKind.Float,
                SettingValue.FromFloat(0.5),
                SettingScope.User,
                numeric: new NumericConstraint(0, 1, 0.25));
            Assert.That(SettingDefinitionValidator.ValidateValue(floatDef, SettingValue.FromFloat(double.PositiveInfinity)).Error.Code, Is.EqualTo(SettingsValidationCode.NonFiniteFloat));
            Assert.That(SettingDefinitionValidator.ValidateValue(floatDef, SettingValue.FromFloat(0.3)).Error.Code, Is.EqualTo(SettingsValidationCode.InvalidStep));

            var optionDef = MustDefinition(
                "input.scheme",
                SettingValueKind.Option,
                SettingValue.FromOption(MustOption("kbm")),
                SettingScope.User,
                options: new OptionConstraint(new[] { MustOption("kbm"), MustOption("pad") }));
            Assert.That(SettingDefinitionValidator.ValidateValue(optionDef, SettingValue.FromOption(MustOption("unknown"))).Error.Code, Is.EqualTo(SettingsValidationCode.UnknownOption));

            var stringDef = MustDefinition(
                "player.name",
                SettingValueKind.String,
                SettingValue.FromString("hero"),
                SettingScope.User,
                stringConstraint: new StringConstraint(8));
            Assert.That(SettingDefinitionValidator.ValidateValue(stringDef, SettingValue.FromString("way-too-long-name")).Error.Code, Is.EqualTo(SettingsValidationCode.StringTooLong));
        }

        [Test]
        public void ProfileLayerRejectsDuplicateOverrides()
        {
            var key = MustKey("audio.mute");
            Assert.Throws<ArgumentException>(() =>
            {
                _ = new SettingsProfileLayer(
                    "base",
                    new Dictionary<SettingKey, SettingValue>
                    {
                        { key, SettingValue.FromBoolean(true) },
                        { key, SettingValue.FromBoolean(false) },
                    });
            });
        }

        [Test]
        public void ApplyRejectsStaleTransaction()
        {
            var coordinator = CreateCoordinator(out _);
            var stale = coordinator.BeginTransaction();
            coordinator.Apply(stale);
            var result = coordinator.Apply(stale);
            Assert.That(result.Outcome, Is.EqualTo(SettingsApplyOutcome.StaleTransaction));
            Assert.That(result.CommittedRevision, Is.EqualTo(1));
        }

        [Test]
        public void ResetScopeStagesDefaultsWithoutTouchingOtherScopes()
        {
            var registry = MustRegistry(
                MustDefinition("global.master", SettingValueKind.Boolean, SettingValue.FromBoolean(true), SettingScope.Global),
                MustDefinition("user.subtitles", SettingValueKind.Boolean, SettingValue.FromBoolean(false), SettingScope.User));
            var known = new Dictionary<ScopedSettingKey, SettingValue>
            {
                { new ScopedSettingKey(MustKey("global.master"), SettingScope.Global), SettingValue.FromBoolean(false) },
                { new ScopedSettingKey(MustKey("user.subtitles"), SettingScope.User), SettingValue.FromBoolean(true) },
            };
            var coordinator = new SettingsCoordinator(registry, new SettingsSnapshot(0, known, new Dictionary<string, SettingValue>()));
            var tx = coordinator.BeginTransaction();
            tx.StageReset(SettingScope.User);
            var result = coordinator.Apply(tx);
            Assert.That(result.Outcome, Is.EqualTo(SettingsApplyOutcome.Applied));
            coordinator.CommittedSnapshot.TryGetKnownValue(new ScopedSettingKey(MustKey("global.master"), SettingScope.Global), out var globalValue);
            coordinator.CommittedSnapshot.TryGetKnownValue(new ScopedSettingKey(MustKey("user.subtitles"), SettingScope.User), out var userValue);
            Assert.That(globalValue.BooleanValue, Is.False);
            Assert.That(userValue.BooleanValue, Is.False);
        }

        [Test]
        public void ProfileLayeringAppliesOrderedOverrides()
        {
            var coordinator = CreateCoordinator(out var registry);
            var profile = SettingsProfile.Create(
                "accessibility",
                new[]
                {
                    new SettingsProfileLayer(
                        "layer-a",
                        new Dictionary<SettingKey, SettingValue> { { MustKey("audio.mute"), SettingValue.FromBoolean(true) } }),
                    new SettingsProfileLayer(
                        "layer-b",
                        new Dictionary<SettingKey, SettingValue> { { MustKey("audio.volume"), SettingValue.FromFloat(0.25) } }),
                }).Value;
            var tx = coordinator.BeginTransaction();
            tx.StageProfile(profile);
            var result = coordinator.Apply(tx);
            Assert.That(result.Outcome, Is.EqualTo(SettingsApplyOutcome.Applied));
            coordinator.CommittedSnapshot.TryGetKnownValue(new ScopedSettingKey(MustKey("audio.mute"), SettingScope.User), out var mute);
            coordinator.CommittedSnapshot.TryGetKnownValue(new ScopedSettingKey(MustKey("audio.volume"), SettingScope.User), out var volume);
            Assert.That(mute.BooleanValue, Is.True);
            Assert.That(volume.FloatValue, Is.EqualTo(0.25).Within(1e-6));
        }

        [Test]
        public void CrossSettingConstraintValidatesCompleteSnapshot()
        {
            var coordinator = CreateCoordinator(
                out _,
                constraints: new[]
                {
                    new DependentVolumeConstraint(),
                });
            var tx = coordinator.BeginTransaction();
            tx.StageSet(new ScopedSettingKey(MustKey("audio.mute"), SettingScope.User), SettingValue.FromBoolean(true));
            tx.StageSet(new ScopedSettingKey(MustKey("audio.volume"), SettingScope.User), SettingValue.FromFloat(0.5));
            var result = coordinator.Apply(tx);
            Assert.That(result.Outcome, Is.EqualTo(SettingsApplyOutcome.ValidationFailed));
            Assert.That(result.ValidationError.Code, Is.EqualTo(SettingsValidationCode.CrossConstraintViolation));
        }

        [Test]
        public void ChangeSetIsDeterministicallyKeySorted()
        {
            var coordinator = CreateCoordinator(out _);
            var tx = coordinator.BeginTransaction();
            tx.StageSet(new ScopedSettingKey(MustKey("audio.volume"), SettingScope.User), SettingValue.FromFloat(0.75));
            tx.StageSet(new ScopedSettingKey(MustKey("audio.mute"), SettingScope.User), SettingValue.FromBoolean(true));
            var result = coordinator.Apply(tx);
            Assert.That(result.Outcome, Is.EqualTo(SettingsApplyOutcome.Applied));
            Assert.That(result.Changes.Select(c => c.Key.Value).ToArray(), Is.EqualTo(new[] { "audio.mute", "audio.volume" }));
        }

        [Test]
        public void NoOpAndFailedApplyDoNotNotify()
        {
            var failApplicator = new RecordingApplicator("a", 0, MustKey("audio.mute"), failOnApply: true);
            var coordinator = CreateCoordinator(out _, applicators: new ISettingApplicator[] { failApplicator });
            var notifications = 0;
            coordinator.ChangesApplied += _ => notifications++;

            var noOp = coordinator.BeginTransaction();
            Assert.That(coordinator.Apply(noOp).Outcome, Is.EqualTo(SettingsApplyOutcome.NoOp));
            Assert.That(notifications, Is.EqualTo(0));

            var failTx = coordinator.BeginTransaction();
            failTx.StageSet(new ScopedSettingKey(MustKey("audio.mute"), SettingScope.User), SettingValue.FromBoolean(true));
            var failResult = coordinator.Apply(failTx);
            Assert.That(failResult.Outcome, Is.EqualTo(SettingsApplyOutcome.ApplicatorFailed));
            Assert.That(notifications, Is.EqualTo(0));
        }

        [Test]
        public void ApplicatorFailureBeforeFirstApplicatorRollsBackNothing()
        {
            var first = new RecordingApplicator("first", 0, MustKey("audio.mute"), failOnApply: true);
            var second = new RecordingApplicator("second", 1, MustKey("audio.volume"));
            var coordinator = CreateCoordinator(out _, applicators: new ISettingApplicator[] { first, second });
            var tx = coordinator.BeginTransaction();
            tx.StageSet(new ScopedSettingKey(MustKey("audio.mute"), SettingScope.User), SettingValue.FromBoolean(true));
            tx.StageSet(new ScopedSettingKey(MustKey("audio.volume"), SettingScope.User), SettingValue.FromFloat(0.75));
            var result = coordinator.Apply(tx);
            Assert.That(result.Outcome, Is.EqualTo(SettingsApplyOutcome.ApplicatorFailed));
            Assert.That(result.CommittedRevision, Is.EqualTo(0));
            Assert.That(first.ApplyCount, Is.EqualTo(1));
            Assert.That(second.ApplyCount, Is.EqualTo(0));
            Assert.That(first.RollbackCount, Is.EqualTo(0));
            Assert.That(second.RollbackCount, Is.EqualTo(0));
        }

        [Test]
        public void ApplicatorFailureAfterIntermediateApplicatorRollsBackPriorEffects()
        {
            var first = new RecordingApplicator("first", 0, MustKey("audio.mute"));
            var second = new RecordingApplicator("second", 1, MustKey("audio.volume"), failOnApply: true);
            var coordinator = CreateCoordinator(out _, applicators: new ISettingApplicator[] { first, second });
            var tx = coordinator.BeginTransaction();
            tx.StageSet(new ScopedSettingKey(MustKey("audio.mute"), SettingScope.User), SettingValue.FromBoolean(true));
            tx.StageSet(new ScopedSettingKey(MustKey("audio.volume"), SettingScope.User), SettingValue.FromFloat(0.75));
            var result = coordinator.Apply(tx);
            Assert.That(result.Outcome, Is.EqualTo(SettingsApplyOutcome.ApplicatorFailed));
            Assert.That(result.PrimaryFailure.ApplicatorId, Is.EqualTo("second"));
            Assert.That(first.ApplyCount, Is.EqualTo(1));
            Assert.That(second.ApplyCount, Is.EqualTo(1));
            Assert.That(first.RollbackCount, Is.EqualTo(1));
            Assert.That(second.RollbackCount, Is.EqualTo(0));
            Assert.That(coordinator.CommittedSnapshot.Revision, Is.EqualTo(0));
        }

        [Test]
        public void RollbackFailureReturnsRollbackDiagnostics()
        {
            var first = new RecordingApplicator("first", 0, MustKey("audio.mute"));
            var second = new RecordingApplicator("second", 1, MustKey("audio.volume"), failOnApply: true, failOnRollback: true);
            var coordinator = CreateCoordinator(out _, applicators: new ISettingApplicator[] { first, second });
            var tx = coordinator.BeginTransaction();
            tx.StageSet(new ScopedSettingKey(MustKey("audio.mute"), SettingScope.User), SettingValue.FromBoolean(true));
            tx.StageSet(new ScopedSettingKey(MustKey("audio.volume"), SettingScope.User), SettingValue.FromFloat(0.75));
            var result = coordinator.Apply(tx);
            Assert.That(result.Outcome, Is.EqualTo(SettingsApplyOutcome.RollbackFailed));
            Assert.That(result.RollbackDiagnostics.Count, Is.EqualTo(1));
            Assert.That(result.RollbackDiagnostics[0].ApplicatorId, Is.EqualTo("first"));
        }

        [Test]
        public void PersistenceSuccessAbsenceAndAppliedNotPersisted()
        {
            var repo = new RecordingRepository(persistFail: false);
            var withRepo = CreateCoordinator(out _, repository: repo);
            var tx = withRepo.BeginTransaction();
            tx.StageSet(new ScopedSettingKey(MustKey("audio.mute"), SettingScope.User), SettingValue.FromBoolean(true));
            var applied = withRepo.Apply(tx);
            Assert.That(applied.Outcome, Is.EqualTo(SettingsApplyOutcome.Applied));
            Assert.That(repo.SaveCount, Is.EqualTo(1));

            var withoutRepo = CreateCoordinator(out _, repository: null);
            var tx2 = withoutRepo.BeginTransaction();
            tx2.StageSet(new ScopedSettingKey(MustKey("audio.mute"), SettingScope.User), SettingValue.FromBoolean(false));
            var applied2 = withoutRepo.Apply(tx2);
            Assert.That(applied2.Outcome, Is.EqualTo(SettingsApplyOutcome.Applied));

            var failRepo = new RecordingRepository(persistFail: true);
            var failCoordinator = CreateCoordinator(out _, repository: failRepo);
            var tx3 = failCoordinator.BeginTransaction();
            tx3.StageSet(new ScopedSettingKey(MustKey("audio.volume"), SettingScope.User), SettingValue.FromFloat(0.5));
            var notPersisted = failCoordinator.Apply(tx3);
            Assert.That(notPersisted.Outcome, Is.EqualTo(SettingsApplyOutcome.AppliedNotPersisted));
            Assert.That(failCoordinator.CommittedSnapshot.Revision, Is.EqualTo(1));
            Assert.That(failRepo.SaveCount, Is.EqualTo(1));
        }

        [Test]
        public void UnknownStoredKeysArePreservedButNotRegistered()
        {
            var registry = MustRegistry(MustDefinition("audio.mute", SettingValueKind.Boolean, SettingValue.FromBoolean(false), SettingScope.User));
            var unknown = new Dictionary<string, SettingValue> { { "legacy.unknown", SettingValue.FromInteger(42) } };
            var snapshot = new SettingsSnapshot(3, SettingsSnapshot.CreateInitial(registry).KnownValues, unknown);
            var coordinator = new SettingsCoordinator(registry, snapshot);
            Assert.That(coordinator.CommittedSnapshot.TryGetUnknownValue("legacy.unknown", out var value), Is.True);
            Assert.That(value.IntegerValue, Is.EqualTo(42));
            Assert.That(registry.TryGetDefinition(MustKey("legacy.unknown"), out _), Is.False);

            var tx = coordinator.BeginTransaction();
            tx.StageSet(new ScopedSettingKey(MustKey("audio.mute"), SettingScope.User), SettingValue.FromBoolean(true));
            var result = coordinator.Apply(tx);
            Assert.That(result.Outcome, Is.EqualTo(SettingsApplyOutcome.Applied));
            Assert.That(coordinator.CommittedSnapshot.TryGetUnknownValue("legacy.unknown", out _), Is.True);
        }

        [Test]
        public void AccessibilityMetadataRoundTripsWithoutComplianceClaims()
        {
            var metadata = new AccessibilityMetadata(
                "motion",
                "reduce_head_bob",
                "settings.motion.title",
                "settings.motion.description",
                true,
                true,
                "docs.motion.reduce");
            var definition = MustDefinition(
                "comfort.head_bob",
                SettingValueKind.Boolean,
                SettingValue.FromBoolean(false),
                SettingScope.User,
                accessibility: metadata);
            Assert.That(definition.Accessibility.Category, Is.EqualTo("motion"));
            Assert.That(definition.Accessibility.FeatureId, Is.EqualTo("reduce_head_bob"));
            Assert.That(definition.Accessibility.TitleKey, Does.Not.Contain("compliant"));
        }

        private static SettingsCoordinator CreateCoordinator(
            out SettingsRegistry registry,
            IEnumerable<ISettingApplicator> applicators = null,
            IEnumerable<ISettingsConstraint> constraints = null,
            ISettingsSnapshotRepository repository = null)
        {
            registry = MustRegistry(
                MustDefinition("audio.mute", SettingValueKind.Boolean, SettingValue.FromBoolean(false), SettingScope.User, order: 0),
                MustDefinition("audio.volume", SettingValueKind.Float, SettingValue.FromFloat(1.0), SettingScope.User, order: 1, numeric: new NumericConstraint(0, 1, 0.25)));
            var snapshot = SettingsSnapshot.CreateInitial(registry);
            return new SettingsCoordinator(registry, snapshot, applicators, constraints, repository);
        }

        private static SettingKey MustKey(string value)
        {
            var result = SettingKey.TryCreate(value);
            Assert.That(result.Succeeded, Is.True);
            return result.Value;
        }

        private static OptionId MustOption(string value)
        {
            var result = OptionId.TryCreate(value);
            Assert.That(result.Succeeded, Is.True);
            return result.Value;
        }

        private static SettingDefinition MustDefinition(
            string key,
            SettingValueKind kind,
            SettingValue defaultValue,
            SettingScope scope,
            int order = 0,
            NumericConstraint? numeric = null,
            StringConstraint? stringConstraint = null,
            OptionConstraint options = null,
            AccessibilityMetadata accessibility = default)
        {
            var built = SettingDefinitionValidator.ValidateBuilt(
                MustKey(key),
                kind,
                defaultValue,
                scope,
                order,
                false,
                numeric,
                stringConstraint,
                options,
                accessibility);
            Assert.That(built.Succeeded, Is.True, built.Error.Message);
            return built.Value;
        }

        private static SettingsRegistry MustRegistry(params SettingDefinition[] definitions)
        {
            var registry = SettingsRegistry.Create(definitions);
            Assert.That(registry.Succeeded, Is.True);
            return registry.Value;
        }

        private sealed class DependentVolumeConstraint : ISettingsConstraint
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

        private sealed class RecordingApplicator : ISettingApplicator
        {
            private readonly SettingKey _key;
            private readonly bool _failOnApply;
            private readonly bool _failOnRollback;

            public RecordingApplicator(string id, int order, SettingKey key, bool failOnApply = false, bool failOnRollback = false)
            {
                ApplicatorId = id;
                Order = order;
                _key = key;
                _failOnApply = failOnApply;
                _failOnRollback = failOnRollback;
            }

            public string ApplicatorId { get; }
            public int Order { get; }
            public int ApplyCount { get; private set; }
            public int RollbackCount { get; private set; }

            public bool CanApply(SettingKey key) => key.Equals(_key);

            public SettingsApplicatorStepResult Apply(IReadOnlyList<SettingChange> changes)
            {
                ApplyCount++;
                return _failOnApply
                    ? SettingsApplicatorStepResult.Fail(ApplicatorId, "Injected apply failure.")
                    : SettingsApplicatorStepResult.Success();
            }

            public SettingsApplicatorStepResult Rollback(IReadOnlyList<SettingChange> changes)
            {
                RollbackCount++;
                return _failOnRollback
                    ? SettingsApplicatorStepResult.Fail(ApplicatorId, "Injected rollback failure.")
                    : SettingsApplicatorStepResult.Success();
            }
        }

        private sealed class RecordingRepository : ISettingsSnapshotRepository
        {
            private readonly bool _persistFail;

            public RecordingRepository(bool persistFail) => _persistFail = persistFail;
            public int SaveCount { get; private set; }

            public SettingsResult<SettingsSnapshot> Load() => SettingsResult<SettingsSnapshot>.Fail(SettingsValidationCode.InvalidKey, "No load seed.");

            public SettingsPersistResult Save(SettingsSnapshot snapshot)
            {
                SaveCount++;
                return _persistFail ? SettingsPersistResult.Fail("disk full") : SettingsPersistResult.Success();
            }
        }
    }
}
