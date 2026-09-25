namespace Lingkyn.PlatformServices.Unity
{
    // Stable codes for by-name or optional resolution failures the Unity adapter reports on its own
    // (a target the Core never sees: a vendor SDK, its singleton, its initialized state, or an
    // optional callback registration) and for the boot entitlement gate, per LESSON-004. Every
    // per-capability validation failure the Core already names (capability.unsupported,
    // identity.missing, and the others) keeps the Core's own code.

    public static class PlatformServicesUnityFailure
    {
        /// <summary>A vendor route's compile-time define (for example <c>XRFOUNDRY_META_PLATFORM</c>)
        /// is absent, so this assembly compiled without that vendor's SDK; the route reports this
        /// code for every call rather than staying silent or reporting a false success.</summary>
        public const string ProviderSdkMissing = "provider.sdk_missing";

        /// <summary>A vendor SDK's own singleton/manager accessor did not resolve to a live instance.</summary>
        public const string ProviderSingletonMissing = "provider.singleton_missing";

        /// <summary>A vendor SDK's own initialized check reports it is not ready.</summary>
        public const string ProviderUninitialized = "provider.uninitialized";

        /// <summary>An optional callback or delegate registration the adapter expects before its
        /// first call was never registered.</summary>
        public const string ProviderCallbackUnregistered = "provider.callback_unregistered";

        /// <summary>The boot entitlement gate is closed (entitlement unresolved or not_entitled) and
        /// no consumer-owned fallback has been applied, so this intent was rejected before it reached
        /// the Core or the provider.</summary>
        public const string EntitlementGateBlocked = "entitlement.gate_blocked";
    }
}
