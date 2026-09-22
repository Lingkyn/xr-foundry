using System;

namespace Lingkyn.LiveTuning.Core
{
    // Engine-light live tuning core: structured results with stable failure codes. No
    // UnityEngine dependency, no I/O, no clock, no rendering. See verification-contract.md
    // for the full clause list every code below answers.

    /// <summary>Stable machine codes carried by every <see cref="LiveTuningResult{T}"/>.</summary>
    public static class LiveTuningFailure
    {
        public const string None = "";
        public const string IdentityMalformed = "identity.malformed";
        public const string KindDeclarationInvalid = "kind.declaration.invalid";
        public const string TunableDuplicate = "tunable.duplicate";
        public const string DefaultOutOfRange = "default.out_of_range";
        public const string TunableUnknown = "tunable.unknown";
        public const string TunableKindMismatch = "tunable.kind.mismatch";
        public const string TunableOutOfRange = "tunable.out_of_range";
        public const string TunableKindUnsupported = "tunable.kind.unsupported";
        public const string SnapshotUnknown = "snapshot.unknown";
        public const string BindingKindMismatch = "binding.kind.mismatch";
        public const string BindingDuplicate = "binding.duplicate";
        public const string TokenUnsupported = "token.unsupported";
        public const string TokenRangeMissing = "token.range.missing";
    }

    /// <summary>Structured outcome: a value on success, a stable code and a human message on failure.</summary>
    public readonly struct LiveTuningResult<T>
    {
        private LiveTuningResult(bool succeeded, T value, string code, string message)
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

        public static LiveTuningResult<T> Ok(T value) => new LiveTuningResult<T>(true, value, LiveTuningFailure.None, string.Empty);

        public static LiveTuningResult<T> Fail(string code, string message)
        {
            if (string.IsNullOrEmpty(code)) throw new ArgumentException("A failure needs a stable code.", nameof(code));
            return new LiveTuningResult<T>(false, default, code, message);
        }

        /// <summary>Re-types a failed result; throws if this result succeeded.</summary>
        public LiveTuningResult<TOther> As<TOther>()
        {
            if (Succeeded) throw new InvalidOperationException("Only a failed result can be re-typed.");
            return LiveTuningResult<TOther>.Fail(Code, Message);
        }
    }
}
