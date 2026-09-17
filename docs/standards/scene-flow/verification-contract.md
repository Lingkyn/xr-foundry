# Scene flow verification contract

## Source gate

- Every derivation input is positive, public, and role-bounded in
  [`source-manifest.json`](source-manifest.json).
- No consumer project, private scene list, loading-screen prefab, fade shader, or
  prior product scene-loader implementation is a derivation input.
- A person confirms the source URLs, page paths, versions, the current scene-list
  API of the pinned editor, and the maintenance state of each open-source
  implementation before the admission record is signed, because the authoring
  environment could not fetch them.

## Core gate

Deterministic tests must cover:

- scene set identity and scene identity: `SceneSetId` and `SceneId` with a
  canonical form (trimmed, non-empty, no whitespace or control characters, compared
  by canonical text), rejection of empty, whitespace, and malformed identifiers with
  a stable `identity.malformed` failure, and value equality between two identities
  with the same text;
- a scene graph that declares the sets, the member scenes of each set, exactly one
  active scene per set, and one fallback set; construction fails closed with
  `set.duplicate` for two sets with the same id, `scene.duplicate` for the same
  scene id declared twice within one set, `active.missing` for a set whose declared
  active scene is absent or is not a member, and `set.unknown` for a fallback set
  that is not declared; a scene may be a member of more than one set so that a
  persistent set and a content set can share nothing or share scenes explicitly;
- transition options as typed values: a fade-out duration, a hold duration, and a
  fade-in duration, each a non-negative duration where zero means the phase completes
  on the tick that enters it, and a negative or unparseable value rejected before
  any intent is applied;
- load, unload, and switch intents applied to an immutable flow state: `Load`
  adds a declared set additively, `Unload` removes a loaded set, and `Switch`
  replaces the current content set with another while leaving sets not involved in
  the switch loaded; the destination is loaded before the origin is unloaded, so the
  state never has zero loaded scenes mid-switch; an intent naming an undeclared set
  is rejected with `set.unknown`, an unload or switch away from a set that is not
  loaded is rejected with `scene.not_loaded`, and an intent that arrives while a
  transition is not `idle` is rejected with `transition.in_progress`;
- a transition state machine with the phases `idle`, `fading_out`, `loading`,
  `holding`, `fading_in`, and back to `idle`, advanced only by explicit intents: an
  elapsed-time intent carrying a duration advances `fading_out`, `holding`, and
  `fading_in` by that duration and completes a phase when its declared duration is
  reached; a load-completed intent carrying the scene id completes `loading` once
  every scene of the destination set has reported; a phase transition that the
  current phase does not admit (for example a load-completed intent while `idle`, or
  a completion for a scene that is not being loaded) is rejected with
  `transition.illegal`; the state enumerates the current phase, the phase elapsed
  time, the loaded sets, the loaded scenes, the active scene, and the pending
  destination;
- a rejected intent leaves the prior state intact and returns it unchanged, and an
  accepted intent produces a new state while the prior state remains readable and
  equal to itself;
- error recovery: a load-failed intent carrying the scene id and a reason during
  `loading` records `load.failed` in the result, abandons the pending destination,
  and starts a transition to the declared fallback set from the same phase; a
  load-failed intent while loading the fallback set itself records
  `fallback.failed` and returns the state to `idle` with the sets that were loaded
  before the failed transition, and never loops; and
- deterministic replay: the same intent sequence (including elapsed-time,
  load-completed, and load-failed intents) over the same graph and options produces
  an equal final state and an equal fingerprint, and structured results carry a
  stable failure code for every rejection listed here, plus a valid intent sequence
  passing clean with every outcome accepted.

The Core references no engine type, performs no I/O, reads no clock, starts no
coroutine or task, and renders nothing; time and load completion enter only as
intents.

## Unity adapter gate

EditMode tests must cover:

- a thin binding of each Core `SceneId` to one scene registered in the pinned
  editor's build scene list, held as an explicit reference or path, with stable
  codes, field paths, and the source asset for a scene missing from the build list
  or disabled in it (`scene.unregistered`), a scene id bound twice
  (`binding.duplicate`), a Core scene with no binding (`binding.missing`), and a
  missing fade surface reference when any fade duration is non-zero
  (`fade.missing`); construction throws with the same report, and a scene that is
  absent from the build list is never reported as loaded or silently skipped
  (LESSON-004);
- an explicit diagnostic for every by-name or optional resolution: a scene path,
  build index, scene-list API, loading operation, or activation flag that the
  adapter resolves by name, by serialized field, or by an optional lookup must be
  pinned to the validated editor version, must report an explicit failure with a
  stable code when the target is missing or incompatible, and must carry a negative
  test for the missing target; silence or a reported success is a defect;
- mapping of Core phases to engine operations through an explicit loader seam:
  `fading_out` drives the bound fade surface, `loading` issues additive
  asynchronous loads with activation held, `holding` keeps activation held until the
  hold duration and every load have completed, activation and the active-scene
  change happen at the end of `holding` and a refused active-scene change is
  reported as `active.refused`, unloads of the origin set are issued after
  activation, and `fading_in` drives the fade surface back; engine completion and
  failure are forwarded to the Core as load-completed and load-failed intents and
  never inferred;
- a runtime constructed with explicit references (graph, bindings, transition
  options, loader seam, fade surface) that forwards accepted intents in the order
  they were accepted and reports accepted and rejected state, with no scene
  singleton, static instance, survive-load object discovery, scene search, or
  reflection discovery; EditMode tests use a fake loader seam and load no scene; and
- two independent runtimes constructed side by side over two graphs that share no
  state and do not interfere with each other's loader or fade surface.

No test claims that a scene appeared, that a fade rendered, how long a load took,
or anything about frame timing during a transition.

## Claim ceiling

An EditMode run proves identity validation, graph construction, intent rejection,
transition-phase sequencing, state immutability, error recovery to the fallback set,
deterministic replay, and scene-binding validation for the tuple recorded in the
compatibility profile. It does not prove load time, hitching, dropped frames,
activation cost, memory behaviour, comfort during a transition, fade visibility, or
any named-device behaviour. Every claim about load time, hitching, or comfort during
a transition requires a Device Lab receipt that binds the observation to a full
commit SHA, the exact dependency tuple, the build target and graphics API, the
posture, the measured duration, and the tester identity; `not_tested` is never
evidence.
