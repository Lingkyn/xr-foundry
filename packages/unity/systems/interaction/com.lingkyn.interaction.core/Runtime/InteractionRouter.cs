using System;
using System.Collections.Generic;
using System.Linq;

namespace Lingkyn.Interaction.Core
{
    public sealed class InteractionRouter
    {
        public InteractionRoutingResult Route(InteractionRegistry registry, IEnumerable<ContextId> activeContexts,
            InteractionPolicySnapshot policy, InteractionFrame frame, InteractionRoutingState priorState,
            InteractionIntentHandler handler = null)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            policy ??= InteractionPolicySnapshot.Empty;
            priorState ??= InteractionRoutingState.Empty;
            var state = new InteractionRoutingStateBuilder(priorState);
            var active = new HashSet<ContextId>(activeContexts ?? Array.Empty<ContextId>());
            var dispatches = new List<InteractionDispatchResult>();
            var diagnostics = new List<InteractionDiagnostic>();
            var events = new List<SemanticInteractionEvent>();

            foreach (var observation in frame.Signals.GroupBy(x => x.ObservationSequence).OrderBy(x => x.Key))
            {
                var candidates = new List<Candidate>();
                foreach (var signal in observation.OrderBy(x => x.IngressSequence))
                {
                    var admitted = Admit(registry, active, policy, signal);
                    if (!admitted.Succeeded)
                    {
                        Reject(signal, admitted.Error, dispatches, diagnostics);
                        continue;
                    }
                    candidates.Add(admitted.Value);
                }
                if (candidates.Count == 0) continue;

                var highest = candidates.Max(x => x.Priority);
                var top = candidates.Where(x => x.Priority == highest)
                    .OrderBy(x => x.Route.RouteOrder).ThenBy(x => x.Route.Id).ToList();
                foreach (var lower in candidates.Where(x => x.Priority < highest).OrderBy(x => x.Signal.IngressSequence))
                {
                    diagnostics.Add(Diagnostic(InteractionDiagnosticKind.ShadowedRoute, InteractionValidationCode.ShadowedContext,
                        "Route is shadowed by a higher-priority active context.", lower));
                    dispatches.Add(Dispatch(InteractionDispatchStatus.Shadowed, lower, null, "Shadowed by higher-priority context."));
                }
                if (top.Count != 1)
                {
                    foreach (var item in top)
                    {
                        diagnostics.Add(Diagnostic(InteractionDiagnosticKind.AmbiguousRoute, InteractionValidationCode.AmbiguousContextCollision,
                            "Equal-priority routes claim one observation.", item));
                        dispatches.Add(Dispatch(InteractionDispatchStatus.Ambiguous, item, null, "Ambiguous equal-priority collision."));
                    }
                    continue;
                }
                Process(top[0], policy, state, handler, dispatches, diagnostics, events);
            }

            return new InteractionRoutingResult(InteractionReadOnly.FreezeList(dispatches),
                InteractionReadOnly.FreezeList(diagnostics), InteractionReadOnly.FreezeList(events),
                new ActiveContextSnapshot(InteractionReadOnly.FreezeList(active.OrderBy(x => x))), state.Build());
        }

        private static InteractionResult<Candidate> Admit(InteractionRegistry registry, HashSet<ContextId> active,
            InteractionPolicySnapshot policy, SourceSignal signal)
        {
            if (!registry.TryGetRoute(signal.RouteId, out var route))
                return InteractionResult<Candidate>.Fail(InteractionValidationCode.UnknownRoute, "Unknown route.", signal.RouteId.Value);
            if (!active.Contains(route.ContextId))
                return InteractionResult<Candidate>.Fail(InteractionValidationCode.InactiveContext, "Inactive context.", route.ContextId.Value);
            if (!registry.TryGetContext(route.ContextId, out var context) || !registry.TryGetIntent(route.IntentId, out var intent))
                return InteractionResult<Candidate>.Fail(InteractionValidationCode.InvalidDefinition, "Route references an unavailable context or intent.", route.Id.Value);
            if (policy.TryGetRoutePolicy(route.Id, out var rp) && !rp.Enabled || policy.TryGetIntentPolicy(intent.Id, out var ip) && !ip.Enabled)
                return InteractionResult<Candidate>.Fail(InteractionValidationCode.DisabledRoute, "Route is disabled by policy.", route.Id.Value);
            if (!route.SourceSelector.Equals(signal.SourceId))
                return InteractionResult<Candidate>.Fail(InteractionValidationCode.UnknownSource, "Observed source does not match the route selector.", route.Id.Value);
            if (route.SourceModality != signal.Modality)
                return InteractionResult<Candidate>.Fail(InteractionValidationCode.CapabilityMismatch, "Observed modality does not match the route modality.", route.Id.Value);
            var vv = InteractionValue.Validate(intent.ValueKind, signal.Value);
            if (!vv.Succeeded) return InteractionResult<Candidate>.Fail(vv.Error.Code, vv.Error.Message, route.Id.Value);
            if ((route.SourceCapabilities & intent.RequiredCapabilities) != intent.RequiredCapabilities
                || (signal.SourceCapabilities & intent.RequiredCapabilities) != intent.RequiredCapabilities)
                return InteractionResult<Candidate>.Fail(InteractionValidationCode.CapabilityMismatch, "Full intent capabilities are not satisfied.", route.Id.Value);
            return InteractionResult<Candidate>.Success(new Candidate(signal, route, intent, context.Priority));
        }

        private static void Process(Candidate item, InteractionPolicySnapshot policy, InteractionRoutingStateBuilder state,
            InteractionIntentHandler handler, List<InteractionDispatchResult> dispatches,
            List<InteractionDiagnostic> diagnostics, List<SemanticInteractionEvent> events)
        {
            var signal = item.Signal; var route = item.Route; var intent = item.Intent;
            var transformed = Transform(policy, route, signal.Value);
            if (!transformed.Succeeded) { Reject(signal, transformed.Error, dispatches, diagnostics, route.ContextId, route.IntentId); return; }
            var intentPolicy = policy.TryGetIntentPolicy(intent.Id, out var configured)
                ? configured : new IntentPolicyEntry(intent.Id, InteractionActivationMode.Momentary, true);

            if (signal.Phase == InteractionPhase.Started)
            {
                if (state.TryGet(route.ContextId, route.Id, signal.SourceId, out _))
                { Reject(signal, new InteractionError(InteractionValidationCode.DuplicatePhase, "Duplicate started phase."), dispatches, diagnostics, route.ContextId, route.IntentId); return; }
                state.Start(route.ContextId, route.Id, signal.SourceId, signal.TimestampTicks);
                Emit(item, transformed.Value, intentPolicy.ActivationMode, null, InteractionDispatchStatus.Routed,
                    "Started lifecycle event emitted.", handler: null, dispatches, diagnostics, events);
                return;
            }

            if (!state.TryGet(route.ContextId, route.Id, signal.SourceId, out var pending))
            { Reject(signal, new InteractionError(InteractionValidationCode.InvalidPhaseTransition, "Performed or canceled requires a preceding started phase."), dispatches, diagnostics, route.ContextId, route.IntentId); return; }
            if (signal.TimestampTicks < pending.StartedAtTicks)
            { Reject(signal, new InteractionError(InteractionValidationCode.InvalidFrame, "Timestamp regressed before the pending start."), dispatches, diagnostics, route.ContextId, route.IntentId); return; }
            state.Clear(route.ContextId, route.Id, signal.SourceId);

            if (signal.Phase == InteractionPhase.Canceled)
            {
                Emit(item, transformed.Value, intentPolicy.ActivationMode, null, InteractionDispatchStatus.Canceled,
                    "Canceled lifecycle event emitted.", handler: null, dispatches, diagnostics, events);
                return;
            }

            if (!IsActivated(transformed.Value, intentPolicy.ActivationThreshold)
                || intentPolicy.ActivationMode == InteractionActivationMode.Hold
                    && signal.TimestampTicks - pending.StartedAtTicks < intentPolicy.HoldDurationTicks)
            {
                diagnostics.Add(Diagnostic(InteractionDiagnosticKind.PolicyApplied, InteractionValidationCode.InvalidPolicy,
                    "Performed was suppressed by activation policy.", item));
                dispatches.Add(Dispatch(InteractionDispatchStatus.Rejected, item, null, "Suppressed by activation policy."));
                return;
            }

            var outputValue = transformed.Value;
            if (intentPolicy.ActivationMode == InteractionActivationMode.Toggle)
            {
                if (intent.ValueKind != InteractionValueKind.Button)
                { Reject(signal, new InteractionError(InteractionValidationCode.InvalidPolicy, "Toggle requires a button intent."), dispatches, diagnostics, route.ContextId, route.IntentId); return; }
                outputValue = InteractionValue.FromButton(state.Toggle(intent.Id));
            }
            Emit(item, outputValue, intentPolicy.ActivationMode, handler, InteractionDispatchStatus.Routed,
                "Performed lifecycle event emitted.", handler, dispatches, diagnostics, events);
        }

        private static void Emit(Candidate item, InteractionValue value, InteractionActivationMode mode,
            InteractionIntentHandler callback, InteractionDispatchStatus defaultStatus, string message,
            InteractionIntentHandler handler, List<InteractionDispatchResult> dispatches,
            List<InteractionDiagnostic> diagnostics, List<SemanticInteractionEvent> events)
        {
            var e = new SemanticInteractionEvent(item.Intent.Id, item.Route.ContextId, item.Route.Id, item.Signal.SourceId,
                item.Signal.Modality, value, item.Signal.Phase, mode, item.Signal.IngressSequence, item.Signal.TimestampTicks);
            events.Add(e); InteractionHandlerOutcome? outcome = null;
            if (callback != null)
            {
                try { outcome = callback(e); }
                catch { outcome = InteractionHandlerOutcome.Failed; }
                diagnostics.Add(Diagnostic(InteractionDiagnosticKind.HandlerResult,
                    outcome == InteractionHandlerOutcome.Failed ? InteractionValidationCode.HandlerFailed : InteractionValidationCode.None,
                    $"Handler returned '{outcome}'.", item));
            }
            dispatches.Add(Dispatch(outcome.HasValue ? InteractionDispatchStatus.HandlerOutcome : defaultStatus, item, outcome,
                outcome.HasValue ? $"Handler outcome '{outcome}'." : message));
        }

        private static bool IsActivated(InteractionValue value, double threshold)
        {
            if (value.Kind == InteractionValueKind.Button) return value.Button;
            if (value.Kind == InteractionValueKind.Scalar) return Math.Abs(value.Scalar) >= threshold;
            return true;
        }

        private static InteractionResult<InteractionValue> Transform(InteractionPolicySnapshot policy, InteractionRoute route, InteractionValue value)
        {
            if (!policy.TryGetRoutePolicy(route.Id, out var rp)) return InteractionResult<InteractionValue>.Success(value);
            InteractionValue output = value;
            if (value.Kind == InteractionValueKind.Scalar)
            { var x = value.Scalar * rp.Sensitivity * (rp.Invert ? -1 : 1); output = InteractionValue.FromScalar(x); }
            else if (value.Kind == InteractionValueKind.Vector2)
            { var m = rp.Sensitivity * (rp.Invert ? -1 : 1); output = InteractionValue.FromVector2(new InteractionVector2(value.Vector2.X * m, value.Vector2.Y * m)); }
            var valid = InteractionValue.Validate(value.Kind, output);
            return valid.Succeeded ? valid : InteractionResult<InteractionValue>.Fail(valid.Error.Code, "Route policy produced a non-finite value.", route.Id.Value);
        }

        private static void Reject(SourceSignal signal, InteractionError error, List<InteractionDispatchResult> dispatches,
            List<InteractionDiagnostic> diagnostics, ContextId contextId = default, IntentId intentId = default)
        {
            diagnostics.Add(new InteractionDiagnostic(InteractionDiagnosticKind.ValidationFailure, error.Code, error.Message,
                signal.RouteId, contextId, intentId, signal.IngressSequence));
            dispatches.Add(new InteractionDispatchResult(InteractionDispatchStatus.Rejected, signal.RouteId, contextId,
                intentId, signal.Phase, null, signal.IngressSequence, error.Message));
        }
        private static InteractionDiagnostic Diagnostic(InteractionDiagnosticKind kind, InteractionValidationCode code, string message, Candidate x) =>
            new InteractionDiagnostic(kind, code, message, x.Route.Id, x.Route.ContextId, x.Route.IntentId, x.Signal.IngressSequence);
        private static InteractionDispatchResult Dispatch(InteractionDispatchStatus status, Candidate x, InteractionHandlerOutcome? outcome, string message) =>
            new InteractionDispatchResult(status, x.Route.Id, x.Route.ContextId, x.Route.IntentId, x.Signal.Phase, outcome, x.Signal.IngressSequence, message);

        private readonly struct Candidate
        {
            public Candidate(SourceSignal signal, InteractionRoute route, IntentDefinition intent, int priority)
            { Signal = signal; Route = route; Intent = intent; Priority = priority; }
            public SourceSignal Signal { get; } public InteractionRoute Route { get; }
            public IntentDefinition Intent { get; } public int Priority { get; }
        }
    }
}
