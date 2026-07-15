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
canonical nested repository path still requires a fresh immutable Git consumer,
so Core and Unity authoring remain incubating until that exact path is proven.
Presentation, UGUI, UI Toolkit, XR UGUI, and XR UI Toolkit form the canonical
renderer-neutral graph. Android and named-device gates remain independent for each
XR renderer, so lower-layer evidence is not evidence that the complete family or
XR experience is finished.

| Package | Version | Maturity | Earliest unsatisfied gate |
| --- | --- | --- | --- |
| `com.lingkyn.inventory.core` | `0.1.0` | `incubating` | `immutable_nested_path_git_consumer` |
| `com.lingkyn.inventory.unity` | `0.1.0` | `incubating` | `immutable_nested_path_git_consumer` |
| `com.lingkyn.inventory.presentation` | `0.1.0` | `incubating` | `local_clean_consumer` |
| `com.lingkyn.inventory.ugui` | `0.2.0` | `incubating` | `local_clean_consumer` |
| `com.lingkyn.inventory.uitoolkit` | `0.1.0` | `incubating` | `local_clean_consumer` |
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
