using System;

namespace Lingkyn.PlatformServices.Core
{
    // Engine-light platform-services core: structured results with stable failure codes. No engine
    // dependency, no vendor SDK type, no network socket, no HTTP client. See verification-contract.md
    // for the clause list every code below answers.

    /// <summary>The exactly-eleven stable machine codes this family's Core gate names. No other
    /// code is invented for a contract-named rejection or a structural (non-rejection) outcome: an
    /// outline of every value this class declares is the whole closed set.</summary>
    public static class PlatformServicesFailure
    {
        public const string None = "";

        /// <summary>An <see cref="AccountId"/> or <see cref="ProviderId"/> failed canonicalization.</summary>
        public const string IdentityMalformed = "identity.malformed";

        /// <summary>A capability name outside the closed set (entitlement, achievement, leaderboard,
        /// cloud_save, identity).</summary>
        public const string CapabilityUnknown = "capability.unknown";

        /// <summary>A <see cref="ProviderId"/> registered twice in one <see cref="ProviderRegistry"/>.</summary>
        public const string ProviderDuplicate = "provider.duplicate";

        /// <summary>A <see cref="ProviderDescriptor"/> declared an empty capability subset, or a
        /// cloud_save payload-size guard rail that is zero, negative, missing while cloud_save is
        /// declared, or present while cloud_save is not declared.</summary>
        public const string ProviderDeclarationInvalid = "provider.declaration.invalid";

        /// <summary>A composition names a required capability, or an intent names a capability, the
        /// active provider's descriptor does not declare.</summary>
        public const string CapabilityUnsupported = "capability.unsupported";

        /// <summary>An intent other than <see cref="CheckEntitlementIntent"/> was issued before an
        /// <see cref="AccountId"/> was resolved for the active provider.</summary>
        public const string IdentityMissing = "identity.missing";

        /// <summary>An idempotency key already resolved to one outcome was resubmitted with a
        /// different outcome.</summary>
        public const string DispatchMismatch = "dispatch.mismatch";

        /// <summary>LESSON-011: an intent named an <c>ExpectedRevision</c> that does not match the
        /// state's current <c>Revision</c>, rejected rather than overwriting a change another actor
        /// already made.</summary>
        public const string StateStale = "state.stale";

        /// <summary>A caller-supplied provider outcome, or a direct entitlement check, reporting the
        /// account does not hold the entitlement.</summary>
        public const string NotEntitled = "not_entitled";

        /// <summary>A write issued while the provider was never reached; the write is queued rather
        /// than applied.</summary>
        public const string Offline = "offline";

        /// <summary>A caller-supplied provider outcome reporting the provider throttled the request.</summary>
        public const string RateLimited = "rate_limited";
    }

    /// <summary>Structured outcome: a value with an optional non-empty structural code on a clean or
    /// folded success, or no value with a stable non-empty code on a rejection.</summary>
    public readonly struct PlatformServicesResult<T>
    {
        private PlatformServicesResult(bool succeeded, T value, string code, string message)
        {
            Succeeded = succeeded;
            Value = value;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        /// <summary>True whenever the state advanced (a clean success, or a dispatched write folded
        /// as <c>not_entitled</c>/<c>rate_limited</c>, or a queued <c>offline</c> write); false only
        /// for a validation-time rejection that leaves the prior state untouched.</summary>
        public bool Succeeded { get; }
        public T Value { get; }
        /// <summary>Empty on a clean success. A stable, non-empty code otherwise: a validation
        /// rejection, or a folded/queued structural outcome on an otherwise-succeeded write.</summary>
        public string Code { get; }
        public string Message { get; }

        public static PlatformServicesResult<T> Ok(T value) => new PlatformServicesResult<T>(true, value, PlatformServicesFailure.None, string.Empty);

        /// <summary>A succeeded application (the state advanced) that still carries a non-empty
        /// structural code: <c>offline</c> for a freshly queued write, or <c>not_entitled</c>/
        /// <c>rate_limited</c> for a dispatched write whose provider outcome updated no projection.</summary>
        public static PlatformServicesResult<T> OkWithCode(T value, string code, string message)
        {
            if (string.IsNullOrEmpty(code)) throw new ArgumentException("A structural code is required.", nameof(code));
            return new PlatformServicesResult<T>(true, value, code, message);
        }

        public static PlatformServicesResult<T> Fail(string code, string message)
        {
            if (string.IsNullOrEmpty(code)) throw new ArgumentException("A failure needs a stable code.", nameof(code));
            return new PlatformServicesResult<T>(false, default, code, message);
        }

        /// <summary>Re-types a failed result; throws if this result succeeded.</summary>
        public PlatformServicesResult<TOther> As<TOther>()
        {
            if (Succeeded) throw new InvalidOperationException("Only a failed result can be re-typed.");
            return PlatformServicesResult<TOther>.Fail(Code, Message);
        }
    }
}
