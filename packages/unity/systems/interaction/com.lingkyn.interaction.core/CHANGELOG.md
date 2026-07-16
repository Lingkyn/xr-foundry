# Changelog

## [Unreleased]

- Implement engine-light semantic interaction routing core with identities, typed values, registry, policy snapshots, deterministic router, diagnostics, handler outcomes, and contract tests.
- Add immutable routing state, observation-scoped route competition, globally
  unique route identity, explicit hold/threshold/toggle policy, binding override
  ports, and focused verification tests.
- Admit routes by RouteId only at runtime; preserve observed SourceId on events without coupling it to route authoring selectors.
- Emit PolicyApplied diagnostics for activation-policy suppression and route value transforms; add acceptance-evidence tests that independently assert routing surfaces per verification contract.
