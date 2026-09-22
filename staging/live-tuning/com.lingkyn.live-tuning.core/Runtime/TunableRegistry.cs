using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Lingkyn.LiveTuning.Core
{
    // An immutable TunableRegistry built only by explicit registration, from any reflection,
    // attribute scan, or field discovery. Registration fails closed and leaves the registry
    // unchanged; a built registry cannot be mutated afterwards.

    /// <summary>One registered tunable: its kind declaration, default, and open group/label
    /// metadata. Never constructed directly; only <see cref="TunableRegistryBuilder.Register"/>
    /// produces one, after validation.</summary>
    public sealed class TunableRegistration
    {
        internal TunableRegistration(TunableId id, TunableKindDeclaration declaration, TunableValue defaultValue, string group, string label)
        {
            Id = id;
            Declaration = declaration;
            Default = defaultValue;
            Group = group ?? string.Empty;
            Label = label ?? string.Empty;
        }

        public TunableId Id { get; }
        public TunableKindDeclaration Declaration { get; }
        public TunableKind Kind => Declaration.Kind;
        public TunableValue Default { get; }
        public string Group { get; }
        public string Label { get; }

        internal string Fingerprint() =>
            $"{Id}:{Kind}:{Declaration.Summarize()}:{Default.ToCanonicalString()}:{Group}:{Label}";
    }

    /// <summary>The immutable, explicitly-registered set of tunables. Enumerates in canonical id
    /// order and exposes no mutator.</summary>
    public sealed class TunableRegistry
    {
        private readonly SortedDictionary<TunableId, TunableRegistration> _entries;
        private readonly string _fingerprint;

        private TunableRegistry(SortedDictionary<TunableId, TunableRegistration> entries)
        {
            _entries = entries;
            var builder = new StringBuilder();
            foreach (var registration in _entries.Values)
            {
                builder.Append(registration.Fingerprint()).Append(';');
            }
            _fingerprint = builder.ToString();
        }

        public int Count => _entries.Count;

        /// <summary>Every registered tunable, in canonical id order.</summary>
        public IEnumerable<TunableRegistration> Tunables => _entries.Values;

        public bool TryGet(TunableId id, out TunableRegistration registration) => _entries.TryGetValue(id, out registration);

        /// <summary>A canonical text covering every registration in canonical id order; equal
        /// registries (built from equal registrations, in any registration order) have equal
        /// fingerprints.</summary>
        public string Fingerprint() => _fingerprint;

        internal static TunableRegistry FromEntries(SortedDictionary<TunableId, TunableRegistration> entries) => new TunableRegistry(entries);
    }

    /// <summary>Collects registrations one at a time; each call validates immediately and either
    /// extends the builder or leaves it unchanged and returns a stable failure code.</summary>
    public sealed class TunableRegistryBuilder
    {
        private readonly SortedDictionary<TunableId, TunableRegistration> _entries = new SortedDictionary<TunableId, TunableRegistration>();

        public int Count => _entries.Count;

        public bool Contains(TunableId id) => _entries.ContainsKey(id);

        /// <summary>Registers one tunable. A rejected call leaves this builder exactly as it was:
        /// kind.declaration.invalid for an invalid declaration, tunable.duplicate for an id already
        /// registered, default.out_of_range for a default outside the declaration's range or value
        /// set (checked in that order).</summary>
        public LiveTuningResult<TunableRegistryBuilder> Register(TunableId id, TunableKindDeclaration declaration, TunableValue defaultValue, string group = "", string label = "")
        {
            if (declaration == null) throw new ArgumentNullException(nameof(declaration));
            if (!declaration.IsValid(out var invalidMessage))
            {
                return LiveTuningResult<TunableRegistryBuilder>.Fail(LiveTuningFailure.KindDeclarationInvalid, $"Tunable '{id}': {invalidMessage}");
            }
            if (_entries.ContainsKey(id))
            {
                return LiveTuningResult<TunableRegistryBuilder>.Fail(LiveTuningFailure.TunableDuplicate, $"Tunable '{id}' is already registered.");
            }
            if (!declaration.Contains(defaultValue))
            {
                return LiveTuningResult<TunableRegistryBuilder>.Fail(LiveTuningFailure.DefaultOutOfRange, $"Tunable '{id}': default value is outside its declared range or value set.");
            }
            _entries[id] = new TunableRegistration(id, declaration, defaultValue, group, label);
            return LiveTuningResult<TunableRegistryBuilder>.Ok(this);
        }

        /// <summary>Produces the immutable registry. Safe to call more than once; each call
        /// snapshots the current entries so a later successful <see cref="Register"/> never
        /// mutates a registry already handed out.</summary>
        public TunableRegistry Build() => TunableRegistry.FromEntries(new SortedDictionary<TunableId, TunableRegistration>(_entries));
    }
}
