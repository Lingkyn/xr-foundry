using System;
using System.Collections.Generic;

namespace Lingkyn.Interaction.Core
{
    public sealed class InteractionRoutingSession
    {
        private readonly Dictionary<string, InteractionPhase> _routePhaseState =
            new Dictionary<string, InteractionPhase>(StringComparer.Ordinal);

        private readonly HashSet<string> _toggleLatchedIntents =
            new HashSet<string>(StringComparer.Ordinal);

        internal bool TryGetRoutePhase(string routeInstanceKey, out InteractionPhase phase) =>
            _routePhaseState.TryGetValue(routeInstanceKey, out phase);

        internal bool IsToggleLatched(IntentId intentId) =>
            _toggleLatchedIntents.Contains(intentId.Value ?? string.Empty);

        internal void SetToggleLatched(IntentId intentId, bool latched)
        {
            var key = intentId.Value ?? string.Empty;
            if (latched)
            {
                _toggleLatchedIntents.Add(key);
            }
            else
            {
                _toggleLatchedIntents.Remove(key);
            }
        }

        internal bool TryAdvancePhase(string routeInstanceKey, InteractionPhase nextPhase, out InteractionError error)
        {
            error = default;
            if (!_routePhaseState.TryGetValue(routeInstanceKey, out var currentPhase))
            {
                if (nextPhase == InteractionPhase.Started)
                {
                    _routePhaseState[routeInstanceKey] = nextPhase;
                    return true;
                }

                error = new InteractionError(
                    InteractionValidationCode.InvalidPhaseTransition,
                    "Performed or canceled requires a preceding started route phase.");
                return false;
            }

            if (currentPhase == nextPhase)
            {
                error = new InteractionError(
                    InteractionValidationCode.DuplicatePhase,
                    "Duplicate phase transition is not allowed.");
                return false;
            }

            switch (currentPhase)
            {
                case InteractionPhase.Started:
                    if (nextPhase == InteractionPhase.Performed || nextPhase == InteractionPhase.Canceled)
                    {
                        _routePhaseState.Remove(routeInstanceKey);
                        return true;
                    }

                    break;
                case InteractionPhase.Performed:
                case InteractionPhase.Canceled:
                    if (nextPhase == InteractionPhase.Started)
                    {
                        _routePhaseState[routeInstanceKey] = nextPhase;
                        return true;
                    }

                    break;
            }

            error = new InteractionError(
                InteractionValidationCode.InvalidPhaseTransition,
                $"Cannot transition from '{currentPhase}' to '{nextPhase}'.");
            return false;
        }

        internal static string BuildRouteInstanceKey(ContextId contextId, RouteId routeId) =>
            $"{contextId.Value ?? string.Empty}\0{routeId.Value ?? string.Empty}";
    }
}
