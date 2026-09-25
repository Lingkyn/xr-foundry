# Spatial placement verification contract

## Source gate

- Every derivation input is positive, public, and role-bounded in
  [`source-manifest.json`](source-manifest.json).
- No consumer project, private placement prefab, product snapping rule, or prior
  product placement code is a derivation input. The Interaction family's routing
  model, the Locomotion family's mode and comfort-policy model, and the XR UI
  shell's fold-retains-state intent in this repository are context only: they are
  the seams this family composes on, and they contribute no derivation input and
  no evidence for placement identity, phase, or validity semantics.
- A person confirms the source URLs, page paths, section anchors, versions, the
  license, and the maintenance state of each open-source implementation before
  the admission record is signed, because the authoring environment could not
  fetch them.

## Core gate

Deterministic tests must cover:

- placement identity: `PlacementSessionId` with a canonical form (lower-case
  segments joined by `.`), rejection of empty, whitespace, and malformed
  identifiers with a stable `identity.malformed` failure, and value equality
  between two identities with the same text; a closed `Hand` set (`left`,
  `right`) identifies which hand a session belongs to and shares the same
  malformed-rejection rule for any value outside the closed set;
- a closed placement-phase set with fixed transitions: `idle`, `aiming`,
  `previewing`, `committed`, `cancelled`; a session exists only while its phase
  is `aiming`, `previewing`, or `committed` (a `committed` or `cancelled`
  session is terminal and immediately removable), and every intent names the
  phase it is legal from; an intent issued against a phase it is not legal from
  is rejected with `phase.invalid` and changes nothing;
- a validity policy declared as plain data, never an engine type: a
  `PlacementValidityPolicy` declares a closed, non-empty surface-kind set drawn
  from a small closed vocabulary (for example `floor`, `wall`, `table`,
  `ceiling`, `unclassified`), a reach range as a closed positive range in
  metres, and an overlap rule from a closed set (`reject`, `allow`,
  `reduce_to_fit`); a declaration with an empty surface-kind set, an inverted or
  non-positive reach range, or an overlap rule outside the closed set is
  rejected with `policy.declaration.invalid`;
- `begin` opens a session for one hand under one validity policy and transitions
  it to `aiming`; a `begin` naming a hand that already owns an active session is
  rejected with `session.active` and the prior session is untouched;
- the aiming hand is sticky to the hand that began the session: every
  subsequent `aim`, `adjust_distance`, `snap`, `commit`, or `cancel` intent
  names the session id and the issuing hand, and an intent whose hand does not
  match the session's owning hand is rejected with `hand.mismatch` and changes
  nothing, so a second hand can never steer, distance-adjust, or resolve a
  session it did not begin;
- `aim` supplies a candidate pose and a candidate surface kind (or none) and is
  legal only in `aiming` or `previewing`; the policy's surface-kind set, reach
  range, and overlap rule are evaluated against the candidate in that order,
  and the first failing check is reported as `placement.invalid_surface` (the
  candidate's surface kind is not in the policy's set), `placement.out_of_reach`
  (the candidate pose's distance from the session's own reach origin falls
  outside the reach range), or `placement.occupied` (the overlap rule is
  `reject` and the candidate is reported occupied); an accepted `aim` stays in
  `aiming` and updates the session's current pose and validity regardless of
  whether the candidate passed, because aiming itself never fails, only the
  candidate's validity does;
- `adjust_distance` carries a signed numeric delta a consumer routes from a
  secondary control (for example a thumbstick axis) while aiming, and moves the
  session's own reach-along-ray distance by that delta clamped to the policy's
  reach range; it is legal only in `aiming` or `previewing` and is rejected
  with `phase.invalid` otherwise; it never changes the candidate pose's lateral
  aim, only how far along the current aim direction the candidate sits;
- a ghost preview as data, never a rendered claim: at every point in `aiming` or
  `previewing` the session's state exposes a `GhostPreview` value (the current
  candidate pose, the current validity result, and, once `snap` has resolved
  one, the current snap-target pose or its absence) that is plain data with no
  drawing, material, or renderer type; it is present in every reachable
  `aiming` and `previewing` state and absent in `idle`, `committed`, and
  `cancelled`;
- `snap` re-validates the current candidate exactly as `aim` does and, only
  when it passes, transitions `aiming` to `previewing` and resolves a snap
  target pose from the policy's declared surface-kind and overlap
  configuration, recorded in the ghost preview; a `snap` on a failing candidate
  is rejected with the same `placement.invalid_surface`,
  `placement.out_of_reach`, or `placement.occupied` code `aim` would report and
  the session stays in `aiming`;
- `commit` re-validates the previewed candidate one more time before it is
  allowed to end the session: legal only in `previewing`, it is rejected with
  `phase.invalid` from any other phase, and a candidate that no longer passes
  the policy (because an intervening `aim` changed it without a further `snap`)
  is rejected with the same placement failure code and the session stays in
  `previewing`; an accepted `commit` transitions to `committed` and the session
  reports its final resolved pose exactly once;
- `cancel` is legal from `aiming`, `previewing`, or `committed`'s immediate
  predecessor states and always succeeds, transitioning to `cancelled`
  unconditionally and discarding the candidate pose and any resolved snap
  target; a `cancel` naming an unknown session id is rejected with
  `session.unknown`;
- an input-ownership declaration as data, not a suppression call: a session
  entering `aiming` carries an `InputOwnershipDeclaration` naming one shared
  control identity (an opaque string this family never interprets, for example
  a thumbstick or trigger identity a consumer's own binding names) and one
  arbitration kind from a closed set (`exclusive_suppress`,
  `priority_yield`); the declaration is present for every `aiming` and
  `previewing` state and absent once the session reaches `committed` or
  `cancelled`, so a consumer can read, at any time, exactly which control a
  live session claims and how; the Core issues no suppression itself and holds
  no reference to any other family's runtime;
- state immutability: a `PlacementState` is immutable, an accepted intent
  produces a new state and leaves the prior state intact, and a rejected intent
  leaves the prior state intact and returns it unchanged with the failure code;
- deterministic replay: the same intent sequence over the same initial state
  and validity policy produces an equal final state and an equal fingerprint;
  the fingerprint covers the policy fingerprint and, per active session, the
  owning hand, the phase, the current candidate pose and validity, the current
  ghost preview, and the input-ownership declaration; two states reached by
  different intent sequences with the same net active sessions have equal
  fingerprints;
- one intent channel for people and agents (LESSON-011): every
  `PlacementIntent` (`begin`, `aim`, `adjust_distance`, `snap`, `commit`,
  `cancel`) carries an `IntentActor` from the closed set `player`, `agent`,
  `replay`, `import`, and an optional expected revision; `PlacementState`
  carries a monotonically increasing revision; a stale expected revision is
  rejected with `state.stale` before the intent's own rule runs and changes
  nothing; the actor never changes validation, so a player-issued and an
  agent-issued copy of the same intent take the same path and yield the same
  outcome and the same resulting state; and every outcome in the replay log
  carries the issuing actor and the revision after it;
- structured results with stable failure codes for a malformed identity, an
  invalid policy declaration, an active-session conflict, a phase violation
  (`phase.invalid`), a hand mismatch, an unknown session, an invalid surface,
  an out-of-reach candidate, an occupied candidate, and a stale expected
  revision, each carrying the offending session id, hand, or field, plus a
  valid intent sequence passing clean with every outcome accepted.

The Core references no engine type, holds no `UnityEngine` type, reads no input
device, computes no raycast, and renders nothing; a pose is a plain
position-and-rotation value, a surface kind and an arbitration kind are plain
closed values, and the shared-control identity in an input-ownership
declaration enters and leaves the Core as an opaque, validated string.

## Unity adapter gate

EditMode tests must cover:

- a thin adapter over an XR Interaction Toolkit ray interactor (or near-far
  interactor) and an interactable attach transform, with explicit references
  resolved through an injected accessor rather than a scene search or a
  singleton lookup, pinned to the validated XR Interaction Toolkit version; a
  missing ray-source or attach-transform reference reports `ray.unresolved` or
  `attach.unresolved` naming the field path and the source asset, and
  construction throws with the same report, carrying a negative test for each
  missing target (LESSON-004);
- an injectable `IPlacementRaySource` and `IPlacementValidityProbe` with
  in-memory test doubles so tests drive `aim`, `adjust_distance`, and `snap`
  from a fully controlled fake pose and a fake validity result without a
  device, a raycast, or a runtime surface-detection dependency; the runtime
  never reads the ray interactor or evaluates validity directly, only through
  these two seams, and a rejected intent reaches neither;
- explicit distance-adjustment wiring from a secondary control: the adapter
  reads one `InputActionReference` (resolved explicitly, never by name or scene
  search) and forwards its scalar value as the Core's `adjust_distance` delta
  on the session the local hand owns; an unresolved or missing action
  reference reports `distance.control.unresolved` with the field path, and
  construction throws with the same report, carrying a negative test for the
  missing reference (LESSON-004);
- a ghost-renderer seam that only receives, never decides: an injectable
  `IGhostPreviewRenderer` is called with exactly the Core's own `GhostPreview`
  value (the candidate pose, the validity result, and the snap-target pose or
  its absence) on every accepted `aim`, `adjust_distance`, or `snap`, and is
  never called with a value the renderer itself computed, clamped, or
  smoothed; a test double records every call and asserts it matches the Core
  state exactly;
- an input-ownership seam with no suppression logic and no dependency on
  another family: the adapter exposes the Core's `InputOwnershipDeclaration` as
  a plain event or read value on session start and on session end, and the
  Unity adapter assembly holds no reference to the Locomotion package or any
  other family's runtime; a consumer's own composition wiring, not this
  family, turns that declaration into a Locomotion comfort-policy change, and
  the adapter's own tests assert only that the declaration payload is emitted
  and cleared at the correct points;
- a fold-or-yield seam the XR UI shell may implement, never a dependency this
  family holds: the adapter exposes a plain session-live/session-ended
  notification (a value or an event, not a shell type) that an XR UI shell
  client may subscribe to in order to fold its own panels by alpha while a
  placement session is live, exactly the peer-client direction LESSON-010
  requires; the Unity adapter assembly references no XR UI shell type, and a
  source-level test proves no such reference exists;
- construction with explicit references only and no polling: a runtime is
  built from an explicit validity-policy set, an explicit ray source, an
  explicit validity probe, an explicit ghost-preview renderer, and an explicit
  distance-control reference; no scene singleton, static instance, scene
  search, or reflection discovery resolves any of them; the runtime is driven
  only by explicit `begin`, `aim`, `adjust_distance`, `snap`, `commit`, and
  `cancel` calls it receives and holds no per-frame update loop that
  re-evaluates validity on its own; two independently constructed runtimes
  over two sets of fakes are tested side by side, so a session begun in one
  changes no phase, candidate, or fake call of the other;
- explicit route selection as configuration, never platform detection inside
  the family: the ray-interactor route and the near-far-interactor route are
  separate `IPlacementRaySource` implementations behind the one seam, and
  which one a runtime uses is the consumer's explicit wiring at construction
  time, never a runtime check of the active platform or build target inside
  the adapter.

No test claims that a surface was detected, that a hand was tracked, that a
placement felt comfortable, or that any input was read from a device.

## Claim ceiling

An EditMode run proves identity validation, phase-transition and policy
validation, session and hand-ownership rules, candidate validation against a
fake validity probe, ghost-preview data propagation, input-ownership
declaration emission and clearing, state immutability, and deterministic
replay, for the tuple recorded in the compatibility profile. It proves the
plumbing only: that an accepted begin, aim, adjust_distance, snap, commit, or
cancel intent reached the injectable fake ray source, fake validity probe, and
ghost-preview renderer with the exact pose, validity, and snap target the Core
computed. Whether placement feels right, whether a reach distance is
comfortable, and whether hand-tracking aiming behaves as documented on any
device are human judgements recorded in a Device Lab receipt that binds the
observation to a full commit SHA, the exact dependency tuple, the named
device, runtime, and input source, the validity-policy fingerprint, the
posture, the measured duration, the procedure, and the tester identity;
`not_tested` is never evidence.
