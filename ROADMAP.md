# Roadmap

XR Foundry grows by proven artifact classes rather than by creating empty engine
folders. Unity is the implemented foundation. Agent guidance and machine-readable
reference selection are part of the product, while model-specific adapters remain
thin.

The maintained [public work map](docs/contributing/public-work-map.md) is the
current outcome-to-task projection. This Roadmap owns broad sequence; GitHub
milestones group outcome batches; the Project owns current scheduling; registered
task contracts own checkpoint execution. A community proposal can add a new route
at any time, but it passes the same source, admission, authority, and evidence gates
as maintainer-planned work.

## Outcome milestones

| Order | Outcome | Current public work | Completion boundary |
| --- | --- | --- | --- |
| M1 | Foundry Commons V1 | [Curated Commons #64](https://github.com/Lingkyn/xr-foundry/issues/64) and [standard entry #62](https://github.com/Lingkyn/xr-foundry/issues/62) | Plans and proposals reach validated, resumable checkpoints through one public map; every admitted family has a complete standard entry. |
| M2 | Package Hardening V1 | [Package Hardening #69](https://github.com/Lingkyn/xr-foundry/issues/69) | Inventory, Persistence, Settings, and Interaction independently close declared compatibility or migration gates; incomplete siblings remain unchanged. |
| M3 | Device Lab Evidence V1 | [Device Lab Evidence #73](https://github.com/Lingkyn/xr-foundry/issues/73) | Repo-only research, immutable Android artifact attestation, and named-device receipts are separately executable and independently integrated. |
| M4 | Localization Admission V1 | [Localization Admission #74](https://github.com/Lingkyn/xr-foundry/issues/74) | Positive evidence produces an admit, revise, or reject decision before any package identity or implementation exists. |

M1 establishes the common work surface, but it does not block independent Ready
checkpoints whose authority already exists. M2 family checkpoints are independent.
M3 separates research, build provenance, hardware execution, and integration so an
Agent without Unity or a headset can still contribute valid work. M4 remains a
source gate until a maintainer admits the architecture.

## Planned but unfinished checkpoint pool

| Work family | Ready without Unity | Requires Unity | Requires named device | Integration waits for |
| --- | --- | --- | --- | --- |
| Foundry Commons | Public work map; standard-entry schema | None | None | Schema, projection, validator, and maintainer review |
| Package Hardening | API and compatibility review portions | Clean consumer, package tests, upgrade and rollback evidence | Only later claim-specific gates | Independently accepted family evidence |
| Device Lab | Quest/Vision Pro research; artifact contract and verifier | Android artifact production is separately scheduled | PICO UGUI and UI Toolkit receipts after artifact admission | Research, attestation, exact receipts, and privacy review |
| Localization | Standards research, architecture, and admission | Only after admission | None in the admission milestone | Source gate and maintainer decision |

The pool is intentionally decomposed. If a contributor or Agent stops, every
completed checkpoint remains usable and the next contributor resumes from the last
public anchor and continuation receipt. A stronger later analysis may revise an
earlier plan through public evidence; prior Agent reasoning is informative input,
not binding authority.

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
