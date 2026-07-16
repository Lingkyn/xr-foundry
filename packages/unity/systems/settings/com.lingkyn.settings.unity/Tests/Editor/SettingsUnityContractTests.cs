using System.Collections.Generic;
using System.Linq;
using Lingkyn.Settings.Core;
using NUnit.Framework;
using UnityEngine;

namespace Lingkyn.Settings.Unity.Editor.Tests
{
    public sealed class SettingsUnityContractTests
    {
        [Test]
        public void CatalogConvertsDeterministicallyToCoreRegistry()
        {
            var catalog = BuildValidCatalog();
            var first = SettingsUnityConverter.ConvertCatalog(catalog);
            var second = SettingsUnityConverter.ConvertCatalog(catalog);
            Assert.That(first.Succeeded, Is.True);
            Assert.That(second.Succeeded, Is.True);
            Assert.That(first.Value.Definitions.Count, Is.EqualTo(second.Value.Definitions.Count));
            for (var i = 0; i < first.Value.Definitions.Count; i++)
            {
                Assert.That(first.Value.Definitions[i].Key.Value, Is.EqualTo(second.Value.Definitions[i].Key.Value));
                Assert.That(first.Value.Definitions[i].DefaultValue, Is.EqualTo(second.Value.Definitions[i].DefaultValue));
            }
        }

        [Test]
        public void ValidationReportsAssetIndexAndKeySpecificErrors()
        {
            var catalog = ScriptableObject.CreateInstance<SettingsCatalogAsset>();
            var duplicateA = BuildDefinition("audio.mute", SettingValueKindRecord.Boolean);
            var duplicateB = BuildDefinition("audio.mute", SettingValueKindRecord.Boolean);
            var invalidKind = BuildDefinition("audio.volume", SettingValueKindRecord.Float);
            invalidKind.numericConstraint.enabled = false;
            var invalidScope = BuildDefinition("audio.scope", SettingValueKindRecord.Boolean);
            invalidScope.defaultScope = (SettingScopeRecord)99;
            catalog.definitions = new[] { duplicateA, duplicateB, invalidKind, invalidScope };

            var issues = SettingsUnityValidator.ValidateCatalog(catalog);
            Assert.That(issues.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(issues.Any(i => i.Index == 1 && i.Key == "audio.mute"));
            Assert.That(issues.Any(i => i.Index == 2 && i.Key == "audio.volume"));
            Assert.That(issues.Any(i => i.Index == 3 && i.Key == "audio.scope"));
        }

        [Test]
        public void ProfileConversionRejectsNullRegistryDuplicateLayersAndConstraintViolations()
        {
            var catalog = BuildValidCatalog();
            var registry = SettingsUnityConverter.ConvertCatalog(catalog).Value;
            Assert.That(SettingsUnityConverter.ConvertProfile(null, registry).Succeeded, Is.False);
            Assert.That(SettingsUnityConverter.ConvertProfile(ScriptableObject.CreateInstance<SettingsProfileAsset>(), null).Succeeded, Is.False);

            var duplicateLayers = ScriptableObject.CreateInstance<SettingsProfileAsset>();
            duplicateLayers.profileId = "demo";
            duplicateLayers.layers = new[]
            {
                new ProfileLayerRecord { layerId = "layer-a" },
                new ProfileLayerRecord { layerId = "layer-a" },
            };
            var duplicateResult = SettingsUnityConverter.ConvertProfile(duplicateLayers, registry);
            Assert.That(duplicateResult.Succeeded, Is.False);
            Assert.That(duplicateResult.Error.Code, Is.EqualTo(SettingsValidationCode.InvalidProfileLayer));

            var profile = ScriptableObject.CreateInstance<SettingsProfileAsset>();
            profile.profileId = "demo";
            var definition = catalog.definitions[0];
            profile.layers = new[]
            {
                new ProfileLayerRecord
                {
                    layerId = "layer-1",
                    overrides = new[]
                    {
                        new ProfileOverrideRecord
                        {
                            definition = definition,
                            kind = SettingValueKindRecord.Boolean,
                            booleanValue = true,
                        },
                        new ProfileOverrideRecord
                        {
                            definition = definition,
                            kind = SettingValueKindRecord.Boolean,
                            booleanValue = false,
                        },
                    },
                },
            };
            var duplicateOverride = SettingsUnityConverter.ConvertProfile(profile, registry);
            Assert.That(duplicateOverride.Succeeded, Is.False);
            Assert.That(duplicateOverride.Error.Code, Is.EqualTo(SettingsValidationCode.DuplicateProfileOverride));

            var violating = ScriptableObject.CreateInstance<SettingsProfileAsset>();
            violating.profileId = "violating";
            var volumeAsset = catalog.definitions[1];
            violating.layers = new[]
            {
                new ProfileLayerRecord
                {
                    layerId = "layer-1",
                    overrides = new[]
                    {
                        new ProfileOverrideRecord
                        {
                            definition = volumeAsset,
                            kind = SettingValueKindRecord.Float,
                            floatValue = 0.3f,
                        },
                    },
                },
            };
            volumeAsset.numericConstraint.step = 0.5;
            volumeAsset.numericConstraint.hasStep = true;
            var violation = SettingsUnityConverter.ConvertProfile(violating, registry);
            Assert.That(violation.Succeeded, Is.False);
            Assert.That(violation.Error.Code, Is.EqualTo(SettingsValidationCode.InvalidStep));
            Assert.That(violation.Error.Message, Does.Contain("layer[0]"));
            Assert.That(violation.Error.Message, Does.Contain("audio.volume"));
        }

        [Test]
        public void FactoryPropagatesRepositoryLoadFailureByDefaultAndSupportsOptInFallback()
        {
            var catalog = BuildValidCatalog();
            var failRepo = new FailLoadRepository();

            var rejected = SettingsUnityFactory.CreateCoordinator(new SettingsUnityFactoryConfig
            {
                Catalog = catalog,
                Repository = failRepo,
            });
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(rejected.Error.Message, Does.Contain("read failed"));

            var fallback = SettingsUnityFactory.CreateCoordinator(new SettingsUnityFactoryConfig
            {
                Catalog = catalog,
                Repository = failRepo,
                UseDefaultsOnRepositoryLoadFailure = true,
            });
            Assert.That(fallback.Succeeded, Is.True);
            Assert.That(fallback.Value.CommittedSnapshot.Revision, Is.EqualTo(0));
        }

        [Test]
        public void FactoryRequiresExplicitApplicatorsRejectsInvalidLoadedSnapshotAndDoesNotMutateAssets()
        {
            var catalog = BuildValidCatalog();
            var originalMute = catalog.definitions[0].defaultBoolean;
            var applicator = new TestApplicator("mute", 0, "audio.mute");
            var created = SettingsUnityFactory.CreateCoordinator(new SettingsUnityFactoryConfig
            {
                Catalog = catalog,
                Applicators = new List<ISettingApplicator> { applicator },
            });
            Assert.That(created.Succeeded, Is.True);

            var tx = created.Value.BeginTransaction();
            tx.StageSet(new ScopedSettingKey(MustKey("audio.mute"), SettingScope.User), SettingValue.FromBoolean(true));
            Assert.That(created.Value.Apply(tx).Outcome, Is.EqualTo(SettingsApplyOutcome.Applied));
            Assert.That(catalog.definitions[0].defaultBoolean, Is.EqualTo(originalMute));
            Assert.That(applicator.ApplyCount, Is.EqualTo(1));

            var invalidRepo = new InvalidLoadedRepository(new SettingsSnapshot(
                2,
                new Dictionary<ScopedSettingKey, SettingValue>
                {
                    { new ScopedSettingKey(MustKey("audio.volume"), SettingScope.User), SettingValue.FromFloat(0.3) },
                },
                new Dictionary<string, SettingValue>()));
            var rejected = SettingsUnityFactory.CreateCoordinator(new SettingsUnityFactoryConfig
            {
                Catalog = catalog,
                Repository = invalidRepo,
            });
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(rejected.Error.Code, Is.EqualTo(SettingsValidationCode.InvalidStep));
        }

        [Test]
        public void AccessibilityMetadataConvertsWithoutComplianceClaims()
        {
            var definition = BuildDefinition("comfort.head_bob", SettingValueKindRecord.Boolean);
            definition.accessibility = new AccessibilityMetadataRecord
            {
                category = "motion",
                featureId = "reduce_head_bob",
                titleKey = "settings.motion.title",
                descriptionKey = "settings.motion.description",
                previewSupported = true,
                availableBeforeGameplay = true,
                documentationKey = "docs.motion",
            };
            var converted = SettingsUnityConverter.ConvertDefinition(definition);
            Assert.That(converted.Succeeded, Is.True);
            Assert.That(converted.Value.Accessibility.Category, Is.EqualTo("motion"));
            Assert.That(converted.Value.Accessibility.FeatureId, Is.EqualTo("reduce_head_bob"));
        }

        private static SettingsCatalogAsset BuildValidCatalog()
        {
            var catalog = ScriptableObject.CreateInstance<SettingsCatalogAsset>();
            catalog.definitions = new[]
            {
                BuildDefinition("audio.mute", SettingValueKindRecord.Boolean),
                BuildDefinition("audio.volume", SettingValueKindRecord.Float),
            };
            catalog.definitions[1].numericConstraint = new NumericConstraintRecord
            {
                enabled = true,
                minInclusive = 0,
                maxInclusive = 1,
                step = 0.25,
                hasStep = true,
            };
            catalog.definitions[1].defaultFloat = 1.0;
            return catalog;
        }

        private static SettingDefinitionAsset BuildDefinition(string key, SettingValueKindRecord kind)
        {
            var asset = ScriptableObject.CreateInstance<SettingDefinitionAsset>();
            asset.key = key;
            asset.kind = kind;
            asset.defaultScope = SettingScopeRecord.User;
            asset.numericConstraint = new NumericConstraintRecord { enabled = true, minInclusive = 0, maxInclusive = 1 };
            asset.stringConstraint = new StringConstraintRecord { enabled = true, maxLength = 32 };
            asset.optionConstraint = new OptionConstraintRecord
            {
                options = new[] { new OptionEntryRecord { optionId = "kbm" } },
            };
            return asset;
        }

        private static SettingKey MustKey(string value)
        {
            var result = SettingKey.TryCreate(value);
            Assert.That(result.Succeeded, Is.True);
            return result.Value;
        }

        private sealed class TestApplicator : ISettingApplicator
        {
            private readonly string _key;

            public TestApplicator(string id, int order, string key)
            {
                ApplicatorId = id;
                Order = order;
                _key = key;
            }

            public string ApplicatorId { get; }
            public int Order { get; }
            public int ApplyCount { get; private set; }

            public bool CanApply(SettingKey key) => key.Value == _key;

            public SettingsApplicatorStepResult Apply(IReadOnlyList<SettingChange> changes)
            {
                ApplyCount++;
                return SettingsApplicatorStepResult.Success();
            }

            public SettingsApplicatorStepResult Rollback(IReadOnlyList<SettingChange> changes)
                => SettingsApplicatorStepResult.Success();
        }

        private sealed class InvalidLoadedRepository : ISettingsSnapshotRepository
        {
            private readonly SettingsSnapshot _snapshot;

            public InvalidLoadedRepository(SettingsSnapshot snapshot) => _snapshot = snapshot;

            public SettingsResult<SettingsSnapshot> Load() => SettingsResult<SettingsSnapshot>.Success(_snapshot);
            public SettingsPersistResult Save(SettingsSnapshot snapshot) => SettingsPersistResult.Success();
        }

        private sealed class FailLoadRepository : ISettingsSnapshotRepository
        {
            public SettingsResult<SettingsSnapshot> Load()
                => SettingsResult<SettingsSnapshot>.Fail(SettingsValidationCode.InvalidKey, "Repository read failed.");

            public SettingsPersistResult Save(SettingsSnapshot snapshot) => SettingsPersistResult.Success();
        }
    }
}
