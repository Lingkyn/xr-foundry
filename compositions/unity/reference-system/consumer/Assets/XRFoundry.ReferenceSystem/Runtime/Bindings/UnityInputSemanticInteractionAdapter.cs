using System;
using System.Collections.Generic;
using System.Linq;
using Lingkyn.Interaction.Core;
using Lingkyn.Interaction.Unity;
using UnityEngine.InputSystem;

namespace XRFoundry.ReferenceSystem.Bindings
{
    /// <summary>
    /// Consumer-owned composition seam from an explicit Unity Input System
    /// observation to the semantic interaction coordinator.
    /// </summary>
    /// <remarks>
    /// The adapter owns neither subscriptions nor lifecycle state. Live Button
    /// callbacks are normalized against pending phases already owned by the
    /// <see cref="InteractionCoordinator"/>. Callers supply callback timing and
    /// sequence facts and explicitly reset the scoped pending state when ending
    /// a subscription.
    /// </remarks>
    public sealed class UnityInputSemanticInteractionAdapter
    {
        private readonly InteractionCoordinator _coordinator;
        private readonly InputRouteBinding[] _orderedCandidates;
        private readonly IReadOnlyList<InputRouteBinding> _readOnlyCandidates;
        private readonly HashSet<RouteId> _candidateRouteIds;
        private readonly Func<InputControl, bool> _callbackControlAdmission;
        private readonly InputAction _action;
        private readonly SourceId _observedSource;
        private readonly string _observedSourceId;
        private readonly InteractionModality _observedModality;
        private readonly InteractionCapability _observedCapabilities;

        public UnityInputSemanticInteractionAdapter(
            InteractionCoordinator coordinator,
            IEnumerable<InputRouteBinding> orderedCandidates,
            Func<InputControl, bool> callbackControlAdmission,
            string observedSourceId,
            InteractionModality observedModality,
            InteractionCapability observedCapabilities)
        {
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _callbackControlAdmission = callbackControlAdmission
                ?? throw new ArgumentNullException(nameof(callbackControlAdmission));
            _orderedCandidates = FreezeAndValidate(
                coordinator,
                orderedCandidates,
                observedSourceId,
                observedModality,
                observedCapabilities);
            _readOnlyCandidates = Array.AsReadOnly(_orderedCandidates);
            _candidateRouteIds = new HashSet<RouteId>(
                _orderedCandidates.Select(candidate => candidate.RouteId));
            _action = _orderedCandidates[0].ActionReference.action;
            _observedSource = SourceId.TryCreate(observedSourceId).Value;
            _observedSourceId = observedSourceId;
            _observedModality = observedModality;
            _observedCapabilities = observedCapabilities;
        }

        public InteractionCoordinator Coordinator => _coordinator;
        public IReadOnlyList<InputRouteBinding> OrderedCandidates => _readOnlyCandidates;
        public InputAction Action => _action;
        public string ObservedSourceId => _observedSourceId;
        public InteractionModality ObservedModality => _observedModality;
        public InteractionCapability ObservedCapabilities => _observedCapabilities;

        public InteractionResult<int> ResetLiveCallbackLifecycle() =>
            _coordinator.ResetPendingPhases(
                _observedSource,
                _orderedCandidates.Select(candidate => candidate.RouteId));

        /// <summary>
        /// Converts a live Input System callback into a semantic frame, then routes
        /// that frame through the injected coordinator.
        /// </summary>
        public InteractionResult<InteractionRoutingResult> RouteCallback(
            InputAction.CallbackContext callback,
            long timestampTicks,
            InputObservationStamp stamp,
            InteractionIntentHandler handler = null)
        {
            if (!ReferenceEquals(callback.action, _action))
            {
                return InteractionResult<InteractionRoutingResult>.Fail(
                    InteractionValidationCode.UnknownRoute,
                    "Callback InputAction instance does not match the frozen route action.",
                    _orderedCandidates[0].RouteId.Value);
            }

            var captured = InputSystemSignalAdapter.CaptureCallback(
                callback,
                _orderedCandidates,
                _callbackControlAdmission,
                _observedSourceId,
                _observedModality,
                _observedCapabilities,
                timestampTicks,
                stamp);
            if (!captured.Succeeded)
            {
                return InteractionResult<InteractionRoutingResult>.Fail(
                    captured.Error.Code,
                    captured.Error.Message,
                    captured.Error.Subject);
            }

            switch (callback.phase)
            {
                case InputActionPhase.Started:
                    return RouteLiveStarted(captured.Value);
                case InputActionPhase.Performed:
                    return RouteLivePerformed(captured.Value, handler);
                case InputActionPhase.Canceled:
                    return RouteLiveCanceled(captured.Value);
                default:
                    return InteractionResult<InteractionRoutingResult>.Fail(
                        InteractionValidationCode.InvalidPhaseTransition,
                        "Only started, performed, and canceled live phases are admitted.");
            }
        }

        /// <summary>
        /// Deterministic ingress for simulator, replay, and tests. It deliberately
        /// uses the same Input System conversion contract as a live callback.
        /// </summary>
        public InteractionResult<InteractionRoutingResult> RouteRawObservation(
            InteractionPhase phase,
            object rawValue,
            long timestampTicks,
            InputObservationStamp stamp,
            InteractionIntentHandler handler = null)
        {
            return RouteCaptured(
                InputSystemSignalAdapter.CaptureRawObservation(
                    _orderedCandidates,
                    _observedSourceId,
                    _observedModality,
                    _observedCapabilities,
                    phase,
                    rawValue,
                    timestampTicks,
                    stamp),
                handler);
        }

        private InteractionResult<InteractionRoutingResult> RouteCaptured(
            InteractionResult<InteractionFrame> captured,
            InteractionIntentHandler handler)
        {
            if (!captured.Succeeded)
            {
                return InteractionResult<InteractionRoutingResult>.Fail(
                    captured.Error.Code,
                    captured.Error.Message,
                    captured.Error.Subject);
            }

            return InteractionResult<InteractionRoutingResult>.Success(
                _coordinator.RouteFrame(captured.Value, handler));
        }

        private InteractionResult<InteractionRoutingResult> RouteLiveStarted(
            InteractionFrame captured)
        {
            var pending = FindOwnedPendingWinner();
            if (!pending.Succeeded)
                return Fail(pending.Error);
            if (pending.Value != null)
            {
                return InteractionResult<InteractionRoutingResult>.Fail(
                    InteractionValidationCode.DuplicatePhase,
                    "A live started phase requires no pending candidate route.",
                    pending.Value.RouteId.Value);
            }
            return RouteFrame(captured, handler: null);
        }

        private InteractionResult<InteractionRoutingResult> RouteLivePerformed(
            InteractionFrame captured,
            InteractionIntentHandler handler)
        {
            var pending = FindOwnedPendingWinner();
            if (!pending.Succeeded)
                return Fail(pending.Error);

            if (pending.Value == null)
            {
                var syntheticStarted = RouteCaptured(
                    RebuildLiveFrame(captured, null, InteractionPhase.Started, buttonPulse: true),
                    handler: null);
                if (!syntheticStarted.Succeeded)
                    return syntheticStarted;

                pending = FindOwnedPendingWinner();
                if (!pending.Succeeded)
                    return Fail(pending.Error);
                if (pending.Value == null)
                    return syntheticStarted;
            }

            return RouteCaptured(
                RebuildLiveFrame(
                    captured,
                    pending.Value.RouteId,
                    InteractionPhase.Performed,
                    buttonPulse: true),
                handler);
        }

        private InteractionResult<InteractionRoutingResult> RouteLiveCanceled(
            InteractionFrame captured)
        {
            var pending = FindOwnedPendingWinner();
            if (!pending.Succeeded)
                return Fail(pending.Error);
            return pending.Value == null
                ? RouteEmptyFrame()
                : RouteCaptured(
                    RebuildLiveFrame(
                        captured,
                        pending.Value.RouteId,
                        InteractionPhase.Canceled,
                        buttonPulse: false),
                    handler: null);
        }

        private InteractionResult<InputRouteBinding> FindOwnedPendingWinner()
        {
            InputRouteBinding winner = null;
            foreach (var pending in _coordinator.State.PendingPhases)
            {
                if (!pending.SourceId.Equals(_observedSource)
                    || !_candidateRouteIds.Contains(pending.RouteId))
                {
                    continue;
                }

                if (winner != null)
                {
                    return InteractionResult<InputRouteBinding>.Fail(
                        InteractionValidationCode.AmbiguousContextCollision,
                        "Multiple pending routes claim one live Input System action.",
                        _observedSource.Value);
                }

                winner = _orderedCandidates.First(candidate =>
                    candidate.RouteId.Equals(pending.RouteId));
            }

            return InteractionResult<InputRouteBinding>.Success(winner);
        }

        private InteractionResult<InteractionRoutingResult> RouteFrame(
            InteractionFrame frame,
            InteractionIntentHandler handler) =>
            RouteCaptured(
                InteractionResult<InteractionFrame>.Success(frame),
                handler);

        private InteractionResult<InteractionRoutingResult> RouteEmptyFrame() =>
            RouteCaptured(
                InteractionFrame.Create(Array.Empty<SourceSignal>()),
                handler: null);

        private static InteractionResult<InteractionFrame> RebuildLiveFrame(
            InteractionFrame frame,
            RouteId? routeId,
            InteractionPhase phase,
            bool buttonPulse) =>
            InteractionFrame.Create(frame.Signals
                .Where(signal => !routeId.HasValue || signal.RouteId.Equals(routeId.Value))
                .Select(signal => new SourceSignal(
                signal.RouteId,
                signal.SourceId,
                signal.Modality,
                signal.SourceCapabilities,
                buttonPulse ? InteractionValue.FromButton(true) : signal.Value,
                phase,
                signal.TimestampTicks,
                signal.IngressSequence,
                signal.ObservationSequence)));

        private static InteractionResult<InteractionRoutingResult> Fail(
            InteractionError error) =>
            InteractionResult<InteractionRoutingResult>.Fail(
                error.Code,
                error.Message,
                error.Subject);

        private static InputRouteBinding[] FreezeAndValidate(
            InteractionCoordinator coordinator,
            IEnumerable<InputRouteBinding> orderedCandidates,
            string observedSourceId,
            InteractionModality observedModality,
            InteractionCapability observedCapabilities)
        {
            if (orderedCandidates == null)
            {
                throw new ArgumentNullException(nameof(orderedCandidates));
            }

            var candidates = orderedCandidates.ToArray();
            if (candidates.Length == 0 || candidates.Any(candidate => candidate == null))
            {
                throw new ArgumentException(
                    "At least one non-null ordered input route candidate is required.",
                    nameof(orderedCandidates));
            }

            var source = SourceId.TryCreate(observedSourceId);
            if (!source.Succeeded)
            {
                throw new ArgumentException(source.Error.Message, nameof(observedSourceId));
            }

            if (!Enum.IsDefined(typeof(InteractionModality), observedModality))
            {
                throw new ArgumentOutOfRangeException(nameof(observedModality));
            }

            var first = candidates[0];
            var firstAction = first.ActionReference == null
                ? null
                : first.ActionReference.action;
            var routeIds = new HashSet<RouteId>();
            foreach (var candidate in candidates)
            {
                if (!routeIds.Add(candidate.RouteId))
                {
                    throw new ArgumentException(
                        $"Input route candidate '{candidate.RouteId.Value}' is duplicated.",
                        nameof(orderedCandidates));
                }

                if (candidate.ActionId != first.ActionId
                    || candidate.ValueKind != first.ValueKind
                    || !candidate.SourceSelector.Equals(source.Value)
                    || candidate.Modality != observedModality
                    || candidate.SourceCapabilities != observedCapabilities)
                {
                    throw new ArgumentException(
                        "One adapter observation requires identical action, source, modality, capability, and value-kind facts.",
                        nameof(orderedCandidates));
                }

                var action = candidate.ActionReference == null
                    ? null
                    : candidate.ActionReference.action;
                if (action == null
                    || action.id != candidate.ActionId
                    || !ReferenceEquals(action, firstAction))
                {
                    throw new ArgumentException(
                        "Input route candidates require one stable shared InputAction instance.",
                        nameof(orderedCandidates));
                }

                if (action.type != InputActionType.Button)
                {
                    throw new ArgumentException(
                        "This consumer bridge currently admits only InputActionType.Button actions.",
                        nameof(orderedCandidates));
                }

                if (!coordinator.Registry.TryGetRoute(candidate.RouteId, out var route))
                {
                    throw new ArgumentException(
                        $"Input binding references unknown route '{candidate.RouteId.Value}'.",
                        nameof(orderedCandidates));
                }

                if (!route.IntentId.Equals(candidate.IntentId)
                    || !route.SourceSelector.Equals(candidate.SourceSelector)
                    || route.SourceModality != candidate.Modality
                    || route.SourceCapabilities != candidate.SourceCapabilities)
                {
                    throw new ArgumentException(
                        $"Input binding facts do not match semantic route '{candidate.RouteId.Value}'.",
                        nameof(orderedCandidates));
                }
                var descriptor = InputSystemSignalAdapter.ValidateRouteBindingDescriptor(route, candidate);
                if (!descriptor.Succeeded)
                {
                    throw new ArgumentException(
                        descriptor.Error.Message,
                        nameof(orderedCandidates));
                }

                if (!coordinator.Registry.TryGetIntent(candidate.IntentId, out var intent)
                    || intent.ValueKind != candidate.ValueKind)
                {
                    throw new ArgumentException(
                        $"Input binding value kind does not match semantic intent '{candidate.IntentId.Value}'.",
                        nameof(orderedCandidates));
                }
                if ((candidate.SourceCapabilities & intent.RequiredCapabilities)
                    != intent.RequiredCapabilities)
                {
                    throw new ArgumentException(
                        $"Input binding does not satisfy semantic intent '{candidate.IntentId.Value}' required capabilities.",
                        nameof(orderedCandidates));
                }
            }

            return candidates;
        }
    }
}
