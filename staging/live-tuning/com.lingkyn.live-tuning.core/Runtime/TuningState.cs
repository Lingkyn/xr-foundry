using System;
using System.Collections.Generic;
using System.Text;

namespace Lingkyn.LiveTuning.Core
{
    // Tuning intents (set, reset, reset_all, snapshot, apply_snapshot) applied to an immutable
    // TuningState whose overrides are the only diff from the registry's defaults, from any
    // control, panel, or input that raises them. Export is a pure read of a state, not an
    // intent that can fail, and lives at the bottom of this file next to TuningState.

    /// <summary>Closed set of intents applied to a <see cref="TuningState"/>.</summary>
    public abstract class TuningIntent
    {
        private protected TuningIntent() { }

        public abstract string Describe();

        internal abstract LiveTuningResult<TuningState> ApplyTo(TuningState state);

        public override string ToString() => Describe();
    }

    public sealed class SetIntent : TuningIntent
    {
        public SetIntent(TunableId id, TunableValue value) { Id = id; Value = value; }

        public TunableId Id { get; }
        public TunableValue Value { get; }

        public override string Describe() => $"set {Id} = {Value.ToCanonicalString()}";

        internal override LiveTuningResult<TuningState> ApplyTo(TuningState state) => state.ApplySet(Id, Value);
    }

    public sealed class ResetIntent : TuningIntent
    {
        public ResetIntent(TunableId id) { Id = id; }

        public TunableId Id { get; }

        public override string Describe() => $"reset {Id}";

        internal override LiveTuningResult<TuningState> ApplyTo(TuningState state) => state.ApplyReset(Id);
    }

    public sealed class ResetAllIntent : TuningIntent
    {
        public override string Describe() => "reset_all";

        internal override LiveTuningResult<TuningState> ApplyTo(TuningState state) => state.ApplyResetAll();
    }

    public sealed class SnapshotIntent : TuningIntent
    {
        public SnapshotIntent(SnapshotId id) { Id = id; }

        public SnapshotId Id { get; }

        public override string Describe() => $"snapshot {Id}";

        internal override LiveTuningResult<TuningState> ApplyTo(TuningState state) => state.ApplySnapshot(Id);
    }

    public sealed class ApplySnapshotIntent : TuningIntent
    {
        public ApplySnapshotIntent(SnapshotId id) { Id = id; }

        public SnapshotId Id { get; }

        public override string Describe() => $"apply_snapshot {Id}";

        internal override LiveTuningResult<TuningState> ApplyTo(TuningState state) => state.ApplyApplySnapshot(Id);
    }

    /// <summary>The outcome of one intent inside a sequence.</summary>
    public sealed class TuningIntentOutcome
    {
        public TuningIntentOutcome(int index, TuningIntent intent, bool accepted, string code, string message)
        {
            Index = index;
            Intent = intent;
            Accepted = accepted;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public int Index { get; }
        public TuningIntent Intent { get; }
        public bool Accepted { get; }
        public string Code { get; }
        public string Message { get; }
    }

    /// <summary>The state after a sequence and one outcome per intent. A rejected intent keeps the
    /// state it found; the next intent in the sequence still applies to that same state.</summary>
    public sealed class TuningSequenceResult
    {
        public TuningSequenceResult(TuningState state, IReadOnlyList<TuningIntentOutcome> outcomes)
        {
            State = state;
            Outcomes = outcomes;
        }

        public TuningState State { get; }
        public IReadOnlyList<TuningIntentOutcome> Outcomes { get; }

        public bool AllAccepted
        {
            get
            {
                foreach (var outcome in Outcomes)
                {
                    if (!outcome.Accepted) return false;
                }
                return true;
            }
        }

        public int AcceptedCount
        {
            get
            {
                var count = 0;
                foreach (var outcome in Outcomes)
                {
                    if (outcome.Accepted) count++;
                }
                return count;
            }
        }

        public int RejectedCount => Outcomes.Count - AcceptedCount;
    }

    /// <summary>
    /// Immutable tuning state over one registry: the overrides that differ from the registry's
    /// defaults, and the named snapshots taken from those overrides. Every accepted intent
    /// returns a new state; the prior state stays intact and readable. The same intent sequence
    /// over the same registry and initial state always produces an equal final state and an
    /// equal fingerprint.
    /// </summary>
    public sealed class TuningState : IEquatable<TuningState>
    {
        private readonly SortedDictionary<TunableId, TunableValue> _overrides;
        private readonly SortedDictionary<SnapshotId, SortedDictionary<TunableId, TunableValue>> _snapshots;

        private TuningState(
            TunableRegistry registry,
            SortedDictionary<TunableId, TunableValue> overrides,
            SortedDictionary<SnapshotId, SortedDictionary<TunableId, TunableValue>> snapshots)
        {
            Registry = registry;
            _overrides = overrides;
            _snapshots = snapshots;
        }

        public TunableRegistry Registry { get; }

        /// <summary>The current overrides, in canonical id order. This and the registry's
        /// defaults are the only two sources of an effective value.</summary>
        public IReadOnlyDictionary<TunableId, TunableValue> Overrides => new SortedDictionary<TunableId, TunableValue>(_overrides);

        public IReadOnlyList<SnapshotId> SnapshotIds => new List<SnapshotId>(_snapshots.Keys);

        public static TuningState Initial(TunableRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            return new TuningState(registry, new SortedDictionary<TunableId, TunableValue>(), new SortedDictionary<SnapshotId, SortedDictionary<TunableId, TunableValue>>());
        }

        /// <summary>The effective value of a registered tunable: its override when present, its
        /// registered default otherwise. False when the id is not registered.</summary>
        public bool TryGetEffective(TunableId id, out TunableValue value)
        {
            if (!Registry.TryGet(id, out var registration))
            {
                value = default;
                return false;
            }
            value = _overrides.TryGetValue(id, out var overriddenValue) ? overriddenValue : registration.Default;
            return true;
        }

        public bool HasOverride(TunableId id) => _overrides.ContainsKey(id);

        public bool TryGetSnapshot(SnapshotId id, out IReadOnlyDictionary<TunableId, TunableValue> overrides)
        {
            if (!_snapshots.TryGetValue(id, out var snapshot))
            {
                overrides = null;
                return false;
            }
            overrides = new SortedDictionary<TunableId, TunableValue>(snapshot);
            return true;
        }

        public LiveTuningResult<TuningState> Apply(TuningIntent intent)
        {
            if (intent == null) throw new ArgumentNullException(nameof(intent));
            return intent.ApplyTo(this);
        }

        /// <summary>Applies every intent in order. A rejected intent is recorded and the state it
        /// found is kept for the next intent in the sequence.</summary>
        public TuningSequenceResult ApplyAll(IEnumerable<TuningIntent> intents)
        {
            if (intents == null) throw new ArgumentNullException(nameof(intents));
            var state = this;
            var outcomes = new List<TuningIntentOutcome>();
            var index = 0;
            foreach (var intent in intents)
            {
                var result = state.Apply(intent);
                if (result.Succeeded)
                {
                    state = result.Value;
                    outcomes.Add(new TuningIntentOutcome(index, intent, true, result.Code, result.Message));
                }
                else
                {
                    outcomes.Add(new TuningIntentOutcome(index, intent, false, result.Code, result.Message));
                }
                index++;
            }
            return new TuningSequenceResult(state, outcomes);
        }

        internal LiveTuningResult<TuningState> ApplySet(TunableId id, TunableValue value)
        {
            if (!Registry.TryGet(id, out var registration))
            {
                return LiveTuningResult<TuningState>.Fail(LiveTuningFailure.TunableUnknown, $"Tunable '{id}' is not registered.");
            }
            if (registration.Kind != value.Kind)
            {
                return LiveTuningResult<TuningState>.Fail(LiveTuningFailure.TunableKindMismatch, $"Tunable '{id}' is registered as {registration.Kind} but the value is {value.Kind}.");
            }
            if (!registration.Declaration.Contains(value))
            {
                return LiveTuningResult<TuningState>.Fail(LiveTuningFailure.TunableOutOfRange, $"Tunable '{id}': value '{value.ToCanonicalString()}' is outside its declared range or value set.");
            }
            var overrides = new SortedDictionary<TunableId, TunableValue>(_overrides);
            if (value.Equals(registration.Default))
            {
                overrides.Remove(id);
            }
            else
            {
                overrides[id] = value;
            }
            return LiveTuningResult<TuningState>.Ok(new TuningState(Registry, overrides, _snapshots));
        }

        internal LiveTuningResult<TuningState> ApplyReset(TunableId id)
        {
            if (!Registry.TryGet(id, out _))
            {
                return LiveTuningResult<TuningState>.Fail(LiveTuningFailure.TunableUnknown, $"Tunable '{id}' is not registered.");
            }
            var overrides = new SortedDictionary<TunableId, TunableValue>(_overrides);
            overrides.Remove(id);
            return LiveTuningResult<TuningState>.Ok(new TuningState(Registry, overrides, _snapshots));
        }

        internal LiveTuningResult<TuningState> ApplyResetAll() =>
            LiveTuningResult<TuningState>.Ok(new TuningState(Registry, new SortedDictionary<TunableId, TunableValue>(), _snapshots));

        internal LiveTuningResult<TuningState> ApplySnapshot(SnapshotId id)
        {
            var snapshots = new SortedDictionary<SnapshotId, SortedDictionary<TunableId, TunableValue>>(_snapshots)
            {
                [id] = new SortedDictionary<TunableId, TunableValue>(_overrides)
            };
            return LiveTuningResult<TuningState>.Ok(new TuningState(Registry, _overrides, snapshots));
        }

        internal LiveTuningResult<TuningState> ApplyApplySnapshot(SnapshotId id)
        {
            if (!_snapshots.TryGetValue(id, out var snapshot))
            {
                return LiveTuningResult<TuningState>.Fail(LiveTuningFailure.SnapshotUnknown, $"Snapshot '{id}' is not held by this state.");
            }
            return LiveTuningResult<TuningState>.Ok(new TuningState(Registry, new SortedDictionary<TunableId, TunableValue>(snapshot), _snapshots));
        }

        /// <summary>A canonical text of the whole state: the registry fingerprint, the overrides
        /// in canonical id order, and the snapshots in canonical id order. Equal states, however
        /// reached, have equal fingerprints.</summary>
        public string Fingerprint()
        {
            var builder = new StringBuilder();
            builder.Append("registry[").Append(Registry.Fingerprint()).Append(']');
            builder.Append(" overrides[");
            foreach (var pair in _overrides)
            {
                builder.Append(pair.Key).Append('=').Append(pair.Value.ToCanonicalString()).Append(';');
            }
            builder.Append(']');
            builder.Append(" snapshots[");
            foreach (var snapshot in _snapshots)
            {
                builder.Append(snapshot.Key).Append('{');
                foreach (var pair in snapshot.Value)
                {
                    builder.Append(pair.Key).Append('=').Append(pair.Value.ToCanonicalString()).Append(';');
                }
                builder.Append('}');
            }
            builder.Append(']');
            return builder.ToString();
        }

        /// <summary>Builds the token-override document for this state's overrides. Never fails:
        /// export has no failure code at the Core level, and an override-free state yields an
        /// empty override list rather than being skipped.</summary>
        public TokenOverrideDocument Export() => TokenOverrideDocument.FromState(this);

        public bool Equals(TuningState other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return ReferenceEquals(Registry, other.Registry) && string.Equals(Fingerprint(), other.Fingerprint(), StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is TuningState other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Fingerprint());
        public override string ToString() => Fingerprint();
    }
}
