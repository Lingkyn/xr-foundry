using System;
using System.Collections.Generic;
using Lingkyn.Settings.Core;
using UnityEngine;

namespace Lingkyn.Settings.Unity
{
    public readonly struct SettingsUnityValidationIssue
    {
        public SettingsUnityValidationIssue(string assetPath, int index, string key, string message)
        {
            AssetPath = assetPath ?? string.Empty;
            Index = index;
            Key = key ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string AssetPath { get; }
        public int Index { get; }
        public string Key { get; }
        public string Message { get; }
    }

    public static class SettingsUnityConverter
    {
        public static SettingsResult<SettingsRegistry> ConvertCatalog(SettingsCatalogAsset catalog)
        {
            if (catalog == null)
            {
                return SettingsResult<SettingsRegistry>.Fail(
                    SettingsValidationCode.InvalidKey,
                    "Catalog asset is required.");
            }

            var definitions = new List<SettingDefinition>();
            var assetPath = GetAssetPath(catalog);
            for (var i = 0; i < (catalog.definitions?.Length ?? 0); i++)
            {
                var definitionAsset = catalog.definitions[i];
                var converted = ConvertDefinition(definitionAsset, assetPath, i);
                if (!converted.Succeeded)
                {
                    return SettingsResult<SettingsRegistry>.Fail(
                        converted.Error.Code,
                        FormatIssue(assetPath, i, definitionAsset?.key, converted.Error.Message),
                        converted.Error.Key);
                }

                definitions.Add(converted.Value);
            }

            return SettingsRegistry.Create(definitions);
        }

        public static SettingsResult<SettingDefinition> ConvertDefinition(
            SettingDefinitionAsset asset,
            string assetPath = null,
            int index = -1)
        {
            if (asset == null)
            {
                return SettingsResult<SettingDefinition>.Fail(
                    SettingsValidationCode.InvalidKey,
                    FormatIssue(assetPath, index, null, "Definition asset is required."));
            }

            var keyResult = SettingKey.TryCreate(asset.key);
            if (!keyResult.Succeeded)
            {
                return SettingsResult<SettingDefinition>.Fail(
                    keyResult.Error.Code,
                    FormatIssue(assetPath, index, asset.key, keyResult.Error.Message),
                    keyResult.Error.Key);
            }

            var kind = ConvertKind(asset.kind);
            var defaultValueResult = ConvertDefaultValue(asset, kind, keyResult.Value, assetPath, index);
            if (!defaultValueResult.Succeeded)
            {
                return defaultValueResult;
            }

            NumericConstraint? numeric = null;
            StringConstraint? stringConstraint = null;
            OptionConstraint optionConstraint = null;

            if (kind == SettingValueKind.Integer || kind == SettingValueKind.Float)
            {
                if (asset.numericConstraint == null || !asset.numericConstraint.enabled)
                {
                    return SettingsResult<SettingDefinition>.Fail(
                        SettingsValidationCode.OutOfRange,
                        FormatIssue(assetPath, index, asset.key, "Numeric constraint is required."),
                        keyResult.Value);
                }

                numeric = new NumericConstraint(
                    asset.numericConstraint.minInclusive,
                    asset.numericConstraint.maxInclusive,
                    asset.numericConstraint.hasStep ? asset.numericConstraint.step : 0);
            }

            if (kind == SettingValueKind.String)
            {
                if (asset.stringConstraint == null || !asset.stringConstraint.enabled)
                {
                    return SettingsResult<SettingDefinition>.Fail(
                        SettingsValidationCode.StringTooLong,
                        FormatIssue(assetPath, index, asset.key, "String constraint is required."),
                        keyResult.Value);
                }

                stringConstraint = new StringConstraint(asset.stringConstraint.maxLength);
            }

            if (kind == SettingValueKind.Option)
            {
                var optionResult = ConvertOptionConstraint(asset, keyResult.Value, assetPath, index);
                if (!optionResult.Succeeded)
                {
                    return SettingsResult<SettingDefinition>.Fail(
                        optionResult.Error.Code,
                        FormatIssue(assetPath, index, asset.key, optionResult.Error.Message),
                        keyResult.Value);
                }

                optionConstraint = optionResult.Value;
            }

            var accessibility = ConvertAccessibility(asset.accessibility);
            return SettingDefinitionValidator.ValidateBuilt(
                keyResult.Value,
                kind,
                defaultValueResult.Value,
                ConvertScope(asset.defaultScope),
                asset.applicationOrder,
                asset.requiresRestart,
                numeric,
                stringConstraint,
                optionConstraint,
                accessibility);
        }

        public static SettingsResult<SettingsProfile> ConvertProfile(
            SettingsProfileAsset asset,
            SettingsRegistry registry,
            string assetPath = null)
        {
            if (asset == null)
            {
                return SettingsResult<SettingsProfile>.Fail(
                    SettingsValidationCode.InvalidProfileLayer,
                    "Profile asset is required.");
            }

            var path = assetPath ?? GetAssetPath(asset);
            var layers = new List<SettingsProfileLayer>();
            for (var layerIndex = 0; layerIndex < (asset.layers?.Length ?? 0); layerIndex++)
            {
                var layerRecord = asset.layers[layerIndex];
                var overrides = new Dictionary<SettingKey, SettingValue>();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (var overrideIndex = 0; overrideIndex < (layerRecord.overrides?.Length ?? 0); overrideIndex++)
                {
                    var overrideRecord = layerRecord.overrides[overrideIndex];
                    if (overrideRecord?.definition == null)
                    {
                        return SettingsResult<SettingsProfile>.Fail(
                            SettingsValidationCode.InvalidKey,
                            $"{path} layer[{layerIndex}] override[{overrideIndex}]: definition reference is required.");
                    }

                    var definitionResult = ConvertDefinition(overrideRecord.definition, path, overrideIndex);
                    if (!definitionResult.Succeeded)
                    {
                        return SettingsResult<SettingsProfile>.Fail(
                            definitionResult.Error.Code,
                            $"{path} layer[{layerIndex}] override[{overrideIndex}]: {definitionResult.Error.Message}",
                            definitionResult.Error.Key);
                    }

                    var key = definitionResult.Value.Key;
                    if (!seen.Add(key.Value))
                    {
                        return SettingsResult<SettingsProfile>.Fail(
                            SettingsValidationCode.DuplicateProfileOverride,
                            $"{path} layer[{layerIndex}] duplicate override for key '{key.Value}'.",
                            key);
                    }

                    if (!registry.TryGetDefinition(key, out _))
                    {
                        return SettingsResult<SettingsProfile>.Fail(
                            SettingsValidationCode.InvalidKey,
                            $"{path} layer[{layerIndex}] override key '{key.Value}' is not registered in catalog.",
                            key);
                    }

                    var valueResult = ConvertOverrideValue(overrideRecord, definitionResult.Value.Kind, key, path, layerIndex, overrideIndex);
                    if (!valueResult.Succeeded)
                    {
                        return SettingsResult<SettingsProfile>.Fail(
                            valueResult.Error.Code,
                            valueResult.Error.Message,
                            key);
                    }

                    overrides[key] = valueResult.Value;
                }

                try
                {
                    layers.Add(new SettingsProfileLayer(layerRecord.layerId, overrides));
                }
                catch (ArgumentException ex)
                {
                    return SettingsResult<SettingsProfile>.Fail(
                        SettingsValidationCode.DuplicateProfileOverride,
                        $"{path} layer[{layerIndex}]: {ex.Message}");
                }
            }

            return SettingsProfile.Create(asset.profileId, layers);
        }

        public static AccessibilityMetadata ConvertAccessibility(AccessibilityMetadataRecord record)
        {
            if (record == null)
            {
                return default;
            }

            return new AccessibilityMetadata(
                record.category,
                record.featureId,
                record.titleKey,
                record.descriptionKey,
                record.previewSupported,
                record.availableBeforeGameplay,
                record.documentationKey);
        }

        private static SettingsResult<SettingValue> ConvertDefaultValue(
            SettingDefinitionAsset asset,
            SettingValueKind kind,
            SettingKey key,
            string assetPath,
            int index)
        {
            switch (kind)
            {
                case SettingValueKind.Boolean:
                    return SettingsResult<SettingValue>.Success(SettingValue.FromBoolean(asset.defaultBoolean));
                case SettingValueKind.Integer:
                    return SettingsResult<SettingValue>.Success(SettingValue.FromInteger(asset.defaultInteger));
                case SettingValueKind.Float:
                    return SettingsResult<SettingValue>.Success(SettingValue.FromFloat(asset.defaultFloat));
                case SettingValueKind.String:
                    return SettingsResult<SettingValue>.Success(SettingValue.FromString(asset.defaultString ?? string.Empty));
                case SettingValueKind.Option:
                {
                    var option = OptionId.TryCreate(asset.defaultOptionId);
                    if (!option.Succeeded)
                    {
                        return SettingsResult<SettingValue>.Fail(
                            option.Error.Code,
                            FormatIssue(assetPath, index, asset.key, option.Error.Message),
                            key);
                    }

                    return SettingsResult<SettingValue>.Success(SettingValue.FromOption(option.Value));
                }
                default:
                    return SettingsResult<SettingValue>.Fail(
                        SettingsValidationCode.KindMismatch,
                        FormatIssue(assetPath, index, asset.key, "Unknown kind."),
                        key);
            }
        }

        private static SettingsResult<SettingValue> ConvertOverrideValue(
            ProfileOverrideRecord record,
            SettingValueKind expectedKind,
            SettingKey key,
            string assetPath,
            int layerIndex,
            int overrideIndex)
        {
            var kind = ConvertKind(record.kind);
            if (kind != expectedKind)
            {
                return SettingsResult<SettingValue>.Fail(
                    SettingsValidationCode.KindMismatch,
                    $"{assetPath} layer[{layerIndex}] override[{overrideIndex}] kind mismatch for '{key.Value}'.",
                    key);
            }

            switch (kind)
            {
                case SettingValueKind.Boolean:
                    return SettingsResult<SettingValue>.Success(SettingValue.FromBoolean(record.booleanValue));
                case SettingValueKind.Integer:
                    return SettingsResult<SettingValue>.Success(SettingValue.FromInteger(record.integerValue));
                case SettingValueKind.Float:
                    return SettingsResult<SettingValue>.Success(SettingValue.FromFloat(record.floatValue));
                case SettingValueKind.String:
                    return SettingsResult<SettingValue>.Success(SettingValue.FromString(record.stringValue ?? string.Empty));
                case SettingValueKind.Option:
                {
                    var option = OptionId.TryCreate(record.optionId);
                    if (!option.Succeeded)
                    {
                        return SettingsResult<SettingValue>.Fail(
                            option.Error.Code,
                            $"{assetPath} layer[{layerIndex}] override[{overrideIndex}] invalid option id.",
                            key);
                    }

                    return SettingsResult<SettingValue>.Success(SettingValue.FromOption(option.Value));
                }
                default:
                    return SettingsResult<SettingValue>.Fail(
                        SettingsValidationCode.KindMismatch,
                        $"{assetPath} layer[{layerIndex}] override[{overrideIndex}] unknown kind.",
                        key);
            }
        }

        private static SettingsResult<OptionConstraint> ConvertOptionConstraint(
            SettingDefinitionAsset asset,
            SettingKey key,
            string assetPath,
            int index)
        {
            var entries = asset.optionConstraint?.options;
            if (entries == null || entries.Length == 0)
            {
                return SettingsResult<OptionConstraint>.Fail(
                    SettingsValidationCode.UnknownOption,
                    FormatIssue(assetPath, index, asset.key, "Option constraint requires allowed options."),
                    key);
            }

            var allowed = new List<OptionId>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < entries.Length; i++)
            {
                var option = OptionId.TryCreate(entries[i]?.optionId);
                if (!option.Succeeded)
                {
                    return SettingsResult<OptionConstraint>.Fail(
                        option.Error.Code,
                        FormatIssue(assetPath, index, asset.key, $"Option[{i}] is invalid."),
                        key);
                }

                if (!seen.Add(option.Value.Value))
                {
                    return SettingsResult<OptionConstraint>.Fail(
                        SettingsValidationCode.UnknownOption,
                        FormatIssue(assetPath, index, asset.key, $"Duplicate option id '{option.Value.Value}'."),
                        key);
                }

                allowed.Add(option.Value);
            }

            try
            {
                return SettingsResult<OptionConstraint>.Success(new OptionConstraint(allowed));
            }
            catch (ArgumentException ex)
            {
                return SettingsResult<OptionConstraint>.Fail(
                    SettingsValidationCode.UnknownOption,
                    FormatIssue(assetPath, index, asset.key, ex.Message),
                    key);
            }
        }

        private static SettingValueKind ConvertKind(SettingValueKindRecord kind)
        {
            return (SettingValueKind)(int)kind;
        }

        private static SettingScope ConvertScope(SettingScopeRecord scope)
        {
            return (SettingScope)(int)scope;
        }

        private static string GetAssetPath(ScriptableObject asset)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.GetAssetPath(asset) ?? asset.name;
#else
            return asset.name;
#endif
        }

        private static string FormatIssue(string assetPath, int index, string key, string message)
        {
            var location = string.IsNullOrEmpty(assetPath) ? "asset" : assetPath;
            if (index >= 0)
            {
                location += $"[{index}]";
            }

            if (!string.IsNullOrEmpty(key))
            {
                location += $" key='{key}'";
            }

            return $"{location}: {message}";
        }
    }

    public static class SettingsUnityValidator
    {
        public static IReadOnlyList<SettingsUnityValidationIssue> ValidateCatalog(SettingsCatalogAsset catalog)
        {
            var issues = new List<SettingsUnityValidationIssue>();
            if (catalog == null)
            {
                issues.Add(new SettingsUnityValidationIssue(string.Empty, -1, string.Empty, "Catalog asset is required."));
                return issues;
            }

            var assetPath = catalog.name;
#if UNITY_EDITOR
            assetPath = UnityEditor.AssetDatabase.GetAssetPath(catalog) ?? catalog.name;
#endif
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < (catalog.definitions?.Length ?? 0); i++)
            {
                var definition = catalog.definitions[i];
                var converted = SettingsUnityConverter.ConvertDefinition(definition, assetPath, i);
                if (!converted.Succeeded)
                {
                    issues.Add(new SettingsUnityValidationIssue(assetPath, i, definition?.key, converted.Error.Message));
                    continue;
                }

                if (!seen.Add(converted.Value.Key.Value))
                {
                    issues.Add(new SettingsUnityValidationIssue(
                        assetPath,
                        i,
                        converted.Value.Key.Value,
                        "Duplicate definition key."));
                }
            }

            return issues;
        }
    }
}
