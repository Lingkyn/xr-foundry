# Haptics package-family standard

Status: proposal in the source-gate queue (`NEXT-HAPTICS`); no Core or Unity
adapter implementation is staged yet, and no package directory or package id
exists.

This standard defines controller and hand haptic feedback as a validated
event identity, a closed haptic-kind set, per-controller and per-hand
profiles, and a small set of intents applied to an immutable state, with a
binding table so the Interaction family's semantic intents and the Audio
family's event ids can drive haptics from the same source event that already
drives audio, without either family depending on this one. It does not define
any product's haptic feel, authored clip content, or judgement that a pattern
reads as intended in a headset. It is derived only from the positive public
sources in [`source-manifest.json`](source-manifest.json).

[`docs/benchmarks/shipped-game-gap-matrix.md`](../../benchmarks/shipped-game-gap-matrix.md)
ranks haptics twelfth and records why it outranks every other remaining
candidate: every one of the three benchmark product shapes (rhythm action,
object interaction, spatial creation) needs a controller to buzz on the first
grab or the first hit, in the first minute, on every headset the product
ships on.

## Capability boundary

The family separates:

- immutable haptic identity (`HapticEventId`, `HapticProfileId`) from any
  clip, asset, route, or device that plays a haptic event;
- a closed haptic-kind set (`transient`, `continuous`, `envelope`) with
  per-event amplitude, duration, and optional frequency guard rails, from any
  engine amplitude, curve, or waveform type;
- an immutable `HapticEventRegistry` built by explicit registration that
  rejects a duplicate id and an invalid guard-rail declaration, from any
  reflection, attribute scan, or asset discovery;
- per-controller and per-hand `HapticProfile`s over a closed target set
  (`left`, `right`, `both`, `named_device`) with a scale and an enable flag
  per target, from any concrete controller type or input device;
- play, stop, stop-all, and set-profile intents applied to an immutable
  `HapticState` whose only content is, per target, the active playback and the
  active profile, from any control, route, or device that raises them;
- a binding table from the Interaction family's semantic intent identities and
  the Audio family's event identities to haptic events, from the routing or
  mixing logic each of those families owns; and
- structured results with stable failure codes (`identity.malformed`,
  `kind.declaration.invalid`, `profile.declaration.invalid`, `event.duplicate`,
  `binding.duplicate`, `event.unknown`, `target.unknown`, `kind.mismatch`,
  `amplitude.out_of_range`, `duration.out_of_range`, `profile.unknown`,
  `binding.unbound`) from any editor tooling or platform review.

The family is a feedback channel, not an authoring tool. It does not author,
import, or edit a proprietary haptic clip format, does not own the
Interaction family's routing or the Audio family's mix graph, and makes no
claim that a pulse, an envelope, or a pattern is felt, is comfortable, or
reads as intended in a headset.

## Why it composes

- **Interaction.** The Interaction family
  ([`docs/standards/interaction/README.md`](../interaction/README.md)) owns
  semantic intent identity and routing from input sources to handlers. A
  `HapticBinding` names one of its intent identities as a source and never
  reimplements routing, priority, or context activation; the binding table
  only says which haptic event, if any, that intent also fires.
- **Audio.** The Audio family
  ([`docs/standards/audio/README.md`](../audio/README.md)) owns event
  identity, mix state, and spatial attachment. A `HapticBinding` can name one
  of its event identities as a source under the same rule, so one grab or one
  hit fires an audio event and a haptic event from one source intent without
  either family calling the other.
- **Feedback effects (future).** The shipped-game gap matrix records a
  planned, still-missing Feedback effects family (visual hit, spawn, and
  dissolve effects) that composes on Audio and Haptics the same way. This
  family's binding table is written so a third source kind can be added
  later without changing the Core's play, stop, stop-all, or set-profile
  semantics.
- **XR UI shell / Live tuning.** Neither applies here: haptics has no panel
  and no visual token, so it holds no dependency on either family and exposes
  no tunable surface under LESSON-009.

## Planned package boundary

| Layer | Owns | Must not own |
| --- | --- | --- |
| Engine-light Core `com.lingkyn.haptics.core` (package id reserved only after admission) | haptic and profile identity; the closed haptic-kind set and guard-rail validation; the immutable event registry and explicit registration; profile declarations over the closed target set; intents, immutable state, last-one-wins per-target playback, deterministic replay, and fingerprint; the binding table from other families' semantic identities to haptic events and unbound-intent reporting; structured results | Unity types, `UnityEngine` types, assets, actuators, clips, waveforms, routes, product profile values, Interaction routing, Audio mixing |
| Unity adapter `com.lingkyn.haptics.unity` (package id reserved only after admission) | explicit target resolution to XR Interaction Toolkit haptic impulse channels and OpenXR haptic actions, with an Input System rumble fallback route, each behind the injectable `IHapticOutputSink`; explicit diagnostics for every by-name or optional resolution; `HapticProfileAsset` as a `ScriptableObject` constructed with explicit references; the envelope-to-transient degrade path with a diagnostic when the runtime lacks the extension | identity rules, kind and profile semantics, intent semantics, binding-table rules, product profile values, scene singletons, static instances, scene search, reflection discovery, platform detection |

## Evidence boundary

An EditMode run can prove identity and kind/profile validation, registry and
profile-set construction, intent application, last-one-wins per-target state,
immutability, deterministic replay, binding-table resolution and
unbound-intent reporting, route-target resolution, profile-asset conversion,
and the envelope degrade path, for the declared tuple. It proves the plumbing
only. It cannot prove that a pulse, an envelope, or a pattern is felt, is
felt at a comfortable intensity, causes fatigue, or reads as intended, or
anything about a named controller's or runtime's actual vibration behavior;
whether a haptic event is felt as authored is a human judgement recorded in a
Device Lab receipt, as
[`verification-contract.md`](verification-contract.md) states in its claim
ceiling.

## Lessons register dispositions (to be added to the register when the family goes live)

| Lesson | Disposition | Rationale |
| --- | --- | --- |
| LESSON-001 closed classification vs open tags | adopted | Haptic and profile ids are validated identities, and haptic kinds and haptic targets are closed enums; the only open metadata is a label on an event or profile declaration and the opaque device-id string on a `named_device` target, neither of which drives validation or intent resolution |
| LESSON-002 migration path | deferred | No release exists yet; the first release records its compatibility policy, and the first breaking change to `HapticEventId`, the kind set, the target set, or a stable failure code ships a migration note in the same commit |
| LESSON-003 single-workstation gate | adopted | Tests will run in `run_unity_gates.py` and the consumer workflow like every family; no gate in this standard depends on a maintainer workstation |
| LESSON-004 by-name resolution fails closed | adopted | The Core resolves nothing by name. The Unity adapter gate requires an explicit diagnostic and a negative test for every by-name or optional resolution: a route handle, an `InputActionReference`, an `IDualMotorRumble`-capable device on the Input System fallback, or an OpenXR extension capability query, each pinned to the validated engine, XR Interaction Toolkit, OpenXR plugin, and Input System versions |
| LESSON-005 clause coverage | deferred | No staged tests exist; the staged implementation item writes `coverage-map.json` mapping every Core and Unity adapter clause to named tests before any clause is cited as evidence |
| LESSON-006 sibling pinning | deferred | Applies at the first Git-consumer validation; the README install matrix will pin the Core and Unity adapter packages, and any Interaction or Audio package a composition binds through the binding table, to one full commit SHA |
| LESSON-007 skin seam | not_applicable | The family renders nothing and owns no visual skin or theme seam; it drives a haptic actuator, not a renderer |
| LESSON-008 version bump is a verification claim | adopted | No package version exists; the first scaffold enters at 0.1.0 and stays there until its first verified compatibility profile |
| LESSON-009 tunable surface as data | not_applicable | The family exposes no injectable skin/theme or live-apply config seam a developer-tuning scaffold would attach to; `HapticProfileAsset` is authored design-time data converted once into the Core's immutable profile, not a live-apply seam re-invoked by a runtime host |
| LESSON-010 developer tool is a peer client, never a dependency | adopted | This family is not a developer or tooling system; it is a product-facing feedback channel that Interaction and Audio bind to through opaque source ids in the binding table, and it holds no dependency on either family's assembly, exactly the direction LESSON-010 requires between a client and the systems it composes on |

## Next steps

1. A person confirms every URL, page path, section anchor, version pin,
   license, and open-source maintenance state in
   [`source-manifest.json`](source-manifest.json).
2. The maintainer copies [`admission.draft.json`](admission.draft.json) into
   `docs/foundry/admissions/` as the durable record through the
   [system admission gate](../../foundry/system-admission.md).
3. The staged implementation authors the Core and the Unity adapter under
   `staging/haptics/` against
   [`verification-contract.md`](verification-contract.md) and writes a
   `coverage-map.json` mapping every clause to a named test; the code is
   authored and unexecuted until a Unity run produces a compatibility
   profile, and nothing enters `packages/` before admission and a green gate.
4. The first Device Lab plan for this family records a haptic session as a
   human judgement of felt intensity, comfort, and pattern legibility, bound
   to the profile and event-registry fingerprint, and never as a package
   claim.

See also:

- [`verification-contract.md`](verification-contract.md)
- [`source-manifest.json`](source-manifest.json)
- [`admission.draft.json`](admission.draft.json)
- [`next-batch.json`](../../foundry/queue/next-batch.json), the queue this
  candidate joins, and the
  [system admission gate](../../foundry/system-admission.md)
- [`lessons-register.json`](../lessons/lessons-register.json)
- [`interaction/README.md`](../interaction/README.md), the semantic-intent
  source this family's binding table can name
- [`audio/README.md`](../audio/README.md), the event-identity source this
  family's binding table can name
- [`shipped-game-gap-matrix.md`](../../benchmarks/shipped-game-gap-matrix.md),
  which ranks this family twelfth and states why every product shape needs it
- [`live-tuning/README.md`](../live-tuning/README.md) and
  [`xr-ui-shell/README.md`](../xr-ui-shell/README.md), sibling standards this
  family is modelled on
