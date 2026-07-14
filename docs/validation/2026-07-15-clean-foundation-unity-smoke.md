# Clean-foundation Unity consumer smoke — 2026-07-15

Status: passed.

## Scope

- Unity Editor: 6000.3.19f1
- Consumer: a newly created project outside this repository and outside any game
  repository
- Source: the clean-history XR Foundry working tree immediately before publication
- Dependency mode: local `file:` references to both package roots

## Results

| Check | Result |
| --- | --- |
| Resolve `com.lingkyn.project-initializer` | Pass |
| Resolve `com.lingkyn.xr-baseline` | Pass |
| Resolve declared Unity dependencies | Pass |
| Compile Runtime, Editor, and test assemblies | Pass; no C# compiler error |
| Run package EditMode tests | Pass — 2 total, 2 passed, 0 failed, 0 skipped |
| Batch-mode shutdown | Pass — exit code 0 |
| Pico/headset behavior | Not tested and not claimed |

The compile log SHA-256 was
`535FDFEDECFCA6A60801B8B4AAF9DDFC3727680A971DF28ACB0CFC36580783DE`.
The EditMode result SHA-256 was
`EA7C755C31672F956028FBAD615A853A4F2B311498ADE4CC60DF3FF8116F1075`.

## Boundary

This proves package resolution, compilation, and the included EditMode tests from
the clean foundation tree. It does not prove a Git URL install until the repository
is published, and it does not prove runtime or target-headset behavior.
