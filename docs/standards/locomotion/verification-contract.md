# Locomotion and comfort verification contract

## Source gate

- Every derivation input is positive, public, and role-bounded in
  [`source-manifest.json`](source-manifest.json).
- No consumer project, private rig prefab, product comfort preset, or prior product
  locomotion implementation is a derivation input.
- A person confirms the source URLs, page paths, versions, and the maintenance
  state of each open-source implementation before the admission record is signed,
  because the authoring environment could not fetch them.

## Core gate

Deterministic tests must cover:

- locomotion mode identity: a closed set of modes (`teleport`, `snap_turn`,
  `smooth_turn`, `continuous_move`) with a canonical form, rejection of empty,
  whitespace, and malformed identifiers with a stable `identity.malformed` failure,
  and value equality between two identities with the same text; anchor identity
  (`AnchorId`) shares the same rules;
- a comfort policy as a closed set of typed options: vignette (`enabled` with an
  `intensity` declared as a closed numeric range), turn mode (`snap` or `smooth`,
  never both for one input source), turn increment (a declared closed set of
  degrees), movement speed (a declared closed numeric range), and posture
  (`seated` or `standing`); with fail-closed rejection of an unknown option
  (`option.unknown`), a value of the wrong kind (`option.kind.mismatch`), and an
  out-of-range or undeclared value (`option.out_of_range`) without changing state;
- locomotion intents applied to an immutable state: a teleport request to a
  registered anchor id, a turn by one declared increment, and a move vector for one
  tick with an explicit tick duration; an unknown anchor rejected with
  `anchor.unknown`, an intent whose mode the policy disables rejected with
  `mode.disabled`, and an intent that the current posture does not admit rejected
  with `posture.mismatch`;
- state immutability: a state is immutable, an accepted intent produces a new state
  and leaves the prior state intact, and a rejected intent leaves the prior state
  intact and returns it unchanged;
- deterministic replay: the same intent sequence over the same initial state and
  policy produces an equal final state and an equal fingerprint, and a state
  enumerates the active mode set, the comfort policy, the current anchor, the
  accumulated heading, and the accumulated planar offset; and
- structured results with stable failure codes for a malformed identity, an unknown
  option, a kind mismatch, an out-of-range value, an unknown anchor, a disabled
  mode, and a posture mismatch, plus a valid intent sequence passing clean with
  every outcome accepted.

The Core references no engine type, reads no input device, computes no world-space
transform, and renders nothing.

## Unity adapter gate

EditMode tests must cover:

- a thin binding of each Core mode to the matching XR Interaction Toolkit
  locomotion provider (teleportation, snap turn, continuous turn, continuous move)
  and of the vignette option to the tunneling vignette controller, with stable
  codes, field paths, and the source asset for a missing provider reference
  (`provider.missing`), a provider of the wrong mode (`provider.mode.mismatch`), a
  duplicate binding, and a missing vignette reference when the policy enables the
  vignette (`vignette.missing`), and construction that throws with the same report;
- an explicit diagnostic for every by-name or optional resolution: a provider
  member, an input action reference, or a body transformer that the adapter
  resolves by name, by serialized field, or by an optional lookup must be pinned to
  the validated toolkit version, must report an explicit failure with a stable code
  when the target is missing or incompatible, and must carry a negative test for the
  missing target (LESSON-004); silence or a reported success is a defect;
- application of an accepted comfort policy to the bound providers: the selected
  turn mode enables exactly one turn provider, the selected increment and speed are
  written to the matching provider settings, the vignette option is written to the
  vignette controller, and a rejected policy is never applied;
- a runtime constructed with explicit references that forwards accepted intents to
  the bound providers in the order they were accepted and reports accepted and
  rejected state, with no scene singleton, static instance, scene search, or
  reflection discovery; and
- two independent runtimes constructed side by side over two rigs that share no
  state and do not interfere with each other's providers.

No test claims that the rig moved, that the vignette rendered, or that any input
was read from a device.

## Claim ceiling

An EditMode run proves mode identity validation, comfort-policy rejection, intent
application, state immutability, deterministic replay, and provider binding
validation for the tuple recorded in the compatibility profile. It does not prove
motion sickness reduction, comfort, latency, frame timing, rig motion on a headset,
controller or hand input, tracking, boundary behaviour, or any named-device
behaviour. Every claim about motion sickness, comfort, latency, or device behaviour
requires a Device Lab receipt that binds the observation to a full commit SHA, the
exact dependency tuple, the posture, the measured duration, and the tester
identity; `not_tested` is never evidence.
