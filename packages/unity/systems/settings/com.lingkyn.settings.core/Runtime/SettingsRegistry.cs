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

        public IReadOnlyList<SettingDefinition> Definitions => Array.AsReadOnly(_definitions);

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
        private readonly IReadOnlyDictionary<SettingKey, SettingValue> _overrides;

        public SettingsProfileLayer(string layerId, IReadOnlyDictionary<SettingKey, SettingValue> overrides)
        {
            LayerId = layerId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(LayerId))
            {
                throw new ArgumentException("Profile layer id is required.", nameof(layerId));
            }

            var built = new Dictionary<SettingKey, SettingValue>();
            if (overrides != null)
            {
                foreach (var pair in overrides)
                {
                    if (!built.ContainsKey(pair.Key))
                    {
                        built[pair.Key] = pair.Value;
                        continue;
                    }

                    throw new ArgumentException(
                        $"Duplicate override for key '{pair.Key.Value}' in profile layer '{LayerId}'.",
                        nameof(overrides));
                }
            }

            _overrides = SettingsReadOnly.FreezeDictionary(built);
        }

        public string LayerId { get; }

        public bool TryGetOverride(SettingKey key, out SettingValue value)
        {
            foreach (var pair in _overrides)
            {
                if (pair.Key.Equals(key))
                {
                    value = pair.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }

        public IReadOnlyDictionary<SettingKey, SettingValue> Overrides => _overrides;
    }

    public sealed class SettingsProfile
    {
        private readonly SettingsProfileLayer[] _layers;

        private SettingsProfile(string profileId, SettingsProfileLayer[] layers)
        {
            ProfileId = profileId;
            _layers = layers ?? Array.Empty<SettingsProfileLayer>();
        }

        public string ProfileId { get; }
        public IReadOnlyList<SettingsProfileLayer> Layers => Array.AsReadOnly(_layers);

        public static SettingsResult<SettingsProfile> Create(string profileId, IEnumerable<SettingsProfileLayer> layers)
        {
            if (string.IsNullOrWhiteSpace(profileId))
            {
                return SettingsResult<SettingsProfile>.Fail(
                    SettingsValidationCode.InvalidProfileLayer,
                    "Profile id is required.");
            }

            if (layers == null)
            {
                return SettingsResult<SettingsProfile>.Fail(
                    SettingsValidationCode.InvalidProfileLayer,
                    "Profile layers are required.");
            }

            var layerList = new List<SettingsProfileLayer>();
            var seenLayerIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var layer in layers)
            {
                if (layer == null)
                {
                    return SettingsResult<SettingsProfile>.Fail(
                        SettingsValidationCode.InvalidProfileLayer,
                        "Profile layer must not be null.");
                }

                if (!seenLayerIds.Add(layer.LayerId))
                {
                    return SettingsResult<SettingsProfile>.Fail(
                        SettingsValidationCode.InvalidProfileLayer,
                        $"Duplicate profile layer id '{layer.LayerId}'.");
                }

                layerList.Add(layer);
            }

            return SettingsResult<SettingsProfile>.Success(new SettingsProfile(profileId, layerList.ToArray()));
        }
    }
}
