# Inventory Package Family Standard

Status: **incubating standard**
Standard version: **0.1.0**
Implementation status: **Core, Unity authoring, renderer-neutral Presentation,
UGUI, UI Toolkit, XR UGUI, and XR UI Toolkit are implemented as incubating work.
Fresh immutable Git-consumer validation for the current package tree is pending.
Renderer-specific acceptance, Android, and named-device gates remain independent
and cannot inherit evidence from another version or composition.**

This standard defines the evidence and architecture required to implement and
promote the Inventory package family in XR Foundry. It is intentionally independent
of any consuming game. The Core package has entered the implementation phase, but
the standard remains incubating until the complete promotion contract is proven.

## Source boundary

The standard is derived only from admitted positive external sources listed in
[`source-manifest.json`](source-manifest.json). Consumer projects, course projects,
internal prototypes, and screened-out candidates are not generation inputs. They
may be used later as independent consumers or compatibility tests, after the
standard has been frozen for an implementation slice.

Popularity can qualify a repository for review, but cannot admit it. A source must
also have clear provenance, a compatible license or public-documentation boundary,
current relevance, and positive evidence for the capability it informs.

No community Inventory repository reviewed in the first round passed all gates for
use as a code seed. The first Core implementation was therefore independently
authored from the public contracts in this standard. Commercial products are
coverage benchmarks only; their proprietary code and assets are not inputs.

## Evidence hierarchy

1. Engine and package-manager documentation defines normative compatibility and
   packaging behavior.
2. Engine-owner samples and published architecture guidance define proven patterns.
3. Mature commercial Inventory systems define production coverage and extension
   expectations through their public documentation.
4. Strongly adopted open source may become a code seed only after license,
   maintenance, package shape, tests, migration, and independent-consumer review.

Source agreement is required before a pattern becomes a core requirement. A
single product-specific feature becomes an optional seam, not a mandatory core
abstraction.

## Required capability coverage

| Area | Minimum standard |
| --- | --- |
| Identity | Stable definition IDs and distinct runtime instance IDs |
| Ownership | Multiple inventories and containers without global-singleton coupling |
| Quantity | Explicit stacking, splitting, merging, maximum-stack, and unique-item rules |
| Mutation | Validated, atomic add/remove/move/swap/transfer operations with structured results |
| Constraints | Composable capacity, category, slot, shape, ownership, and custom policies |
| Extensibility | Optional item capabilities/fragments without subclass explosion |
| Persistence | Versioned snapshots, adapter boundary, migration, and unresolved-definition handling |
| Presentation | Read-only view state and commands; UI cannot mutate storage directly |
| Composition | Nested panel, grid, slot, item-view, details, and action prefabs |
| Equipment | Optional integration boundary; inventory and equipment remain separate lifecycles |
| Online | Optional authority/concurrency adapter; local core is not coupled to a service SDK |
| XR | Optional world-space interaction package; non-XR consumers do not depend on XR packages |
| Operations | Diagnostics, deterministic tests, samples, migration notes, and rollback guidance |

## Package family

The intended public family is defined in
[`architecture-contract.md`](architecture-contract.md). The generic core remains
usable without XR. XR Foundry supplies an optional XR presentation and interaction
profile rather than redefining Inventory semantics for a headset.

## Admission sequence

```text
positive external evidence
-> frozen architecture contract
-> independently authored core
-> deterministic and stateful tests
-> Unity authoring and presentation adapters
-> clean consumer install
-> XR adapter and world-space sample
-> real-device evidence for XR claims
```

Until these steps pass, the Inventory family is a standard and implementation
candidate, not a stable package.

## Current promotion state

| Layer | Current state | Earliest unsatisfied gate |
| --- | --- | --- |
| `com.lingkyn.inventory.core` | Incubating `0.1.1`; source and architecture are present | Run current atomic/persistence/state/API tests, then immutable consumer and review gates |
| `com.lingkyn.inventory.unity` | Incubating `0.1.1`; authoring, conversion, diagnostics, and immutability surfaces are present | Run current clean-consumer EditMode tests, then immutable consumer and review gates |
| `com.lingkyn.inventory.presentation` | Incubating `0.1.0`; renderer-neutral contract is present | Run presenter/compile/consumer tests, then immutable consumer and API review |
| `com.lingkyn.inventory.ugui` | Incubating `0.2.0`; neutral nested renderer composition is present | Run state/raycast/local tests, then non-XR immutable consumer and review |
| `com.lingkyn.inventory.uitoolkit` | Incubating `0.1.0`; peer VisualElement/UXML/USS route and sample are present | Run semantic/local tests, then immutable consumer and renderer review |
| `com.lingkyn.inventory.xr.ugui` | Incubating `0.1.0`; explicit UGUI/XRI composition and test routes are present | Run local/immutable/XRI tests, then Android/device gates |
| `com.lingkyn.inventory.xr.uitoolkit` | Incubating `0.1.0`; explicit UI Toolkit/XRI composition and test routes are present | Run local/immutable/XRI tests, then Android/device gates |

These rows are claim boundaries, not a percentage-complete estimate. A lower layer
may reach candidate maturity without promoting a higher layer or the whole family.

## Controlled artifacts

- [`source-manifest.json`](source-manifest.json): admitted positive sources only.
- [`coverage-matrix.md`](coverage-matrix.md): convergence from sources to requirements.
- [`inventory-standard.json`](inventory-standard.json): machine-readable package and capability contract.
- [`architecture-contract.md`](architecture-contract.md): domain, package, UI, persistence, and XR boundaries.
- [`verification-contract.md`](verification-contract.md): implementation and promotion evidence.
- [`core-api-contract.md`](core-api-contract.md): prerelease compatibility and deprecation policy.
- [`core-api-baseline.json`](core-api-baseline.json): machine-checked public type baseline.
- [`renderer-neutral-architecture.md`](renderer-neutral-architecture.md):
  renderer-neutral presentation, renderer-explicit XR composition, Git pinning,
  and evidence boundaries.
