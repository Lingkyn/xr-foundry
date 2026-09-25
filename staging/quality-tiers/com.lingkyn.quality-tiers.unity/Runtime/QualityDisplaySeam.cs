using System;
using System.Collections.Generic;

namespace Lingkyn.QualityTiers.Unity
{
    // A thin seam over the XR display subsystem for refresh rate and render scale, with explicit
    // resolution (verification-contract.md, Unity adapter gate bullet 1): the adapter resolves the
    // active display through an explicit, injected accessor rather than a scene search or a
    // singleton lookup. A missing or unresolved display reports display.unresolved with the route
    // name (LESSON-004); render-scale application goes through the capability-probe-before-write
    // degrade path (QualityCapabilityDegrade), never inferred from a silent no-op.

    /// <summary>Explicit, injected access to the active XR display subsystem. A real
    /// implementation wraps <c>UnityEngine.XR.XRDisplaySubsystem</c>; a fixed fake reports whatever
    /// a test wires it to.</summary>
    public interface IXrDisplayControl
    {
        /// <summary>Resolves the active display's name. False (never a throw) when no display
        /// subsystem is running.</summary>
        bool TryResolveDisplay(out string displayName);

        /// <summary>Applies a refresh rate on the resolved display and returns the value the
        /// runtime actually applied (which a real implementation reads back after the write).</summary>
        float ApplyRefreshRate(string displayName, float requestedHz);

        /// <summary>Applies a render scale on the resolved display and returns the value the
        /// runtime actually applied.</summary>
        float ApplyRenderScale(string displayName, float requestedScale);
    }

    /// <summary>One display-resolution failure: a stable code, the route name, and a human
    /// message. Never silent (LESSON-004).</summary>
    public sealed class QualityDisplayDiagnostic
    {
        public QualityDisplayDiagnostic(string code, string routeName, string message)
        {
            Code = code;
            RouteName = routeName ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Code { get; }
        public string RouteName { get; }
        public string Message { get; }
    }

    public sealed class QualityDisplayReport
    {
        public QualityDisplayReport(IReadOnlyList<QualityDisplayDiagnostic> diagnostics)
        {
            Diagnostics = diagnostics;
        }

        public IReadOnlyList<QualityDisplayDiagnostic> Diagnostics { get; }
        public bool IsValid => Diagnostics.Count == 0;
    }

    public sealed class QualityDisplayException : Exception
    {
        public QualityDisplayException(QualityDisplayReport report) : base(Describe(report))
        {
            Report = report;
        }

        public QualityDisplayReport Report { get; }

        private static string Describe(QualityDisplayReport report)
        {
            var lines = new List<string>(report.Diagnostics.Count + 1) { "Quality display resolution is invalid:" };
            foreach (var diagnostic in report.Diagnostics)
            {
                lines.Add($"  [{diagnostic.Code}] ({diagnostic.RouteName}): {diagnostic.Message}");
            }
            return string.Join("\n", lines);
        }
    }

    /// <summary>Resolves the active display once at construction time, throwing
    /// <see cref="QualityDisplayException"/> with <see cref="QualityUnityFailure.DisplayUnresolved"/>
    /// when no display subsystem resolves (LESSON-004). Holds the resolved display name for the
    /// lifetime of the resolver; a runtime never re-resolves it per frame (no polling).</summary>
    public sealed class QualityDisplayResolver
    {
        private QualityDisplayResolver(IXrDisplayControl control, string displayName)
        {
            Control = control;
            DisplayName = displayName;
        }

        public IXrDisplayControl Control { get; }
        public string DisplayName { get; }

        public static QualityDisplayReport Validate(IXrDisplayControl control)
        {
            if (control == null) throw new ArgumentNullException(nameof(control));
            var diagnostics = new List<QualityDisplayDiagnostic>();
            if (!control.TryResolveDisplay(out _))
            {
                diagnostics.Add(new QualityDisplayDiagnostic(QualityUnityFailure.DisplayUnresolved, "xr_display_subsystem", "No active XRDisplaySubsystem resolved through the injected accessor."));
            }
            return new QualityDisplayReport(diagnostics);
        }

        public static QualityDisplayResolver Create(IXrDisplayControl control)
        {
            var report = Validate(control);
            if (!report.IsValid) throw new QualityDisplayException(report);
            control.TryResolveDisplay(out var displayName);
            return new QualityDisplayResolver(control, displayName);
        }

        public float ApplyRefreshRate(float requestedHz) => Control.ApplyRefreshRate(DisplayName, requestedHz);

        /// <summary>Applies a render scale through the capability-probe-before-write degrade path:
        /// queries <paramref name="probe"/> before writing, applies the resolved (possibly clamped)
        /// value, and returns the value actually applied plus a diagnostic when it was degraded.</summary>
        public (float Applied, QualityDegradeDiagnostic? Diagnostic) ApplyRenderScale(IDeviceCapabilityProbe probe, float requestedScale)
        {
            var (resolved, diagnostic) = QualityCapabilityDegrade.ResolveRenderScale(probe, requestedScale);
            var applied = Control.ApplyRenderScale(DisplayName, resolved);
            return (applied, diagnostic);
        }
    }

    /// <summary>An in-memory test double: resolves to a fixed display (or none), records every
    /// applied value, and echoes back a configurable "actually applied" value so a test can assert
    /// exactly what was requested and what was reported applied.</summary>
    public sealed class FakeXrDisplayControl : IXrDisplayControl
    {
        private readonly bool _resolves;
        private readonly string _displayName;
        private readonly Func<float, float> _refreshRateEcho;
        private readonly Func<float, float> _renderScaleEcho;

        public FakeXrDisplayControl(bool resolves, string displayName = "fake_display", Func<float, float> refreshRateEcho = null, Func<float, float> renderScaleEcho = null)
        {
            _resolves = resolves;
            _displayName = displayName ?? string.Empty;
            _refreshRateEcho = refreshRateEcho ?? (requested => requested);
            _renderScaleEcho = renderScaleEcho ?? (requested => requested);
        }

        public List<float> AppliedRefreshRates { get; } = new List<float>();
        public List<float> AppliedRenderScales { get; } = new List<float>();

        public bool TryResolveDisplay(out string displayName)
        {
            displayName = _resolves ? _displayName : string.Empty;
            return _resolves;
        }

        public float ApplyRefreshRate(string displayName, float requestedHz)
        {
            AppliedRefreshRates.Add(requestedHz);
            return _refreshRateEcho(requestedHz);
        }

        public float ApplyRenderScale(string displayName, float requestedScale)
        {
            AppliedRenderScales.Add(requestedScale);
            return _renderScaleEcho(requestedScale);
        }
    }
}
