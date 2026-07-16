# Lingkyn Interaction Core

- `IntentId`: stable application meaning.
- `ContextId`: independently activated route group with explicit priority.
- `RouteId`: globally unique admitted route identity owned by one context.
- `ObservationSequence`: alternative route candidates for one physical observation.
- `InteractionRoutingState`: immutable phase/toggle input and `NextState` output.
- `InteractionCoordinator`: convenience holder that replaces state after a frame.
- `BindingOverride` / `BindingSuggestion`: inert opaque adapter tokens.
- `InteractionPolicySnapshot`: immutable activation, hold, threshold, and route policy.
- `InteractionRouter`: stateless deterministic routing with diagnostics and outcomes.

See `docs/standards/interaction/architecture-contract.md` for the claim boundary.
