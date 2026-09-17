# XAG-XR-02 real three-Agent construction loop

Status: **loop 8 Unity engineering evidence and the final repository recheck
passed; administrative remote closure is outside this local-only experiment**

Execution date: 2026-09-05

Historical baseline: `58c8a7070b8e938952805c1c4ddce9439f30ed02`

Source cycle: `cycle-708e9f138d99` (formally handed off, never relabelled as a
pass)

Successor cycle: `cycle-c39d0f0f6b70`

Continuation item: `handoff-82cd141f97c790c95114`

Collaboration topic: `topic-3828b5cb01a4`

The immutable local checkpoint that contains this document owns the final Git
identity. That identity is recorded by Git and the Capsule after the checkpoint;
it is intentionally not self-referenced inside the commit whose hash it helps
produce.

## Research question and method

This bounded design-science experiment asked whether three separately assigned
Agents could make material, non-overlapping XR system contributions which a
Supervisor could integrate into one clean Unity consumer, falsify through real
compile and test failures, correct at the owning layer, and revalidate without
using an external account, remote repository mutation, wallet or chain.

The unit of analysis was one local collaboration topic and its successor cycle.
The intervention used three Builder contexts, a Supervisor integration context,
a typed composition manifest and lock, a disposable Unity consumer, strict XML
acceptance, repository contract tests and an independent read-only review. The
method was iterative action research rather than a benchmark: observations were
allowed to change both the artifact and the mechanism that admitted or verified
the artifact.

The three Builder contexts shared one Supervisor, host, accountable principal,
orchestration lineage and evidence root. They are independent implementation
contexts, not independent governance principals. Their agreement is not a DAO
vote, quorum or Sybil-resistant consensus.

## Goal and constraints

The experiment had to preserve three attributed contributions:

1. Unity Input System signals to semantic interaction;
2. Inventory presentation to a concrete UGUI/XR surface; and
3. Settings snapshot persistence and rehydration through Persistence Core.

The Supervisor had to integrate all three into the existing reference consumer,
materialize the exact package set, run Unity import/compile, EditMode and
PlayMode, repair failures, keep XFCM source bindings current, and record the
control-loop feedback. The original dirty checkout remained out of bounds.

Prohibited effects were account switching, credential changes, wallet, token,
treasury or on-chain operations, remote settings, push, pull request, player
build, and unobserved headset or controller claims. Unity used the host's
pre-existing local licence service; no account or licence configuration was
changed.

## Runtime authority and handoff

The first cycle had a malformed Goal Contract: it required future effect,
finish and close records as evidence before effect acceptance. The Goal was
immutable, and the available rebind path also required a writer lease which a
valid worktree lane did not own. The Supervisor therefore used the formal
handoff transaction. The source cycle is terminal as cancelled/incomplete with
the exact old Goal digest
`sha256:59cf7b672221538bafa79b1d33286a90da07495616f240fd1e9519f44db37527`.
The continuation item preserves its lineage.

The corrected successor is bound to Goal digest
`sha256:60bcf89e5ca2cf2764049498d6ab5387f4ff045189d7ff8f2520fe7bc522d396`,
the same session, thread, lane, branch and baseline, plus the expanded validator
write boundary. Its cycle packet records the pinned `unity-dev` route, local
execution authority and active obligation graph.

The successor did not receive authority merely because a document said so. Its
successful start followed real fail-closed observations:

| Observation | Owning-layer response | Result |
| --- | --- | --- |
| Multi-lane Topic lacked a plan post and Supervisor claim | Published one plan and claimed `titem-b1cd8fcee086` | Topic plan and claim gates passed |
| Validator source was absent from the registered lane write set | Used the guarded append-only write-set expansion transaction | Registry recorded the added path; admission passed |
| Old Host contract still bound the predecessor Goal, six paths and old stop condition | Authored a successor Host contract while preserving the admitted baseline and Builder assignment | Host admission passed |
| A handoff receipt was incorrectly supplied as an enforceable route assessment | Removed the invalid classifier claim and used the pinned route in shadow selection mode | Route resolved to the current local Unity provider |
| Corrected Goal used descriptive completion keys but omitted the kernel-required `administrative_closed` field | Corrected the Goal body before binding | Goal Contract compiled into the obligation graph |
| Earlier failed start attempts published overlapping retry intents | The blackboard admission terminalized same-lane retry intents | Exactly one successor acquired runtime authority |

This sequence is evidence that the local control kernel was exercised rather
than reconstructed as a narrative after the code was written.

## Agent contribution map

| Topic item | Builder | Material contribution | Primary owned files |
| --- | --- | --- | --- |
| `titem-b6533dd84ca8` | `/root/xr_input_builder` | `InputSystemSignalAdapter` feeds typed semantic events into `InteractionCoordinator`, freezes route facts per signal and returns typed capture failures | `UnityInputSemanticInteractionAdapter.cs`, EditMode tests |
| `titem-923bd3ceaee6` | `/root/xr_surface_builder` | `InventoryWorldSpaceSurface` composes Inventory presenter/shell with a UGUI renderer and XR surface while keeping domain mutation outside the view | `InventoryUguiXrSurfaceAdapter.cs`, EditMode and PlayMode tests |
| `titem-da5cd7547b9c` | `/root/xr_settings_builder` | `ISettingsSnapshotRepository` persists through `SaveCoordinator`, uses deterministic JSON, preserves unknown settings and exposes typed diagnostics | `SettingsPersistenceRehydrationAdapter.cs`, EditMode tests |
| `titem-b1cd8fcee086` | Supervisor | Integrated assemblies, package materialization, manifest/lock bindings, validation boundary, Unity evidence and this receipt | shared consumer, XFCM, scripts, tests and evidence document |

The Builder claims were created before source mutation. A runtime defect remains:
their Topic claim events recorded an empty `cycle_id`. The topic, lane, session,
paths, Agent and attempt identifiers still preserve attribution, but the missing
cycle foreign key is control-kernel debt and must not be interpreted as stronger
provenance than it provides.

## System under test

The consumer materializes eleven local UPM packages spanning Interaction,
Inventory, Persistence and Settings. Together with the three pre-existing
cross-family adapters, XFCM now resolves 13 components and seven implemented
bindings. `runtime_ready` deliberately remains `false`: Editor compile and
synthetic tests do not prove player, OpenXR, controller or headset readiness.

Environment:

- Unity `6000.3.19f1` (`7689f4515d75`);
- macOS Editor, batch mode and `NullGfxDevice`;
- Input System `1.19.0`;
- XR Interaction Toolkit `3.5.1`;
- no Player Build and no physical device.

The adapter boundary is typed. No global event bus, service locator, reflection
discovery or Agent-specific domain write path was added.

## Build-observe-correct-rebuild trail

`A` means an artifact/runtime defect, `B` a producer or verifier defect, and `C`
a governing-principle defect. The typed-boundary, fail-closed and evidence-level
principles remained adequate, so no C-level rule was weakened.

| Loop | Observation | Class | Correction | Regenerated evidence |
| --- | --- | --- | --- | --- |
| 0 | Initial project entry, lane path, host carrier and Goal stop semantics were incomplete; the first pre-edit carrier also said editing was not admitted | B | Added the minimal local project profile and exact lane/host carriers, corrected pre-edit admission and then used a formal Goal handoff when the immutable circularity was found | Source cycle preserved as failed/handoff evidence; successor admitted |
| 1 | Sandboxed Unity could not write its package database and the local package-manager socket returned `EPERM` | B | Re-ran only the disposable consumer outside the restrictive sandbox | Package manager could start; no source change |
| 1b | One stale licensing client caused the first unrestricted import to miss the package registration window, producing broad missing-namespace errors | B | Inspected the exact process and stopped only that stale process, then created a new consumer rather than accepting the polluted run | Loop 2 registered 27 packages and compiled |
| 2 | EditMode passed 34/35 and PlayMode 3/4. Tests expected synthetic direct event execution to activate the surface, while the shipped surface correctly failed closed | A | Opened only `CanvasGroup.interactable` for direct synthetic execution; retained `blocksRaycasts=false` and public `InteractionEnabled=false` | Corrected behavioural oracle and surface semantics |
| 3 | New assertions reached the concrete tracked-raycaster type and failed compile with `CS0012`, coupling the tests to the XR toolkit assembly | A | Removed concrete toolkit assertions instead of widening test-assembly dependencies | Loop 4 compiled; EditMode 35/35 and PlayMode 4/4 passed |
| 4 review | Constructor review found that `InventoryPresenter` subscribed before `Refresh`; malformed deep grid binding could throw and leak the observer | A | Prevalidated Grid `ContentRoot`, `SlotTemplate` and parent relation before presenter construction; added a regression proving later Inventory mutation remains safe | Loop 5 reference EditMode increased to 36/36; reference PlayMode remained 4/4 |
| Surface review | Adversarial review found that constructor failure could still retain subscriptions and that several surface tests asserted setup rather than the observable guard result | A and B | Rolled back both subscriptions if initial refresh throws; replaced weak assertions with guard-specific cross-surface negatives, post-dispose mutation oracles and idempotent disposal coverage | Independent surface review reported no High or Medium finding |
| Settings review | Persistence-side validation had begun to duplicate authority owned by Settings Core; missing wire fields, maximum revision and failed rehydration could be misclassified or overwrite committed state | A | Moved presence-sensitive default completion and constraint checks into Settings Core, retained unknown fields, rejected maximum revision before effects, and kept a failed load from replacing the committed snapshot | Independent settings review reported no High or Medium finding |
| Input review | A matching action identifier did not prove callback object identity; live phase completion could drift from the Core pending winner; reset semantics were not scoped strongly enough | A | Required the frozen `InputAction` instance, made Core own scoped reset and pending-winner selection, normalized live Button pulses and added same-identifier/different-instance and lifecycle regressions | Independent input review reduced the result to no High finding and one final Medium, which the object-identity guard then closed |
| Repository 1 | Source changes made the composition lock stale | B | Regenerated the lock from the current manifest and source digests | XFCM check passed with 13 components and seven implemented bindings |
| Repository 2 | Text safety scanned ignored local control projections and rejected the newly required project profile | B | Enumerated Git publication candidates, retained a fail-closed filesystem fallback, and allowed only declared control markers in the exact root profile | Complete 195-test suite passed; tracked/force-added ignored unsafe content still fails |
| Cycle repair | Successor admission exposed stale write authority, Host contract, route-assessment and Goal-schema assumptions one at a time | B | Repaired each authority carrier at its owning layer and retried from a new admission attempt | `cycle-c39d0f0f6b70` started with lane, Topic, Host, route and Goal gates passing |
| 6 | The fresh consumer compile encountered a stale licensing mutex/channel, timed out during licence initialization, assigned unknown entitlement status and failed to register the required packages; the resulting missing-namespace compiler errors were downstream symptoms | B | Preserved the failed log, treated it as an exclusive host-resource failure rather than a source verdict, serialized later Unity work and materialized a new consumer | Loop 6 remains historical failed evidence, not source failure and not a pass |
| 7 | A newly materialized consumer compiled; five package assemblies passed exactly, but the 71-case reference EditMode assembly ran 55 passed and 16 failed. Every failure was a live Input callback case whose callbacks never fired under the EditMode default Editor update path | B | Two independent read-only analysis lanes, `input_callback_identity_probe` and `input_live_test_failure_probe`, converged from Input System 1.19.0 source and the failed XML. The fixture then reused the already-passing package test pattern by deriving from `InputTestFixture` | Loop 7 failure XML was retained; no production semantic rule was weakened |
| 8 | The corrected fixture was materialized into another fresh consumer | — | Recompiled and ran each source-audited assembly separately, with strict count, assembly, freshness and outcome verification | 146/146 EditMode plus 4/4 PlayMode passed; 150/150 total |

The loop therefore changed both the produced Unity system and the machinery that
admits, composes and validates it. Failed evidence was retained as falsification
history; it was not overwritten into a pass.

## Final Unity evidence

The final disposable consumer was created at a fresh, repository-external
`<temporary-loop8-consumer>` path. The audited
source set had digest
`1c8a5b5abbce81ff81f9149612626e46a55cc3a361cf4e8eeea2b367e272c4cf`.
The corrected Input fixture had SHA-256
`021687f6580691669e8c217628af3a677dd42fe56cb35d51c069e47a57c89dfe`.

| Gate and exact assembly | Exact loop 8 evidence path | Result | Evidence SHA-256 |
| --- | --- | ---: | --- |
| Unity import/script compile | `<temporary-loop8-consumer>/compile.log` | exit 0; no C# compiler error | `83355b7e65105fa70efe3fa7f8fb2499da45aa1cafb18a0a012dfe25a51ee8a4` |
| `Lingkyn.Interaction.Core.Editor.Tests` | `<temporary-loop8-consumer>/interaction-core-edit.xml` | 16/16 EditMode | `00adc699e9cb3e818a88f9e637f10331f040072142e2fd06f85d4c64629be8ff` |
| `Lingkyn.Interaction.Unity.Editor.Tests` | `<temporary-loop8-consumer>/interaction-unity-edit.xml` | 17/17 EditMode | `9e7485bec24e2ab9b1ef61e456b1fa59e9c5dee8aa3cc11d0e1299f240e04567` |
| `Lingkyn.Inventory.Presentation.Editor.Tests` | `<temporary-loop8-consumer>/inventory-presentation-edit.xml` | 5/5 EditMode | `69ad69c0ee029e8ca154a4815aff44b88872186c9c343e8aa7fc64e4f6545e66` |
| `Lingkyn.Settings.Core.Editor.Tests` | `<temporary-loop8-consumer>/settings-core-edit.xml` | 29/29 EditMode | `7216f16c25191414462a9d088413a38ef82bb2cfb0684b2ec499e61264e39d86` |
| `Lingkyn.Settings.Unity.Editor.Tests` | `<temporary-loop8-consumer>/settings-unity-edit.xml` | 8/8 EditMode | `9767507f758d94e09a0abfcadae95f32b95720070ce334679644ee7586e49a72` |
| `XRFoundry.ReferenceSystem.EditMode.Tests` | `<temporary-loop8-consumer>/reference-edit.xml` | 71/71 EditMode | `6464f39be0e82763f6c0956cd52f8955f7e5dedab9a55adbe4c6d9b6d7b857e1` |
| `XRFoundry.ReferenceSystem.PlayMode.Tests` | `<temporary-loop8-consumer>/reference-play.xml` | 4/4 PlayMode | `c8c569b4d2a3477ece6191643407d6116100610fec06f5269379098082bf2cca` |

The exact total is 146/146 EditMode plus 4/4 PlayMode, or 150/150 cases.
Every XML reports zero failed, skipped and inconclusive cases. The strict checker
confirmed actual case counts, unique test identities, platform, one exact
assembly and file freshness. Its regression suite contains wrong-count,
stale-file, skipped, failed, mismatched-assembly and unstable-read negatives.

Loop 6's licensing/entitlement failure and loop 7's
`<temporary-loop7-consumer>/reference-edit.xml`
(`55/71`, 16 failures) remain falsification evidence. The two read-only analysis
lanes are engineering independence inside one Supervisor lineage, not separate
governance principals or durable quorum receipts.

Raw Unity logs and XML remain local and disposable because they contain host
paths and process details. Their paths and hashes make this local run auditable,
but do not make it remote, device or governance evidence.

## Repository evidence state

- final Python contract run: **195/195 passed** after the fixture correction;
- final composition check: **pass**, 13 components,
  `bindings_implemented=true`, `runtime_ready=false`;
- final canonical repository validation: **pass**, zero reported errors and a
  successful contract-test subprocess;
- final Unity result: **146/146 EditMode plus 4/4 PlayMode passed**.

Selected source bindings:

| Artifact | SHA-256 |
| --- | --- |
| Final audited source set | `1c8a5b5abbce81ff81f9149612626e46a55cc3a361cf4e8eeea2b367e272c4cf` |
| Unity input adapter | `6df16100246bc02ad9ef2580164e64ce018907a20b48516de82835ed92bf58c6` |
| Unity input adapter tests | `021687f6580691669e8c217628af3a677dd42fe56cb35d51c069e47a57c89dfe` |
| Inventory UGUI/XR surface adapter | `3f20154c10c6f8a89444465ed279d873f862f01de818b99c3f99fcd385f3428b` |
| Settings persistence adapter | `807b54b0b0dbe708f22648c28c999bb9763912cc48a9e70419c1753292e30c0a` |
| XFCM project manifest | `12e77dd1ee0e67f5d207634e8c585fe432ef00cf71058dacb1f85ff29879fe63` |
| XFCM structural/source lock | `a8358b4992646859976962c99914dbf5370c90d0f9c9b7e1b84912d6fbfa7d5e` |
| Consumer manifest | `54dc10ccebf6370bcbb121800a022ab18843896430f2a6ad71fff7b9d14a0004` |
| Loop 8 Unity-generated package lock | `9d1bdfbf9a7352c61284f8522e65a8673c95f81cd7383d329fea4aabb7aa4c71` |
| Consumer materializer | `ebfa914e9f93f20d027d1edace1a5f4e744f5866417c8db06927e1023dd58ebb` |
| Unity result checker | `d7e981fcfc8165c5a721a7bffb6a8750028c9f58a9ac5661a51825a276b7f304` |

The configured Python 3.11 executable lacked `jsonschema`. Validation used a
pre-existing cached Python environment plus compatible user-site
`jsonschema 4.26.0` and `PyYAML 6.0.3`. No global package install or host
configuration mutation was performed. This proves the source in the observed
environment but leaves exact, hermetic offline dependency provisioning as debt.

## Loop Realization receipt

| Field | Realized state and evidence |
| --- | --- |
| Trigger | Owner requested real Agent contribution and repeated build-observe-correct-rebuild, not a paper simulation |
| State | Baseline had three core adapters and XFCM v0.2 but lacked real Unity input ingress, a concrete Inventory UGUI/XR composition and Settings persistence rehydration |
| Action | Three Builders authored disjoint typed adapters/tests; Supervisor integrated shared files, materialization, composition bindings and validator correction |
| Validation | Lane and Topic admission; fresh loop 8 Unity compile; 146 EditMode and 4 PlayMode cases; strict per-assembly XML acceptance; final 195 Python tests, XFCM check and canonical repository validation; independent read-only review |
| Writeback | This experiment preserves Agent attribution, failed observations, corrections, bounded claims and control-kernel debt; Git and Capsule own checkpoint/effect identity |
| Next Trigger | Add a composition-root lifecycle with startup rollback and reverse stop, provision an exact offline Python environment, or run a separately authorized named-device test; repair remote-only closure policy before expecting a no-push Goal to close administratively |

## Supported claims

- Three Agent contexts made distinct material C# and test contributions.
- The Supervisor integrated them into one materialized reference consumer.
- The typed adapters compile together and the exercised Editor paths pass all
  seven exact assembly suites: 146 EditMode and 4 PlayMode cases.
- XFCM content-binds seven implemented adapters and remains explicit that the
  system is not runtime/device ready.
- The loop repaired artifact defects and mechanism defects, then regenerated
  the affected evidence.
- The original dirty checkout remained isolated from this integration lane.

## Non-claims, validity threats and debt

- No player build, OpenXR runtime, headset, controller, comfort, performance,
  stereo readability or real UGUI appearance was observed.
- Synthetic direct UI events are not controller ray or hand-tracking evidence.
- The final consumer tests five selected package assemblies and two reference
  assemblies, not every behaviour in all 13 selected components.
- Three contexts under one orchestration lineage do not establish Agent
  membership, independent governance review, delegation, revocation or quorum.
- The Builder Topic claims lack a cycle foreign key, even though their path,
  attempt, lane and Agent attribution remain visible.
- One guarded write-set-expansion API exists but has no command-line surface;
  the Supervisor had to call the authority function directly.
- The configured offline Python runtime is incomplete and the compatible fallback
  is not an exact pinned environment.
- Final local checkpoint evidence is not remote durability. The current Goal
  explicitly prohibits push while the control kernel's non-empty-write closure
  requires remote containment. Effect may be recorded, but finish and close must
  remain unclaimed unless that policy conflict is resolved later.
- This experiment did not execute any wallet, token, treasury, chain, DAO
  activation, external account or remote repository operation.

## Stop interpretation

The engineering core may be accepted after the exact intended files are locally
checkpointed, the worktree is clean and the independent review remains valid for
that revision. Administrative completion is a separate layer. Under the present
no-push constraint, a remote-containment gate is expected to block that layer;
the truthful outcome is a locally accepted engineering effect plus explicit
administrative debt, not a false finish and not a claim that XR Foundry or its
future DAO is complete.
