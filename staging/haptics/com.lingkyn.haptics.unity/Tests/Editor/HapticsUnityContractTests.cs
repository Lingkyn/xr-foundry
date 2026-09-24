using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Lingkyn.Haptics.Core;
using Lingkyn.Haptics.Unity;
using UnityEngine;

namespace Lingkyn.Haptics.Unity.Editor.Tests
{
    // Authored and unexecuted: this assembly has never compiled or run. It is checked against
    // docs/standards/haptics/verification-contract.md and coverage-map.json. No test here claims
    // that a controller vibrated, that an amplitude or a pattern was felt, or that any input was
    // read from a device: every assertion is against the recording sink or a fixed probe fake.
    public sealed class HapticsUnityContractTests
    {
        private static readonly HapticEventId GrabContact = HapticEventId.Parse("grab.contact");
        private static readonly HapticEventId EngineHum = HapticEventId.Parse("engine.hum");
        private static readonly HapticEventId ImpactPulse = HapticEventId.Parse("impact.pulse");
        private static readonly HapticProfileId DefaultProfile = HapticProfileId.Parse("default");

        private static HapticEventRegistry BuildRegistry()
        {
            var builder = new HapticEventRegistryBuilder();
            builder.Register(GrabContact, HapticKind.Transient, new AmplitudeRange(0f, 1f), 0.6f, new DurationRangeMs(10f, 200f), 40f, null, null, "grab contact");
            builder.Register(EngineHum, HapticKind.Continuous, new AmplitudeRange(0f, 1f), 0.3f, new DurationRangeMs(50f, 5000f), 500f, new FrequencyRangeHz(20f, 300f), 90f, "engine hum");
            builder.Register(ImpactPulse, HapticKind.Envelope, new AmplitudeRange(0f, 1f), 0.8f, new DurationRangeMs(10f, 500f), 120f, new FrequencyRangeHz(20f, 300f), 150f, "impact pulse");
            return builder.Build();
        }

        private static HapticState BuildState()
        {
            var registry = BuildRegistry();
            var profile = new HapticProfileBuilder(DefaultProfile).Build();
            var profiles = HapticProfileSet.Create(new[] { profile });
            return HapticState.Initial(registry, profiles, DefaultProfile);
        }

        // ----- explicit target resolution to a route handle (HU-01) -----

        private static HapticRouteMap BuildFullRouteMap() =>
            HapticRouteMap.Create(new Dictionary<HapticTarget, HapticRouteHandle>
            {
                [HapticTarget.Left] = HapticRouteHandle.XriChannel("left_channel"),
                [HapticTarget.Right] = HapticRouteHandle.XriChannel("right_channel"),
                [HapticTarget.Both] = HapticRouteHandle.XriChannel("both_channel"),
            });

        [Test]
        public void RouteMapResolvesEachClosedSetTargetToItsExplicitlyMappedRouteHandle()
        {
            var routeMap = BuildFullRouteMap();
            Assert.That(routeMap.TryResolve(HapticTarget.Left, out var left), Is.True);
            Assert.That(left.ChannelName, Is.EqualTo("left_channel"));
            Assert.That(routeMap.TryResolve(HapticTarget.Right, out var right), Is.True);
            Assert.That(right.ChannelName, Is.EqualTo("right_channel"));
            Assert.That(routeMap.TryResolve(HapticTarget.Both, out var both), Is.True);
            Assert.That(both.ChannelName, Is.EqualTo("both_channel"));
        }

        [Test]
        public void RouteMapValidateReportsTargetUnknownWithTheTargetAndRouteNameForAMissingClosedSetEntry()
        {
            var incomplete = new Dictionary<HapticTarget, HapticRouteHandle>
            {
                [HapticTarget.Left] = HapticRouteHandle.XriChannel("left_channel"),
                [HapticTarget.Right] = HapticRouteHandle.XriChannel("right_channel"),
            };
            var report = HapticRouteMap.Validate(incomplete);
            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Diagnostics.Count, Is.EqualTo(1));
            Assert.That(report.Diagnostics[0].Code, Is.EqualTo(HapticFailure.TargetUnknown));
            Assert.That(report.Diagnostics[0].Target, Is.EqualTo(HapticTarget.Both));
            Assert.That(report.Diagnostics[0].RouteName, Is.Not.Empty);
        }

        [Test]
        public void RouteMapCreateThrowsWithTheSameReportForAMissingClosedSetEntry()
        {
            var incomplete = new Dictionary<HapticTarget, HapticRouteHandle>
            {
                [HapticTarget.Left] = HapticRouteHandle.XriChannel("left_channel"),
            };
            var thrown = Assert.Throws<HapticRouteException>(() => HapticRouteMap.Create(incomplete));
            Assert.That(thrown.Report.IsValid, Is.False);
            Assert.That(thrown.Report.Diagnostics.Count, Is.EqualTo(2), "right and both are both missing");
        }

        [Test]
        public void RouteMapTryResolveReturnsFalseForAnUnmappedNamedDeviceTargetWithoutThrowing()
        {
            var routeMap = BuildFullRouteMap();
            Assert.That(routeMap.TryResolve(HapticTarget.NamedDevice("glove_left"), out _), Is.False);
        }

        // ----- injectable IHapticOutputSink and the recording fake (HU-02) -----

        [Test]
        public void RecordingSinkCapturesExactlyTheResolvedTargetAmplitudeDurationAndFrequencyForAnAcceptedPlay()
        {
            var sink = new RecordingHapticSink();
            var runtime = new HapticUnityRuntime(BuildState(), sink, new FixedHapticEnvelopeSupport(true, "test_runtime"));

            var outcome = runtime.Apply(new PlayIntent(EngineHum, HapticTarget.Right));

            Assert.That(outcome.Accepted, Is.True, outcome.Message);
            Assert.That(sink.PlayCalls.Count, Is.EqualTo(1));
            var call = sink.PlayCalls[0];
            Assert.That(call.Target, Is.EqualTo(HapticTarget.Right));
            Assert.That(call.Kind, Is.EqualTo(HapticKind.Continuous));
            Assert.That(call.Amplitude, Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(call.DurationMs, Is.EqualTo(500f));
            Assert.That(call.FrequencyHz, Is.EqualTo(90f));
        }

        [Test]
        public void RecordingSinkCapturesTheAbsenceOfFrequencyForAKindThatDoesNotUseIt()
        {
            var sink = new RecordingHapticSink();
            var runtime = new HapticUnityRuntime(BuildState(), sink, new FixedHapticEnvelopeSupport(true, "test_runtime"));

            runtime.Apply(new PlayIntent(GrabContact, HapticTarget.Left));

            Assert.That(sink.PlayCalls[0].FrequencyHz, Is.Null);
        }

        [Test]
        public void ARejectedIntentReachesNoSinkCall()
        {
            var sink = new RecordingHapticSink();
            var runtime = new HapticUnityRuntime(BuildState(), sink, new FixedHapticEnvelopeSupport(true, "test_runtime"));

            var outcome = runtime.Apply(new PlayIntent(HapticEventId.Parse("no.such.event"), HapticTarget.Left));

            Assert.That(outcome.Accepted, Is.False);
            Assert.That(sink.PlayCalls, Is.Empty);
        }

        [Test]
        public void StopAndStopAllForwardToTheSinkForAnAcceptedIntent()
        {
            var sink = new RecordingHapticSink();
            var runtime = new HapticUnityRuntime(BuildState(), sink, new FixedHapticEnvelopeSupport(true, "test_runtime"));

            runtime.Apply(new PlayIntent(GrabContact, HapticTarget.Left));
            runtime.Apply(new StopIntent(HapticTarget.Left));
            runtime.Apply(new StopAllIntent());

            Assert.That(sink.StopCalls, Is.EqualTo(new[] { HapticTarget.Left }));
            Assert.That(sink.StopAllCallCount, Is.EqualTo(1));
        }

        // ----- HapticProfileAsset constructed with explicit references (HU-03) -----

        [Test]
        public void ProfileAssetConvertsDeterministicallyToTheCoresImmutableProfileWithoutMutatingTheAsset()
        {
            var asset = ScriptableObject.CreateInstance<HapticProfileAsset>();
            asset.ProfileId = "default";
            asset.LeftScale = 1.5f;
            asset.NamedDevices = new List<HapticNamedDeviceEntry> { new HapticNamedDeviceEntry { DeviceId = "glove_left", Scale = 0.5f, Enabled = true } };

            var profile = HapticProfileAssetConverter.Convert(asset);

            Assert.That(profile.Id, Is.EqualTo(HapticProfileId.Parse("default")));
            Assert.That(profile.Resolve(HapticTarget.Left), Is.EqualTo((1.5f, true)));
            Assert.That(profile.Resolve(HapticTarget.NamedDevice("glove_left")), Is.EqualTo((0.5f, true)));
            Assert.That(asset.LeftScale, Is.EqualTo(1.5f), "conversion must not mutate the authored asset");
        }

        [Test]
        public void ProfileAssetValidationReportsAnInvalidScaleWithProfileDeclarationInvalidAndTheFieldPath()
        {
            var asset = ScriptableObject.CreateInstance<HapticProfileAsset>();
            asset.ProfileId = "default";
            asset.LeftScale = 10f;

            var report = HapticProfileAssetConverter.Validate(asset);

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Diagnostics[0].Code, Is.EqualTo(HapticFailure.ProfileDeclarationInvalid));
            Assert.That(report.Diagnostics[0].FieldPath, Is.EqualTo("left"));
            Assert.That(report.Diagnostics[0].Source, Is.SameAs(asset));
        }

        [Test]
        public void ProfileAssetValidationReportsADuplicateNamedDeviceIdWithProfileDeclarationInvalid()
        {
            var asset = ScriptableObject.CreateInstance<HapticProfileAsset>();
            asset.ProfileId = "default";
            asset.NamedDevices = new List<HapticNamedDeviceEntry>
            {
                new HapticNamedDeviceEntry { DeviceId = "glove_left", Scale = 1f, Enabled = true },
                new HapticNamedDeviceEntry { DeviceId = "glove_left", Scale = 2f, Enabled = true },
            };

            var report = HapticProfileAssetConverter.Validate(asset);

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Diagnostics[0].Code, Is.EqualTo(HapticFailure.ProfileDeclarationInvalid));
            Assert.That(report.Diagnostics[0].FieldPath, Is.EqualTo("namedDevices[1]"));
        }

        [Test]
        public void ProfileAssetConversionThrowsWithTheSameReportOnAnInvalidAsset()
        {
            var asset = ScriptableObject.CreateInstance<HapticProfileAsset>();
            asset.ProfileId = "default";
            asset.RightScale = -1f;

            var thrown = Assert.Throws<HapticProfileAssetException>(() => HapticProfileAssetConverter.Convert(asset));
            Assert.That(thrown.Report.IsValid, Is.False);
            Assert.That(thrown.Report.Diagnostics[0].FieldPath, Is.EqualTo("right"));
        }

        [Test]
        public void ProfileAssetValidationReportsAMalformedProfileIdWithIdentityMalformed()
        {
            var asset = ScriptableObject.CreateInstance<HapticProfileAsset>();
            asset.ProfileId = "Not A Valid Id";

            var report = HapticProfileAssetConverter.Validate(asset);

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Diagnostics[0].Code, Is.EqualTo(HapticFailure.IdentityMalformed));
            Assert.That(report.Diagnostics[0].FieldPath, Is.EqualTo("profileId"));
        }

        // ----- envelope degrade to transient (HU-04) -----

        [Test]
        public void EnvelopeDegradeToTransientUsesThePeakAmplitudeAndTotalDurationWithANamedDiagnostic()
        {
            var support = new FixedHapticEnvelopeSupport(false, "quest_runtime_without_envelope");
            var resolution = HapticEnvelopeDegrade.Resolve(support, HapticKind.Envelope, 0.8f, 120f, 150f);

            Assert.That(resolution.Kind, Is.EqualTo(HapticKind.Transient));
            Assert.That(resolution.Amplitude, Is.EqualTo(0.8f));
            Assert.That(resolution.DurationMs, Is.EqualTo(120f));
            Assert.That(resolution.Diagnostic.HasValue, Is.True);
            Assert.That(resolution.Diagnostic.Value.Code, Is.EqualTo(HapticUnityFailure.EnvelopeUnsupportedDegraded));
            Assert.That(resolution.Diagnostic.Value.RuntimeName, Is.EqualTo("quest_runtime_without_envelope"));
        }

        [Test]
        public void EnvelopeDegradeNeverReportsThatTheRequestedEnvelopeKindPlayedWhenItDidNot()
        {
            var support = new FixedHapticEnvelopeSupport(false, "runtime_x");
            var resolution = HapticEnvelopeDegrade.Resolve(support, HapticKind.Envelope, 0.5f, 90f, 200f);

            Assert.That(resolution.Kind, Is.Not.EqualTo(HapticKind.Envelope));
            Assert.That(resolution.FrequencyHz, Is.Null, "a transient does not use frequency");
            Assert.That(resolution.Diagnostic.Value.RequestedKind, Is.EqualTo(HapticKind.Envelope));
            Assert.That(resolution.Diagnostic.Value.PlayedKind, Is.EqualTo(HapticKind.Transient));
        }

        [Test]
        public void EnvelopePlaysUnchangedAndProducesNoDiagnosticWhenTheRuntimeSupportsIt()
        {
            var support = new FixedHapticEnvelopeSupport(true, "runtime_with_envelope");
            var resolution = HapticEnvelopeDegrade.Resolve(support, HapticKind.Envelope, 0.8f, 120f, 150f);

            Assert.That(resolution.Kind, Is.EqualTo(HapticKind.Envelope));
            Assert.That(resolution.FrequencyHz, Is.EqualTo(150f));
            Assert.That(resolution.Diagnostic.HasValue, Is.False);
        }

        [Test]
        public void ATransientOrContinuousKindIsNeverDegradedRegardlessOfSupport()
        {
            var supportless = new FixedHapticEnvelopeSupport(false, "runtime_without_envelope");
            var transient = HapticEnvelopeDegrade.Resolve(supportless, HapticKind.Transient, 0.6f, 40f, null);
            var continuous = HapticEnvelopeDegrade.Resolve(supportless, HapticKind.Continuous, 0.3f, 500f, 90f);

            Assert.That(transient.Kind, Is.EqualTo(HapticKind.Transient));
            Assert.That(transient.Diagnostic.HasValue, Is.False);
            Assert.That(continuous.Kind, Is.EqualTo(HapticKind.Continuous));
            Assert.That(continuous.Diagnostic.HasValue, Is.False);
        }

        // ----- explicit diagnostics for every by-name/optional resolution (HU-05) -----

        [Test]
        public void XriSinkReportsChannelMissingForAChannelTheProbeDoesNotConfirmExists()
        {
            var routeMap = BuildFullRouteMap();
            var sink = new XriImpulseChannelSink(routeMap, new FixedXriChannelProbe(Array.Empty<string>()));

            var result = sink.Play(HapticTarget.Left, HapticKind.Transient, 0.6f, 40f, null);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(HapticUnityFailure.ChannelMissing));
        }

        [Test]
        public void XriSinkSucceedsWhenTheProbeConfirmsTheChannelExists()
        {
            var routeMap = BuildFullRouteMap();
            var sink = new XriImpulseChannelSink(routeMap, new FixedXriChannelProbe(new[] { "left_channel" }));

            var result = sink.Play(HapticTarget.Left, HapticKind.Transient, 0.6f, 40f, null);

            Assert.That(result.Succeeded, Is.True, result.Message);
        }

        [Test]
        public void OpenXrSinkReportsDeviceMissingForAnActionTheProbeDoesNotConfirmExists()
        {
            var routeMap = HapticRouteMap.Create(new Dictionary<HapticTarget, HapticRouteHandle>
            {
                [HapticTarget.Left] = HapticRouteHandle.OpenXrAction("haptic", "/user/hand/left"),
                [HapticTarget.Right] = HapticRouteHandle.OpenXrAction("haptic", "/user/hand/right"),
                [HapticTarget.Both] = HapticRouteHandle.OpenXrAction("haptic", "/user/hand/both"),
            });
            var sink = new OpenXrHapticActionSink(routeMap, new FixedOpenXrDeviceProbe(Array.Empty<(string, string)>()));

            var result = sink.Play(HapticTarget.Left, HapticKind.Transient, 0.6f, 40f, null);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(HapticUnityFailure.DeviceMissing));
        }

        [Test]
        public void InputSystemRumbleFallbackReportsActionMissingForAnUnmappedTarget()
        {
            var sink = new InputSystemRumbleFallbackSink(new Dictionary<HapticTarget, UnityEngine.InputSystem.InputActionReference>(), new FixedInputSystemRumbleProbe(true));

            var result = sink.Play(HapticTarget.Left, HapticKind.Transient, 0.6f, 40f, null);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(HapticUnityFailure.ActionMissing));
        }

        [Test]
        public void InputSystemRumbleFallbackReportsDeviceMissingWhenNoRumbleCapableDeviceResolves()
        {
            var action = new UnityEngine.InputSystem.InputAction("haptic_fallback_test");
            var reference = UnityEngine.InputSystem.InputActionReference.Create(action);
            var routes = new Dictionary<HapticTarget, UnityEngine.InputSystem.InputActionReference> { [HapticTarget.Left] = reference };
            var sink = new InputSystemRumbleFallbackSink(routes, new FixedInputSystemRumbleProbe(false));

            var result = sink.Stop(HapticTarget.Left);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(HapticUnityFailure.DeviceMissing));
        }

        [Test]
        public void NoSinkReportsSuccessWhenItsOwnResolutionFailed()
        {
            var xri = new XriImpulseChannelSink(BuildFullRouteMap(), new FixedXriChannelProbe(Array.Empty<string>()));
            var openXr = new OpenXrHapticActionSink(HapticRouteMap.Create(new Dictionary<HapticTarget, HapticRouteHandle>
            {
                [HapticTarget.Left] = HapticRouteHandle.OpenXrAction("haptic", "/user/hand/left"),
                [HapticTarget.Right] = HapticRouteHandle.OpenXrAction("haptic", "/user/hand/right"),
                [HapticTarget.Both] = HapticRouteHandle.OpenXrAction("haptic", "/user/hand/both"),
            }), new FixedOpenXrDeviceProbe(Array.Empty<(string, string)>()));

            Assert.That(xri.Play(HapticTarget.Left, HapticKind.Transient, 0.6f, 40f, null).Succeeded, Is.False);
            Assert.That(openXr.Play(HapticTarget.Left, HapticKind.Transient, 0.6f, 40f, null).Succeeded, Is.False);
        }

        // ----- no polling (HU-06) -----

        [Test]
        public void NoUnityRuntimeSourceFileDeclaresAPerFrameUpdateLoopOrCoroutine()
        {
            foreach (var (path, text) in RuntimeSourceFiles())
            {
                Assert.That(text, Does.Not.Contain("void Update("), path);
                Assert.That(text, Does.Not.Contain("MonoBehaviour"), path);
                Assert.That(text, Does.Not.Contain("Coroutine"), path);
                Assert.That(text, Does.Not.Contain("InvokeRepeating"), path);
            }
        }

        [Test]
        public void AContinuousKindsDurationIsTheCallersExplicitBoundNotAResentLoop()
        {
            var sink = new RecordingHapticSink();
            var runtime = new HapticUnityRuntime(BuildState(), sink, new FixedHapticEnvelopeSupport(true, "test_runtime"));

            runtime.Apply(new PlayIntent(EngineHum, HapticTarget.Right));

            Assert.That(sink.PlayCalls.Count, Is.EqualTo(1), "one explicit play call, never a resent impulse");
        }

        // ----- explicit references only; independence (HU-07) -----

        [Test]
        public void RuntimeConstructionRequiresExplicitReferences()
        {
            Assert.Throws<ArgumentNullException>(() => new HapticUnityRuntime(null, new RecordingHapticSink(), new FixedHapticEnvelopeSupport(true, "r")));
            Assert.Throws<ArgumentNullException>(() => new HapticUnityRuntime(BuildState(), null, new FixedHapticEnvelopeSupport(true, "r")));
            Assert.Throws<ArgumentNullException>(() => new HapticUnityRuntime(BuildState(), new RecordingHapticSink(), null));
        }

        [Test]
        public void TwoRuntimesConstructedSideBySideShareNoState()
        {
            var firstSink = new RecordingHapticSink();
            var secondSink = new RecordingHapticSink();
            var firstRuntime = new HapticUnityRuntime(BuildState(), firstSink, new FixedHapticEnvelopeSupport(true, "r"));
            var secondRuntime = new HapticUnityRuntime(BuildState(), secondSink, new FixedHapticEnvelopeSupport(true, "r"));

            firstRuntime.Apply(new PlayIntent(GrabContact, HapticTarget.Left));

            Assert.That(firstSink.PlayCalls.Count, Is.EqualTo(1));
            Assert.That(secondSink.PlayCalls, Is.Empty);
            Assert.That(secondRuntime.State.ActiveTargets, Is.Empty);
            Assert.That(ReferenceEquals(firstRuntime.State, secondRuntime.State), Is.False);
        }

        [Test]
        public void NoUnityRuntimeSourceFileUsesResourcesLoadASceneSearchOrASingleton()
        {
            var forbidden = new[] { "Resources.Load", "FindObjectOfType", "FindObjectsOfType", "GameObject.Find", "SceneManager", "static readonly HapticUnityRuntime Instance" };
            foreach (var (path, text) in RuntimeSourceFiles())
            {
                foreach (var token in forbidden)
                {
                    Assert.That(text, Does.Not.Contain(token), $"{path} must not use '{token}'.");
                }
            }
        }

        // ----- explicit route selection as configuration (HU-08) -----

        [Test]
        public void OrderedFallbackTriesEachSinkInTheExplicitOrderUntilOneSucceeds()
        {
            var failing = new RecordingSinkThatAlwaysFails();
            var recording = new RecordingHapticSink();
            var fallback = new OrderedFallbackHapticSink(new IHapticOutputSink[] { failing, recording });

            var result = fallback.Play(HapticTarget.Left, HapticKind.Transient, 0.6f, 40f, null);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(recording.PlayCalls.Count, Is.EqualTo(1));
        }

        [Test]
        public void OrderedFallbackReturnsTheLastFailureWhenEverySinkFails()
        {
            var first = new RecordingSinkThatAlwaysFails();
            var second = new RecordingSinkThatAlwaysFails();
            var fallback = new OrderedFallbackHapticSink(new IHapticOutputSink[] { first, second });

            var result = fallback.Play(HapticTarget.Left, HapticKind.Transient, 0.6f, 40f, null);

            Assert.That(result.Succeeded, Is.False);
        }

        [Test]
        public void NoUnityRuntimeSourceFileChecksTheRuntimePlatformOrABuildTargetDefine()
        {
            foreach (var (path, text) in RuntimeSourceFiles())
            {
                Assert.That(text, Does.Not.Contain("RuntimePlatform"), path);
                Assert.That(text, Does.Not.Contain("#if UNITY_ANDROID"), path);
                Assert.That(text, Does.Not.Contain("#if UNITY_IOS"), path);
                Assert.That(text, Does.Not.Contain("#if UNITY_STANDALONE"), path);
            }
        }

        private sealed class RecordingSinkThatAlwaysFails : IHapticOutputSink
        {
            public HapticSinkResult Play(HapticTarget target, HapticKind kind, float amplitude, float durationMs, float? frequencyHz) =>
                HapticSinkResult.Fail(HapticUnityFailure.DeviceMissing, "always fails, for the fallback-order test");

            public HapticSinkResult Stop(HapticTarget target) => HapticSinkResult.Fail(HapticUnityFailure.DeviceMissing, "always fails");
            public HapticSinkResult StopAll() => HapticSinkResult.Fail(HapticUnityFailure.DeviceMissing, "always fails");
        }

        private static IEnumerable<(string Path, string Text)> RuntimeSourceFiles()
        {
            var runtimeDirectory = System.IO.Path.GetFullPath(System.IO.Path.Combine(TestSourceDirectory(), "..", "..", "Runtime"));
            Assert.That(System.IO.Directory.Exists(runtimeDirectory), Is.True, runtimeDirectory);
            var sourceFiles = System.IO.Directory.GetFiles(runtimeDirectory, "*.cs", System.IO.SearchOption.TopDirectoryOnly);
            Assert.That(sourceFiles, Is.Not.Empty, runtimeDirectory);
            foreach (var sourceFile in sourceFiles)
            {
                yield return (sourceFile, System.IO.File.ReadAllText(sourceFile));
            }
        }

        private static string TestSourceDirectory([CallerFilePath] string sourceFilePath = "") => System.IO.Path.GetDirectoryName(sourceFilePath);
    }
}
