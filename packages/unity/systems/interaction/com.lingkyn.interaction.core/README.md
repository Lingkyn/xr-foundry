# Lingkyn Interaction Core

Status: incubating implementation; not catalog-admitted or released.

Engine-light semantic intents, contexts, routes, advisory binding suggestions, typed source signals, capability-aware deterministic routing, policy snapshots, diagnostics, and handler outcomes.

## Scope

This package owns the engine-neutral interaction routing substrate described in `docs/standards/interaction/architecture-contract.md`. It does not import Unity, Input System, XRI, OpenXR, renderers, or device APIs.

## Validation

Run the editor tests from a Unity 6000.x project that references this package:

- `Tests/Editor/InteractionCoreContractTests.cs` — focused contract/unit coverage
- `Tests/Editor/InteractionCoreAcceptanceEvidenceTests.cs` — verification-contract acceptance evidence with independent assertions for events, dispatches, diagnostics, handler calls, ingress sequences, and active-context snapshots

Local `dotnet test` against the runtime and both test assemblies is also supported for CI-less validation.
