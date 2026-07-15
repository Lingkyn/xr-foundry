# Roadmap

XR Foundry grows by proven artifact classes rather than by creating empty engine
folders. Unity is the implemented foundation. Agent guidance and machine-readable
reference selection are part of the product, while model-specific adapters remain
thin.

## Candidate gate

- Repository validator and Python contract tests pass.
- Every promoted package resolves and compiles in a fresh Unity 6000.3 project.
- EditMode package tests pass.
- Installation and migration evidence names an immutable commit.

## Stable gate

- Public API/compatibility policy and migration path are proven across a release.
- Documentation and samples match the shipped API.
- XR claims have current real-device evidence where required.

## Inventory package family

Inventory is an incubating system standard with a positive-external-source-only
manifest, package-family architecture, nested presentation composition, and a
verification contract. The independently authored Core `0.1.0` implementation is
implemented with source, API, persistence, and local-consumer evidence. The
canonical nested repository layout now has a fresh immutable Git consumer at
`b3d4b8dfd3ae9f6025026bc6737eb10cacbd894f`: all nine packages resolved from
GitHub, compiled, and passed 49 EditMode plus 8 PlayMode tests. Presentation,
UGUI, UI Toolkit, XR UGUI, and XR UI Toolkit form the canonical renderer-neutral
graph. Candidate review, renderer acceptance, Android, and named-device gates
remain independent; lower-layer evidence is not evidence that the complete family
or XR experience is finished.

| Package | Version | Maturity | Earliest unsatisfied gate |
| --- | --- | --- | --- |
| `com.lingkyn.inventory.core` | `0.1.0` | `incubating` | `candidate_release_review` |
| `com.lingkyn.inventory.unity` | `0.1.0` | `incubating` | `candidate_release_review` |
| `com.lingkyn.inventory.presentation` | `0.1.0` | `incubating` | `public_api_review` |
| `com.lingkyn.inventory.ugui` | `0.2.0` | `incubating` | `candidate_release_review` |
| `com.lingkyn.inventory.uitoolkit` | `0.1.0` | `incubating` | `renderer_acceptance` |
| `com.lingkyn.inventory.xr.ugui` | `0.1.0` | `incubating` | `android_build_install_open` |
| `com.lingkyn.inventory.xr.uitoolkit` | `0.1.0` | `incubating` | `android_build_install_open` |

## Reference-library evolution

- Add coverage, extension seams, failure cases, and migration evidence to each
  reference entry as packages mature.
- Promote each optional XR renderer composition only after its own Android and
  named-device evidence without weakening the lower-layer boundaries.
- Add reusable tools, templates, and validation contracts when they have a real
  consumer and evidence.
- Consider Unreal Engine and Godot collections only when working implementations,
  maintainers, tests, samples, and engine-specific validation exist.
