using System;

namespace Lingkyn.Haptics.Core
{
    // The closed haptic-kind set (transient, continuous, envelope), the closed per-kind guard
    // rails (amplitude within [0,1], duration as a positive range in milliseconds, and an
    // optional frequency range in hertz only for a kind that uses it), and the declared default
    // amplitude/duration/frequency an intent falls back to when it supplies no override.

    /// <summary>The closed set of haptic kinds. Adding a member here is a breaking change; until
    /// then, a kind value outside these three is not representable.</summary>
    public enum HapticKind
    {
        Transient,
        Continuous,
        Envelope,
    }

    /// <summary>A closed sub-range of <c>[0, 1]</c> for an event's amplitude. Invalid when either
    /// bound lies outside <c>[0, 1]</c> or the range is inverted.</summary>
    public readonly struct AmplitudeRange
    {
        public AmplitudeRange(float min, float max) { Min = min; Max = max; }

        public float Min { get; }
        public float Max { get; }

        public bool Contains(float value) => !float.IsNaN(value) && value >= Min && value <= Max;

        internal bool IsValid =>
            !float.IsNaN(Min) && !float.IsNaN(Max) && Min >= 0f && Max <= 1f && Min <= Max;
    }

    /// <summary>A closed, positive range in milliseconds for an event's duration. Invalid when a
    /// bound is non-finite, the lower bound is not positive, or the range is inverted.</summary>
    public readonly struct DurationRangeMs
    {
        public DurationRangeMs(float min, float max) { Min = min; Max = max; }

        public float Min { get; }
        public float Max { get; }

        public bool Contains(float value) => !float.IsNaN(value) && value >= Min && value <= Max;

        internal bool IsValid => IsFinite(Min) && IsFinite(Max) && Min > 0f && Min <= Max;

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>A closed, positive range in hertz for an event's frequency. Only meaningful for a
    /// kind that uses frequency (<see cref="HapticEventDefinition.KindUsesFrequency"/>); a
    /// declaration for a kind outside that subset is invalid.</summary>
    public readonly struct FrequencyRangeHz
    {
        public FrequencyRangeHz(float min, float max) { Min = min; Max = max; }

        public float Min { get; }
        public float Max { get; }

        public bool Contains(float value) => !float.IsNaN(value) && value >= Min && value <= Max;

        internal bool IsValid => IsFinite(Min) && IsFinite(Max) && Min > 0f && Min <= Max;

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>One declared haptic event: its kind, its amplitude/duration/optional-frequency
    /// guard rails, the default value an intent falls back to when it supplies no override for
    /// each, and an open label. Constructed only through <see cref="TryCreate"/>, which fails
    /// closed with <see cref="HapticFailure.KindDeclarationInvalid"/> for an inverted range, a
    /// non-positive duration bound, an amplitude bound outside <c>[0, 1]</c>, a frequency range on
    /// a kind that does not use frequency, or a default outside its own guard rail.</summary>
    public sealed class HapticEventDefinition
    {
        /// <summary>The closed subset of kinds a frequency guard rail may be declared for.
        /// <see cref="HapticKind.Transient"/> is excluded: a transient impulse has no frequency.</summary>
        public static bool KindUsesFrequency(HapticKind kind) => kind == HapticKind.Continuous || kind == HapticKind.Envelope;

        private HapticEventDefinition(
            HapticEventId id,
            HapticKind kind,
            AmplitudeRange amplitude,
            float defaultAmplitude,
            DurationRangeMs duration,
            float defaultDurationMs,
            FrequencyRangeHz? frequency,
            float? defaultFrequencyHz,
            string label)
        {
            Id = id;
            Kind = kind;
            Amplitude = amplitude;
            DefaultAmplitude = defaultAmplitude;
            Duration = duration;
            DefaultDurationMs = defaultDurationMs;
            Frequency = frequency;
            DefaultFrequencyHz = defaultFrequencyHz;
            Label = label ?? string.Empty;
        }

        public HapticEventId Id { get; }
        public HapticKind Kind { get; }
        public AmplitudeRange Amplitude { get; }
        /// <summary>The amplitude an intent uses when it supplies no override.</summary>
        public float DefaultAmplitude { get; }
        public DurationRangeMs Duration { get; }
        /// <summary>The duration an intent uses when it supplies no override.</summary>
        public float DefaultDurationMs { get; }
        /// <summary>Null when <see cref="Kind"/> does not use frequency.</summary>
        public FrequencyRangeHz? Frequency { get; }
        /// <summary>Null exactly when <see cref="Frequency"/> is null.</summary>
        public float? DefaultFrequencyHz { get; }
        public string Label { get; }

        public static HapticResult<HapticEventDefinition> TryCreate(
            HapticEventId id,
            HapticKind kind,
            AmplitudeRange amplitude,
            float defaultAmplitude,
            DurationRangeMs duration,
            float defaultDurationMs,
            FrequencyRangeHz? frequency = null,
            float? defaultFrequencyHz = null,
            string label = "")
        {
            if (!amplitude.IsValid)
            {
                return HapticResult<HapticEventDefinition>.Fail(HapticFailure.KindDeclarationInvalid, $"Event '{id}': amplitude range [{amplitude.Min},{amplitude.Max}] must lie within [0,1] and not be inverted.");
            }
            if (!duration.IsValid)
            {
                return HapticResult<HapticEventDefinition>.Fail(HapticFailure.KindDeclarationInvalid, $"Event '{id}': duration range [{duration.Min},{duration.Max}] ms must be positive and not inverted.");
            }
            if (frequency.HasValue)
            {
                if (!KindUsesFrequency(kind))
                {
                    return HapticResult<HapticEventDefinition>.Fail(HapticFailure.KindDeclarationInvalid, $"Event '{id}': kind {kind} does not use a frequency guard rail.");
                }
                if (!frequency.Value.IsValid)
                {
                    return HapticResult<HapticEventDefinition>.Fail(HapticFailure.KindDeclarationInvalid, $"Event '{id}': frequency range [{frequency.Value.Min},{frequency.Value.Max}] Hz must be positive and not inverted.");
                }
                if (!defaultFrequencyHz.HasValue || !frequency.Value.Contains(defaultFrequencyHz.Value))
                {
                    return HapticResult<HapticEventDefinition>.Fail(HapticFailure.KindDeclarationInvalid, $"Event '{id}': default frequency must be supplied and lie within [{frequency.Value.Min},{frequency.Value.Max}] Hz.");
                }
            }
            else if (defaultFrequencyHz.HasValue)
            {
                return HapticResult<HapticEventDefinition>.Fail(HapticFailure.KindDeclarationInvalid, $"Event '{id}': a default frequency was supplied but no frequency guard rail was declared.");
            }
            if (!amplitude.Contains(defaultAmplitude))
            {
                return HapticResult<HapticEventDefinition>.Fail(HapticFailure.KindDeclarationInvalid, $"Event '{id}': default amplitude {defaultAmplitude} lies outside [{amplitude.Min},{amplitude.Max}].");
            }
            if (!duration.Contains(defaultDurationMs))
            {
                return HapticResult<HapticEventDefinition>.Fail(HapticFailure.KindDeclarationInvalid, $"Event '{id}': default duration {defaultDurationMs} ms lies outside [{duration.Min},{duration.Max}].");
            }
            return HapticResult<HapticEventDefinition>.Ok(new HapticEventDefinition(id, kind, amplitude, defaultAmplitude, duration, defaultDurationMs, frequency, defaultFrequencyHz, label));
        }
    }
}
