# Canonical nested Git consumer validation — 2026-07-15

Status: **passed for the automated package boundary**.

## Immutable source

- Repository: `https://github.com/Lingkyn/xr-foundry`
- Revision: `b3d4b8dfd3ae9f6025026bc6737eb10cacbd894f`
- Consumer: a clean Unity project outside this repository and outside any game
  project
- Resolution rule: every custom package was listed directly with its canonical
  `?path=/packages/unity/...` selector and the same full revision

Unity Package Manager resolved all nine custom packages with `source=git` and
`hash=b3d4b8dfd3ae9f6025026bc6737eb10cacbd894f`:

| Collection | Package |
| --- | --- |
| Foundations | `com.lingkyn.project-initializer` |
| Foundations | `com.lingkyn.xr-baseline` |
| Inventory | `com.lingkyn.inventory.core` |
| Inventory | `com.lingkyn.inventory.unity` |
| Inventory | `com.lingkyn.inventory.presentation` |
| Inventory | `com.lingkyn.inventory.ugui` |
| Inventory | `com.lingkyn.inventory.uitoolkit` |
| Inventory XR | `com.lingkyn.inventory.xr.ugui` |
| Inventory XR | `com.lingkyn.inventory.xr.uitoolkit` |

No root-level package path, redirect, duplicate package directory, symlink, or
compatibility shim was used.

## Environment

- Unity: `6000.3.19f1`
- XR Interaction Toolkit: `3.5.1`
- Input System: `1.19.0`
- UGUI: `2.0.0`
- XR Plug-in Management: `4.5.3`
- OpenXR: `1.16.0`

## Results

| Check | Result |
| --- | --- |
| Resolve all canonical nested Git selectors | Pass |
| Verify all custom lock entries use the exact revision | Pass |
| Compile runtime, editor, sample, and test assemblies | Pass; batch-mode exit `0`, no C# compiler errors |
| EditMode | Pass — 49 total, 49 passed, 0 failed, 0 skipped |
| PlayMode | Pass — 8 total, 8 passed, 0 failed, 0 skipped |
| Renderer routes | Both UGUI and UI Toolkit assemblies and automated tests passed |
| XR routes | Both renderer-explicit XR assemblies and automated routing/configuration tests passed |

Evidence digests (SHA-256):

| Evidence | Digest |
| --- | --- |
| Compile log | `0738d98d8c7ecefc1fd34c8e072de8e9f7160cbc1da2c2cad1b241646867bd40` |
| EditMode log | `32e172a89adfc115a6b326f51f76ae81f1659a1de2c2166cdfec6e1a1c312273` |
| EditMode result | `4d09266c21679472e2ed42c37118c17a5c99d8b177689f668d98bf51a70f00fb` |
| PlayMode log | `8518df78a71d44eac4e876c4d85d2d892369adeee4629858f55ad96594820f57` |
| PlayMode result | `1e59612d02e3680b3807434ab2978ee64438fa62fe9c88a747815c59f2dec112` |
| Consumer manifest | `d5dcf172cfc31b3713bdb1fadc4a5cc1a657e986095fe99398bedf7d465775f1` |
| Consumer lock file | `5bc29b2f9eaba93ef02670986c74bfd1ddae87e9e10cb42e370c3d89f0c1eadf` |

## Claim boundary

This receipt proves public Git resolution from the canonical nested repository
layout, compilation, and the named automated tests at the exact revision. It does
not prove Android build/install/open, binocular readability, world anchoring,
controller behavior on a physical headset, visual scale or angle, occlusion,
comfort, PICO behavior, Quest behavior, or visionOS behavior.

Those device claims remain `not_tested` and require a renderer-specific completed
Device Lab receipt. Deferred human testing does not invalidate this automated
receipt, but it continues to block every corresponding device or usability claim.
