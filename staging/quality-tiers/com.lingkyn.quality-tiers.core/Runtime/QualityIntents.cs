using System;
using System.Collections.Generic;

namespace Lingkyn.QualityTiers.Core
{
    // select_tier/set_override/reset/apply_preset intents applied to an immutable QualityState,
    // from any control, route, or device that raises them. Also carries LESSON-011's one-intent-
    // channel shape (IntentActor, optional expected revision, state.stale): every live family
    // answers LESSON-011 with the same shape (see staging/haptics, staging/live-tuning).

    /// <summary>The closed set of sources that may issue a quality intent: a person through some
    /// control, an agent adapter, a replay of a recorded sequence, or an import. Carried on every
    /// intent for attribution and replay only; it never selects a different validation rule, so a
    /// player-issued and an agent-issued copy of the same intent take the same path through
    /// <see cref="QualityState.Apply"/> and get the same result (LESSON-011, QC-14).</summary>
    public enum IntentActor
    {
        Player,
        Agent,
        Replay,
        Import,
    }

    /// <summary>Closed set of intents applied to a <see cref="QualityState"/>. Every intent carries
    /// an <see cref="Actor"/> (defaulting to <see cref="IntentActor.Player"/> when a caller uses the
    /// short constructor) and an optional <see cref="ExpectedRevision"/>: a non-null value that
    /// does not match the state's current revision is rejected with
    /// <see cref="QualityFailure.StateStale"/> by <see cref="QualityState.Apply"/> before this
    /// intent's own rule ever runs (QC-13). Neither field changes which path an intent takes or
    /// what it validates (QC-14).</summary>
    public abstract class QualityIntent
    {
        private protected QualityIntent(DeviceProfileId deviceProfileId, IntentActor actor, long? expectedRevision)
        {
            DeviceProfileId = deviceProfileId;
            Actor = actor;
            ExpectedRevision = expectedRevision;
        }

        /// <summary>Every quality intent names one device profile.</summary>
        public DeviceProfileId DeviceProfileId { get; }

        /// <summary>Who issued this intent. Attribution and replay only; see the type doc.</summary>
        public IntentActor Actor { get; }

        /// <summary>The revision this intent's issuer last observed, or null to skip the check.</summary>
        public long? ExpectedRevision { get; }

        public abstract string Describe();

        internal abstract QualityResult<QualityState> ApplyTo(QualityState state);

        public override string ToString() => Describe();
    }

    /// <summary>Selects <paramref name="TierId"/> as the active tier for a device profile.</summary>
    public sealed class SelectTierIntent : QualityIntent
    {
        public SelectTierIntent(DeviceProfileId deviceProfileId, TierId tierId)
            : this(deviceProfileId, tierId, IntentActor.Player, null) { }

        public SelectTierIntent(DeviceProfileId deviceProfileId, TierId tierId, IntentActor actor, long? expectedRevision = null)
            : base(deviceProfileId, actor, expectedRevision)
        {
            TierId = tierId;
        }

        public TierId TierId { get; }

        public override string Describe() => $"select_tier {DeviceProfileId} -> {TierId}";

        internal override QualityResult<QualityState> ApplyTo(QualityState state) => state.ApplySelectTier(DeviceProfileId, TierId);
    }

    /// <summary>Overrides one closed-set field for a device profile's active tier.</summary>
    public sealed class SetOverrideIntent : QualityIntent
    {
        public SetOverrideIntent(DeviceProfileId deviceProfileId, string fieldName, float value)
            : this(deviceProfileId, fieldName, value, IntentActor.Player, null) { }

        public SetOverrideIntent(DeviceProfileId deviceProfileId, string fieldName, float value, IntentActor actor, long? expectedRevision = null)
            : base(deviceProfileId, actor, expectedRevision)
        {
            FieldName = fieldName ?? string.Empty;
            Value = value;
        }

        /// <summary>The raw field name, checked against the closed override-field set only when
        /// this intent is applied (QC-05).</summary>
        public string FieldName { get; }
        public float Value { get; }

        public override string Describe() => $"set_override {DeviceProfileId} {FieldName}={Value}";

        internal override QualityResult<QualityState> ApplyTo(QualityState state) => state.ApplySetOverride(DeviceProfileId, FieldName, Value);
    }

    /// <summary>Clears every override for a device profile, keeping its selected tier.</summary>
    public sealed class ResetIntent : QualityIntent
    {
        public ResetIntent(DeviceProfileId deviceProfileId) : this(deviceProfileId, IntentActor.Player, null) { }

        public ResetIntent(DeviceProfileId deviceProfileId, IntentActor actor, long? expectedRevision = null)
            : base(deviceProfileId, actor, expectedRevision) { }

        public override string Describe() => $"reset {DeviceProfileId}";

        internal override QualityResult<QualityState> ApplyTo(QualityState state) => state.ApplyReset(DeviceProfileId);
    }

    /// <summary>Selects <paramref name="TierId"/> as the device's declared default, identically to
    /// <see cref="SelectTierIntent"/> in every validation rule.</summary>
    public sealed class ApplyPresetIntent : QualityIntent
    {
        public ApplyPresetIntent(DeviceProfileId deviceProfileId, TierId tierId)
            : this(deviceProfileId, tierId, IntentActor.Player, null) { }

        public ApplyPresetIntent(DeviceProfileId deviceProfileId, TierId tierId, IntentActor actor, long? expectedRevision = null)
            : base(deviceProfileId, actor, expectedRevision)
        {
            TierId = tierId;
        }

        public TierId TierId { get; }

        public override string Describe() => $"apply_preset {DeviceProfileId} -> {TierId}";

        internal override QualityResult<QualityState> ApplyTo(QualityState state) => state.ApplySelectTier(DeviceProfileId, TierId);
    }

    /// <summary>The outcome of one intent inside a sequence. Together, the outcomes of a sequence
    /// are the replay log (QC-15): <see cref="Actor"/> (the issuer, from the intent) and
    /// <see cref="RevisionAfter"/> (the state's revision immediately after this outcome) record who
    /// did what and at which revision, for every intent, accepted or rejected, in order.</summary>
    public sealed class QualityIntentOutcome
    {
        public QualityIntentOutcome(int index, QualityIntent intent, bool accepted, string code, string message, long revisionAfter)
        {
            Index = index;
            Intent = intent;
            Accepted = accepted;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            RevisionAfter = revisionAfter;
        }

        public int Index { get; }
        public QualityIntent Intent { get; }
        public bool Accepted { get; }
        public string Code { get; }
        public string Message { get; }
        public long RevisionAfter { get; }

        /// <summary>The actor that issued this outcome's intent. Forwarded from <see cref="Intent"/>
        /// for convenience.</summary>
        public IntentActor Actor => Intent.Actor;
    }

    /// <summary>The state after a sequence and one outcome per intent. A rejected intent keeps the
    /// state it found; the next intent in the sequence still applies to that same state.</summary>
    public sealed class QualitySequenceResult
    {
        public QualitySequenceResult(QualityState state, IReadOnlyList<QualityIntentOutcome> outcomes)
        {
            State = state;
            Outcomes = outcomes;
        }

        public QualityState State { get; }
        public IReadOnlyList<QualityIntentOutcome> Outcomes { get; }

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
}
