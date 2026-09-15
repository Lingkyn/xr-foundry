# Roadmap

XR Foundry grows by proven artifact classes rather than by creating empty engine
folders. Unity is the implemented foundation. Agent guidance and machine-readable
reference selection are part of the product, while model-specific adapters remain
thin.

## Foundry production line

Foundry V1 publishes a dry-run-first package blueprint/scaffolder and an immutable
batch registry. Two incubating batches are released: `unity-first-batch` registers
the nine foundation and Inventory packages, and `unity-next-systems` registers the
six Persistence, Settings, and Interaction packages, so the 15 live Unity packages
are each covered exactly once. A batch is a discovery and install surface, not a
maturity promotion. New systems enter through the public source-gate queue, where
Localization is the current proposal; no package directory or package ID is
created before admission. See [`docs/foundry`](docs/foundry/README.md) and
[`docs/releases`](docs/releases/).

Persistence, Settings/Accessibility, and Semantic Interaction have completed
their first independently validated Core and Unity checkpoints. Their next gates
are public API compatibility review, one release upgrade/rollback exercise, and
any separately scoped live runtime or named-device evidence required by a claim.

## Execution order after Inventory

The library grows one evidence gate at a time, in this order, and an Agent under
an operating mandate works the first unblocked item without waiting to be asked:

1. **Editor evidence for the authored tests.** Every Core and adapter test named in
   the family coverage maps (`docs/standards/*/coverage-map*.json`) runs through
   `scripts/run_unity_gates.py` or the `unity-consumer-tests` workflow, and the
   compatibility profiles move to the current commit. Needs a Unity Editor or a
   Unity license secret; nothing else in this list is credible before it.
2. **Process-decided merges.** The advisory `merge-readiness` verdict runs on every
   pull request and already computes process-merge eligibility; RFC 0007 proposes
   the rule under which a routine change on a mandated branch merges by GitHub
   auto-merge, after its review window and two owner-only repository settings.
3. **Second checkpoints for Persistence, Settings, and Interaction.** Public API
   compatibility review, one release upgrade/rollback exercise per family, and the
   open dispositions in the lessons register.
4. **Inventory renderer and device gates.** Land the UGUI skin seam (#81) without
   an unverified version bump, then the Device Lab plan
   `inventory-world-space-ui-v1` on one named headset.
5. **Localization** through the source-gate queue (`NEXT-LOCALIZATION`): cross-project
   admission, positive-source manifest, then a Core and Unity blueprint. No package
   directory exists before admission.
6. **Whole-composition evidence.** A green reference-system run, a player build, and
   named-device evidence lift `runtime_ready` for the XFCM composition.

Families beyond Localization enter only through the queue with their own
admission record; the roadmap does not pre-announce them.

## Composition

XFCM v0.2 gives every live package a colocated component manifest and resolves the
Unity reference composition to a deterministic 13-component lock with three bound
cross-family adapter sources. The bounded consumer experiment covers the eight
packages at the binding endpoints in one Editor tuple. The composition keeps
`runtime_ready: false` until whole-composition Unity evidence, a player build,
and named-device evidence exist; each of those is a separate gate. See
[`docs/architecture/component-composition-model.md`](docs/architecture/component-composition-model.md).

## Continuous Unity evidence

The `unity-consumer-tests` workflow materializes the repository-owned reference
consumer, derives the exact test-case count of every test assembly from source, runs
each assembly in its own Unity process, and accepts a result only when the
repository's verifier proves that exact assembly passed completely. It activates
when a Unity license secret is configured and skips itself on fork pull requests.
Until its first green run on `main`, every `*_tests` and `local_clean_consumer`
gate remains workstation evidence anchored at a recorded commit. A green run is
Editor evidence for one tuple, never a player, controller, or headset claim.

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
