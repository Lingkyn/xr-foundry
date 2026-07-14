# Git URL Unity consumer smoke — 2026-07-15

Status: passed after the nested package-source inclusion repair.

## Scope

- Unity Editor: 6000.3.19f1
- Consumer: a newly created project outside XR Foundry and outside any game
  repository
- Package source: public Git URLs pinned to
  `e0bfc8ab02d6a37af957336e87854da33a3cddad`
- Packages: `com.lingkyn.project-initializer` and `com.lingkyn.xr-baseline`

## Results

| Check | Result |
| --- | --- |
| Resolve both public Git URL dependencies | Pass |
| Lock both dependencies to the reviewed commit | Pass |
| Compile Runtime, Editor, and test assemblies | Pass; no C# compiler error |
| Run package EditMode tests | Pass — 2 total, 2 passed, 0 failed, 0 skipped |
| Batch-mode shutdown | Pass — exit code 0 |
| Pico/headset behavior | Not tested and not claimed |

Compile log SHA-256:
`732180892C2AED182B5403B35EA7DA0676979B651DE52ECB54FA651560367CC7`.
EditMode result SHA-256:
`BDF3D0D485193CF84022FFB805CF5816D86802D3CF0F4005BC463012790CA065`.

## Failure found by this gate

The first public Git URL run failed because an unanchored root `Build/` ignore rule
also ignored package source under `Editor/Build`. Local `file:` validation still
saw those files and passed. The repair root-anchored Unity generated-directory
ignores, committed the nested build sources, and added static checks for ignore
scope and unresolved internal namespace imports. The reviewed commit above is the
successful repair; the earlier commit must not be used as a package pin.

## Boundary

This receipt proves public Git dependency resolution, package compilation, and the
included EditMode tests. It does not prove runtime or target-headset behavior.

