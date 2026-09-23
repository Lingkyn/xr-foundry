using System;

namespace Lingkyn.XrUiShell.Core
{
    // Engine-light XR UI shell core: structured results with stable failure codes. No
    // UnityEngine dependency, no I/O, no clock, no pose, no raycast, no rendering. See
    // verification-contract.md for the full clause list every code below answers. This is the
    // closed set of failure codes the Core gate names; no other code is ever returned.

    /// <summary>Stable machine codes carried by every <see cref="ShellResult{T}"/>. This is the
    /// whole closed set the Core gate names; nothing outside it is ever returned.</summary>
    public static class ShellFailure
    {
        public const string None = "";
        public const string IdentityMalformed = "identity.malformed";
        public const string PanelUnknown = "panel.unknown";
        public const string PanelDuplicate = "panel.duplicate";
        public const string AnchorKindUnsupported = "anchor.kind.unsupported";
        public const string FocusConflict = "focus.conflict";
        public const string SourceUnknown = "source.unknown";
        public const string SourceKindUnsupported = "source.kind.unsupported";
        public const string RouteAmbiguous = "route.ambiguous";
        public const string RouteNone = "route.none";
        public const string TokenUnknown = "token.unknown";
        public const string SlotUnknown = "slot.unknown";
        public const string SlotUnmapped = "slot.unmapped";
        public const string OrnamentNeverFolds = "ornament.fold.unsupported";
        public const string VerbUnknown = "verb.unknown";
        public const string VerbDuplicate = "verb.duplicate";
        public const string VerbUnwired = "verb.unwired";
        public const string VerbNoTarget = "verb.no_target";
    }

    /// <summary>Structured outcome: a value on success, a stable code and a human message, plus
    /// the offending field path, on failure.</summary>
    public readonly struct ShellResult<T>
    {
        private ShellResult(bool succeeded, T value, string code, string message, string fieldPath)
        {
            Succeeded = succeeded;
            Value = value;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            FieldPath = fieldPath ?? string.Empty;
        }

        public bool Succeeded { get; }
        public T Value { get; }
        /// <summary>Empty on success. A stable, non-empty code from <see cref="ShellFailure"/> on
        /// a rejection.</summary>
        public string Code { get; }
        public string Message { get; }
        /// <summary>The offending field path (an id, a slot name, a token name, or an intent
        /// field). Empty on success.</summary>
        public string FieldPath { get; }

        public static ShellResult<T> Ok(T value) => new ShellResult<T>(true, value, ShellFailure.None, string.Empty, string.Empty);

        public static ShellResult<T> Fail(string code, string message, string fieldPath = "")
        {
            if (string.IsNullOrEmpty(code)) throw new ArgumentException("A failure needs a stable code.", nameof(code));
            return new ShellResult<T>(false, default, code, message, fieldPath);
        }

        /// <summary>Re-types a failed result; throws if this result succeeded.</summary>
        public ShellResult<TOther> As<TOther>()
        {
            if (Succeeded) throw new InvalidOperationException("Only a failed result can be re-typed.");
            return ShellResult<TOther>.Fail(Code, Message, FieldPath);
        }
    }
}
