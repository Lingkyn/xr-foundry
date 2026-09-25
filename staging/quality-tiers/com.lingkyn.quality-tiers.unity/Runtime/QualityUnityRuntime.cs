using System;
using System.Collections.Generic;
using Lingkyn.QualityTiers.Core;

namespace Lingkyn.QualityTiers.Unity
{
    // A plain runtime constructed with explicit references only (verification-contract.md, Unity
    // adapter gate bullet 8): an initial Core QualityState, a resolved display seam, a resolved
    // foveation seam, an injected capability probe, an explicit renderer-asset map, and an
    // injectable renderer-asset switch. No scene singleton, static instance, scene search, or
    // reflection discovery resolves any of these; the runtime is driven only by the explicit Apply
    // calls it receives and holds no per-frame update loop that re-applies a tier on its own (no
    // polling). Which routes a runtime uses (the XR display route, the URP renderer-asset route, an
    // optional Adaptive Performance feedback route) is the consumer's explicit wiring at
    // construction time, never a runtime check of the active platform (bullet 9).

    /// <summary>The values one device's accepted selection resolves to, given its selected tier and
    /// any overrides. For a ranged field (refresh rate, render scale, shadow budget,
    /// post-processing budget) with no override, this adapter applies the tier's own guard-rail
    /// maximum — an adapter-side convention documented here, not a Core semantic: the Core tracks
    /// only the selected tier id and any override fields (see QualityState).</summary>
    public readonly struct EffectiveQualityValues
    {
        public EffectiveQualityValues(float refreshRateHz, float renderScale, FoveationLevel foveation, MsaaSampleCount msaa, float shadowBudget, float postProcessingBudget)
        {
            RefreshRateHz = refreshRateHz;
            RenderScale = renderScale;
            Foveation = foveation;
            Msaa = msaa;
            ShadowBudget = shadowBudget;
            PostProcessingBudget = postProcessingBudget;
        }

        public float RefreshRateHz { get; }
        public float RenderScale { get; }
        public FoveationLevel Foveation { get; }
        public MsaaSampleCount Msaa { get; }
        public float ShadowBudget { get; }
        public float PostProcessingBudget { get; }
    }

    public static class QualityEffectiveValuesResolver
    {
        public static EffectiveQualityValues Resolve(QualityTierDefinition tier, DeviceSelection selection)
        {
            var refreshRate = selection.Overrides.TryGetValue(QualityOverrideField.RefreshRate, out var refreshOverride) ? refreshOverride : tier.RefreshRate.Max;
            var renderScale = selection.Overrides.TryGetValue(QualityOverrideField.RenderScale, out var scaleOverride) ? scaleOverride : tier.RenderScale.Max;
            var foveation = selection.Overrides.TryGetValue(QualityOverrideField.FoveationLevel, out var foveationOverride) ? (FoveationLevel)(int)foveationOverride : tier.Foveation;
            var msaa = selection.Overrides.TryGetValue(QualityOverrideField.Msaa, out var msaaOverride) ? (MsaaSampleCount)(int)msaaOverride : tier.Msaa;
            var shadowBudget = selection.Overrides.TryGetValue(QualityOverrideField.ShadowBudget, out var shadowOverride) ? shadowOverride : tier.ShadowBudget.Max;
            var postProcessingBudget = selection.Overrides.TryGetValue(QualityOverrideField.PostProcessingBudget, out var postOverride) ? postOverride : tier.PostProcessingBudget.Max;
            return new EffectiveQualityValues(refreshRate, renderScale, foveation, msaa, shadowBudget, postProcessingBudget);
        }
    }

    public sealed class QualityUnityRuntime
    {
        private readonly QualityDisplayResolver _displayResolver;
        private readonly QualityFoveationResolver _foveationResolver;
        private readonly IDeviceCapabilityProbe _capabilityProbe;
        private readonly QualityRendererAssetMap _rendererAssetMap;
        private readonly IRendererAssetSwitch _rendererAssetSwitch;
        private readonly List<QualityIntentOutcome> _outcomes = new List<QualityIntentOutcome>();
        private readonly List<QualityDegradeDiagnostic> _degradeDiagnostics = new List<QualityDegradeDiagnostic>();

        public QualityUnityRuntime(
            QualityState initialState,
            QualityDisplayResolver displayResolver,
            QualityFoveationResolver foveationResolver,
            IDeviceCapabilityProbe capabilityProbe,
            QualityRendererAssetMap rendererAssetMap,
            IRendererAssetSwitch rendererAssetSwitch)
        {
            State = initialState ?? throw new ArgumentNullException(nameof(initialState));
            _displayResolver = displayResolver ?? throw new ArgumentNullException(nameof(displayResolver));
            _foveationResolver = foveationResolver ?? throw new ArgumentNullException(nameof(foveationResolver));
            _capabilityProbe = capabilityProbe ?? throw new ArgumentNullException(nameof(capabilityProbe));
            _rendererAssetMap = rendererAssetMap ?? throw new ArgumentNullException(nameof(rendererAssetMap));
            _rendererAssetSwitch = rendererAssetSwitch ?? throw new ArgumentNullException(nameof(rendererAssetSwitch));
        }

        public QualityState State { get; private set; }

        /// <summary>Every intent this runtime saw, accepted or rejected, in the order it saw them.</summary>
        public IReadOnlyList<QualityIntentOutcome> Outcomes => _outcomes;

        /// <summary>Every capability degrade (render scale or foveation level) this runtime has
        /// reported, in order.</summary>
        public IReadOnlyList<QualityDegradeDiagnostic> DegradeDiagnostics => _degradeDiagnostics;

        /// <summary>Applies one intent through the Core's one entry point
        /// (<see cref="QualityState.Apply"/>) and no other path: a rejected intent (including a
        /// stale <c>state.stale</c>) reaches no seam. Two runtimes constructed side by side over two
        /// states and two sets of fakes share nothing: applying an intent to one changes no
        /// display, foveation, or renderer-asset call recorded by the other.</summary>
        public QualityIntentOutcome Apply(QualityIntent intent)
        {
            if (intent == null) throw new ArgumentNullException(nameof(intent));
            var index = _outcomes.Count;
            var result = State.Apply(intent);
            if (!result.Succeeded)
            {
                var rejected = new QualityIntentOutcome(index, intent, false, result.Code, result.Message, State.Revision);
                _outcomes.Add(rejected);
                return rejected;
            }
            State = result.Value;
            Dispatch(intent.DeviceProfileId);
            var accepted = new QualityIntentOutcome(index, intent, true, result.Code, result.Message, State.Revision);
            _outcomes.Add(accepted);
            return accepted;
        }

        /// <summary>Turns one accepted intent's resulting selection into exactly one call on each
        /// of the three explicit seams. Nothing to dispatch when the device has no selection left
        /// (a reset on a device that was never selected).</summary>
        private void Dispatch(DeviceProfileId deviceProfileId)
        {
            if (!State.TryGetSelection(deviceProfileId, out var selection)) return;
            if (!State.Registry.TryGet(selection.TierId, out var tier)) return;

            var effective = QualityEffectiveValuesResolver.Resolve(tier, selection);

            _displayResolver.ApplyRefreshRate(effective.RefreshRateHz);

            var (_, scaleDiagnostic) = _displayResolver.ApplyRenderScale(_capabilityProbe, effective.RenderScale);
            if (scaleDiagnostic.HasValue) _degradeDiagnostics.Add(scaleDiagnostic.Value);

            var (_, foveationDiagnostic) = _foveationResolver.Apply(_capabilityProbe, effective.Foveation);
            if (foveationDiagnostic.HasValue) _degradeDiagnostics.Add(foveationDiagnostic.Value);

            if (_rendererAssetMap.TryResolve(selection.TierId, out var asset, out var qualityLevel))
            {
                _rendererAssetSwitch.Apply(selection.TierId, asset, qualityLevel);
            }
        }
    }
}
