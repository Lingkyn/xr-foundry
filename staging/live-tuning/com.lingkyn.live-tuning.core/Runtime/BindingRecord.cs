using System;
using System.Collections.Generic;
using System.Linq;

namespace Lingkyn.LiveTuning.Core
{
    // Engine-free binding records: a TunableId, its kind, an opaque target path the adapter
    // resolves (never interpreted here), and open scope metadata for filtering. Validating a
    // set of records against a registry is the only way to obtain a BindingSet.

    /// <summary>One binding: a tunable id, its declared kind, an opaque target path, and open
    /// scope metadata (package, skin, or screen labels) used only for filtering.</summary>
    public sealed class BindingRecord
    {
        private static readonly IReadOnlyDictionary<string, string> EmptyScope = new Dictionary<string, string>();

        public BindingRecord(TunableId id, TunableKind kind, string targetPath, IReadOnlyDictionary<string, string> scope = null)
        {
            Id = id;
            Kind = kind;
            TargetPath = targetPath ?? string.Empty;
            Scope = scope ?? EmptyScope;
        }

        public TunableId Id { get; }
        public TunableKind Kind { get; }
        /// <summary>An opaque locator the adapter resolves. Never interpreted here.</summary>
        public string TargetPath { get; }
        public IReadOnlyDictionary<string, string> Scope { get; }

        public bool TryGetScope(string key, out string value) => Scope.TryGetValue(key, out value);
    }

    /// <summary>The immutable, validated set of binding records for one registry. Enumerates in
    /// canonical id order; a filter changes which records are returned and never changes which
    /// editor kind any of them resolves to.</summary>
    public sealed class BindingSet
    {
        private readonly SortedDictionary<TunableId, BindingRecord> _records;

        private BindingSet(SortedDictionary<TunableId, BindingRecord> records)
        {
            _records = records;
        }

        public int Count => _records.Count;

        /// <summary>Every validated record, in canonical id order.</summary>
        public IEnumerable<BindingRecord> Records => _records.Values;

        public bool TryGet(TunableId id, out BindingRecord record) => _records.TryGetValue(id, out record);

        /// <summary>Every record whose scope carries <paramref name="value"/> for <paramref name="key"/>.
        /// Metadata-only: it never changes which editor kind a returned record resolves to.</summary>
        public IEnumerable<BindingRecord> WhereScope(string key, string value) =>
            Records.Where(record => record.TryGetScope(key, out var found) && string.Equals(found, value, StringComparison.Ordinal));

        /// <summary>Validates a list of binding records against a registry: an id that is not
        /// registered is rejected with tunable.unknown, a kind that differs from the registered
        /// kind with binding.kind.mismatch, and a second record for an id already seen with
        /// binding.duplicate. The first invalid record stops validation and leaves no set.</summary>
        public static LiveTuningResult<BindingSet> Validate(IEnumerable<BindingRecord> records, TunableRegistry registry)
        {
            if (records == null) throw new ArgumentNullException(nameof(records));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            var map = new SortedDictionary<TunableId, BindingRecord>();
            foreach (var record in records)
            {
                if (record == null) throw new ArgumentException("A binding record must not be null.", nameof(records));
                if (!registry.TryGet(record.Id, out var registration))
                {
                    return LiveTuningResult<BindingSet>.Fail(LiveTuningFailure.TunableUnknown, $"Binding names tunable '{record.Id}' which is not registered.");
                }
                if (registration.Kind != record.Kind)
                {
                    return LiveTuningResult<BindingSet>.Fail(LiveTuningFailure.BindingKindMismatch, $"Binding for '{record.Id}' declares kind {record.Kind} but the tunable is registered as {registration.Kind}.");
                }
                if (map.ContainsKey(record.Id))
                {
                    return LiveTuningResult<BindingSet>.Fail(LiveTuningFailure.BindingDuplicate, $"Tunable '{record.Id}' already has a binding record.");
                }
                map[record.Id] = record;
            }
            return LiveTuningResult<BindingSet>.Ok(new BindingSet(map));
        }
    }
}
