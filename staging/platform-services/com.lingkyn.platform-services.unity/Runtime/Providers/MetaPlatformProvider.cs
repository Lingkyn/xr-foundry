#if XRFOUNDRY_META_PLATFORM
using Oculus.Platform;
#endif
using Lingkyn.PlatformServices.Core;

namespace Lingkyn.PlatformServices.Unity
{
    /// <summary>Thin shell over the Meta Horizon Platform SDK, behind the
    /// <c>XRFOUNDRY_META_PLATFORM</c> compile-time define so this assembly compiles with no SDK
    /// present. No vendor SDK is bundled with this package: every call reports
    /// <see cref="PlatformServicesUnityFailure.ProviderSdkMissing"/> until a consumer adds the real
    /// Meta Platform SDK package and defines the symbol. No vendor SDK type or namespace this shell
    /// does not own appears anywhere else in this assembly.</summary>
    public sealed class MetaPlatformProvider : IPlatformServicesProvider
    {
#if XRFOUNDRY_META_PLATFORM
        private const bool SdkDefinePresent = true;
#else
        private const bool SdkDefinePresent = false;
#endif
        private const string VendorName = "meta_horizon_platform";

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
#if XRFOUNDRY_META_PLATFORM
            // Illustrative only; no Meta Platform SDK is bundled with this package. A real
            // implementation would build a VendorReadinessGate wired to the Meta SDK's own
            // Core.IsInitialized() and logged-in-user callback, then call the vendor's own API — for
            // example Entitlements.IsUserEntitledToApplication() or Achievements.Unlock(achievementId)
            // — mapping the Core's opaque idempotency key to Achievements' own request semantics or a
            // ClientSideDedupeStore record when none exists.
            return PlatformServicesProviderCallResult.ForDiagnostic(PlatformServicesUnityFailure.ProviderSdkMissing, VendorName);
#else
            return PlatformServicesProviderCallResult.ForDiagnostic(PlatformServicesUnityFailure.ProviderSdkMissing, VendorName);
#endif
        }
    }
}
