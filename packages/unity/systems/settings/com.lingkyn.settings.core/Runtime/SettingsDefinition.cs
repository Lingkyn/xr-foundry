using System;
using System.Collections.Generic;
using System.Linq;

namespace Lingkyn.Settings.Core
{
    public readonly struct NumericConstraint
    {
        public NumericConstraint(double minInclusive, double maxInclusive, double step)
        {
            MinInclusive = minInclusive;
            MaxInclusive = maxInclusive;
            Step = step;
        }

        public double MinInclusive { get; }
        public double MaxInclusive { get; }
        public double Step { get; }
        public bool HasStep => Step > 0;
    }

    public readonly struct StringConstraint
    {
        public StringConstraint(int maxLength) => MaxLength = maxLength;
        public int MaxLength { get; }
    }

    public sealed class OptionConstraint
    {
        private readonly OptionId[] _allowed;

        public OptionConstraint(IEnumerable<OptionId> allowed)
        {
            _allowed = allowed?.ToArray() ?? Array.Empty<OptionId>();
            if (_allowed.Length == 0)
            {
                throw new ArgumentException("Option constraint requires at least one allowed option.", nameof(allowed));
            }

            var seen = new HashSet<OptionId>();
            foreach (var option in _allowed)
            {
                if (!seen.Add(option))
                {
                    throw new ArgumentException("Option constraint contains duplicate option ids.", nameof(allowed));
                }
            }
        }

        public IReadOnlyList<OptionId> Allowed => Array.AsReadOnly(_allowed);

        public bool IsAllowed(OptionId option)
        {
            for (var i = 0; i < _allowed.Length; i++)
            {
                if (_allowed[i].Equals(option))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public readonly struct AccessibilityMetadata
    {
        public AccessibilityMetadata(
            string category,
            string featureId,
            string titleKey,
            string descriptionKey,
            bool previewSupported,
            bool availableBeforeGameplay,
            string documentationKey)
        {
            Category = category ?? string.Empty;
            FeatureId = featureId ?? string.Empty;
            TitleKey = titleKey ?? string.Empty;
            DescriptionKey = descriptionKey ?? string.Empty;
            PreviewSupported = previewSupported;
            AvailableBeforeGameplay = availableBeforeGameplay;
            DocumentationKey = documentationKey ?? string.Empty;
        }

        public string Category { get; }
        public string FeatureId { get; }
        public string TitleKey { get; }
        public string DescriptionKey { get; }
        public bool PreviewSupported { get; }
        public bool AvailableBeforeGameplay { get; }
        public string DocumentationKey { get; }
        public bool IsEmpty =>
            string.IsNullOrEmpty(Category)
            && string.IsNullOrEmpty(FeatureId)
            && string.IsNullOrEmpty(TitleKey)
            && string.IsNullOrEmpty(DescriptionKey)
            && string.IsNullOrEmpty(DocumentationKey)
            && !PreviewSupported
            && !AvailableBeforeGameplay;
    }

    public sealed class SettingDefinition
    {
        public SettingDefinition(
            SettingKey key,
            SettingValueKind kind,
            SettingValue defaultValue,
            SettingScope defaultScope,
            int applicationOrder,
            bool requiresRestart,
            NumericConstraint? numericConstraint,
            StringConstraint? stringConstraint,
            OptionConstraint optionConstraint,
            AccessibilityMetadata accessibility)
        {
            Key = key;
            Kind = kind;
            DefaultValue = defaultValue;
            DefaultScope = defaultScope;
            ApplicationOrder = applicationOrder;
            RequiresRestart = requiresRestart;
            NumericConstraint = numericConstraint;
            StringConstraint = stringConstraint;
            OptionConstraint = optionConstraint;
            Accessibility = accessibility;
        }

        public SettingKey Key { get; }
        public SettingValueKind Kind { get; }
        public SettingValue DefaultValue { get; }
        public SettingScope DefaultScope { get; }
        public int ApplicationOrder { get; }
        public bool RequiresRestart { get; }
        public NumericConstraint? NumericConstraint { get; }
        public StringConstraint? StringConstraint { get; }
        public OptionConstraint OptionConstraint { get; }
        public AccessibilityMetadata Accessibility { get; }
    }

    public static class SettingDefinitionValidator
    {
        public static SettingsResult<SettingDefinition> ValidateBuilt(
            SettingKey key,
            SettingValueKind kind,
            SettingValue defaultValue,
            SettingScope defaultScope,
            int applicationOrder,
            bool requiresRestart,
            NumericConstraint? numericConstraint,
            StringConstraint? stringConstraint,
            OptionConstraint optionConstraint,
            AccessibilityMetadata accessibility)
        {
            var scopeValidation = SettingScopeValidator.Validate(defaultScope);
            if (!scopeValidation.Succeeded)
            {
                return SettingsResult<SettingDefinition>.Fail(
                    scopeValidation.Error.Code,
                    scopeValidation.Error.Message,
                    key);
            }

            if (defaultValue.Kind != kind)
            {
                return SettingsResult<SettingDefinition>.Fail(
                    SettingsValidationCode.KindMismatch,
                    "Default value kind does not match definition kind.",
                    key);
            }

            var valueValidation = ValidateValue(kind, defaultValue, numericConstraint, stringConstraint, optionConstraint, key);
            if (!valueValidation.Succeeded)
            {
                return SettingsResult<SettingDefinition>.Fail(
                    SettingsValidationCode.InvalidDefault,
                    valueValidation.Error.Message,
                    key);
            }

            if (kind == SettingValueKind.Integer || kind == SettingValueKind.Float)
            {
                if (!numericConstraint.HasValue)
                {
                    return SettingsResult<SettingDefinition>.Fail(
                        SettingsValidationCode.OutOfRange,
                        "Numeric definitions require a numeric constraint.",
                        key);
                }
            }

            if (kind == SettingValueKind.String && !stringConstraint.HasValue)
            {
                return SettingsResult<SettingDefinition>.Fail(
                    SettingsValidationCode.StringTooLong,
                    "String definitions require a string constraint.",
                    key);
            }

            if (kind == SettingValueKind.Option && optionConstraint == null)
            {
                return SettingsResult<SettingDefinition>.Fail(
                    SettingsValidationCode.UnknownOption,
                    "Option definitions require an option constraint.",
                    key);
            }

            if (numericConstraint.HasValue)
            {
                var numericValidation = ValidateNumericConstraint(numericConstraint.Value, key);
                if (!numericValidation.Succeeded)
                {
                    return SettingsResult<SettingDefinition>.Fail(
                        numericValidation.Error.Code,
                        numericValidation.Error.Message,
                        key);
                }
            }

            if (stringConstraint.HasValue && stringConstraint.Value.MaxLength <= 0)
            {
                return SettingsResult<SettingDefinition>.Fail(
                    SettingsValidationCode.StringTooLong,
                    "String max length must be positive.",
                    key);
            }

            return SettingsResult<SettingDefinition>.Success(
                new SettingDefinition(
                    key,
                    kind,
                    defaultValue,
                    defaultScope,
                    applicationOrder,
                    requiresRestart,
                    numericConstraint,
                    stringConstraint,
                    optionConstraint,
                    accessibility));
        }

        public static SettingsResult ValidateValue(
            SettingDefinition definition,
            SettingValue value)
        {
            if (value.Kind != definition.Kind)
            {
                return SettingsResult.Fail(
                    SettingsValidationCode.KindMismatch,
                    "Value kind does not match definition kind.",
                    definition.Key);
            }

            return ValidateValue(
                definition.Kind,
                value,
                definition.NumericConstraint,
                definition.StringConstraint,
                definition.OptionConstraint,
                definition.Key);
        }

        public static SettingsResult ValidateNumericConstraint(NumericConstraint numeric, SettingKey key = default)
        {
            if (IsNonFinite(numeric.MinInclusive) || IsNonFinite(numeric.MaxInclusive) || IsNonFinite(numeric.Step))
            {
                return SettingsResult.Fail(
                    SettingsValidationCode.NonFiniteFloat,
                    "Numeric constraint bounds and step must be finite.",
                    key);
            }

            if (numeric.Step < 0)
            {
                return SettingsResult.Fail(
                    SettingsValidationCode.InvalidStep,
                    "Numeric step must not be negative.",
                    key);
            }

            if (numeric.MinInclusive > numeric.MaxInclusive)
            {
                return SettingsResult.Fail(
                    SettingsValidationCode.OutOfRange,
                    "Numeric min must not exceed max.",
                    key);
            }

            return SettingsResult.Success();
        }

        private static bool IsNonFinite(double value) => double.IsNaN(value) || double.IsInfinity(value);

        private static SettingsResult ValidateValue(
            SettingValueKind kind,
            SettingValue value,
            NumericConstraint? numericConstraint,
            StringConstraint? stringConstraint,
            OptionConstraint optionConstraint,
            SettingKey key)
        {
            switch (kind)
            {
                case SettingValueKind.Boolean:
                    return SettingsResult.Success();
                case SettingValueKind.Integer:
                {
                    if (!numericConstraint.HasValue)
                    {
                        return SettingsResult.Fail(SettingsValidationCode.OutOfRange, "Missing numeric constraint.", key);
                    }

                    var numeric = numericConstraint.Value;
                    if (value.IntegerValue < numeric.MinInclusive || value.IntegerValue > numeric.MaxInclusive)
                    {
                        return SettingsResult.Fail(SettingsValidationCode.OutOfRange, "Integer value is out of range.", key);
                    }

                    if (numeric.HasStep)
                    {
                        var offset = value.IntegerValue - numeric.MinInclusive;
                        var remainder = offset % numeric.Step;
                        if (Math.Abs(remainder) > 1e-9 && Math.Abs(remainder - numeric.Step) > 1e-9)
                        {
                            return SettingsResult.Fail(SettingsValidationCode.InvalidStep, "Integer value does not align to step.", key);
                        }
                    }

                    return SettingsResult.Success();
                }
                case SettingValueKind.Float:
                {
                    if (double.IsNaN(value.FloatValue) || double.IsInfinity(value.FloatValue))
                    {
                        return SettingsResult.Fail(SettingsValidationCode.NonFiniteFloat, "Float value must be finite.", key);
                    }

                    if (!numericConstraint.HasValue)
                    {
                        return SettingsResult.Fail(SettingsValidationCode.OutOfRange, "Missing numeric constraint.", key);
                    }

                    var numeric = numericConstraint.Value;
                    if (value.FloatValue < numeric.MinInclusive || value.FloatValue > numeric.MaxInclusive)
                    {
                        return SettingsResult.Fail(SettingsValidationCode.OutOfRange, "Float value is out of range.", key);
                    }

                    if (numeric.HasStep)
                    {
                        var offset = value.FloatValue - numeric.MinInclusive;
                        var steps = offset / numeric.Step;
                        if (Math.Abs(steps - Math.Round(steps)) > 1e-6)
                        {
                            return SettingsResult.Fail(SettingsValidationCode.InvalidStep, "Float value does not align to step.", key);
                        }
                    }

                    return SettingsResult.Success();
                }
                case SettingValueKind.String:
                {
                    if (!stringConstraint.HasValue)
                    {
                        return SettingsResult.Fail(SettingsValidationCode.StringTooLong, "Missing string constraint.", key);
                    }

                    var length = value.StringValue?.Length ?? 0;
                    if (length > stringConstraint.Value.MaxLength)
                    {
                        return SettingsResult.Fail(SettingsValidationCode.StringTooLong, "String value exceeds max length.", key);
                    }

                    return SettingsResult.Success();
                }
                case SettingValueKind.Option:
                {
                    if (optionConstraint == null || !optionConstraint.IsAllowed(value.OptionValue))
                    {
                        return SettingsResult.Fail(SettingsValidationCode.UnknownOption, "Option value is not allowed.", key);
                    }

                    return SettingsResult.Success();
                }
                default:
                    return SettingsResult.Fail(SettingsValidationCode.KindMismatch, "Unknown value kind.", key);
            }
        }
    }
}
