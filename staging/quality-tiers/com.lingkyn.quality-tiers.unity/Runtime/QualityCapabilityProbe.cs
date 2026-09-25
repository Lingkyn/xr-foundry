using System;
using System.Collections.Generic;
using System.Linq;
using Lingkyn.QualityTiers.Core;

namespace Lingkyn.QualityTiers.Unity
{
    // The explicit, injected accessor the render-scale and foveation-level seams query "before the
    // write, never inferred from a silent no-op" (verification-contract.md, Unity adapter gate
    // bullet 2). A real implementation queries the live runtime once per apply; a fixed fake
    // reports whatever a test wires it to.

    public interface IDeviceCapabilityProbe
    {
        /// <summary>The live render-scale range the active runtime actually honors right now. A
        /// real implementation reads it from the active XR display/runtime; may differ from the
        /// Core's own <see cref="DeviceCapabilityDescriptor"/> when a runtime is more restrictive
        /// than declared.</summary>
        RenderScaleRange SupportedRenderScaleRange { get; }

        /// <summary>The live set of foveation levels the active runtime actually honors right now.</summary>
        IReadOnlyCollection<FoveationLevel> SupportedFoveationLevels { get; }

        /// <summary>A name for diagnostics: the runtime this probe queried.</summary>
        string RuntimeName { get; }
    }

    /// <summary>A fixed, explicitly-constructed <see cref="IDeviceCapabilityProbe"/> for tests and
    /// for a consumer that already knows its target runtime's live capability ahead of time.</summary>
    public sealed class FixedDeviceCapabilityProbe : IDeviceCapabilityProbe
    {
        public FixedDeviceCapabilityProbe(RenderScaleRange supportedRenderScaleRange, IEnumerable<FoveationLevel> supportedFoveationLevels, string runtimeName)
        {
            SupportedRenderScaleRange = supportedRenderScaleRange;
            SupportedFoveationLevels = new List<FoveationLevel>(supportedFoveationLevels ?? Enumerable.Empty<FoveationLevel>());
            RuntimeName = runtimeName ?? string.Empty;
        }

        public RenderScaleRange SupportedRenderScaleRange { get; }
        public IReadOnlyCollection<FoveationLevel> SupportedFoveationLevels { get; }
        public string RuntimeName { get; }
    }

    /// <summary>Names one capability-degrade substitution: the runtime that could not honor the
    /// requested value, the value requested, and the value actually applied instead. The adapter
    /// never reports that the requested value took effect when it did not.</summary>
    public readonly struct QualityDegradeDiagnostic
    {
        public QualityDegradeDiagnostic(string runtimeName, string fieldName, float requestedValue, float appliedValue)
        {
            RuntimeName = runtimeName ?? string.Empty;
            FieldName = fieldName ?? string.Empty;
            RequestedValue = requestedValue;
            AppliedValue = appliedValue;
        }

        public string Code => QualityUnityFailure.CapabilityUnsupportedDegraded;
        public string RuntimeName { get; }
        public string FieldName { get; }
        public float RequestedValue { get; }
        public float AppliedValue { get; }
        public string Message => $"Runtime '{RuntimeName}' does not support {FieldName}={RequestedValue}; applied {AppliedValue} instead.";
    }

    public static class QualityCapabilityDegrade
    {
        /// <summary>Resolves what render scale is actually applied: the requested value when the
        /// probe reports it supported, otherwise the nearest bound of the probe's own supported
        /// range, with a named diagnostic.</summary>
        public static (float Applied, QualityDegradeDiagnostic? Diagnostic) ResolveRenderScale(IDeviceCapabilityProbe probe, float requested)
        {
            if (probe == null) throw new ArgumentNullException(nameof(probe));
            if (probe.SupportedRenderScaleRange.Contains(requested))
            {
                return (requested, null);
            }
            var clamped = Math.Min(Math.Max(requested, probe.SupportedRenderScaleRange.Min), probe.SupportedRenderScaleRange.Max);
            return (clamped, new QualityDegradeDiagnostic(probe.RuntimeName, "render_scale", requested, clamped));
        }

        /// <summary>Resolves what foveation level is actually applied: the requested level when the
        /// probe reports it supported, otherwise <see cref="FoveationLevel.Off"/> as the universally
        /// safe fallback, with a named diagnostic.</summary>
        public static (FoveationLevel Applied, QualityDegradeDiagnostic? Diagnostic) ResolveFoveationLevel(IDeviceCapabilityProbe probe, FoveationLevel requested)
        {
            if (probe == null) throw new ArgumentNullException(nameof(probe));
            if (probe.SupportedFoveationLevels.Contains(requested))
            {
                return (requested, null);
            }
            return (FoveationLevel.Off, new QualityDegradeDiagnostic(probe.RuntimeName, "foveation_level", (float)requested, (float)FoveationLevel.Off));
        }
    }
}
