using System.Collections.Generic;
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
            catalog.definitions = new[] { duplicateA, duplicateB, invalidKind };

            var issues = SettingsUnityValidator.ValidateCatalog(catalog);
            Assert.That(issues.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(issues.Exists(i => i.Index == 1 && i.Key == "audio.mute"));
            Assert.That(issues.Exists(i => i.Index == 2 && i.Key == "audio.volume"));
        }

        [Test]
        public void ProfileConversionRejectsDuplicateOverridesAndUnknownKeys()
        {
            var catalog = BuildValidCatalog();
            var registry = SettingsUnityConverter.ConvertCatalog(catalog).Value;
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

            var duplicate = SettingsUnityConverter.ConvertProfile(profile, registry);
            Assert.That(duplicate.Succeeded, Is.False);
            Assert.That(duplicate.Error.Code, Is.EqualTo(SettingsValidationCode.DuplicateProfileOverride));
        }

        [Test]
        public void FactoryRequiresExplicitApplicatorsAndDoesNotMutateAssets()
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
            var applied = created.Value.Apply(tx);
            Assert.That(applied.Outcome, Is.EqualTo(SettingsApplyOutcome.Applied));
            Assert.That(catalog.definitions[0].defaultBoolean, Is.EqualTo(originalMute));
            Assert.That(applicator.ApplyCount, Is.EqualTo(1));
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
    }
}
