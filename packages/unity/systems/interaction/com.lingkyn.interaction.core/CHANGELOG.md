# Changelog

## [Unreleased]

- Implement engine-light semantic interaction routing core with identities, typed values, registry, policy snapshots, deterministic router, diagnostics, handler outcomes, and contract tests.
- Add immutable routing state, observation-scoped route competition, globally
  unique route identity, explicit hold/threshold/toggle policy, binding override
  ports, and focused verification tests.
- Admit routes by RouteId only at runtime; preserve observed SourceId on events without coupling it to route authoring selectors.
- Emit PolicyApplied diagnostics for activation-policy suppression and route value transforms; add acceptance-evidence tests that independently assert routing surfaces per verification contract.
- Reconcile routing state across effective policy and active-context changes by
  discarding affected in-flight phases and resetting toggles only when their
  intent policy changes or no active enabled route remains; reconciliation is
  silent and does not synthesize canceled events.
