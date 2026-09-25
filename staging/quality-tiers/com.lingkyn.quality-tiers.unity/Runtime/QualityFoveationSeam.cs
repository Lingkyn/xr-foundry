using System;
using System.Collections.Generic;
using Lingkyn.QualityTiers.Core;

namespace Lingkyn.QualityTiers.Unity
{
    // Explicit foveation-level application through its own accessor (verification-contract.md,
    // Unity adapter gate bullet 2), separate from the display seam: setting a level the runtime
    // reports as unsupported (queried through IDeviceCapabilityProbe before the write) reports
    // capability.unsupported.degraded naming the runtime, the requested level, and the level
    // actually applied.

    /// <summary>Explicit, injected access to the active XR foveation provider (for example the
    /// OpenXR foveation extension or a vendor foveated-rendering API). A real implementation wraps
    /// that provider; a fixed fake reports whatever a test wires it to.</summary>
    public interface IFoveationControl
    {
        /// <summary>Resolves the active foveation provider's name. False (never a throw) when no
        /// provider is running.</summary>
        bool TryResolveProvider(out string providerName);

        /// <summary>Applies a foveation level on the resolved provider and returns the level the
        /// runtime actually applied.</summary>
        FoveationLevel Apply(string providerName, FoveationLevel requested);
    }

    public sealed class QualityFoveationResolver
    {
        private QualityFoveationResolver(IFoveationControl control, string providerName)
        {
            Control = control;
            ProviderName = providerName;
        }

        public IFoveationControl Control { get; }
        public string ProviderName { get; }

        public static QualityDisplayReport Validate(IFoveationControl control)
        {
            if (control == null) throw new ArgumentNullException(nameof(control));
            var diagnostics = new List<QualityDisplayDiagnostic>();
            if (!control.TryResolveProvider(out _))
            {
                // The foveation provider is an XRSettings-style accessor for this diagnostic's
                // purposes (verification-contract.md, Unity adapter gate bullet 6): resolved by an
                // optional lookup, reported explicitly rather than silently when missing.
                diagnostics.Add(new QualityDisplayDiagnostic(QualityUnityFailure.SettingsUnresolved, "xr_foveation_provider", "No active foveation provider resolved through the injected accessor."));
            }
            return new QualityDisplayReport(diagnostics);
        }

        public static QualityFoveationResolver Create(IFoveationControl control)
        {
            var report = Validate(control);
            if (!report.IsValid) throw new QualityDisplayException(report);
            control.TryResolveProvider(out var providerName);
            return new QualityFoveationResolver(control, providerName);
        }

        /// <summary>Applies a foveation level through the capability-probe-before-write degrade
        /// path: queries <paramref name="probe"/> before writing, applies the resolved (possibly
        /// substituted) level, and returns the level actually applied plus a diagnostic when it was
        /// degraded.</summary>
        public (FoveationLevel Applied, QualityDegradeDiagnostic? Diagnostic) Apply(IDeviceCapabilityProbe probe, FoveationLevel requested)
        {
            var (resolved, diagnostic) = QualityCapabilityDegrade.ResolveFoveationLevel(probe, requested);
            var applied = Control.Apply(ProviderName, resolved);
            return (applied, diagnostic);
        }
    }

    /// <summary>An in-memory test double: resolves to a fixed provider (or none), records every
    /// applied level, and echoes back a configurable "actually applied" level.</summary>
    public sealed class FakeFoveationControl : IFoveationControl
    {
        private readonly bool _resolves;
        private readonly string _providerName;
        private readonly Func<FoveationLevel, FoveationLevel> _echo;

        public FakeFoveationControl(bool resolves, string providerName = "fake_foveation_provider", Func<FoveationLevel, FoveationLevel> echo = null)
        {
            _resolves = resolves;
            _providerName = providerName ?? string.Empty;
            _echo = echo ?? (requested => requested);
        }

        public List<FoveationLevel> AppliedLevels { get; } = new List<FoveationLevel>();

        public bool TryResolveProvider(out string providerName)
        {
            providerName = _resolves ? _providerName : string.Empty;
            return _resolves;
        }

        public FoveationLevel Apply(string providerName, FoveationLevel requested)
        {
            AppliedLevels.Add(requested);
            return _echo(requested);
        }
    }
}
