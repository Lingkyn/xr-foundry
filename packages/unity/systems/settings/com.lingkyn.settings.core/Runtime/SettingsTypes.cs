using System;
using System.Collections.Generic;

namespace Lingkyn.Settings.Core
{
    public enum SettingValueKind
    {
        Boolean = 0,
        Integer = 1,
        Float = 2,
        String = 3,
        Option = 4,
    }

    public enum SettingScope
    {
        Global = 0,
        User = 1,
        Profile = 2,
        Session = 3,
    }

    public enum SettingsApplyOutcome
    {
        Applied = 0,
        NoOp = 1,
        ValidationFailed = 2,
        ApplicatorFailed = 3,
        RollbackFailed = 4,
        StaleTransaction = 5,
        AppliedNotPersisted = 6,
    }

    public enum SettingsValidationCode
    {
        None = 0,
        InvalidKey,
        DuplicateDefinition,
        KindMismatch,
        InvalidDefault,
        NonFiniteFloat,
        OutOfRange,
        InvalidStep,
        UnknownOption,
        StringTooLong,
        DuplicateProfileOverride,
        CrossConstraintViolation,
        InvalidProfileLayer,
        InvalidScope,
    }

    public readonly struct SettingKey : IEquatable<SettingKey>, IComparable<SettingKey>
    {
        private static readonly HashSet<char> Allowed = BuildAllowed();

        public string Value { get; }

        private SettingKey(string value) => Value = value;

        public static SettingsResult<SettingKey> TryCreate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return SettingsResult<SettingKey>.Fail(
                    SettingsValidationCode.InvalidKey,
                    "Setting key must not be empty.");
            }

            var trimmed = value.Trim();
            if (!trimmed.Equals(value, StringComparison.Ordinal))
            {
                return SettingsResult<SettingKey>.Fail(
                    SettingsValidationCode.InvalidKey,
                    "Setting key must not contain leading or trailing whitespace.");
            }

            if (trimmed.Length > 128)
            {
                return SettingsResult<SettingKey>.Fail(
                    SettingsValidationCode.InvalidKey,
                    "Setting key exceeds maximum length.");
            }

            for (var i = 0; i < trimmed.Length; i++)
            {
                var ch = trimmed[i];
                if (!Allowed.Contains(ch))
                {
                    return SettingsResult<SettingKey>.Fail(
                        SettingsValidationCode.InvalidKey,
                        $"Setting key contains invalid character '{ch}'.");
                }
            }

            return SettingsResult<SettingKey>.Success(new SettingKey(trimmed));
        }

        public bool Equals(SettingKey other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SettingKey other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        public int CompareTo(SettingKey other) => string.Compare(Value, other.Value, StringComparison.Ordinal);
        public override string ToString() => Value ?? string.Empty;

        private static HashSet<char> BuildAllowed()
        {
            var set = new HashSet<char>();
            for (var c = 'a'; c <= 'z'; c++) set.Add(c);
            for (var c = 'A'; c <= 'Z'; c++) set.Add(c);
            for (var c = '0'; c <= '9'; c++) set.Add(c);
            set.Add('.');
            set.Add('_');
            set.Add('-');
            return set;
        }
    }

    public readonly struct OptionId : IEquatable<OptionId>, IComparable<OptionId>
    {
        public string Value { get; }

        private OptionId(string value) => Value = value;

        public static SettingsResult<OptionId> TryCreate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return SettingsResult<OptionId>.Fail(
                    SettingsValidationCode.UnknownOption,
                    "Option id must not be empty.");
            }

            var trimmed = value.Trim();
            if (!trimmed.Equals(value, StringComparison.Ordinal) || trimmed.Length > 128)
            {
                return SettingsResult<OptionId>.Fail(
                    SettingsValidationCode.UnknownOption,
                    "Option id is invalid.");
            }

            for (var i = 0; i < trimmed.Length; i++)
            {
                var ch = trimmed[i];
                if (!(char.IsLetterOrDigit(ch) || ch == '.' || ch == '_' || ch == '-'))
                {
                    return SettingsResult<OptionId>.Fail(
                        SettingsValidationCode.UnknownOption,
                        $"Option id contains invalid character '{ch}'.");
                }
            }

            return SettingsResult<OptionId>.Success(new OptionId(trimmed));
        }

        public bool Equals(OptionId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is OptionId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        public int CompareTo(OptionId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);
        public override string ToString() => Value ?? string.Empty;
    }

    public readonly struct SettingValue : IEquatable<SettingValue>
    {
        public SettingValueKind Kind { get; }
        public bool BooleanValue { get; }
        public long IntegerValue { get; }
        public double FloatValue { get; }
        public string StringValue { get; }
        public OptionId OptionValue { get; }

        private SettingValue(
            SettingValueKind kind,
            bool booleanValue,
            long integerValue,
            double floatValue,
            string stringValue,
            OptionId optionValue)
        {
            Kind = kind;
            BooleanValue = booleanValue;
            IntegerValue = integerValue;
            FloatValue = floatValue;
            StringValue = stringValue;
            OptionValue = optionValue;
        }

        public static SettingValue FromBoolean(bool value) => new SettingValue(SettingValueKind.Boolean, value, 0, 0, null, default);
        public static SettingValue FromInteger(long value) => new SettingValue(SettingValueKind.Integer, false, value, 0, null, default);
        public static SettingValue FromFloat(double value) => new SettingValue(SettingValueKind.Float, false, 0, value, null, default);
        public static SettingValue FromString(string value) => new SettingValue(SettingValueKind.String, false, 0, 0, value ?? string.Empty, default);
        public static SettingValue FromOption(OptionId value) => new SettingValue(SettingValueKind.Option, false, 0, 0, null, value);

        public bool Equals(SettingValue other)
        {
            if (Kind != other.Kind) return false;
            return Kind switch
            {
                SettingValueKind.Boolean => BooleanValue == other.BooleanValue,
                SettingValueKind.Integer => IntegerValue == other.IntegerValue,
                SettingValueKind.Float => FloatValue.Equals(other.FloatValue),
                SettingValueKind.String => string.Equals(StringValue, other.StringValue, StringComparison.Ordinal),
                SettingValueKind.Option => OptionValue.Equals(other.OptionValue),
                _ => false,
            };
        }

        public override bool Equals(object obj) => obj is SettingValue other && Equals(other);
        public override int GetHashCode()
        {
            return Kind switch
            {
                SettingValueKind.Boolean => ((int)Kind * 397) ^ BooleanValue.GetHashCode(),
                SettingValueKind.Integer => ((int)Kind * 397) ^ IntegerValue.GetHashCode(),
                SettingValueKind.Float => ((int)Kind * 397) ^ FloatValue.GetHashCode(),
                SettingValueKind.String => ((int)Kind * 397) ^ StringComparer.Ordinal.GetHashCode(StringValue ?? string.Empty),
                SettingValueKind.Option => ((int)Kind * 397) ^ OptionValue.GetHashCode(),
                _ => (int)Kind,
            };
        }
    }

    public static class SettingScopeValidator
    {
        public static SettingsResult Validate(SettingScope scope)
        {
            if (!Enum.IsDefined(typeof(SettingScope), scope))
            {
                return SettingsResult.Fail(
                    SettingsValidationCode.InvalidScope,
                    "Setting scope is invalid.");
            }

            return SettingsResult.Success();
        }
    }

    public readonly struct ScopedSettingKey : IEquatable<ScopedSettingKey>, IComparable<ScopedSettingKey>
    {
        public SettingKey Key { get; }
        public SettingScope Scope { get; }

        private ScopedSettingKey(SettingKey key, SettingScope scope)
        {
            Key = key;
            Scope = scope;
        }

        public static SettingsResult<ScopedSettingKey> TryCreate(SettingKey key, SettingScope scope)
        {
            if (string.IsNullOrEmpty(key.Value))
            {
                return SettingsResult<ScopedSettingKey>.Fail(
                    SettingsValidationCode.InvalidKey,
                    "Setting key must not be empty.");
            }

            var scopeValidation = SettingScopeValidator.Validate(scope);
            if (!scopeValidation.Succeeded)
            {
                return SettingsResult<ScopedSettingKey>.Fail(
                    scopeValidation.Error.Code,
                    scopeValidation.Error.Message,
                    key);
            }

            return SettingsResult<ScopedSettingKey>.Success(new ScopedSettingKey(key, scope));
        }

        public ScopedSettingKey(SettingKey key, SettingScope scope)
        {
            Key = key;
            Scope = scope;
        }

        public bool Equals(ScopedSettingKey other) => Key.Equals(other.Key) && Scope == other.Scope;
        public override bool Equals(object obj) => obj is ScopedSettingKey other && Equals(other);
        public override int GetHashCode() => (Key.GetHashCode() * 397) ^ (int)Scope;
        public int CompareTo(ScopedSettingKey other)
        {
            var keyCompare = Key.CompareTo(other.Key);
            return keyCompare != 0 ? keyCompare : Scope.CompareTo(other.Scope);
        }
    }

    public readonly struct SettingsValidationError
    {
        public SettingsValidationError(SettingsValidationCode code, string message, SettingKey key = default)
        {
            Code = code;
            Message = message ?? string.Empty;
            Key = key;
        }

        public SettingsValidationCode Code { get; }
        public string Message { get; }
        public SettingKey Key { get; }
    }

    public readonly struct SettingsResult
    {
        public bool Succeeded { get; }
        public SettingsValidationError Error { get; }

        private SettingsResult(bool succeeded, SettingsValidationError error)
        {
            Succeeded = succeeded;
            Error = error;
        }

        public static SettingsResult Success() => new SettingsResult(true, default);
        public static SettingsResult Fail(SettingsValidationCode code, string message, SettingKey key = default)
            => new SettingsResult(false, new SettingsValidationError(code, message, key));
    }

    public readonly struct SettingsResult<T>
    {
        public bool Succeeded { get; }
        public T Value { get; }
        public SettingsValidationError Error { get; }

        private SettingsResult(bool succeeded, T value, SettingsValidationError error)
        {
            Succeeded = succeeded;
            Value = value;
            Error = error;
        }

        public static SettingsResult<T> Success(T value) => new SettingsResult<T>(true, value, default);
        public static SettingsResult<T> Fail(SettingsValidationCode code, string message, SettingKey key = default)
            => new SettingsResult<T>(false, default, new SettingsValidationError(code, message, key));
    }
}
