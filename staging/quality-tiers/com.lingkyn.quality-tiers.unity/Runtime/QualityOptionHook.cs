using Lingkyn.QualityTiers.Core;

namespace Lingkyn.QualityTiers.Unity
{
    // The one seam the Settings family's option value crosses into this family
    // (verification-contract.md, Unity adapter gate bullet 7): a pure function from a Settings-
    // owned option value to a select_tier intent, taking the current registry, capability
    // descriptor, and device profile id as explicit parameters. This assembly references no
    // Settings type: IQualityOptionSource and QualityOptionHook both take a plain string for the
    // option's current value, exactly as a Settings-owned option would hand it across the seam,
    // never a Settings type itself.

    /// <summary>The one seam a Settings-owned quality option crosses into this family: given the
    /// option's current raw value, reports which registered <see cref="TierId"/> it maps to, if
    /// any. A real implementation is authored per-consumer (the option's own value vocabulary is
    /// not this family's concern); a fixed fake reports whatever a test wires it to.</summary>
    public interface IQualityOptionSource
    {
        bool TryResolveTierId(string optionValue, out TierId tierId);
    }

    public sealed class FixedQualityOptionSource : IQualityOptionSource
    {
        private readonly System.Collections.Generic.IReadOnlyDictionary<string, TierId> _map;

        public FixedQualityOptionSource(System.Collections.Generic.IReadOnlyDictionary<string, TierId> map)
        {
            _map = map;
        }

        public bool TryResolveTierId(string optionValue, out TierId tierId) => _map.TryGetValue(optionValue ?? string.Empty, out tierId);
    }

    public static class QualityOptionHook
    {
        /// <summary>Converts a Settings-owned option's current raw value into a
        /// <see cref="SelectTierIntent"/> for <paramref name="deviceProfileId"/>. A pure function:
        /// it reads only its explicit parameters, calls no Settings API, and raises no intent
        /// itself — the caller still applies the returned intent through
        /// <see cref="QualityState.Apply"/>. Fails with the Core's own
        /// <see cref="QualityFailure.TierUnknown"/> when the option source names no registered
        /// tier for <paramref name="optionValue"/>.</summary>
        public static QualityResult<QualityIntent> FromOption(
            IQualityOptionSource optionSource,
            string optionValue,
            DeviceProfileId deviceProfileId,
            IntentActor actor = IntentActor.Player,
            long? expectedRevision = null)
        {
            if (optionSource == null) throw new System.ArgumentNullException(nameof(optionSource));
            if (!optionSource.TryResolveTierId(optionValue, out var tierId))
            {
                return QualityResult<QualityIntent>.Fail(QualityFailure.TierUnknown, $"Quality option value '{optionValue}' does not map to a registered tier.");
            }
            QualityIntent intent = new SelectTierIntent(deviceProfileId, tierId, actor, expectedRevision);
            return QualityResult<QualityIntent>.Ok(intent);
        }
    }
}
