using System;
using System.Collections.Generic;
using System.Linq;
using Lingkyn.Interaction.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lingkyn.Interaction.Unity
{
    public sealed class InputRouteBinding : IEquatable<InputRouteBinding>
    {
        public InputRouteBinding(
            RouteId routeId,
            IntentId intentId,
            SourceId sourceSelector,
            InteractionModality modality,
            InteractionCapability sourceCapabilities,
            InteractionValueKind valueKind,
            InputActionReference actionReference)
        {
            if (string.IsNullOrEmpty(routeId.Value))
                throw new ArgumentException("Route identity is required.", nameof(routeId));
            if (string.IsNullOrEmpty(intentId.Value))
                throw new ArgumentException("Intent identity is required.", nameof(intentId));
            if (string.IsNullOrEmpty(sourceSelector.Value))
                throw new ArgumentException("Source selector is required.", nameof(sourceSelector));
            if (actionReference == null || actionReference.action == null || actionReference.action.id == Guid.Empty)
                throw new ArgumentException("A stable InputActionReference is required.", nameof(actionReference));

            RouteId = routeId;
            IntentId = intentId;
            SourceSelector = sourceSelector;
            Modality = modality;
            SourceCapabilities = sourceCapabilities;
            ValueKind = valueKind;
            ActionReference = actionReference;
            ActionId = actionReference.action.id;
        }

        public RouteId RouteId { get; }
        public IntentId IntentId { get; }
        public SourceId SourceSelector { get; }
        public InteractionModality Modality { get; }
        public InteractionCapability SourceCapabilities { get; }
        public InteractionValueKind ValueKind { get; }
        public InputActionReference ActionReference { get; }
        public Guid ActionId { get; }

        public bool Equals(InputRouteBinding other) => other != null
            && RouteId.Equals(other.RouteId)
            && IntentId.Equals(other.IntentId)
            && SourceSelector.Equals(other.SourceSelector)
            && Modality == other.Modality
            && SourceCapabilities == other.SourceCapabilities
            && ValueKind == other.ValueKind
            && ActionId.Equals(other.ActionId);

        public override bool Equals(object obj) => Equals(obj as InputRouteBinding);
        public override int GetHashCode() =>
            HashCode.Combine(RouteId, IntentId, SourceSelector, (int)Modality, (int)SourceCapabilities, (int)ValueKind, ActionId);
    }

    public readonly struct InputObservationStamp : IEquatable<InputObservationStamp>
    {
        public InputObservationStamp(int observationSequence, int firstIngressSequence)
        {
            if (observationSequence < 0)
                throw new ArgumentOutOfRangeException(nameof(observationSequence));
            if (firstIngressSequence < 0)
                throw new ArgumentOutOfRangeException(nameof(firstIngressSequence));
            ObservationSequence = observationSequence;
            FirstIngressSequence = firstIngressSequence;
        }

        public int ObservationSequence { get; }
        public int FirstIngressSequence { get; }
        public bool Equals(InputObservationStamp other) =>
            ObservationSequence == other.ObservationSequence
            && FirstIngressSequence == other.FirstIngressSequence;
        public override bool Equals(object obj) => obj is InputObservationStamp other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(ObservationSequence, FirstIngressSequence);
    }

    public static class InputSystemSignalAdapter
    {
        public static InteractionResult<InteractionFrame> CaptureCallback(
            InputAction.CallbackContext callback,
            IEnumerable<InputRouteBinding> orderedCandidates,
            string observedSourceId,
            InteractionModality observedModality,
            InteractionCapability observedCapabilities,
            long timestampTicks,
            InputObservationStamp stamp)
        {
            var candidates = FreezeCandidates(orderedCandidates);
            var common = ValidateCommonObservation(
                candidates, observedSourceId, observedModality, observedCapabilities, timestampTicks);
            if (!common.Succeeded)
                return InteractionResult<InteractionFrame>.Fail(common.Error.Code, common.Error.Message, common.Error.Subject);
            if (callback.action == null || callback.action.id != candidates[0].ActionId)
            {
                return InteractionResult<InteractionFrame>.Fail(
                    InteractionValidationCode.UnknownRoute,
                    "Callback action GUID does not match the explicit route binding.",
                    candidates[0].RouteId.Value);
            }

            var phase = ConvertPhase(callback.phase);
            if (!phase.Succeeded)
                return InteractionResult<InteractionFrame>.Fail(phase.Error.Code, phase.Error.Message, phase.Error.Subject);

            object rawValue;
            switch (candidates[0].ValueKind)
            {
                case InteractionValueKind.Button:
                    rawValue = callback.ReadValueAsButton();
                    break;
                case InteractionValueKind.Scalar:
                    rawValue = callback.ReadValue<float>();
                    break;
                case InteractionValueKind.Vector2:
                    rawValue = callback.ReadValue<Vector2>();
                    break;
                case InteractionValueKind.Vector3:
                    rawValue = callback.ReadValue<Vector3>();
                    break;
                default:
                    return InteractionResult<InteractionFrame>.Fail(
                        InteractionValidationCode.KindMismatch,
                        $"Input System callback conversion does not support '{candidates[0].ValueKind}'.",
                        candidates[0].RouteId.Value);
            }

            return CaptureRawObservation(
                candidates,
                observedSourceId,
                observedModality,
                observedCapabilities,
                phase.Value,
                rawValue,
                timestampTicks,
                stamp);
        }

        public static InteractionResult<InteractionFrame> CaptureRawObservation(
            IEnumerable<InputRouteBinding> orderedCandidates,
            string observedSourceId,
            InteractionModality observedModality,
            InteractionCapability observedCapabilities,
            InteractionPhase phase,
            object rawValue,
            long timestampTicks,
            InputObservationStamp stamp)
        {
            var candidates = FreezeCandidates(orderedCandidates);
            var common = ValidateCommonObservation(
                candidates, observedSourceId, observedModality, observedCapabilities, timestampTicks);
            if (!common.Succeeded)
                return InteractionResult<InteractionFrame>.Fail(common.Error.Code, common.Error.Message, common.Error.Subject);
            if (!Enum.IsDefined(typeof(InteractionPhase), phase))
            {
                return InteractionResult<InteractionFrame>.Fail(
                    InteractionValidationCode.InvalidPhaseTransition,
                    "Only started, performed, and canceled phases are admitted.");
            }

            var value = ConvertRawValue(candidates[0].ValueKind, rawValue);
            if (!value.Succeeded)
                return InteractionResult<InteractionFrame>.Fail(value.Error.Code, value.Error.Message, value.Error.Subject);

            var sourceId = SourceId.TryCreate(observedSourceId);
            if (!sourceId.Succeeded)
                return InteractionResult<InteractionFrame>.Fail(sourceId.Error.Code, sourceId.Error.Message, sourceId.Error.Subject);

            var signals = new List<SourceSignal>(candidates.Count);
            for (var index = 0; index < candidates.Count; index++)
            {
                signals.Add(new SourceSignal(
                    candidates[index].RouteId,
                    sourceId.Value,
                    observedModality,
                    observedCapabilities,
                    value.Value,
                    phase,
                    timestampTicks,
                    checked(stamp.FirstIngressSequence + index),
                    stamp.ObservationSequence));
            }
            return InteractionFrame.Create(signals);
        }

        public static InteractionResult<InteractionValue> ConvertRawValue(
            InteractionValueKind valueKind,
            object rawValue)
        {
            switch (valueKind)
            {
                case InteractionValueKind.Button when rawValue is bool button:
                    return InteractionResult<InteractionValue>.Success(InteractionValue.FromButton(button));
                case InteractionValueKind.Scalar when rawValue is float scalarFloat:
                    return Validate(InteractionValue.FromScalar(scalarFloat));
                case InteractionValueKind.Scalar when rawValue is double scalarDouble:
                    return Validate(InteractionValue.FromScalar(scalarDouble));
                case InteractionValueKind.Vector2 when rawValue is Vector2 vector2:
                    return Validate(InteractionValue.FromVector2(
                        new InteractionVector2(vector2.x, vector2.y)));
                case InteractionValueKind.Vector3 when rawValue is Vector3 vector3:
                    return Validate(InteractionValue.FromVector3(
                        new InteractionVector3(vector3.x, vector3.y, vector3.z)));
                default:
                    return InteractionResult<InteractionValue>.Fail(
                        InteractionValidationCode.KindMismatch,
                        $"Raw Input System value does not match '{valueKind}'.");
            }
        }

        public static InteractionResult<InteractionPhase> ConvertPhase(InputActionPhase phase)
        {
            switch (phase)
            {
                case InputActionPhase.Started:
                    return InteractionResult<InteractionPhase>.Success(InteractionPhase.Started);
                case InputActionPhase.Performed:
                    return InteractionResult<InteractionPhase>.Success(InteractionPhase.Performed);
                case InputActionPhase.Canceled:
                    return InteractionResult<InteractionPhase>.Success(InteractionPhase.Canceled);
                default:
                    return InteractionResult<InteractionPhase>.Fail(
                        InteractionValidationCode.InvalidPhaseTransition,
                        $"Input System phase '{phase}' is not an admitted semantic phase.");
            }
        }

        private static InteractionResult<InteractionValue> Validate(InteractionValue value) =>
            InteractionValue.Validate(value.Kind, value);

        private static List<InputRouteBinding> FreezeCandidates(IEnumerable<InputRouteBinding> candidates) =>
            candidates == null ? new List<InputRouteBinding>() : candidates.ToList();

        private static InteractionResult ValidateCommonObservation(
            IReadOnlyList<InputRouteBinding> candidates,
            string observedSourceId,
            InteractionModality observedModality,
            InteractionCapability observedCapabilities,
            long timestampTicks)
        {
            if (candidates == null || candidates.Count == 0 || candidates.Any(candidate => candidate == null))
                return InteractionResult.Fail(InteractionValidationCode.InvalidFrame, "At least one non-null ordered route candidate is required.");
            if (timestampTicks < 0)
                return InteractionResult.Fail(InteractionValidationCode.InvalidFrame, "Timestamp must be non-negative.");
            var source = SourceId.TryCreate(observedSourceId);
            if (!source.Succeeded)
                return InteractionResult.Fail(source.Error.Code, source.Error.Message, source.Error.Subject);

            var first = candidates[0];
            var routeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var candidate in candidates)
            {
                if (!routeIds.Add(candidate.RouteId.Value))
                    return InteractionResult.Fail(InteractionValidationCode.DuplicateIdentity, "Observation route candidates must be unique.", candidate.RouteId.Value);
                if (candidate.ActionId != first.ActionId || candidate.ValueKind != first.ValueKind
                    || !candidate.SourceSelector.Equals(source.Value)
                    || candidate.Modality != observedModality
                    || candidate.SourceCapabilities != observedCapabilities)
                {
                    return InteractionResult.Fail(
                        InteractionValidationCode.InvalidFrame,
                        "One physical observation requires identical action, source, modality, capability, and value-kind facts.",
                        candidate.RouteId.Value);
                }
            }
            return InteractionResult.Success();
        }
    }
}
