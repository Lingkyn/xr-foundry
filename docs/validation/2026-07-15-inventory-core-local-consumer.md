# Inventory Core local consumer validation - 2026-07-15

Status: passed after restoring the explicit test dependency.

## Scope

- Unity Editor: 6000.3.19f1
- Consumer: newly created project outside this repository and outside any game
  repository
- Package source: local `file:` reference to `com.lingkyn.inventory.core`
- Test dependency: `com.unity.test-framework` 1.4.6

## Results

| Check | Result |
| --- | --- |
| Resolve `com.lingkyn.inventory.core` | Pass |
| Compile engine-light Runtime assembly | Pass; no C# compiler error |
| Compile package EditMode tests | Pass |
| Run deterministic and stateful tests | Pass - 10 total, 10 passed, 0 failed, 0 skipped |
| Batch-mode shutdown | Pass - exit code 0 |
| Git URL install | Pending until the reviewed revision is public |
| UI or XR behavior | Not present and not claimed |

Compile/test log SHA-256:
`1EE8E4458C688F294DBDB7060C096A27752A71F3828DC259E59BB92B566CD36F`

EditMode result SHA-256:
`9A5FCD09FF66671D823DD27109823C8F3642424E5A64E63A6E1C3B64E8DF9A75`

## Failure found by the gate

The first run used a blank Unity project without an explicit Test Framework
dependency. The package Runtime assembly compiled, but the test assembly could not
resolve NUnit and the run exited with code 1. Adding
`com.unity.test-framework` 1.4.6 to the clean consumer and keeping the package in
`testables` restored the supported test path. The second run compiled and passed
all ten tests.

## Boundary

This proves local package resolution, compilation, and the current core invariant
tests. It does not prove public Git installation, compatibility with an existing
game, persistence-provider behavior, Unity authoring, UI, XR interaction, or device
behavior.
