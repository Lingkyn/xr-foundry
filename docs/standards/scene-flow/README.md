# Scene flow package-family standard

Status: proposal in the source-gate queue (`NEXT-SCENE-FLOW`); Core and Unity adapter
implementation staged in [`staging/scene-flow/`](../../../staging/scene-flow/README.md)

This standard defines reusable scene flow: scene set and scene identity, a declared
scene graph with one active scene per set, load, unload, and switch intents applied
to an immutable flow state through a transition state machine with typed fade and
hold durations, recovery to a declared fallback set, and deterministic replay, with a
thin Unity additive scene-loading adapter planned separately. It does not define any
product's scene list, level order, loading-screen content, fade art, or what is
restored after a switch. It is derived only from the positive public sources in
[`source-manifest.json`](source-manifest.json).

## Capability boundary

The family separates:

- immutable scene set identity (`SceneSetId`) and scene identity (`SceneId`) from
  any scene asset, path, build index, or addressable key that implements a scene;
- a scene graph that declares sets, their member scenes, exactly one active scene per
  set, and one fallback set, validated fail-closed at construction (`set.duplicate`,
  `scene.duplicate`, `active.missing`, `set.unknown`) from any project's build scene
  list or editor scene hierarchy;
- transition options as typed non-negative durations (fade out, hold, fade in) from
  any fade renderer, loading screen, or animation curve;
- load, unload, and switch intents applied to an immutable flow state through the
  phases `idle`, `fading_out`, `loading`, `holding`, `fading_in`, and back to
  `idle`, advanced only by explicit elapsed-time, load-completed, and load-failed
  intents, from any engine operation, coroutine, clock, or event;
- error recovery in which a failed load records `load.failed` and transitions to the
  fallback set, and a failed fallback records `fallback.failed` and returns to
  `idle` without looping, from any product's error screen or retry policy; and
- structured results with stable failure codes (`identity.malformed`,
  `set.duplicate`, `scene.duplicate`, `active.missing`, `set.unknown`,
  `transition.in_progress`, `transition.illegal`, `scene.not_loaded`,
  `load.failed`, `fallback.failed`) from any log line, exception, or engine return
  value.

The family does not own the scene assets, the build scene list, the loading-screen
content, the fade rendering, asset bundling, save-and-restore across a switch, or any
claim about load time, hitching, or comfort during a transition in a headset.

## Planned package boundary

| Layer | Owns | Must not own |
| --- | --- | --- |
| Engine-light Core (package id reserved only after admission) | scene set and scene identity; the scene graph and its validation; transition options; load, unload, and switch intents; the transition state machine; immutable flow state; error recovery to the fallback set; deterministic replay and fingerprint; structured results | Unity or package types, scene assets, paths, build indices, addressable keys, asynchronous operations, clocks, coroutines, rendering, loading-screen content |
| Unity additive scene-loading adapter (package id reserved only after admission) | binding of each scene id to one build-list scene with fail-closed validation and stable codes; explicit diagnostics for every by-name or optional resolution; mapping of Core phases to additive asynchronous loads with activation held, the active-scene change, unloads, and a fade surface through an explicit loader seam; forwarding of engine completion and failure as Core intents; a plain runtime constructed with explicit references | scene identity, graph rules, phase semantics, durations as defaults, scene singletons, static instances, survive-load object discovery, scene search, reflection discovery, loading-screen art, fade art |

An addressable-key binding is a separately validated sibling adapter with its own
compatibility profile, planned only after a person confirms that the pinned editor
tuple resolves the package; it is not part of the first Unity adapter.

## Evidence boundary

An EditMode test can prove identity validation, graph construction, intent
rejection, transition-phase sequencing, state immutability, error recovery to the
fallback set, deterministic replay, and scene-binding validation for the declared
tuple, using a fake loader seam that loads no scene. It cannot prove that a scene
appeared, that a fade rendered, how long a load took, or anything about hitching,
dropped frames, activation cost, memory, comfort, or named-device behaviour during a
transition; those claims require a Device Lab receipt as
[`verification-contract.md`](verification-contract.md) states in its claim ceiling.

## Lessons register dispositions (to be added to the register when the family goes live)

| Lesson | Disposition | Rationale |
| --- | --- | --- |
| LESSON-001 closed classification vs open tags | adopted | Transition phases and intent kinds are closed enums, and scene set and scene ids are validated identities; any consumer grouping of scenes (chapter, region, difficulty) stays in open metadata outside Core |
| LESSON-002 migration path | deferred | No release exists yet; the first release records its compatibility policy and the first breaking change ships a migration note in the same commit |
| LESSON-003 single-workstation gate | adopted | Tests will run in `run_unity_gates.py` and the consumer workflow like every family; the adapter's EditMode tests use a fake loader seam so no gate depends on a maintainer workstation or a scene list |
| LESSON-004 by-name resolution fails closed | adopted | The Core resolves nothing by name; the Unity adapter pins the editor version, reports every scene missing from or disabled in the build list as `scene.unregistered`, every unbound scene id, duplicate binding, missing fade surface, and refused active-scene change with a stable code, forwards engine failures as `load.failed` rather than inferring success, and carries a negative test for each missing target |
| LESSON-005 clause coverage | deferred | [`coverage-map.json`](coverage-map.json) maps every Core and Unity adapter clause to named staged tests (18 of 19 covered, CU-01 partial); the tests are authored and unexecuted, so the disposition stays deferred until the first Unity gate runs them |
| LESSON-006 sibling pinning | deferred | Applies at the first Git-consumer validation; the README install matrix will pin every sibling to one full SHA |
| LESSON-007 skin seam | not_applicable | The family renders nothing; the fade surface is an injected seam that the consumer's own renderer implements, and loading-screen content is consumer-owned |
| LESSON-008 version bump is a verification claim | adopted | No package version exists; the first scaffold enters at 0.1.0 and stays there until its first verified profile |

## Staged implementation

The first implementation of both layers lives in
[`staging/scene-flow/`](../../../staging/scene-flow/README.md) as staging material,
not a live package: `com.lingkyn.scene-flow.core` (scene set and scene identity, the
scene graph, transition options, intents, the transition state machine, immutable
state, error recovery, deterministic replay, results) and
`com.lingkyn.scene-flow.unity` (scene binding asset, binding validation, the
`ISceneLoaderSurface` seam, the runtime). [`coverage-map.json`](coverage-map.json)
maps every Core and Unity adapter gate clause of the verification contract to named
tests. All of that code and every test is authored and unexecuted: nothing has
compiled or run until the first Unity gate records a receipt, and the coverage map
is a mapping, not execution evidence.

## Next steps

1. A person confirms every URL, page path, version pin, the current scene-list API
   of the pinned editor, and the open-source maintenance state in
   [`source-manifest.json`](source-manifest.json).
2. The maintainer copies [`admission.draft.json`](admission.draft.json) into
   `docs/foundry/admissions/` as the durable record through the
   [system admission gate](../../foundry/system-admission.md).
3. A person with a Unity Editor runs `python scripts/scaffold_unity_package.py`
   against the admitted blueprints, replaces the generated scaffold sources with the
   files staged under `staging/scene-flow/`, and runs
   `python scripts/run_unity_gates.py` to turn the authored, unexecuted code into a
   verified compatibility profile; nothing enters `packages/` before admission and a
   green gate.

See also:

- [`verification-contract.md`](verification-contract.md)
- [`coverage-map.json`](coverage-map.json)
- [`source-manifest.json`](source-manifest.json)
- [`admission.draft.json`](admission.draft.json)
- [`next-batch.json`](../../foundry/queue/next-batch.json) queue entry and the
  [system admission gate](../../foundry/system-admission.md)
- [`lessons-register.json`](../lessons/lessons-register.json)
- [`audio/README.md`](../audio/README.md) and
  [`locomotion/README.md`](../locomotion/README.md), the sibling families this
  standard is modelled on
