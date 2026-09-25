using System.Linq;
using System.Text;

namespace Lingkyn.PlatformServices.Core.Samples
{
    /// <summary>
    /// Domain-only walkthrough of the Platform Services Core: a provider registry built by explicit
    /// registration, a composed initial state, a full check_entitlement/unlock/report_score/
    /// read_leaderboard/cloud_write/cloud_read intent sequence including an offline queue-and-resolve
    /// pass, and a replay that proves the final state is deterministic. No vendor SDK, asset, or
    /// scene is involved.
    /// </summary>
    public static class PlatformServicesWalkthroughSample
    {
        public static string Run()
        {
            var report = new StringBuilder();

            // 1. Register one provider that declares every capability this walkthrough exercises,
            //    with a positive cloud_save payload-size guard rail. An invalid declaration or a
            //    duplicate id is rejected before it ever reaches the registry.
            var registryBuilder = new ProviderRegistryBuilder();
            var providerId = ProviderId.Parse("meta_horizon_platform");
            registryBuilder.Register(providerId, new[]
            {
                PlatformServiceCapability.Entitlement,
                PlatformServiceCapability.Achievement,
                PlatformServiceCapability.Leaderboard,
                PlatformServiceCapability.CloudSave,
            }, 1_000_000);
            var registry = registryBuilder.Build();
            report.AppendLine("registry: " + registry.Count + " provider(s), fingerprint length " + registry.Fingerprint().Length);

            // 2. Compose the initial state: the active provider must declare every capability this
            //    walkthrough will require, checked once, before any intent is issued.
            var composed = PlatformServicesState.Initial(registry, providerId, new[]
            {
                PlatformServiceCapability.Entitlement,
                PlatformServiceCapability.Achievement,
                PlatformServiceCapability.Leaderboard,
                PlatformServiceCapability.CloudSave,
            });
            report.AppendLine("composition: " + (composed.Succeeded ? "accepted" : composed.Code + " " + composed.Message));
            var state = composed.Value;

            // 3. Resolve identity, then drive a full intent sequence: an achievement unlocked
            //    immediately, a score reported, a leaderboard read, and a cloud write issued while the
            //    provider is unreachable (queued offline), then resolved on a later reconnection.
            var player = AccountId.Parse("player.one");
            state = Apply(report, state, new CheckEntitlementIntent(player, true));
            state = Apply(report, state, new UnlockIntent("grab.master", "key-unlock-1", ProviderOutcome.Accepted));
            state = Apply(report, state, new ReportScoreIntent("weekly", 1200, LeaderboardUploadPolicy.KeepBest, "key-score-1", ProviderOutcome.Accepted));
            state = Apply(report, state, new ReadLeaderboardIntent("weekly", LeaderboardRange.Top));
            state = Apply(report, state, new CloudWriteIntent("save.slot.0", new byte[] { 1, 2, 3 }, "key-cloud-1"));
            report.AppendLine("pending offline writes: " + state.PendingWrites.Count);
            state = Apply(report, state, new CloudWriteIntent("save.slot.0", new byte[] { 1, 2, 3 }, "key-cloud-1", ProviderOutcome.Accepted));
            report.AppendLine("pending offline writes after resolution: " + state.PendingWrites.Count);

            // 4. Replay the same short sequence from a fresh composed state; the final state and its
            //    fingerprint are equal, which is what "deterministic replay" means.
            PlatformServicesSequenceResult Replay()
            {
                var fresh = PlatformServicesState.Initial(registry, providerId, new[] { PlatformServiceCapability.Entitlement, PlatformServiceCapability.Achievement }).Value;
                return fresh.ApplyAll(new PlatformServicesIntent[]
                {
                    new CheckEntitlementIntent(player, true),
                    new UnlockIntent("grab.master", "key-unlock-1", ProviderOutcome.Accepted),
                });
            }

            var replayA = Replay();
            var replayB = Replay();
            report.AppendLine("deterministic replay: " + (replayA.State.Fingerprint() == replayB.State.Fingerprint() ? "yes" : "no"));
            report.AppendLine("replay outcomes accepted: " + replayA.Outcomes.Count(item => item.Accepted) + "/" + replayA.Outcomes.Count);

            return report.ToString();
        }

        private static PlatformServicesState Apply(StringBuilder report, PlatformServicesState state, PlatformServicesIntent intent)
        {
            var result = state.Apply(intent);
            report.AppendLine("  " + intent.Describe() + ": " + (result.Succeeded ? (string.IsNullOrEmpty(result.Code) ? "accepted" : "accepted (" + result.Code + ")") : result.Code + " " + result.Message));
            return result.Succeeded ? result.Value : state;
        }
    }
}
