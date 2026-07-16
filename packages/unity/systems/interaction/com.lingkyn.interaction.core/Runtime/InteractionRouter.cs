using System;
using System.Collections.Generic;
using System.Linq;

namespace Lingkyn.Interaction.Core
{
    public sealed class InteractionRouter
    {
        public InteractionRoutingResult Route(
            InteractionRegistry registry,
            IEnumerable<ContextId> activeContexts,
            InteractionPolicySnapshot policy,
            InteractionFrame frame,
            InteractionRoutingSession session,
            InteractionIntentHandler handler)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            if (frame == null)
            {
                throw new ArgumentNullException(nameof(frame));
            }

            session ??= new InteractionRoutingSession();
            policy ??= InteractionPolicySnapshot.Empty;
            var activeSet = BuildActiveContextSet(activeContexts);
            var activeSnapshot = new ActiveContextSnapshot(
                InteractionReadOnly.FreezeList(activeSet.OrderBy(id => id, Comparer<ContextId>.Default)));

            var dispatches = new List<InteractionDispatchResult>();
            var diagnostics = new List<InteractionDiagnostic>();
            var events = new List<SemanticInteractionEvent>();
            var resolutionByRouteId = new Dictionary<string, RouteResolution>(StringComparer.Ordinal);

            foreach (var signal in frame.Signals)
            {
                if (!resolutionByRouteId.TryGetValue(signal.RouteId.Value ?? string.Empty, out var resolution))
                {
                    resolution = ResolveRouteAdmission(registry, activeSet, signal.RouteId);
                    resolutionByRouteId[signal.RouteId.Value ?? string.Empty] = resolution;
                }

                foreach (var shadowed in resolution.ShadowedRoutes)
                {
                    diagnostics.Add(new InteractionDiagnostic(
                        InteractionDiagnosticKind.ShadowedRoute,
                        InteractionValidationCode.ShadowedContext,
                        $"Route '{shadowed.Id.Value}' is shadowed by a higher-priority active context.",
                        shadowed.Id,
                        shadowed.ContextId,
                        shadowed.IntentId,
                        signal.IngressSequence));
                    dispatches.Add(new InteractionDispatchResult(
                        InteractionDispatchStatus.Shadowed,
                        shadowed.Id,
                        shadowed.ContextId,
                        shadowed.IntentId,
                        signal.Phase,
                        null,
                        signal.IngressSequence,
                        "Shadowed by higher-priority context."));
                }

                if (resolution.Ambiguous)
                {
                    diagnostics.Add(new InteractionDiagnostic(
                        InteractionDiagnosticKind.AmbiguousRoute,
                        InteractionValidationCode.AmbiguousContextCollision,
                        "Equal-priority contexts claim the same source route.",
                        signal.RouteId,
                        default,
                        default,
                        signal.IngressSequence));
                    dispatches.Add(new InteractionDispatchResult(
                        InteractionDispatchStatus.Ambiguous,
                        signal.RouteId,
                        default,
                        default,
                        signal.Phase,
                        null,
                        signal.IngressSequence,
                        "Ambiguous equal-priority collision."));
                    continue;
                }

                if (!resolution.HasWinner)
                {
                    var status = resolution.IsInactive
                        ? InteractionValidationCode.InactiveContext
                        : InteractionValidationCode.UnknownRoute;
                    var message = resolution.IsInactive
                        ? "Inactive context."
                        : resolution.Error.Message;
                    diagnostics.Add(new InteractionDiagnostic(
                        resolution.IsInactive
                            ? InteractionDiagnosticKind.InactiveContext
                            : InteractionDiagnosticKind.ValidationFailure,
                        status,
                        message,
                        signal.RouteId,
                        resolution.Route?.ContextId ?? default,
                        resolution.Route?.IntentId ?? default,
                        signal.IngressSequence));
                    dispatches.Add(new InteractionDispatchResult(
                        InteractionDispatchStatus.Rejected,
                        signal.RouteId,
                        resolution.Route?.ContextId ?? default,
                        resolution.Route?.IntentId ?? default,
                        signal.Phase,
                        null,
                        signal.IngressSequence,
                        message));
                    continue;
                }

                var validation = ValidateSignal(registry, resolution.Route, signal);
                if (!validation.Succeeded)
                {
                    diagnostics.Add(CreateDiagnostic(
                        InteractionDiagnosticKind.ValidationFailure,
                        validation.Error,
                        signal,
                        resolution.Route.ContextId,
                        resolution.Route.IntentId));
                    dispatches.Add(new InteractionDispatchResult(
                        InteractionDispatchStatus.Rejected,
                        resolution.Route.Id,
                        resolution.Route.ContextId,
                        resolution.Route.IntentId,
                        signal.Phase,
                        null,
                        signal.IngressSequence,
                        validation.Error.Message));
                    continue;
                }

                if (IsRouteDisabled(policy, resolution.Route))
                {
                    diagnostics.Add(new InteractionDiagnostic(
                        InteractionDiagnosticKind.DisabledRoute,
                        InteractionValidationCode.DisabledRoute,
                        $"Route '{resolution.Route.Id.Value}' is disabled by policy.",
                        resolution.Route.Id,
                        resolution.Route.ContextId,
                        resolution.Route.IntentId,
                        signal.IngressSequence));
                    dispatches.Add(new InteractionDispatchResult(
                        InteractionDispatchStatus.Rejected,
                        resolution.Route.Id,
                        resolution.Route.ContextId,
                        resolution.Route.IntentId,
                        signal.Phase,
                        null,
                        signal.IngressSequence,
                        "Disabled route."));
                    continue;
                }

                ProcessSignal(
                    policy,
                    session,
                    validation.Value,
                    resolution.Route,
                    signal,
                    handler,
                    dispatches,
                    diagnostics,
                    events);
            }

            return new InteractionRoutingResult(
                InteractionReadOnly.FreezeList(dispatches),
                InteractionReadOnly.FreezeList(diagnostics),
                InteractionReadOnly.FreezeList(events),
                activeSnapshot);
        }

        private static RouteResolution ResolveRouteAdmission(
            InteractionRegistry registry,
            HashSet<ContextId> activeSet,
            RouteId routeId)
        {
            if (!registry.TryGetRoutes(routeId, out var routes) || routes.Count == 0)
            {
                return RouteResolution.Fail(
                    InteractionResult<(InteractionRoute, IntentDefinition)>.Fail(
                        InteractionValidationCode.UnknownRoute,
                        $"Unknown route '{routeId.Value}'.",
                        routeId.Value));
            }

            var activeRoutes = routes.Where(route => activeSet.Contains(route.ContextId)).ToList();
            if (activeRoutes.Count == 0)
            {
                return RouteResolution.Inactive(routes[0]);
            }

            if (activeRoutes.Count == 1)
            {
                return RouteResolution.Win(activeRoutes[0], Array.Empty<InteractionRoute>());
            }

            var prioritized = activeRoutes
                .Select(route =>
                {
                    registry.TryGetContext(route.ContextId, out var context);
                    return new PrioritizedRoute(route, context?.Priority ?? int.MinValue);
                })
                .ToList();

            var highest = prioritized.Max(entry => entry.Priority);
            var winners = prioritized.Where(entry => entry.Priority == highest).ToList();
            if (winners.Select(entry => entry.Route.ContextId.Value).Distinct(StringComparer.Ordinal).Count() > 1)
            {
                return RouteResolution.AmbiguousRoute();
            }

            var winner = winners
                .OrderBy(entry => entry.Route.RouteOrder)
                .ThenBy(entry => entry.Route.ContextId, Comparer<ContextId>.Default)
                .First()
                .Route;

            var shadowed = activeRoutes
                .Where(route => !route.ContextId.Equals(winner.ContextId))
                .OrderBy(route => route.ContextId, Comparer<ContextId>.Default)
                .ToList();

            return RouteResolution.Win(winner, shadowed);
        }

        private void ProcessSignal(
            InteractionPolicySnapshot policy,
            InteractionRoutingSession session,
            IntentDefinition intent,
            InteractionRoute route,
            SourceSignal signal,
            InteractionIntentHandler handler,
            List<InteractionDispatchResult> dispatches,
            List<InteractionDiagnostic> diagnostics,
            List<SemanticInteractionEvent> events)
        {
            var routeInstanceKey = InteractionRoutingSession.BuildRouteInstanceKey(route.ContextId, route.Id);
            var activationMode = ResolveActivationMode(policy, intent.Id);
            var holdEligible = activationMode == InteractionActivationMode.Hold
                && session.TryGetRoutePhase(routeInstanceKey, out var currentPhase)
                && currentPhase == InteractionPhase.Started;

            if (!session.TryAdvancePhase(routeInstanceKey, signal.Phase, out var phaseError))
            {
                diagnostics.Add(new InteractionDiagnostic(
                    InteractionDiagnosticKind.ValidationFailure,
                    phaseError.Code,
                    phaseError.Message,
                    route.Id,
                    route.ContextId,
                    route.IntentId,
                    signal.IngressSequence));
                dispatches.Add(new InteractionDispatchResult(
                    InteractionDispatchStatus.Rejected,
                    route.Id,
                    route.ContextId,
                    route.IntentId,
                    signal.Phase,
                    null,
                    signal.IngressSequence,
                    phaseError.Message));
                return;
            }

            var semanticEvent = new SemanticInteractionEvent(
                intent.Id,
                route.ContextId,
                route.Id,
                signal.SourceId,
                signal.Modality,
                ApplyRoutePolicy(policy, route, signal.Value),
                signal.Phase,
                activationMode,
                signal.IngressSequence,
                signal.TimestampTicks);

            switch (signal.Phase)
            {
                case InteractionPhase.Started:
                    events.Add(semanticEvent);
                    dispatches.Add(new InteractionDispatchResult(
                        InteractionDispatchStatus.Routed,
                        route.Id,
                        route.ContextId,
                        route.IntentId,
                        signal.Phase,
                        null,
                        signal.IngressSequence,
                        "Started lifecycle event emitted."));
                    break;

                case InteractionPhase.Canceled:
                    session.SetToggleLatched(intent.Id, false);
                    events.Add(semanticEvent);
                    dispatches.Add(new InteractionDispatchResult(
                        InteractionDispatchStatus.Canceled,
                        route.Id,
                        route.ContextId,
                        route.IntentId,
                        signal.Phase,
                        null,
                        signal.IngressSequence,
                        "Canceled lifecycle event emitted."));
                    break;

                case InteractionPhase.Performed:
                    if (!ShouldDispatchPerformed(session, activationMode, intent.Id, signal, holdEligible))
                    {
                        diagnostics.Add(new InteractionDiagnostic(
                            InteractionDiagnosticKind.PolicyApplied,
                            InteractionValidationCode.None,
                            $"Performed suppressed by activation policy '{activationMode}'.",
                            route.Id,
                            route.ContextId,
                            route.IntentId,
                            signal.IngressSequence));
                        dispatches.Add(new InteractionDispatchResult(
                            InteractionDispatchStatus.Rejected,
                            route.Id,
                            route.ContextId,
                            route.IntentId,
                            signal.Phase,
                            null,
                            signal.IngressSequence,
                            "Performed suppressed by activation policy."));
                        return;
                    }

                    if (policy.TryGetRoutePolicy(route.Id, out var appliedRoutePolicy)
                        && (appliedRoutePolicy.Sensitivity != 1d || appliedRoutePolicy.Invert))
                    {
                        diagnostics.Add(new InteractionDiagnostic(
                            InteractionDiagnosticKind.PolicyApplied,
                            InteractionValidationCode.None,
                            "Route policy transformed performed value.",
                            route.Id,
                            route.ContextId,
                            route.IntentId,
                            signal.IngressSequence));
                    }

                    events.Add(semanticEvent);
                    InteractionHandlerOutcome? handlerOutcome = null;
                    if (handler != null)
                    {
                        handlerOutcome = handler(semanticEvent);
                        diagnostics.Add(new InteractionDiagnostic(
                            InteractionDiagnosticKind.HandlerResult,
                            InteractionValidationCode.None,
                            $"Handler returned '{handlerOutcome}'.",
                            route.Id,
                            route.ContextId,
                            route.IntentId,
                            signal.IngressSequence));
                    }

                    dispatches.Add(new InteractionDispatchResult(
                        handlerOutcome.HasValue
                            ? InteractionDispatchStatus.HandlerOutcome
                            : InteractionDispatchStatus.Routed,
                        route.Id,
                        route.ContextId,
                        route.IntentId,
                        signal.Phase,
                        handlerOutcome,
                        signal.IngressSequence,
                        handlerOutcome.HasValue
                            ? $"Handler outcome '{handlerOutcome}'."
                            : "Performed lifecycle event emitted."));
                    break;
            }
        }

        private static bool ShouldDispatchPerformed(
            InteractionRoutingSession session,
            InteractionActivationMode activationMode,
            IntentId intentId,
            SourceSignal signal,
            bool holdEligible)
        {
            switch (activationMode)
            {
                case InteractionActivationMode.Momentary:
                    return signal.Value.Kind != InteractionValueKind.Button || signal.Value.Button;
                case InteractionActivationMode.Toggle:
                    if (session.IsToggleLatched(intentId))
                    {
                        session.SetToggleLatched(intentId, false);
                        return true;
                    }

                    if (signal.Value.Kind == InteractionValueKind.Button && signal.Value.Button)
                    {
                        session.SetToggleLatched(intentId, true);
                    }

                    return false;
                case InteractionActivationMode.Hold:
                    return holdEligible;
                default:
                    return true;
            }
        }

        private static InteractionActivationMode ResolveActivationMode(
            InteractionPolicySnapshot policy,
            IntentId intentId)
        {
            return policy.TryGetIntentPolicy(intentId, out var entry)
                ? entry.ActivationMode
                : InteractionActivationMode.Momentary;
        }

        private static bool IsRouteDisabled(InteractionPolicySnapshot policy, InteractionRoute route)
        {
            if (policy.TryGetRoutePolicy(route.Id, out var routePolicy) && !routePolicy.Enabled)
            {
                return true;
            }

            if (policy.TryGetIntentPolicy(route.IntentId, out var intentPolicy) && !intentPolicy.Enabled)
            {
                return true;
            }

            return false;
        }

        private static InteractionValue ApplyRoutePolicy(
            InteractionPolicySnapshot policy,
            InteractionRoute route,
            InteractionValue value)
        {
            if (!policy.TryGetRoutePolicy(route.Id, out var routePolicy))
            {
                return value;
            }

            switch (value.Kind)
            {
                case InteractionValueKind.Scalar:
                    var scalar = value.Scalar * routePolicy.Sensitivity;
                    return routePolicy.Invert
                        ? InteractionValue.FromScalar(-scalar)
                        : InteractionValue.FromScalar(scalar);
                case InteractionValueKind.Vector2:
                    var vector2 = value.Vector2;
                    var scaled2 = new InteractionVector2(
                        vector2.X * routePolicy.Sensitivity,
                        vector2.Y * routePolicy.Sensitivity);
                    return routePolicy.Invert
                        ? InteractionValue.FromVector2(new InteractionVector2(-scaled2.X, -scaled2.Y))
                        : InteractionValue.FromVector2(scaled2);
                default:
                    return value;
            }
        }

        private static InteractionResult<IntentDefinition> ValidateSignal(
            InteractionRegistry registry,
            InteractionRoute route,
            SourceSignal signal)
        {
            if (!registry.TryGetIntent(route.IntentId, out var intent))
            {
                return InteractionResult<IntentDefinition>.Fail(
                    InteractionValidationCode.UnknownIntent,
                    $"Unknown intent '{route.IntentId.Value}'.",
                    route.IntentId.Value);
            }

            var valueValidation = InteractionValue.Validate(intent.ValueKind, signal.Value);
            if (!valueValidation.Succeeded)
            {
                return InteractionResult<IntentDefinition>.Fail(
                    valueValidation.Error.Code,
                    valueValidation.Error.Message,
                    route.Id.Value);
            }

            var requiredSourceCaps = InteractionValue.RequiredCapabilitiesForKind(intent.ValueKind);
            if ((signal.SourceCapabilities & requiredSourceCaps) != requiredSourceCaps)
            {
                return InteractionResult<IntentDefinition>.Fail(
                    InteractionValidationCode.CapabilityMismatch,
                    $"Source capabilities do not satisfy intent '{intent.Id.Value}'.",
                    route.Id.Value);
            }

            if ((route.SourceCapabilities & requiredSourceCaps) != requiredSourceCaps)
            {
                return InteractionResult<IntentDefinition>.Fail(
                    InteractionValidationCode.CapabilityMismatch,
                    $"Route capabilities do not satisfy intent '{intent.Id.Value}'.",
                    route.Id.Value);
            }

            // RouteId admits the route; SourceId is observed source evidence on the signal
            // and must not be rewritten from route authoring metadata (architecture identity separation).

            if (intent.ValueKind == InteractionValueKind.Button
                && signal.Modality == InteractionModality.Gaze
                && (signal.SourceCapabilities & InteractionCapability.Digital) == 0)
            {
                return InteractionResult<IntentDefinition>.Fail(
                    InteractionValidationCode.CapabilityMismatch,
                    "Gaze direction alone cannot produce a digital performed outcome.",
                    route.Id.Value);
            }

            return InteractionResult<IntentDefinition>.Success(intent);
        }

        private static HashSet<ContextId> BuildActiveContextSet(IEnumerable<ContextId> activeContexts)
        {
            var set = new HashSet<ContextId>();
            if (activeContexts == null)
            {
                return set;
            }

            foreach (var contextId in activeContexts)
            {
                set.Add(contextId);
            }

            return set;
        }

        private static InteractionDiagnostic CreateDiagnostic(
            InteractionDiagnosticKind kind,
            InteractionError error,
            SourceSignal signal,
            ContextId contextId = default,
            IntentId intentId = default)
        {
            return new InteractionDiagnostic(
                kind,
                error.Code,
                error.Message,
                signal.RouteId,
                contextId,
                intentId,
                signal.IngressSequence);
        }

        private readonly struct PrioritizedRoute
        {
            public PrioritizedRoute(InteractionRoute route, int priority)
            {
                Route = route;
                Priority = priority;
            }

            public InteractionRoute Route { get; }
            public int Priority { get; }
        }

        private readonly struct RouteResolution
        {
            private RouteResolution(
                InteractionRoute route,
                IReadOnlyList<InteractionRoute> shadowedRoutes,
                bool hasWinner,
                bool ambiguous,
                bool inactive,
                InteractionError error)
            {
                Route = route;
                ShadowedRoutes = shadowedRoutes ?? Array.Empty<InteractionRoute>();
                HasWinner = hasWinner;
                Ambiguous = ambiguous;
                IsInactive = inactive;
                Error = error;
            }

            public InteractionRoute Route { get; }
            public IReadOnlyList<InteractionRoute> ShadowedRoutes { get; }
            public bool HasWinner { get; }
            public bool Ambiguous { get; }
            public bool IsInactive { get; }
            public InteractionError Error { get; }

            public static RouteResolution Win(InteractionRoute route, IReadOnlyList<InteractionRoute> shadowedRoutes) =>
                new RouteResolution(route, shadowedRoutes, true, false, false, default);

            public static RouteResolution Inactive(InteractionRoute route) =>
                new RouteResolution(route, Array.Empty<InteractionRoute>(), false, false, true, default);

            public static RouteResolution AmbiguousRoute() =>
                new RouteResolution(default, Array.Empty<InteractionRoute>(), false, true, false, default);

            public static RouteResolution Fail(InteractionResult<(InteractionRoute, IntentDefinition)> error) =>
                new RouteResolution(default, Array.Empty<InteractionRoute>(), false, false, false, error.Error);
        }
    }
}
