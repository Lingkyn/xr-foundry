using Lingkyn.Haptics.Core;

namespace Lingkyn.Haptics.Unity
{
    // Envelope playback degrading to a transient with a diagnostic when the active runtime does
    // not report the amplitude-envelope (or PCM) OpenXR extension. The adapter never reports that
    // the requested envelope kind played when it did not: the diagnostic and the kind actually
    // sent both say "transient".

    /// <summary>An explicit, injected report of whether the active runtime supports the
    /// amplitude-envelope (or PCM) OpenXR extension. A real implementation queries the runtime
    /// once at construction; a fixed fake reports whatever a test wires it to.</summary>
    public interface IHapticEnvelopeSupport
    {
        bool SupportsEnvelope { get; }
        /// <summary>A name for diagnostics: the runtime this support query was read from.</summary>
        string RuntimeName { get; }
    }

    /// <summary>A fixed, explicitly-constructed <see cref="IHapticEnvelopeSupport"/>. Used by tests
    /// and by a consumer that already knows its target runtime's extension support ahead of time.</summary>
    public sealed class FixedHapticEnvelopeSupport : IHapticEnvelopeSupport
    {
        public FixedHapticEnvelopeSupport(bool supportsEnvelope, string runtimeName)
        {
            SupportsEnvelope = supportsEnvelope;
            RuntimeName = runtimeName ?? string.Empty;
        }

        public bool SupportsEnvelope { get; }
        public string RuntimeName { get; }
    }

    /// <summary>Names the substitution: the runtime that lacked the extension, the kind that was
    /// requested (always <see cref="HapticKind.Envelope"/>), and the kind that actually played
    /// (always <see cref="HapticKind.Transient"/>).</summary>
    public readonly struct HapticEnvelopeDiagnostic
    {
        public HapticEnvelopeDiagnostic(string runtimeName, HapticKind requestedKind, HapticKind playedKind)
        {
            RuntimeName = runtimeName ?? string.Empty;
            RequestedKind = requestedKind;
            PlayedKind = playedKind;
        }

        public string Code => HapticUnityFailure.EnvelopeUnsupportedDegraded;
        public string RuntimeName { get; }
        public HapticKind RequestedKind { get; }
        public HapticKind PlayedKind { get; }
        public string Message => $"Runtime '{RuntimeName}' does not report the amplitude-envelope extension; '{RequestedKind}' played as '{PlayedKind}'.";
    }

    /// <summary>Resolves what a single accepted play outcome actually sends to the
    /// <see cref="IHapticOutputSink"/>.</summary>
    public static class HapticEnvelopeDegrade
    {
        /// <summary>When <paramref name="requestedKind"/> is <see cref="HapticKind.Envelope"/> and
        /// <paramref name="support"/> reports no extension support, returns
        /// <see cref="HapticKind.Transient"/> using the envelope's own peak amplitude and total
        /// duration (the frequency is dropped, since a transient does not use one) plus a named
        /// diagnostic. Otherwise returns the requested kind and values unchanged and no diagnostic.</summary>
        public static HapticEnvelopeResolution Resolve(IHapticEnvelopeSupport support, HapticKind requestedKind, float amplitude, float durationMs, float? frequencyHz)
        {
            if (requestedKind != HapticKind.Envelope || (support != null && support.SupportsEnvelope))
            {
                return new HapticEnvelopeResolution(requestedKind, amplitude, durationMs, frequencyHz, null);
            }
            var runtimeName = support != null ? support.RuntimeName : string.Empty;
            var diagnostic = new HapticEnvelopeDiagnostic(runtimeName, requestedKind, HapticKind.Transient);
            return new HapticEnvelopeResolution(HapticKind.Transient, amplitude, durationMs, null, diagnostic);
        }
    }

    public readonly struct HapticEnvelopeResolution
    {
        public HapticEnvelopeResolution(HapticKind kind, float amplitude, float durationMs, float? frequencyHz, HapticEnvelopeDiagnostic? diagnostic)
        {
            Kind = kind;
            Amplitude = amplitude;
            DurationMs = durationMs;
            FrequencyHz = frequencyHz;
            Diagnostic = diagnostic;
        }

        public HapticKind Kind { get; }
        public float Amplitude { get; }
        public float DurationMs { get; }
        public float? FrequencyHz { get; }
        /// <summary>Null unless this play was degraded from envelope to transient.</summary>
        public HapticEnvelopeDiagnostic? Diagnostic { get; }
    }
}
