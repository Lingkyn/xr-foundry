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
Unity authoring is also candidate. UGUI `0.1.1` is candidate after the withdrawn
`0.1.0` structure-only verdict was corrected with functional shipped prefabs,
immutable install, sample, consumer-owned variant upgrade/rollback, and review evidence.
XR `0.1.0` is implemented at incubating maturity with a provider-neutral world-space
prefab, strict input validation, local tracked-ray/poke tests, and imported sample
evidence. Immutable Git-consumer, Android, and Pico gates remain, so candidate lower
layers are not evidence that the complete family or XR experience is finished.

| Package | Version | Maturity | Earliest unsatisfied gate |
| --- | --- | --- | --- |
| `com.lingkyn.inventory.core` | `0.1.0` | `candidate` | `none` |
| `com.lingkyn.inventory.unity` | `0.1.0` | `candidate` | `none` |
| `com.lingkyn.inventory.ugui` | `0.1.1` | `candidate` | `none` |
| `com.lingkyn.inventory.xr` | `0.1.0` | `incubating` | `immutable_git_url_clean_consumer` |

## Reference-library evolution

- Add coverage, extension seams, failure cases, and migration evidence to each
  reference entry as packages mature.
- Promote the optional XR layer only after immutable-consumer, Android, and named-device evidence without weakening the candidate lower-layer boundaries.
- Add reusable tools, templates, and validation contracts when they have a real
  consumer and evidence.
- Consider Unreal Engine and Godot collections only when working implementations,
  maintainers, tests, samples, and engine-specific validation exist.
