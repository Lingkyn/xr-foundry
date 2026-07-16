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

                return copy;
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

                return copy;
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

    public readonly struct SettingChange
    {
        public SettingChange(ScopedSettingKey scopedKey, SettingValue oldValue, SettingValue newValue, SettingDefinition definition)
        {
            ScopedKey = scopedKey;
            OldValue = oldValue;
            NewValue = newValue;
            Definition = definition;
        }

        public ScopedSettingKey ScopedKey { get; }
        public SettingKey Key => ScopedKey.Key;
        public SettingScope Scope => ScopedKey.Scope;
        public SettingValue OldValue { get; }
        public SettingValue NewValue { get; }
        public SettingDefinition Definition { get; }
    }
}
