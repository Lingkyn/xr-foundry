using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Lingkyn.Haptics.Core
{
    // play/stop/stop_all/set_profile intents applied to an immutable, last-one-wins HapticState,
    // from any control, route, or device that raises them. Also carries LESSON-011's one-intent-
    // channel shape (IntentActor, optional expected revision, state.stale): the actor and revision
    // are not among docs/standards/haptics/verification-contract.md's literal Core-gate clauses,
    // but every live family answers LESSON-011 and this is the same shape and the same
    // "state.stale" code the Live Tuning and XR UI shell families already use for it.

    /// <summary>The closed set of sources that may issue a haptic intent: a person through some
    /// control, an agent adapter, a replay of a recorded sequence, or an import. Carried on every
    /// intent for attribution and replay only; it never selects a different validation rule, so a
    /// player-issued and an agent-issued copy of the same intent take the same path through
    /// <see cref="HapticState.Apply"/> and get the same result (LESSON-011).</summary>
    public enum IntentActor
    {
        Player,
        Agent,
        Replay,
        Import,
    }

    /// <summary>Closed set of intents applied to a <see cref="HapticState"/>. Every intent carries
    /// an <see cref="Actor"/> (defaulting to <see cref="IntentActor.Player"/> when a caller uses the
    /// short constructor) and an optional <see cref="ExpectedRevision"/>: a non-null value that
    /// does not match the state's current revision is rejected with
    /// <see cref="HapticFailure.StateStale"/> by <see cref="HapticState.Apply"/> before this
    /// intent's own <see cref="ApplyTo"/> ever runs, rather than overwriting another actor's
    /// change. Neither field changes which path an intent takes or what it validates.</summary>
    public abstract class HapticIntent
    {
        private protected HapticIntent(IntentActor actor, long? expectedRevision)
        {
            Actor = actor;
            ExpectedRevision = expectedRevision;
        }

        /// <summary>Who issued this intent. Attribution and replay only; see the type doc.</summary>
        public IntentActor Actor { get; }

        /// <summary>The revision this intent's issuer last observed, or null to skip the check.</summary>
        public long? ExpectedRevision { get; }

        public abstract string Describe();

        internal abstract HapticResult<HapticState> ApplyTo(HapticState state);

        public override string ToString() => Describe();
    }

    public sealed class PlayIntent : HapticIntent
    {
        public PlayIntent(HapticEventId eventId, HapticTarget target, float? amplitude = null, float? durationMs = null, float? frequencyHz = null)
            : this(eventId, target, amplitude, durationMs, frequencyHz, IntentActor.Player, null) { }

        public PlayIntent(HapticEventId eventId, HapticTarget target, float? amplitude, float? durationMs, float? frequencyHz, IntentActor actor, long? expectedRevision = null)
            : base(actor, expectedRevision)
        {
            EventId = eventId;
            Target = target;
            Amplitude = amplitude;
            DurationMs = durationMs;
            FrequencyHz = frequencyHz;
        }

        public HapticEventId EventId { get; }
        public HapticTarget Target { get; }
        public float? Amplitude { get; }
        public float? DurationMs { get; }
        public float? FrequencyHz { get; }

        public override string Describe() => $"play {EventId} @ {Target}";

        internal override HapticResult<HapticState> ApplyTo(HapticState state) => state.ApplyPlay(EventId, Target, Amplitude, DurationMs, FrequencyHz);
    }

    public sealed class StopIntent : HapticIntent
    {
        public StopIntent(HapticTarget target) : this(target, IntentActor.Player, null) { }

        public StopIntent(HapticTarget target, IntentActor actor, long? expectedRevision = null) : base(actor, expectedRevision)
        {
            Target = target;
        }

        public HapticTarget Target { get; }

        public override string Describe() => $"stop @ {Target}";

        internal override HapticResult<HapticState> ApplyTo(HapticState state) => state.ApplyStop(Target);
    }

    public sealed class StopAllIntent : HapticIntent
    {
        public StopAllIntent() : this(IntentActor.Player, null) { }

        public StopAllIntent(IntentActor actor, long? expectedRevision = null) : base(actor, expectedRevision) { }

        public override string Describe() => "stop_all";

        internal override HapticResult<HapticState> ApplyTo(HapticState state) => state.ApplyStopAll();
    }

    public sealed class SetProfileIntent : HapticIntent
    {
        public SetProfileIntent(HapticProfileId profileId) : this(profileId, IntentActor.Player, null) { }

        public SetProfileIntent(HapticProfileId profileId, IntentActor actor, long? expectedRevision = null) : base(actor, expectedRevision)
        {
            ProfileId = profileId;
        }

        public HapticProfileId ProfileId { get; }

        public override string Describe() => $"set_profile {ProfileId}";

        internal override HapticResult<HapticState> ApplyTo(HapticState state) => state.ApplySetProfile(ProfileId);
    }

    /// <summary>The outcome of one intent inside a sequence. Together, the outcomes of a sequence
    /// are the replay log: <see cref="Actor"/> (the issuer, from the intent) and
    /// <see cref="RevisionAfter"/> (the state's revision immediately after this outcome — the new
    /// state's revision when accepted, the unchanged state's revision when rejected) record who
    /// did what and at which revision, for every intent, accepted or rejected, in order.</summary>
    public sealed class HapticIntentOutcome
    {
        public HapticIntentOutcome(int index, HapticIntent intent, bool accepted, string code, string message, long revisionAfter)
        {
            Index = index;
            Intent = intent;
            Accepted = accepted;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            RevisionAfter = revisionAfter;
        }

        public int Index { get; }
        public HapticIntent Intent { get; }
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
    public sealed class HapticSequenceResult
    {
        public HapticSequenceResult(HapticState state, IReadOnlyList<HapticIntentOutcome> outcomes)
        {
            State = state;
            Outcomes = outcomes;
        }

        public HapticState State { get; }
        public IReadOnlyList<HapticIntentOutcome> Outcomes { get; }

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

    /// <summary>One target's active playback: the event that is playing and the effective
    /// amplitude, duration, and frequency the profile's scale was applied to when it started.</summary>
    public readonly struct ActivePlayback
    {
        public ActivePlayback(HapticEventId eventId, float amplitude, float durationMs, float? frequencyHz)
        {
            EventId = eventId;
            Amplitude = amplitude;
            DurationMs = durationMs;
            FrequencyHz = frequencyHz;
        }

        public HapticEventId EventId { get; }
        public float Amplitude { get; }
        public float DurationMs { get; }
        public float? FrequencyHz { get; }
    }

    /// <summary>
    /// Immutable haptic state over one event registry and one profile set: at most one active
    /// playback per target (last-one-wins) and the active profile id. Every accepted intent
    /// returns a new state; the prior state stays intact and readable. The same intent sequence
    /// over the same registry, profile set, and initial state always produces an equal final state
    /// and an equal fingerprint.
    /// </summary>
    public sealed class HapticState : IEquatable<HapticState>
    {
        private readonly SortedDictionary<HapticTarget, ActivePlayback> _active;

        private HapticState(
            HapticEventRegistry registry,
            HapticProfileSet profiles,
            HapticProfileId activeProfileId,
            SortedDictionary<HapticTarget, ActivePlayback> active,
            long revision)
        {
            Registry = registry;
            Profiles = profiles;
            ActiveProfileId = activeProfileId;
            _active = active;
            Revision = revision;
        }

        public HapticEventRegistry Registry { get; }
        public HapticProfileSet Profiles { get; }
        public HapticProfileId ActiveProfileId { get; }

        /// <summary>Monotonically increasing: 0 for <see cref="Initial"/>, and one higher on every
        /// accepted intent, whether or not the active playbacks actually changed. Not part of
        /// <see cref="Fingerprint"/> (LESSON-011): two states with the same net active playbacks and
        /// the same active profile still have equal fingerprints even when they reached different
        /// revisions.</summary>
        public long Revision { get; }

        /// <summary>Every target with an active playback, in canonical target order.</summary>
        public IReadOnlyList<HapticTarget> ActiveTargets => new List<HapticTarget>(_active.Keys);

        public static HapticState Initial(HapticEventRegistry registry, HapticProfileSet profiles, HapticProfileId initialProfileId)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (profiles == null) throw new ArgumentNullException(nameof(profiles));
            if (!profiles.TryGet(initialProfileId, out _))
            {
                throw new ArgumentException($"Initial profile '{initialProfileId}' is not in the profile set.", nameof(initialProfileId));
            }
            return new HapticState(registry, profiles, initialProfileId, new SortedDictionary<HapticTarget, ActivePlayback>(), 0);
        }

        public bool TryGetActive(HapticTarget target, out ActivePlayback playback) => _active.TryGetValue(target, out playback);

        /// <summary>Applies one intent. Checked in exactly two steps, in this order, for every
        /// intent regardless of <see cref="HapticIntent.Actor"/> (LESSON-011: the actor is never a
        /// second validation rule): first, a non-null <see cref="HapticIntent.ExpectedRevision"/>
        /// that does not match <see cref="Revision"/> is rejected with
        /// <see cref="HapticFailure.StateStale"/> and changes nothing — the intent's own
        /// <see cref="HapticIntent.ApplyTo"/> never runs; second, the intent's own rule applies.</summary>
        public HapticResult<HapticState> Apply(HapticIntent intent)
        {
            if (intent == null) throw new ArgumentNullException(nameof(intent));
            if (intent.ExpectedRevision.HasValue && intent.ExpectedRevision.Value != Revision)
            {
                return HapticResult<HapticState>.Fail(
                    HapticFailure.StateStale,
                    $"Intent expected revision {intent.ExpectedRevision.Value} but the state is at revision {Revision}; nothing changed.");
            }
            return intent.ApplyTo(this);
        }

        /// <summary>Applies every intent in order. A rejected intent is recorded and the state it
        /// found is kept for the next intent in the sequence. The outcomes are the replay log: each
        /// carries its intent's actor and the state's revision immediately after it settled.</summary>
        public HapticSequenceResult ApplyAll(IEnumerable<HapticIntent> intents)
        {
            if (intents == null) throw new ArgumentNullException(nameof(intents));
            var state = this;
            var outcomes = new List<HapticIntentOutcome>();
            var index = 0;
            foreach (var intent in intents)
            {
                var result = state.Apply(intent);
                if (result.Succeeded)
                {
                    state = result.Value;
                    outcomes.Add(new HapticIntentOutcome(index, intent, true, result.Code, result.Message, state.Revision));
                }
                else
                {
                    outcomes.Add(new HapticIntentOutcome(index, intent, false, result.Code, result.Message, state.Revision));
                }
                index++;
            }
            return new HapticSequenceResult(state, outcomes);
        }

        internal HapticResult<HapticState> ApplyPlay(HapticEventId eventId, HapticTarget target, float? amplitudeOverride, float? durationOverride, float? frequencyOverride)
        {
            if (!Registry.TryGet(eventId, out var definition))
            {
                return HapticResult<HapticState>.Fail(HapticFailure.EventUnknown, $"Haptic event '{eventId}' is not registered.");
            }
            if (!IsResolvableTarget(target))
            {
                return HapticResult<HapticState>.Fail(HapticFailure.TargetUnknown, $"Target '{target}' is not in the closed target set, or is a named_device id the active profile does not declare.");
            }
            if (frequencyOverride.HasValue && !HapticEventDefinition.KindUsesFrequency(definition.Kind))
            {
                return HapticResult<HapticState>.Fail(HapticFailure.KindMismatch, $"Event '{eventId}' is {definition.Kind} and does not use a frequency override.");
            }
            var amplitude = amplitudeOverride ?? definition.DefaultAmplitude;
            if (!definition.Amplitude.Contains(amplitude))
            {
                return HapticResult<HapticState>.Fail(HapticFailure.AmplitudeOutOfRange, $"Event '{eventId}': amplitude {amplitude} is outside [{definition.Amplitude.Min},{definition.Amplitude.Max}].");
            }
            var durationMs = durationOverride ?? definition.DefaultDurationMs;
            if (!definition.Duration.Contains(durationMs))
            {
                return HapticResult<HapticState>.Fail(HapticFailure.DurationOutOfRange, $"Event '{eventId}': duration {durationMs} ms is outside [{definition.Duration.Min},{definition.Duration.Max}].");
            }
            float? frequency = null;
            if (HapticEventDefinition.KindUsesFrequency(definition.Kind) && definition.Frequency.HasValue)
            {
                frequency = frequencyOverride ?? definition.DefaultFrequencyHz;
            }

            Profiles.TryGet(ActiveProfileId, out var activeProfile);
            var (scale, _) = activeProfile.Resolve(target);
            var effective = new ActivePlayback(eventId, amplitude * scale, durationMs, frequency);

            var active = new SortedDictionary<HapticTarget, ActivePlayback>(_active) { [target] = effective };
            return HapticResult<HapticState>.Ok(new HapticState(Registry, Profiles, ActiveProfileId, active, Revision + 1));
        }

        internal HapticResult<HapticState> ApplyStop(HapticTarget target)
        {
            var active = new SortedDictionary<HapticTarget, ActivePlayback>(_active);
            active.Remove(target);
            return HapticResult<HapticState>.Ok(new HapticState(Registry, Profiles, ActiveProfileId, active, Revision + 1));
        }

        internal HapticResult<HapticState> ApplyStopAll() =>
            HapticResult<HapticState>.Ok(new HapticState(Registry, Profiles, ActiveProfileId, new SortedDictionary<HapticTarget, ActivePlayback>(), Revision + 1));

        internal HapticResult<HapticState> ApplySetProfile(HapticProfileId profileId)
        {
            if (!Profiles.TryGet(profileId, out _))
            {
                return HapticResult<HapticState>.Fail(HapticFailure.ProfileUnknown, $"Profile '{profileId}' is not in the profile set.");
            }
            return HapticResult<HapticState>.Ok(new HapticState(Registry, Profiles, profileId, new SortedDictionary<HapticTarget, ActivePlayback>(_active), Revision + 1));
        }

        private bool IsResolvableTarget(HapticTarget target)
        {
            if (target.Kind == HapticTargetKind.Left || target.Kind == HapticTargetKind.Right || target.Kind == HapticTargetKind.Both)
            {
                return true;
            }
            if (target.Kind == HapticTargetKind.NamedDevice)
            {
                if (!Profiles.TryGet(ActiveProfileId, out var activeProfile)) return false;
                foreach (var declared in activeProfile.Targets)
                {
                    if (declared.Equals(target)) return true;
                }
                return false;
            }
            return false;
        }

        /// <summary>A canonical text of the whole state: the event-registry fingerprint, the
        /// profile-set fingerprint, the active profile id, and the active-playback entries in
        /// canonical target order. <see cref="Revision"/> is excluded (LESSON-011): two states
        /// reached by different intent sequences with the same net active playbacks and the same
        /// active profile have equal fingerprints.</summary>
        public string Fingerprint()
        {
            var builder = new StringBuilder();
            builder.Append("registry[").Append(Registry.Fingerprint()).Append(']');
            builder.Append(" profiles[").Append(Profiles.Fingerprint()).Append(']');
            builder.Append(" active_profile[").Append(ActiveProfileId).Append(']');
            builder.Append(" playbacks[");
            foreach (var pair in _active)
            {
                builder.Append(pair.Key).Append('=').Append(pair.Value.EventId).Append(':')
                       .Append(pair.Value.Amplitude.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                       .Append(pair.Value.DurationMs.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                       .Append(pair.Value.FrequencyHz.HasValue ? pair.Value.FrequencyHz.Value.ToString("R", CultureInfo.InvariantCulture) : "-")
                       .Append(';');
            }
            builder.Append(']');
            return builder.ToString();
        }

        public bool Equals(HapticState other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return ReferenceEquals(Registry, other.Registry) && ReferenceEquals(Profiles, other.Profiles) && string.Equals(Fingerprint(), other.Fingerprint(), StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is HapticState other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Fingerprint());
        public override string ToString() => Fingerprint();
    }
}
