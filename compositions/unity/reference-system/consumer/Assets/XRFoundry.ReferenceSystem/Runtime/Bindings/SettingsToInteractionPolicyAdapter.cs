using System;
using System.Collections.Generic;
using System.Linq;
using Lingkyn.Interaction.Core;
using Lingkyn.Settings.Core;

namespace XRFoundry.ReferenceSystem.Bindings
{
    public enum IntentPolicySettingField
    {
        Enabled = 0,
        ActivationMode = 1,
        HoldDurationTicks = 2,
        ActivationThreshold = 3,
    }

    public enum RoutePolicySettingField
    {
        Enabled = 0,
        Sensitivity = 1,
        Invert = 2,
    }

    public readonly struct IntentPolicySettingBinding
    {
        public IntentPolicySettingBinding(
            ScopedSettingKey setting,
            IntentId intentId,
            IntentPolicySettingField field)
        {
            Setting = setting;
            IntentId = intentId;
            Field = field;
        }

        public ScopedSettingKey Setting { get; }
        public IntentId IntentId { get; }
        public IntentPolicySettingField Field { get; }
    }

    public readonly struct RoutePolicySettingBinding
    {
        public RoutePolicySettingBinding(
            ScopedSettingKey setting,
            RouteId routeId,
            RoutePolicySettingField field)
        {
            Setting = setting;
            RouteId = routeId;
            Field = field;
        }

        public ScopedSettingKey Setting { get; }
        public RouteId RouteId { get; }
        public RoutePolicySettingField Field { get; }
    }

    /// <summary>
    /// Consumer-owned bridge from exact scoped settings to the semantic interaction policy.
    /// The bridge deliberately does not resolve scope precedence: each binding owns one
    /// <see cref="ScopedSettingKey"/> and ignores changes at other scopes.
    /// </summary>
    public sealed class SettingsToInteractionPolicyAdapter : ISettingApplicator
    {
        public const string MomentaryOption = "momentary";
        public const string ToggleOption = "toggle";
        public const string HoldOption = "hold";

        private const string DefaultApplicatorId = "xr-foundry.settings-to-interaction-policy";

        private readonly InteractionCoordinator _interaction;
        private readonly IntentPolicySettingBinding[] _intentBindings;
        private readonly RoutePolicySettingBinding[] _routeBindings;
        private readonly HashSet<SettingKey> _coarseKeys;
        private readonly HashSet<ScopedSettingKey> _exactKeys;

        private SettingsCoordinator _settings;
        private Dictionary<ScopedSettingKey, SettingValue> _boundValues;
        private InteractionPolicySnapshot _rollbackPolicy;
        private Dictionary<ScopedSettingKey, SettingValue> _rollbackBoundValues;
        private bool _hasRollback;

        public SettingsToInteractionPolicyAdapter(
            InteractionCoordinator interaction,
            IEnumerable<IntentPolicySettingBinding> intentBindings = null,
            IEnumerable<RoutePolicySettingBinding> routeBindings = null,
            int order = 0,
            string applicatorId = DefaultApplicatorId)
        {
            _interaction = interaction ?? throw new ArgumentNullException(nameof(interaction));
            if (string.IsNullOrWhiteSpace(applicatorId))
            {
                throw new ArgumentException("Applicator id is required.", nameof(applicatorId));
            }

            ApplicatorId = applicatorId.Trim();
            Order = order;
            _intentBindings = (intentBindings ?? Array.Empty<IntentPolicySettingBinding>()).ToArray();
            _routeBindings = (routeBindings ?? Array.Empty<RoutePolicySettingBinding>()).ToArray();
            _coarseKeys = new HashSet<SettingKey>();
            _exactKeys = new HashSet<ScopedSettingKey>();

            foreach (var binding in _intentBindings)
            {
                _coarseKeys.Add(binding.Setting.Key);
                _exactKeys.Add(binding.Setting);
            }

            foreach (var binding in _routeBindings)
            {
                _coarseKeys.Add(binding.Setting.Key);
                _exactKeys.Add(binding.Setting);
            }
        }

        public string ApplicatorId { get; }
        public int Order { get; }
        public bool IsInitialized => _settings != null;

        /// <summary>
        /// Validates the composition bindings and synchronizes the coordinator's already
        /// loaded (or default) committed snapshot before the first interaction frame.
        /// </summary>
        public SettingsApplicatorStepResult Initialize(SettingsCoordinator settings)
        {
            if (settings == null)
            {
                return Fail("Settings coordinator is required.");
            }

            var bindingError = ValidateBindings(settings);
            if (!string.IsNullOrEmpty(bindingError))
            {
                return Fail(bindingError);
            }

            var snapshotValidation = SettingsSnapshotValidator.ValidateLoaded(
                settings.Registry,
                settings.CommittedSnapshot);
            if (!snapshotValidation.Succeeded)
            {
                return Fail($"Committed settings snapshot is invalid: {snapshotValidation.Error.Message}");
            }

            var values = BuildBoundValues(settings);
            if (!TryComposePolicy(_interaction.Policy, values, out var policy, out var composeError))
            {
                return Fail(composeError);
            }

            _interaction.SetPolicy(policy);
            _settings = settings;
            _boundValues = values;
            ClearRollback();
            return SettingsApplicatorStepResult.Success();
        }

        /// <summary>
        /// Coarse filter required by the Settings Core port. Exact scope matching is
        /// intentionally deferred to <see cref="Apply"/>.
        /// </summary>
        public bool CanApply(SettingKey key) => _coarseKeys.Contains(key);

        public SettingsApplicatorStepResult Apply(IReadOnlyList<SettingChange> changes)
        {
            ClearRollback();
            if (_settings == null || _boundValues == null)
            {
                return Fail("Adapter must be initialized before applying settings.");
            }

            var candidateValues = new Dictionary<ScopedSettingKey, SettingValue>(_boundValues);
            var touched = false;
            foreach (var change in changes ?? Array.Empty<SettingChange>())
            {
                if (!_exactKeys.Contains(change.ScopedKey))
                {
                    continue;
                }

                if (!_settings.Registry.TryGetDefinition(change.Key, out var definition))
                {
                    return Fail($"Bound setting '{change.Key.Value}' is no longer registered.");
                }

                var value = change.HasNewValue ? change.NewValue : definition.DefaultValue;
                var validation = SettingDefinitionValidator.ValidateValue(definition, value);
                if (!validation.Succeeded)
                {
                    return Fail($"Bound setting '{change.Key.Value}' is invalid: {validation.Error.Message}");
                }

                candidateValues[change.ScopedKey] = value;
                touched = true;
            }

            if (!touched)
            {
                return SettingsApplicatorStepResult.Success();
            }

            var originalPolicy = _interaction.Policy;
            if (!TryComposePolicy(originalPolicy, candidateValues, out var candidatePolicy, out var composeError))
            {
                return Fail(composeError);
            }

            // Cache both sides of the effect before committing the validated policy so a
            // later applicator can trigger an exact, object-level rollback.
            _rollbackPolicy = originalPolicy;
            _rollbackBoundValues = new Dictionary<ScopedSettingKey, SettingValue>(_boundValues);
            _hasRollback = true;

            _interaction.SetPolicy(candidatePolicy);
            _boundValues = candidateValues;
            return SettingsApplicatorStepResult.Success();
        }

        public SettingsApplicatorStepResult Rollback(IReadOnlyList<SettingChange> changes)
        {
            if (!_hasRollback)
            {
                return SettingsApplicatorStepResult.Success();
            }

            _interaction.SetPolicy(_rollbackPolicy);
            _boundValues = new Dictionary<ScopedSettingKey, SettingValue>(_rollbackBoundValues);
            ClearRollback();
            return SettingsApplicatorStepResult.Success();
        }

        private string ValidateBindings(SettingsCoordinator settings)
        {
            var intentFields = new HashSet<string>(StringComparer.Ordinal);
            foreach (var binding in _intentBindings)
            {
                if (!Enum.IsDefined(typeof(IntentPolicySettingField), binding.Field))
                {
                    return "Intent policy binding contains an unknown field.";
                }

                var settingError = ValidateSettingBinding(
                    settings,
                    binding.Setting,
                    ExpectedKind(binding.Field));
                if (!string.IsNullOrEmpty(settingError))
                {
                    return settingError;
                }

                if (!_interaction.Registry.TryGetIntent(binding.IntentId, out _))
                {
                    return $"Intent policy binding references unknown intent '{binding.IntentId.Value}'.";
                }

                if (!intentFields.Add($"{binding.IntentId.Value}:{(int)binding.Field}"))
                {
                    return $"Intent policy field '{binding.IntentId.Value}.{binding.Field}' is bound more than once.";
                }
            }

            var routeFields = new HashSet<string>(StringComparer.Ordinal);
            foreach (var binding in _routeBindings)
            {
                if (!Enum.IsDefined(typeof(RoutePolicySettingField), binding.Field))
                {
                    return "Route policy binding contains an unknown field.";
                }

                var settingError = ValidateSettingBinding(
                    settings,
                    binding.Setting,
                    ExpectedKind(binding.Field));
                if (!string.IsNullOrEmpty(settingError))
                {
                    return settingError;
                }

                if (!_interaction.Registry.TryGetRoute(binding.RouteId, out _))
                {
                    return $"Route policy binding references unknown route '{binding.RouteId.Value}'.";
                }

                if (!routeFields.Add($"{binding.RouteId.Value}:{(int)binding.Field}"))
                {
                    return $"Route policy field '{binding.RouteId.Value}.{binding.Field}' is bound more than once.";
                }
            }

            return string.Empty;
        }

        private static string ValidateSettingBinding(
            SettingsCoordinator settings,
            ScopedSettingKey scopedKey,
            SettingValueKind expectedKind)
        {
            var scopedValidation = ScopedSettingKey.TryCreate(scopedKey.Key, scopedKey.Scope);
            if (!scopedValidation.Succeeded)
            {
                return $"Invalid scoped setting binding: {scopedValidation.Error.Message}";
            }

            if (!settings.Registry.TryGetDefinition(scopedKey.Key, out var definition))
            {
                return $"Binding references unknown setting '{scopedKey.Key.Value}'.";
            }

            if (definition.DefaultScope != scopedKey.Scope)
            {
                return $"Binding for '{scopedKey.Key.Value}' must use its declared default scope '{definition.DefaultScope}'.";
            }

            if (definition.Kind != expectedKind)
            {
                return $"Binding for '{scopedKey.Key.Value}' requires kind '{expectedKind}', not '{definition.Kind}'.";
            }

            return string.Empty;
        }

        private Dictionary<ScopedSettingKey, SettingValue> BuildBoundValues(SettingsCoordinator settings)
        {
            var values = new Dictionary<ScopedSettingKey, SettingValue>();
            foreach (var scopedKey in _exactKeys)
            {
                settings.Registry.TryGetDefinition(scopedKey.Key, out var definition);
                values[scopedKey] = settings.CommittedSnapshot.TryGetKnownValue(scopedKey, out var committed)
                    ? committed
                    : definition.DefaultValue;
            }

            return values;
        }

        private bool TryComposePolicy(
            InteractionPolicySnapshot source,
            IReadOnlyDictionary<ScopedSettingKey, SettingValue> values,
            out InteractionPolicySnapshot policy,
            out string error)
        {
            source ??= InteractionPolicySnapshot.Empty;
            var intents = source.IntentPolicies.ToDictionary(entry => entry.IntentId.Value, StringComparer.Ordinal);
            var routes = source.RoutePolicies.ToDictionary(entry => entry.RouteId.Value, StringComparer.Ordinal);

            foreach (var binding in _intentBindings)
            {
                if (!values.TryGetValue(binding.Setting, out var value))
                {
                    policy = null;
                    error = $"No committed value exists for bound setting '{binding.Setting.Key.Value}'.";
                    return false;
                }

                var current = intents.TryGetValue(binding.IntentId.Value, out var existing)
                    ? existing
                    : new IntentPolicyEntry(binding.IntentId, InteractionActivationMode.Momentary, true);
                if (!TryApply(current, binding, value, out var updated, out error))
                {
                    policy = null;
                    return false;
                }

                intents[binding.IntentId.Value] = updated;
            }

            foreach (var binding in _routeBindings)
            {
                if (!values.TryGetValue(binding.Setting, out var value))
                {
                    policy = null;
                    error = $"No committed value exists for bound setting '{binding.Setting.Key.Value}'.";
                    return false;
                }

                var current = routes.TryGetValue(binding.RouteId.Value, out var existing)
                    ? existing
                    : new RoutePolicyEntry(binding.RouteId, true, 1.0, false);
                if (!TryApply(current, binding, value, out var updated, out error))
                {
                    policy = null;
                    return false;
                }

                routes[binding.RouteId.Value] = updated;
            }

            var composed = InteractionPolicySnapshot.Create(intents.Values, routes.Values);
            if (!composed.Succeeded)
            {
                policy = null;
                error = $"Composed interaction policy is invalid: {composed.Error}";
                return false;
            }

            foreach (var intentId in _intentBindings.Select(binding => binding.IntentId).Distinct())
            {
                if (composed.Value.TryGetIntentPolicy(intentId, out var intentPolicy)
                    && intentPolicy.ActivationMode == InteractionActivationMode.Toggle
                    && _interaction.Registry.TryGetIntent(intentId, out var definition)
                    && definition.ValueKind != InteractionValueKind.Button)
                {
                    policy = null;
                    error = $"Toggle activation requires button intent '{intentId.Value}'.";
                    return false;
                }
            }

            policy = composed.Value;
            error = string.Empty;
            return true;
        }

        private static bool TryApply(
            IntentPolicyEntry current,
            IntentPolicySettingBinding binding,
            SettingValue value,
            out IntentPolicyEntry updated,
            out string error)
        {
            if (value.Kind != ExpectedKind(binding.Field))
            {
                updated = default;
                error = $"Setting '{binding.Setting.Key.Value}' has the wrong value kind for '{binding.Field}'.";
                return false;
            }

            var enabled = current.Enabled;
            var activationMode = current.ActivationMode;
            var holdDurationTicks = current.HoldDurationTicks;
            var activationThreshold = current.ActivationThreshold;

            switch (binding.Field)
            {
                case IntentPolicySettingField.Enabled:
                    enabled = value.BooleanValue;
                    break;
                case IntentPolicySettingField.ActivationMode:
                    if (!TryParseActivationMode(value.OptionValue, out activationMode))
                    {
                        updated = default;
                        error = $"Setting '{binding.Setting.Key.Value}' contains unknown activation mode '{value.OptionValue.Value}'.";
                        return false;
                    }

                    break;
                case IntentPolicySettingField.HoldDurationTicks:
                    holdDurationTicks = value.IntegerValue;
                    break;
                case IntentPolicySettingField.ActivationThreshold:
                    activationThreshold = value.FloatValue;
                    break;
                default:
                    updated = default;
                    error = "Intent policy binding contains an unknown field.";
                    return false;
            }

            updated = new IntentPolicyEntry(
                current.IntentId,
                activationMode,
                enabled,
                holdDurationTicks,
                activationThreshold);
            error = string.Empty;
            return true;
        }

        private static bool TryApply(
            RoutePolicyEntry current,
            RoutePolicySettingBinding binding,
            SettingValue value,
            out RoutePolicyEntry updated,
            out string error)
        {
            if (value.Kind != ExpectedKind(binding.Field))
            {
                updated = default;
                error = $"Setting '{binding.Setting.Key.Value}' has the wrong value kind for '{binding.Field}'.";
                return false;
            }

            var enabled = current.Enabled;
            var sensitivity = current.Sensitivity;
            var invert = current.Invert;

            switch (binding.Field)
            {
                case RoutePolicySettingField.Enabled:
                    enabled = value.BooleanValue;
                    break;
                case RoutePolicySettingField.Sensitivity:
                    sensitivity = value.FloatValue;
                    break;
                case RoutePolicySettingField.Invert:
                    invert = value.BooleanValue;
                    break;
                default:
                    updated = default;
                    error = "Route policy binding contains an unknown field.";
                    return false;
            }

            updated = new RoutePolicyEntry(current.RouteId, enabled, sensitivity, invert);
            error = string.Empty;
            return true;
        }

        private static SettingValueKind ExpectedKind(IntentPolicySettingField field)
        {
            switch (field)
            {
                case IntentPolicySettingField.Enabled:
                    return SettingValueKind.Boolean;
                case IntentPolicySettingField.ActivationMode:
                    return SettingValueKind.Option;
                case IntentPolicySettingField.HoldDurationTicks:
                    return SettingValueKind.Integer;
                case IntentPolicySettingField.ActivationThreshold:
                    return SettingValueKind.Float;
                default:
                    return (SettingValueKind)(-1);
            }
        }

        private static SettingValueKind ExpectedKind(RoutePolicySettingField field)
        {
            switch (field)
            {
                case RoutePolicySettingField.Enabled:
                case RoutePolicySettingField.Invert:
                    return SettingValueKind.Boolean;
                case RoutePolicySettingField.Sensitivity:
                    return SettingValueKind.Float;
                default:
                    return (SettingValueKind)(-1);
            }
        }

        private static bool TryParseActivationMode(OptionId option, out InteractionActivationMode mode)
        {
            switch (option.Value)
            {
                case MomentaryOption:
                    mode = InteractionActivationMode.Momentary;
                    return true;
                case ToggleOption:
                    mode = InteractionActivationMode.Toggle;
                    return true;
                case HoldOption:
                    mode = InteractionActivationMode.Hold;
                    return true;
                default:
                    mode = default;
                    return false;
            }
        }

        private SettingsApplicatorStepResult Fail(string message)
            => SettingsApplicatorStepResult.Fail(ApplicatorId, message);

        private void ClearRollback()
        {
            _rollbackPolicy = null;
            _rollbackBoundValues = null;
            _hasRollback = false;
        }
    }
}
