# Lingkyn Interaction Core

Status: incubating implementation; not catalog-admitted or released.

Engine-light semantic intents, contexts, routes, advisory binding suggestions,
typed observations, capability-aware deterministic routing, immutable policy and
routing-state snapshots, diagnostics, and handler outcomes.

## Scope

This package owns the engine-neutral substrate described in
`docs/standards/interaction/architecture-contract.md`. It imports no Unity,
Input System, XRI, OpenXR, renderer, vendor, scene, or device API.

`RouteId` is globally unique. `ObservationSequence` groups alternative route
candidates for one physical observation; separate observations are never
collapsed. Cross-frame phase and toggle facts enter as an immutable
`InteractionRoutingState` and leave as `InteractionRoutingResult.NextState`.

## Validation

`Tests/Editor/InteractionCoreTests.cs` covers immutable routing state,
observation-scoped conflicts, policy behavior, capability/modality boundaries,
diagnostics, and fail-closed conversion. Published compatibility evidence must
come from an exact Unity consumer pinned to an immutable Git revision.
