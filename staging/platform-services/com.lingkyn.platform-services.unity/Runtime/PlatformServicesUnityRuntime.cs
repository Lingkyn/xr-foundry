using System;
using System.Collections.Generic;
using Lingkyn.PlatformServices.Core;

namespace Lingkyn.PlatformServices.Unity
{
    // A plain runtime constructed with explicit references (an initial Core state, an injected
    // IPlatformServicesProvider, and an injected connectivity signal) that turns every capability
    // call into exactly one Core intent. No scene singleton, static instance, scene search, or
    // reflection discovery resolves any of this runtime's references; no per-frame update loop drives
    // it — only the explicit calls it receives.

    public sealed class PlatformServicesUnityRuntime
    {
        private readonly IPlatformServicesProvider _provider;
        private readonly IPlatformServicesConnectivitySignal _connectivity;
        private readonly ClientSideDedupeStore _dedupe = new ClientSideDedupeStore();
        private readonly List<PlatformServicesIntentOutcome> _outcomes = new List<PlatformServicesIntentOutcome>();
        private bool _consumerFallbackApplied;

        public PlatformServicesUnityRuntime(PlatformServicesState initialState, IPlatformServicesProvider provider, IPlatformServicesConnectivitySignal connectivity)
        {
            State = initialState ?? throw new ArgumentNullException(nameof(initialState));
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _connectivity = connectivity ?? throw new ArgumentNullException(nameof(connectivity));
        }

        public PlatformServicesState State { get; private set; }

        /// <summary>Every intent this runtime saw, accepted, rejected, or gate-blocked, in the order
        /// it saw them.</summary>
        public IReadOnlyList<PlatformServicesIntentOutcome> Outcomes => _outcomes;

        /// <summary>True once the Core state carries a resolved account id for the active provider
        /// (an accepted <see cref="CheckEntitlement"/>, on this runtime or an earlier one over the
        /// same state), or the consumer has explicitly applied its own fallback with
        /// <see cref="ApplyConsumerOwnedFallback"/>. Derived from the Core state rather than a
        /// separately tracked flag, so a runtime reconstructed over an already-entitled state (for
        /// example after a reconnect) does not need to re-issue check_entitlement. False by default
        /// (fails closed): no default-entitled fallback exists inside this adapter.</summary>
        public bool EntitlementGateOpen => State.TryGetResolvedAccountId(State.ActiveProviderId, out _) || _consumerFallbackApplied;

        /// <summary>The only way past a closed gate: a consumer-owned decision the consumer explicitly
        /// applies (for example, a single-player offline mode the game itself decides to allow). This
        /// adapter never calls this on its own, never loads a scene, and never calls
        /// <c>Application.Quit</c>; the consumer decides and wires the fallback intent itself.</summary>
        public void ApplyConsumerOwnedFallback() => _consumerFallbackApplied = true;

        /// <summary>Issues check_entitlement before any other capability's intents are accepted. While
        /// this has not yet been accepted, every other method on this runtime is rejected with
        /// <see cref="PlatformServicesUnityFailure.EntitlementGateBlocked"/> before it reaches the
        /// Core or the provider.</summary>
        public PlatformServicesIntentOutcome CheckEntitlement(IntentActor actor = IntentActor.Player)
        {
            var call = _provider.CheckEntitlement();
            if (call.HasDiagnostic) return DiagnosticOutcome(new CheckEntitlementIntent(default, false, actor), call);
            var intent = call.Connected
                ? (PlatformServicesIntent)new CheckEntitlementIntent(call.AccountId, call.Entitled, actor)
                : new CheckEntitlementIntent(default, false, actor);
            return Dispatch(intent);
        }

        public PlatformServicesIntentOutcome Unlock(string achievementId, string idempotencyKey, IntentActor actor = IntentActor.Player)
        {
            if (!EntitlementGateOpen) return Blocked(new UnlockIntent(achievementId, idempotencyKey, null, actor));
            return DispatchWrite(idempotencyKey, () => _provider.Unlock(achievementId, idempotencyKey), outcome => new UnlockIntent(achievementId, idempotencyKey, outcome, actor));
        }

        public PlatformServicesIntentOutcome ReportScore(string leaderboardId, long score, LeaderboardUploadPolicy uploadPolicy, string idempotencyKey, IntentActor actor = IntentActor.Player)
        {
            if (!EntitlementGateOpen) return Blocked(new ReportScoreIntent(leaderboardId, score, uploadPolicy, idempotencyKey, null, actor));
            return DispatchWrite(idempotencyKey, () => _provider.ReportScore(leaderboardId, score, uploadPolicy, idempotencyKey), outcome => new ReportScoreIntent(leaderboardId, score, uploadPolicy, idempotencyKey, outcome, actor));
        }

        public PlatformServicesIntentOutcome ReadLeaderboard(string leaderboardId, LeaderboardRange range, IntentActor actor = IntentActor.Player)
        {
            var intent = new ReadLeaderboardIntent(leaderboardId, range, actor);
            if (!EntitlementGateOpen) return Blocked(intent);
            var call = _provider.ReadLeaderboard(leaderboardId, range);
            if (call.HasDiagnostic) return DiagnosticOutcome(intent, call);
            return Dispatch(intent);
        }

        public PlatformServicesIntentOutcome CloudWrite(string key, byte[] payload, string idempotencyKey, IntentActor actor = IntentActor.Player)
        {
            if (!EntitlementGateOpen) return Blocked(new CloudWriteIntent(key, payload, idempotencyKey, null, actor));
            return DispatchWrite(idempotencyKey, () => _provider.CloudWrite(key, payload, idempotencyKey), outcome => new CloudWriteIntent(key, payload, idempotencyKey, outcome, actor));
        }

        public PlatformServicesIntentOutcome CloudRead(string key, IntentActor actor = IntentActor.Player)
        {
            var intent = new CloudReadIntent(key, actor);
            if (!EntitlementGateOpen) return Blocked(intent);
            var call = _provider.CloudRead(key);
            if (call.HasDiagnostic) return DiagnosticOutcome(intent, call);
            return Dispatch(intent);
        }

        /// <summary>Resolves every write currently queued offline by re-dispatching it to the
        /// provider. Runs only from this explicit call — never from a per-frame poll — so a consumer
        /// wires it to its own reconnection event.</summary>
        public IReadOnlyList<PlatformServicesIntentOutcome> DrainPendingWrites(IntentActor actor = IntentActor.Player)
        {
            var results = new List<PlatformServicesIntentOutcome>();
            foreach (var pending in State.PendingWrites)
            {
                switch (pending.Capability)
                {
                    case PlatformServiceCapability.Achievement:
                        results.Add(Unlock(pending.TargetId, pending.IdempotencyKey, actor));
                        break;
                    case PlatformServiceCapability.Leaderboard:
                        results.Add(ReportScore(pending.TargetId, pending.Score ?? 0, pending.UploadPolicy ?? LeaderboardUploadPolicy.KeepBest, pending.IdempotencyKey, actor));
                        break;
                    case PlatformServiceCapability.CloudSave:
                        results.Add(CloudWrite(pending.TargetId, pending.Payload, pending.IdempotencyKey, actor));
                        break;
                }
            }
            return results;
        }

        /// <summary>Shared by every write capability: consults the explicit connectivity signal once
        /// (never polled), never sends a retried call for an idempotency key already sent to the
        /// provider (the client-side dedupe record), and always routes through the Core's one
        /// <see cref="PlatformServicesState.Apply"/> entry point.</summary>
        private PlatformServicesIntentOutcome DispatchWrite(string idempotencyKey, Func<PlatformServicesProviderCallResult> callProvider, Func<ProviderOutcome?, PlatformServicesIntent> makeIntent)
        {
            if (!_connectivity.IsOnline)
            {
                return Dispatch(makeIntent(null));
            }
            if (_dedupe.TryGetPrior(idempotencyKey, out var prior))
            {
                if (prior.HasDiagnostic) return DiagnosticOutcome(makeIntent(null), prior);
                return Dispatch(makeIntent(prior.Connected ? prior.Outcome : null));
            }
            var call = callProvider();
            if (call.HasDiagnostic)
            {
                _dedupe.Record(idempotencyKey, call);
                return DiagnosticOutcome(makeIntent(null), call);
            }
            if (!call.Connected)
            {
                return Dispatch(makeIntent(null));
            }
            _dedupe.Record(idempotencyKey, call);
            return Dispatch(makeIntent(call.Outcome));
        }

        private PlatformServicesIntentOutcome Dispatch(PlatformServicesIntent intent)
        {
            var index = _outcomes.Count;
            var result = State.Apply(intent);
            PlatformServicesIntentOutcome outcome;
            if (result.Succeeded)
            {
                State = result.Value;
                outcome = new PlatformServicesIntentOutcome(index, intent, true, result.Code, result.Message, State.Revision);
            }
            else
            {
                outcome = new PlatformServicesIntentOutcome(index, intent, false, result.Code, result.Message, State.Revision);
            }
            _outcomes.Add(outcome);
            return outcome;
        }

        /// <summary>An explicit by-name/optional-resolution failure the provider reported
        /// (LESSON-004): never reaches the Core, never queues offline, and never claims a value the
        /// provider did not actually return.</summary>
        private PlatformServicesIntentOutcome DiagnosticOutcome(PlatformServicesIntent intent, PlatformServicesProviderCallResult call)
        {
            var outcome = new PlatformServicesIntentOutcome(_outcomes.Count, intent, false, call.DiagnosticCode, call.DiagnosticMessage, State.Revision);
            _outcomes.Add(outcome);
            return outcome;
        }

        private PlatformServicesIntentOutcome Blocked(PlatformServicesIntent intent)
        {
            var outcome = new PlatformServicesIntentOutcome(
                _outcomes.Count,
                intent,
                false,
                PlatformServicesUnityFailure.EntitlementGateBlocked,
                $"The entitlement gate is closed for provider '{State.ActiveProviderId}'; issue check_entitlement, or apply a consumer-owned fallback, before any other capability.",
                State.Revision);
            _outcomes.Add(outcome);
            return outcome;
        }
    }
}
