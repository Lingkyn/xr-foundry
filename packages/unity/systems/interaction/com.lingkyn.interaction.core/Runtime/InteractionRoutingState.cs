using System;
using System.Collections.Generic;
using System.Linq;

namespace Lingkyn.Interaction.Core
{
    public readonly struct InteractionRoutePhaseState : IEquatable<InteractionRoutePhaseState>
    {
        public InteractionRoutePhaseState(ContextId contextId, RouteId routeId, SourceId sourceId, long startedAtTicks)
        {
            ContextId = contextId;
            RouteId = routeId;
            SourceId = sourceId;
            StartedAtTicks = startedAtTicks;
        }

        public ContextId ContextId { get; }
        public RouteId RouteId { get; }
        public SourceId SourceId { get; }
        public long StartedAtTicks { get; }

        public bool Equals(InteractionRoutePhaseState other) =>
            ContextId.Equals(other.ContextId) && RouteId.Equals(other.RouteId)
            && SourceId.Equals(other.SourceId) && StartedAtTicks == other.StartedAtTicks;
        public override bool Equals(object obj) => obj is InteractionRoutePhaseState other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(ContextId, RouteId, SourceId, StartedAtTicks);
    }

    public readonly struct InteractionToggleState : IEquatable<InteractionToggleState>
    {
        public InteractionToggleState(IntentId intentId, bool active) { IntentId = intentId; Active = active; }
        public IntentId IntentId { get; }
        public bool Active { get; }
        public bool Equals(InteractionToggleState other) => IntentId.Equals(other.IntentId) && Active == other.Active;
        public override bool Equals(object obj) => obj is InteractionToggleState other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(IntentId, Active);
    }

    public sealed class InteractionRoutingState : IEquatable<InteractionRoutingState>
    {
        private readonly InteractionRoutePhaseState[] _phases;
        private readonly InteractionToggleState[] _toggles;

        internal InteractionRoutingState(IEnumerable<InteractionRoutePhaseState> phases, IEnumerable<InteractionToggleState> toggles)
        {
            _phases = (phases ?? Array.Empty<InteractionRoutePhaseState>())
                .OrderBy(x => Key(x.ContextId, x.RouteId, x.SourceId), StringComparer.Ordinal).ToArray();
            _toggles = (toggles ?? Array.Empty<InteractionToggleState>())
                .OrderBy(x => x.IntentId, Comparer<IntentId>.Default).ToArray();
        }

        public IReadOnlyList<InteractionRoutePhaseState> PendingPhases => Array.AsReadOnly(_phases);
        public IReadOnlyList<InteractionToggleState> ToggleStates => Array.AsReadOnly(_toggles);
        public static InteractionRoutingState Empty { get; } = new InteractionRoutingState(null, null);

        public bool Equals(InteractionRoutingState other) =>
            other != null && _phases.SequenceEqual(other._phases) && _toggles.SequenceEqual(other._toggles);
        public override bool Equals(object obj) => Equals(obj as InteractionRoutingState);
        public override int GetHashCode()
        {
            unchecked { var h = 17; foreach (var x in _phases) h = h * 31 + x.GetHashCode(); foreach (var x in _toggles) h = h * 31 + x.GetHashCode(); return h; }
        }

        internal static string Key(ContextId contextId, RouteId routeId, SourceId sourceId) =>
            $"{contextId.Value ?? string.Empty}\0{routeId.Value ?? string.Empty}\0{sourceId.Value ?? string.Empty}";
    }

    internal sealed class InteractionRoutingStateBuilder
    {
        private readonly Dictionary<string, InteractionRoutePhaseState> _phases = new Dictionary<string, InteractionRoutePhaseState>(StringComparer.Ordinal);
        private readonly Dictionary<string, InteractionToggleState> _toggles = new Dictionary<string, InteractionToggleState>(StringComparer.Ordinal);

        public InteractionRoutingStateBuilder(InteractionRoutingState prior)
        {
            foreach (var phase in (prior ?? InteractionRoutingState.Empty).PendingPhases)
                _phases[InteractionRoutingState.Key(phase.ContextId, phase.RouteId, phase.SourceId)] = phase;
            foreach (var toggle in (prior ?? InteractionRoutingState.Empty).ToggleStates)
                _toggles[toggle.IntentId.Value] = toggle;
        }

        public bool TryGet(ContextId contextId, RouteId routeId, SourceId sourceId, out InteractionRoutePhaseState phase) =>
            _phases.TryGetValue(InteractionRoutingState.Key(contextId, routeId, sourceId), out phase);
        public void Start(ContextId contextId, RouteId routeId, SourceId sourceId, long ticks) =>
            _phases[InteractionRoutingState.Key(contextId, routeId, sourceId)] = new InteractionRoutePhaseState(contextId, routeId, sourceId, ticks);
        public void Clear(ContextId contextId, RouteId routeId, SourceId sourceId) =>
            _phases.Remove(InteractionRoutingState.Key(contextId, routeId, sourceId));
        public bool Toggle(IntentId intentId)
        {
            var next = !_toggles.TryGetValue(intentId.Value, out var current) || !current.Active;
            _toggles[intentId.Value] = new InteractionToggleState(intentId, next);
            return next;
        }
        public InteractionRoutingState Build() => new InteractionRoutingState(_phases.Values, _toggles.Values);
    }
}
