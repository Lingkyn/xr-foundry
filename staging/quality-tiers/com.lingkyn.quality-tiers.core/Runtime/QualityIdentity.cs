using System;

namespace Lingkyn.QualityTiers.Core
{
    // Immutable tier and device-profile identity, decoupled from any renderer, subsystem, or
    // device that applies a tier. Both identities share one canonical form (lower-case dotted
    // segments) and one rejection code, but are separate C# value types with no cross-type
    // equality: a DeviceProfileId is never equal to a TierId built from the same text.
    //
    // DeviceProfileId is a validated identity this family defines for its own capability and
    // preset keying only. It is never equal to, derived from, or checked against this
    // repository's own build/verification compatibility-profile ids (compatibility-profiles.json):
    // the Core canonicalizes a DeviceProfileId's text and nothing else, so an arbitrary
    // caller-chosen device-profile id (whether or not it happens to resemble a compatibility
    // profile id used elsewhere in this repository) is accepted purely on its own canonical form.

    internal static class QualityIdentity
    {
        /// <summary>
        /// The one canonical form shared by <see cref="TierId"/> and <see cref="DeviceProfileId"/>:
        /// trimmed, non-empty, split on '.', each segment non-empty and made only of lower-case
        /// ASCII letters, digits, and underscores. Anything else (whitespace, control characters,
        /// upper-case letters, a leading/trailing/doubled dot, an empty segment) is malformed.
        /// </summary>
        public static QualityResult<string> TryCanonicalize(string value, string kind)
        {
            if (value == null)
            {
                return QualityResult<string>.Fail(QualityFailure.IdentityMalformed, $"A {kind} id must not be null.");
            }
            var trimmed = value.Trim();
            if (trimmed.Length == 0)
            {
                return QualityResult<string>.Fail(QualityFailure.IdentityMalformed, $"A {kind} id must not be empty.");
            }
            var segments = trimmed.Split('.');
            foreach (var segment in segments)
            {
                if (segment.Length == 0)
                {
                    return QualityResult<string>.Fail(QualityFailure.IdentityMalformed, $"The {kind} id '{value}' has an empty segment (a leading, trailing, or doubled '.').");
                }
                foreach (var character in segment)
                {
                    var isLowerLetter = character >= 'a' && character <= 'z';
                    var isDigit = character >= '0' && character <= '9';
                    if (!isLowerLetter && !isDigit && character != '_')
                    {
                        return QualityResult<string>.Fail(QualityFailure.IdentityMalformed, $"The {kind} id '{value}' has a character ('{character}') outside lower-case letters, digits, and underscore.");
                    }
                }
            }
            return QualityResult<string>.Ok(trimmed);
        }
    }

    /// <summary>Stable identity of one declared quality tier: a canonical dotted path such as
    /// <c>tier.high</c>, decoupled from any renderer, subsystem, or asset.</summary>
    public readonly struct TierId : IEquatable<TierId>, IComparable<TierId>
    {
        private TierId(string value) { Value = value; }

        public string Value { get; }

        public static QualityResult<TierId> TryCreate(string value)
        {
            var canonical = QualityIdentity.TryCanonicalize(value, "quality tier");
            return canonical.Succeeded ? QualityResult<TierId>.Ok(new TierId(canonical.Value)) : canonical.As<TierId>();
        }

        public static TierId Parse(string value)
        {
            var result = TryCreate(value);
            if (!result.Succeeded) throw new ArgumentException(result.Message, nameof(value));
            return result.Value;
        }

        public bool Equals(TierId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is TierId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public int CompareTo(TierId other) => string.CompareOrdinal(Value, other.Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(TierId left, TierId right) => left.Equals(right);
        public static bool operator !=(TierId left, TierId right) => !left.Equals(right);
    }

    /// <summary>Stable identity of one device profile this family defines for its own capability
    /// descriptor and preset keying. Shares <see cref="TierId"/>'s canonical form and rejection
    /// code, but is a distinct type: a <see cref="DeviceProfileId"/> is never equal to a
    /// <see cref="TierId"/> built from the same text, and this family never compares it against a
    /// repository build/verification compatibility-profile id (see the file header note).</summary>
    public readonly struct DeviceProfileId : IEquatable<DeviceProfileId>, IComparable<DeviceProfileId>
    {
        private DeviceProfileId(string value) { Value = value; }

        public string Value { get; }

        public static QualityResult<DeviceProfileId> TryCreate(string value)
        {
            var canonical = QualityIdentity.TryCanonicalize(value, "device profile");
            return canonical.Succeeded ? QualityResult<DeviceProfileId>.Ok(new DeviceProfileId(canonical.Value)) : canonical.As<DeviceProfileId>();
        }

        public static DeviceProfileId Parse(string value)
        {
            var result = TryCreate(value);
            if (!result.Succeeded) throw new ArgumentException(result.Message, nameof(value));
            return result.Value;
        }

        public bool Equals(DeviceProfileId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is DeviceProfileId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public int CompareTo(DeviceProfileId other) => string.CompareOrdinal(Value, other.Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(DeviceProfileId left, DeviceProfileId right) => left.Equals(right);
        public static bool operator !=(DeviceProfileId left, DeviceProfileId right) => !left.Equals(right);
    }
}
