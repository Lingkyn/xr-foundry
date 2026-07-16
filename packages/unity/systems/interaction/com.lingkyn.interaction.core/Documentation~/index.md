# Lingkyn Interaction Core

Engine-light semantic interaction routing for XR Foundry.

## Concepts

- **IntentId** — application meaning such as `ui.confirm`
- **ContextId** — independently activated route groups with priority
- **RouteId** — adapter-owned source route identity; may appear in multiple contexts and is admitted by priority
- **InteractionCoordinator** — routes immutable frames against registry, active contexts, policy, and session state
- **InteractionRoutingSession** — holds phase and toggle latch state across frames
- **BindingOverride** / **BindingSuggestion** — advisory or persisted opaque adapter tokens; they do not activate routes
- **InteractionPolicySnapshot** — immutable per-intent activation and route availability supplied by a consumer port
- **InteractionRouter** — deterministic ingress-ordered routing with diagnostics and handler outcomes

See `docs/standards/interaction/architecture-contract.md` for the full contract.
