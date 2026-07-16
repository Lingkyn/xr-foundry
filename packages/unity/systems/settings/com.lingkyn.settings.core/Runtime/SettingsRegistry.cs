using System;
using System.Collections.Generic;
using System.Linq;

namespace Lingkyn.Settings.Core
{
    public sealed class SettingsRegistry
    {
        private readonly SettingDefinition[] _definitions;
        private readonly Dictionary<string, SettingDefinition> _lookup;

        private SettingsRegistry(IReadOnlyList<SettingDefinition> definitions)
        {
            _definitions = definitions.OrderBy(d => d.Key, Comparer<SettingKey>.Default).ToArray();
            _lookup = new Dictionary<string, SettingDefinition>(StringComparer.Ordinal);
            foreach (var definition in _definitions)
            {
                _lookup[definition.Key.Value] = definition;
            }
        }

        public IReadOnlyList<SettingDefinition> Definitions => _definitions;

        public bool TryGetDefinition(SettingKey key, out SettingDefinition definition)
            => _lookup.TryGetValue(key.Value, out definition);

        public static SettingsResult<SettingsRegistry> Create(IEnumerable<SettingDefinition> definitions)
        {
            if (definitions == null)
            {
                return SettingsResult<SettingsRegistry>.Fail(
                    SettingsValidationCode.InvalidKey,
                    "Definitions are required.");
            }

            var list = new List<SettingDefinition>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var definition in definitions)
            {
                if (definition == null)
                {
                    return SettingsResult<SettingsRegistry>.Fail(
                        SettingsValidationCode.InvalidKey,
                        "Definition must not be null.");
                }

                if (!seen.Add(definition.Key.Value))
                {
                    return SettingsResult<SettingsRegistry>.Fail(
                        SettingsValidationCode.DuplicateDefinition,
                        "Duplicate setting definition detected.",
                        definition.Key);
                }

                list.Add(definition);
            }

            return SettingsResult<SettingsRegistry>.Success(new SettingsRegistry(list));
        }
    }

    public sealed class SettingsProfileLayer
    {
        private readonly Dictionary<string, SettingValue> _overrides;

        public SettingsProfileLayer(string layerId, IReadOnlyDictionary<SettingKey, SettingValue> overrides)
        {
            LayerId = layerId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(LayerId))
            {
                throw new ArgumentException("Profile layer id is required.", nameof(layerId));
            }

            _overrides = new Dictionary<string, SettingValue>(StringComparer.Ordinal);
            if (overrides != null)
            {
                foreach (var pair in overrides)
                {
                    if (!_overrides.ContainsKey(pair.Key.Value))
                    {
                        _overrides[pair.Key.Value] = pair.Value;
                        continue;
                    }

                    throw new ArgumentException(
                        $"Duplicate override for key '{pair.Key.Value}' in profile layer '{LayerId}'.",
                        nameof(overrides));
                }
            }
        }

        public string LayerId { get; }

        public bool TryGetOverride(SettingKey key, out SettingValue value)
            => _overrides.TryGetValue(key.Value, out value);

        public IReadOnlyDictionary<SettingKey, SettingValue> Overrides
        {
            get
            {
                var result = new Dictionary<SettingKey, SettingValue>();
                foreach (var pair in _overrides.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    var keyResult = SettingKey.TryCreate(pair.Key);
                    if (keyResult.Succeeded)
                    {
                        result[keyResult.Value] = pair.Value;
                    }
                }

                return result;
            }
        }
    }

    public sealed class SettingsProfile
    {
        private readonly SettingsProfileLayer[] _layers;

        public SettingsProfile(string profileId, IEnumerable<SettingsProfileLayer> layers)
        {
            ProfileId = profileId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(ProfileId))
            {
                throw new ArgumentException("Profile id is required.", nameof(profileId));
            }

            _layers = layers?.ToArray() ?? Array.Empty<SettingsProfileLayer>();
        }

        public string ProfileId { get; }
        public IReadOnlyList<SettingsProfileLayer> Layers => _layers;

        public static SettingsResult<SettingsProfile> Create(string profileId, IEnumerable<SettingsProfileLayer> layers)
        {
            try
            {
                return SettingsResult<SettingsProfile>.Success(new SettingsProfile(profileId, layers));
            }
            catch (ArgumentException ex)
            {
                return SettingsResult<SettingsProfile>.Fail(
                    SettingsValidationCode.DuplicateProfileOverride,
                    ex.Message);
            }
        }
    }
}
