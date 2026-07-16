using System;
using System.Collections.Generic;
using System.Linq;

namespace Lingkyn.Settings.Core
{
    public sealed class SettingsSnapshot
    {
        private readonly Dictionary<ScopedSettingKey, SettingValue> _knownValues;
        private readonly Dictionary<string, SettingValue> _unknownValues;

        public SettingsSnapshot(
            long revision,
            IReadOnlyDictionary<ScopedSettingKey, SettingValue> knownValues,
            IReadOnlyDictionary<string, SettingValue> unknownValues)
        {
            Revision = revision;
            _knownValues = new Dictionary<ScopedSettingKey, SettingValue>();
            if (knownValues != null)
            {
                foreach (var pair in knownValues)
                {
                    _knownValues[pair.Key] = pair.Value;
                }
            }

            _unknownValues = new Dictionary<string, SettingValue>(StringComparer.Ordinal);
            if (unknownValues != null)
            {
                foreach (var pair in unknownValues)
                {
                    if (!string.IsNullOrEmpty(pair.Key))
                    {
                        _unknownValues[pair.Key] = pair.Value;
                    }
                }
            }
        }

        public long Revision { get; }

        public bool TryGetKnownValue(ScopedSettingKey scopedKey, out SettingValue value)
            => _knownValues.TryGetValue(scopedKey, out value);

        public bool TryGetUnknownValue(string rawKey, out SettingValue value)
            => _unknownValues.TryGetValue(rawKey, out value);

        public IReadOnlyDictionary<ScopedSettingKey, SettingValue> KnownValues
        {
            get
            {
                var copy = new Dictionary<ScopedSettingKey, SettingValue>();
                foreach (var pair in _knownValues.OrderBy(p => p.Key))
                {
                    copy[pair.Key] = pair.Value;
                }

                return SettingsReadOnly.FreezeDictionary(copy);
            }
        }

        public IReadOnlyDictionary<string, SettingValue> UnknownValues
        {
            get
            {
                var copy = new Dictionary<string, SettingValue>(StringComparer.Ordinal);
                foreach (var pair in _unknownValues.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    copy[pair.Key] = pair.Value;
                }

                return SettingsReadOnly.FreezeDictionary(copy);
            }
        }

        internal SettingsSnapshot WithRevision(long revision)
        {
            return new SettingsSnapshot(revision, _knownValues, _unknownValues);
        }

        internal SettingsSnapshot WithKnownValues(Dictionary<ScopedSettingKey, SettingValue> knownValues)
        {
            return new SettingsSnapshot(Revision, knownValues, _unknownValues);
        }

        public static SettingsSnapshot CreateInitial(SettingsRegistry registry, long revision = 0)
        {
            var known = new Dictionary<ScopedSettingKey, SettingValue>();
            foreach (var definition in registry.Definitions)
            {
                known[new ScopedSettingKey(definition.Key, definition.DefaultScope)] = definition.DefaultValue;
            }

            return new SettingsSnapshot(revision, known, new Dictionary<string, SettingValue>());
        }
    }

    public static class SettingsSnapshotValidator
    {
        public static SettingsResult<SettingsSnapshot> ValidateLoaded(SettingsRegistry registry, SettingsSnapshot snapshot)
        {
            if (registry == null)
            {
                return SettingsResult<SettingsSnapshot>.Fail(
                    SettingsValidationCode.InvalidKey,
                    "Registry is required.");
            }

            if (snapshot == null)
            {
                return SettingsResult<SettingsSnapshot>.Fail(
                    SettingsValidationCode.InvalidKey,
                    "Snapshot is required.");
            }

            foreach (var pair in snapshot.KnownValues)
            {
                var scopeValidation = SettingScopeValidator.Validate(pair.Key.Scope);
                if (!scopeValidation.Succeeded)
                {
                    return SettingsResult<SettingsSnapshot>.Fail(
                        scopeValidation.Error.Code,
                        $"Loaded snapshot contains invalid scope for key '{pair.Key.Key.Value}'.",
                        pair.Key.Key);
                }

                if (!registry.TryGetDefinition(pair.Key.Key, out var definition))
                {
                    return SettingsResult<SettingsSnapshot>.Fail(
                        SettingsValidationCode.InvalidKey,
                        $"Loaded snapshot contains unregistered key '{pair.Key.Key.Value}'.",
                        pair.Key.Key);
                }

                var valueValidation = SettingDefinitionValidator.ValidateValue(definition, pair.Value);
                if (!valueValidation.Succeeded)
                {
                    return SettingsResult<SettingsSnapshot>.Fail(
                        valueValidation.Error.Code,
                        valueValidation.Error.Message,
                        pair.Key.Key);
                }
            }

            return SettingsResult<SettingsSnapshot>.Success(snapshot);
        }
    }

    public readonly struct SettingChange
    {
        public SettingChange(
            ScopedSettingKey scopedKey,
            bool hadOldValue,
            SettingValue oldValue,
            bool hasNewValue,
            SettingValue newValue,
            SettingDefinition definition)
        {
            ScopedKey = scopedKey;
            HadOldValue = hadOldValue;
            OldValue = oldValue;
            HasNewValue = hasNewValue;
            NewValue = newValue;
            Definition = definition;
        }

        public ScopedSettingKey ScopedKey { get; }
        public SettingKey Key => ScopedKey.Key;
        public SettingScope Scope => ScopedKey.Scope;
        public bool HadOldValue { get; }
        public SettingValue OldValue { get; }
        public bool HasNewValue { get; }
        public SettingValue NewValue { get; }
        public SettingDefinition Definition { get; }
    }
}
