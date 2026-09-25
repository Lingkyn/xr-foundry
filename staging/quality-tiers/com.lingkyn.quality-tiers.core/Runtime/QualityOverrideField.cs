namespace Lingkyn.QualityTiers.Core
{
    // The closed override-field set set_override draws its field name from. A field name outside
    // this set is rejected with value.out_of_range naming the field (QC-05), distinct from a value
    // inside the set but outside its guard rail, which the same code also names.

    /// <summary>The closed set of fields <c>set_override</c> may name. Adding a member here is a
    /// breaking change.</summary>
    public enum QualityOverrideField
    {
        RefreshRate,
        RenderScale,
        FoveationLevel,
        Msaa,
        ShadowBudget,
        PostProcessingBudget,
    }

    public static class QualityOverrideFieldNames
    {
        public const string RefreshRate = "refresh_rate";
        public const string RenderScale = "render_scale";
        public const string FoveationLevel = "foveation_level";
        public const string Msaa = "msaa";
        public const string ShadowBudget = "shadow_budget";
        public const string PostProcessingBudget = "post_processing_budget";

        /// <summary>Parses a raw field name against the closed set. False (never a throw) for a
        /// name outside the set; the caller reports <see cref="QualityFailure.ValueOutOfRange"/>
        /// naming the field, per the contract's closed override-field-set clause (QC-05).</summary>
        public static bool TryParse(string rawFieldName, out QualityOverrideField field)
        {
            switch (rawFieldName)
            {
                case RefreshRate: field = QualityOverrideField.RefreshRate; return true;
                case RenderScale: field = QualityOverrideField.RenderScale; return true;
                case FoveationLevel: field = QualityOverrideField.FoveationLevel; return true;
                case Msaa: field = QualityOverrideField.Msaa; return true;
                case ShadowBudget: field = QualityOverrideField.ShadowBudget; return true;
                case PostProcessingBudget: field = QualityOverrideField.PostProcessingBudget; return true;
                default:
                    field = default;
                    return false;
            }
        }

        public static string ToRawName(QualityOverrideField field)
        {
            switch (field)
            {
                case QualityOverrideField.RefreshRate: return RefreshRate;
                case QualityOverrideField.RenderScale: return RenderScale;
                case QualityOverrideField.FoveationLevel: return FoveationLevel;
                case QualityOverrideField.Msaa: return Msaa;
                case QualityOverrideField.ShadowBudget: return ShadowBudget;
                case QualityOverrideField.PostProcessingBudget: return PostProcessingBudget;
                default: return string.Empty;
            }
        }

        /// <summary>True when the device capability descriptor's closed sets cover
        /// <paramref name="field"/> (refresh rate, render scale, foveation level, MSAA). Shadow and
        /// post-processing budgets have no device-capability entry, so only their tier guard rail
        /// gates a <c>set_override</c> or a <c>select_tier</c>/<c>apply_preset</c> for them.</summary>
        public static bool IsCapabilityGated(QualityOverrideField field) =>
            field == QualityOverrideField.RefreshRate
            || field == QualityOverrideField.RenderScale
            || field == QualityOverrideField.FoveationLevel
            || field == QualityOverrideField.Msaa;
    }
}
