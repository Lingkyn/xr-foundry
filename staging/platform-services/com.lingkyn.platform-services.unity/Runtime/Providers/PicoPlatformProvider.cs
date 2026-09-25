#if XRFOUNDRY_PICO_PLATFORM
using Pico.Platform;
#endif
using Lingkyn.PlatformServices.Core;

namespace Lingkyn.PlatformServices.Unity
{
    /// <summary>Thin shell over the PICO platform SDK, behind the <c>XRFOUNDRY_PICO_PLATFORM</c>
    /// compile-time define so this assembly compiles with no SDK present. No vendor SDK is bundled
    /// with this package: every call reports <see cref="PlatformServicesUnityFailure.ProviderSdkMissing"/>
    /// until a consumer adds the real PICO platform SDK package and defines the symbol. No vendor SDK
    /// type or namespace this shell does not own appears anywhere else in this assembly.</summary>
    public sealed class PicoPlatformProvider : IPlatformServicesProvider
    {
#if XRFOUNDRY_PICO_PLATFORM
        private const bool SdkDefinePresent = true;
#else
        private const bool SdkDefinePresent = false;
#endif
        private const string VendorName = "pico_platform";

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
#if XRFOUNDRY_PICO_PLATFORM
            // Illustrative only; no PICO platform SDK is bundled with this package. A real
            // implementation would build a VendorReadinessGate wired to the PICO SDK's own
            // initialization and login callback, then call the vendor's own achievement/leaderboard/
            // cloud-save API, mapping the Core's opaque idempotency key to that API's own
            // deduplication mechanism or a ClientSideDedupeStore record when none exists.
            return PlatformServicesProviderCallResult.ForDiagnostic(PlatformServicesUnityFailure.ProviderSdkMissing, VendorName);
#else
            return PlatformServicesProviderCallResult.ForDiagnostic(PlatformServicesUnityFailure.ProviderSdkMissing, VendorName);
#endif
        }
    }
}
