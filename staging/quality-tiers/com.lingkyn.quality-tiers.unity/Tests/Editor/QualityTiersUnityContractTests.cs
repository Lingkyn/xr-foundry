using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Lingkyn.QualityTiers.Core;
using Lingkyn.QualityTiers.Unity;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Lingkyn.QualityTiers.Unity.Editor.Tests
{
    // Authored and unexecuted: this assembly has never compiled or run. It is checked against
    // docs/standards/quality-tiers/verification-contract.md and coverage-map.json. No test here
    // claims that a device held a measured frame rate, that a render scale or foveation level
    // looked correct, or that a build passed store certification: every assertion is against a
    // fake display, foveation, renderer-asset, or capability-probe double.
    public sealed class QualityTiersUnityContractTests
    {
        private static readonly TierId TierMedium = TierId.Parse("tier.medium");
        private static readonly TierId TierHigh = TierId.Parse("tier.high");
        private static readonly DeviceProfileId DeviceStandalone = DeviceProfileId.Parse("device.standalone");

        private static QualityTierRegistry BuildRegistry()
        {
            var builder = new QualityTierRegistryBuilder();
            builder.Register(TierMedium, new RefreshRateRangeHz(72f, 90f), new RenderScaleRange(0.8f, 1.0f), FoveationLevel.Medium, MsaaSampleCount.Two, new ShadowBudgetRange(5f, 20f), new PostProcessingBudgetRange(5f, 15f), "medium");
            builder.Register(TierHigh, new RefreshRateRangeHz(90f, 120f), new RenderScaleRange(1.0f, 1.4f), FoveationLevel.High, MsaaSampleCount.Four, new ShadowBudgetRange(15f, 40f), new PostProcessingBudgetRange(10f, 30f), "high");
            return builder.Build();
        }

        private static DeviceCapabilitySet BuildCapabilities()
        {
            var builder = new DeviceCapabilitySetBuilder();
            var descriptor = DeviceCapabilityDescriptor.TryCreate(
                DeviceStandalone,
                new[] { 72f, 90f, 120f },
                new RenderScaleRange(0.7f, 1.4f),
                new[] { FoveationLevel.Medium, FoveationLevel.High },
                new[] { MsaaSampleCount.Two, MsaaSampleCount.Four }).Value;
            builder.Register(descriptor);
            return builder.Build();
        }

        private static QualityState InitialState() => QualityState.Initial(BuildRegistry(), BuildCapabilities());

        private static QualityRendererAssetMap BuildRendererAssetMap(QualityTierRegistry registry)
        {
            var bindings = new Dictionary<TierId, (UniversalRenderPipelineAsset Asset, int QualityLevel)>
            {
                [TierMedium] = (ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>(), 1),
                [TierHigh] = (ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>(), 2),
            };
            return QualityRendererAssetMap.Create(registry, bindings);
        }

        // ----- thin adapter over the XR display subsystem for refresh rate (QU-01, partial) -----

        [Test]
        public void ConstructingADisplayResolverThrowsWithDisplayUnresolvedWhenNoDisplaySubsystemResolves()
        {
            var control = new FakeXrDisplayControl(resolves: false);
            var thrown = Assert.Throws<QualityDisplayException>(() => QualityDisplayResolver.Create(control));
            Assert.That(thrown.Report.Diagnostics[0].Code, Is.EqualTo(QualityUnityFailure.DisplayUnresolved));
        }

        [Test]
        public void DisplayResolverAppliesTheRequestedRefreshRateToTheResolvedDisplay()
        {
            var control = new FakeXrDisplayControl(resolves: true);
            var resolver = QualityDisplayResolver.Create(control);
            resolver.ApplyRefreshRate(90f);
            Assert.That(control.AppliedRefreshRates, Is.EqualTo(new[] { 90f }));
        }

        // ----- explicit render-scale and foveation-level application (QU-02) -----

        [Test]
        public void ApplyRenderScaleReportsCapabilityUnsupportedDegradedWhenTheProbeReportsTheValueUnsupported()
        {
            var control = new FakeXrDisplayControl(resolves: true);
            var resolver = QualityDisplayResolver.Create(control);
            var probe = new FixedDeviceCapabilityProbe(new RenderScaleRange(0.5f, 0.9f), new[] { FoveationLevel.Medium }, "fake_runtime");

            var (applied, diagnostic) = resolver.ApplyRenderScale(probe, 1.4f); // outside the probe's [0.5,0.9]

            Assert.That(diagnostic.HasValue, Is.True);
            Assert.That(diagnostic.Value.Code, Is.EqualTo(QualityUnityFailure.CapabilityUnsupportedDegraded));
            Assert.That(diagnostic.Value.RequestedValue, Is.EqualTo(1.4f));
            Assert.That(diagnostic.Value.AppliedValue, Is.EqualTo(0.9f));
            Assert.That(applied, Is.EqualTo(0.9f)); // clamped, never the unsupported requested value
            Assert.That(control.AppliedRenderScales, Is.EqualTo(new[] { 0.9f }));
        }

        [Test]
        public void ApplyFoveationLevelReportsCapabilityUnsupportedDegradedWhenTheProbeReportsTheLevelUnsupported()
        {
            var control = new FakeFoveationControl(resolves: true);
            var resolver = QualityFoveationResolver.Create(control);
            var probe = new FixedDeviceCapabilityProbe(new RenderScaleRange(0.5f, 1.5f), new[] { FoveationLevel.Off, FoveationLevel.Low }, "fake_runtime");

            var (applied, diagnostic) = resolver.Apply(probe, FoveationLevel.High);

            Assert.That(diagnostic.HasValue, Is.True);
            Assert.That(diagnostic.Value.Code, Is.EqualTo(QualityUnityFailure.CapabilityUnsupportedDegraded));
            Assert.That(applied, Is.EqualTo(FoveationLevel.Off));
            Assert.That(control.AppliedLevels, Is.EqualTo(new[] { FoveationLevel.Off }));
        }

        [Test]
        public void ApplyRenderScaleReportsNoDiagnosticWhenTheProbeSupportsTheRequestedValue()
        {
            var control = new FakeXrDisplayControl(resolves: true);
            var resolver = QualityDisplayResolver.Create(control);
            var probe = new FixedDeviceCapabilityProbe(new RenderScaleRange(0.5f, 1.5f), new[] { FoveationLevel.Medium }, "fake_runtime");

            var (applied, diagnostic) = resolver.ApplyRenderScale(probe, 1.0f);

            Assert.That(diagnostic.HasValue, Is.False);
            Assert.That(applied, Is.EqualTo(1.0f));
        }

        // ----- injectable fakes assert exact plumbing for every accepted intent (QU-03) -----

        [Test]
        public void RuntimeDispatchSendsTheResolvedDevicesEffectiveValuesToEveryFakeOnAnAcceptedSelectTier()
        {
            var registry = BuildRegistry();
            var displayControl = new FakeXrDisplayControl(resolves: true);
            var foveationControl = new FakeFoveationControl(resolves: true);
            var probe = new FixedDeviceCapabilityProbe(new RenderScaleRange(0.5f, 1.5f), new[] { FoveationLevel.Medium, FoveationLevel.High }, "fake_runtime");
            var rendererAssetMap = BuildRendererAssetMap(registry);
            var rendererSwitch = new RecordingRendererAssetSwitch();
            var runtime = new QualityUnityRuntime(
                InitialState(),
                QualityDisplayResolver.Create(displayControl),
                QualityFoveationResolver.Create(foveationControl),
                probe,
                rendererAssetMap,
                rendererSwitch);

            var outcome = runtime.Apply(new SelectTierIntent(DeviceStandalone, TierMedium));

            Assert.That(outcome.Accepted, Is.True, outcome.Code);
            Assert.That(displayControl.AppliedRefreshRates, Is.EqualTo(new[] { 90f })); // tier.medium's guard-rail max
            Assert.That(displayControl.AppliedRenderScales, Is.EqualTo(new[] { 1.0f }));
            Assert.That(foveationControl.AppliedLevels, Is.EqualTo(new[] { FoveationLevel.Medium }));
            Assert.That(rendererSwitch.Calls.Count, Is.EqualTo(1));
            Assert.That(rendererSwitch.Calls[0].Tier, Is.EqualTo(TierMedium));
            Assert.That(rendererSwitch.Calls[0].QualityLevel, Is.EqualTo(1));
        }

        [Test]
        public void ARejectedIntentReachesNoSeam()
        {
            var registry = BuildRegistry();
            var displayControl = new FakeXrDisplayControl(resolves: true);
            var foveationControl = new FakeFoveationControl(resolves: true);
            var probe = new FixedDeviceCapabilityProbe(new RenderScaleRange(0.5f, 1.5f), new[] { FoveationLevel.Medium, FoveationLevel.High }, "fake_runtime");
            var rendererSwitch = new RecordingRendererAssetSwitch();
            var runtime = new QualityUnityRuntime(
                InitialState(),
                QualityDisplayResolver.Create(displayControl),
                QualityFoveationResolver.Create(foveationControl),
                probe,
                BuildRendererAssetMap(registry),
                rendererSwitch);

            var outcome = runtime.Apply(new SelectTierIntent(DeviceStandalone, TierId.Parse("tier.unregistered")));

            Assert.That(outcome.Accepted, Is.False);
            Assert.That(outcome.Code, Is.EqualTo(QualityFailure.TierUnknown));
            Assert.That(displayControl.AppliedRefreshRates, Is.Empty);
            Assert.That(foveationControl.AppliedLevels, Is.Empty);
            Assert.That(rendererSwitch.Calls, Is.Empty);
        }

        // ----- MSAA/shadow/post-processing via an explicit URP renderer-asset swap (QU-04) -----

        [Test]
        public void ConstructingARendererAssetMapThrowsWithRendererAssetUnboundWhenATierHasNoBoundAsset()
        {
            var registry = BuildRegistry();
            var incomplete = new Dictionary<TierId, (UniversalRenderPipelineAsset Asset, int QualityLevel)>
            {
                [TierMedium] = (ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>(), 1),
            };
            var thrown = Assert.Throws<QualityRendererAssetException>(() => QualityRendererAssetMap.Create(registry, incomplete));
            Assert.That(thrown.Report.Diagnostics.Count, Is.EqualTo(1));
            Assert.That(thrown.Report.Diagnostics[0].Code, Is.EqualTo(QualityUnityFailure.RendererAssetUnbound));
            Assert.That(thrown.Report.Diagnostics[0].Tier, Is.EqualTo(TierHigh));
        }

        [Test]
        public void RendererAssetMapResolvesTheBoundAssetAndQualityLevelForARegisteredTier()
        {
            var registry = BuildRegistry();
            var map = BuildRendererAssetMap(registry);
            Assert.That(map.TryResolve(TierHigh, out var asset, out var qualityLevel), Is.True);
            Assert.That(asset, Is.Not.Null);
            Assert.That(qualityLevel, Is.EqualTo(2));
        }

        // ----- frame-time sampler reports measured frame time as data (QU-05, partial) -----

        [Test]
        public void FrameTimeSamplerReportsTheSampledFrameTimesAndFractionExceedingTheTargetAsPlainData()
        {
            var policy = FrameBudgetPolicy.TryCreate(11.1f, 0.5f, 5f).Value;
            var source = new FixedFrameTimeSource(new[] { 8f, 9f, 15f, 20f }); // two of four exceed 11.1ms
            var report = QualityFrameTimeSampler.Sample(source, policy);
            Assert.That(report.FrameTimesMs, Is.EqualTo(new[] { 8f, 9f, 15f, 20f }));
            Assert.That(report.FractionExceedingTarget, Is.EqualTo(0.5f));
        }

        [Test]
        public void FrameTimeReportExposesOnlyPlainDataAndNoComfortOrCertifiedVerdict()
        {
            var properties = typeof(QualityFrameTimeReport).GetProperties().Select(property => property.Name).OrderBy(name => name).ToArray();
            Assert.That(properties, Is.EqualTo(new[] { "FractionExceedingTarget", "FrameTimesMs" }));
        }

        // ----- explicit diagnostic for every by-name or optional resolution (QU-06) -----

        [Test]
        public void SettingsAccessorResolutionReportsSettingsUnresolvedWhenNoTargetResolves()
        {
            var (settingName, diagnostic) = QualitySettingsResolution.Resolve(new FixedXrSettingsAccessor(resolves: false));
            Assert.That(settingName, Is.Empty);
            Assert.That(diagnostic.Code, Is.EqualTo(QualityUnityFailure.SettingsUnresolved));
        }

        [Test]
        public void AdaptivePerformanceFeedbackResolutionReportsAdaptivePerformanceUnresolvedWhenNoProviderResolves()
        {
            var (providerName, diagnostic) = QualityAdaptivePerformanceFeedback.Resolve(new FixedAdaptivePerformanceFeedbackProvider(resolves: false));
            Assert.That(providerName, Is.Empty);
            Assert.That(diagnostic.Code, Is.EqualTo(QualityUnityFailure.AdaptivePerformanceUnresolved));
        }

        // ----- the Settings hook (QU-07) -----

        [Test]
        public void QualityOptionHookFromOptionMapsAKnownOptionValueToASelectTierIntent()
        {
            var source = new FixedQualityOptionSource(new Dictionary<string, TierId> { ["high"] = TierMedium });
            var result = QualityOptionHook.FromOption(source, "high", DeviceStandalone);
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Value, Is.InstanceOf<SelectTierIntent>());
            var intent = (SelectTierIntent)result.Value;
            Assert.That(intent.TierId, Is.EqualTo(TierMedium));
            Assert.That(intent.DeviceProfileId, Is.EqualTo(DeviceStandalone));
        }

        [Test]
        public void QualityOptionHookFromOptionRejectsAnUnknownOptionValueWithTierUnknown()
        {
            var source = new FixedQualityOptionSource(new Dictionary<string, TierId> { ["high"] = TierMedium });
            var result = QualityOptionHook.FromOption(source, "ultra", DeviceStandalone);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(QualityFailure.TierUnknown));
        }

        [Test]
        public void UnityAssemblyReferencesNoSettingsType()
        {
            var runtimeDirectory = System.IO.Path.GetFullPath(System.IO.Path.Combine(TestSourceDirectory(), "..", "..", "Runtime"));
            var forbidden = new[] { "Lingkyn.Settings", "SettingsOption", "SettingsProfile" };
            foreach (var sourceFile in System.IO.Directory.GetFiles(runtimeDirectory, "*.cs", System.IO.SearchOption.TopDirectoryOnly))
            {
                var text = System.IO.File.ReadAllText(sourceFile);
                foreach (var token in forbidden)
                {
                    Assert.That(text, Does.Not.Contain(token), $"{System.IO.Path.GetFileName(sourceFile)} must not reference '{token}'.");
                }
            }
        }

        // ----- construction with explicit references only, no polling (QU-08) -----

        [Test]
        public void ConstructingARuntimeThrowsOnANullSeamOrProbe()
        {
            var registry = BuildRegistry();
            var displayResolver = QualityDisplayResolver.Create(new FakeXrDisplayControl(resolves: true));
            var foveationResolver = QualityFoveationResolver.Create(new FakeFoveationControl(resolves: true));
            var probe = new FixedDeviceCapabilityProbe(new RenderScaleRange(0.5f, 1.5f), new[] { FoveationLevel.Medium, FoveationLevel.High }, "fake_runtime");
            var rendererAssetMap = BuildRendererAssetMap(registry);
            var rendererSwitch = new RecordingRendererAssetSwitch();

            Assert.Throws<ArgumentNullException>(() => new QualityUnityRuntime(null, displayResolver, foveationResolver, probe, rendererAssetMap, rendererSwitch));
            Assert.Throws<ArgumentNullException>(() => new QualityUnityRuntime(InitialState(), displayResolver, foveationResolver, null, rendererAssetMap, rendererSwitch));
        }

        [Test]
        public void TwoIndependentlyConstructedRuntimesShareNoStateBetweenTheirFakes()
        {
            var registry = BuildRegistry();
            var probe = new FixedDeviceCapabilityProbe(new RenderScaleRange(0.5f, 1.5f), new[] { FoveationLevel.Medium, FoveationLevel.High }, "fake_runtime");

            var displayA = new FakeXrDisplayControl(resolves: true);
            var runtimeA = new QualityUnityRuntime(InitialState(), QualityDisplayResolver.Create(displayA), QualityFoveationResolver.Create(new FakeFoveationControl(true)), probe, BuildRendererAssetMap(registry), new RecordingRendererAssetSwitch());

            var displayB = new FakeXrDisplayControl(resolves: true);
            var runtimeB = new QualityUnityRuntime(InitialState(), QualityDisplayResolver.Create(displayB), QualityFoveationResolver.Create(new FakeFoveationControl(true)), probe, BuildRendererAssetMap(registry), new RecordingRendererAssetSwitch());

            runtimeA.Apply(new SelectTierIntent(DeviceStandalone, TierMedium));

            Assert.That(displayA.AppliedRefreshRates, Is.Not.Empty);
            Assert.That(displayB.AppliedRefreshRates, Is.Empty);
            Assert.That(runtimeB.State.TryGetSelection(DeviceStandalone, out _), Is.False);
        }

        // ----- explicit route selection as configuration (QU-09) -----

        [Test]
        public void ARuntimeIsWiredWithDifferentConcreteSeamsPurelyByConsumerConstructionChoice()
        {
            var registry = BuildRegistry();
            var probe = new FixedDeviceCapabilityProbe(new RenderScaleRange(0.5f, 1.5f), new[] { FoveationLevel.Medium, FoveationLevel.High }, "fake_runtime");

            var namedDisplay = new FakeXrDisplayControl(resolves: true, displayName: "vendor_a_display");
            var runtimeOne = new QualityUnityRuntime(InitialState(), QualityDisplayResolver.Create(namedDisplay), QualityFoveationResolver.Create(new FakeFoveationControl(true)), probe, BuildRendererAssetMap(registry), new RecordingRendererAssetSwitch());

            var otherDisplay = new FakeXrDisplayControl(resolves: true, displayName: "vendor_b_display");
            var runtimeTwo = new QualityUnityRuntime(InitialState(), QualityDisplayResolver.Create(otherDisplay), QualityFoveationResolver.Create(new FakeFoveationControl(true)), probe, BuildRendererAssetMap(registry), new RecordingRendererAssetSwitch());

            // The runtime itself never inspects a platform or build target; which concrete display
            // it drives is entirely the consumer's own constructor argument.
            Assert.That(QualityDisplayResolver.Create(namedDisplay).DisplayName, Is.EqualTo("vendor_a_display"));
            Assert.That(QualityDisplayResolver.Create(otherDisplay).DisplayName, Is.EqualTo("vendor_b_display"));
            Assert.That(runtimeOne, Is.Not.SameAs(runtimeTwo));
        }

        [Test]
        public void AdaptivePerformanceFeedbackRouteIsOptionalAndNeverBlocksConstructionWhenAbsent()
        {
            var (providerName, diagnostic) = QualityAdaptivePerformanceFeedback.Resolve(new FixedAdaptivePerformanceFeedbackProvider(resolves: false));
            Assert.That(diagnostic, Is.Not.Null); // reported, never silent
            Assert.That(providerName, Is.EqualTo(string.Empty));
            // Resolving it never throws: it is optional configuration, not a required seam.
            Assert.DoesNotThrow(() => QualityAdaptivePerformanceFeedback.Resolve(new FixedAdaptivePerformanceFeedbackProvider(resolves: true)));
        }

        private static string TestSourceDirectory([System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "") => System.IO.Path.GetDirectoryName(sourceFilePath);
    }
}
