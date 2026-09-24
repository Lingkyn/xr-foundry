using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Lingkyn.Haptics.Core
{
    // An immutable HapticEventRegistry built only by explicit registration, never by reflection,
    // an attribute scan, or asset discovery. Registration fails closed and leaves the registry
    // unchanged; a built registry enumerates in canonical id order and cannot be mutated afterwards.

    /// <summary>The immutable, explicitly-registered set of haptic events.</summary>
    public sealed class HapticEventRegistry
    {
        private readonly SortedDictionary<HapticEventId, HapticEventDefinition> _entries;
        private readonly string _fingerprint;

        private HapticEventRegistry(SortedDictionary<HapticEventId, HapticEventDefinition> entries)
        {
            _entries = entries;
            var builder = new StringBuilder();
            foreach (var definition in _entries.Values)
            {
                builder.Append(FingerprintOf(definition)).Append(';');
            }
            _fingerprint = builder.ToString();
        }

        public int Count => _entries.Count;

        /// <summary>Every registered event, in canonical id order.</summary>
        public IEnumerable<HapticEventDefinition> Events => _entries.Values;

        public bool TryGet(HapticEventId id, out HapticEventDefinition definition) => _entries.TryGetValue(id, out definition);

        /// <summary>A canonical text covering every registration in canonical id order; equal
        /// registries (built from equal registrations, in any registration order) have equal
        /// fingerprints.</summary>
        public string Fingerprint() => _fingerprint;

        private static string FingerprintOf(HapticEventDefinition definition) =>
            $"{definition.Id}:{definition.Kind}:{Number(definition.Amplitude.Min)},{Number(definition.Amplitude.Max)}:{Number(definition.DefaultAmplitude)}" +
            $":{Number(definition.Duration.Min)},{Number(definition.Duration.Max)}:{Number(definition.DefaultDurationMs)}" +
            $":{(definition.Frequency.HasValue ? $"{Number(definition.Frequency.Value.Min)},{Number(definition.Frequency.Value.Max)}:{Number(definition.DefaultFrequencyHz.Value)}" : "-")}" +
            $":{definition.Label}";

        private static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);

        internal static HapticEventRegistry FromEntries(SortedDictionary<HapticEventId, HapticEventDefinition> entries) => new HapticEventRegistry(entries);
    }

    /// <summary>Collects event registrations one at a time; each call validates immediately and
    /// either extends the builder or leaves it unchanged and returns a stable failure code.</summary>
    public sealed class HapticEventRegistryBuilder
    {
        private readonly SortedDictionary<HapticEventId, HapticEventDefinition> _entries = new SortedDictionary<HapticEventId, HapticEventDefinition>();

        public int Count => _entries.Count;

        public bool Contains(HapticEventId id) => _entries.ContainsKey(id);

        /// <summary>Registers one haptic event. A rejected call leaves this builder exactly as it
        /// was: <see cref="HapticFailure.KindDeclarationInvalid"/> for an invalid declaration
        /// (checked first, by <see cref="HapticEventDefinition.TryCreate"/>), then
        /// <see cref="HapticFailure.EventDuplicate"/> for an id already registered.</summary>
        public HapticResult<HapticEventRegistryBuilder> Register(
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
            var definition = HapticEventDefinition.TryCreate(id, kind, amplitude, defaultAmplitude, duration, defaultDurationMs, frequency, defaultFrequencyHz, label);
            if (!definition.Succeeded)
            {
                return definition.As<HapticEventRegistryBuilder>();
            }
            if (_entries.ContainsKey(id))
            {
                return HapticResult<HapticEventRegistryBuilder>.Fail(HapticFailure.EventDuplicate, $"Haptic event '{id}' is already registered.");
            }
            _entries[id] = definition.Value;
            return HapticResult<HapticEventRegistryBuilder>.Ok(this);
        }

        /// <summary>Produces the immutable registry. Safe to call more than once; each call
        /// snapshots the current entries so a later successful <see cref="Register"/> never mutates
        /// a registry already handed out.</summary>
        public HapticEventRegistry Build() => HapticEventRegistry.FromEntries(new SortedDictionary<HapticEventId, HapticEventDefinition>(_entries));
    }
}
