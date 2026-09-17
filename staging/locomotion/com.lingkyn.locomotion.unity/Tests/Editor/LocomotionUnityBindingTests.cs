using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Lingkyn.Locomotion.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Lingkyn.Locomotion.Unity.Editor.Tests
{
    // No XR Interaction Toolkit locomotion provider, body transformer, or input action
    // can be constructed in EditMode without a rig, so every test drives the adapter
    // through ILocomotionProviderSurface with a fake that mirrors the by-name and
    // optional-lookup contract the real surface makes to the toolkit. The real
    // LocomotionProviderSurface is exercised only in its provider-absent branch, through
    // a binding asset whose provider references are left null (the same limitation
    // documented in the coverage map).
    public sealed class LocomotionUnityBindingTests
    {
        private static readonly AnchorId Spawn = AnchorId.Parse("spawn.point.a");
        private static readonly AnchorId Overlook = AnchorId.Parse("overlook.b");
        private static readonly string[] AllModes = { "teleport", "snap_turn", "smooth_turn", "continuous_move" };

        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _created)
            {
                if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
            }
            _created.Clear();
        }

        // ----- binding validation (AU-01) -----

        [Test]
        public void BindingAssetConvertsToBindingWithoutMutation()
        {
            var asset = CreateAsset(AllModes);
            var before = EditorJsonUtility.ToJson(asset);

            var binding = asset.ToBinding(ComfortPolicy.Default(), Fake());

            Assert.That(binding.BoundModes, Is.EquivalentTo(new[] { ModeId.Teleport, ModeId.SnapTurn, ModeId.SmoothTurn, ModeId.ContinuousMove }));
            Assert.That(EditorJsonUtility.ToJson(asset), Is.EqualTo(before));
        }

        [Test]
        public void BindingValidationReportsMissingProviderWithFieldPathAndSource()
        {
            var asset = CreateAsset(new[] { "teleport" });
            var fake = Fake();
            fake.Present[ModeId.Teleport] = false;

            var report = LocomotionBindingValidation.Validate(asset, ComfortPolicy.Default(), fake);

            Assert.That(report.IsValid, Is.False);
            var missing = report.Diagnostics.Single(item => item.Code == LocomotionBindingValidation.ProviderMissing);
            Assert.That(missing.FieldPath, Is.EqualTo("providers.Array.data[0].provider"));
            Assert.That(missing.Source, Is.EqualTo(asset));
        }

        [Test]
        public void BindingValidationReportsProviderModeMismatch()
        {
            var asset = CreateAsset(new[] { "snap_turn" });
            var fake = Fake();
            fake.Matches[ModeId.SnapTurn] = false;

            var report = LocomotionBindingValidation.Validate(asset, ComfortPolicy.Default(), fake);

            var mismatch = report.Diagnostics.Single();
            Assert.That(mismatch.Code, Is.EqualTo(LocomotionBindingValidation.ProviderModeMismatch));
            Assert.That(mismatch.FieldPath, Is.EqualTo("providers.Array.data[0].provider"));
            Assert.That(mismatch.Source, Is.EqualTo(asset));
        }

        [Test]
        public void BindingValidationReportsDuplicateBindings()
        {
            var asset = CreateAsset(new[] { "teleport", "TELEPORT" });

            var report = LocomotionBindingValidation.Validate(asset, ComfortPolicy.Default(), Fake());

            var duplicate = report.Diagnostics.Single();
            Assert.That(duplicate.Code, Is.EqualTo(LocomotionBindingValidation.BindingDuplicate));
            Assert.That(duplicate.FieldPath, Is.EqualTo("providers.Array.data[1].modeId"));
            Assert.That(duplicate.Message, Does.Contain("providers.Array.data[0]"));
        }

        [Test]
        public void BindingValidationReportsVignetteMissingOnlyWhenPolicyEnablesIt()
        {
            var asset = CreateAsset(new[] { "teleport" });
            var fake = Fake();
            fake.VignettePresent = false;
            var offPolicy = ComfortPolicy.Default();

            Assert.That(LocomotionBindingValidation.Validate(asset, offPolicy, fake).IsValid, Is.True, "No vignette diagnostic is expected while the policy leaves the vignette disabled.");

            var onPolicy = offPolicy.TrySet(ComfortOptions.VignetteEnabledName, ComfortOptionValue.Bool(true)).Value;
            var report = LocomotionBindingValidation.Validate(asset, onPolicy, fake);
            var missing = report.Diagnostics.Single();
            Assert.That(missing.Code, Is.EqualTo(LocomotionBindingValidation.VignetteMissing));
            Assert.That(missing.FieldPath, Is.EqualTo("vignette"));
            Assert.That(missing.Source, Is.EqualTo(asset));
        }

        [Test]
        public void BindingConstructionThrowsWithTheSameReport()
        {
            var asset = CreateAsset(new[] { "teleport" });
            var fake = Fake();
            fake.Present[ModeId.Teleport] = false;
            var report = LocomotionBindingValidation.Validate(asset, ComfortPolicy.Default(), fake);
            Assert.That(report.IsValid, Is.False);

            var thrown = Assert.Throws<LocomotionBindingException>(() => asset.ToBinding(ComfortPolicy.Default(), fake));

            Assert.That(thrown.Report.Diagnostics.Select(item => item.Code), Is.EqualTo(report.Diagnostics.Select(item => item.Code)));
            Assert.That(thrown.Message, Does.Contain("[provider.missing]"));
            Assert.That(LocomotionBindingValidation.Validate(null, ComfortPolicy.Default(), fake).Diagnostics.Single().Code, Is.EqualTo(LocomotionBindingValidation.AssetMissing));
        }

        [Test]
        public void BindingValidationReportsUnknownModeIdentitiesAndMalformedText()
        {
            var asset = CreateAsset(new[] { "bad id", "jump" });

            var report = LocomotionBindingValidation.Validate(asset, ComfortPolicy.Default(), Fake());

            var codes = report.Diagnostics.Select(item => (item.Code, item.FieldPath)).ToList();
            Assert.That(codes, Does.Contain((LocomotionFailure.IdentityMalformed, "providers.Array.data[0].modeId")));
            Assert.That(codes, Does.Contain((LocomotionFailure.IdentityMalformed, "providers.Array.data[1].modeId")), "'jump' is well-formed text but not a declared mode.");
            Assert.That(report.Diagnostics.All(item => item.Source == asset), Is.True);
        }

        // ----- LESSON-004: provider member, input action, and body transformer resolution -----

        [Test]
        public void BindingValidationReportsBodyTransformerMissingForContinuousModesOnly()
        {
            var asset = CreateAsset(AllModes);
            var fake = Fake();
            fake.BodyTransformerResolved[ModeId.SmoothTurn] = false;
            fake.BodyTransformerResolved[ModeId.ContinuousMove] = false;

            var report = LocomotionBindingValidation.Validate(asset, ComfortPolicy.Default(), fake);

            var fieldPaths = report.Diagnostics.Where(item => item.Code == LocomotionBindingValidation.BodyTransformerMissing).Select(item => item.FieldPath).ToList();
            Assert.That(fieldPaths, Is.EquivalentTo(new[] { "providers.Array.data[2].bodyTransformer", "providers.Array.data[3].bodyTransformer" }));
            Assert.That(fieldPaths.Any(path => path.Contains("data[0]") || path.Contains("data[1]")), Is.False, "Teleport and snap turn need no body transformer.");
        }

        [Test]
        public void BindingValidationReportsInputActionUnresolved()
        {
            var asset = CreateAsset(new[] { "teleport" });
            var fake = Fake();
            fake.InputActionResolved[ModeId.Teleport] = false;

            var report = LocomotionBindingValidation.Validate(asset, ComfortPolicy.Default(), fake);

            var diagnostic = report.Diagnostics.Single();
            Assert.That(diagnostic.Code, Is.EqualTo(LocomotionBindingValidation.InputActionUnresolved));
            Assert.That(diagnostic.FieldPath, Is.EqualTo("providers.Array.data[0].activateAction"));
        }

        // ----- application of an accepted comfort policy (AU-03) -----

        [Test]
        public void RuntimeAppliesInitialPolicyToProvidersOnConstruction()
        {
            var fake = Fake();
            Runtime(fake, AllModes);

            Assert.That(fake.Log, Is.EqualTo(new[]
            {
                "turn.enabled snap_turn True",
                "turn.enabled smooth_turn False",
                "turn.increment snap_turn 45",
                "move.speed 1.5",
                "vignette.enabled False",
            }));
        }

        [Test]
        public void RuntimeSwitchesExactlyOneTurnProviderWhenPolicyChanges()
        {
            var fake = Fake();
            var runtime = Runtime(fake, AllModes);
            fake.Log.Clear();

            var outcome = runtime.Apply(new SetComfortOptionIntent(ComfortOptions.TurnModeName, ComfortOptionValue.Enumerated("smooth")));

            Assert.That(outcome.Accepted, Is.True, outcome.Message);
            Assert.That(fake.Log, Does.Contain("turn.enabled smooth_turn True"));
            Assert.That(fake.Log, Does.Contain("turn.enabled snap_turn False"));
            Assert.That(fake.Log, Does.Contain("turn.increment smooth_turn 45"));
            Assert.That(runtime.Diagnostics, Is.Empty);
        }

        [Test]
        public void RuntimeWritesChangedTurnIncrementAndMoveSpeedToProviders()
        {
            var fake = Fake();
            var runtime = Runtime(fake, AllModes);
            fake.Log.Clear();

            runtime.Apply(new SetComfortOptionIntent(ComfortOptions.TurnIncrementDegreesName, ComfortOptionValue.DiscreteDegrees(90f)));
            Assert.That(fake.Log, Does.Contain("turn.increment snap_turn 90"));

            runtime.Apply(new SetComfortOptionIntent(ComfortOptions.MovementSpeedName, ComfortOptionValue.FloatRange(2.5f)));
            Assert.That(fake.Log, Does.Contain("move.speed 2.5"));
        }

        [Test]
        public void RuntimeWritesVignetteOptionsWhenPolicyEnablesVignette()
        {
            var fake = Fake();
            var runtime = Runtime(fake, AllModes);
            fake.Log.Clear();

            runtime.Apply(new SetComfortOptionIntent(ComfortOptions.VignetteEnabledName, ComfortOptionValue.Bool(true)));
            Assert.That(fake.Log, Does.Contain("vignette.enabled True"));
            Assert.That(fake.Log, Does.Contain("vignette.intensity 0"));

            fake.Log.Clear();
            runtime.Apply(new SetComfortOptionIntent(ComfortOptions.VignetteIntensityName, ComfortOptionValue.FloatRange(0.7f)));
            Assert.That(fake.Log, Does.Contain("vignette.intensity 0.7"));
        }

        [Test]
        public void RejectedComfortPolicyIsNeverAppliedToProviders()
        {
            var fake = Fake();
            var runtime = Runtime(fake, AllModes);
            fake.Log.Clear();

            var outcome = runtime.Apply(new SetComfortOptionIntent(ComfortOptions.MovementSpeedName, ComfortOptionValue.FloatRange(99f)));

            Assert.That(outcome.Accepted, Is.False);
            Assert.That(outcome.Code, Is.EqualTo(LocomotionFailure.OptionOutOfRange));
            Assert.That(fake.Log, Is.Empty, "A rejected comfort policy is never applied to the bound providers.");
            Assert.That(runtime.Diagnostics, Is.Empty);
        }

        // ----- LESSON-004: runtime write and forwarding diagnostics -----

        [Test]
        public void RuntimeReportsDiagnosticWhenTurnIncrementWriteFails()
        {
            var fake = Fake();
            fake.RejectIncrement.Add(ModeId.SnapTurn);

            var runtime = Runtime(fake, AllModes);

            var diagnostic = runtime.Diagnostics.Single();
            Assert.That(diagnostic.Code, Is.EqualTo(ProviderDiagnosticCodes.TurnWriteFailed));
        }

        [Test]
        public void RuntimeReportsDiagnosticWhenMoveSpeedWriteFails()
        {
            var fake = Fake();
            fake.RejectMoveSpeed = true;

            var runtime = Runtime(fake, AllModes);

            Assert.That(runtime.Diagnostics.Single().Code, Is.EqualTo(ProviderDiagnosticCodes.MoveWriteFailed));
        }

        [Test]
        public void RuntimeReportsDiagnosticWhenVignetteWriteFails()
        {
            var fake = Fake();
            fake.RejectVignetteEnabled = true;
            var runtime = Runtime(fake, AllModes);
            Assert.That(runtime.Diagnostics.Single().Code, Is.EqualTo(ProviderDiagnosticCodes.VignetteWriteFailed));

            var vignettePolicy = ComfortPolicy.Default().TrySet(ComfortOptions.VignetteEnabledName, ComfortOptionValue.Bool(true)).Value;
            var intensityFake = Fake();
            intensityFake.RejectVignetteIntensity = true;
            var intensityRuntime = Runtime(intensityFake, AllModes, vignettePolicy);
            Assert.That(intensityRuntime.Diagnostics.Single(item => item.Code == ProviderDiagnosticCodes.VignetteWriteFailed).Message, Does.Contain("intensity"));
        }

        [Test]
        public void RuntimeReportsDiagnosticWhenTeleportRequestFails()
        {
            var fake = Fake();
            fake.RejectTeleport = true;
            var runtime = Runtime(fake, AllModes);

            runtime.Apply(new RegisterAnchorIntent(Spawn));
            var outcome = runtime.Apply(new TeleportIntent(Spawn));

            Assert.That(outcome.Accepted, Is.True, "The Core accepted the teleport; the provider failure is a separate diagnostic.");
            Assert.That(runtime.State.CurrentAnchor, Is.EqualTo(Spawn));
            var diagnostic = runtime.Diagnostics.Single(item => item.Code == ProviderDiagnosticCodes.TeleportRequestFailed);
            Assert.That(diagnostic.Intent, Is.SameAs(outcome.Intent));
        }

        [Test]
        public void RuntimeReportsDiagnosticsWhenTurnOrMoveForwardingFails()
        {
            var fake = Fake();
            fake.RejectTurnApply.Add(ModeId.SnapTurn);
            fake.RejectMoveApply = true;
            var runtime = Runtime(fake, AllModes);
            fake.Log.Clear();

            var turnOutcome = runtime.Apply(new TurnIntent(ModeId.SnapTurn, TurnDirection.Right));
            Assert.That(turnOutcome.Accepted, Is.True);
            Assert.That(runtime.Diagnostics.Any(item => item.Code == ProviderDiagnosticCodes.TurnWriteFailed && item.Intent == turnOutcome.Intent), Is.True);

            var moveOutcome = runtime.Apply(new MoveIntent(new PlanarOffset(1f, 0f), 1f));
            Assert.That(moveOutcome.Accepted, Is.True);
            Assert.That(runtime.Diagnostics.Any(item => item.Code == ProviderDiagnosticCodes.MoveWriteFailed && item.Intent == moveOutcome.Intent), Is.True);
        }

        // ----- runtime ordering and reporting (AU-04) -----

        [Test]
        public void RuntimeAppliesAcceptedIntentsInOrderAndReportsRejectedState()
        {
            var fake = Fake();
            var runtime = Runtime(fake, AllModes);
            fake.Log.Clear();

            var result = runtime.ApplyAll(new LocomotionIntent[]
            {
                new RegisterAnchorIntent(Spawn),
                new TeleportIntent(Overlook),
                new TeleportIntent(Spawn),
                new TurnIntent(ModeId.SnapTurn, TurnDirection.Right),
                new MoveIntent(new PlanarOffset(1f, 0f), 1f),
            });

            Assert.That(result.Outcomes.Select(item => item.Code), Is.EqualTo(new[] { "", LocomotionFailure.AnchorUnknown, "", "", "" }));
            Assert.That(runtime.AcceptedCount, Is.EqualTo(4));
            Assert.That(runtime.RejectedCount, Is.EqualTo(1));
            Assert.That(fake.Log, Is.EqualTo(new[] { "teleport spawn.point.a", "turn.apply snap_turn 45", "move.apply 1 0" }), "A rejected intent never reaches the providers.");
        }

        [Test]
        public void RuntimeConstructorRejectsNullBindingOrState()
        {
            var binding = CreateAsset(new[] { "teleport" }).ToBinding(ComfortPolicy.Default(), Fake());

            Assert.Throws<ArgumentNullException>(() => new LocomotionProviderRuntime(null, LocomotionState.Initial(ComfortPolicy.Default())));
            Assert.Throws<ArgumentNullException>(() => new LocomotionProviderRuntime(binding, null));
        }

        // ----- no shared state (AU-05) -----

        [Test]
        public void TwoRuntimesAreIndependentWithoutSharedState()
        {
            var firstFake = Fake();
            var secondFake = Fake();
            var first = Runtime(firstFake, AllModes);
            var second = Runtime(secondFake, AllModes);
            firstFake.Log.Clear();
            secondFake.Log.Clear();

            first.Apply(new RegisterAnchorIntent(Spawn));
            first.Apply(new TeleportIntent(Spawn));

            Assert.That(first.Outcomes.Count, Is.EqualTo(2));
            Assert.That(second.Outcomes, Is.Empty);
            Assert.That(second.State.CurrentAnchor, Is.Null);
            Assert.That(firstFake.Log, Is.Not.Empty);
            Assert.That(secondFake.Log, Is.Empty);
            Assert.That(ReferenceEquals(first.Binding, second.Binding), Is.False);
            Assert.That(ReferenceEquals(first.State, second.State), Is.False);

            const BindingFlags statics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var type in new[] { typeof(LocomotionProviderRuntime), typeof(LocomotionProviderBinding), typeof(LocomotionProviderSurface), typeof(LocomotionProviderBindingAsset) })
            {
                Assert.That(type.GetFields(statics).Where(field => !field.IsLiteral), Is.Empty, type.Name + " must hold no static instance.");
                Assert.That(type.GetProperties(statics), Is.Empty, type.Name + " must expose no static instance.");
            }
        }

        // ----- helpers -----

        private static FakeLocomotionProviderSurface Fake() => new FakeLocomotionProviderSurface(new[] { ModeId.Teleport, ModeId.SnapTurn, ModeId.SmoothTurn, ModeId.ContinuousMove });

        private LocomotionProviderRuntime Runtime(FakeLocomotionProviderSurface fake, string[] modeIds, ComfortPolicy policy = null)
        {
            var usedPolicy = policy ?? ComfortPolicy.Default();
            var binding = CreateAsset(modeIds).ToBinding(usedPolicy, fake);
            return new LocomotionProviderRuntime(binding, LocomotionState.Initial(usedPolicy));
        }

        private LocomotionProviderBindingAsset CreateAsset(string[] modeIds)
        {
            var asset = ScriptableObject.CreateInstance<LocomotionProviderBindingAsset>();
            _created.Add(asset);
            var serialized = new SerializedObject(asset);
            var list = serialized.FindProperty("providers");
            list.arraySize = modeIds.Length;
            for (var index = 0; index < modeIds.Length; index++)
            {
                var element = list.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("modeId").stringValue = modeIds[index];
                element.FindPropertyRelative("provider").objectReferenceValue = null;
                element.FindPropertyRelative("bodyTransformer").objectReferenceValue = null;
                element.FindPropertyRelative("activateAction").objectReferenceValue = null;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        /// <summary>Mirrors the toolkit's by-name and optional-lookup contract: an unresolved target fails through the return value or a resolvable flag, never an exception.</summary>
        private sealed class FakeLocomotionProviderSurface : ILocomotionProviderSurface
        {
            public FakeLocomotionProviderSurface(IEnumerable<ModeId> modes)
            {
                foreach (var mode in modes)
                {
                    Present[mode] = true;
                    Matches[mode] = true;
                    BodyTransformerResolved[mode] = true;
                    InputActionResolved[mode] = true;
                }
            }

            public Dictionary<ModeId, bool> Present { get; } = new Dictionary<ModeId, bool>();
            public Dictionary<ModeId, bool> Matches { get; } = new Dictionary<ModeId, bool>();
            public Dictionary<ModeId, bool> BodyTransformerResolved { get; } = new Dictionary<ModeId, bool>();
            public Dictionary<ModeId, bool> InputActionResolved { get; } = new Dictionary<ModeId, bool>();
            public bool VignettePresent { get; set; } = true;
            public HashSet<ModeId> RejectIncrement { get; } = new HashSet<ModeId>();
            public HashSet<ModeId> RejectTurnApply { get; } = new HashSet<ModeId>();
            public bool RejectMoveSpeed { get; set; }
            public bool RejectVignetteEnabled { get; set; }
            public bool RejectVignetteIntensity { get; set; }
            public bool RejectTeleport { get; set; }
            public bool RejectMoveApply { get; set; }
            public List<string> Log { get; } = new List<string>();

            public bool HasProvider(ModeId mode) => Present.TryGetValue(mode, out var value) && value;
            public bool ProviderMatchesMode(ModeId mode) => Matches.TryGetValue(mode, out var value) && value;
            public bool HasVignette => VignettePresent;
            public bool HasBodyTransformer(ModeId mode) => BodyTransformerResolved.TryGetValue(mode, out var value) && value;
            public bool HasResolvedInputAction(ModeId mode) => !InputActionResolved.TryGetValue(mode, out var value) || value;

            public bool TrySetTurnEnabled(ModeId mode, bool enabled)
            {
                if (!HasProvider(mode)) return false;
                Log.Add($"turn.enabled {mode} {enabled}");
                return true;
            }

            public bool TrySetTurnIncrementDegrees(ModeId mode, float degrees)
            {
                if (!HasProvider(mode) || RejectIncrement.Contains(mode)) return false;
                Log.Add($"turn.increment {mode} {Num(degrees)}");
                return true;
            }

            public bool TrySetMoveSpeed(float metersPerSecond)
            {
                if (!HasProvider(ModeId.ContinuousMove) || RejectMoveSpeed) return false;
                Log.Add($"move.speed {Num(metersPerSecond)}");
                return true;
            }

            public bool TrySetVignetteEnabled(bool enabled)
            {
                if (!VignettePresent || RejectVignetteEnabled) return false;
                Log.Add($"vignette.enabled {enabled}");
                return true;
            }

            public bool TrySetVignetteIntensity(float intensity)
            {
                if (!VignettePresent || RejectVignetteIntensity) return false;
                Log.Add($"vignette.intensity {Num(intensity)}");
                return true;
            }

            public bool TryRequestTeleport(string anchorId)
            {
                if (!HasProvider(ModeId.Teleport) || RejectTeleport) return false;
                Log.Add($"teleport {anchorId}");
                return true;
            }

            public bool TryApplyTurn(ModeId mode, float signedDegrees)
            {
                if (!HasProvider(mode) || RejectTurnApply.Contains(mode)) return false;
                Log.Add($"turn.apply {mode} {Num(signedDegrees)}");
                return true;
            }

            public bool TryApplyMove(float x, float z)
            {
                if (!HasProvider(ModeId.ContinuousMove) || RejectMoveApply) return false;
                Log.Add($"move.apply {Num(x)} {Num(z)}");
                return true;
            }

            private static string Num(float value) => value.ToString("G", CultureInfo.InvariantCulture);
        }
    }
}
