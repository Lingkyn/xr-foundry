using System.Collections.Generic;
using System.Text;

namespace Lingkyn.QualityTiers.Core
{
    // An immutable TierRegistry built only by explicit registration, never by reflection, an
    // attribute scan, or asset discovery (QC-03). Registration fails closed and leaves the
    // registry unchanged; a built registry enumerates in canonical id order and cannot be mutated
    // afterwards.

    /// <summary>The immutable, explicitly-registered closed set of declared quality tiers.</summary>
    public sealed class QualityTierRegistry
    {
        private readonly SortedDictionary<TierId, QualityTierDefinition> _entries;
        private readonly string _fingerprint;

        private QualityTierRegistry(SortedDictionary<TierId, QualityTierDefinition> entries)
        {
            _entries = entries;
            var builder = new StringBuilder();
            foreach (var definition in _entries.Values)
            {
                builder.Append(definition.Fingerprint()).Append(';');
            }
            _fingerprint = builder.ToString();
        }

        public int Count => _entries.Count;

        /// <summary>Every registered tier, in canonical id order.</summary>
        public IEnumerable<QualityTierDefinition> Tiers => _entries.Values;

        public bool TryGet(TierId id, out QualityTierDefinition definition) => _entries.TryGetValue(id, out definition);

        /// <summary>A canonical text covering every registration in canonical id order; equal
        /// registries (built from equal registrations, in any registration order) have equal
        /// fingerprints.</summary>
        public string Fingerprint() => _fingerprint;

        internal static QualityTierRegistry FromEntries(SortedDictionary<TierId, QualityTierDefinition> entries) => new QualityTierRegistry(entries);
    }

    /// <summary>Collects tier registrations one at a time; each call validates immediately and
    /// either extends the builder or leaves it unchanged and returns a stable failure code.</summary>
    public sealed class QualityTierRegistryBuilder
    {
        private readonly SortedDictionary<TierId, QualityTierDefinition> _entries = new SortedDictionary<TierId, QualityTierDefinition>();

        public int Count => _entries.Count;

        public bool Contains(TierId id) => _entries.ContainsKey(id);

        /// <summary>Registers one tier declaration. A rejected call leaves this builder exactly as
        /// it was: <see cref="QualityFailure.TierDeclarationInvalid"/> for an invalid declaration
        /// (checked first, by <see cref="QualityTierDefinition.TryCreate"/>), then
        /// <see cref="QualityFailure.TierDuplicate"/> for an id already registered.</summary>
        public QualityResult<QualityTierRegistryBuilder> Register(
            TierId id,
            RefreshRateRangeHz refreshRate,
            RenderScaleRange renderScale,
            FoveationLevel foveation,
            MsaaSampleCount msaa,
            ShadowBudgetRange shadowBudget,
            PostProcessingBudgetRange postProcessingBudget,
            string label = "")
        {
            var definition = QualityTierDefinition.TryCreate(id, refreshRate, renderScale, foveation, msaa, shadowBudget, postProcessingBudget, label);
            if (!definition.Succeeded)
            {
                return definition.As<QualityTierRegistryBuilder>();
            }
            if (_entries.ContainsKey(id))
            {
                return QualityResult<QualityTierRegistryBuilder>.Fail(QualityFailure.TierDuplicate, $"Quality tier '{id}' is already registered.");
            }
            _entries[id] = definition.Value;
            return QualityResult<QualityTierRegistryBuilder>.Ok(this);
        }

        /// <summary>Produces the immutable registry. Safe to call more than once; each call
        /// snapshots the current entries so a later successful <see cref="Register"/> never mutates
        /// a registry already handed out.</summary>
        public QualityTierRegistry Build() => QualityTierRegistry.FromEntries(new SortedDictionary<TierId, QualityTierDefinition>(_entries));
    }
}
