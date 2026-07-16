using System;
using System.Collections.Generic;
using System.Linq;

namespace Lingkyn.Interaction.Core
{
    public sealed class InteractionCoordinator
    {
        private readonly InteractionRegistry _registry;
        private readonly InteractionRouter _router;
        private readonly InteractionRoutingSession _session;
        private IReadOnlyList<ContextId> _activeContexts;
        private InteractionPolicySnapshot _policy;

        public InteractionCoordinator(
            InteractionRegistry registry,
            IEnumerable<ContextId> activeContexts = null,
            InteractionPolicySnapshot policy = null,
            InteractionRoutingSession session = null)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _router = new InteractionRouter();
            _session = session ?? new InteractionRoutingSession();
            _activeContexts = InteractionReadOnly.FreezeList(MaterializeContextIds(activeContexts));
            _policy = policy ?? InteractionPolicySnapshot.Empty;
        }

        public InteractionRegistry Registry => _registry;

        public InteractionRoutingSession Session => _session;

        public IReadOnlyList<ContextId> ActiveContexts => _activeContexts;

        public InteractionPolicySnapshot Policy => _policy;

        public void SetActiveContexts(IEnumerable<ContextId> activeContexts)
        {
            _activeContexts = InteractionReadOnly.FreezeList(MaterializeContextIds(activeContexts));
        }

        public void SetPolicy(InteractionPolicySnapshot policy)
        {
            _policy = policy ?? InteractionPolicySnapshot.Empty;
        }

        public InteractionRoutingResult RouteFrame(InteractionFrame frame, InteractionIntentHandler handler = null)
        {
            if (frame == null)
            {
                throw new ArgumentNullException(nameof(frame));
            }

            return _router.Route(_registry, _activeContexts, _policy, frame, _session, handler);
        }

        private static List<ContextId> MaterializeContextIds(IEnumerable<ContextId> activeContexts)
        {
            if (activeContexts == null)
            {
                return new List<ContextId>();
            }

            return activeContexts
                .OrderBy(id => id, Comparer<ContextId>.Default)
                .ToList();
        }
    }
}
