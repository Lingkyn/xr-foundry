# Changelog

## [Unreleased]

- Implement engine-light semantic interaction routing core with identities, typed values, registry, policy snapshots, deterministic router, diagnostics, handler outcomes, and contract tests.
- Add InteractionCoordinator, InteractionRoutingSession, binding override ports, shared RouteId admission across contexts, strict ingress-ordered routing, and expanded verification tests.
- Admit routes by RouteId only at runtime; preserve observed SourceId on events without coupling it to route authoring selectors.
- Emit PolicyApplied diagnostics for activation-policy suppression and route value transforms; add acceptance-evidence tests that independently assert routing surfaces per verification contract.
