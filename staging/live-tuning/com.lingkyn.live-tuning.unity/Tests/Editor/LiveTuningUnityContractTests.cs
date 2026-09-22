using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Lingkyn.LiveTuning.Core;
using NUnit.Framework;
using UnityEngine;

namespace Lingkyn.LiveTuning.Unity.Editor.Tests
{
    // Authored and unexecuted: this assembly has never compiled or run. It is checked against
    // docs/standards/live-tuning/verification-contract.md and coverage-map.json. Every seam
    // (ISkinApplyTarget, ITuningFileSystem, ITuningFileReader, ITuningPanelSurface) is driven
    // through a fake here; no test in this file claims that a control was visible or reachable,
    // that a colour looked right, or that any input was read from a device.
    public sealed class LiveTuningUnityContractTests
    {
        private static readonly TunableId SurfaceId = TunableId.Parse("skin.surface");
        private static readonly TunableId TextId = TunableId.Parse("skin.text");
        private static readonly TunableId CornerRadiusId = TunableId.Parse("skin.corner_radius");
        private static readonly TunableId DensityId = TunableId.Parse("skin.density");

        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();
        private readonly List<string> _createdAssetPaths = new List<string>();

        [TearDown]
        public void TearDown()
        {
            foreach (var path in _createdAssetPaths)
            {
                UnityEditor.AssetDatabase.DeleteAsset(path);
            }
            _createdAssetPaths.Clear();
            foreach (var asset in _created)
            {
                if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
            }
            _created.Clear();
        }

        // ----- explicit target resolution from binding records (CU-01) -----

        [Test]
        public void BindingValidationResolvesEachRecordToOneAssetAndOneMemberThroughItsBinder()
        {
            var primary = CreateDemoSkin();
            var records = new[]
            {
                new BindingRecord(SurfaceId, TunableKind.Colour, SkinTargetPath.Format("primary", LiveTuningDemoSkinBinder.SurfaceMember)),
                new BindingRecord(CornerRadiusId, TunableKind.Float, SkinTargetPath.Format("primary", LiveTuningDemoSkinBinder.CornerRadiusMember)),
            };
            var bindings = SkinBindingSet.Create(records, SampleRegistry(), AssetsByKey(primary, null), Binders());

            Assert.That(bindings.Count, Is.EqualTo(2));
            Assert.That(bindings.TryGet(SurfaceId, out var surfaceBinding), Is.True);
            Assert.That(surfaceBinding.Asset, Is.SameAs(primary));
            Assert.That(surfaceBinding.MemberName, Is.EqualTo(LiveTuningDemoSkinBinder.SurfaceMember));
        }

        [Test]
        public void BindingValidationReportsMissingAssetReferenceWithBindingTargetMissing()
        {
            var records = new[] { new BindingRecord(SurfaceId, TunableKind.Colour, SkinTargetPath.Format("ghost_asset", LiveTuningDemoSkinBinder.SurfaceMember)) };
            var report = SkinBindingValidation.Validate(records, SampleRegistry(), AssetsByKey(null, null), Binders());
            Assert.That(report.IsValid, Is.False);
            var diagnostic = report.Diagnostics.Single();
            Assert.That(diagnostic.Code, Is.EqualTo(LiveTuningUnityFailure.BindingTargetMissing));
            Assert.That(diagnostic.FieldPath, Does.Contain("ghost_asset"));

            var malformed = new[] { new BindingRecord(SurfaceId, TunableKind.Colour, "not_a_valid_path") };
            var malformedReport = SkinBindingValidation.Validate(malformed, SampleRegistry(), AssetsByKey(null, null), Binders());
            Assert.That(malformedReport.Diagnostics.Single().Code, Is.EqualTo(LiveTuningUnityFailure.BindingTargetMissing));
        }

        [Test]
        public void BindingValidationReportsUnknownMemberWithBindingMemberUnknown()
        {
            var primary = CreateDemoSkin();
            var records = new[] { new BindingRecord(SurfaceId, TunableKind.Colour, SkinTargetPath.Format("primary", "no_such_member")) };
            var report = SkinBindingValidation.Validate(records, SampleRegistry(), AssetsByKey(primary, null), Binders());
            Assert.That(report.IsValid, Is.False);
            var diagnostic = report.Diagnostics.Single();
            Assert.That(diagnostic.Code, Is.EqualTo(LiveTuningUnityFailure.BindingMemberUnknown));
            Assert.That(diagnostic.Source, Is.SameAs(primary));
            Assert.That(diagnostic.FieldPath, Does.Contain("no_such_member"));
        }

        [Test]
        public void BindingValidationReportsMemberKindMismatchWithBindingKindMismatch()
        {
            var primary = CreateDemoSkin();
            // skin.corner_radius is registered as Float, but this record claims the surface member
            // (a Colour member) for it, so the member's declared kind disagrees with the record's.
            var records = new[] { new BindingRecord(CornerRadiusId, TunableKind.Float, SkinTargetPath.Format("primary", LiveTuningDemoSkinBinder.SurfaceMember)) };
            var report = SkinBindingValidation.Validate(records, SampleRegistry(), AssetsByKey(primary, null), Binders());
            Assert.That(report.Diagnostics.Single().Code, Is.EqualTo(LiveTuningFailure.BindingKindMismatch));
        }

        [Test]
        public void SkinBindingSetCreateThrowsWithTheSameReportOnInvalidBindings()
        {
            var records = new[] { new BindingRecord(SurfaceId, TunableKind.Colour, SkinTargetPath.Format("ghost_asset", LiveTuningDemoSkinBinder.SurfaceMember)) };
            var report = SkinBindingValidation.Validate(records, SampleRegistry(), AssetsByKey(null, null), Binders());

            var thrown = Assert.Throws<SkinBindingException>(() => SkinBindingSet.Create(records, SampleRegistry(), AssetsByKey(null, null), Binders()));
            Assert.That(thrown.Report.Diagnostics.Select(item => item.Code), Is.EqualTo(report.Diagnostics.Select(item => item.Code)));
            Assert.That(thrown.Message, Does.Contain("[" + LiveTuningUnityFailure.BindingTargetMissing + "]"));
        }

        [Test]
        public void ScaffoldTypesReferenceNoConcreteBinderOrSkinType()
        {
            var forbidden = new[]
            {
                typeof(LiveTuningDemoSkinAsset), typeof(LiveTuningDemoSkinBinder),
                typeof(LiveTuningSecondaryDemoSkinAsset), typeof(LiveTuningSecondaryDemoSkinBinder),
            };
            var scaffoldTypes = new[]
            {
                typeof(TuningPanelHost), typeof(TuningSlot), typeof(TuningEditorFactory), typeof(UguiFallbackPanelSurface),
                typeof(FloatEditorControl), typeof(IntegerEditorControl), typeof(BoolEditorControl),
                typeof(EnumeratedEditorControl), typeof(ColourEditorControl), typeof(Vector2EditorControl), typeof(Vector3EditorControl),
            };
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            foreach (var scaffoldType in scaffoldTypes)
            {
                foreach (var member in scaffoldType.GetMembers(flags))
                {
                    foreach (var referenced in ReferencedTypesOf(member))
                    {
                        Assert.That(forbidden, Has.None.EqualTo(referenced), $"{scaffoldType.Name}.{member.Name} references {referenced.Name}.");
                    }
                }
            }
        }

        // ----- explicit diagnostic for every by-name or optional resolution (CU-02) -----

        [Test]
        public void ImportRejectsMissingFileWithBindingTargetMissing()
        {
            var reader = new FakeTuningFileReader();
            var result = TuningImport.ImportFromPath(TuningState.Initial(SampleRegistry()), "device://missing.json", reader);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(LiveTuningUnityFailure.BindingTargetMissing));
        }

        [Test]
        public void ImportRequiresExplicitPathAndThrowsWithoutOne()
        {
            var reader = new FakeTuningFileReader();
            var state = TuningState.Initial(SampleRegistry());
            Assert.Throws<ArgumentException>(() => TuningImport.ImportFromPath(state, "", reader));
            Assert.Throws<ArgumentException>(() => TuningImport.ImportFromPath(state, null, reader));
        }

        [Test]
        public void EditorAssetExportSinkReportsUnsavedAssetWithExportWriteFailed()
        {
            var asset = CreateOverrideAsset();
            var sink = new EditorAssetExportSink(asset);
            var document = TuningState.Initial(SampleRegistry()).Export();

            var result = sink.Write(document, "test");

            Assert.That(result.Succeeded, Is.False, "An in-memory asset (never saved to disk) has no asset path.");
            Assert.That(result.Code, Is.EqualTo(LiveTuningUnityFailure.ExportWriteFailed));
        }

        [Test]
        public void DeviceExportSinkReportsUnwritablePathWithExportWriteFailed()
        {
            var fileSystem = new FakeTuningFileSystem { ThrowOnWrite = true };
            var sink = new DeviceTokenExportSink("overrides.json", fileSystem);
            var document = TuningState.Initial(SampleRegistry())
                .Apply(new SetIntent(CornerRadiusId, TunableValue.OfFloat(4f))).Value.Export();

            var result = sink.Write(document, "test");

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(LiveTuningUnityFailure.ExportWriteFailed));
            Assert.That(result.Message, Does.Contain("overrides.json"));
        }

        [Test]
        public void DeviceExportSinkWritesUnderPersistentDataPathAndReturnsPathPlatformAndByteCount()
        {
            var fileSystem = new FakeTuningFileSystem();
            var sink = new DeviceTokenExportSink("overrides.json", fileSystem);
            var document = TuningState.Initial(SampleRegistry())
                .Apply(new SetIntent(CornerRadiusId, TunableValue.OfFloat(4f))).Value.Export();

            var result = sink.Write(document, "test");

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Path, Is.EqualTo(System.IO.Path.Combine(Application.persistentDataPath, "overrides.json")));
            Assert.That(result.Platform, Is.EqualTo(Application.platform.ToString()));
            Assert.That(result.ByteCount, Is.GreaterThan(0));
            Assert.That(fileSystem.LastContents, Is.EqualTo(document.ToJson()));
        }

        // ----- injectable ITuningExportSink (CU-04) -----

        [Test]
        public void ExportOnStateWithNoOverridesWritesEmptyOverrideListRatherThanSkipping()
        {
            var fileSystem = new FakeTuningFileSystem();
            var sink = new DeviceTokenExportSink("overrides.json", fileSystem);
            var result = sink.Write(TuningState.Initial(SampleRegistry()).Export(), "test");
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.IdsWritten, Is.Empty);
            Assert.That(fileSystem.LastContents, Does.Contain("\"overrides\":[]"));
        }

        [Test]
        public void EachSinkWritesOnlyTheIdsTheStateOverrides()
        {
            var state = TuningState.Initial(SampleRegistry())
                .Apply(new SetIntent(CornerRadiusId, TunableValue.OfFloat(4f))).Value
                .Apply(new SetIntent(TextId, TunableValue.OfColour(0.5f, 0.5f, 0.5f, 1f))).Value;
            var document = state.Export();

            var deviceResult = new DeviceTokenExportSink("overrides.json", new FakeTuningFileSystem()).Write(document, "test");
            Assert.That(deviceResult.Succeeded, Is.True, deviceResult.Message);
            Assert.That(deviceResult.IdsWritten, Is.EquivalentTo(new[] { CornerRadiusId, TextId }));

            var overrideAsset = CreateSavedOverrideAsset();
            var editorResult = new EditorAssetExportSink(overrideAsset).Write(document, "test");
            Assert.That(editorResult.Succeeded, Is.True, editorResult.Message);
            Assert.That(editorResult.IdsWritten, Is.EquivalentTo(new[] { CornerRadiusId, TextId }));
            Assert.That(overrideAsset.OverrideJson, Is.EqualTo(document.ToJson()));
        }

        // ----- live application through the existing skin seam (CU-03) -----

        [Test]
        public void RuntimeAppliesAcceptedSetToTheBoundSkinThroughItsBinderAndReinvokesApplySkin()
        {
            var primary = CreateDemoSkin();
            var applyTarget = new FakeSkinApplyTarget();
            var runtime = Runtime(primary, null, applyTarget);

            var outcome = runtime.Apply(new SetIntent(CornerRadiusId, TunableValue.OfFloat(4f)));

            Assert.That(outcome.Accepted, Is.True);
            Assert.That(primary.CornerRadius, Is.EqualTo(4f));
            Assert.That(applyTarget.Applied, Is.EqualTo(new UnityEngine.Object[] { primary }));
            Assert.That(runtime.State.TryGetEffective(CornerRadiusId, out var effective), Is.True);
            Assert.That(effective.AsFloat, Is.EqualTo(primary.CornerRadius), "The value written equals the Core's effective value.");
            Assert.That(UnityEditor.AssetDatabase.GetAssetPath(primary), Is.Empty, "The skin instance was never saved to disk, so applying a value alone never touches disk.");
        }

        [Test]
        public void RuntimeNeverCallsABinderForARejectedIntent()
        {
            var primary = CreateDemoSkin();
            var applyTarget = new FakeSkinApplyTarget();
            var runtime = Runtime(primary, null, applyTarget);
            var before = primary.CornerRadius;

            var outcome = runtime.Apply(new SetIntent(CornerRadiusId, TunableValue.OfFloat(999f)));

            Assert.That(outcome.Accepted, Is.False);
            Assert.That(primary.CornerRadius, Is.EqualTo(before));
            Assert.That(applyTarget.Applied, Is.Empty);
        }

        [Test]
        public void RuntimeConstructionRequiresExplicitReferencesAndAMatchingRegistry()
        {
            var primary = CreateDemoSkin();
            var registry = SampleRegistry();
            var bindings = SkinBindingSet.Create(SampleRecords(), registry, AssetsByKey(primary, null), Binders());
            var matchingState = TuningState.Initial(registry);
            var foreignState = TuningState.Initial(SampleRegistry());

            Assert.Throws<ArgumentNullException>(() => new TuningRuntime(null, matchingState, bindings, new FakeSkinApplyTarget(), NoOpSink()));
            Assert.Throws<ArgumentNullException>(() => new TuningRuntime(registry, matchingState, null, new FakeSkinApplyTarget(), NoOpSink()));
            Assert.Throws<ArgumentNullException>(() => new TuningRuntime(registry, matchingState, bindings, null, NoOpSink()));
            Assert.Throws<ArgumentNullException>(() => new TuningRuntime(registry, matchingState, bindings, new FakeSkinApplyTarget(), null));
            Assert.Throws<ArgumentNullException>(() => new TuningRuntime(registry, null, bindings, new FakeSkinApplyTarget(), NoOpSink()));
            Assert.Throws<ArgumentException>(() => new TuningRuntime(registry, foreignState, bindings, new FakeSkinApplyTarget(), NoOpSink()), "A state built over a different registry instance is rejected.");
            Assert.DoesNotThrow(() => new TuningRuntime(registry, matchingState, bindings, new FakeSkinApplyTarget(), NoOpSink()));

            const BindingFlags statics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var type in new[] { typeof(TuningRuntime), typeof(SkinBindingSet), typeof(TuningPanelHost), typeof(UguiFallbackPanelSurface) })
            {
                Assert.That(type.GetFields(statics).Where(field => !field.IsLiteral), Is.Empty, type.Name + " must hold no static instance.");
                Assert.That(type.GetProperties(statics), Is.Empty, type.Name + " must expose no static instance.");
            }
        }

        // ----- import (CU-05) -----

        [Test]
        public void ImportSkipsUnknownMismatchedAndOutOfRangeOverridesReportingTheCoresCodeAndAppliesTheRest()
        {
            const string schema = "\"schema\":\"" + TokenOverrideDocument.SchemaId + "\"";
            var registry = SampleRegistry();
            var fingerprint = registry.Fingerprint();
            var json = "{" + schema + ",\"registry_fingerprint\":" + "\"" + fingerprint + "\"" + ",\"overrides\":["
                + "{\"id\":\"ghost.value\",\"value\":\"1\"},"
                + "{\"id\":\"" + TextId.Value + "\",\"value\":\"not_a_colour\"},"
                + "{\"id\":\"" + CornerRadiusId.Value + "\",\"value\":\"999\"},"
                + "{\"id\":\"" + SurfaceId.Value + "\",\"value\":\"0.5,0.5,0.5,1\"}"
                + "]}";
            var reader = new FakeTuningFileReader();
            reader.Files["device://overrides.json"] = json;

            var result = TuningImport.ImportFromPath(TuningState.Initial(registry), "device://overrides.json", reader);

            Assert.That(result.Succeeded, Is.True, result.Message);
            var outcomes = result.Value.Outcomes;
            Assert.That(outcomes[0].Accepted, Is.False);
            Assert.That(outcomes[0].Code, Is.EqualTo(LiveTuningFailure.TunableUnknown));
            Assert.That(outcomes[1].Accepted, Is.False);
            Assert.That(outcomes[1].Code, Is.EqualTo(LiveTuningFailure.TunableKindMismatch));
            Assert.That(outcomes[2].Accepted, Is.False);
            Assert.That(outcomes[2].Code, Is.EqualTo(LiveTuningFailure.TunableOutOfRange));
            Assert.That(outcomes[3].Accepted, Is.True);
            Assert.That(result.Value.State.HasOverride(SurfaceId), Is.True);
            Assert.That(result.Value.State.HasOverride(TextId), Is.False);
            Assert.That(result.Value.State.HasOverride(CornerRadiusId), Is.False);
        }

        // ----- one editor per kind (CU-06) -----

        [Test]
        public void TuningEditorFactoryCreatesTheOneEditorTypeForEachTunableKind()
        {
            var expected = new Dictionary<TunableKind, Type>
            {
                [TunableKind.Float] = typeof(FloatEditorControl),
                [TunableKind.Integer] = typeof(IntegerEditorControl),
                [TunableKind.Bool] = typeof(BoolEditorControl),
                [TunableKind.Enumerated] = typeof(EnumeratedEditorControl),
                [TunableKind.Colour] = typeof(ColourEditorControl),
                [TunableKind.Vector2] = typeof(Vector2EditorControl),
                [TunableKind.Vector3] = typeof(Vector3EditorControl),
            };
            foreach (var pair in expected)
            {
                var result = TuningEditorFactory.Create(pair.Key);
                Assert.That(result.Succeeded, Is.True, pair.Key.ToString());
                Assert.That(result.Value.GetType(), Is.EqualTo(pair.Value));
                Assert.That(result.Value.Kind, Is.EqualTo(EditorKindResolver.ResolveEditorKind(pair.Key).Value));
            }
        }

        [Test]
        public void RegisteringManyColourTunablesAttachesTheOneColourEditorTypeNTimes()
        {
            var builder = new TunableRegistryBuilder();
            var ids = new[] { "pkg_a.tint", "pkg_b.tint", "screen_menu.highlight" }.Select(TunableId.Parse).ToArray();
            foreach (var id in ids) builder.Register(id, ColourDeclaration.Instance, TunableValue.OfColour(0f, 0f, 0f, 1f));
            var registry = builder.Build();

            var editors = registry.Tunables.Select(registration => TuningEditorFactory.Create(registration.Kind).Value).ToList();
            Assert.That(editors.Select(editor => editor.GetType()), Has.All.EqualTo(typeof(ColourEditorControl)));
            Assert.That(editors.Count, Is.EqualTo(ids.Length));
        }

        [Test]
        public void UnsupportedEditorKindFailsClosedAtAttachTimeWithDiagnosticNamingTheTunable()
        {
            var result = TuningEditorFactory.Create((TunableKind)999);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(LiveTuningFailure.TunableKindUnsupported));
        }

        // ----- panel host (CU-07) -----

        [Test]
        public void PanelHostCreatesOneSlotPerValidatedBindingAndLabelsGroupsByRegistration()
        {
            var primary = CreateDemoSkin();
            var host = Host(primary, null, out _);
            host.AttachAll();

            Assert.That(host.Slots.Count, Is.EqualTo(3));
            Assert.That(host.Slots.Select(slot => slot.Id.Value), Is.Ordered);
            var surfaceSlot = host.Slots.Single(slot => slot.Id == SurfaceId);
            Assert.That(surfaceSlot.Group, Is.EqualTo("skin"));
            Assert.That(surfaceSlot.Label, Is.EqualTo("surface"));
            Assert.That(surfaceSlot.Editor, Is.InstanceOf<ColourEditorControl>());
        }

        [Test]
        public void PanelHostAttachPathIsIdenticalForBindingsFromTwoDifferentSkinTypes()
        {
            var primary = CreateDemoSkin();
            var secondary = CreateSecondaryDemoSkin();
            var host = Host(primary, secondary, out _);

            host.AttachAll();
            var firstPassSlotCount = host.Slots.Count;
            var firstPassKinds = host.Slots.Select(slot => slot.Editor.Kind).ToList();

            host.AttachAll();
            Assert.That(host.Slots.Count, Is.EqualTo(firstPassSlotCount), "Attaching bindings from two asset types runs the same attach path both times.");
            Assert.That(host.Slots.Select(slot => slot.Editor.Kind), Is.EqualTo(firstPassKinds));
            Assert.That(host.Slots.Any(slot => slot.Id == DensityId), Is.True, "The secondary skin type's binding attached a slot too.");
        }

        [Test]
        public void EditorChangeRaisesExactlyOneSetIntentThroughTheHostAndWritesToNoTargetDirectly()
        {
            var primary = CreateDemoSkin();
            var host = Host(primary, null, out var runtime);
            host.AttachAll();
            var slot = host.Slots.Single(s => s.Id == CornerRadiusId);
            var outcomesBefore = runtime.Outcomes.Count;

            ((TuningEditorControlBase)slot.Editor).RaiseChange(TunableValue.OfFloat(4f));

            Assert.That(runtime.Outcomes.Count, Is.EqualTo(outcomesBefore + 1));
            Assert.That(runtime.Outcomes.Last().Intent, Is.InstanceOf<SetIntent>());
            Assert.That(primary.CornerRadius, Is.EqualTo(4f));
        }

        [Test]
        public void RejectedIntentRestoresTheEditorToTheEffectiveValue()
        {
            var primary = CreateDemoSkin();
            var host = Host(primary, null, out var runtime);
            host.AttachAll();
            var slot = host.Slots.Single(s => s.Id == CornerRadiusId);
            runtime.State.TryGetEffective(CornerRadiusId, out var effectiveBefore);

            ((TuningEditorControlBase)slot.Editor).RaiseChange(TunableValue.OfFloat(999f));

            Assert.That(slot.Editor.CurrentValue, Is.EqualTo(effectiveBefore), "A rejected intent restores the editor to the effective value.");
        }

        [Test]
        public void ResetControlPerSlotRaisesResetAndHostLevelResetRaisesResetAll()
        {
            var primary = CreateDemoSkin();
            var host = Host(primary, null, out var runtime);
            host.AttachAll();
            runtime.Apply(new SetIntent(CornerRadiusId, TunableValue.OfFloat(4f)));
            runtime.Apply(new SetIntent(TextId, TunableValue.OfColour(0.5f, 0.5f, 0.5f, 1f)));

            host.Reset(CornerRadiusId);
            Assert.That(runtime.State.HasOverride(CornerRadiusId), Is.False);
            Assert.That(runtime.State.HasOverride(TextId), Is.True);

            host.ResetAll();
            Assert.That(runtime.State.HasOverride(TextId), Is.False);
        }

        // ----- scope filtering as metadata only (CU-08) -----

        [Test]
        public void ChangedOnlyViewListsExactlyTheSlotsWithAnOverride()
        {
            var primary = CreateDemoSkin();
            var host = Host(primary, null, out var runtime);
            host.AttachAll();
            runtime.Apply(new SetIntent(CornerRadiusId, TunableValue.OfFloat(4f)));

            Assert.That(host.ChangedOnly().Select(slot => slot.Id), Is.EqualTo(new[] { CornerRadiusId }));
        }

        [Test]
        public void ScopeFilterListsExactlyTheRecordsCarryingThatLabelAndNeverChangesTheEditorKind()
        {
            var primary = CreateDemoSkin();
            var scopedRecords = new[]
            {
                new BindingRecord(SurfaceId, TunableKind.Colour, SkinTargetPath.Format("primary", LiveTuningDemoSkinBinder.SurfaceMember), Scope("skin", "ugui")),
                new BindingRecord(TextId, TunableKind.Colour, SkinTargetPath.Format("primary", LiveTuningDemoSkinBinder.TextMember), Scope("skin", "ui_toolkit")),
            };
            var registry = SampleRegistry();
            var bindings = SkinBindingSet.Create(scopedRecords, registry, AssetsByKey(primary, null), Binders());
            var host = new TuningPanelHost(new TuningRuntime(registry, TuningState.Initial(registry), bindings, new FakeSkinApplyTarget(), NoOpSink()), new UguiFallbackPanelSurface());
            host.AttachAll();

            Assert.That(host.WhereScope("skin", "ugui").Select(slot => slot.Id), Is.EqualTo(new[] { SurfaceId }));
            Assert.That(host.WhereScope("skin", "ui_toolkit").Select(slot => slot.Id), Is.EqualTo(new[] { TextId }));
            foreach (var scopeLabel in new[] { "ugui", "ui_toolkit" })
            {
                var slot = host.WhereScope("skin", scopeLabel).Single();
                Assert.That(slot.Editor, Is.InstanceOf<ColourEditorControl>(), scopeLabel);
            }
        }

        // ----- the host's own look; UGUI fallback (CU-09) -----

        [Test]
        public void UguiFallbackSurfaceTracksAddedSlotsWithNoSceneOrSingleton()
        {
            var primary = CreateDemoSkin();
            var host = Host(primary, null, out _);
            host.AttachAll();

            var surface = (UguiFallbackPanelSurface)host.Surface;
            Assert.That(surface.Slots.Count, Is.EqualTo(host.Slots.Count));
            Assert.That(surface.Slots, Is.EqualTo(host.Slots));

            host.AttachAll();
            Assert.That(surface.Slots.Count, Is.EqualTo(host.Slots.Count), "Re-attaching clears the surface's prior slots first.");
        }

        // ----- two independent runtimes (CU-10) -----

        [Test]
        public void TwoRuntimesAreIndependentWithSeparateSkinsAndSinksSharingNoState()
        {
            var firstSkin = CreateDemoSkin();
            var secondSkin = CreateDemoSkin();
            var firstApply = new FakeSkinApplyTarget();
            var secondApply = new FakeSkinApplyTarget();
            var firstRuntime = Runtime(firstSkin, null, firstApply);
            var secondRuntime = Runtime(secondSkin, null, secondApply);
            var firstFileSystem = new FakeTuningFileSystem();
            var secondFileSystem = new FakeTuningFileSystem();

            firstRuntime.Apply(new SetIntent(CornerRadiusId, TunableValue.OfFloat(4f)));
            new DeviceTokenExportSink("a.json", firstFileSystem).Write(firstRuntime.State.Export(), "a");

            Assert.That(firstSkin.CornerRadius, Is.EqualTo(4f));
            Assert.That(secondSkin.CornerRadius, Is.Not.EqualTo(4f));
            Assert.That(secondRuntime.Outcomes, Is.Empty);
            Assert.That(secondApply.Applied, Is.Empty);
            Assert.That(firstFileSystem.LastContents, Is.Not.Null);
            Assert.That(secondFileSystem.LastContents, Is.Null);
            Assert.That(ReferenceEquals(firstRuntime.State, secondRuntime.State), Is.False);
        }

        // ----- helpers -----

        private static IReadOnlyDictionary<string, string> Scope(string key, string value) => new Dictionary<string, string> { [key] = value };

        private static List<Type> ReferencedTypesOf(MemberInfo member)
        {
            var types = new List<Type>();
            if (member is FieldInfo field) types.Add(field.FieldType);
            else if (member is PropertyInfo property) types.Add(property.PropertyType);
            else if (member is MethodInfo method)
            {
                types.Add(method.ReturnType);
                types.AddRange(method.GetParameters().Select(parameter => parameter.ParameterType));
            }
            return types;
        }

        private static TunableRegistry SampleRegistry()
        {
            var builder = new TunableRegistryBuilder();
            builder.Register(SurfaceId, ColourDeclaration.Instance, TunableValue.OfColour(0.03f, 0.09f, 0.12f, 0.96f), "skin", "surface");
            builder.Register(TextId, ColourDeclaration.Instance, TunableValue.OfColour(0.88f, 0.98f, 1f, 1f), "skin", "text");
            builder.Register(CornerRadiusId, new FloatDeclaration(0f, 32f, 0.5f), TunableValue.OfFloat(8f), "skin", "corner_radius");
            builder.Register(DensityId, new EnumeratedDeclaration(new[] { "comfortable", "compact" }), TunableValue.OfEnumerated("comfortable"), "skin", "density");
            return builder.Build();
        }

        private static BindingRecord[] SampleRecords() => new[]
        {
            new BindingRecord(SurfaceId, TunableKind.Colour, SkinTargetPath.Format("primary", LiveTuningDemoSkinBinder.SurfaceMember)),
            new BindingRecord(TextId, TunableKind.Colour, SkinTargetPath.Format("primary", LiveTuningDemoSkinBinder.TextMember)),
            new BindingRecord(CornerRadiusId, TunableKind.Float, SkinTargetPath.Format("primary", LiveTuningDemoSkinBinder.CornerRadiusMember)),
        };

        private static BindingRecord[] SampleRecordsWithSecondary() => SampleRecords()
            .Append(new BindingRecord(DensityId, TunableKind.Enumerated, SkinTargetPath.Format("secondary", LiveTuningSecondaryDemoSkinBinder.DensityMember)))
            .ToArray();

        private static IReadOnlyDictionary<string, UnityEngine.Object> AssetsByKey(LiveTuningDemoSkinAsset primary, LiveTuningSecondaryDemoSkinAsset secondary)
        {
            var map = new Dictionary<string, UnityEngine.Object>();
            if (primary != null) map["primary"] = primary;
            if (secondary != null) map["secondary"] = secondary;
            return map;
        }

        private static IReadOnlyList<ISkinBinder> Binders() => new ISkinBinder[] { new LiveTuningDemoSkinBinder(), new LiveTuningSecondaryDemoSkinBinder() };

        private static ITuningExportSink NoOpSink() => new DeviceTokenExportSink("unused.json", new FakeTuningFileSystem());

        private TuningRuntime Runtime(LiveTuningDemoSkinAsset primary, LiveTuningSecondaryDemoSkinAsset secondary, ISkinApplyTarget applyTarget)
        {
            var registry = SampleRegistry();
            var records = secondary != null ? SampleRecordsWithSecondary() : SampleRecords();
            var bindings = SkinBindingSet.Create(records, registry, AssetsByKey(primary, secondary), Binders());
            return new TuningRuntime(registry, TuningState.Initial(registry), bindings, applyTarget, NoOpSink());
        }

        private TuningPanelHost Host(LiveTuningDemoSkinAsset primary, LiveTuningSecondaryDemoSkinAsset secondary, out TuningRuntime runtime)
        {
            runtime = Runtime(primary, secondary, new FakeSkinApplyTarget());
            return new TuningPanelHost(runtime, new UguiFallbackPanelSurface());
        }

        private LiveTuningDemoSkinAsset CreateDemoSkin()
        {
            var asset = ScriptableObject.CreateInstance<LiveTuningDemoSkinAsset>();
            _created.Add(asset);
            return asset;
        }

        private LiveTuningSecondaryDemoSkinAsset CreateSecondaryDemoSkin()
        {
            var asset = ScriptableObject.CreateInstance<LiveTuningSecondaryDemoSkinAsset>();
            _created.Add(asset);
            return asset;
        }

        private LiveTuningOverrideAsset CreateOverrideAsset()
        {
            var asset = ScriptableObject.CreateInstance<LiveTuningOverrideAsset>();
            _created.Add(asset);
            return asset;
        }

        /// <summary>An override asset actually saved to a temporary project path, so
        /// <c>AssetDatabase.GetAssetPath</c> resolves and the Editor sink's happy path runs.
        /// Removed from the project again in <see cref="TearDown"/>.</summary>
        private LiveTuningOverrideAsset CreateSavedOverrideAsset()
        {
            var asset = ScriptableObject.CreateInstance<LiveTuningOverrideAsset>();
            var path = "Assets/LiveTuningTempOverrides_" + Guid.NewGuid().ToString("N") + ".asset";
            UnityEditor.AssetDatabase.CreateAsset(asset, path);
            _createdAssetPaths.Add(path);
            _created.Add(asset);
            return asset;
        }

        private sealed class FakeSkinApplyTarget : ISkinApplyTarget
        {
            public readonly List<UnityEngine.Object> Applied = new List<UnityEngine.Object>();
            public void ApplySkin(UnityEngine.Object skin) => Applied.Add(skin);
        }

        private sealed class FakeTuningFileSystem : ITuningFileSystem
        {
            public bool ThrowOnWrite;
            public string LastPath;
            public string LastContents;

            public void WriteAllText(string path, string contents)
            {
                if (ThrowOnWrite) throw new System.IO.IOException("simulated write failure");
                LastPath = path;
                LastContents = contents;
            }
        }

        private sealed class FakeTuningFileReader : ITuningFileReader
        {
            public readonly Dictionary<string, string> Files = new Dictionary<string, string>();

            public bool TryReadAllText(string path, out string contents) => Files.TryGetValue(path, out contents);
        }
    }
}
