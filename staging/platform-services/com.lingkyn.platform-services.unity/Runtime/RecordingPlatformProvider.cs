using System;
using System.Collections.Generic;
using Lingkyn.PlatformServices.Core;

namespace Lingkyn.PlatformServices.Unity
{
    /// <summary>An in-memory test double: maintains its own account, achievement set, leaderboard,
    /// and cloud store, and records exactly which call it received (capability, target, idempotency
    /// key) so an EditMode test can assert exactly what was sent without a network or a vendor SDK.
    /// <see cref="Connected"/> and <see cref="NextOutcome"/> are set by the test before each call.</summary>
    public sealed class RecordingPlatformProvider : IPlatformServicesProvider
    {
        private readonly List<string> _receivedCalls = new List<string>();
        private readonly HashSet<string> _unlockedAchievements = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, long> _leaderboardStore = new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly Dictionary<string, byte[]> _cloudStore = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        public IReadOnlyList<string> ReceivedCalls => _receivedCalls;
        public IReadOnlyCollection<string> UnlockedAchievements => _unlockedAchievements;
        public IReadOnlyDictionary<string, long> LeaderboardStore => _leaderboardStore;
        public IReadOnlyDictionary<string, byte[]> CloudStore => _cloudStore;

        /// <summary>Whether the next call reaches this fake at all. Set to false to simulate the
        /// provider being unreachable (an ordinary offline condition).</summary>
        public bool Connected { get; set; } = true;
        /// <summary>The outcome the next write call reports once <see cref="Connected"/> is true.</summary>
        public ProviderOutcome NextOutcome { get; set; } = ProviderOutcome.Accepted;
        public bool Entitled { get; set; } = true;
        public AccountId AccountId { get; set; } = AccountId.Parse("player.fake");

        public PlatformServicesProviderCallResult CheckEntitlement()
        {
            _receivedCalls.Add("check_entitlement");
            return Connected ? PlatformServicesProviderCallResult.ForEntitlement(AccountId, Entitled) : PlatformServicesProviderCallResult.Unreachable();
        }

        public PlatformServicesProviderCallResult Unlock(string achievementId, string idempotencyKey)
        {
            _receivedCalls.Add($"unlock:{achievementId}:{idempotencyKey}");
            if (!Connected) return PlatformServicesProviderCallResult.Unreachable();
            if (NextOutcome == ProviderOutcome.Accepted) _unlockedAchievements.Add(achievementId ?? string.Empty);
            return PlatformServicesProviderCallResult.ForWrite(NextOutcome);
        }

        public PlatformServicesProviderCallResult ReportScore(string leaderboardId, long score, LeaderboardUploadPolicy uploadPolicy, string idempotencyKey)
        {
            _receivedCalls.Add($"report_score:{leaderboardId}:{score}:{uploadPolicy}:{idempotencyKey}");
            if (!Connected) return PlatformServicesProviderCallResult.Unreachable();
            if (NextOutcome == ProviderOutcome.Accepted) _leaderboardStore[leaderboardId ?? string.Empty] = score;
            return PlatformServicesProviderCallResult.ForWrite(NextOutcome);
        }

        public PlatformServicesProviderCallResult ReadLeaderboard(string leaderboardId, LeaderboardRange range)
        {
            _receivedCalls.Add($"read_leaderboard:{leaderboardId}:{range}");
            if (!Connected) return PlatformServicesProviderCallResult.Unreachable();
            _leaderboardStore.TryGetValue(leaderboardId ?? string.Empty, out var score);
            return PlatformServicesProviderCallResult.ForLeaderboardRead(score);
        }

        public PlatformServicesProviderCallResult CloudWrite(string key, byte[] payload, string idempotencyKey)
        {
            _receivedCalls.Add($"cloud_write:{key}:{idempotencyKey}");
            if (!Connected) return PlatformServicesProviderCallResult.Unreachable();
            if (NextOutcome == ProviderOutcome.Accepted) _cloudStore[key ?? string.Empty] = payload;
            return PlatformServicesProviderCallResult.ForWrite(NextOutcome);
        }

        public PlatformServicesProviderCallResult CloudRead(string key)
        {
            _receivedCalls.Add($"cloud_read:{key}");
            if (!Connected) return PlatformServicesProviderCallResult.Unreachable();
            _cloudStore.TryGetValue(key ?? string.Empty, out var payload);
            return PlatformServicesProviderCallResult.ForCloudRead(payload);
        }
    }
}
