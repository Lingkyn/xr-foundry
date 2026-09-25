#if XRFOUNDRY_APPLE_GAME_CENTER
using UnityEngine.SocialPlatforms.GameCenter;
#endif
using Lingkyn.PlatformServices.Core;

namespace Lingkyn.PlatformServices.Unity
{
    /// <summary>Thin shell over Apple Game Center and StoreKit, behind the
    /// <c>XRFOUNDRY_APPLE_GAME_CENTER</c> compile-time define so this assembly compiles with no SDK
    /// present. No vendor SDK is bundled with this package: every call reports
    /// <see cref="PlatformServicesUnityFailure.ProviderSdkMissing"/> until a consumer targets an Apple
    /// platform, adds the Game Center/StoreKit support, and defines the symbol. No vendor SDK type or
    /// namespace this shell does not own appears anywhere else in this assembly.</summary>
    public sealed class AppleGameCenterProvider : IPlatformServicesProvider
    {
#if XRFOUNDRY_APPLE_GAME_CENTER
        private const bool SdkDefinePresent = true;
#else
        private const bool SdkDefinePresent = false;
#endif
        private const string VendorName = "apple_game_center";

        public PlatformServicesProviderCallResult CheckEntitlement() => Missing();
        public PlatformServicesProviderCallResult Unlock(string achievementId, string idempotencyKey) => Missing();
        public PlatformServicesProviderCallResult ReportScore(string leaderboardId, long score, LeaderboardUploadPolicy uploadPolicy, string idempotencyKey) => Missing();
        public PlatformServicesProviderCallResult ReadLeaderboard(string leaderboardId, LeaderboardRange range) => Missing();
        public PlatformServicesProviderCallResult CloudWrite(string key, byte[] payload, string idempotencyKey) => Missing();
        public PlatformServicesProviderCallResult CloudRead(string key) => Missing();

        private static PlatformServicesProviderCallResult Missing()
        {
            if (!SdkDefinePresent)
            {
                return PlatformServicesProviderCallResult.ForDiagnostic(PlatformServicesUnityFailure.ProviderSdkMissing, VendorName);
            }
#if XRFOUNDRY_APPLE_GAME_CENTER
            // Illustrative only; no Game Center/StoreKit support is bundled with this package. A real
            // implementation would build a VendorReadinessGate wired to GameCenterPlatform's own
            // authenticated-local-player callback, then call the vendor's own API — for example
            // Social.ReportProgress() for an achievement, Social.ReportScore() for a leaderboard, and
            // StoreKit's transaction receipt for entitlement — mapping the Core's opaque idempotency
            // key to a ClientSideDedupeStore record, since Game Center exposes no native write-dedupe
            // key for these calls.
            return PlatformServicesProviderCallResult.ForDiagnostic(PlatformServicesUnityFailure.ProviderSdkMissing, VendorName);
#else
            return PlatformServicesProviderCallResult.ForDiagnostic(PlatformServicesUnityFailure.ProviderSdkMissing, VendorName);
#endif
        }
    }
}
