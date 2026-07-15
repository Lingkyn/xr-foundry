# Inventory Unity authoring immutable Git validation — 2026-07-15

## Verdict

`com.lingkyn.inventory.unity@0.1.0` passes the Unity authoring candidate gate. Both
the Core dependency and authoring package resolved from the same immutable Git
revision and passed their EditMode suites in an independent consumer.

This verdict covers authoring assets, validation, inspectors, and conversion only.
It makes no UGUI, prefab, XR, controller, headset, comfort, or Pico claim.

## Immutable revision

- Candidate commit: `b8b49235a66d8a388a123739d3fc7586bd7cf211`
- Authoring package tree: `62b4f7b04e7b5332b6f8175acb7ee9d5c7213682`
- Core and authoring packages used the same pinned revision and explicit package
  subdirectory selectors.

Final evidence-only repository changes do not modify the authoring package tree.

## Consumer and evidence

- Unity: `6000.3.19f1`
- Test platform: EditMode, batch mode
- Consumer: the existing persistent clean Unity smoke project outside this repository
- Unity test run: 27 passed, 0 failed
- Inventory Unity authoring tests: 4 passed, 0 failed
- Inventory Core tests: 21 passed, 0 failed
- Result SHA-256:
  `548815758DBC0A03F3BDD10D2BC6F60F4D4FAEE4592C76B9E9F7F9C1B73A18BF`
- Log SHA-256:
  `7D311549499D7C176CB40D70A38D685EFF1D2C1FD73490344A9B1F14D6E5B67C`

The package lock recorded both package names as Git sources at the expected commit.
The tested authoring behaviors include deterministic conversion, actionable invalid
catalog diagnostics, explicit stable identity across asset rename/move, and proof
that aggregate mutations do not write back into ScriptableObject assets.

## Claim boundary

This receipt closes Issue #7's authoring gates only. It cannot be used as evidence
for presenter boundaries, nested prefabs, interaction states, world-space UI, or
real-device usability.
