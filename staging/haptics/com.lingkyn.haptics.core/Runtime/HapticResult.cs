using System;

namespace Lingkyn.Haptics.Core
{
    // Engine-light haptics core: structured results with stable failure codes. No engine
    // dependency, no asset read, no sound played, no actuator driven. See verification-contract.md
    // for the full clause list every code below answers.

    /// <summary>Stable machine codes carried by every <see cref="HapticResult{T}"/>. The twelve
    /// codes above <see cref="StateStale"/> are exactly the codes
    /// docs/standards/haptics/verification-contract.md names for the Core gate's structured-results
    /// clause; no other code is invented for a contract-named rejection.</summary>
    public static class HapticFailure
    {
        public const string None = "";
        public const string IdentityMalformed = "identity.malformed";
        public const string KindDeclarationInvalid = "kind.declaration.invalid";
        public const string EventDuplicate = "event.duplicate";
        public const string ProfileDeclarationInvalid = "profile.declaration.invalid";
        public const string EventUnknown = "event.unknown";
        public const string TargetUnknown = "target.unknown";
        public const string KindMismatch = "kind.mismatch";
        public const string AmplitudeOutOfRange = "amplitude.out_of_range";
        public const string DurationOutOfRange = "duration.out_of_range";
        public const string ProfileUnknown = "profile.unknown";
        public const string BindingDuplicate = "binding.duplicate";
        public const string BindingUnbound = "binding.unbound";

        /// <summary>LESSON-011 addition: an intent named an <c>ExpectedRevision</c> that does not
        /// match the state's current <c>Revision</c>, rejected rather than overwriting a change
        /// another actor already made. This code is not among the contract's literal twelve; it is
        /// the same canonical "state.stale" code the Live Tuning and XR UI shell families already
        /// use for the identical one-intent-channel shape, added here because every live family
        /// answers LESSON-011 (see staging/haptics/README.md and CHANGELOG note).</summary>
        public const string StateStale = "state.stale";
    }

    /// <summary>Structured outcome: a value on success, a stable code and a human message on failure.</summary>
    public readonly struct HapticResult<T>
    {
        private HapticResult(bool succeeded, T value, string code, string message)
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

        public static HapticResult<T> Ok(T value) => new HapticResult<T>(true, value, HapticFailure.None, string.Empty);

        public static HapticResult<T> Fail(string code, string message)
        {
            if (string.IsNullOrEmpty(code)) throw new ArgumentException("A failure needs a stable code.", nameof(code));
            return new HapticResult<T>(false, default, code, message);
        }

        /// <summary>Re-types a failed result; throws if this result succeeded.</summary>
        public HapticResult<TOther> As<TOther>()
        {
            if (Succeeded) throw new InvalidOperationException("Only a failed result can be re-typed.");
            return HapticResult<TOther>.Fail(Code, Message);
        }
    }
}
