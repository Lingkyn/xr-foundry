using System;
using System.Collections.Generic;

namespace Lingkyn.Interaction.Core
{
    public readonly struct SemanticInteractionEvent : IEquatable<SemanticInteractionEvent>
    {
        public SemanticInteractionEvent(
            IntentId intentId,
            ContextId contextId,
            RouteId routeId,
            SourceId sourceId,
            InteractionModality modality,
            InteractionValue value,
            InteractionPhase phase,
            InteractionActivationMode activationMode,
            int ingressSequence,
            long timestampTicks)
        {
            IntentId = intentId;
            ContextId = contextId;
            RouteId = routeId;
            SourceId = sourceId;
            Modality = modality;
            Value = value;
            Phase = phase;
            ActivationMode = activationMode;
            IngressSequence = ingressSequence;
            TimestampTicks = timestampTicks;
        }

        public IntentId IntentId { get; }
        public ContextId ContextId { get; }
        public RouteId RouteId { get; }
        public SourceId SourceId { get; }
        public InteractionModality Modality { get; }
        public InteractionValue Value { get; }
        public InteractionPhase Phase { get; }
        public InteractionActivationMode ActivationMode { get; }
        public int IngressSequence { get; }
        public long TimestampTicks { get; }

        public bool Equals(SemanticInteractionEvent other) =>
            IntentId.Equals(other.IntentId)
            && ContextId.Equals(other.ContextId)
            && RouteId.Equals(other.RouteId)
            && SourceId.Equals(other.SourceId)
            && Modality == other.Modality
            && Value.Equals(other.Value)
            && Phase == other.Phase
            && ActivationMode == other.ActivationMode
            && IngressSequence == other.IngressSequence
            && TimestampTicks == other.TimestampTicks;

        public override bool Equals(object obj) => obj is SemanticInteractionEvent other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = HashCode.Combine(IntentId, ContextId, RouteId, SourceId, Modality, Value, Phase, ActivationMode);
                hash = HashCode.Combine(hash, IngressSequence, TimestampTicks);
                return hash;
            }
        }
    }

    public readonly struct InteractionDiagnostic : IEquatable<InteractionDiagnostic>
    {
        public InteractionDiagnostic(
            InteractionDiagnosticKind kind,
            InteractionValidationCode code,
            string message,
            RouteId routeId,
            ContextId contextId,
            IntentId intentId,
            int ingressSequence)
        {
            Kind = kind;
            Code = code;
            Message = message ?? string.Empty;
            RouteId = routeId;
            ContextId = contextId;
            IntentId = intentId;
            IngressSequence = ingressSequence;
        }

        public InteractionDiagnosticKind Kind { get; }
        public InteractionValidationCode Code { get; }
        public string Message { get; }
        public RouteId RouteId { get; }
        public ContextId ContextId { get; }
        public IntentId IntentId { get; }
        public int IngressSequence { get; }

        public bool Equals(InteractionDiagnostic other) =>
            Kind == other.Kind
            && Code == other.Code
            && string.Equals(Message, other.Message, StringComparison.Ordinal)
            && RouteId.Equals(other.RouteId)
            && ContextId.Equals(other.ContextId)
            && IntentId.Equals(other.IntentId)
            && IngressSequence == other.IngressSequence;

        public override bool Equals(object obj) => obj is InteractionDiagnostic other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Kind, Code, Message, RouteId, ContextId, IntentId, IngressSequence);
    }

    public readonly struct InteractionDispatchResult : IEquatable<InteractionDispatchResult>
    {
        public InteractionDispatchResult(
            InteractionDispatchStatus status,
            RouteId routeId,
            ContextId contextId,
            IntentId intentId,
            InteractionPhase phase,
            InteractionHandlerOutcome? handlerOutcome,
            int ingressSequence,
            string message)
        {
            Status = status;
            RouteId = routeId;
            ContextId = contextId;
            IntentId = intentId;
            Phase = phase;
            HandlerOutcome = handlerOutcome;
            IngressSequence = ingressSequence;
            Message = message ?? string.Empty;
        }

        public InteractionDispatchStatus Status { get; }
        public RouteId RouteId { get; }
        public ContextId ContextId { get; }
        public IntentId IntentId { get; }
        public InteractionPhase Phase { get; }
        public InteractionHandlerOutcome? HandlerOutcome { get; }
        public int IngressSequence { get; }
        public string Message { get; }

        public bool Equals(InteractionDispatchResult other) =>
            Status == other.Status
            && RouteId.Equals(other.RouteId)
            && ContextId.Equals(other.ContextId)
            && IntentId.Equals(other.IntentId)
            && Phase == other.Phase
            && HandlerOutcome == other.HandlerOutcome
            && IngressSequence == other.IngressSequence
            && string.Equals(Message, other.Message, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is InteractionDispatchResult other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Status, RouteId, ContextId, IntentId, Phase, HandlerOutcome, IngressSequence, Message);
    }

    public sealed class ActiveContextSnapshot
    {
        private readonly IReadOnlyList<ContextId> _activeContexts;

        internal ActiveContextSnapshot(IReadOnlyList<ContextId> activeContexts)
        {
            _activeContexts = activeContexts ?? Array.Empty<ContextId>();
        }

        public IReadOnlyList<ContextId> ActiveContexts => _activeContexts;
    }

    public sealed class InteractionRoutingResult
    {
        internal InteractionRoutingResult(
            IReadOnlyList<InteractionDispatchResult> dispatches,
            IReadOnlyList<InteractionDiagnostic> diagnostics,
            IReadOnlyList<SemanticInteractionEvent> events,
            ActiveContextSnapshot activeContexts,
            InteractionRoutingState nextState)
        {
            Dispatches = dispatches ?? Array.Empty<InteractionDispatchResult>();
            Diagnostics = diagnostics ?? Array.Empty<InteractionDiagnostic>();
            Events = events ?? Array.Empty<SemanticInteractionEvent>();
            ActiveContexts = activeContexts ?? new ActiveContextSnapshot(Array.Empty<ContextId>());
            NextState = nextState ?? InteractionRoutingState.Empty;
        }

        public IReadOnlyList<InteractionDispatchResult> Dispatches { get; }
        public IReadOnlyList<InteractionDiagnostic> Diagnostics { get; }
        public IReadOnlyList<SemanticInteractionEvent> Events { get; }
        public ActiveContextSnapshot ActiveContexts { get; }
        public InteractionRoutingState NextState { get; }
    }

    public delegate InteractionHandlerOutcome InteractionIntentHandler(SemanticInteractionEvent semanticEvent);
}
