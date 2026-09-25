using System;

namespace Lingkyn.PlatformServices.Unity
{
    // The three by-name/optional resolutions LESSON-004 names for a vendor SDK adapter: its own
    // singleton/manager accessor, its SDK-initialized check, and an optional callback or delegate
    // registration. Generic and vendor-agnostic so it is fully testable with fixed fakes, independent
    // of any specific vendor SDK or compile-time define; a real vendor shell (behind its own #if)
    // wires this gate to that vendor's own singleton/initialized/callback state before its first call.

    /// <summary>Explicit, injected confirmation that a vendor SDK's own singleton/manager accessor
    /// resolves to a live instance. A real implementation queries that vendor SDK's own singleton or
    /// initialization result (each vendor route in this package wires its own, behind its own
    /// compile-time define); a fixed fake reports whatever a test wires it to.</summary>
    public interface IVendorSingletonProbe
    {
        bool SingletonResolves { get; }
    }

    public sealed class FixedVendorSingletonProbe : IVendorSingletonProbe
    {
        public FixedVendorSingletonProbe(bool resolves) { SingletonResolves = resolves; }
        public bool SingletonResolves { get; }
    }

    /// <summary>Explicit, injected confirmation that the vendor SDK reports itself initialized.</summary>
    public interface IVendorInitializedProbe
    {
        bool IsInitialized { get; }
    }

    public sealed class FixedVendorInitializedProbe : IVendorInitializedProbe
    {
        public FixedVendorInitializedProbe(bool isInitialized) { IsInitialized = isInitialized; }
        public bool IsInitialized { get; }
    }

    /// <summary>Explicit, injected confirmation that an optional callback or delegate this adapter
    /// expects before its first call has actually been registered.</summary>
    public interface IVendorCallbackProbe
    {
        bool CallbackIsRegistered { get; }
    }

    public sealed class FixedVendorCallbackProbe : IVendorCallbackProbe
    {
        public FixedVendorCallbackProbe(bool registered) { CallbackIsRegistered = registered; }
        public bool CallbackIsRegistered { get; }
    }

    /// <summary>Checked, in this fixed order, before a vendor shell attempts its first real call:
    /// the singleton accessor, the initialized check, then the optional callback registration. Each
    /// miss reports an explicit, stable code rather than staying silent or reporting a false success
    /// (LESSON-004).</summary>
    public sealed class VendorReadinessGate
    {
        private readonly IVendorSingletonProbe _singleton;
        private readonly IVendorInitializedProbe _initialized;
        private readonly IVendorCallbackProbe _callback;

        public VendorReadinessGate(IVendorSingletonProbe singleton, IVendorInitializedProbe initialized, IVendorCallbackProbe callback)
        {
            _singleton = singleton ?? throw new ArgumentNullException(nameof(singleton));
            _initialized = initialized ?? throw new ArgumentNullException(nameof(initialized));
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));
        }

        /// <summary>Null when every check passes; otherwise the first failing check's stable code.</summary>
        public string CheckReadiness()
        {
            if (!_singleton.SingletonResolves) return PlatformServicesUnityFailure.ProviderSingletonMissing;
            if (!_initialized.IsInitialized) return PlatformServicesUnityFailure.ProviderUninitialized;
            if (!_callback.CallbackIsRegistered) return PlatformServicesUnityFailure.ProviderCallbackUnregistered;
            return null;
        }
    }
}
