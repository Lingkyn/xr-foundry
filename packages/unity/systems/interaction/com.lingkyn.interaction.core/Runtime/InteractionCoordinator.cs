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
        public void SetActiveContexts(IEnumerable<ContextId> value) =>
            _activeContexts = InteractionReadOnly.FreezeList((value ?? Array.Empty<ContextId>()).OrderBy(x => x));
        public void SetPolicy(InteractionPolicySnapshot value) => _policy = value ?? InteractionPolicySnapshot.Empty;
        public InteractionRoutingResult RouteFrame(InteractionFrame frame, InteractionIntentHandler handler = null)
        {
            var result = _router.Route(_registry, _activeContexts, _policy, frame, _state, handler);
            _state = result.NextState;
            return result;
        }
    }
}
