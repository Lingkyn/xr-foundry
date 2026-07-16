using System;
using System.Collections.Generic;
using System.Linq;

namespace Lingkyn.Interaction.Core
{
    public readonly struct IntentPolicyEntry : IEquatable<IntentPolicyEntry>
    {
        public IntentPolicyEntry(IntentId intentId, InteractionActivationMode activationMode, bool enabled,
            long holdDurationTicks = 0, double activationThreshold = 0.5)
        { IntentId = intentId; ActivationMode = activationMode; Enabled = enabled; HoldDurationTicks = holdDurationTicks; ActivationThreshold = activationThreshold; }
        public IntentId IntentId { get; }
        public InteractionActivationMode ActivationMode { get; }
        public bool Enabled { get; }
        public long HoldDurationTicks { get; }
        public double ActivationThreshold { get; }
        public bool Equals(IntentPolicyEntry other) => IntentId.Equals(other.IntentId) && ActivationMode == other.ActivationMode
            && Enabled == other.Enabled && HoldDurationTicks == other.HoldDurationTicks && ActivationThreshold.Equals(other.ActivationThreshold);
        public override bool Equals(object obj) => obj is IntentPolicyEntry other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(IntentId, ActivationMode, Enabled, HoldDurationTicks, ActivationThreshold);
    }

    public readonly struct RoutePolicyEntry : IEquatable<RoutePolicyEntry>
    {
        public RoutePolicyEntry(RouteId routeId, bool enabled, double sensitivity, bool invert)
        { RouteId = routeId; Enabled = enabled; Sensitivity = sensitivity; Invert = invert; }
        public RouteId RouteId { get; }
        public bool Enabled { get; }
        public double Sensitivity { get; }
        public bool Invert { get; }
        public bool Equals(RoutePolicyEntry other) => RouteId.Equals(other.RouteId) && Enabled == other.Enabled
            && Sensitivity.Equals(other.Sensitivity) && Invert == other.Invert;
        public override bool Equals(object obj) => obj is RoutePolicyEntry other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(RouteId, Enabled, Sensitivity, Invert);
    }

    public sealed class InteractionPolicySnapshot
    {
        private readonly IReadOnlyDictionary<string, IntentPolicyEntry> _intents;
        private readonly IReadOnlyDictionary<string, RoutePolicyEntry> _routes;
        private InteractionPolicySnapshot(IDictionary<string, IntentPolicyEntry> intents, IDictionary<string, RoutePolicyEntry> routes)
        { _intents = InteractionReadOnly.FreezeDictionary(intents); _routes = InteractionReadOnly.FreezeDictionary(routes); }
        public IReadOnlyList<IntentPolicyEntry> IntentPolicies => InteractionReadOnly.FreezeList(_intents.Values.OrderBy(x => x.IntentId));
        public IReadOnlyList<RoutePolicyEntry> RoutePolicies => InteractionReadOnly.FreezeList(_routes.Values.OrderBy(x => x.RouteId));
        public bool TryGetIntentPolicy(IntentId id, out IntentPolicyEntry entry) => _intents.TryGetValue(id.Value ?? string.Empty, out entry);
        public bool TryGetRoutePolicy(RouteId id, out RoutePolicyEntry entry) => _routes.TryGetValue(id.Value ?? string.Empty, out entry);

        public static InteractionResult<InteractionPolicySnapshot> Create(IEnumerable<IntentPolicyEntry> intents, IEnumerable<RoutePolicyEntry> routes)
        {
            var im = new Dictionary<string, IntentPolicyEntry>(StringComparer.Ordinal);
            foreach (var e in intents ?? Array.Empty<IntentPolicyEntry>())
            {
                var key = e.IntentId.Value ?? string.Empty;
                if (key.Length == 0 || !Enum.IsDefined(typeof(InteractionActivationMode), e.ActivationMode)
                    || e.HoldDurationTicks < 0 || !InteractionVector2.IsFiniteNumber(e.ActivationThreshold)
                    || e.ActivationThreshold < 0 || e.ActivationThreshold > 1)
                    return InteractionResult<InteractionPolicySnapshot>.Fail(InteractionValidationCode.InvalidPolicy, "Invalid intent policy.", key);
                if (im.ContainsKey(key)) return InteractionResult<InteractionPolicySnapshot>.Fail(InteractionValidationCode.InvalidPolicy, "Duplicate intent policy.", key);
                im[key] = e;
            }
            var rm = new Dictionary<string, RoutePolicyEntry>(StringComparer.Ordinal);
            foreach (var e in routes ?? Array.Empty<RoutePolicyEntry>())
            {
                var key = e.RouteId.Value ?? string.Empty;
                if (key.Length == 0 || !InteractionVector2.IsFiniteNumber(e.Sensitivity) || e.Sensitivity < 0)
                    return InteractionResult<InteractionPolicySnapshot>.Fail(InteractionValidationCode.InvalidPolicy, "Invalid route policy.", key);
                if (rm.ContainsKey(key)) return InteractionResult<InteractionPolicySnapshot>.Fail(InteractionValidationCode.InvalidPolicy, "Duplicate route policy.", key);
                rm[key] = e;
            }
            return InteractionResult<InteractionPolicySnapshot>.Success(new InteractionPolicySnapshot(im, rm));
        }
        public static InteractionPolicySnapshot Empty { get; } = new InteractionPolicySnapshot(
            new Dictionary<string, IntentPolicyEntry>(), new Dictionary<string, RoutePolicyEntry>());
    }
}
