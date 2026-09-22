using System;

namespace Lingkyn.LiveTuning.Core
{
    // Immutable tunable and snapshot identity, decoupled from any asset, field, token
    // path, or control that implements a tunable. Both identities share one canonical
    // form (lower-case dotted segments) and one rejection code, but are separate C#
    // value types with no cross-type equality.

    internal static class LiveTuningIdentity
    {
        /// <summary>
        /// The one canonical form shared by <see cref="TunableId"/> and <see cref="SnapshotId"/>:
        /// trimmed, non-empty, split on '.', each segment non-empty and made only of lower-case
        /// ASCII letters, digits, and underscores. Anything else (whitespace, control characters,
        /// upper-case letters, a leading/trailing/doubled dot, an empty segment) is malformed.
        /// </summary>
        public static LiveTuningResult<string> TryCanonicalize(string value, string kind)
        {
            if (value == null)
            {
                return LiveTuningResult<string>.Fail(LiveTuningFailure.IdentityMalformed, $"A {kind} id must not be null.");
            }
            var trimmed = value.Trim();
            if (trimmed.Length == 0)
            {
                return LiveTuningResult<string>.Fail(LiveTuningFailure.IdentityMalformed, $"A {kind} id must not be empty.");
            }
            var segments = trimmed.Split('.');
            foreach (var segment in segments)
            {
                if (segment.Length == 0)
                {
                    return LiveTuningResult<string>.Fail(LiveTuningFailure.IdentityMalformed, $"The {kind} id '{value}' has an empty segment (a leading, trailing, or doubled '.').");
                }
                foreach (var character in segment)
                {
                    var isLowerLetter = character >= 'a' && character <= 'z';
                    var isDigit = character >= '0' && character <= '9';
                    if (!isLowerLetter && !isDigit && character != '_')
                    {
                        return LiveTuningResult<string>.Fail(LiveTuningFailure.IdentityMalformed, $"The {kind} id '{value}' has a character ('{character}') outside lower-case letters, digits, and underscore.");
                    }
                }
            }
            return LiveTuningResult<string>.Ok(trimmed);
        }
    }

    /// <summary>Stable identity of one registered tunable: a canonical dotted path such as
    /// <c>surface.panel</c>, decoupled from any asset, field, or control.</summary>
    public readonly struct TunableId : IEquatable<TunableId>, IComparable<TunableId>
    {
        private TunableId(string value) { Value = value; }

        public string Value { get; }

        public static LiveTuningResult<TunableId> TryCreate(string value)
        {
            var canonical = LiveTuningIdentity.TryCanonicalize(value, "tunable");
            return canonical.Succeeded ? LiveTuningResult<TunableId>.Ok(new TunableId(canonical.Value)) : canonical.As<TunableId>();
        }

        public static TunableId Parse(string value)
        {
            var result = TryCreate(value);
            if (!result.Succeeded) throw new ArgumentException(result.Message, nameof(value));
            return result.Value;
        }

        public bool Equals(TunableId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is TunableId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public int CompareTo(TunableId other) => string.CompareOrdinal(Value, other.Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(TunableId left, TunableId right) => left.Equals(right);
        public static bool operator !=(TunableId left, TunableId right) => !left.Equals(right);
    }

    /// <summary>Stable identity of one stored snapshot. Shares <see cref="TunableId"/>'s canonical
    /// form and rejection code, but is a distinct type: a <see cref="SnapshotId"/> is never equal
    /// to a <see cref="TunableId"/> built from the same text.</summary>
    public readonly struct SnapshotId : IEquatable<SnapshotId>, IComparable<SnapshotId>
    {
        private SnapshotId(string value) { Value = value; }

        public string Value { get; }

        public static LiveTuningResult<SnapshotId> TryCreate(string value)
        {
            var canonical = LiveTuningIdentity.TryCanonicalize(value, "snapshot");
            return canonical.Succeeded ? LiveTuningResult<SnapshotId>.Ok(new SnapshotId(canonical.Value)) : canonical.As<SnapshotId>();
        }

        public static SnapshotId Parse(string value)
        {
            var result = TryCreate(value);
            if (!result.Succeeded) throw new ArgumentException(result.Message, nameof(value));
            return result.Value;
        }

        public bool Equals(SnapshotId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SnapshotId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public int CompareTo(SnapshotId other) => string.CompareOrdinal(Value, other.Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(SnapshotId left, SnapshotId right) => left.Equals(right);
        public static bool operator !=(SnapshotId left, SnapshotId right) => !left.Equals(right);
    }
}
