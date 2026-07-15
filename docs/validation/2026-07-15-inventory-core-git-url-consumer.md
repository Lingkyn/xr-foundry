# Inventory Core Git URL consumer validation - 2026-07-15

Status: passed after replacing a short revision with the full immutable commit SHA.

## Scope

- Unity Editor: 6000.3.19f1
- Consumer: clean project outside this repository and outside any game repository
- Package source:
  `https://github.com/Lingkyn/xr-foundry.git?path=com.lingkyn.inventory.core#3bb74d14b85a51b520f8c78a1e919c1f9e00de03`
- Resolved package source: `git`
- Resolved package hash: `3bb74d14b85a51b520f8c78a1e919c1f9e00de03`

## Results

| Check | Result |
| --- | --- |
| Clone public repository | Pass |
| Resolve package subfolder at immutable revision | Pass |
| Compile Runtime and test assemblies | Pass; no C# compiler error |
| Run package EditMode tests | Pass - 10 total, 10 passed, 0 failed, 0 skipped |
| Batch-mode shutdown | Pass - exit code 0 |
| UI or XR behavior | Not present and not claimed |

Compile/test log SHA-256:
`F34F62F189A986188CED751C2651B10D244032B39FEBAFD13D4EA85A932F7E44`

EditMode result SHA-256:
`D22076E69C5119D59F0BB2B06522ADE065B3FF21C1553BC638B0B1B8889D33E0`

## Failure found by the gate

The first Git run pinned `3bb74d1`. Unity Package Manager reported that the short
value was not a valid branch, tag, or full commit hash and exited with code 1. The
same URL with the complete 40-character revision resolved and passed. Public install
guidance therefore requires full commit SHAs.

## Boundary

This proves public Git dependency resolution, compilation, and current core tests
at the named revision. It does not promote the package beyond `incubating`, prove
API stability, or prove Unity authoring, UI, persistence-provider, online, XR, or
device behavior.
