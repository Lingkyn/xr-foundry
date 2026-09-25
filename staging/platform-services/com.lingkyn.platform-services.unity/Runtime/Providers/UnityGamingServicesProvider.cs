#if XRFOUNDRY_UNITY_GAMING_SERVICES
using Unity.Services.CloudSave;
#endif
using Lingkyn.PlatformServices.Core;

namespace Lingkyn.PlatformServices.Unity
{
    /// <summary>Thin shell over Unity Gaming Services Cloud Save, behind the
    /// <c>XRFOUNDRY_UNITY_GAMING_SERVICES</c> compile-time define so this assembly compiles with no
    /// SDK present. This route serves only <c>cloud_save</c>; every other capability call reports
    /// <see cref="PlatformServicesUnityFailure.ProviderSdkMissing"/>, as does every call while the
    /// define is absent. No vendor SDK is bundled with this package, and no vendor SDK type or
    /// namespace this shell does not own appears anywhere else in this assembly.</summary>
    public sealed class UnityGamingServicesProvider : IPlatformServicesProvider
    {
#if XRFOUNDRY_UNITY_GAMING_SERVICES
        private const bool SdkDefinePresent = true;
#else
        private const bool SdkDefinePresent = false;
#endif
        private const string VendorName = "unity_gaming_services";

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
#if XRFOUNDRY_UNITY_GAMING_SERVICES
            // Illustrative only; no Unity Gaming Services package is bundled with this package. A real
            // implementation would build a VendorReadinessGate wired to UnityServices.State and the
            // signed-in-player callback, then call CloudSaveService.Instance.Data.Player.SaveAsync()/
            // LoadAsync(), mapping the Core's opaque idempotency key to a ClientSideDedupeStore record
            // since Cloud Save exposes no native write-dedupe key for a Save call.
            return PlatformServicesProviderCallResult.ForDiagnostic(PlatformServicesUnityFailure.ProviderSdkMissing, VendorName);
#else
            return PlatformServicesProviderCallResult.ForDiagnostic(PlatformServicesUnityFailure.ProviderSdkMissing, VendorName);
#endif
        }
    }
}
