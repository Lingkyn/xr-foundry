using System;
using System.Collections.Generic;
using System.Linq;

namespace Lingkyn.Interaction.Core
{
    public readonly struct IntentPolicyEntry : IEquatable<IntentPolicyEntry>
    {
        public IntentPolicyEntry(
            IntentId intentId,
            InteractionActivationMode activationMode,
            bool enabled)
        {
            IntentId = intentId;
            ActivationMode = activationMode;
            Enabled = enabled;
        }

        public IntentId IntentId { get; }
        public InteractionActivationMode ActivationMode { get; }
        public bool Enabled { get; }

        public bool Equals(IntentPolicyEntry other) =>
            IntentId.Equals(other.IntentId)
            && ActivationMode == other.ActivationMode
            && Enabled == other.Enabled;

        public override bool Equals(object obj) => obj is IntentPolicyEntry other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(IntentId, ActivationMode, Enabled);
    }

    public readonly struct RoutePolicyEntry : IEquatable<RoutePolicyEntry>
    {
        public RoutePolicyEntry(RouteId routeId, bool enabled, double sensitivity, bool invert)
        {
            RouteId = routeId;
            Enabled = enabled;
            Sensitivity = sensitivity;
            Invert = invert;
        }

        public RouteId RouteId { get; }
        public bool Enabled { get; }
        public double Sensitivity { get; }
        public bool Invert { get; }

        public bool Equals(RoutePolicyEntry other) =>
            RouteId.Equals(other.RouteId)
            && Enabled == other.Enabled
            && Sensitivity.Equals(other.Sensitivity)
            && Invert == other.Invert;

        public override bool Equals(object obj) => obj is RoutePolicyEntry other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(RouteId, Enabled, Sensitivity, Invert);
    }

    public sealed class InteractionPolicySnapshot
    {
        private readonly IReadOnlyDictionary<string, IntentPolicyEntry> _intentPolicies;
        private readonly IReadOnlyDictionary<string, RoutePolicyEntry> _routePolicies;

        private InteractionPolicySnapshot(
            IReadOnlyDictionary<string, IntentPolicyEntry> intentPolicies,
            IReadOnlyDictionary<string, RoutePolicyEntry> routePolicies)
        {
            _intentPolicies = intentPolicies;
            _routePolicies = routePolicies;
        }

        public IReadOnlyList<IntentPolicyEntry> IntentPolicies =>
            InteractionReadOnly.FreezeList(_intentPolicies.Values.OrderBy(entry => entry.IntentId, Comparer<IntentId>.Default));

        public IReadOnlyList<RoutePolicyEntry> RoutePolicies =>
            InteractionReadOnly.FreezeList(_routePolicies.Values.OrderBy(entry => entry.RouteId, Comparer<RouteId>.Default));

        public bool TryGetIntentPolicy(IntentId intentId, out IntentPolicyEntry entry)
        {
            return _intentPolicies.TryGetValue(intentId.Value ?? string.Empty, out entry);
        }

        public bool TryGetRoutePolicy(RouteId routeId, out RoutePolicyEntry entry)
        {
            return _routePolicies.TryGetValue(routeId.Value ?? string.Empty, out entry);
        }

        public static InteractionResult<InteractionPolicySnapshot> Create(
            IEnumerable<IntentPolicyEntry> intentPolicies,
            IEnumerable<RoutePolicyEntry> routePolicies)
        {
            var intentMap = new Dictionary<string, IntentPolicyEntry>(StringComparer.Ordinal);
            if (intentPolicies != null)
            {
                foreach (var entry in intentPolicies)
                {
                    var key = entry.IntentId.Value ?? string.Empty;
                    if (intentMap.ContainsKey(key))
                    {
                        return InteractionResult<InteractionPolicySnapshot>.Fail(
                            InteractionValidationCode.InvalidPolicy,
                            $"Duplicate intent policy for '{key}'.",
                            key);
                    }

                    intentMap[key] = entry;
                }
            }

            var routeMap = new Dictionary<string, RoutePolicyEntry>(StringComparer.Ordinal);
            if (routePolicies != null)
            {
                foreach (var entry in routePolicies)
                {
                    var key = entry.RouteId.Value ?? string.Empty;
                    if (routeMap.ContainsKey(key))
                    {
                        return InteractionResult<InteractionPolicySnapshot>.Fail(
                            InteractionValidationCode.InvalidPolicy,
                            $"Duplicate route policy for '{key}'.",
                            key);
                    }

                    if (!InteractionVector2.IsFiniteNumber(entry.Sensitivity))
                    {
                        return InteractionResult<InteractionPolicySnapshot>.Fail(
                            InteractionValidationCode.NonFiniteValue,
                            $"Route policy sensitivity for '{key}' must be finite.",
                            key);
                    }

                    routeMap[key] = entry;
                }
            }

            return InteractionResult<InteractionPolicySnapshot>.Success(
                new InteractionPolicySnapshot(
                    InteractionReadOnly.FreezeDictionary(intentMap),
                    InteractionReadOnly.FreezeDictionary(routeMap)));
        }

        public static InteractionPolicySnapshot Empty { get; } =
            new InteractionPolicySnapshot(
                InteractionReadOnly.FreezeDictionary(new Dictionary<string, IntentPolicyEntry>()),
                InteractionReadOnly.FreezeDictionary(new Dictionary<string, RoutePolicyEntry>()));
    }

}
