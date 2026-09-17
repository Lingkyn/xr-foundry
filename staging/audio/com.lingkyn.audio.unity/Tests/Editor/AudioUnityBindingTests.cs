using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Lingkyn.Audio.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Lingkyn.Audio.Unity.Editor.Tests
{
    // AudioMixer and AudioMixerSnapshot cannot be created from script, so every test
    // drives the adapter through IMixerSurface with a fake that mirrors the engine's
    // by-name contract (GetFloat/SetFloat/FindSnapshot report failure through a return
    // value). The real AudioMixerSurface is exercised only in its mixer-absent branch.
    public sealed class AudioUnityBindingTests
    {
        private static readonly BusId Master = BusId.Parse("master");
        private static readonly BusId Music = BusId.Parse("music");
        private static readonly BusId Effects = BusId.Parse("effects");
        private static readonly ParameterId Volume = ParameterId.Parse("volume");
        private static readonly ParameterId Muted = ParameterId.Parse("muted");
        private static readonly ParameterId Mode = ParameterId.Parse("mode");
        private static readonly SnapshotId Calm = SnapshotId.Parse("calm");
        private static readonly SnapshotId Tense = SnapshotId.Parse("tense");

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _created)
            {
                if (asset != null) Object.DestroyImmediate(asset);
            }
            _created.Clear();
        }

        // ----- binding validation -----

        [Test]
        public void BindingAssetConvertsToBindingWithoutMutation()
        {
            var asset = CreateAsset(
                new[] { P("music", "volume", "MusicVolume"), P("effects", "muted", "EffectsMuted") },
                new[] { S("calm", "Calm"), S("tense", "Tense") });
            var before = EditorJsonUtility.ToJson(asset);

            var binding = asset.ToBinding(Graph(), Fake());

            Assert.That(binding.ParameterCount, Is.EqualTo(2));
            Assert.That(binding.SnapshotCount, Is.EqualTo(2));
            Assert.That(binding.TryGetParameter(Music, Volume, out var volume), Is.True);
            Assert.That(volume.ExposedName, Is.EqualTo("MusicVolume"));
            Assert.That(volume.Kind, Is.EqualTo(ParameterKind.Float));
            Assert.That(binding.TryGetParameter(Effects, Muted, out var muted), Is.True);
            Assert.That(muted.Kind, Is.EqualTo(ParameterKind.Bool));
            Assert.That(binding.TryGetParameter(Master, Volume, out _), Is.False);
            Assert.That(binding.TryGetSnapshot(Calm, out var calm), Is.True);
            Assert.That(calm.MixerSnapshotName, Is.EqualTo("Calm"));
            Assert.That(EditorJsonUtility.ToJson(asset), Is.EqualTo(before));
        }

        [Test]
        public void BindingValidationReportsMissingMixerWithFieldPathAndSource()
        {
            var asset = CreateAsset(new[] { P("music", "volume", "MusicVolume") }, new[] { S("calm", "Calm") });
            var absent = Fake();
            absent.IsPresent = false;

            var report = AudioBindingValidation.Validate(asset, Graph(), absent);

            Assert.That(report.IsValid, Is.False);
            var missing = report.Diagnostics.Single(item => item.Code == "mixer.missing");
            Assert.That(missing.FieldPath, Is.EqualTo("mixer"));
            Assert.That(missing.Source, Is.EqualTo(asset));
            Assert.That(report.Diagnostics.Count, Is.EqualTo(1), "Without a mixer no by-name probe runs, so nothing else is reported.");

            var real = AudioBindingValidation.Validate(asset, Graph(), asset.CreateSurface());
            Assert.That(real.Diagnostics.Single().Code, Is.EqualTo("mixer.missing"), "The real surface over a null mixer reports the same code.");
        }

        [Test]
        public void BindingValidationReportsUnexposedParameter()
        {
            var asset = CreateAsset(new[] { P("music", "volume", "Renamed"), P("effects", "muted", "") }, new SnapshotBindingSpec[0]);

            var report = AudioBindingValidation.Validate(asset, Graph(), Fake());

            var codes = report.Diagnostics.Select(item => (item.Code, item.FieldPath)).ToList();
            Assert.That(codes, Is.EquivalentTo(new[]
            {
                ("mixer.parameter.unexposed", "parameters.Array.data[0].exposedName"),
                ("mixer.parameter.unexposed", "parameters.Array.data[1].exposedName"),
            }));
            Assert.That(report.Diagnostics[0].Message, Does.Contain("Renamed").And.Contain("GetFloat"));
            Assert.That(report.Diagnostics.All(item => item.Source == asset), Is.True);
        }

        [Test]
        public void BindingValidationReportsKindMismatchForEnumeratedParameter()
        {
            var asset = CreateAsset(new[] { P("effects", "mode", "EffectsMode"), P("effects", "muted", "EffectsMuted") }, new SnapshotBindingSpec[0]);

            var report = AudioBindingValidation.Validate(asset, Graph(), Fake());

            var mismatch = report.Diagnostics.Single();
            Assert.That(mismatch.Code, Is.EqualTo("mixer.parameter.kind.mismatch"));
            Assert.That(mismatch.FieldPath, Is.EqualTo("parameters.Array.data[0].parameterId"));
            Assert.That(mismatch.Message, Does.Contain("Enumerated"));
            Assert.That(AudioBindingValidation.IsRepresentable(ParameterKind.Float), Is.True);
            Assert.That(AudioBindingValidation.IsRepresentable(ParameterKind.Bool), Is.True);
            Assert.That(AudioBindingValidation.IsRepresentable(ParameterKind.Enumerated), Is.False);
        }

        [Test]
        public void BindingValidationReportsDuplicateBindings()
        {
            var asset = CreateAsset(
                new[] { P("music", "volume", "MusicVolume"), P("MUSIC", "Volume", "MusicVolume2"), P("effects", "muted", "MusicVolume") },
                new[] { S("calm", "Calm"), S("Calm", "Tense") });

            var report = AudioBindingValidation.Validate(asset, Graph(), Fake());

            var duplicates = report.Diagnostics.Where(item => item.Code == "binding.duplicate").Select(item => item.FieldPath).ToList();
            Assert.That(duplicates, Is.EquivalentTo(new[]
            {
                "parameters.Array.data[1].parameterId",
                "parameters.Array.data[2].exposedName",
                "snapshots.Array.data[1].snapshotId",
            }));
            Assert.That(report.Diagnostics.Single(item => item.FieldPath == "parameters.Array.data[1].parameterId").Message, Does.Contain("parameters.Array.data[0]"));
        }

        [Test]
        public void BindingValidationReportsUnknownIdentitiesAndMalformedText()
        {
            var asset = CreateAsset(
                new[] { P("bad bus", "volume", "MusicVolume"), P("voice", "volume", "MusicVolume2"), P("music", "pitch", "MusicVolume3"), P("music", "", "MusicVolume4") },
                new[] { S("storm", "Calm"), S("", "Tense") });

            var report = AudioBindingValidation.Validate(asset, Graph(), Fake());

            var codes = report.Diagnostics.Select(item => (item.Code, item.FieldPath)).ToList();
            Assert.That(codes, Does.Contain(("identity.malformed", "parameters.Array.data[0].busId")));
            Assert.That(codes, Does.Contain(("bus.unknown", "parameters.Array.data[1].busId")));
            Assert.That(codes, Does.Contain(("parameter.unknown", "parameters.Array.data[2].parameterId")));
            Assert.That(codes, Does.Contain(("identity.malformed", "parameters.Array.data[3].parameterId")));
            Assert.That(codes, Does.Contain(("snapshot.unknown", "snapshots.Array.data[0].snapshotId")));
            Assert.That(codes, Does.Contain(("identity.malformed", "snapshots.Array.data[1].snapshotId")));
            Assert.That(report.Diagnostics.All(item => item.Source == asset), Is.True);
        }

        [Test]
        public void BindingConstructionThrowsWithTheSameReport()
        {
            var asset = CreateAsset(new[] { P("music", "volume", "Renamed"), P("effects", "mode", "EffectsMode") }, new[] { S("calm", "Missing") });
            var report = AudioBindingValidation.Validate(asset, Graph(), Fake());
            Assert.That(report.IsValid, Is.False);

            var thrown = Assert.Throws<AudioBindingException>(() => asset.ToBinding(Graph(), Fake()));

            Assert.That(thrown.Report.Diagnostics.Select(item => item.Code), Is.EqualTo(report.Diagnostics.Select(item => item.Code)));
            Assert.That(thrown.Report.Diagnostics.Select(item => item.FieldPath), Is.EqualTo(report.Diagnostics.Select(item => item.FieldPath)));
            Assert.That(thrown.Message, Does.Contain("[mixer.parameter.unexposed]").And.Contain("[mixer.parameter.kind.mismatch]").And.Contain("[mixer.snapshot.missing]"));
            Assert.That(AudioBindingValidation.Validate(null, Graph(), Fake()).Diagnostics.Single().Code, Is.EqualTo("binding.asset.missing"));
        }

        // ----- snapshots -----

        [Test]
        public void SnapshotBindingMapsIdToMixerSnapshotNameAndMissingSnapshotIsReported()
        {
            var valid = CreateAsset(new ParameterBindingSpec[0], new[] { S("calm", "Calm") });
            var binding = valid.ToBinding(Graph(), Fake());
            Assert.That(binding.TryGetSnapshot(Calm, out var calm), Is.True);
            Assert.That(calm.MixerSnapshotName, Is.EqualTo("Calm"));
            Assert.That(binding.TryGetSnapshot(Tense, out _), Is.False);

            var renamed = CreateAsset(new ParameterBindingSpec[0], new[] { S("calm", "Serene"), S("tense", "") });
            var report = AudioBindingValidation.Validate(renamed, Graph(), Fake());
            var codes = report.Diagnostics.Select(item => (item.Code, item.FieldPath)).ToList();
            Assert.That(codes, Is.EquivalentTo(new[]
            {
                ("mixer.snapshot.missing", "snapshots.Array.data[0].snapshot"),
                ("mixer.snapshot.missing", "snapshots.Array.data[1].snapshot"),
            }));
            Assert.That(report.Diagnostics[0].Message, Does.Contain("Serene").And.Contain("FindSnapshot"));
        }

        [Test]
        public void RuntimeTransitionsSnapshotWithExplicitDuration()
        {
            var fake = Fake();
            var runtime = Runtime(fake, new[] { P("music", "volume", "MusicVolume") }, new[] { S("calm", "Calm"), S("tense", "Tense") });

            var outcome = runtime.Apply(new TransitionSnapshotIntent(Calm, 1.5f));

            Assert.That(outcome.Accepted, Is.True, outcome.Message);
            Assert.That(runtime.State.CurrentSnapshot, Is.EqualTo(Calm));
            Assert.That(runtime.Transitions.Count, Is.EqualTo(1));
            Assert.That(runtime.Transitions[0].MixerSnapshotName, Is.EqualTo("Calm"));
            Assert.That(runtime.Transitions[0].DurationSeconds, Is.EqualTo(1.5f));
            Assert.That(fake.Log, Is.EqualTo(new[] { "transition Calm 1.5" }));
            Assert.That(runtime.Diagnostics, Is.Empty);

            Assert.That(runtime.Apply(new TransitionSnapshotIntent(Tense, 0f)).Accepted, Is.True);
            Assert.That(fake.Log.Last(), Is.EqualTo("transition Tense 0"));
            Assert.That(runtime.Apply(new TransitionSnapshotIntent(SnapshotId.Parse("storm"), 1f)).Code, Is.EqualTo(AudioFailure.SnapshotUnknown));
            Assert.That(fake.Log.Count, Is.EqualTo(2), "A rejected intent never reaches the mixer.");
        }

        // ----- LESSON-004: by-name failures are reported, never swallowed -----

        [Test]
        public void RuntimeReportsDiagnosticWhenSetFloatFailsInsteadOfSilentSuccess()
        {
            var fake = Fake();
            var runtime = Runtime(fake, new[] { P("music", "volume", "MusicVolume") }, new SnapshotBindingSpec[0]);
            fake.RejectSets.Add("MusicVolume");

            var outcome = runtime.Apply(new SetParameterIntent(Music, Volume, ParameterValue.Float(0.5f)));

            Assert.That(outcome.Accepted, Is.True, "The Core accepted the value; the mixer failure is a separate diagnostic.");
            Assert.That(runtime.Writes, Is.Empty, "A write the mixer refused is not reported as applied.");
            var diagnostic = runtime.Diagnostics.Single();
            Assert.That(diagnostic.Code, Is.EqualTo("mixer.parameter.unexposed"));
            Assert.That(diagnostic.Intent, Is.SameAs(outcome.Intent));
            Assert.That(diagnostic.Message, Does.Contain("SetFloat").And.Contain("MusicVolume"));
        }

        [Test]
        public void RuntimeReportsDiagnosticWhenSnapshotLookupFailsByName()
        {
            var fake = Fake();
            var runtime = Runtime(fake, new ParameterBindingSpec[0], new[] { S("calm", "Calm") });
            fake.SnapshotNames.Remove("Calm");

            var outcome = runtime.Apply(new TransitionSnapshotIntent(Calm, 2f));

            Assert.That(outcome.Accepted, Is.True);
            Assert.That(runtime.Transitions, Is.Empty);
            var diagnostic = runtime.Diagnostics.Single();
            Assert.That(diagnostic.Code, Is.EqualTo("mixer.snapshot.missing"));
            Assert.That(diagnostic.Message, Does.Contain("FindSnapshot").And.Contain("Calm"));

            Assert.That(runtime.Apply(new TransitionSnapshotIntent(Tense, 1f)).Accepted, Is.True);
            Assert.That(runtime.Diagnostics.Last().Code, Is.EqualTo("mixer.snapshot.missing"));
            Assert.That(runtime.Diagnostics.Last().Message, Does.Contain("no mixer snapshot is bound"));
        }

        [Test]
        public void RuntimeReportsDiagnosticWhenMixerIsAbsentAtRuntime()
        {
            var fake = Fake();
            var runtime = Runtime(fake, new[] { P("music", "volume", "MusicVolume") }, new[] { S("calm", "Calm") });
            fake.IsPresent = false;

            runtime.Apply(new SetParameterIntent(Music, Volume, ParameterValue.Float(0.5f)));
            runtime.Apply(new TransitionSnapshotIntent(Calm, 1f));

            Assert.That(runtime.AcceptedCount, Is.EqualTo(2));
            Assert.That(runtime.Diagnostics.Select(item => item.Code), Is.EqualTo(new[] { "mixer.missing", "mixer.missing" }));
            Assert.That(runtime.Writes, Is.Empty);
            Assert.That(runtime.Transitions, Is.Empty);
            Assert.That(fake.Log, Is.Empty);
        }

        [Test]
        public void RuntimeReportsUnboundParameterInsteadOfSilence()
        {
            var fake = Fake();
            var runtime = Runtime(fake, new[] { P("music", "volume", "MusicVolume") }, new SnapshotBindingSpec[0]);

            var outcome = runtime.Apply(new SetParameterIntent(Master, Volume, ParameterValue.Float(0.5f)));

            Assert.That(outcome.Accepted, Is.True);
            Assert.That(runtime.State.TryGetParameter(Master, Volume, out var value), Is.True);
            Assert.That(value, Is.EqualTo(ParameterValue.Float(0.5f)));
            Assert.That(runtime.Diagnostics.Single().Code, Is.EqualTo("mixer.parameter.unbound"));
            Assert.That(fake.Log, Is.Empty);
        }

        // ----- runtime ordering and reporting -----

        [Test]
        public void RuntimeAppliesAcceptedParametersInOrderAndReportsRejected()
        {
            var fake = Fake();
            var runtime = Runtime(fake, new[] { P("music", "volume", "MusicVolume"), P("effects", "muted", "EffectsMuted") }, new SnapshotBindingSpec[0]);

            var result = runtime.ApplyAll(new AudioIntent[]
            {
                new SetParameterIntent(Music, Volume, ParameterValue.Float(0.75f)),
                new SetParameterIntent(Music, Volume, ParameterValue.Float(9f)),
                new SetParameterIntent(Effects, Muted, ParameterValue.Bool(true)),
                new SetParameterIntent(Effects, Muted, ParameterValue.Float(1f)),
                new SetParameterIntent(BusId.Parse("voice"), Volume, ParameterValue.Float(0.1f)),
                new SetParameterIntent(Music, Volume, ParameterValue.Float(0.25f)),
            });

            Assert.That(result.Outcomes.Select(item => item.Code), Is.EqualTo(new[] { "", AudioFailure.ParameterOutOfRange, "", AudioFailure.ParameterKindMismatch, AudioFailure.BusUnknown, "" }));
            Assert.That(result.AcceptedCount, Is.EqualTo(3));
            Assert.That(result.RejectedCount, Is.EqualTo(3));
            Assert.That(runtime.AcceptedCount, Is.EqualTo(3));
            Assert.That(runtime.RejectedCount, Is.EqualTo(3));
            Assert.That(runtime.Writes.Select(item => item.ExposedName + "=" + item.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture)), Is.EqualTo(new[] { "MusicVolume=0.75", "EffectsMuted=1", "MusicVolume=0.25" }));
            Assert.That(fake.Log, Is.EqualTo(new[] { "set MusicVolume 0.75", "set EffectsMuted 1", "set MusicVolume 0.25" }));
            Assert.That(fake.Values["MusicVolume"], Is.EqualTo(0.25f));
            Assert.That(runtime.Diagnostics, Is.Empty);
            Assert.That(runtime.State.TryGetParameter(Music, Volume, out var volume), Is.True);
            Assert.That(volume, Is.EqualTo(ParameterValue.Float(0.25f)));
            Assert.That(result.State, Is.SameAs(runtime.State));
        }

        [Test]
        public void RuntimeMapsBoolParametersToZeroOrOne()
        {
            var fake = Fake();
            var runtime = Runtime(fake, new[] { P("effects", "muted", "EffectsMuted") }, new SnapshotBindingSpec[0]);

            runtime.Apply(new SetParameterIntent(Effects, Muted, ParameterValue.Bool(true)));
            runtime.Apply(new SetParameterIntent(Effects, Muted, ParameterValue.Bool(false)));

            Assert.That(runtime.Writes.Select(item => item.Value), Is.EqualTo(new[] { 1f, 0f }));
            Assert.That(fake.Values["EffectsMuted"], Is.EqualTo(0f));
            Assert.That(AudioMixerBinding.ToMixerValue(ParameterValue.Float(-6f)), Is.EqualTo(-6f));
            Assert.Throws<System.InvalidOperationException>(() => AudioMixerBinding.ToMixerValue(ParameterValue.Enumerated("indoor")));
        }

        // ----- no shared state -----

        [Test]
        public void TwoRuntimesAreIndependentWithoutSharedState()
        {
            var firstFake = Fake();
            var secondFake = Fake();
            var first = Runtime(firstFake, new[] { P("music", "volume", "MusicVolume") }, new[] { S("calm", "Calm") });
            var second = Runtime(secondFake, new[] { P("music", "volume", "MusicVolume") }, new[] { S("calm", "Calm") });

            first.Apply(new SetParameterIntent(Music, Volume, ParameterValue.Float(0.2f)));
            first.Apply(new TransitionSnapshotIntent(Calm, 1f));

            Assert.That(first.Outcomes.Count, Is.EqualTo(2));
            Assert.That(second.Outcomes, Is.Empty);
            Assert.That(second.Writes, Is.Empty);
            Assert.That(second.State.CurrentSnapshot, Is.Null);
            Assert.That(secondFake.Log, Is.Empty);
            Assert.That(firstFake.Log.Count, Is.EqualTo(2));
            Assert.That(ReferenceEquals(first.Binding, second.Binding), Is.False);
            Assert.That(ReferenceEquals(first.State, second.State), Is.False);

            const BindingFlags statics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var type in new[] { typeof(AudioMixerRuntime), typeof(AudioMixerBinding), typeof(AudioMixerSurface), typeof(AudioMixerBindingAsset) })
            {
                Assert.That(type.GetFields(statics).Where(field => !field.IsLiteral), Is.Empty, type.Name + " must hold no static instance.");
                Assert.That(type.GetProperties(statics), Is.Empty, type.Name + " must expose no static instance.");
            }
        }

        [Test]
        public void RuntimeRejectsStateBuiltOverAnotherGraph()
        {
            var binding = CreateAsset(new[] { P("music", "volume", "MusicVolume") }, new SnapshotBindingSpec[0]).ToBinding(Graph(), Fake());
            var foreignState = AudioState.Initial(Graph());

            Assert.Throws<System.ArgumentException>(() => new AudioMixerRuntime(binding, foreignState));
            Assert.Throws<System.ArgumentNullException>(() => new AudioMixerRuntime(null, foreignState));
            Assert.Throws<System.ArgumentNullException>(() => new AudioMixerRuntime(binding, null));
            Assert.DoesNotThrow(() => new AudioMixerRuntime(binding, AudioState.Initial(binding.Graph)));
        }

        [Test]
        public void RealMixerSurfaceFailsClosedWithoutMixer()
        {
            var surface = new AudioMixerSurface(null);

            Assert.That(surface.IsPresent, Is.False);
            Assert.That(surface.TryGetFloat("MusicVolume", out var value), Is.False);
            Assert.That(value, Is.EqualTo(0f));
            Assert.That(surface.TrySetFloat("MusicVolume", 0.5f), Is.False);
            Assert.That(surface.HasSnapshot("Calm"), Is.False);
            Assert.That(surface.TryTransitionToSnapshot("Calm", 1f), Is.False);
        }

        // ----- helpers -----

        private static MixGraph Graph()
        {
            var result = new MixGraphBuilder()
                .RootBus(Master, ParameterContract.Float(Volume, 0f, 1f, 1f))
                .Bus(Music, Master, ParameterContract.Float(Volume, 0f, 1f, 1f))
                .Bus(Effects, Master,
                    ParameterContract.Bool(Muted, false),
                    ParameterContract.Enumerated(Mode, new[] { "indoor", "outdoor" }, "indoor"))
                .Snapshot(Calm, Music, Effects)
                .Snapshot(Tense, Music)
                .Build();
            Assert.That(result.Succeeded, Is.True, result.Message);
            return result.Value;
        }

        private static FakeMixerSurface Fake() => new FakeMixerSurface(new[] { "MusicVolume", "MusicVolume2", "MusicVolume3", "MusicVolume4", "EffectsMuted", "EffectsMode" }, new[] { "Calm", "Tense" });

        private AudioMixerRuntime Runtime(FakeMixerSurface fake, ParameterBindingSpec[] parameters, SnapshotBindingSpec[] snapshots)
        {
            var binding = CreateAsset(parameters, snapshots).ToBinding(Graph(), fake);
            return new AudioMixerRuntime(binding, AudioState.Initial(binding.Graph));
        }

        private static ParameterBindingSpec P(string bus, string parameter, string exposedName) => new ParameterBindingSpec(bus, parameter, exposedName);

        private static SnapshotBindingSpec S(string snapshot, string name) => new SnapshotBindingSpec(snapshot, name);

        private AudioMixerBindingAsset CreateAsset(ParameterBindingSpec[] parameters, SnapshotBindingSpec[] snapshots)
        {
            var asset = ScriptableObject.CreateInstance<AudioMixerBindingAsset>();
            _created.Add(asset);
            var serialized = new SerializedObject(asset);
            var parameterList = serialized.FindProperty("parameters");
            parameterList.arraySize = parameters.Length;
            for (var index = 0; index < parameters.Length; index++)
            {
                var element = parameterList.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("busId").stringValue = parameters[index].Bus;
                element.FindPropertyRelative("parameterId").stringValue = parameters[index].Parameter;
                element.FindPropertyRelative("exposedName").stringValue = parameters[index].ExposedName;
            }
            var snapshotList = serialized.FindProperty("snapshots");
            snapshotList.arraySize = snapshots.Length;
            for (var index = 0; index < snapshots.Length; index++)
            {
                var element = snapshotList.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("snapshotId").stringValue = snapshots[index].Snapshot;
                element.FindPropertyRelative("snapshotName").stringValue = snapshots[index].Name;
                element.FindPropertyRelative("snapshot").objectReferenceValue = null;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private readonly struct ParameterBindingSpec
        {
            public ParameterBindingSpec(string bus, string parameter, string exposedName)
            {
                Bus = bus;
                Parameter = parameter;
                ExposedName = exposedName;
            }

            public string Bus { get; }
            public string Parameter { get; }
            public string ExposedName { get; }
        }

        private readonly struct SnapshotBindingSpec
        {
            public SnapshotBindingSpec(string snapshot, string name)
            {
                Snapshot = snapshot;
                Name = name;
            }

            public string Snapshot { get; }
            public string Name { get; }
        }

        /// <summary>Mirrors the engine's by-name contract: unknown names fail through the return value.</summary>
        private sealed class FakeMixerSurface : IMixerSurface
        {
            public FakeMixerSurface(IEnumerable<string> exposed, IEnumerable<string> snapshots)
            {
                foreach (var name in exposed) Values[name] = 0f;
                foreach (var name in snapshots) SnapshotNames.Add(name);
            }

            public bool IsPresent { get; set; } = true;
            public Dictionary<string, float> Values { get; } = new Dictionary<string, float>(System.StringComparer.Ordinal);
            public HashSet<string> SnapshotNames { get; } = new HashSet<string>(System.StringComparer.Ordinal);
            public HashSet<string> RejectSets { get; } = new HashSet<string>(System.StringComparer.Ordinal);
            public List<string> Log { get; } = new List<string>();

            public bool TryGetFloat(string exposedName, out float value)
            {
                value = 0f;
                if (!IsPresent || exposedName == null) return false;
                return Values.TryGetValue(exposedName, out value);
            }

            public bool TrySetFloat(string exposedName, float value)
            {
                if (!IsPresent || exposedName == null || !Values.ContainsKey(exposedName) || RejectSets.Contains(exposedName)) return false;
                Values[exposedName] = value;
                Log.Add("set " + exposedName + " " + value.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                return true;
            }

            public bool HasSnapshot(string snapshotName) => IsPresent && snapshotName != null && SnapshotNames.Contains(snapshotName);

            public bool TryTransitionToSnapshot(string snapshotName, float durationSeconds)
            {
                if (!HasSnapshot(snapshotName)) return false;
                Log.Add("transition " + snapshotName + " " + durationSeconds.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                return true;
            }
        }
    }
}
