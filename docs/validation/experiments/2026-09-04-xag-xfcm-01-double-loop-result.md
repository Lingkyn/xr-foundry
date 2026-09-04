# XAG-XFCM-01 three-Agent double-loop result

Status: **passed as a bounded local engineering experiment; not formal governance acceptance**

Execution window: 2026-09-04 to 2026-09-05

Tested source commit: `2605c1276c9a60065db8fa49ddd37c18c29e814d`

Baseline commit: `461d022a7e2e9ce33e54764424a4acfb48588409`

## Goal Contract

The experiment had to make three independently assigned cross-family binding
lanes contribute to one working system, not merely ask three Agents for opinions:

1. implement semantic interaction to Inventory intent;
2. implement Inventory state to persistence; and
3. implement Settings policy to semantic interaction.

The Supervisor had to integrate those changes in a clean worktree, materialize a
new Unity consumer, compile it, exercise the bindings and failure recovery, route
every observed defect to the smallest accountable layer, and regenerate the
affected output until the Stop Condition held.

No GitHub or other account-setting change, wallet, token, treasury, smart contract,
on-chain action, remote repository setting, push or pull request was intentionally
invoked by the Supervisor. Unity automatically used the host's pre-existing
LicensingClient and entitlement during the local runs, and the logs report a
license update; no account switch or external configuration operation was
requested.

## Agent topology and contribution boundary

The three implementation lanes used separate contexts, branches, worktrees and
non-overlapping primary write sets. The Supervisor reviewed and cherry-picked the
results into one local integration branch.

| Lane | Material contribution | Lane commit | Integrated commit |
| --- | --- | --- | --- |
| Interaction binding | `InteractionToInventoryIntentAdapter` and 7 EditMode tests | `73638614bcff584e5391916f011f435332db6657` | `7e39caa` |
| Persistence binding | `InventoryToPersistenceAdapter` and 9 EditMode tests | `20c17062686fbf93e8f1d8f0c067fc8b14128e6d` | `5205c95` |
| Settings binding | `SettingsToInteractionPolicyAdapter` and 7 EditMode tests | `f56f42e39bc55c59603e5149eb8f0e1077048061` | `a1eecb1` |

The harness foundation was integrated as `6ca3a6c`. Subsequent repair lanes and
Supervisor work changed the system, XFCM generator and evidence gate rather than
silently patching generated artifacts:

- optional DTO normalization: `0ed7b7b`;
- composition PlayMode coverage: `8264dae` and `4275ad4`;
- source-bound XFCM v0.2: `00a0d44` and `939641b`;
- production local-file recovery: `4d0a293`;
- interaction state reconciliation: `5a99749`;
- Unity result acceptance gate: `d203960`; and
- bounded evidence documentation: `2605c12`.

This is real execution isolation, collision control and integration work. It is
not governance independence: all Agents shared one Supervisor, accountable human
principal, host, orchestration lineage and evidence root. Their agreement is not a
DAO vote, quorum, independent approval or Sybil-resistant consensus.

## System under test

The committed XFCM lock structurally resolves 13 components. The disposable Unity
consumer deliberately materializes the eight packages at the three binding
endpoints:

- `com.lingkyn.interaction.core`;
- `com.lingkyn.inventory.core`;
- `com.lingkyn.inventory.presentation`;
- `com.lingkyn.inventory.unity`;
- `com.lingkyn.persistence.core`;
- `com.lingkyn.persistence.unity`;
- `com.lingkyn.settings.core`; and
- `com.lingkyn.settings.unity`.

The consumer uses synthetic semantic input and a recording Inventory view. The
end-to-end PlayMode path crosses frames from Settings and semantic interaction to
Inventory and persistence. A separate PlayMode case uses the production
`LocalFileSaveStore` and `JsonUtility` codec against a temporary directory to
recover from a zero-byte primary file to a valid backup.

Environment: Unity `6000.3.19f1` (`7689f4515d75`), macOS Editor,
`StandaloneOSX` target and `NullGfxDevice`. The compile command performs Unity
import and script compilation; it is not a player build.

## Bootstrapping double loop

Each observation was classified before correction. `A` is the produced system or
test output, `B` is the producer/resolver/acceptance mechanism, and `C` is a
governing principle. No C-level change was needed in this experiment; the existing
typed-boundary, fail-closed and evidence-separation principles were sufficient.

| Cycle | Observed evidence | Route | Correction at the accountable layer | Regenerated result |
| --- | --- | --- | --- | --- |
| 1 | First fresh EditMode run passed 19/22; Unity `JsonUtility` materialized an optional null string as empty | A | Normalize blank optional instance IDs at the adapter boundary and add regression coverage | Fresh run passed 23/23 |
| 2 | First PlayMode run passed 1/2; the test expected no dispatch while the formal router emits one `Rejected` receipt | A | Correct the test oracle to assert the rejection receipt and absence of domain mutation | Fresh run passed 2/2 |
| 3 | XFCM v0.1 could only encode pending bindings, so implemented adapters could not be represented truthfully | B | Introduce v0.2 implemented-binding records with confined source path, assembly and SHA-256 while preserving v0.1 semantics | Resolver and repository contracts passed with `bindings_implemented: true` and `runtime_ready: false` |
| 4 | Adversarial schema review found v0.1 duplicate-binding regression and newline/control-character canonicalization gaps | B | Restore exact v0.1 behavior and require canonical full-string v0.2 metadata | Negative mutations failed closed; full contract passed |
| 5 | Real persistence review found an empty primary was rejected by the store before the candidate selector could reach a valid backup | A | Let raw empty candidates reach the core selector, which classifies corruption and continues deterministically | Core 51/51, Unity 34/34 and real local-file PlayMode recovery passed |
| 6 | Policy changes could leave an old `Started` interaction pending and later produce a duplicate/ghost lifecycle after re-enable | A | Reconcile pending/toggle state in Interaction Core at intent, route and active-context scope; preserve unrelated shared-intent state | Interaction Core 14/14 and end-to-end PlayMode passed |
| 7 | The original XML checker could accept a wrong platform/assembly, skipped cases, a filtered one-case run or an ordinary stale result predating the recorded run boundary | B | Require source-audited `expected_total`, a pre-run epoch, one exact assembly/platform, stable same-file reads, consistent counts, unique full names and every case passed | 36 verifier regressions, two negative controls and all five fresh final XML files passed |

The earlier local evidence remains useful as falsification history rather than as
the final acceptance artifact:

| Run | Result | SHA-256 evidence |
| --- | --- | --- |
| 1 | compile pass; EditMode 19/22 fail | compile `025b18b9472c9a0b80663eb8d589b71dd84cdc811e7fcebdf6db8df889155959`; XML `9d7086ee45d86b4c59ce1b6c65ef1c749dbcdd3438d03c9e3d936f4f35109449` |
| 2 | compile pass; EditMode 23/23 pass | compile `435cc1a1efc1c9341cb8f35b51bbe91fd110b5ee9cef2b4ac210ca420bfecc88`; XML `3c442636cfb6bbc631cd2fe5d85e89418ca23fe1e62011c14998d8ab46f1ead6` |
| 3 | compile pass; PlayMode 1/2 fail | compile `8b6791de75e840719c8f556d98d7a8077b6547917a0d857954b8ad33d307a14c`; XML `8e4df74a7a554b57fa0cad63442a76922ad115b00c29c99f0a3583ff388247cd` |
| 4 | compile pass; EditMode 23/23 and PlayMode 2/2 pass | compile `2a9cd49ba1a4f8aaa1728bffde22a2b6445a2e9a666fb45f2ceb9616cfd8a6e4`; Edit XML `b8ea03d0d99cc4302b6d4577aef57f5fbeba78196fc333cf7a1875931d1875fb`; Play XML `da536925cb5fe64458ff34a3d8557f0bd96be913d4f733bb7822672c6fa90858` |
| Provisional | compile pass; EditMode 23/23 and PlayMode 2/2, later invalidated as final evidence by mechanism/runtime review | compile `f55e4578565a606c5b8dc8b5d69449045b8733d73eea2ea32bea47f1d55f4da1`; Edit XML `0e10d376173a93c5aa2413a65e69193806330783a4ee1fc6e8a65aa470f03430`; Play XML `4447f08770391a2676c1f8d9a9451fb4040ea9a08f479fdec486a572cf1e6415` |

## Final clean-consumer evidence

The final source commit was materialized into a path that did not exist before the
command. Every Unity result also used a new filename. The epoch was recorded
immediately before its Unity command; the verifier rejected any result predating
that boundary and required the independently source-counted total.

| Gate | Start epoch | Exact result | Result SHA-256 |
| --- | ---: | --- | --- |
| Unity import/script compile | `1788564637` | exit 0; batch mode exited successfully | `267aee415682fe18a34c9be5f3d254da9dd7c8b20f38923fd039662afc2462d0` (log) |
| Reference EditMode | `1788564671` | 23/23 passed; 0 failed/skipped/inconclusive | `e3e5ea57120906336967f3477f5b5308557612b12f788d50ec66206e0f08aec1` |
| Interaction Core EditMode | `1788564693` | 14/14 passed; 0 failed/skipped/inconclusive | `606abdc10358efb161f6ca86292c940de14c2f3e81139fafc47d511b1873a81f` |
| Persistence Core EditMode | `1788564719` | 51/51 passed; 0 failed/skipped/inconclusive | `f5253c98872aa6634407fbfce236bff81b23a91f2b0688e22febddaa0f8c1595` |
| Persistence Unity EditMode | `1788564741` | 34/34 passed; 0 failed/skipped/inconclusive | `92447e1a27f0f5383209809bb47a0432ee3978ca1b79b810ceb5f3472cb74ee3` |
| Reference PlayMode | `1788564764` | 3/3 passed; 0 failed/skipped/inconclusive | `ff1383b52f12b7f689093cd3b2c0954e1529cbd39da6a2edf17c1d310ee96960` |

The final total is 122 EditMode cases plus 3 PlayMode cases. Static inventory
found no parameterized, repeated, ignored, explicit or test-case-level
platform-excluded cases in these assemblies. The outer Unity Project suite
legitimately reported the whole project discovery count for filtered EditMode
runs; exact totals were bound to the test run, the one requested Assembly and its
actual cases.

Two controls against the real 23-case XML failed as intended:

- changing `expected_total` to 22 returned exit 1 with mismatches at run,
  Assembly and actual-case levels; and
- moving `not_before_epoch` after the file mtime returned exit 1 as stale.

Additional exact artifact bindings:

| Artifact | SHA-256 |
| --- | --- |
| Materialized `Packages/manifest.json` | `ee24844559f0423e791129f3c1519e9876399c303527ebd62dd8f4c5e657ccaf` |
| Unity-generated `Packages/packages-lock.json` | `ba54412344e5cc71a967114f35282ae7f7b5fc7d4ac461e353ff3d7b328043db` |
| Consumer materializer | `951c0374731f8efb8a16f1aff17e08ce32126ff85291f6de4c3125f04c656149` |
| Unity result verifier | `d7e981fcfc8165c5a721a7bffb6a8750028c9f58a9ac5661a51825a276b7f304` |
| XFCM v0.2 project manifest | `1608d36b629f07930c639251392adf7fa7705b69fef4aa040bbafcb22ec6a08c` |
| XFCM v0.2 structural lock | `f0c7e04b6e225700aba303aaf47894415b25bd8a71d60f544c9fad8e31a76424` |

The eight materialized package trees differed from their repository sources only
by `.meta` files generated by Unity for previously untracked root assets. The
source bytes under test were otherwise identical.

Repository gates at the tested commit:

- `scripts/compose_system.py --check --json`: pass, 13 components,
  `bindings_implemented: true`, `runtime_ready: false`;
- 36 focused Unity-result-verifier regressions: pass; and
- `scripts/validate_repository.py --json --run-contract-tests`: pass with
  `errors: []` and contract-test return code 0.

The Python gate used an offline temporary environment with compatible cached
`jsonschema 4.26.0` and `PyYAML 6.0.3`, not the repository's exact pinned
`4.25.1` and `6.0.2`. This proves the contract on the observed environment but is
not a hermetic exact-dependency reproduction.

Raw Unity logs and XML remain local and disposable because Unity embeds local
paths, hostnames and network-interface data. Their hashes, counts, commands and
bounded conclusions are retained here; no raw private host data is committed.

## Claims supported

- Three Agents made distinct, material C# and test contributions which the
  Supervisor integrated into one consumer.
- The three typed cross-family binding paths compile together and execute in one
  Unity Editor consumer at the tested source commit.
- The tested interaction lifecycle survives the exercised Settings disable,
  rejection and re-enable sequence without resurrecting stale state.
- The tested production local-file persistence path recovers from a zero-byte
  primary to a valid backup without leaking primary state.
- XFCM v0.2 can represent and content-bind the three implemented consumer-owned
  adapter sources while honestly retaining `runtime_ready: false`.
- The loop changed both produced code and the mechanisms that produce or accept
  evidence, then regenerated the affected results.

## Non-claims and residual debt

- This does not compile or run all 13 selected components. Inventory Unity and
  Settings Unity were imported and compiled but not directly behavior-tested by
  the named system cases.
- It is not a player build and does not exercise physical XR input, controller
  bindings, UGUI rendering, an OpenXR runtime, a headset or another named device.
- Synthetic input and a recording presentation view do not establish performance,
  comfort, usability or renderer correctness.
- One temporary-filesystem recovery case does not establish general durability,
  crash safety, permissions behavior or cross-platform storage compatibility.
- Settings snapshots must currently load before policy-adapter initialization;
  automatic load-after-initialize resynchronization is a later lifecycle slice.
- The XFCM lock binds component manifests and adapter sources, not every package
  implementation byte or a complete consumer Git tree. Resolver filesystem checks
  assume a serialized writer and no hostile same-privilege mutation.
- Freshness evidence assumes a serialized, non-adversarial Supervisor using a new
  result path. It does not resist same-privilege replay or bind XML
  cryptographically to the Unity process or source commit.
- The offline Python environment was compatible rather than exactly pinned.
- The local commits and evidence are not remotely reachable and are not public
  governance records because repository push and public evidence publication were
  forbidden.
- Unity's automatic use of the host's pre-existing LicensingClient is disclosed
  above and is not evidence of an XR Foundry account, governance or DAO operation.
- The Agent lanes do not prove autonomous membership, delegation, revocation,
  quorum, treasury or on-chain DAO operation.

Next-loop candidates are a full 13-component consumer, executable lifecycle
startup/rollback/reverse-stop semantics, complete source/revision binding, exact
offline dependency provisioning, Settings load resynchronization, and later named
device evidence. None is silently promoted by this receipt.

## Stop Condition

The bounded Goal Stop Condition is satisfied: all three Agent contributions are
integrated; a newly materialized consumer compiles; all five source-counted Unity
assemblies pass with fresh-path and recorded-epoch verification; real local-file
failure recovery passes; repository contracts pass; every observed blocking
defect was routed and regenerated; and the Supervisor intentionally invoked no
prohibited account-setting, wallet, chain or remote-repository operation. Unity's
automatic pre-existing license-client behavior is the disclosed exception to a
broader "no external side effect" reading.

The correct outcome is therefore **local engineering pass with explicit limits**,
not “XR Foundry is finished” and not “the DAO is operational.”
