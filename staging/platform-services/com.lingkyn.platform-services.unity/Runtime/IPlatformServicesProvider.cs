using System;
using Lingkyn.PlatformServices.Core;

namespace Lingkyn.PlatformServices.Unity
{
    // The one interface every vendor route (Meta Horizon Platform, PICO, Steam, Apple Game
    // Center, Unity Gaming Services) and the in-memory RecordingPlatformProvider fake implement. The
    // runtime never calls a vendor SDK directly, only through this seam, so tests assert exactly
    // which capability was exercised, which idempotency key was used, and what outcome came back,
    // without a network or a vendor SDK dependency.

    /// <summary>One provider call's result. <see cref="Connected"/> distinguishes "the provider was
    /// reached" from a diagnostic (<see cref="HasDiagnostic"/>): a missing SDK, an unresolved vendor
    /// singleton, or an uninitialized SDK is reported as a diagnostic, never as an ordinary
    /// disconnection the runtime would queue offline.</summary>
    public readonly struct PlatformServicesProviderCallResult
    {
        private PlatformServicesProviderCallResult(bool connected, ProviderOutcome? outcome, AccountId accountId, bool entitled, byte[] payload, long score, string diagnosticCode, string diagnosticMessage)
        {
            Connected = connected;
            Outcome = outcome;
            AccountId = accountId;
            Entitled = entitled;
            Payload = payload;
            Score = score;
            DiagnosticCode = diagnosticCode ?? string.Empty;
            DiagnosticMessage = diagnosticMessage ?? string.Empty;
        }

        /// <summary>False when the provider itself was not reached (an ordinary offline condition,
        /// distinct from a diagnostic).</summary>
        public bool Connected { get; }
        /// <summary>Meaningful for a write call once <see cref="Connected"/> is true.</summary>
        public ProviderOutcome? Outcome { get; }
        /// <summary>Meaningful for <c>check_entitlement</c>.</summary>
        public AccountId AccountId { get; }
        public bool Entitled { get; }
        /// <summary>Meaningful for a cloud_read.</summary>
        public byte[] Payload { get; }
        /// <summary>Meaningful for a leaderboard read.</summary>
        public long Score { get; }
        /// <summary>Empty unless this call reports an explicit by-name/optional-resolution failure
        /// (LESSON-004): a missing SDK, an unresolved vendor singleton, an uninitialized SDK, or an
        /// unregistered callback.</summary>
        public string DiagnosticCode { get; }
        public string DiagnosticMessage { get; }
        public bool HasDiagnostic => !string.IsNullOrEmpty(DiagnosticCode);

        public static PlatformServicesProviderCallResult ForEntitlement(AccountId accountId, bool entitled) =>
            new PlatformServicesProviderCallResult(true, null, accountId, entitled, null, 0, string.Empty, string.Empty);

        public static PlatformServicesProviderCallResult ForWrite(ProviderOutcome outcome) =>
            new PlatformServicesProviderCallResult(true, outcome, default, false, null, 0, string.Empty, string.Empty);

        public static PlatformServicesProviderCallResult ForCloudRead(byte[] payload) =>
            new PlatformServicesProviderCallResult(true, ProviderOutcome.Accepted, default, false, payload ?? Array.Empty<byte>(), 0, string.Empty, string.Empty);

        public static PlatformServicesProviderCallResult ForLeaderboardRead(long score) =>
            new PlatformServicesProviderCallResult(true, ProviderOutcome.Accepted, default, false, null, score, string.Empty, string.Empty);

        /// <summary>The provider was not reached — an ordinary connectivity condition, never a
        /// diagnostic.</summary>
        public static PlatformServicesProviderCallResult Unreachable() =>
            new PlatformServicesProviderCallResult(false, null, default, false, null, 0, string.Empty, string.Empty);

        /// <summary>An explicit by-name/optional-resolution failure (LESSON-004): reported with a
        /// stable code rather than silence or a false success.</summary>
        public static PlatformServicesProviderCallResult ForDiagnostic(string code, string message)
        {
            if (string.IsNullOrEmpty(code)) throw new ArgumentException("A diagnostic needs a stable code.", nameof(code));
            return new PlatformServicesProviderCallResult(true, null, default, false, null, 0, code, message);
        }
    }

    /// <summary>The one seam every vendor route (a thin shell per vendor SDK) and the recording fake
    /// implement. The runtime calls this interface only, never a vendor SDK type directly.</summary>
    public interface IPlatformServicesProvider
    {
        PlatformServicesProviderCallResult CheckEntitlement();
        PlatformServicesProviderCallResult Unlock(string achievementId, string idempotencyKey);
        PlatformServicesProviderCallResult ReportScore(string leaderboardId, long score, LeaderboardUploadPolicy uploadPolicy, string idempotencyKey);
        PlatformServicesProviderCallResult ReadLeaderboard(string leaderboardId, LeaderboardRange range);
        PlatformServicesProviderCallResult CloudWrite(string key, byte[] payload, string idempotencyKey);
        PlatformServicesProviderCallResult CloudRead(string key);
    }
}
