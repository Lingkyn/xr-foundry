using System.Collections.Generic;
using Lingkyn.Haptics.Core;

namespace Lingkyn.Haptics.Unity
{
    // The injectable seam every accepted Core play/stop/stop_all outcome is turned into a call
    // on. The runtime never calls a route directly, only through this seam, and a rejected Core
    // intent reaches no sink. A sink reports an explicit HapticSinkResult rather than throwing or
    // silently succeeding (LESSON-004): a route-level failure (a missing channel or device) is a
    // returned result, never an exception and never an unreported success.

    public readonly struct HapticSinkResult
    {
        private HapticSinkResult(bool succeeded, string code, string message)
        {
            Succeeded = succeeded;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public bool Succeeded { get; }
        /// <summary>Empty on success. A stable, non-empty code (from <see cref="HapticUnityFailure"/>
        /// or a reused Core code) on a rejection.</summary>
        public string Code { get; }
        public string Message { get; }

        public static HapticSinkResult Ok() => new HapticSinkResult(true, string.Empty, string.Empty);
        public static HapticSinkResult Fail(string code, string message) => new HapticSinkResult(false, code, message);
    }

    /// <summary>The one seam every haptic-output route (XRI, OpenXR, the Input System rumble
    /// fallback, and the in-memory recording fake) implements. Called only for an accepted Core
    /// outcome, with the Core's own resolved target, kind, and effective amplitude/duration/
    /// frequency — never a route handle directly, so a test can assert exactly what was sent
    /// without a device or a route dependency.</summary>
    public interface IHapticOutputSink
    {
        HapticSinkResult Play(HapticTarget target, HapticKind kind, float amplitude, float durationMs, float? frequencyHz);
        HapticSinkResult Stop(HapticTarget target);
        HapticSinkResult StopAll();
    }

    public readonly struct RecordedPlayCall
    {
        public RecordedPlayCall(HapticTarget target, HapticKind kind, float amplitude, float durationMs, float? frequencyHz)
        {
            Target = target;
            Kind = kind;
            Amplitude = amplitude;
            DurationMs = durationMs;
            FrequencyHz = frequencyHz;
        }

        public HapticTarget Target { get; }
        public HapticKind Kind { get; }
        public float Amplitude { get; }
        public float DurationMs { get; }
        public float? FrequencyHz { get; }
    }

    /// <summary>An in-memory test double: always succeeds and records exactly what it was sent, so
    /// an EditMode test can assert the resolved target, the effective amplitude, the effective
    /// duration, and the effective frequency (or its absence) without a device or a route
    /// dependency.</summary>
    public sealed class RecordingHapticSink : IHapticOutputSink
    {
        private readonly List<RecordedPlayCall> _playCalls = new List<RecordedPlayCall>();
        private readonly List<HapticTarget> _stopCalls = new List<HapticTarget>();

        public IReadOnlyList<RecordedPlayCall> PlayCalls => _playCalls;
        public IReadOnlyList<HapticTarget> StopCalls => _stopCalls;
        public int StopAllCallCount { get; private set; }

        public HapticSinkResult Play(HapticTarget target, HapticKind kind, float amplitude, float durationMs, float? frequencyHz)
        {
            _playCalls.Add(new RecordedPlayCall(target, kind, amplitude, durationMs, frequencyHz));
            return HapticSinkResult.Ok();
        }

        public HapticSinkResult Stop(HapticTarget target)
        {
            _stopCalls.Add(target);
            return HapticSinkResult.Ok();
        }

        public HapticSinkResult StopAll()
        {
            StopAllCallCount++;
            return HapticSinkResult.Ok();
        }
    }
}
