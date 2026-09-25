using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lingkyn.QualityTiers.Core
{
    // A DeviceCapabilityDescriptor per device profile, built the same explicit way as a tier
    // declaration: a closed set of supported refresh rates, a supported render-scale range, a
    // closed set of supported foveation levels, and a closed set of supported MSAA sample counts
    // (QC-04). Shadow and post-processing budgets have no device-capability entry (QualityOverrideFieldNames.
    // IsCapabilityGated): a device never fails a tier or an override closed for either.

    /// <summary>One device profile's closed capability: which refresh rates, which render-scale
    /// range, which foveation levels, and which MSAA sample counts it supports. Constructed only
    /// through <see cref="TryCreate"/>, which fails closed with
    /// <see cref="QualityFailure.CapabilityDeclarationInvalid"/> for an empty supported set or an
    /// inverted render-scale range.</summary>
    public sealed class DeviceCapabilityDescriptor
    {
        private readonly HashSet<float> _supportedRefreshRates;
        private readonly HashSet<FoveationLevel> _supportedFoveationLevels;
        private readonly HashSet<MsaaSampleCount> _supportedMsaaSampleCounts;

        private DeviceCapabilityDescriptor(
            DeviceProfileId deviceProfileId,
            HashSet<float> supportedRefreshRates,
            RenderScaleRange supportedRenderScaleRange,
            HashSet<FoveationLevel> supportedFoveationLevels,
            HashSet<MsaaSampleCount> supportedMsaaSampleCounts)
        {
            DeviceProfileId = deviceProfileId;
            _supportedRefreshRates = supportedRefreshRates;
            SupportedRenderScaleRange = supportedRenderScaleRange;
            _supportedFoveationLevels = supportedFoveationLevels;
            _supportedMsaaSampleCounts = supportedMsaaSampleCounts;
        }

        public DeviceProfileId DeviceProfileId { get; }
        public RenderScaleRange SupportedRenderScaleRange { get; }
        public IReadOnlyCollection<float> SupportedRefreshRates => _supportedRefreshRates;
        public IReadOnlyCollection<FoveationLevel> SupportedFoveationLevels => _supportedFoveationLevels;
        public IReadOnlyCollection<MsaaSampleCount> SupportedMsaaSampleCounts => _supportedMsaaSampleCounts;

        public bool SupportsRefreshRate(float hz) => _supportedRefreshRates.Contains(hz);
        public bool SupportsFoveationLevel(FoveationLevel level) => _supportedFoveationLevels.Contains(level);
        public bool SupportsMsaa(MsaaSampleCount msaa) => _supportedMsaaSampleCounts.Contains(msaa);

        /// <summary>True when at least one supported refresh rate falls inside
        /// <paramref name="tier"/>'s own refresh-rate range, the tier's render-scale range overlaps
        /// this device's supported render-scale range, this device supports the tier's declared
        /// foveation level, and this device supports the tier's declared MSAA value. Used by
        /// <c>select_tier</c> and <c>apply_preset</c> to fail closed with
        /// <see cref="QualityFailure.CapabilityUnsupported"/> before any state change.</summary>
        public bool SupportsTier(QualityTierDefinition tier)
        {
            if (tier == null) throw new ArgumentNullException(nameof(tier));
            var refreshRateSupported = _supportedRefreshRates.Any(rate => tier.RefreshRate.Contains(rate));
            var renderScaleSupported = SupportedRenderScaleRange.Overlaps(tier.RenderScale);
            var foveationSupported = _supportedFoveationLevels.Contains(tier.Foveation);
            var msaaSupported = _supportedMsaaSampleCounts.Contains(tier.Msaa);
            return refreshRateSupported && renderScaleSupported && foveationSupported && msaaSupported;
        }

        /// <summary>True when this device's capability supports <paramref name="value"/> for a
        /// capability-gated override field (see <see cref="QualityOverrideFieldNames.IsCapabilityGated"/>).
        /// Refresh rate and MSAA require exact membership in the closed supported set; render scale
        /// requires containment in the supported range; foveation level requires exact membership
        /// after rounding <paramref name="value"/> to the nearest <see cref="FoveationLevel"/>.</summary>
        public bool SupportsOverrideValue(QualityOverrideField field, float value)
        {
            switch (field)
            {
                case QualityOverrideField.RefreshRate: return SupportsRefreshRate(value);
                case QualityOverrideField.RenderScale: return SupportedRenderScaleRange.Contains(value);
                case QualityOverrideField.FoveationLevel: return Enum.IsDefined(typeof(FoveationLevel), (int)value) && SupportsFoveationLevel((FoveationLevel)(int)value);
                case QualityOverrideField.Msaa: return Enum.IsDefined(typeof(MsaaSampleCount), (int)value) && SupportsMsaa((MsaaSampleCount)(int)value);
                default: return true;
            }
        }

        public static QualityResult<DeviceCapabilityDescriptor> TryCreate(
            DeviceProfileId deviceProfileId,
            IEnumerable<float> supportedRefreshRates,
            RenderScaleRange supportedRenderScaleRange,
            IEnumerable<FoveationLevel> supportedFoveationLevels,
            IEnumerable<MsaaSampleCount> supportedMsaaSampleCounts)
        {
            var refreshRates = new HashSet<float>(supportedRefreshRates ?? Enumerable.Empty<float>());
            var foveationLevels = new HashSet<FoveationLevel>(supportedFoveationLevels ?? Enumerable.Empty<FoveationLevel>());
            var msaaSampleCounts = new HashSet<MsaaSampleCount>(supportedMsaaSampleCounts ?? Enumerable.Empty<MsaaSampleCount>());

            if (refreshRates.Count == 0)
            {
                return QualityResult<DeviceCapabilityDescriptor>.Fail(QualityFailure.CapabilityDeclarationInvalid, $"Device '{deviceProfileId}': the supported refresh-rate set must not be empty.");
            }
            if (!supportedRenderScaleRange.IsValid)
            {
                return QualityResult<DeviceCapabilityDescriptor>.Fail(QualityFailure.CapabilityDeclarationInvalid, $"Device '{deviceProfileId}': supported render-scale range [{supportedRenderScaleRange.Min},{supportedRenderScaleRange.Max}] must be positive and not inverted.");
            }
            if (foveationLevels.Count == 0)
            {
                return QualityResult<DeviceCapabilityDescriptor>.Fail(QualityFailure.CapabilityDeclarationInvalid, $"Device '{deviceProfileId}': the supported foveation-level set must not be empty.");
            }
            if (msaaSampleCounts.Count == 0)
            {
                return QualityResult<DeviceCapabilityDescriptor>.Fail(QualityFailure.CapabilityDeclarationInvalid, $"Device '{deviceProfileId}': the supported MSAA sample-count set must not be empty.");
            }
            return QualityResult<DeviceCapabilityDescriptor>.Ok(new DeviceCapabilityDescriptor(deviceProfileId, refreshRates, supportedRenderScaleRange, foveationLevels, msaaSampleCounts));
        }

        internal string Fingerprint()
        {
            var rates = string.Join(",", _supportedRefreshRates.OrderBy(r => r).Select(r => r.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
            var foveations = string.Join(",", _supportedFoveationLevels.OrderBy(f => f));
            var msaas = string.Join(",", _supportedMsaaSampleCounts.OrderBy(m => (int)m));
            return $"{DeviceProfileId}:[{rates}]:[{SupportedRenderScaleRange.Min},{SupportedRenderScaleRange.Max}]:[{foveations}]:[{msaas}]";
        }
    }

    /// <summary>The explicit, immutable set of per-device capability descriptors a
    /// <see cref="QualityState"/> is built over. Built by explicit registration; registering a
    /// device profile id already present is rejected with the descriptor's own
    /// <see cref="QualityFailure.CapabilityDeclarationInvalid"/> code (QC-04), the same code an
    /// invalid individual descriptor uses.</summary>
    public sealed class DeviceCapabilitySet
    {
        private readonly SortedDictionary<DeviceProfileId, DeviceCapabilityDescriptor> _descriptors;

        private DeviceCapabilitySet(SortedDictionary<DeviceProfileId, DeviceCapabilityDescriptor> descriptors)
        {
            _descriptors = descriptors;
        }

        public int Count => _descriptors.Count;
        public IEnumerable<DeviceCapabilityDescriptor> Descriptors => _descriptors.Values;
        public bool TryGet(DeviceProfileId deviceProfileId, out DeviceCapabilityDescriptor descriptor) => _descriptors.TryGetValue(deviceProfileId, out descriptor);

        internal static DeviceCapabilitySet FromEntries(SortedDictionary<DeviceProfileId, DeviceCapabilityDescriptor> descriptors) => new DeviceCapabilitySet(descriptors);

        internal string Fingerprint()
        {
            var builder = new StringBuilder();
            foreach (var descriptor in _descriptors.Values)
            {
                builder.Append(descriptor.Fingerprint()).Append(';');
            }
            return builder.ToString();
        }
    }

    /// <summary>Collects device capability descriptors one at a time; each call validates
    /// immediately and either extends the builder or leaves it unchanged and returns a stable
    /// failure code.</summary>
    public sealed class DeviceCapabilitySetBuilder
    {
        private readonly SortedDictionary<DeviceProfileId, DeviceCapabilityDescriptor> _descriptors = new SortedDictionary<DeviceProfileId, DeviceCapabilityDescriptor>();

        public int Count => _descriptors.Count;

        public bool Contains(DeviceProfileId deviceProfileId) => _descriptors.ContainsKey(deviceProfileId);

        public QualityResult<DeviceCapabilitySetBuilder> Register(DeviceCapabilityDescriptor descriptor)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            if (_descriptors.ContainsKey(descriptor.DeviceProfileId))
            {
                return QualityResult<DeviceCapabilitySetBuilder>.Fail(QualityFailure.CapabilityDeclarationInvalid, $"Device profile '{descriptor.DeviceProfileId}' is already registered in this capability set.");
            }
            _descriptors[descriptor.DeviceProfileId] = descriptor;
            return QualityResult<DeviceCapabilitySetBuilder>.Ok(this);
        }

        public DeviceCapabilitySet Build() => DeviceCapabilitySet.FromEntries(new SortedDictionary<DeviceProfileId, DeviceCapabilityDescriptor>(_descriptors));
    }
}
