using System;

namespace Lingkyn.QualityTiers.Core
{
    // Engine-light quality-tier core: structured results with stable failure codes. No engine
    // dependency, no display queried, no renderer switched, no frame measured. See
    // verification-contract.md for the full clause list every code below answers.

    /// <summary>Stable machine codes carried by every <see cref="QualityResult{T}"/>. These ten
    /// codes are exactly the codes docs/standards/quality-tiers/verification-contract.md names for
    /// the Core gate's structured-results clause, plus <see cref="StateStale"/> for the
    /// LESSON-011 addition; no other code is invented for a Core rejection (QC-11).</summary>
    public static class QualityFailure
    {
        public const string None = "";
        public const string IdentityMalformed = "identity.malformed";
        public const string TierDeclarationInvalid = "tier.declaration.invalid";
        public const string CapabilityDeclarationInvalid = "capability.declaration.invalid";
        public const string BudgetDeclarationInvalid = "budget.declaration.invalid";
        public const string TierDuplicate = "tier.duplicate";
        public const string TierUnknown = "tier.unknown";
        public const string DeviceUnknown = "device.unknown";
        public const string CapabilityUnsupported = "capability.unsupported";
        public const string ValueOutOfRange = "value.out_of_range";

        /// <summary>LESSON-011 addition: an intent named an expected revision that does not match
        /// the state's current revision, rejected before the intent's own rule runs rather than
        /// overwriting a change another actor already made. Every live family answers LESSON-011
        /// with this same canonical "state.stale" code (see staging/haptics and staging/live-tuning).</summary>
        public const string StateStale = "state.stale";
    }

    /// <summary>Structured outcome: a value on success, a stable code and a human message on failure.</summary>
    public readonly struct QualityResult<T>
    {
        private QualityResult(bool succeeded, T value, string code, string message)
        {
            Succeeded = succeeded;
            Value = value;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public bool Succeeded { get; }
        public T Value { get; }
        /// <summary>Empty on success. A stable, non-empty code on a rejection.</summary>
        public string Code { get; }
        public string Message { get; }

        public static QualityResult<T> Ok(T value) => new QualityResult<T>(true, value, QualityFailure.None, string.Empty);

        public static QualityResult<T> Fail(string code, string message)
        {
            if (string.IsNullOrEmpty(code)) throw new ArgumentException("A failure needs a stable code.", nameof(code));
            return new QualityResult<T>(false, default, code, message);
        }

        /// <summary>Re-types a failed result; throws if this result succeeded.</summary>
        public QualityResult<TOther> As<TOther>()
        {
            if (Succeeded) throw new InvalidOperationException("Only a failed result can be re-typed.");
            return QualityResult<TOther>.Fail(Code, Message);
        }
    }
}
