using System;
using System.Collections.Generic;
using System.Text;

namespace Lingkyn.LiveTuning.Core
{
    // Tuning intents (set, reset, reset_all, snapshot, apply_snapshot) applied to an immutable
    // TuningState whose overrides are the only diff from the registry's defaults, from any
    // control, panel, or input that raises them. Export is a pure read of a state, not an
    // intent that can fail, and lives at the bottom of this file next to TuningState.

    /// <summary>The closed set of sources that may issue a tuning intent: a person through the
    /// panel host UI, an agent adapter, a replay of a recorded sequence, or a device/Editor
    /// override import. Carried on every intent for attribution and replay only; it never selects
    /// a different validation rule, so a player-issued and an agent-issued copy of the same
    /// intent take the same path through <see cref="TuningState.Apply"/> and get the same
    /// result (LESSON-011).</summary>
    public enum IntentActor
    {
        Player,
        Agent,
        Replay,
        Import,
    }

    /// <summary>Closed set of intents applied to a <see cref="TuningState"/>. Every intent carries
    /// an <see cref="Actor"/> (defaulting to <see cref="IntentActor.Player"/> when a caller uses
    /// the short constructor) and an optional <see cref="ExpectedRevision"/>: a non-null value
    /// that does not match the state's current revision is rejected with
    /// <see cref="LiveTuningFailure.StateStale"/> by <see cref="TuningState.Apply"/> before this
    /// intent's own <see cref="ApplyTo"/> ever runs, rather than overwriting another actor's
    /// change. Neither field changes which path an intent takes or what it validates.</summary>
    public abstract class TuningIntent
    {
        private protected TuningIntent(IntentActor actor, long? expectedRevision)
        {
            Actor = actor;
            ExpectedRevision = expectedRevision;
        }

        /// <summary>Who issued this intent. Attribution and replay only; see the type doc.</summary>
        public IntentActor Actor { get; }

        /// <summary>The revision this intent's issuer last observed, or null to skip the check.</summary>
        public long? ExpectedRevision { get; }

        public abstract string Describe();

        internal abstract LiveTuningResult<TuningState> ApplyTo(TuningState state);

        public override string ToString() => Describe();
    }

    public sealed class SetIntent : TuningIntent
    {
        public SetIntent(TunableId id, TunableValue value) : this(id, value, IntentActor.Player, null) { }

        public SetIntent(TunableId id, TunableValue value, IntentActor actor, long? expectedRevision = null)
            : base(actor, expectedRevision)
        {
            Id = id;
            Value = value;
        }

        public TunableId Id { get; }
        public TunableValue Value { get; }

        public override string Describe() => $"set {Id} = {Value.ToCanonicalString()}";

        internal override LiveTuningResult<TuningState> ApplyTo(TuningState state) => state.ApplySet(Id, Value);
    }

    public sealed class ResetIntent : TuningIntent
    {
        public ResetIntent(TunableId id) : this(id, IntentActor.Player, null) { }

        public ResetIntent(TunableId id, IntentActor actor, long? expectedRevision = null) : base(actor, expectedRevision)
        {
            Id = id;
        }

        public TunableId Id { get; }

        public override string Describe() => $"reset {Id}";

        internal override LiveTuningResult<TuningState> ApplyTo(TuningState state) => state.ApplyReset(Id);
    }

    public sealed class ResetAllIntent : TuningIntent
    {
        public ResetAllIntent() : this(IntentActor.Player, null) { }

        public ResetAllIntent(IntentActor actor, long? expectedRevision = null) : base(actor, expectedRevision) { }

        public override string Describe() => "reset_all";

        internal override LiveTuningResult<TuningState> ApplyTo(TuningState state) => state.ApplyResetAll();
    }

    public sealed class SnapshotIntent : TuningIntent
    {
        public SnapshotIntent(SnapshotId id) : this(id, IntentActor.Player, null) { }

        public SnapshotIntent(SnapshotId id, IntentActor actor, long? expectedRevision = null) : base(actor, expectedRevision)
        {
            Id = id;
        }

        public SnapshotId Id { get; }

        public override string Describe() => $"snapshot {Id}";

        internal override LiveTuningResult<TuningState> ApplyTo(TuningState state) => state.ApplySnapshot(Id);
    }

    public sealed class ApplySnapshotIntent : TuningIntent
    {
        public ApplySnapshotIntent(SnapshotId id) : this(id, IntentActor.Player, null) { }

        public ApplySnapshotIntent(SnapshotId id, IntentActor actor, long? expectedRevision = null) : base(actor, expectedRevision)
        {
            Id = id;
        }

        public SnapshotId Id { get; }

        public override string Describe() => $"apply_snapshot {Id}";

        internal override LiveTuningResult<TuningState> ApplyTo(TuningState state) => state.ApplyApplySnapshot(Id);
    }

    /// <summary>The outcome of one intent inside a sequence. Together, the outcomes of a sequence
    /// are the replay log: <see cref="Actor"/> (the issuer, from the intent) and
    /// <see cref="RevisionAfter"/> (the state's revision immediately after this outcome — the new
    /// state's revision when accepted, the unchanged state's revision when rejected) record who
    /// did what and at which revision, for every intent, accepted or rejected, in order.</summary>
    public sealed class TuningIntentOutcome
    {
        public TuningIntentOutcome(int index, TuningIntent intent, bool accepted, string code, string message, long revisionAfter)
        {
            Index = index;
            Intent = intent;
            Accepted = accepted;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            RevisionAfter = revisionAfter;
        }

        public int Index { get; }
        public TuningIntent Intent { get; }
        public bool Accepted { get; }
        public string Code { get; }
        public string Message { get; }
        public long RevisionAfter { get; }

        /// <summary>The actor that issued this outcome's intent. Forwarded from <see cref="Intent"/>
        /// for convenience; attribution and replay only, per <see cref="IntentActor"/>.</summary>
        public IntentActor Actor => Intent.Actor;
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
            SortedDictionary<SnapshotId, SortedDictionary<TunableId, TunableValue>> snapshots,
            long revision)
        {
            Registry = registry;
            _overrides = overrides;
            _snapshots = snapshots;
            Revision = revision;
        }

        public TunableRegistry Registry { get; }

        /// <summary>Monotonically increasing: 0 for <see cref="Initial"/>, and one higher on every
        /// accepted intent, whether the override set actually changed or not. Not part of
        /// <see cref="Fingerprint"/>: two states with the same overrides reached by different
        /// sequences still have equal fingerprints even when they reached different revisions.
        /// Used only to gate a stale <see cref="TuningIntent.ExpectedRevision"/>.</summary>
        public long Revision { get; }

        /// <summary>The current overrides, in canonical id order. This and the registry's
        /// defaults are the only two sources of an effective value.</summary>
        public IReadOnlyDictionary<TunableId, TunableValue> Overrides => new SortedDictionary<TunableId, TunableValue>(_overrides);

        public IReadOnlyList<SnapshotId> SnapshotIds => new List<SnapshotId>(_snapshots.Keys);

        public static TuningState Initial(TunableRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            return new TuningState(registry, new SortedDictionary<TunableId, TunableValue>(), new SortedDictionary<SnapshotId, SortedDictionary<TunableId, TunableValue>>(), 0);
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

        /// <summary>Applies one intent. Checked in exactly two steps, in this order, for every
        /// intent regardless of <see cref="TuningIntent.Actor"/> (LESSON-011: the actor is never a
        /// second validation rule): first, a non-null <see cref="TuningIntent.ExpectedRevision"/>
        /// that does not match <see cref="Revision"/> is rejected with
        /// <see cref="LiveTuningFailure.StateStale"/> and changes nothing — the intent's own
        /// <see cref="TuningIntent.ApplyTo"/> never runs; second, the intent's own rule
        /// applies.</summary>
        public LiveTuningResult<TuningState> Apply(TuningIntent intent)
        {
            if (intent == null) throw new ArgumentNullException(nameof(intent));
            if (intent.ExpectedRevision.HasValue && intent.ExpectedRevision.Value != Revision)
            {
                return LiveTuningResult<TuningState>.Fail(
                    LiveTuningFailure.StateStale,
                    $"Intent expected revision {intent.ExpectedRevision.Value} but the state is at revision {Revision}; nothing changed.");
            }
            return intent.ApplyTo(this);
        }

        /// <summary>Applies every intent in order. A rejected intent is recorded and the state it
        /// found is kept for the next intent in the sequence. The outcomes are the replay log: each
        /// carries its intent's actor and the state's revision immediately after it settled.</summary>
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
                    outcomes.Add(new TuningIntentOutcome(index, intent, true, result.Code, result.Message, state.Revision));
                }
                else
                {
                    outcomes.Add(new TuningIntentOutcome(index, intent, false, result.Code, result.Message, state.Revision));
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
            return LiveTuningResult<TuningState>.Ok(new TuningState(Registry, overrides, _snapshots, Revision + 1));
        }

        internal LiveTuningResult<TuningState> ApplyReset(TunableId id)
        {
            if (!Registry.TryGet(id, out _))
            {
                return LiveTuningResult<TuningState>.Fail(LiveTuningFailure.TunableUnknown, $"Tunable '{id}' is not registered.");
            }
            var overrides = new SortedDictionary<TunableId, TunableValue>(_overrides);
            overrides.Remove(id);
            return LiveTuningResult<TuningState>.Ok(new TuningState(Registry, overrides, _snapshots, Revision + 1));
        }

        internal LiveTuningResult<TuningState> ApplyResetAll() =>
            LiveTuningResult<TuningState>.Ok(new TuningState(Registry, new SortedDictionary<TunableId, TunableValue>(), _snapshots, Revision + 1));

        internal LiveTuningResult<TuningState> ApplySnapshot(SnapshotId id)
        {
            var snapshots = new SortedDictionary<SnapshotId, SortedDictionary<TunableId, TunableValue>>(_snapshots)
            {
                [id] = new SortedDictionary<TunableId, TunableValue>(_overrides)
            };
            return LiveTuningResult<TuningState>.Ok(new TuningState(Registry, _overrides, snapshots, Revision + 1));
        }

        internal LiveTuningResult<TuningState> ApplyApplySnapshot(SnapshotId id)
        {
            if (!_snapshots.TryGetValue(id, out var snapshot))
            {
                return LiveTuningResult<TuningState>.Fail(LiveTuningFailure.SnapshotUnknown, $"Snapshot '{id}' is not held by this state.");
            }
            return LiveTuningResult<TuningState>.Ok(new TuningState(Registry, new SortedDictionary<TunableId, TunableValue>(snapshot), _snapshots, Revision + 1));
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

        /// <summary>The actor- and revision-aware export: the same document as <see cref="Export"/>,
        /// gated by an optional expected revision exactly like a mutating intent (LESSON-011). A
        /// non-null <paramref name="expectedRevision"/> that does not match <see cref="Revision"/>
        /// is rejected with <see cref="LiveTuningFailure.StateStale"/> and writes nothing, rather
        /// than exporting a document another actor's change has already superseded.
        /// <paramref name="actor"/> is carried for attribution only and never changes this
        /// gate.</summary>
        public LiveTuningResult<TokenOverrideDocument> Export(IntentActor actor, long? expectedRevision)
        {
            if (expectedRevision.HasValue && expectedRevision.Value != Revision)
            {
                return LiveTuningResult<TokenOverrideDocument>.Fail(
                    LiveTuningFailure.StateStale,
                    $"Export expected revision {expectedRevision.Value} but the state is at revision {Revision}; nothing was exported.");
            }
            return LiveTuningResult<TokenOverrideDocument>.Ok(Export());
        }

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
