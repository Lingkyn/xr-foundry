#if XRFOUNDRY_STEAMWORKS
using Steamworks;
#endif
using Lingkyn.PlatformServices.Core;

namespace Lingkyn.PlatformServices.Unity
{
    /// <summary>Thin shell over Steamworks, behind the <c>XRFOUNDRY_STEAMWORKS</c> compile-time define
    /// so this assembly compiles with no SDK present. No vendor SDK is bundled with this package:
    /// every call reports <see cref="PlatformServicesUnityFailure.ProviderSdkMissing"/> until a
    /// consumer adds a Steamworks wrapper package and defines the symbol. No vendor SDK type or
    /// namespace this shell does not own appears anywhere else in this assembly.</summary>
    public sealed class SteamworksProvider : IPlatformServicesProvider
    {
#if XRFOUNDRY_STEAMWORKS
        private const bool SdkDefinePresent = true;
#else
        private const bool SdkDefinePresent = false;
#endif
        private const string VendorName = "steamworks";

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
#if XRFOUNDRY_STEAMWORKS
            // Illustrative only; no Steamworks wrapper is bundled with this package. A real
            // implementation would build a VendorReadinessGate wired to SteamAPI.Init() and the Steam
            // overlay callback, then call the vendor's own API — for example
            // SteamUserStats.SetAchievement(achievementId), SteamUserStats.UploadLeaderboardScore(),
            // or SteamRemoteStorage.FileWrite() — mapping the Core's opaque idempotency key to a
            // ClientSideDedupeStore record, since Steamworks exposes no native write-dedupe key.
            return PlatformServicesProviderCallResult.ForDiagnostic(PlatformServicesUnityFailure.ProviderSdkMissing, VendorName);
#else
            return PlatformServicesProviderCallResult.ForDiagnostic(PlatformServicesUnityFailure.ProviderSdkMissing, VendorName);
#endif
        }
    }
}
