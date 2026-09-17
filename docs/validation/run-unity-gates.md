# Run the Unity gates: what to test, how, and what it proves

Audience: anyone with a Unity Editor and Python who wants to test XR Foundry
packages, whether or not they have repository write access. No hand-maintained
Unity project is needed; the runner generates a disposable host for you.

## What you need

| Requirement | Detail |
| --- | --- |
| Unity Editor | `6000.3.19f1` is the recorded profile. Install it through Unity Hub. Another `6000.3` editor runs too, but its receipt is a new tuple, not evidence for the recorded profile. |
| Python | `3.11`, `3.12`, or `3.13`, with the pinned contract dependencies installed in a virtual environment (see `README.md`). |
| Disk and time | The first run imports XR Interaction Toolkit, OpenXR, and Input System. Expect several minutes per assembly on the first run and much less afterwards. |
| No headset | Nothing on this page needs a device. Headset evidence is a separate route (below). |

## One command

```bash
python scripts/run_unity_gates.py
```

That runs the reference-system binding consumer: eleven packages, every test
assembly, EditMode and PlayMode. Variants:

| Goal | Command |
| --- | --- |
| Check the setup without launching Unity | `python scripts/run_unity_gates.py --dry-run` |
| Every live package (all 15) | `python scripts/run_unity_gates.py --host all` |
| One package plus its Foundry dependencies | `python scripts/run_unity_gates.py --host com.lingkyn.inventory.xr.ugui` |
| Only EditMode | `python scripts/run_unity_gates.py --mode EditMode` |
| One assembly | `python scripts/run_unity_gates.py --assembly Lingkyn.Inventory.Core.Editor.Tests` |
| A specific editor | `python scripts/run_unity_gates.py --unity <path to the Unity executable>` (on Windows the Hub installs it under `Unity\Hub\Editor\<version>\Editor\Unity.exe` inside Program Files) |

The runner finds the editor from `--unity`, then `UNITY_EDITOR`, then the Unity Hub
default install folders, preferring the recorded profile version.

## What it does

1. Generates a host project in a temporary directory: the reference consumer
   template, or a fresh project with the selected packages embedded under
   `Packages/` and listed as `testables`. Packages that depend on XR Interaction
   Toolkit also pin Input System `1.19.0`, XR Plug-in Management `4.5.3`, and OpenXR
   `1.16.0`, the tuple the recorded profiles used.
2. Audits every test assembly from source and computes its exact test-case count
   (`scripts/audit_unity_test_inventory.py`). This is the count the result must
   match; it is never learned from the result.
3. Runs each assembly in its own Unity batchmode process with `-assemblyNames`,
   records the run boundary first, and verifies the NUnit XML with
   `scripts/verify_unity_test_results.py`: exactly one assembly, exact count, every
   case passed, file newer than the boundary.
4. Writes `receipt.json` next to the XML and log files, by default under
   `.unity-gates/<timestamp>/` (ignored by Git).

## What to test, by package family

| Family | Host | What a pass shows |
| --- | --- | --- |
| Inventory (Core, Unity, Presentation, UGUI, XR UGUI) | `reference-system` or `--host com.lingkyn.inventory.xr.ugui` | Domain invariants, authoring conversion, presenter, UGUI prefab replay, XR UGUI scene validation and XRI routing in the Editor |
| Inventory UI Toolkit route | `--host com.lingkyn.inventory.xr.uitoolkit` | The peer renderer; never mix its result with the UGUI route |
| Persistence | `--host com.lingkyn.persistence.unity` | Envelope, migration, recovery, and local-file commit semantics in a temporary directory |
| Settings | `--host com.lingkyn.settings.unity` | Transactions, rollback, ScriptableObject conversion |
| Interaction | `--host com.lingkyn.interaction.unity` | Semantic routing and Input System conversion |
| Foundations | `--host com.lingkyn.xr-baseline` | Sandbox defaults and the far-caster repair (#21) |
| Everything | `--host all` | The union; slower, one lock for all packages |

## Reading the receipt

- `status: pass` means every selected assembly passed with the exact count on the
  recorded commit. `repository.worktree_dirty: true` means the run did not test a
  clean commit; rerun from a clean checkout before submitting.
- `editor.selection` says which editor was used and whether it matches the profile.
- `host.resolved_lock_sha256` and the copied `packages-lock.json` bind the exact
  dependency tuple; a different lock is a different tuple.
- Each run carries the Unity exit code, the result digest, and every verifier error.

## Submitting a result

Attach `receipt.json`, the `*.xml` results, and the `packages-lock.json` to the
pull request or Issue you are working on, and name the commit, editor version, and
operating system in the comment. A receipt from a clean checkout of `main` may be
promoted by a maintainer into `docs/validation/evidence/` after independent review;
the receipt itself grants no maturity or release status.

## Running without touching your own machine

- **GitHub Actions with a license secret**: the `unity-consumer-tests` workflow runs
  the same audit and verifier inside game-ci containers. A maintainer adds
  `UNITY_LICENSE` (or `UNITY_EMAIL` and `UNITY_PASSWORD`) as repository secrets and
  triggers it; fork pull requests skip it.
- **Your computer as the runner**: install Unity on a machine, register it as a
  GitHub self-hosted runner with the label `unity`, and trigger the
  `unity-self-hosted-gates` workflow. It runs `run_unity_gates.py --host all` on that
  machine and uploads the receipt. Use this only on machines you control and only
  through manual dispatch; never expose a self-hosted runner to fork pull requests.

## What a green run does not prove

Editor tests with the Null graphics device prove compilation and the automated
behaviors under test for one exact tuple. They prove nothing about a player build,
Android installation, controller input on hardware, world-space readability,
comfort, or any named device. Those claims need a Device Lab receipt:
[`docs/device-lab/README.md`](../device-lab/README.md).
