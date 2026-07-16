# Canonical nested Git consumer validation

Status: **passed for Windows Editor automated claims at commit
`a25bbc7c9855ae4f094a58e43d8ab5ffdf37a7bf`**.

## Evidence boundary

Nine independent Unity consumer graphs resolved the canonical nested Git package
selectors at the same immutable public revision. A single disposable Unity project
was reused sequentially, but every profile saved the manifest and Unity-generated
lock from its own graph before the next graph was loaded. Each receipt binds those
files, the exact reachable dependency versions, structured compile evidence, and
the NUnit result files by SHA-256.

`com.unity.test-framework@1.6.0` is an explicit dependency of each automated test
harness. It is not being added to package manifests and is not claimed as a runtime
consumer requirement. A first Core probe that exposed package tests without this
harness dependency correctly failed compilation with unresolved NUnit `Test` types;
that failed probe was not promoted or reused as passing evidence. The corrected,
fully recorded graph was then compiled and tested again.

## Results

| Compatibility profile | Compile | EditMode | PlayMode |
| --- | --- | ---: | ---: |
| `unity-6000.3-project-initializer-windows-editor` | pass | 1/1 | not applicable |
| `unity-6000.3-xr-baseline-openxr-windows-editor` | pass | 1/1 | not applicable |
| `unity-6000.3-inventory-core-windows-editor` | pass | 21/21 | not applicable |
| `unity-6000.3-inventory-authoring-windows-editor` | pass | 25/25 | not applicable |
| `unity-6000.3-inventory-presentation-windows-editor` | pass | 24/24 | not applicable |
| `unity-6000.3-inventory-ugui-non-xr-windows-editor` | pass | 32/32 | 3/3 |
| `unity-6000.3-inventory-uitoolkit-non-xr-windows-editor` | pass | 26/26 | 1/1 |
| `unity-6000.3-inventory-xr-ugui-openxr-windows-editor` | pass | 36/36 | 6/6 |
| `unity-6000.3-inventory-xr-uitoolkit-openxr-windows-editor` | pass | 31/31 | 2/2 |

Profiles without a PlayMode test assembly do not publish a PlayMode claim. The
non-XR UGUI graph contains no XR Foundry package, XR Interaction Toolkit, XR
Plug-in Management, or OpenXR entry. Renderer-explicit XR graphs retain their own
manifest, lock, and test results; evidence is not transferred between renderers.

## Public artifacts

Each profile has a directory under [`evidence/`](evidence/) containing:

- the real consumer `manifest.json` and Unity-generated `packages-lock.json`;
- a structured compile result;
- real Unity NUnit XML for every applicable test platform; and
- a compatibility receipt binding all artifacts and the exact execution tuple.

Machine and personal absolute paths in NUnit output were replaced with public
placeholders only. Test suites, assemblies, cases, counts, durations, and results
were not synthesized or changed.

## Claim limit

This evidence proves only Unity `6000.3.19f1` batchmode compilation and applicable
EditMode/PlayMode tests for `WindowsEditor / Null / Mono / x86_64`. It does not
prove an Android build, install, headset rendering, controller behavior, comfort,
PICO, Quest, visionOS, or any other device/runtime behavior. Those claims require
separate build and Device Lab receipts.

The verified version is an evidence anchor, not a restriction that requires this
repository to publish one package copy per Unity version. An Agent may use the
package and its public reference material to generate a candidate for another
Unity or tool version; that candidate becomes a support claim only after it is
validated on its own exact target tuple.
