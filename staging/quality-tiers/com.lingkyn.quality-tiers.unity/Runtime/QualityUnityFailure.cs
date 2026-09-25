namespace Lingkyn.QualityTiers.Unity
{
    // Stable codes for the Unity adapter's own by-name or optional resolution failures (a target
    // the Core never sees: a display, a settings accessor, a renderer asset, an Adaptive
    // Performance provider). These are exactly the codes
    // docs/standards/quality-tiers/verification-contract.md names for the Unity adapter gate
    // (display.unresolved, capability.unsupported.degraded, renderer.asset.unbound), plus two more
    // for the by-name resolution targets the contract's diagnostic clause names but does not
    // itself code (an XRSettings accessor, an optional Adaptive Performance provider query),
    // following the same LESSON-004 pattern staging/haptics uses for its own adapter-only codes.
    // Every Core-level rejection (tier.unknown, device.unknown, capability.unsupported,
    // value.out_of_range, state.stale, and the rest) keeps the Core's own code unchanged.

    public static class QualityUnityFailure
    {
        /// <summary>The active <c>XRDisplaySubsystem</c> does not resolve through the injected
        /// <see cref="IXrDisplayControl"/> accessor. Construction throws with the same report,
        /// carrying a negative test for the missing subsystem (LESSON-004).</summary>
        public const string DisplayUnresolved = "display.unresolved";

        /// <summary>Diagnostic-only (never a rejection): a render-scale or foveation-level write
        /// the runtime reports as unsupported, queried through the capability-descriptor accessor
        /// before the write. Names the runtime, the requested value, and the value actually
        /// applied; never reports that the requested value took effect when it did not.</summary>
        public const string CapabilityUnsupportedDegraded = "capability.unsupported.degraded";

        /// <summary>A registered tier has no bound <c>UniversalRenderPipelineAsset</c> in the
        /// <see cref="QualityRendererAssetMap"/>. Construction throws with the same report, carrying
        /// a negative test for the unbound tier (LESSON-004).</summary>
        public const string RendererAssetUnbound = "renderer.asset.unbound";

        /// <summary>An <c>XRSettings</c>-style accessor resolved by name or by an optional lookup
        /// does not resolve to a live target.</summary>
        public const string SettingsUnresolved = "settings.unresolved";

        /// <summary>An optional Adaptive Performance feedback provider query does not resolve to a
        /// live provider. Never a rejection on its own: the feedback route is optional
        /// configuration, but a missing provider is still reported explicitly, never silent.</summary>
        public const string AdaptivePerformanceUnresolved = "adaptive_performance.unresolved";
    }
}
