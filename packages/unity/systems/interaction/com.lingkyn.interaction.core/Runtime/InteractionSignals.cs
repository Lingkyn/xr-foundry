using System;
using System.Collections.Generic;
using System.Linq;

namespace Lingkyn.Interaction.Core
{
    public readonly struct SourceSignal : IEquatable<SourceSignal>
    {
        public SourceSignal(
            RouteId routeId,
            SourceId sourceId,
            InteractionModality modality,
            InteractionCapability sourceCapabilities,
            InteractionValue value,
            InteractionPhase phase,
            long timestampTicks,
            int ingressSequence)
        {
            RouteId = routeId;
            SourceId = sourceId;
            Modality = modality;
            SourceCapabilities = sourceCapabilities;
            Value = value;
            Phase = phase;
            TimestampTicks = timestampTicks;
            IngressSequence = ingressSequence;
        }

        public RouteId RouteId { get; }
        public SourceId SourceId { get; }
        public InteractionModality Modality { get; }
        public InteractionCapability SourceCapabilities { get; }
        public InteractionValue Value { get; }
        public InteractionPhase Phase { get; }
        public long TimestampTicks { get; }
        public int IngressSequence { get; }

        public bool Equals(SourceSignal other) =>
            RouteId.Equals(other.RouteId)
            && SourceId.Equals(other.SourceId)
            && Modality == other.Modality
            && SourceCapabilities == other.SourceCapabilities
            && Value.Equals(other.Value)
            && Phase == other.Phase
            && TimestampTicks == other.TimestampTicks
            && IngressSequence == other.IngressSequence;

        public override bool Equals(object obj) => obj is SourceSignal other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(RouteId, SourceId, Modality, SourceCapabilities, Value, Phase, TimestampTicks, IngressSequence);
    }

    public sealed class InteractionFrame
    {
        private readonly IReadOnlyList<SourceSignal> _signals;

        private InteractionFrame(IReadOnlyList<SourceSignal> signals)
        {
            _signals = signals;
        }

        public IReadOnlyList<SourceSignal> Signals => _signals;

        public static InteractionResult<InteractionFrame> Create(IEnumerable<SourceSignal> signals)
        {
            if (signals == null)
            {
                return InteractionResult<InteractionFrame>.Fail(
                    InteractionValidationCode.InvalidFrame,
                    "Frame signals are required.");
            }

            var list = new List<SourceSignal>();
            var lastSequence = -1;
            foreach (var signal in signals)
            {
                if (signal.IngressSequence <= lastSequence)
                {
                    return InteractionResult<InteractionFrame>.Fail(
                        InteractionValidationCode.InvalidIngressSequence,
                        "Frame signals must preserve strictly increasing ingress sequence.",
                        signal.RouteId.Value);
                }

                lastSequence = signal.IngressSequence;
                list.Add(signal);
            }

            return InteractionResult<InteractionFrame>.Success(
                new InteractionFrame(InteractionReadOnly.FreezeList(list)));
        }
    }
}
