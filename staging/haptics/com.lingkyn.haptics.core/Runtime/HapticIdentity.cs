using System;

namespace Lingkyn.Haptics.Core
{
    // Immutable haptic and profile identity, decoupled from any clip, asset, route, or device
    // that plays a haptic event. Both identities share one canonical form (lower-case dotted
    // segments) and one rejection code, but are separate C# value types with no cross-type
    // equality: a HapticProfileId is never equal to a HapticEventId built from the same text.

    internal static class HapticIdentity
    {
        /// <summary>
        /// The one canonical form shared by <see cref="HapticEventId"/> and <see cref="HapticProfileId"/>:
        /// trimmed, non-empty, split on '.', each segment non-empty and made only of lower-case ASCII
        /// letters, digits, and underscores. Anything else (whitespace, control characters, upper-case
        /// letters, a leading/trailing/doubled dot, an empty segment) is malformed.
        /// </summary>
        public static HapticResult<string> TryCanonicalize(string value, string kind)
        {
            if (value == null)
            {
                return HapticResult<string>.Fail(HapticFailure.IdentityMalformed, $"A {kind} id must not be null.");
            }
            var trimmed = value.Trim();
            if (trimmed.Length == 0)
            {
                return HapticResult<string>.Fail(HapticFailure.IdentityMalformed, $"A {kind} id must not be empty.");
            }
            var segments = trimmed.Split('.');
            foreach (var segment in segments)
            {
                if (segment.Length == 0)
                {
                    return HapticResult<string>.Fail(HapticFailure.IdentityMalformed, $"The {kind} id '{value}' has an empty segment (a leading, trailing, or doubled '.').");
                }
                foreach (var character in segment)
                {
                    var isLowerLetter = character >= 'a' && character <= 'z';
                    var isDigit = character >= '0' && character <= '9';
                    if (!isLowerLetter && !isDigit && character != '_')
                    {
                        return HapticResult<string>.Fail(HapticFailure.IdentityMalformed, $"The {kind} id '{value}' has a character ('{character}') outside lower-case letters, digits, and underscore.");
                    }
                }
            }
            return HapticResult<string>.Ok(trimmed);
        }
    }

    /// <summary>Stable identity of one registered haptic event: a canonical dotted path such as
    /// <c>grab.contact</c>, decoupled from any clip, asset, or actuator.</summary>
    public readonly struct HapticEventId : IEquatable<HapticEventId>, IComparable<HapticEventId>
    {
        private HapticEventId(string value) { Value = value; }

        public string Value { get; }

        public static HapticResult<HapticEventId> TryCreate(string value)
        {
            var canonical = HapticIdentity.TryCanonicalize(value, "haptic event");
            return canonical.Succeeded ? HapticResult<HapticEventId>.Ok(new HapticEventId(canonical.Value)) : canonical.As<HapticEventId>();
        }

        public static HapticEventId Parse(string value)
        {
            var result = TryCreate(value);
            if (!result.Succeeded) throw new ArgumentException(result.Message, nameof(value));
            return result.Value;
        }

        public bool Equals(HapticEventId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is HapticEventId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public int CompareTo(HapticEventId other) => string.CompareOrdinal(Value, other.Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(HapticEventId left, HapticEventId right) => left.Equals(right);
        public static bool operator !=(HapticEventId left, HapticEventId right) => !left.Equals(right);
    }

    /// <summary>Stable identity of one per-controller/per-hand haptic profile. Shares
    /// <see cref="HapticEventId"/>'s canonical form and rejection code, but is a distinct type: a
    /// <see cref="HapticProfileId"/> is never equal to a <see cref="HapticEventId"/> built from the
    /// same text.</summary>
    public readonly struct HapticProfileId : IEquatable<HapticProfileId>, IComparable<HapticProfileId>
    {
        private HapticProfileId(string value) { Value = value; }

        public string Value { get; }

        public static HapticResult<HapticProfileId> TryCreate(string value)
        {
            var canonical = HapticIdentity.TryCanonicalize(value, "haptic profile");
            return canonical.Succeeded ? HapticResult<HapticProfileId>.Ok(new HapticProfileId(canonical.Value)) : canonical.As<HapticProfileId>();
        }

        public static HapticProfileId Parse(string value)
        {
            var result = TryCreate(value);
            if (!result.Succeeded) throw new ArgumentException(result.Message, nameof(value));
            return result.Value;
        }

        public bool Equals(HapticProfileId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is HapticProfileId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public int CompareTo(HapticProfileId other) => string.CompareOrdinal(Value, other.Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(HapticProfileId left, HapticProfileId right) => left.Equals(right);
        public static bool operator !=(HapticProfileId left, HapticProfileId right) => !left.Equals(right);
    }
}
