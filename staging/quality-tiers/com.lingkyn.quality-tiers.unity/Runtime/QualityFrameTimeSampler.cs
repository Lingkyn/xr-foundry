using System;
using System.Collections.Generic;
using System.Linq;
using Lingkyn.QualityTiers.Core;

namespace Lingkyn.QualityTiers.Unity
{
    // A frame-time sampler that reports measured frame time as plain data over an explicit
    // sampling window, with no claim of comfort or of a passed frame budget
    // (verification-contract.md, Unity adapter gate bullet 5; QU-05, partial — this file's
    // arithmetic is proven with an injected fixed frame-time source; a real measured value from a
    // running device is a Device Lab receipt, never a package claim, per the claim ceiling).

    /// <summary>Explicit, injected source of one sampling window's frame times, in milliseconds. A
    /// real implementation reads a rolling buffer the runtime fills every frame; a fixed fake
    /// returns whatever a test wires it to.</summary>
    public interface IFrameTimeSource
    {
        IReadOnlyList<float> SampleWindow();
    }

    public sealed class FixedFrameTimeSource : IFrameTimeSource
    {
        private readonly IReadOnlyList<float> _frameTimesMs;

        public FixedFrameTimeSource(IReadOnlyList<float> frameTimesMs)
        {
            _frameTimesMs = frameTimesMs ?? Array.Empty<float>();
        }

        public IReadOnlyList<float> SampleWindow() => _frameTimesMs;
    }

    /// <summary>The sampler's report: the sampled frame times as given, and the fraction exceeding
    /// the <see cref="FrameBudgetPolicy"/>'s target frame time as a computed value the caller
    /// reads. Carries no comfort or certification verdict of any kind: there is no such field or
    /// method here, and none is ever computed.</summary>
    public sealed class QualityFrameTimeReport
    {
        public QualityFrameTimeReport(IReadOnlyList<float> frameTimesMs, float fractionExceedingTarget)
        {
            FrameTimesMs = frameTimesMs;
            FractionExceedingTarget = fractionExceedingTarget;
        }

        public IReadOnlyList<float> FrameTimesMs { get; }
        public float FractionExceedingTarget { get; }
    }

    public static class QualityFrameTimeSampler
    {
        /// <summary>Reads <paramref name="source"/>'s current window and reports it alongside the
        /// fraction of sampled frame times exceeding <paramref name="policy"/>'s target frame time.
        /// An empty window reports a fraction of zero. This method never asserts, logs, or returns
        /// a "comfortable" or "certified" verdict.</summary>
        public static QualityFrameTimeReport Sample(IFrameTimeSource source, FrameBudgetPolicy policy)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            var frameTimes = source.SampleWindow() ?? Array.Empty<float>();
            if (frameTimes.Count == 0)
            {
                return new QualityFrameTimeReport(frameTimes, 0f);
            }
            var exceeding = frameTimes.Count(frameTime => frameTime > policy.TargetFrameTimeMs);
            var fraction = (float)exceeding / frameTimes.Count;
            return new QualityFrameTimeReport(frameTimes, fraction);
        }
    }
}
