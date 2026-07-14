# Independent Unity consumer smoke — 2026-07-14

Status: passed with an explicit XR sample/device boundary.

## Environment

- Unity: 6000.3.19f1
- Consumer: newly created empty Unity project outside this repository and outside
  any game repository
- Dependency mode: local `file:` references to both package roots

## Results

| Check | Result |
| --- | --- |
| Resolve `com.lingkyn.project-initializer` | Pass |
| Resolve `com.lingkyn.xr-baseline` | Pass |
| Compile all Runtime, Editor, and test assemblies | Pass |
| Unity EditMode tests | Pass — 2 executed, 2 passed, 0 failed |
| Run project initializer from command line | Pass — four scenes and scaffold created |
| Run XR Sandbox initializer from command line | Pass — greybox/config/scene saved |
| XRI Starter Assets rig creation | Not exercised — sample was intentionally absent; the initializer emitted an actionable warning |
| Pico/headset behavior | Not tested and not claimed |

The first compile exposed and then removed one hidden legacy namespace reference in
the XR Editor assembly. The successful rerun is the evidence used here; a
project-local test was not accepted as a substitute.

## Promotion boundary

This receipt supports package neutrality and independent compilation. Package
maturity remains `incubating` until repository review and remote CI are complete.
XR device behavior remains outside this receipt and requires target-headset evidence.
