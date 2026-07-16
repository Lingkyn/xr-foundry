using System;
using System.Collections.Generic;

namespace Lingkyn.Interaction.Core
{
    public readonly struct SourceSignal : IEquatable<SourceSignal>
    {
        public SourceSignal(RouteId routeId, SourceId sourceId, InteractionModality modality,
            InteractionCapability sourceCapabilities, InteractionValue value, InteractionPhase phase,
            long timestampTicks, int ingressSequence, int observationSequence = 0)
        { RouteId = routeId; SourceId = sourceId; Modality = modality; SourceCapabilities = sourceCapabilities;
          Value = value; Phase = phase; TimestampTicks = timestampTicks; IngressSequence = ingressSequence; ObservationSequence = observationSequence; }
        public RouteId RouteId { get; }
        public SourceId SourceId { get; }
        public InteractionModality Modality { get; }
        public InteractionCapability SourceCapabilities { get; }
        public InteractionValue Value { get; }
        public InteractionPhase Phase { get; }
        public long TimestampTicks { get; }
        public int IngressSequence { get; }
        public int ObservationSequence { get; }
        public bool Equals(SourceSignal other) => RouteId.Equals(other.RouteId) && PhysicalEquals(other)
            && IngressSequence == other.IngressSequence && ObservationSequence == other.ObservationSequence;
        internal bool PhysicalEquals(SourceSignal other) => SourceId.Equals(other.SourceId) && Modality == other.Modality
            && SourceCapabilities == other.SourceCapabilities && Value.Equals(other.Value) && Phase == other.Phase && TimestampTicks == other.TimestampTicks;
        public override bool Equals(object obj) => obj is SourceSignal other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(RouteId, SourceId, Modality, SourceCapabilities, Value, Phase, TimestampTicks, IngressSequence) ^ ObservationSequence;
    }

    public sealed class InteractionFrame
    {
        private readonly IReadOnlyList<SourceSignal> _signals;
        private InteractionFrame(IReadOnlyList<SourceSignal> signals) { _signals = signals; }
        public IReadOnlyList<SourceSignal> Signals => _signals;
        public static InteractionResult<InteractionFrame> Create(IEnumerable<SourceSignal> signals)
        {
            if (signals == null) return InteractionResult<InteractionFrame>.Fail(InteractionValidationCode.InvalidFrame, "Frame signals are required.");
            var list = new List<SourceSignal>(); var lastIngress = -1; var lastObservation = -1; var lastTimestamp = -1L;
            SourceSignal observationPrototype = default; var hasPrototype = false;
            foreach (var s in signals)
            {
                if (s.IngressSequence < 0 || s.IngressSequence <= lastIngress || s.ObservationSequence < 0
                    || s.ObservationSequence < lastObservation || s.TimestampTicks < 0 || s.TimestampTicks < lastTimestamp
                    || string.IsNullOrEmpty(s.RouteId.Value) || string.IsNullOrEmpty(s.SourceId.Value)
                    || !Enum.IsDefined(typeof(InteractionModality), s.Modality) || !Enum.IsDefined(typeof(InteractionPhase), s.Phase)
                    || !KnownCapabilities(s.SourceCapabilities))
                    return InteractionResult<InteractionFrame>.Fail(InteractionValidationCode.InvalidFrame, "Invalid signal order, identity, timestamp, enum, or capability.", s.RouteId.Value);
                if (s.ObservationSequence != lastObservation) { observationPrototype = s; hasPrototype = true; }
                else if (!hasPrototype || !observationPrototype.PhysicalEquals(s))
                    return InteractionResult<InteractionFrame>.Fail(InteractionValidationCode.InvalidFrame, "Signals in one observation must share identical physical facts.", s.RouteId.Value);
                list.Add(s); lastIngress = s.IngressSequence; lastObservation = s.ObservationSequence; lastTimestamp = s.TimestampTicks;
            }
            return InteractionResult<InteractionFrame>.Success(new InteractionFrame(InteractionReadOnly.FreezeList(list)));
        }
        internal static bool KnownCapabilities(InteractionCapability value)
        {
            const InteractionCapability all = InteractionCapability.Digital | InteractionCapability.Scalar | InteractionCapability.Vector2
                | InteractionCapability.Vector3 | InteractionCapability.Pose | InteractionCapability.Pointing | InteractionCapability.HapticOutput | InteractionCapability.Text;
            return (value & ~all) == 0;
        }
    }
}
