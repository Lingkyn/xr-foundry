using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Lingkyn.PlatformServices.Core
{
    /// <summary>
    /// Immutable platform-services state composed over one <see cref="ProviderRegistry"/> and one
    /// active provider selection: per provider, the resolved account id; and, scoped to the active
    /// provider's session, the unlocked-achievement set, the last accepted score per leaderboard, the
    /// cloud key/payload map, the ordered offline pending-write queue, and the resolved-idempotency-key
    /// ledger. Every accepted intent (queued, resolved, or immediately applied) returns a new state;
    /// the prior state stays intact and readable. The same intent sequence over the same registry,
    /// active provider, and initial state always produces an equal final state and an equal
    /// fingerprint.
    /// </summary>
    public sealed class PlatformServicesState : IEquatable<PlatformServicesState>
    {
        private readonly SortedDictionary<ProviderId, AccountId> _resolvedAccountIds;
        private readonly SortedSet<string> _unlockedAchievements;
        private readonly SortedDictionary<string, long> _lastAcceptedScores;
        private readonly SortedDictionary<string, byte[]> _cloudPayloads;
        private readonly List<PendingWrite> _pendingWrites;
        private readonly SortedDictionary<string, ResolvedWriteRecord> _resolvedIdempotencyKeys;

        private PlatformServicesState(
            ProviderRegistry registry,
            ProviderId activeProviderId,
            ProviderDescriptor activeProviderDescriptor,
            SortedDictionary<ProviderId, AccountId> resolvedAccountIds,
            SortedSet<string> unlockedAchievements,
            SortedDictionary<string, long> lastAcceptedScores,
            SortedDictionary<string, byte[]> cloudPayloads,
            List<PendingWrite> pendingWrites,
            SortedDictionary<string, ResolvedWriteRecord> resolvedIdempotencyKeys,
            long revision)
        {
            Registry = registry;
            ActiveProviderId = activeProviderId;
            ActiveProviderDescriptor = activeProviderDescriptor;
            _resolvedAccountIds = resolvedAccountIds;
            _unlockedAchievements = unlockedAchievements;
            _lastAcceptedScores = lastAcceptedScores;
            _cloudPayloads = cloudPayloads;
            _pendingWrites = pendingWrites;
            _resolvedIdempotencyKeys = resolvedIdempotencyKeys;
            Revision = revision;
        }

        public ProviderRegistry Registry { get; }
        public ProviderId ActiveProviderId { get; }
        public ProviderDescriptor ActiveProviderDescriptor { get; }

        /// <summary>Monotonically increasing: 0 for <see cref="Initial"/>, and one higher on every
        /// accepted intent — including a read and a fresh offline enqueue — except an idempotent
        /// replay hit, which returns the unchanged state and revision it already had (LESSON-011).
        /// Not part of <see cref="Fingerprint"/>: two states with the same net projections still have
        /// equal fingerprints even when they reached different revisions.</summary>
        public long Revision { get; }

        public bool TryGetResolvedAccountId(ProviderId providerId, out AccountId accountId) => _resolvedAccountIds.TryGetValue(providerId, out accountId);
        public bool IsAchievementUnlocked(string achievementId) => _unlockedAchievements.Contains(achievementId ?? string.Empty);
        public bool TryGetLastAcceptedScore(string leaderboardId, out long score) => _lastAcceptedScores.TryGetValue(leaderboardId ?? string.Empty, out score);
        public bool TryGetCloudPayload(string key, out byte[] payload) => _cloudPayloads.TryGetValue(key ?? string.Empty, out payload);

        /// <summary>Every write queued because the provider was never reached, in FIFO order (the
        /// order later resolutions apply in).</summary>
        public IReadOnlyList<PendingWrite> PendingWrites => _pendingWrites;

        public bool ProviderSupports(PlatformServiceCapability capability) => ActiveProviderDescriptor.Supports(capability);

        /// <summary>Composes the initial state: the active provider must be registered and must
        /// declare every capability this consumer will require, checked once, before any intent can
        /// be issued against this composition. A missing provider or an undeclared required
        /// capability is rejected with <see cref="PlatformServicesFailure.CapabilityUnsupported"/>,
        /// naming the capability and the provider.</summary>
        public static PlatformServicesResult<PlatformServicesState> Initial(
            ProviderRegistry registry,
            ProviderId activeProviderId,
            IEnumerable<PlatformServiceCapability> requiredCapabilities)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (requiredCapabilities == null) throw new ArgumentNullException(nameof(requiredCapabilities));
            if (!registry.TryGet(activeProviderId, out var descriptor))
            {
                return PlatformServicesResult<PlatformServicesState>.Fail(
                    PlatformServicesFailure.CapabilityUnsupported,
                    $"Provider '{activeProviderId}' is not registered in the composed registry; composition rejected before any intent can be issued.");
            }
            foreach (var capability in requiredCapabilities)
            {
                if (!descriptor.Supports(capability))
                {
                    return PlatformServicesResult<PlatformServicesState>.Fail(
                        PlatformServicesFailure.CapabilityUnsupported,
                        $"Provider '{activeProviderId}' does not declare capability '{PlatformServiceCapabilities.Name(capability)}'; composition rejected before any intent can be issued.");
                }
            }
            return PlatformServicesResult<PlatformServicesState>.Ok(new PlatformServicesState(
                registry,
                activeProviderId,
                descriptor,
                new SortedDictionary<ProviderId, AccountId>(),
                new SortedSet<string>(StringComparer.Ordinal),
                new SortedDictionary<string, long>(StringComparer.Ordinal),
                new SortedDictionary<string, byte[]>(StringComparer.Ordinal),
                new List<PendingWrite>(),
                new SortedDictionary<string, ResolvedWriteRecord>(StringComparer.Ordinal),
                0));
        }

        /// <summary>Applies one intent. Checked in exactly two steps, in this order, for every intent
        /// regardless of <see cref="PlatformServicesIntent.Actor"/> (LESSON-011: the actor is never a
        /// second validation rule): first, a non-null <see cref="PlatformServicesIntent.ExpectedRevision"/>
        /// that does not match <see cref="Revision"/> is rejected with
        /// <see cref="PlatformServicesFailure.StateStale"/> and changes nothing — the intent's own
        /// <see cref="PlatformServicesIntent.ApplyTo"/> never runs; second, the intent's own rule
        /// applies.</summary>
        public PlatformServicesResult<PlatformServicesState> Apply(PlatformServicesIntent intent)
        {
            if (intent == null) throw new ArgumentNullException(nameof(intent));
            if (intent.ExpectedRevision.HasValue && intent.ExpectedRevision.Value != Revision)
            {
                return PlatformServicesResult<PlatformServicesState>.Fail(
                    PlatformServicesFailure.StateStale,
                    $"Intent expected revision {intent.ExpectedRevision.Value} but the state is at revision {Revision}; nothing changed.");
            }
            return intent.ApplyTo(this);
        }

        /// <summary>Applies every intent in order. A rejected intent is recorded and the state it
        /// found is kept for the next intent in the sequence. The outcomes are the replay log: each
        /// carries its intent's actor and the state's revision immediately after it settled.</summary>
        public PlatformServicesSequenceResult ApplyAll(IEnumerable<PlatformServicesIntent> intents)
        {
            if (intents == null) throw new ArgumentNullException(nameof(intents));
            var state = this;
            var outcomes = new List<PlatformServicesIntentOutcome>();
            var index = 0;
            foreach (var intent in intents)
            {
                var result = state.Apply(intent);
                if (result.Succeeded)
                {
                    state = result.Value;
                    outcomes.Add(new PlatformServicesIntentOutcome(index, intent, true, result.Code, result.Message, state.Revision));
                }
                else
                {
                    outcomes.Add(new PlatformServicesIntentOutcome(index, intent, false, result.Code, result.Message, state.Revision));
                }
                index++;
            }
            return new PlatformServicesSequenceResult(state, outcomes);
        }

        // ----- check_entitlement -----

        internal PlatformServicesResult<PlatformServicesState> ApplyCheckEntitlement(AccountId accountId, bool entitled)
        {
            if (!ProviderSupports(PlatformServiceCapability.Entitlement))
            {
                return Fail(PlatformServicesFailure.CapabilityUnsupported, $"Provider '{ActiveProviderId}' does not declare capability 'entitlement'.");
            }
            if (!entitled)
            {
                return Fail(PlatformServicesFailure.NotEntitled, $"Provider '{ActiveProviderId}' reports account '{accountId}' as not entitled; identity is not resolved.");
            }
            var resolved = new SortedDictionary<ProviderId, AccountId>(_resolvedAccountIds) { [ActiveProviderId] = accountId };
            return Ok(With(resolvedAccountIds: resolved));
        }

        // ----- unlock -----

        internal PlatformServicesResult<PlatformServicesState> ApplyUnlock(string achievementId, string idempotencyKey, ProviderOutcome? outcome, IntentActor actor)
        {
            if (!ProviderSupports(PlatformServiceCapability.Achievement))
            {
                return Fail(PlatformServicesFailure.CapabilityUnsupported, $"Provider '{ActiveProviderId}' does not declare capability 'achievement'.");
            }
            if (!_resolvedAccountIds.ContainsKey(ActiveProviderId))
            {
                return Fail(PlatformServicesFailure.IdentityMissing, $"No account is resolved for provider '{ActiveProviderId}'; issue check_entitlement first.");
            }
            return FoldWrite(
                PlatformServiceCapability.Achievement,
                idempotencyKey,
                outcome,
                () => PendingWrite.ForAchievement(idempotencyKey, achievementId, actor),
                $"Queued unlock for '{achievementId}' with idempotency key '{idempotencyKey}'.",
                accepted =>
                {
                    var achievements = new SortedSet<string>(_unlockedAchievements, StringComparer.Ordinal) { achievementId ?? string.Empty };
                    return With(unlockedAchievements: achievements);
                });
        }

        // ----- report_score -----

        internal PlatformServicesResult<PlatformServicesState> ApplyReportScore(string leaderboardId, long score, LeaderboardUploadPolicy uploadPolicy, string idempotencyKey, ProviderOutcome? outcome, IntentActor actor)
        {
            if (!ProviderSupports(PlatformServiceCapability.Leaderboard))
            {
                return Fail(PlatformServicesFailure.CapabilityUnsupported, $"Provider '{ActiveProviderId}' does not declare capability 'leaderboard'.");
            }
            if (!_resolvedAccountIds.ContainsKey(ActiveProviderId))
            {
                return Fail(PlatformServicesFailure.IdentityMissing, $"No account is resolved for provider '{ActiveProviderId}'; issue check_entitlement first.");
            }
            return FoldWrite(
                PlatformServiceCapability.Leaderboard,
                idempotencyKey,
                outcome,
                () => PendingWrite.ForScore(idempotencyKey, leaderboardId, score, uploadPolicy, actor),
                $"Queued report_score for '{leaderboardId}'={score} with idempotency key '{idempotencyKey}'.",
                accepted =>
                {
                    var key = leaderboardId ?? string.Empty;
                    var scores = new SortedDictionary<string, long>(_lastAcceptedScores, StringComparer.Ordinal);
                    if (uploadPolicy == LeaderboardUploadPolicy.Always || !scores.TryGetValue(key, out var existing) || score > existing)
                    {
                        scores[key] = score;
                    }
                    return With(lastAcceptedScores: scores);
                });
        }

        // ----- read_leaderboard -----

        internal PlatformServicesResult<PlatformServicesState> ApplyReadLeaderboard(string leaderboardId, LeaderboardRange range)
        {
            if (!ProviderSupports(PlatformServiceCapability.Leaderboard))
            {
                return Fail(PlatformServicesFailure.CapabilityUnsupported, $"Provider '{ActiveProviderId}' does not declare capability 'leaderboard'.");
            }
            if (!_resolvedAccountIds.ContainsKey(ActiveProviderId))
            {
                return Fail(PlatformServicesFailure.IdentityMissing, $"No account is resolved for provider '{ActiveProviderId}'; issue check_entitlement first.");
            }
            return Ok(With());
        }

        // ----- cloud_write -----

        internal PlatformServicesResult<PlatformServicesState> ApplyCloudWrite(string key, byte[] payload, string idempotencyKey, ProviderOutcome? outcome, IntentActor actor)
        {
            if (!ProviderSupports(PlatformServiceCapability.CloudSave))
            {
                return Fail(PlatformServicesFailure.CapabilityUnsupported, $"Provider '{ActiveProviderId}' does not declare capability 'cloud_save'.");
            }
            if (!_resolvedAccountIds.ContainsKey(ActiveProviderId))
            {
                return Fail(PlatformServicesFailure.IdentityMissing, $"No account is resolved for provider '{ActiveProviderId}'; issue check_entitlement first.");
            }
            return FoldWrite(
                PlatformServiceCapability.CloudSave,
                idempotencyKey,
                outcome,
                () => PendingWrite.ForCloudWrite(idempotencyKey, key, payload, actor),
                $"Queued cloud_write for '{key}' ({(payload?.Length ?? 0)} bytes) with idempotency key '{idempotencyKey}'.",
                accepted =>
                {
                    var payloads = new SortedDictionary<string, byte[]>(_cloudPayloads, StringComparer.Ordinal) { [key ?? string.Empty] = payload ?? Array.Empty<byte>() };
                    return With(cloudPayloads: payloads);
                });
        }

        // ----- cloud_read -----

        internal PlatformServicesResult<PlatformServicesState> ApplyCloudRead(string key)
        {
            if (!ProviderSupports(PlatformServiceCapability.CloudSave))
            {
                return Fail(PlatformServicesFailure.CapabilityUnsupported, $"Provider '{ActiveProviderId}' does not declare capability 'cloud_save'.");
            }
            if (!_resolvedAccountIds.ContainsKey(ActiveProviderId))
            {
                return Fail(PlatformServicesFailure.IdentityMissing, $"No account is resolved for provider '{ActiveProviderId}'; issue check_entitlement first.");
            }
            return Ok(With());
        }

        // ----- shared write fold: idempotency, the offline queue, and outcome folding -----

        /// <summary>Shared by <see cref="ApplyUnlock"/>, <see cref="ApplyReportScore"/>, and
        /// <see cref="ApplyCloudWrite"/>. In order: (1) a key already resolved returns the originally
        /// recorded result without reapplying it, or <see cref="PlatformServicesFailure.DispatchMismatch"/>
        /// when the newly supplied outcome differs; (2) a null outcome enqueues (or, for a key already
        /// queued, is rejected as a mismatch — the same offline write must not be queued twice); (3) an
        /// outcome resolves the write, removing exactly the matching queued entry (if any) in FIFO
        /// order relative to other resolutions, folding <see cref="ProviderOutcome.Accepted"/> into
        /// <paramref name="applyAcceptedProjection"/> and any other outcome into no projection change,
        /// and records the resolution so a later resubmission with the same key is idempotent.</summary>
        private PlatformServicesResult<PlatformServicesState> FoldWrite(
            PlatformServiceCapability capability,
            string idempotencyKey,
            ProviderOutcome? outcome,
            Func<PendingWrite> makePendingEntry,
            string queuedMessage,
            Func<bool, PlatformServicesState> applyAcceptedProjection)
        {
            if (_resolvedIdempotencyKeys.TryGetValue(idempotencyKey, out var recorded))
            {
                if (!outcome.HasValue || OutcomeCode(outcome.Value) != recorded.OutcomeCode)
                {
                    return Fail(PlatformServicesFailure.DispatchMismatch, $"Idempotency key '{idempotencyKey}' already resolved to '{(string.IsNullOrEmpty(recorded.OutcomeCode) ? "accepted" : recorded.OutcomeCode)}'; cannot resolve again with a different outcome.");
                }
                return string.IsNullOrEmpty(recorded.OutcomeCode)
                    ? Ok(this)
                    : OkWithCode(this, recorded.OutcomeCode, recorded.Message);
            }

            var pendingIndex = _pendingWrites.FindIndex(entry => string.Equals(entry.IdempotencyKey, idempotencyKey, StringComparison.Ordinal));

            if (!outcome.HasValue)
            {
                if (pendingIndex >= 0)
                {
                    return Fail(PlatformServicesFailure.DispatchMismatch, $"Idempotency key '{idempotencyKey}' is already queued offline.");
                }
                var pending = new List<PendingWrite>(_pendingWrites) { makePendingEntry() };
                return OkWithCode(With(pendingWrites: pending), PlatformServicesFailure.Offline, queuedMessage);
            }

            var pendingWrites = pendingIndex >= 0 ? RemoveAt(_pendingWrites, pendingIndex) : new List<PendingWrite>(_pendingWrites);
            var code = OutcomeCode(outcome.Value);
            var resolved = new SortedDictionary<string, ResolvedWriteRecord>(_resolvedIdempotencyKeys, StringComparer.Ordinal)
            {
                [idempotencyKey] = new ResolvedWriteRecord(code, string.Empty),
            };

            if (outcome.Value == ProviderOutcome.Accepted)
            {
                var accepted = applyAcceptedProjection(true);
                return Ok(accepted.With(pendingWrites: pendingWrites, resolvedIdempotencyKeys: resolved, bumpRevision: false));
            }

            var unaffected = With(pendingWrites: pendingWrites, resolvedIdempotencyKeys: resolved);
            return OkWithCode(unaffected, code, $"Provider reported '{code}' for idempotency key '{idempotencyKey}'; no projection was updated.");
        }

        private static string OutcomeCode(ProviderOutcome outcome) => outcome switch
        {
            ProviderOutcome.Accepted => string.Empty,
            ProviderOutcome.NotEntitled => PlatformServicesFailure.NotEntitled,
            ProviderOutcome.RateLimited => PlatformServicesFailure.RateLimited,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Not a member of the closed provider-outcome set."),
        };

        private static List<PendingWrite> RemoveAt(List<PendingWrite> source, int index)
        {
            var copy = new List<PendingWrite>(source);
            copy.RemoveAt(index);
            return copy;
        }

        // ----- helpers -----

        private static PlatformServicesResult<PlatformServicesState> Ok(PlatformServicesState state) => PlatformServicesResult<PlatformServicesState>.Ok(state);
        private static PlatformServicesResult<PlatformServicesState> OkWithCode(PlatformServicesState state, string code, string message) => PlatformServicesResult<PlatformServicesState>.OkWithCode(state, code, message);
        private static PlatformServicesResult<PlatformServicesState> Fail(string code, string message) => PlatformServicesResult<PlatformServicesState>.Fail(code, message);

        /// <summary>Returns a new state carrying the given overrides and every other field unchanged,
        /// with the revision one higher unless <paramref name="bumpRevision"/> is false (used only by
        /// the accepted branch of <see cref="FoldWrite"/>, which already bumped the revision inside
        /// <paramref name="applyAcceptedProjection"/>'s own recursive <c>With</c> call).</summary>
        private PlatformServicesState With(
            SortedDictionary<ProviderId, AccountId> resolvedAccountIds = null,
            SortedSet<string> unlockedAchievements = null,
            SortedDictionary<string, long> lastAcceptedScores = null,
            SortedDictionary<string, byte[]> cloudPayloads = null,
            List<PendingWrite> pendingWrites = null,
            SortedDictionary<string, ResolvedWriteRecord> resolvedIdempotencyKeys = null,
            bool bumpRevision = true) =>
            new PlatformServicesState(
                Registry,
                ActiveProviderId,
                ActiveProviderDescriptor,
                resolvedAccountIds ?? _resolvedAccountIds,
                unlockedAchievements ?? _unlockedAchievements,
                lastAcceptedScores ?? _lastAcceptedScores,
                cloudPayloads ?? _cloudPayloads,
                pendingWrites ?? _pendingWrites,
                resolvedIdempotencyKeys ?? _resolvedIdempotencyKeys,
                bumpRevision ? Revision + 1 : Revision);

        /// <summary>A canonical text of the whole state: the provider-registry fingerprint, the
        /// resolved account id per provider, the unlocked-achievement set, the last accepted score per
        /// leaderboard, the cloud key/payload map, and the pending-write queue's contents, each in
        /// canonical key order. <see cref="Revision"/> and the resolved-idempotency-key ledger are
        /// excluded: two states reached by different intent sequences with the same net projections
        /// have equal fingerprints even when they reached different revisions or resolved different
        /// idempotency-key histories to get there.</summary>
        public string Fingerprint()
        {
            var builder = new StringBuilder();
            builder.Append("registry[").Append(Registry.Fingerprint()).Append(']');
            builder.Append(" active_provider[").Append(ActiveProviderId).Append(']');
            builder.Append(" accounts[");
            foreach (var pair in _resolvedAccountIds)
            {
                builder.Append(pair.Key).Append('=').Append(pair.Value).Append(';');
            }
            builder.Append(']');
            builder.Append(" achievements[").Append(string.Join(",", _unlockedAchievements)).Append(']');
            builder.Append(" scores[");
            foreach (var pair in _lastAcceptedScores)
            {
                builder.Append(pair.Key).Append('=').Append(pair.Value.ToString(CultureInfo.InvariantCulture)).Append(';');
            }
            builder.Append(']');
            builder.Append(" cloud[");
            foreach (var pair in _cloudPayloads)
            {
                builder.Append(pair.Key).Append('=').Append(Convert.ToBase64String(pair.Value)).Append(';');
            }
            builder.Append(']');
            builder.Append(" pending[");
            foreach (var entry in _pendingWrites.OrderBy(entry => entry.IdempotencyKey, StringComparer.Ordinal))
            {
                builder.Append(entry.IdempotencyKey).Append('=').Append(entry.Capability).Append(':').Append(entry.TargetId).Append(';');
            }
            builder.Append(']');
            return builder.ToString();
        }

        public bool Equals(PlatformServicesState other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return ReferenceEquals(Registry, other.Registry) && string.Equals(Fingerprint(), other.Fingerprint(), StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is PlatformServicesState other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Fingerprint());
        public override string ToString() => Fingerprint();
    }
}
