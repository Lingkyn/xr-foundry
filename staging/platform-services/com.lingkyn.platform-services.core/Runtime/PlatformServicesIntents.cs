using System;
using System.Collections.Generic;

namespace Lingkyn.PlatformServices.Core
{
    // The six typed intents applied to an immutable PlatformServicesState, from any control, UI, or
    // gameplay event that raises them. Also carries LESSON-011's one-intent-channel shape
    // (IntentActor, optional expected revision, state.stale): every intent is issued through this one
    // seam, whether a person's UI, an agent adapter, a replay, or an import raises it.

    /// <summary>The closed set of sources that may issue a platform-services intent: a person through
    /// some UI, an agent adapter, a replay of a recorded sequence, or an import. Carried on every
    /// intent for attribution and replay only; it never selects a different validation rule, so a
    /// player-issued and an agent-issued copy of the same intent take the same path through
    /// <see cref="PlatformServicesState.Apply"/> and get the same result (LESSON-011).</summary>
    public enum IntentActor
    {
        Player,
        Agent,
        Replay,
        Import,
    }

    /// <summary>The closed set of upload policies <see cref="ReportScoreIntent"/> may name.
    /// <see cref="KeepBest"/> only overwrites the leaderboard's last accepted score when the new
    /// score is strictly better (higher); <see cref="Always"/> overwrites unconditionally.</summary>
    public enum LeaderboardUploadPolicy
    {
        KeepBest,
        Always,
    }

    /// <summary>The closed set of range selectors <see cref="ReadLeaderboardIntent"/> may name. The
    /// Core never resolves a range to rows itself; the selector is opaque data the Unity adapter's
    /// injected provider interprets.</summary>
    public enum LeaderboardRange
    {
        Top,
        AroundPlayer,
        Friends,
    }

    /// <summary>The closed set of outcomes a caller (the Unity adapter; in tests, a fake provider)
    /// may report for a dispatched <see cref="UnlockIntent"/>, <see cref="ReportScoreIntent"/>, or
    /// <see cref="CloudWriteIntent"/>. Never inferred by the Core from a network call it never
    /// makes.</summary>
    public enum ProviderOutcome
    {
        Accepted,
        NotEntitled,
        RateLimited,
    }

    /// <summary>Closed set of intents applied to a <see cref="PlatformServicesState"/>. Every intent
    /// names the <see cref="Capability"/> it exercises, carries an <see cref="Actor"/> (defaulting to
    /// <see cref="IntentActor.Player"/> when a caller uses the short constructor), and an optional
    /// <see cref="ExpectedRevision"/>: a non-null value that does not match the state's current
    /// revision is rejected with <see cref="PlatformServicesFailure.StateStale"/> by
    /// <see cref="PlatformServicesState.Apply"/> before this intent's own <see cref="ApplyTo"/> ever
    /// runs, rather than overwriting another actor's change. Neither field changes which path an
    /// intent takes or what it validates.</summary>
    public abstract class PlatformServicesIntent
    {
        private protected PlatformServicesIntent(PlatformServiceCapability capability, IntentActor actor, long? expectedRevision)
        {
            Capability = capability;
            Actor = actor;
            ExpectedRevision = expectedRevision;
        }

        /// <summary>The single capability this intent exercises. An active provider whose descriptor
        /// does not declare this capability rejects the intent with
        /// <see cref="PlatformServicesFailure.CapabilityUnsupported"/> rather than attempting it.</summary>
        public PlatformServiceCapability Capability { get; }

        /// <summary>Who issued this intent. Attribution and replay only; see the type doc.</summary>
        public IntentActor Actor { get; }

        /// <summary>The revision this intent's issuer last observed, or null to skip the check.</summary>
        public long? ExpectedRevision { get; }

        public abstract string Describe();

        internal abstract PlatformServicesResult<PlatformServicesState> ApplyTo(PlatformServicesState state);

        public override string ToString() => Describe();
    }

    /// <summary>Resolves identity for the active provider (this is the one intent exempt from the
    /// identity precondition: every other intent issued before an <see cref="AccountId"/> is
    /// resolved is rejected with <see cref="PlatformServicesFailure.IdentityMissing"/>) and reports
    /// whether the resolved account holds the entitlement. The caller (the Unity adapter; in tests, a
    /// fake provider) supplies both the resolved account id and whether it is entitled; the Core
    /// infers neither from a network call it never makes. A <c>false</c> <see cref="Entitled"/>
    /// rejects with <see cref="PlatformServicesFailure.NotEntitled"/> and resolves no identity,
    /// leaving the prior state intact, so the identity precondition still holds for every subsequent
    /// intent until a later, entitled check_entitlement succeeds.</summary>
    public sealed class CheckEntitlementIntent : PlatformServicesIntent
    {
        public CheckEntitlementIntent(AccountId accountId, bool entitled) : this(accountId, entitled, IntentActor.Player, null) { }

        public CheckEntitlementIntent(AccountId accountId, bool entitled, IntentActor actor, long? expectedRevision = null)
            : base(PlatformServiceCapability.Entitlement, actor, expectedRevision)
        {
            AccountId = accountId;
            Entitled = entitled;
        }

        public AccountId AccountId { get; }
        public bool Entitled { get; }

        public override string Describe() => $"check_entitlement {AccountId} entitled={Entitled}";

        internal override PlatformServicesResult<PlatformServicesState> ApplyTo(PlatformServicesState state) => state.ApplyCheckEntitlement(AccountId, Entitled);
    }

    public sealed class UnlockIntent : PlatformServicesIntent
    {
        public UnlockIntent(string achievementId, string idempotencyKey, ProviderOutcome? outcome = null)
            : this(achievementId, idempotencyKey, outcome, IntentActor.Player, null) { }

        public UnlockIntent(string achievementId, string idempotencyKey, ProviderOutcome? outcome, IntentActor actor, long? expectedRevision = null)
            : base(PlatformServiceCapability.Achievement, actor, expectedRevision)
        {
            if (string.IsNullOrEmpty(idempotencyKey)) throw new ArgumentException("An idempotency key is required.", nameof(idempotencyKey));
            AchievementId = achievementId ?? string.Empty;
            IdempotencyKey = idempotencyKey;
            Outcome = outcome;
        }

        /// <summary>An opaque, vendor-defined achievement id. Never validated or classified by the
        /// Core (LESSON-001-style opaque content, see the family README's capability boundary).</summary>
        public string AchievementId { get; }
        public string IdempotencyKey { get; }
        /// <summary>Null when the provider was never reached (issued offline).</summary>
        public ProviderOutcome? Outcome { get; }

        public override string Describe() => $"unlock '{AchievementId}' key={IdempotencyKey} outcome={(Outcome.HasValue ? Outcome.Value.ToString() : "offline")}";

        internal override PlatformServicesResult<PlatformServicesState> ApplyTo(PlatformServicesState state) => state.ApplyUnlock(AchievementId, IdempotencyKey, Outcome, Actor);
    }

    public sealed class ReportScoreIntent : PlatformServicesIntent
    {
        public ReportScoreIntent(string leaderboardId, long score, LeaderboardUploadPolicy uploadPolicy, string idempotencyKey, ProviderOutcome? outcome = null)
            : this(leaderboardId, score, uploadPolicy, idempotencyKey, outcome, IntentActor.Player, null) { }

        public ReportScoreIntent(string leaderboardId, long score, LeaderboardUploadPolicy uploadPolicy, string idempotencyKey, ProviderOutcome? outcome, IntentActor actor, long? expectedRevision = null)
            : base(PlatformServiceCapability.Leaderboard, actor, expectedRevision)
        {
            if (string.IsNullOrEmpty(idempotencyKey)) throw new ArgumentException("An idempotency key is required.", nameof(idempotencyKey));
            LeaderboardId = leaderboardId ?? string.Empty;
            Score = score;
            UploadPolicy = uploadPolicy;
            IdempotencyKey = idempotencyKey;
            Outcome = outcome;
        }

        public string LeaderboardId { get; }
        public long Score { get; }
        public LeaderboardUploadPolicy UploadPolicy { get; }
        public string IdempotencyKey { get; }
        public ProviderOutcome? Outcome { get; }

        public override string Describe() => $"report_score '{LeaderboardId}'={Score} policy={UploadPolicy} key={IdempotencyKey} outcome={(Outcome.HasValue ? Outcome.Value.ToString() : "offline")}";

        internal override PlatformServicesResult<PlatformServicesState> ApplyTo(PlatformServicesState state) => state.ApplyReportScore(LeaderboardId, Score, UploadPolicy, IdempotencyKey, Outcome, Actor);
    }

    public sealed class ReadLeaderboardIntent : PlatformServicesIntent
    {
        public ReadLeaderboardIntent(string leaderboardId, LeaderboardRange range) : this(leaderboardId, range, IntentActor.Player, null) { }

        public ReadLeaderboardIntent(string leaderboardId, LeaderboardRange range, IntentActor actor, long? expectedRevision = null)
            : base(PlatformServiceCapability.Leaderboard, actor, expectedRevision)
        {
            LeaderboardId = leaderboardId ?? string.Empty;
            Range = range;
        }

        public string LeaderboardId { get; }
        public LeaderboardRange Range { get; }

        public override string Describe() => $"read_leaderboard '{LeaderboardId}' range={Range}";

        internal override PlatformServicesResult<PlatformServicesState> ApplyTo(PlatformServicesState state) => state.ApplyReadLeaderboard(LeaderboardId, Range);
    }

    public sealed class CloudWriteIntent : PlatformServicesIntent
    {
        public CloudWriteIntent(string key, byte[] payload, string idempotencyKey, ProviderOutcome? outcome = null)
            : this(key, payload, idempotencyKey, outcome, IntentActor.Player, null) { }

        public CloudWriteIntent(string key, byte[] payload, string idempotencyKey, ProviderOutcome? outcome, IntentActor actor, long? expectedRevision = null)
            : base(PlatformServiceCapability.CloudSave, actor, expectedRevision)
        {
            if (string.IsNullOrEmpty(idempotencyKey)) throw new ArgumentException("An idempotency key is required.", nameof(idempotencyKey));
            Key = key ?? string.Empty;
            Payload = payload ?? Array.Empty<byte>();
            IdempotencyKey = idempotencyKey;
            Outcome = outcome;
        }

        /// <summary>An opaque, consumer-defined cloud key. Never validated or classified by the
        /// Core.</summary>
        public string Key { get; }
        /// <summary>An opaque payload. The Core never inspects, parses, or serializes it; it may be
        /// the same bytes a local save's document holds (the Unity adapter's
        /// <c>IPersistedDocumentSource</c> seam), but the Core holds no dependency on that shape.</summary>
        public byte[] Payload { get; }
        public string IdempotencyKey { get; }
        public ProviderOutcome? Outcome { get; }

        public override string Describe() => $"cloud_write '{Key}' ({Payload.Length} bytes) key={IdempotencyKey} outcome={(Outcome.HasValue ? Outcome.Value.ToString() : "offline")}";

        internal override PlatformServicesResult<PlatformServicesState> ApplyTo(PlatformServicesState state) => state.ApplyCloudWrite(Key, Payload, IdempotencyKey, Outcome, Actor);
    }

    public sealed class CloudReadIntent : PlatformServicesIntent
    {
        public CloudReadIntent(string key) : this(key, IntentActor.Player, null) { }

        public CloudReadIntent(string key, IntentActor actor, long? expectedRevision = null)
            : base(PlatformServiceCapability.CloudSave, actor, expectedRevision)
        {
            Key = key ?? string.Empty;
        }

        public string Key { get; }

        public override string Describe() => $"cloud_read '{Key}'";

        internal override PlatformServicesResult<PlatformServicesState> ApplyTo(PlatformServicesState state) => state.ApplyCloudRead(Key);
    }

    /// <summary>One write queued because the provider was never reached: enough to resolve it later
    /// with exactly the same effect an immediate write with the same outcome would have had.</summary>
    public sealed class PendingWrite
    {
        private PendingWrite(string idempotencyKey, PlatformServiceCapability capability, string targetId, long? score, LeaderboardUploadPolicy? uploadPolicy, byte[] payload, IntentActor actor)
        {
            IdempotencyKey = idempotencyKey;
            Capability = capability;
            TargetId = targetId;
            Score = score;
            UploadPolicy = uploadPolicy;
            Payload = payload;
            Actor = actor;
        }

        public string IdempotencyKey { get; }
        public PlatformServiceCapability Capability { get; }
        /// <summary>The achievement id, leaderboard id, or cloud key this write targets.</summary>
        public string TargetId { get; }
        /// <summary>Meaningful only when <see cref="Capability"/> is <see cref="PlatformServiceCapability.Leaderboard"/>.</summary>
        public long? Score { get; }
        public LeaderboardUploadPolicy? UploadPolicy { get; }
        /// <summary>Meaningful only when <see cref="Capability"/> is <see cref="PlatformServiceCapability.CloudSave"/>.</summary>
        public byte[] Payload { get; }
        public IntentActor Actor { get; }

        internal static PendingWrite ForAchievement(string idempotencyKey, string achievementId, IntentActor actor) =>
            new PendingWrite(idempotencyKey, PlatformServiceCapability.Achievement, achievementId, null, null, null, actor);

        internal static PendingWrite ForScore(string idempotencyKey, string leaderboardId, long score, LeaderboardUploadPolicy policy, IntentActor actor) =>
            new PendingWrite(idempotencyKey, PlatformServiceCapability.Leaderboard, leaderboardId, score, policy, null, actor);

        internal static PendingWrite ForCloudWrite(string idempotencyKey, string key, byte[] payload, IntentActor actor) =>
            new PendingWrite(idempotencyKey, PlatformServiceCapability.CloudSave, key, null, null, payload, actor);
    }

    /// <summary>One idempotency key's final recorded outcome, kept so a resubmission returns the
    /// originally recorded result without reapplying it, and a resubmission with a different outcome
    /// is rejected with <see cref="PlatformServicesFailure.DispatchMismatch"/>.</summary>
    public sealed class ResolvedWriteRecord
    {
        public ResolvedWriteRecord(string outcomeCode, string message)
        {
            OutcomeCode = outcomeCode ?? string.Empty;
            Message = message ?? string.Empty;
        }

        /// <summary>Empty for an accepted write; otherwise <see cref="PlatformServicesFailure.NotEntitled"/>
        /// or <see cref="PlatformServicesFailure.RateLimited"/>.</summary>
        public string OutcomeCode { get; }
        public string Message { get; }
    }

    /// <summary>The outcome of one intent inside a sequence. Together, the outcomes of a sequence are
    /// the replay log: <see cref="Actor"/> (the issuer, from the intent) and
    /// <see cref="RevisionAfter"/> (the state's revision immediately after this outcome — the new
    /// state's revision when accepted, the unchanged state's revision when rejected) record who did
    /// what and at which revision, for every intent, accepted or rejected, in order.</summary>
    public sealed class PlatformServicesIntentOutcome
    {
        public PlatformServicesIntentOutcome(int index, PlatformServicesIntent intent, bool accepted, string code, string message, long revisionAfter)
        {
            Index = index;
            Intent = intent;
            Accepted = accepted;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            RevisionAfter = revisionAfter;
        }

        public int Index { get; }
        public PlatformServicesIntent Intent { get; }
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
    public sealed class PlatformServicesSequenceResult
    {
        public PlatformServicesSequenceResult(PlatformServicesState state, IReadOnlyList<PlatformServicesIntentOutcome> outcomes)
        {
            State = state;
            Outcomes = outcomes;
        }

        public PlatformServicesState State { get; }
        public IReadOnlyList<PlatformServicesIntentOutcome> Outcomes { get; }

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
