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
admitted at `candidate` maturity after persistence/migration, typed instance-state,
public API, immutable install, upgrade, rollback, and clean-consumer gates passed.
Unity authoring is also candidate. UGUI `0.1.1` is an incubating correction after
the `0.1.0` structure-only prefab verdict was withdrawn; its functional local tests
pass, while immutable install, sample, upgrade/rollback, and review remain gated.
XR is still not implemented. Candidate lower layers are not evidence that the
complete family or XR experience is finished.

| Package | Version | Maturity | Earliest unsatisfied gate |
| --- | --- | --- | --- |
| `com.lingkyn.inventory.core` | `0.1.0` | `candidate` | `none` |
| `com.lingkyn.inventory.unity` | `0.1.0` | `candidate` | `none` |
| `com.lingkyn.inventory.ugui` | `0.1.1` | `incubating` | `immutable_git_url_functional_consumer` |

## Reference-library evolution

- Add coverage, extension seams, failure cases, and migration evidence to each
  reference entry as packages mature.
- Build UGUI and optional XR layers without weakening the candidate Core and authoring boundaries.
- Add reusable tools, templates, and validation contracts when they have a real
  consumer and evidence.
- Consider Unreal Engine and Godot collections only when working implementations,
  maintainers, tests, samples, and engine-specific validation exist.
