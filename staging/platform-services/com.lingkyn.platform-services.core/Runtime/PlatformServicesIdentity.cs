using System;

namespace Lingkyn.PlatformServices.Core
{
    // Immutable AccountId and ProviderId identity, decoupled from any vendor SDK session, token, or
    // account object. Both share one canonical form and one rejection code, but are separate C#
    // value types with no cross-type equality: an AccountId is never equal to a ProviderId built
    // from the same text. Every other identity this family names (an achievement id, a leaderboard
    // id, a cloud key) enters and leaves the Core as a plain, opaque string the Core never validates
    // or canonicalizes.

    internal static class PlatformServicesIdentity
    {
        /// <summary>
        /// The one canonical form shared by <see cref="AccountId"/> and <see cref="ProviderId"/>:
        /// trimmed, non-empty, split on '.', each segment non-empty and made only of lower-case ASCII
        /// letters, digits, and underscores. Anything else (whitespace, control characters, upper-case
        /// letters, a leading/trailing/doubled dot, an empty segment) is malformed.
        /// </summary>
        public static PlatformServicesResult<string> TryCanonicalize(string value, string kind)
        {
            if (value == null)
            {
                return PlatformServicesResult<string>.Fail(PlatformServicesFailure.IdentityMalformed, $"A {kind} id must not be null.");
            }
            var trimmed = value.Trim();
            if (trimmed.Length == 0)
            {
                return PlatformServicesResult<string>.Fail(PlatformServicesFailure.IdentityMalformed, $"A {kind} id must not be empty.");
            }
            var segments = trimmed.Split('.');
            foreach (var segment in segments)
            {
                if (segment.Length == 0)
                {
                    return PlatformServicesResult<string>.Fail(PlatformServicesFailure.IdentityMalformed, $"The {kind} id '{value}' has an empty segment (a leading, trailing, or doubled '.').");
                }
                foreach (var character in segment)
                {
                    var isLowerLetter = character >= 'a' && character <= 'z';
                    var isDigit = character >= '0' && character <= '9';
                    if (!isLowerLetter && !isDigit && character != '_')
                    {
                        return PlatformServicesResult<string>.Fail(PlatformServicesFailure.IdentityMalformed, $"The {kind} id '{value}' has a character ('{character}') outside lower-case letters, digits, and underscore.");
                    }
                }
            }
            return PlatformServicesResult<string>.Ok(trimmed);
        }
    }

    /// <summary>A validated account identity, canonical within one provider's namespace (for example
    /// <c>player.7f2c1a</c>). Decoupled from any vendor SDK session, token, or account object.</summary>
    public readonly struct AccountId : IEquatable<AccountId>, IComparable<AccountId>
    {
        private AccountId(string value) { Value = value; }

        public string Value { get; }

        public static PlatformServicesResult<AccountId> TryCreate(string value)
        {
            var canonical = PlatformServicesIdentity.TryCanonicalize(value, "account");
            return canonical.Succeeded ? PlatformServicesResult<AccountId>.Ok(new AccountId(canonical.Value)) : canonical.As<AccountId>();
        }

        public static AccountId Parse(string value)
        {
            var result = TryCreate(value);
            if (!result.Succeeded) throw new ArgumentException(result.Message, nameof(value));
            return result.Value;
        }

        public bool Equals(AccountId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is AccountId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public int CompareTo(AccountId other) => string.CompareOrdinal(Value, other.Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(AccountId left, AccountId right) => left.Equals(right);
        public static bool operator !=(AccountId left, AccountId right) => !left.Equals(right);
    }

    /// <summary>A validated platform-provider identity (for example <c>meta_horizon_platform</c>).
    /// Shares <see cref="AccountId"/>'s canonical form and rejection code, but is a distinct type: a
    /// <see cref="ProviderId"/> is never equal to an <see cref="AccountId"/> built from the same
    /// text.</summary>
    public readonly struct ProviderId : IEquatable<ProviderId>, IComparable<ProviderId>
    {
        private ProviderId(string value) { Value = value; }

        public string Value { get; }

        public static PlatformServicesResult<ProviderId> TryCreate(string value)
        {
            var canonical = PlatformServicesIdentity.TryCanonicalize(value, "provider");
            return canonical.Succeeded ? PlatformServicesResult<ProviderId>.Ok(new ProviderId(canonical.Value)) : canonical.As<ProviderId>();
        }

        public static ProviderId Parse(string value)
        {
            var result = TryCreate(value);
            if (!result.Succeeded) throw new ArgumentException(result.Message, nameof(value));
            return result.Value;
        }

        public bool Equals(ProviderId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ProviderId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public int CompareTo(ProviderId other) => string.CompareOrdinal(Value, other.Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(ProviderId left, ProviderId right) => left.Equals(right);
        public static bool operator !=(ProviderId left, ProviderId right) => !left.Equals(right);
    }
}
