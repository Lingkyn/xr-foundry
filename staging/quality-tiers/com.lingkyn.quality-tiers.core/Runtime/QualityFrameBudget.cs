namespace Lingkyn.QualityTiers.Core
{
    // A frame-budget policy as plain data, not an engine type (QC-10). FrameBudgetPolicy has no
    // dependency on any renderer, subsystem, or platform type: it is two positive numbers a caller
    // (this family's Unity adapter, or any other consumer) reads to judge sampled frame times
    // against, never a measurement itself and never a comfort or certification verdict.

    /// <summary>A target frame time and a drop threshold, both plain data. Constructed only through
    /// <see cref="TryCreate"/>, which fails closed with
    /// <see cref="QualityFailure.BudgetDeclarationInvalid"/> for a non-positive target frame time,
    /// a drop threshold outside <c>[0, 1]</c>, or a non-positive sampling window.</summary>
    public sealed class FrameBudgetPolicy
    {
        private FrameBudgetPolicy(float targetFrameTimeMs, float dropThreshold, float samplingWindowSeconds)
        {
            TargetFrameTimeMs = targetFrameTimeMs;
            DropThreshold = dropThreshold;
            SamplingWindowSeconds = samplingWindowSeconds;
        }

        /// <summary>The target frame time in milliseconds a sampled frame is judged against.</summary>
        public float TargetFrameTimeMs { get; }

        /// <summary>The maximum tolerated fraction, in <c>[0, 1]</c>, of frames in a sampling
        /// window that may exceed <see cref="TargetFrameTimeMs"/>.</summary>
        public float DropThreshold { get; }

        /// <summary>The declared sampling window, in seconds, the drop threshold is evaluated
        /// over.</summary>
        public float SamplingWindowSeconds { get; }

        public static QualityResult<FrameBudgetPolicy> TryCreate(float targetFrameTimeMs, float dropThreshold, float samplingWindowSeconds)
        {
            if (!IsFinite(targetFrameTimeMs) || targetFrameTimeMs <= 0f)
            {
                return QualityResult<FrameBudgetPolicy>.Fail(QualityFailure.BudgetDeclarationInvalid, $"Target frame time {targetFrameTimeMs} ms must be positive.");
            }
            if (!IsFinite(dropThreshold) || dropThreshold < 0f || dropThreshold > 1f)
            {
                return QualityResult<FrameBudgetPolicy>.Fail(QualityFailure.BudgetDeclarationInvalid, $"Drop threshold {dropThreshold} must lie within [0, 1].");
            }
            if (!IsFinite(samplingWindowSeconds) || samplingWindowSeconds <= 0f)
            {
                return QualityResult<FrameBudgetPolicy>.Fail(QualityFailure.BudgetDeclarationInvalid, $"Sampling window {samplingWindowSeconds} s must be positive.");
            }
            return QualityResult<FrameBudgetPolicy>.Ok(new FrameBudgetPolicy(targetFrameTimeMs, dropThreshold, samplingWindowSeconds));
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
