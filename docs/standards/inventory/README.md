# Inventory Package Family Standard

Status: **incubating standard**
Standard version: **0.1.0**
Implementation status: **Core admitted at incubating maturity; Unity authoring,
UGUI, and XR layers pending**

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
| `com.lingkyn.inventory.core` | Implemented, incubating; transactional persistence round-trip/migration and clean consumers pass | Typed mutable instance state and public API compatibility across a release |
| `com.lingkyn.inventory.unity` | Not implemented | ScriptableObject authoring, catalog conversion, validation, and asset-immutability tests |
| `com.lingkyn.inventory.ugui` | Not implemented | Presenter boundary, nested-prefab composition, state coverage, and PlayMode tests |
| `com.lingkyn.inventory.xr` | Not implemented | World-space/XRI adapter, automated configuration tests, and real Pico evidence |

These rows are claim boundaries, not a percentage-complete estimate. A lower layer
may reach candidate maturity without promoting a higher layer or the whole family.

## Controlled artifacts

- [`source-manifest.json`](source-manifest.json): admitted positive sources only.
- [`coverage-matrix.md`](coverage-matrix.md): convergence from sources to requirements.
- [`inventory-standard.json`](inventory-standard.json): machine-readable package and capability contract.
- [`architecture-contract.md`](architecture-contract.md): domain, package, UI, persistence, and XR boundaries.
- [`verification-contract.md`](verification-contract.md): implementation and promotion evidence.
