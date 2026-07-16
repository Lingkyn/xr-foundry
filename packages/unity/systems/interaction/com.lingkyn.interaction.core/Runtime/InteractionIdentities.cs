using System;
using System.Collections.Generic;

namespace Lingkyn.Interaction.Core
{
    internal static class SemanticIdentityValidator
    {
        private static readonly HashSet<char> Allowed = BuildAllowed();

        public static InteractionResult<string> TryCreate(string value, string subjectLabel)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return InteractionResult<string>.Fail(
                    InteractionValidationCode.InvalidIdentity,
                    $"{subjectLabel} must not be empty.");
            }

            var trimmed = value.Trim();
            if (!trimmed.Equals(value, StringComparison.Ordinal))
            {
                return InteractionResult<string>.Fail(
                    InteractionValidationCode.InvalidIdentity,
                    $"{subjectLabel} must not contain leading or trailing whitespace.");
            }

            if (trimmed.Equals("default", StringComparison.OrdinalIgnoreCase))
            {
                return InteractionResult<string>.Fail(
                    InteractionValidationCode.DefaultIdentity,
                    $"{subjectLabel} must not use the reserved default identity.");
            }

            if (trimmed.Length > 128)
            {
                return InteractionResult<string>.Fail(
                    InteractionValidationCode.InvalidIdentity,
                    $"{subjectLabel} exceeds maximum length.");
            }

            if (trimmed[0] == '.' || trimmed[trimmed.Length - 1] == '.')
            {
                return InteractionResult<string>.Fail(
                    InteractionValidationCode.InvalidIdentity,
                    $"{subjectLabel} must not start or end with a separator.");
            }

            for (var i = 0; i < trimmed.Length; i++)
            {
                var ch = trimmed[i];
                if (ch == '.')
                {
                    if (i == 0 || trimmed[i - 1] == '.')
                    {
                        return InteractionResult<string>.Fail(
                            InteractionValidationCode.InvalidIdentity,
                            $"{subjectLabel} must not contain empty segments.");
                    }

                    continue;
                }

                if (!Allowed.Contains(ch))
                {
                    return InteractionResult<string>.Fail(
                        InteractionValidationCode.InvalidIdentity,
                        $"{subjectLabel} contains invalid character '{ch}'.");
                }
            }

            return InteractionResult<string>.Success(trimmed);
        }

        private static HashSet<char> BuildAllowed()
        {
            var allowed = new HashSet<char>();
            for (var c = 'a'; c <= 'z'; c++)
            {
                allowed.Add(c);
            }

            for (var c = '0'; c <= '9'; c++)
            {
                allowed.Add(c);
            }

            allowed.Add('.');
            allowed.Add('_');
            allowed.Add('-');
            return allowed;
        }
    }

    public readonly struct IntentId : IEquatable<IntentId>, IComparable<IntentId>
    {
        public string Value { get; }

        private IntentId(string value) => Value = value;

        public static InteractionResult<IntentId> TryCreate(string value)
        {
            var validated = SemanticIdentityValidator.TryCreate(value, "IntentId");
            return validated.Succeeded
                ? InteractionResult<IntentId>.Success(new IntentId(validated.Value))
                : InteractionResult<IntentId>.Fail(validated.Error.Code, validated.Error.Message, validated.Error.Subject);
        }

        public bool Equals(IntentId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is IntentId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        public int CompareTo(IntentId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);
        public override string ToString() => Value ?? string.Empty;
    }

    public readonly struct ContextId : IEquatable<ContextId>, IComparable<ContextId>
    {
        public string Value { get; }

        private ContextId(string value) => Value = value;

        public static InteractionResult<ContextId> TryCreate(string value)
        {
            var validated = SemanticIdentityValidator.TryCreate(value, "ContextId");
            return validated.Succeeded
                ? InteractionResult<ContextId>.Success(new ContextId(validated.Value))
                : InteractionResult<ContextId>.Fail(validated.Error.Code, validated.Error.Message, validated.Error.Subject);
        }

        public bool Equals(ContextId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ContextId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        public int CompareTo(ContextId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);
        public override string ToString() => Value ?? string.Empty;
    }

    public readonly struct RouteId : IEquatable<RouteId>, IComparable<RouteId>
    {
        public string Value { get; }

        private RouteId(string value) => Value = value;

        public static InteractionResult<RouteId> TryCreate(string value)
        {
            var validated = SemanticIdentityValidator.TryCreate(value, "RouteId");
            return validated.Succeeded
                ? InteractionResult<RouteId>.Success(new RouteId(validated.Value))
                : InteractionResult<RouteId>.Fail(validated.Error.Code, validated.Error.Message, validated.Error.Subject);
        }

        public bool Equals(RouteId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is RouteId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        public int CompareTo(RouteId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);
        public override string ToString() => Value ?? string.Empty;
    }

    public readonly struct SourceId : IEquatable<SourceId>, IComparable<SourceId>
    {
        public string Value { get; }

        private SourceId(string value) => Value = value;

        public static InteractionResult<SourceId> TryCreate(string value)
        {
            var validated = SemanticIdentityValidator.TryCreate(value, "SourceId");
            return validated.Succeeded
                ? InteractionResult<SourceId>.Success(new SourceId(validated.Value))
                : InteractionResult<SourceId>.Fail(validated.Error.Code, validated.Error.Message, validated.Error.Subject);
        }

        public bool Equals(SourceId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SourceId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        public int CompareTo(SourceId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);
        public override string ToString() => Value ?? string.Empty;
    }

    public readonly struct BindingSuggestionId : IEquatable<BindingSuggestionId>, IComparable<BindingSuggestionId>
    {
        public string Value { get; }

        private BindingSuggestionId(string value) => Value = value;

        public static InteractionResult<BindingSuggestionId> TryCreate(string value)
        {
            var validated = SemanticIdentityValidator.TryCreate(value, "BindingSuggestionId");
            return validated.Succeeded
                ? InteractionResult<BindingSuggestionId>.Success(new BindingSuggestionId(validated.Value))
                : InteractionResult<BindingSuggestionId>.Fail(validated.Error.Code, validated.Error.Message, validated.Error.Subject);
        }

        public bool Equals(BindingSuggestionId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is BindingSuggestionId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        public int CompareTo(BindingSuggestionId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);
        public override string ToString() => Value ?? string.Empty;
    }
}
