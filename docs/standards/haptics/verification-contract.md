# Haptics verification contract

## Source gate

- Every derivation input is positive, public, and role-bounded in
  [`source-manifest.json`](source-manifest.json).
- No consumer project, private haptic clip, product profile asset, or prior
  product vibration code is a derivation input. The Interaction family's
  semantic intents and the Audio family's event ids in this repository are
  context only: they are the seams this family binds to, and they contribute
  no derivation input and no evidence for haptic identity, kind, or profile
  semantics.
- A person confirms the source URLs, page paths, section anchors, versions, the
  license, and the maintenance state of each open-source implementation before
  the admission record is signed, because the authoring environment could not
  fetch them.

## Core gate

Deterministic tests must cover:

- haptic identity: `HapticEventId` with a canonical form (lower-case segments
  joined by `.`), rejection of empty, whitespace, and malformed identifiers with
  a stable `identity.malformed` failure, and value equality between two
  identities with the same text; `HapticProfileId` shares the same rules and has
  no equality with a `HapticEventId` of the same text;
- a closed haptic-kind set with guard rails: `transient`, `continuous`, and
  `envelope`; a `HapticEventDefinition` declares (id, kind, an amplitude guard
  rail as a closed sub-range of `[0, 1]`, a duration guard rail as a closed,
  positive range in milliseconds, and, only for a kind that uses it, an optional
  frequency guard rail as a closed positive range in hertz); a declaration with
  an inverted range, a non-positive duration bound, an amplitude bound outside
  `[0, 1]`, or a frequency range on a kind that does not use frequency is
  rejected with `kind.declaration.invalid`;
- an immutable `HapticEventRegistry` built by explicit registration of (id,
  kind, amplitude guard rail, duration guard rail, optional frequency guard
  rail, label): registering an id twice is rejected with `event.duplicate`, a
  rejected registration leaves the registry unchanged, and a built registry
  enumerates its events in canonical id order and cannot be mutated afterwards;
- per-controller and per-hand `HapticProfile`s built the same explicit way: a
  closed `HapticTarget` set (`left`, `right`, `both`, and `named_device` with an
  opaque device-id string), and, per target present in the profile, a scale
  factor and an enable flag; a scale outside a documented positive closed range
  or a duplicate target within one profile is rejected with
  `profile.declaration.invalid`, and an unlisted target defaults to enabled with
  a scale of `1.0`;
- tuning intents applied to an immutable `HapticState`: `play` (event id,
  target, and optional amplitude, duration, and frequency overrides), `stop`
  (target), `stop_all`, and `set_profile` (profile id); `play` naming an
  unregistered event is rejected with `event.unknown`, naming an unregistered
  target (not in the closed set, or a `named_device` id no active profile
  declares) with `target.unknown`, supplying a frequency override on a kind that
  does not use frequency with `kind.mismatch`, an amplitude override (or, absent
  an override, the definition's default) outside the event's amplitude guard
  rail with `amplitude.out_of_range`, and a duration override (or default)
  outside the event's duration guard rail with `duration.out_of_range`;
  `set_profile` naming an unregistered profile id is rejected with
  `profile.unknown`;
- last-one-wins per target as the only state: a state holds, per target, at
  most one active playback (its event id and the effective amplitude, duration,
  and frequency the profile's scale was applied to) and the active profile id;
  a `play` accepted for a target that already holds an active playback replaces
  it; `stop` clears exactly the named target's active playback and is a no-op
  returning success when that target holds none; `stop_all` clears every
  target's active playback and never touches the active profile;
- state immutability: a state is immutable, an accepted intent produces a new
  state and leaves the prior state intact, and a rejected intent leaves the
  prior state intact and returns it unchanged with the failure code;
- deterministic replay: the same intent sequence over the same registry,
  profile set, and initial state produces an equal final state and an equal
  fingerprint; the fingerprint covers the event-registry fingerprint, the
  profile-set fingerprint, the active profile id, and the active-playback
  entries in canonical target order; two states reached by different intent
  sequences with the same net active playbacks and the same active profile have
  equal fingerprints;
- a binding table from other families' semantic identities to haptic events: a
  `HapticBinding` names one source kind (`interaction_intent` or
  `audio_event`), one source id (an opaque string this family never interprets,
  reusing the Interaction family's `IntentId` text or the Audio family's event
  identity text), one `HapticEventId`, and a default target; a binding whose
  haptic event id is not registered is rejected with `event.unknown`, and a
  second binding for the same source kind and source id is rejected with
  `binding.duplicate`; a validated binding table enumerates in canonical
  source-kind-then-source-id order and resolves one source kind and id to at
  most one binding;
- composed dispatch from one source intent: given a source kind and id, the
  binding table resolves independently to a haptic `play` intent (if bound), and
  the same source kind and id is separately resolvable by the Audio family's own
  binding and, once it exists, a future feedback-effect family's binding, so one
  grab or one hit dispatches haptics, audio, and a feedback effect from one
  source intent without any of the three blocking the others; a source kind and
  id with no haptic binding produces a structured `binding.unbound` result
  naming the source kind and id rather than silently doing nothing, and this
  result never prevents the audio or feedback dispatch for the same source
  intent;
- structured results with stable failure codes for a malformed identity, an
  invalid kind declaration, an invalid profile declaration, a duplicate event, a
  duplicate binding, an unknown event, an unknown target, a kind mismatch, an
  out-of-range amplitude, an out-of-range duration, an unknown profile, and an
  unbound source intent, each carrying the offending id, target, or source kind
  and id, plus a valid intent sequence passing clean with every outcome
  accepted.

The Core references no engine type, holds no `UnityEngine` type, reads no
asset, plays no sound, and drives no actuator; amplitude, duration, and
frequency are plain numeric values, and a `named_device` target's device id
enters and leaves the Core as an opaque string.

## Unity adapter gate

EditMode tests must cover:

- a thin adapter over two haptic-output routes, XR Interaction Toolkit haptic
  impulse channels and OpenXR haptic actions, with explicit target resolution:
  the adapter resolves each Core `HapticTarget` to one concrete route handle
  (an XRI haptic impulse channel or an OpenXR action-and-subaction-path pair)
  through an explicit, injected mapping rather than a scene search or a
  name-matching scan of active input devices, pinned to the validated XR
  Interaction Toolkit, OpenXR plugin, and Input System versions; a target the
  mapping does not resolve reports `target.unknown` with the target and the
  route name, and construction throws with the same report, carrying a
  negative test for the missing target (LESSON-004);
- an injectable `IHapticOutputSink` with an in-memory test double so tests
  assert exactly what was sent for every accepted intent — the resolved target,
  the effective amplitude, the effective duration, and the effective frequency
  (or its absence) — without a device or a route dependency; the runtime never
  calls a route directly, only through the sink, and a rejected intent reaches
  no sink;
- `HapticProfileAsset` as a `ScriptableObject` constructed with explicit
  references, converted deterministically to the Core's immutable
  `HapticProfile` without mutating the authored asset; an asset with a scale
  outside its documented range, a duplicate target, or an unlisted target field
  reports the Core's `profile.declaration.invalid` with the field path and the
  source asset, and construction throws with the same report;
- envelope playback degrading to a transient with a diagnostic: when the
  active runtime does not report the amplitude-envelope (or the PCM) OpenXR
  extension, the adapter plays an `envelope` event as a `transient` impulse
  using the envelope's declared peak amplitude and total duration and reports
  an explicit `envelope.unsupported.degraded` diagnostic naming the runtime and
  the substitution; it never reports that the requested `envelope` kind played
  when it did not;
- an explicit diagnostic for every by-name or optional resolution: a route
  handle, an `InputActionReference`, an `IDualMotorRumble`-capable device on the
  Input System fallback route, or an OpenXR extension capability query that the
  adapter resolves by name, by serialized field, or by an optional lookup must
  report an explicit failure with a stable code when the target is missing or
  unsupported, carrying a negative test for the missing target; silence or a
  reported success is a defect (LESSON-004);
- no polling: the runtime is driven only by explicit `play`, `stop`,
  `stop_all`, and `set_profile` calls it receives; it holds no per-frame update
  loop that resends an impulse, and a `continuous` kind's duration is the
  caller's explicit bound rather than a loop the adapter maintains on its own;
- construction with explicit references only: a runtime is built from an
  explicit registry, an explicit initial profile, an explicit binding table,
  and an explicit sink; no scene singleton, static instance, scene search, or
  reflection discovery resolves any of them; two independently constructed
  runtimes over two sinks that share no state are tested side by side, so a
  `play` in one changes no target, active playback, or sink call of the other;
- explicit route selection as configuration, never platform detection inside
  the family: the XRI route, the OpenXR route, and the Input System rumble
  fallback route are separate `IHapticOutputSink` implementations behind the
  one seam, and which one (or which ordered set, with the degrade path as a
  named fallback) a runtime uses is the consumer's explicit wiring at
  construction time, never a runtime check of the active platform or build
  target inside the adapter.

No test claims that a controller vibrated, that an amplitude or a pattern was
felt, or that any input was read from a device.

## Claim ceiling

An EditMode run proves identity validation, kind and profile declaration
validation, registry and profile-set construction, intent application,
last-one-wins per-target state, state immutability, deterministic replay,
binding-table resolution and unbound-intent reporting, route-target resolution,
profile-asset conversion, and the envelope degrade path, for the tuple recorded
in the compatibility profile. It proves the plumbing only: that an accepted
play, stop, stop-all, or set-profile intent reached the injectable output sink
with the exact target, amplitude, duration, and frequency the Core computed.
Felt intensity, comfort, fatigue, and whether a pattern reads as intended are
Device Lab receipts, never package claims; no test in this family's gates
claims a controller vibrated. Whether a haptic event is felt as authored,
whether its amplitude or duration is comfortable, and any named-device or
named-runtime behavior are human judgements recorded in a Device Lab receipt
that binds the observation to a full commit SHA, the exact dependency tuple,
the named runtime and controller, the profile and event-registry fingerprint,
the posture, the measured duration, the named input sources, and the tester
identity; `not_tested` is never evidence.
