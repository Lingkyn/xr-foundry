using System;

namespace Lingkyn.XrUiShell.Core
{
    // Immutable shell surface and input source identity, decoupled from any Canvas, document,
    // transform, interactor, or hand pose that implements them. PanelId, WristMenuId, HandMenuId,
    // and InputSourceId all share one canonical form and one rejection code, but are four
    // separate C# value types with no cross-type equality: a PanelId and a WristMenuId built from
    // the same text are never equal to each other.

    internal static class ShellIdentityText
    {
        /// <summary>The one canonical form every identity type in this file shares: trimmed,
        /// non-empty, split on '.', each segment non-empty and made only of lower-case ASCII
        /// letters, digits, and underscores. Anything else (whitespace, control characters,
        /// upper-case letters, a leading/trailing/doubled dot, an empty segment) is malformed.</summary>
        public static ShellResult<string> TryCanonicalize(string value, string kind)
        {
            if (value == null)
            {
                return ShellResult<string>.Fail(ShellFailure.IdentityMalformed, $"A {kind} id must not be null.", kind);
            }
            var trimmed = value.Trim();
            if (trimmed.Length == 0)
            {
                return ShellResult<string>.Fail(ShellFailure.IdentityMalformed, $"A {kind} id must not be empty.", kind);
            }
            var segments = trimmed.Split('.');
            foreach (var segment in segments)
            {
                if (segment.Length == 0)
                {
                    return ShellResult<string>.Fail(ShellFailure.IdentityMalformed, $"The {kind} id '{value}' has an empty segment (a leading, trailing, or doubled '.').", kind);
                }
                foreach (var character in segment)
                {
                    var isLowerLetter = character >= 'a' && character <= 'z';
                    var isDigit = character >= '0' && character <= '9';
                    if (!isLowerLetter && !isDigit && character != '_')
                    {
                        return ShellResult<string>.Fail(ShellFailure.IdentityMalformed, $"The {kind} id '{value}' has a character ('{character}') outside lower-case letters, digits, and underscore.", kind);
                    }
                }
            }
            return ShellResult<string>.Ok(trimmed);
        }
    }

    /// <summary>Stable identity of one declared world-space panel, decoupled from any Canvas,
    /// document, or transform that implements it.</summary>
    public readonly struct PanelId : IEquatable<PanelId>, IComparable<PanelId>
    {
        private PanelId(string value) { Value = value; }

        public string Value { get; }

        public static ShellResult<PanelId> TryCreate(string value)
        {
            var canonical = ShellIdentityText.TryCanonicalize(value, "panel");
            return canonical.Succeeded ? ShellResult<PanelId>.Ok(new PanelId(canonical.Value)) : canonical.As<PanelId>();
        }

        public static PanelId Parse(string value)
        {
            var result = TryCreate(value);
            if (!result.Succeeded) throw new ArgumentException(result.Message, nameof(value));
            return result.Value;
        }

        public bool Equals(PanelId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is PanelId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public int CompareTo(PanelId other) => string.CompareOrdinal(Value, other.Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(PanelId left, PanelId right) => left.Equals(right);
        public static bool operator !=(PanelId left, PanelId right) => !left.Equals(right);
    }

    /// <summary>Stable identity of one declared wrist menu. Shares <see cref="PanelId"/>'s
    /// canonical form and rejection code but is never equal to a <see cref="PanelId"/> built from
    /// the same text.</summary>
    public readonly struct WristMenuId : IEquatable<WristMenuId>, IComparable<WristMenuId>
    {
        private WristMenuId(string value) { Value = value; }

        public string Value { get; }

        public static ShellResult<WristMenuId> TryCreate(string value)
        {
            var canonical = ShellIdentityText.TryCanonicalize(value, "wrist menu");
            return canonical.Succeeded ? ShellResult<WristMenuId>.Ok(new WristMenuId(canonical.Value)) : canonical.As<WristMenuId>();
        }

        public static WristMenuId Parse(string value)
        {
            var result = TryCreate(value);
            if (!result.Succeeded) throw new ArgumentException(result.Message, nameof(value));
            return result.Value;
        }

        public bool Equals(WristMenuId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is WristMenuId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public int CompareTo(WristMenuId other) => string.CompareOrdinal(Value, other.Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(WristMenuId left, WristMenuId right) => left.Equals(right);
        public static bool operator !=(WristMenuId left, WristMenuId right) => !left.Equals(right);
    }

    /// <summary>Stable identity of one declared hand menu. Shares <see cref="PanelId"/>'s
    /// canonical form and rejection code but is never equal to a <see cref="PanelId"/> or a
    /// <see cref="WristMenuId"/> built from the same text.</summary>
    public readonly struct HandMenuId : IEquatable<HandMenuId>, IComparable<HandMenuId>
    {
        private HandMenuId(string value) { Value = value; }

        public string Value { get; }

        public static ShellResult<HandMenuId> TryCreate(string value)
        {
            var canonical = ShellIdentityText.TryCanonicalize(value, "hand menu");
            return canonical.Succeeded ? ShellResult<HandMenuId>.Ok(new HandMenuId(canonical.Value)) : canonical.As<HandMenuId>();
        }

        public static HandMenuId Parse(string value)
        {
            var result = TryCreate(value);
            if (!result.Succeeded) throw new ArgumentException(result.Message, nameof(value));
            return result.Value;
        }

        public bool Equals(HandMenuId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is HandMenuId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public int CompareTo(HandMenuId other) => string.CompareOrdinal(Value, other.Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(HandMenuId left, HandMenuId right) => left.Equals(right);
        public static bool operator !=(HandMenuId left, HandMenuId right) => !left.Equals(right);
    }

    /// <summary>Stable identity of one registered pointer or gaze input source, decoupled from any
    /// interactor, controller, or hand-tracking provider. Shares the same canonical form and
    /// rejection code as the surface identities but is never equal to any of them.</summary>
    public readonly struct InputSourceId : IEquatable<InputSourceId>, IComparable<InputSourceId>
    {
        private InputSourceId(string value) { Value = value; }

        public string Value { get; }

        public static ShellResult<InputSourceId> TryCreate(string value)
        {
            var canonical = ShellIdentityText.TryCanonicalize(value, "input source");
            return canonical.Succeeded ? ShellResult<InputSourceId>.Ok(new InputSourceId(canonical.Value)) : canonical.As<InputSourceId>();
        }

        public static InputSourceId Parse(string value)
        {
            var result = TryCreate(value);
            if (!result.Succeeded) throw new ArgumentException(result.Message, nameof(value));
            return result.Value;
        }

        public bool Equals(InputSourceId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is InputSourceId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public int CompareTo(InputSourceId other) => string.CompareOrdinal(Value, other.Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(InputSourceId left, InputSourceId right) => left.Equals(right);
        public static bool operator !=(InputSourceId left, InputSourceId right) => !left.Equals(right);
    }
}
