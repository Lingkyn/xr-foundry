# RFC 0005: XR Foundry Component and Composition Model

Status: **Accepted for bounded local implementation by the maintainer**

Public deliberation: **not opened by this local implementation**

Related decisions:

- [RFC 0003: Foundry V1 production line](0003-foundry-production-line.md)
- [RFC 0004: Progressive governance](0004-progressive-governance.md)
- [Version-adaptive reference model](../architecture/version-adaptive-reference-model.md)

## Summary

XR Foundry will use a thin, repository-owned component and composition model so
its packages remain independently reusable while forming a coherent XR system.
Every package declares versioned capabilities and requirements. A composition
selects components and mutually exclusive variants, resolves them deterministically,
and commits an exact lock. Repository validation rejects missing, incompatible,
ambiguous, cyclic, drifted, or falsely evidenced compositions.

The model is named the **XR Foundry Component and Composition Model (XFCM)**. V0.2
is a local architectural contract, not a claim to be a universal industry protocol.

## Problem

Independent Unity packages are valuable only if consumers can answer system-level
questions reliably:

- Which packages belong together?
- Which capability does each package provide or require?
- Which implementations are alternatives rather than cumulative dependencies?
- In what order are components configured, started, and stopped?
- Which cross-domain adapters remain the consumer's responsibility?
- What exact set was validated, and at what evidence strength?

Package manifests alone describe installation dependencies but not semantic roles,
variant choice, cross-family topology, lifecycle, or whole-system evidence. A
single global event bus would hide those gaps rather than solve them.

## Decision

Adopt the following architecture:

1. Keep the runtime data plane strongly typed and in process.
2. Add one colocated `foundry.component.json` for every live package.
3. Register stable capability IDs and exact contract versions centrally.
4. Declare mutually exclusive implementations as catalog slots.
5. Make a consumer-owned composition root select fixed components and slot choices.
6. Resolve the graph statically and commit a deterministic content-bound lock.
7. Treat cross-family bindings as explicit adapter boundaries.
8. Separate structural, compile, runtime, and device evidence.
9. Reserve MCP for an optional external composition/control adapter.

The normative details are in the
[XFCM v0.2 architecture contract](../architecture/component-composition-model.md).

## Phase-one deliverables (v0.1 baseline)

This implementation introduces:

- component, component-catalog, capability-registry, composition, and lock schemas;
- a component manifest for all 15 current Unity packages;
- a capability registry that agrees with those manifests;
- explicit Inventory renderer and XR-surface slots;
- a Unity reference composition using the UGUI route;
- three visible pending cross-family adapter boundaries, later implemented by the
  v0.2 slice;
- a deterministic resolver and committed lock; and
- repository and negative contract tests.

Phase one does not add account access, remote settings, wallets, a treasury,
tokens, smart contracts, on-chain execution, a package registry, or a networked
runtime service.

## V0.2 implementation update

The bounded v0.2 slice turns the three declared boundaries into consumer-owned,
strongly typed C# adapters. The manifest records each adapter's composition-local
source path and assembly. The deterministic lock records the canonical
repository-relative source path and SHA-256, and becomes stale when an adapter
source changes. The resolver rejects missing, escaping, linked, non-regular, or
non-canonical sources and unsafe lock paths. It also requires the source to belong
to the nearest unique regular Unity `.asmdef` whose name equals the declared
assembly; `.asmref` ownership currently fails closed. Authority JSON rejects
duplicate keys at any depth, and v0.2 versions follow SemVer 2.0 numeric
prerelease rules. V0.1 manifests and locks remain supported for their original
pending-binding semantics. This local safety contract assumes serialized lock
generation in a non-adversarial worktree; it does not yet claim cross-process
compare-and-swap or same-privilege TOCTOU resistance.

A clean eight-package endpoint consumer exercises the adapters without claiming
that the full 13-component reference composition ran. Its local experiment and
the defects found during repeated execution are recorded in the
[double-loop result receipt](../validation/experiments/2026-09-04-xag-xfcm-01-double-loop-result.md).

## Runtime and MCP

MCP has useful host/client/server composition and capability-negotiation ideas, but
it is optimized for external tool and context exchange. It must not become the
hot-path protocol between Unity systems. In XFCM:

- C# interfaces, immutable values, semantic intents, and domain events carry
  runtime behavior;
- JSON manifests and locks carry composition metadata; and
- an optional future MCP server may query or drive the resolver under the same
  validation and authority boundaries.

This avoids coupling the product runtime to an Agent protocol while preserving a
clean automation surface.

## Alternatives considered

### Keep only Unity package dependencies

Rejected. They cannot express semantic capabilities, variant slots, lifecycle,
cross-family bindings, or whole-composition evidence.

### One global event bus

Rejected. String topics and unconstrained payloads create hidden dependencies,
weaken refactoring, and move contract failures into runtime. Local typed events
remain appropriate inside a bounded domain.

### Use MCP everywhere

Rejected for the runtime data plane. Serialization, process boundaries, discovery,
and host semantics are unnecessary for normal in-process XR behavior. MCP remains
eligible at the external control boundary.

### Build a universal cross-engine ABI now

Deferred. Unity is the only implemented engine collection. Cross-engine semantics
should be extracted after a second real adapter exists, not predicted through empty
abstractions.

### Put every renderer in one composition

Rejected. UGUI and UI Toolkit are peer implementations. Slots make the entire
product line discoverable while keeping any concrete runtime choice coherent.

## Compatibility and migration

XFCM model version `0.2.0` is incubating. Additive schema fields require an explicit
model update and validator support. Breaking capability behavior creates a new
capability major version. Package SemVer does not silently change a capability
contract version. The validator dispatches exact v0.1 and v0.2 schema/model pairs;
unknown or mismatched pairs fail closed.

Existing package APIs are not changed by this RFC. The first lock records current
package versions and manifest hashes. A manifest, registry, selection, or
composition change makes the lock stale and blocks validation until reviewed and
regenerated.

## Evidence and non-claims

The current reference lock proves that 13 selected components, including one
choice from each current variant slot, have a complete acyclic capability graph.
It also proves that all three declared bindings point to exact adapter source
bytes. It deliberately retains `runtime_ready: false`: source-bound adapters and
an eight-package endpoint test do not establish execution of the full selection.

This RFC does not claim:

- a Unity import/compile or player build for the complete locked set;
- runtime integration of components outside the eight-package endpoint consumer;
- frame-time, memory, comfort, or usability performance;
- any OpenXR runtime, controller, headset, or device result; or
- compatibility for Unreal Engine or Godot.

Those claims require subsequent revision-bound evidence.

## Governance fit

XFCM is compatible with the DAO-ready Open Commons direction because it separates
shared technical contracts from implementation ownership. Working groups can own
capability areas and conformance evidence without fragmenting the system or gaining
automatic repository authority. Changes remain normal reviewed repository changes;
the component model does not create governance roles, votes, permissions, or an
autonomous executor.
