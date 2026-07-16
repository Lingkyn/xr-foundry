# Roadmap

XR Foundry grows by proven artifact classes rather than by creating empty engine
folders. Unity is the implemented foundation. Agent guidance and machine-readable
reference selection are part of the product, while model-specific adapters remain
thin.

## Foundry production line

Foundry V1 registers the first nine implemented Unity packages as one incubating
batch and publishes a dry-run-first package blueprint/scaffolder. New systems enter
through the public source-gate queue; no package directory or package ID is created
before admission. See [`docs/foundry`](docs/foundry/README.md).

Persistence, Settings/Accessibility, and Semantic Interaction have completed
their first independently validated Core and Unity checkpoints. Their next gates
are public API compatibility review, one release upgrade/rollback exercise, and
any separately scoped live runtime or named-device evidence required by a claim.

## Candidate gate

- Repository validator and Python contract tests pass.
- Every promoted package resolves and compiles in a fresh consumer matching its
  declared compatibility profile.
- EditMode package tests pass.
- Installation and migration evidence names an immutable commit.

## Stable gate

- Public API/compatibility policy and migration path are proven across a release.
- Documentation and samples match the shipped API.
- XR claims have current real-device evidence where required.

## Inventory package family

Inventory is an incubating system standard with a positive-external-source-only
manifest, package-family architecture, nested presentation composition, and a
verification contract. Core, Unity authoring, Presentation, UGUI, UI Toolkit, XR
UGUI, and XR UI Toolkit form the implemented renderer-neutral graph. Exact
automated profiles exist at their recorded evidence commits. The first-batch
release commit still needs its own immutable Git-consumer evidence before any layer
can advance to later promotion, renderer, Android, or named-device gates; evidence
from an earlier package or dependency tuple is not inherited.

Unity `6000.3.19f1` is the first automated implementation profile, not the
repository's generation limit. Another Unity, UI, XRI, or future engine tuple
begins as raw-material regeneration and earns its own profile only after equivalent
validation.

| Package | Version | Maturity | Earliest unsatisfied gate |
| --- | --- | --- | --- |
| `com.lingkyn.inventory.core` | `0.1.1` | `incubating` | `core_atomic_mutation_tests` |
| `com.lingkyn.inventory.unity` | `0.1.1` | `incubating` | `local_clean_consumer_editmode_tests` |
| `com.lingkyn.inventory.presentation` | `0.1.0` | `incubating` | `presenter_unit_tests` |
| `com.lingkyn.inventory.ugui` | `0.2.0` | `incubating` | `required_visible_state_replay` |
| `com.lingkyn.inventory.uitoolkit` | `0.1.0` | `incubating` | `semantic_state_and_intent_tests` |
| `com.lingkyn.inventory.xr.ugui` | `0.1.0` | `incubating` | `local_clean_consumer` |
| `com.lingkyn.inventory.xr.uitoolkit` | `0.1.0` | `incubating` | `local_clean_consumer` |

## Reference-library evolution

- Add coverage, extension seams, failure cases, and migration evidence to each
  reference entry as packages mature.
- Promote each optional XR renderer composition only after its own Android and
  named-device evidence without weakening the lower-layer boundaries.
- Add reusable tools, templates, and validation contracts when they have a real
  consumer and evidence.
- Consider Unreal Engine and Godot collections only when working implementations,
  maintainers, tests, samples, and engine-specific validation exist.
