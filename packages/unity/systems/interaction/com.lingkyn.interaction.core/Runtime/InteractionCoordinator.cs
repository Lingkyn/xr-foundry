using System;
using System.Collections.Generic;
using System.Linq;

namespace Lingkyn.Interaction.Core
{
    public sealed class InteractionCoordinator
    {
        private readonly InteractionRegistry _registry;
        private readonly InteractionRouter _router = new InteractionRouter();
        private IReadOnlyList<ContextId> _activeContexts;
        private InteractionPolicySnapshot _policy;
        private InteractionRoutingState _state;

        public InteractionCoordinator(InteractionRegistry registry, IEnumerable<ContextId> activeContexts = null,
            InteractionPolicySnapshot policy = null, InteractionRoutingState state = null)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _activeContexts = InteractionReadOnly.FreezeList((activeContexts ?? Array.Empty<ContextId>()).OrderBy(x => x));
            _policy = policy ?? InteractionPolicySnapshot.Empty;
            _state = state ?? InteractionRoutingState.Empty;
        }
        public InteractionRegistry Registry => _registry;
        public InteractionRoutingState State => _state;
        public IReadOnlyList<ContextId> ActiveContexts => _activeContexts;
        public InteractionPolicySnapshot Policy => _policy;
        public void SetActiveContexts(IEnumerable<ContextId> value)
        {
            var next = InteractionReadOnly.FreezeList((value ?? Array.Empty<ContextId>()).OrderBy(x => x));
            var previous = new HashSet<ContextId>(_activeContexts);
            var retained = new HashSet<ContextId>(next);
            var removed = new HashSet<ContextId>(_activeContexts.Where(contextId => !retained.Contains(contextId)));
            var added = new HashSet<ContextId>(next.Where(contextId => !previous.Contains(contextId)));
            if (removed.Count > 0 || added.Count > 0)
            {
                var affectedIntents = new HashSet<IntentId>(_registry.Routes
                    .Where(route => removed.Contains(route.ContextId))
                    .Select(route => route.IntentId));
                var resetToggles = new HashSet<IntentId>(affectedIntents
                    .Where(intentId => !HasActiveEnabledRoute(_policy, intentId, next)));
                ReconcileState(
                    // A newly active context can change arbitration for any in-flight observation.
                    phase => added.Count == 0 && !removed.Contains(phase.ContextId),
                    toggle => !resetToggles.Contains(toggle.IntentId));
            }
            _activeContexts = next;
        }

        public void SetPolicy(InteractionPolicySnapshot value)
        {
            var next = value ?? InteractionPolicySnapshot.Empty;
            var changedIntentPolicies = new HashSet<IntentId>();
            var changedRoutePolicies = new HashSet<RouteId>();
            var resetToggles = new HashSet<IntentId>();

            foreach (var intent in _registry.Intents)
            {
                if (!EffectiveIntentPolicy(_policy, intent.Id).Equals(EffectiveIntentPolicy(next, intent.Id)))
                {
                    changedIntentPolicies.Add(intent.Id);
                    resetToggles.Add(intent.Id);
                }
            }
            foreach (var route in _registry.Routes)
            {
                if (!EffectiveRoutePolicy(_policy, route.Id).Equals(EffectiveRoutePolicy(next, route.Id)))
                {
                    changedRoutePolicies.Add(route.Id);
                    if (!HasActiveEnabledRoute(next, route.IntentId, _activeContexts))
                        resetToggles.Add(route.IntentId);
                }
            }

            if (changedIntentPolicies.Count > 0 || changedRoutePolicies.Count > 0 || resetToggles.Count > 0)
            {
                ReconcileState(
                    phase => !changedRoutePolicies.Contains(phase.RouteId)
                        && (!_registry.TryGetRoute(phase.RouteId, out var route)
                            || !changedIntentPolicies.Contains(route.IntentId)),
                    toggle => !resetToggles.Contains(toggle.IntentId));
            }
            _policy = next;
        }

        public InteractionRoutingResult RouteFrame(InteractionFrame frame, InteractionIntentHandler handler = null)
        {
            var result = _router.Route(_registry, _activeContexts, _policy, frame, _state, handler);
            _state = result.NextState;
            return result;
        }

        public InteractionResult<int> ResetPendingPhases(
            SourceId sourceId,
            IEnumerable<RouteId> routeIds)
        {
            var source = SourceId.TryCreate(sourceId.Value);
            if (!source.Succeeded)
            {
                return InteractionResult<int>.Fail(
                    source.Error.Code,
                    source.Error.Message,
                    source.Error.Subject);
            }

            if (routeIds == null)
            {
                return InteractionResult<int>.Fail(
                    InteractionValidationCode.InvalidFrame,
                    "At least one route is required to reset pending phases.");
            }

            var routes = routeIds.ToArray();
            if (routes.Length == 0)
            {
                return InteractionResult<int>.Fail(
                    InteractionValidationCode.InvalidFrame,
                    "At least one route is required to reset pending phases.");
            }

            var routeSet = new HashSet<RouteId>();
            foreach (var routeId in routes)
            {
                if (!routeSet.Add(routeId))
                {
                    return InteractionResult<int>.Fail(
                        InteractionValidationCode.DuplicateIdentity,
                        "Pending-phase reset routes must be unique.",
                        routeId.Value);
                }

                if (!_registry.TryGetRoute(routeId, out var route))
                {
                    return InteractionResult<int>.Fail(
                        InteractionValidationCode.UnknownRoute,
                        "Pending-phase reset references an unknown route.",
                        routeId.Value);
                }

                if (!route.SourceSelector.Equals(source.Value))
                {
                    return InteractionResult<int>.Fail(
                        InteractionValidationCode.UnknownSource,
                        "Pending-phase reset source does not match the route selector.",
                        routeId.Value);
                }
            }

            var retained = _state.PendingPhases.Where(phase =>
                !phase.SourceId.Equals(source.Value)
                || !routeSet.Contains(phase.RouteId)).ToArray();
            var cleared = _state.PendingPhases.Count - retained.Length;
            if (cleared > 0)
                _state = new InteractionRoutingState(retained, _state.ToggleStates);
            return InteractionResult<int>.Success(cleared);
        }

        private void ReconcileState(
            Func<InteractionRoutePhaseState, bool> retainPhase,
            Func<InteractionToggleState, bool> retainToggle)
        {
            var phases = _state.PendingPhases.Where(retainPhase).ToArray();
            var toggles = _state.ToggleStates.Where(retainToggle).ToArray();
            if (phases.Length != _state.PendingPhases.Count || toggles.Length != _state.ToggleStates.Count)
                _state = new InteractionRoutingState(phases, toggles);
        }

        private static IntentPolicyEntry EffectiveIntentPolicy(InteractionPolicySnapshot policy, IntentId intentId) =>
            policy.TryGetIntentPolicy(intentId, out var configured)
                ? configured
                : new IntentPolicyEntry(intentId, InteractionActivationMode.Momentary, true);

        private static RoutePolicyEntry EffectiveRoutePolicy(InteractionPolicySnapshot policy, RouteId routeId) =>
            policy.TryGetRoutePolicy(routeId, out var configured)
                ? configured
                : new RoutePolicyEntry(routeId, true, 1.0, false);

        private bool HasActiveEnabledRoute(
            InteractionPolicySnapshot policy,
            IntentId intentId,
            IEnumerable<ContextId> activeContexts)
        {
            if (!EffectiveIntentPolicy(policy, intentId).Enabled)
                return false;
            var active = new HashSet<ContextId>(activeContexts ?? Array.Empty<ContextId>());
            return _registry.Routes.Any(route => route.IntentId.Equals(intentId)
                && active.Contains(route.ContextId)
                && EffectiveRoutePolicy(policy, route.Id).Enabled);
        }
    }
}
