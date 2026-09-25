using System;

namespace Lingkyn.QualityTiers.Core
{
    // The closed FoveationLevel and MsaaSampleCount sets, the four closed guard-rail range types
    // (refresh rate, render scale, shadow budget, post-processing budget), and QualityTierDefinition
    // itself: one declared tier's guard rails, validated fail-closed with tier.declaration.invalid.

    /// <summary>The small closed set of foveation levels a tier or a device capability descriptor
    /// may declare. Adding a member here is a breaking change.</summary>
    public enum FoveationLevel
    {
        Off,
        Low,
        Medium,
        High,
    }

    /// <summary>The closed set of MSAA sample counts a tier or a device capability descriptor may
    /// declare. Adding a member here is a breaking change.</summary>
    public enum MsaaSampleCount
    {
        None = 1,
        Two = 2,
        Four = 4,
        Eight = 8,
    }

    /// <summary>A closed, positive range in hertz for a tier's refresh rate. Invalid when a bound
    /// is non-finite, the lower bound is not positive, or the range is inverted.</summary>
    public readonly struct RefreshRateRangeHz
    {
        public RefreshRateRangeHz(float min, float max) { Min = min; Max = max; }
        public float Min { get; }
        public float Max { get; }
        public bool Contains(float value) => !float.IsNaN(value) && value >= Min && value <= Max;
        internal bool IsValid => IsFinite(Min) && IsFinite(Max) && Min > 0f && Min <= Max;
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>A closed, positive range for a tier's render scale. Invalid when a bound is
    /// non-finite, the lower bound is not positive, or the range is inverted.</summary>
    public readonly struct RenderScaleRange
    {
        public RenderScaleRange(float min, float max) { Min = min; Max = max; }
        public float Min { get; }
        public float Max { get; }
        public bool Contains(float value) => !float.IsNaN(value) && value >= Min && value <= Max;
        internal bool IsValid => IsFinite(Min) && IsFinite(Max) && Min > 0f && Min <= Max;
        internal bool Overlaps(RenderScaleRange other) => Min <= other.Max && other.Min <= Max;
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>A closed, non-negative range for a tier's shadow budget. Invalid when a bound is
    /// non-finite, negative, or the range is inverted.</summary>
    public readonly struct ShadowBudgetRange
    {
        public ShadowBudgetRange(float min, float max) { Min = min; Max = max; }
        public float Min { get; }
        public float Max { get; }
        public bool Contains(float value) => !float.IsNaN(value) && value >= Min && value <= Max;
        internal bool IsValid => IsFinite(Min) && IsFinite(Max) && Min >= 0f && Min <= Max;
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>A closed, non-negative range for a tier's post-processing budget. Invalid when a
    /// bound is non-finite, negative, or the range is inverted.</summary>
    public readonly struct PostProcessingBudgetRange
    {
        public PostProcessingBudgetRange(float min, float max) { Min = min; Max = max; }
        public float Min { get; }
        public float Max { get; }
        public bool Contains(float value) => !float.IsNaN(value) && value >= Min && value <= Max;
        internal bool IsValid => IsFinite(Min) && IsFinite(Max) && Min >= 0f && Min <= Max;
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>One declared quality tier: its refresh-rate, render-scale, shadow-budget, and
    /// post-processing-budget guard rails (each a closed range), its single closed
    /// <see cref="FoveationLevel"/> value, its single closed <see cref="MsaaSampleCount"/> value,
    /// and an open label that drives no validation or intent resolution. Constructed only through
    /// <see cref="TryCreate"/>, which fails closed with
    /// <see cref="QualityFailure.TierDeclarationInvalid"/> for an inverted or out-of-domain range,
    /// a foveation level outside the closed set, or an MSAA value outside the closed sample-count
    /// set.</summary>
    public sealed class QualityTierDefinition
    {
        private QualityTierDefinition(
            TierId id,
            RefreshRateRangeHz refreshRate,
            RenderScaleRange renderScale,
            FoveationLevel foveation,
            MsaaSampleCount msaa,
            ShadowBudgetRange shadowBudget,
            PostProcessingBudgetRange postProcessingBudget,
            string label)
        {
            Id = id;
            RefreshRate = refreshRate;
            RenderScale = renderScale;
            Foveation = foveation;
            Msaa = msaa;
            ShadowBudget = shadowBudget;
            PostProcessingBudget = postProcessingBudget;
            Label = label ?? string.Empty;
        }

        public TierId Id { get; }
        public RefreshRateRangeHz RefreshRate { get; }
        public RenderScaleRange RenderScale { get; }
        public FoveationLevel Foveation { get; }
        public MsaaSampleCount Msaa { get; }
        public ShadowBudgetRange ShadowBudget { get; }
        public PostProcessingBudgetRange PostProcessingBudget { get; }
        public string Label { get; }

        public static QualityResult<QualityTierDefinition> TryCreate(
            TierId id,
            RefreshRateRangeHz refreshRate,
            RenderScaleRange renderScale,
            FoveationLevel foveation,
            MsaaSampleCount msaa,
            ShadowBudgetRange shadowBudget,
            PostProcessingBudgetRange postProcessingBudget,
            string label = "")
        {
            if (!refreshRate.IsValid)
            {
                return QualityResult<QualityTierDefinition>.Fail(QualityFailure.TierDeclarationInvalid, $"Tier '{id}': refresh-rate range [{refreshRate.Min},{refreshRate.Max}] Hz must be positive and not inverted.");
            }
            if (!renderScale.IsValid)
            {
                return QualityResult<QualityTierDefinition>.Fail(QualityFailure.TierDeclarationInvalid, $"Tier '{id}': render-scale range [{renderScale.Min},{renderScale.Max}] must be positive and not inverted.");
            }
            if (!Enum.IsDefined(typeof(FoveationLevel), foveation))
            {
                return QualityResult<QualityTierDefinition>.Fail(QualityFailure.TierDeclarationInvalid, $"Tier '{id}': foveation level '{foveation}' is outside the closed set (off, low, medium, high).");
            }
            if (!Enum.IsDefined(typeof(MsaaSampleCount), msaa))
            {
                return QualityResult<QualityTierDefinition>.Fail(QualityFailure.TierDeclarationInvalid, $"Tier '{id}': MSAA sample count '{msaa}' is outside the closed sample-count set (1, 2, 4, 8).");
            }
            if (!shadowBudget.IsValid)
            {
                return QualityResult<QualityTierDefinition>.Fail(QualityFailure.TierDeclarationInvalid, $"Tier '{id}': shadow budget range [{shadowBudget.Min},{shadowBudget.Max}] must be non-negative and not inverted.");
            }
            if (!postProcessingBudget.IsValid)
            {
                return QualityResult<QualityTierDefinition>.Fail(QualityFailure.TierDeclarationInvalid, $"Tier '{id}': post-processing budget range [{postProcessingBudget.Min},{postProcessingBudget.Max}] must be non-negative and not inverted.");
            }
            return QualityResult<QualityTierDefinition>.Ok(new QualityTierDefinition(id, refreshRate, renderScale, foveation, msaa, shadowBudget, postProcessingBudget, label));
        }

        /// <summary>True when <paramref name="value"/> lies within this tier's guard rail for
        /// <paramref name="field"/>. <see cref="QualityOverrideField.FoveationLevel"/> and
        /// <see cref="QualityOverrideField.Msaa"/> have a single declared value rather than a
        /// range, so their guard rail is the degenerate interval [value, value]: only the tier's
        /// own declared foveation level or MSAA value passes.</summary>
        public bool GuardRailContains(QualityOverrideField field, float value)
        {
            switch (field)
            {
                case QualityOverrideField.RefreshRate: return RefreshRate.Contains(value);
                case QualityOverrideField.RenderScale: return RenderScale.Contains(value);
                case QualityOverrideField.ShadowBudget: return ShadowBudget.Contains(value);
                case QualityOverrideField.PostProcessingBudget: return PostProcessingBudget.Contains(value);
                case QualityOverrideField.FoveationLevel: return Math.Abs(value - (float)Foveation) < float.Epsilon;
                case QualityOverrideField.Msaa: return Math.Abs(value - (float)(int)Msaa) < float.Epsilon;
                default: return false;
            }
        }

        internal string Fingerprint() =>
            $"{Id}:{Number(RefreshRate.Min)},{Number(RefreshRate.Max)}" +
            $":{Number(RenderScale.Min)},{Number(RenderScale.Max)}" +
            $":{Foveation}:{(int)Msaa}" +
            $":{Number(ShadowBudget.Min)},{Number(ShadowBudget.Max)}" +
            $":{Number(PostProcessingBudget.Min)},{Number(PostProcessingBudget.Max)}" +
            $":{Label}";

        private static string Number(float value) => value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
    }
}
