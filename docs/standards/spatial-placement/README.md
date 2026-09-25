# Spatial placement package-family standard

Status: incubating reference, source gate only

This standard defines spatial placement as a placement session with a closed
phase set (`idle`, `aiming`, `previewing`, `committed`, `cancelled`), a ghost
preview expressed as plain data (pose, validity, snap target) rather than a
rendered claim, a small set of intents (`begin`, `aim`, `adjust_distance`,
`snap`, `commit`, `cancel`) applied to an immutable state with deterministic
replay, a sticky aiming-hand rule, a distance-adjustment intent a consumer
routes from a secondary control while aiming, an input-ownership declaration
that names the shared control a live session claims and its arbitration kind,
and a validity policy expressed as data (surface kinds, reach range, overlap
rule). It does not define any product's placement content, art direction, or
judgement that a placement feels right, reads as reachable, or holds up under
hand tracking on a headset. It is derived only from the positive public
sources in [`source-manifest.json`](source-manifest.json), and no package id,
staged implementation, or catalog entry exists yet.

[`docs/benchmarks/shipped-game-gap-matrix.md`](../../benchmarks/shipped-game-gap-matrix.md)
ranks spatial placement sixteenth, from the shape-C benchmark (a room-scale
placement and editing tool): ray placement with a ghost preview, snapping,
commit, and cancel is core to that shape and present at least partially in the
object-interaction shape, and every shape needs some way to place an object
in space without either the object or the scene ending up in an invalid or
ambiguous state.

## Claim ceiling

Passing this family's gates proves the placement-session plumbing only: that
an accepted intent reached an injectable fake ray source, fake validity
probe, and ghost-preview renderer with the exact pose, validity, and snap
target the Core computed. Whether placement feels right, whether a reach
distance is comfortable, and whether hand-tracking aiming behaves as
documented are never package claims; each is a human judgement recorded in a
Device Lab receipt, as
[`verification-contract.md`](verification-contract.md) states in its own
claim ceiling.

## Capability boundary

The family separates:

- placement and hand identity (`PlacementSessionId`, the closed `Hand` set)
  from any interactor, controller, or hand-tracking device that raises an
  intent against them;
- a closed placement-phase set (`idle`, `aiming`, `previewing`, `committed`,
  `cancelled`) with fixed legal transitions, from any renderer state, UI
  panel, or animation that a consumer layers on top of a phase;
- a validity policy (closed surface-kind set, reach range, overlap rule)
  declared as plain data, from any raycast, collider, or surface-detection
  subsystem that supplies a candidate;
- `begin`, `aim`, `adjust_distance`, `snap`, `commit`, and `cancel` intents
  applied to an immutable `PlacementState` whose only content is, per active
  session, the owning hand, the phase, the current candidate, the ghost
  preview, and the input-ownership declaration, from any control, route, or
  device that raises them;
- a ghost preview (pose, validity, snap target) as plain data, from any
  ghost-object renderer, material, or shader a consumer's adapter supplies;
- an input-ownership declaration (a named shared control and an arbitration
  kind) as plain data the Core emits and never enforces, from the Locomotion
  family's own comfort-policy intents that a consumer's composition wiring
  turns that declaration into; and
- structured results with stable failure codes (`identity.malformed`,
  `policy.declaration.invalid`, `session.active`, `hand.mismatch`,
  `phase.invalid`, `session.unknown`, `placement.invalid_surface`,
  `placement.out_of_reach`, `placement.occupied`, `state.stale`) from any
  editor tooling, platform review, or Device Lab judgement.

The family is a placement-session substrate, not a placement rendering engine
or a surface-reconstruction system. It does not detect a real surface, does
not own the Interaction family's routing or the Locomotion family's comfort
policy, and makes no claim that a placement, a reach distance, or a
hand-tracking-driven aim is comfortable, legible, or correctly tracked on any
device.

## Why it composes

- **Interaction.** The Interaction family
  ([`docs/standards/interaction/verification-contract.md`](../interaction/verification-contract.md))
  owns semantic intent identity and multi-modal routing from ray, controller,
  and hand sources to handlers. A placement session's `begin` and its
  aim-updating intents are the target this family raises from an already
  routed ray or select intent; the Interaction family's routing, priority,
  and context-activation rules are not reimplemented here, and this family
  raises no route or context of its own.
- **Locomotion.** The Locomotion family
  ([`docs/standards/locomotion/verification-contract.md`](../locomotion/verification-contract.md))
  owns locomotion-mode identity and a comfort policy applied to an immutable
  state. This family's input-ownership declaration names the shared control
  (for example the same thumbstick a `continuous_move` or `smooth_turn` mode
  reads) and an arbitration kind while a session is `aiming` or `previewing`,
  so a consumer's own composition wiring can suppress that Locomotion mode
  for the session's lifetime; this family never calls Locomotion directly and
  holds no dependency on its package.
- **XR UI shell.** The XR UI shell
  ([`docs/standards/xr-ui-shell/verification-contract.md`](../xr-ui-shell/verification-contract.md))
  owns panel identity, layout, and the `fold` intent that keeps a folded
  panel's full open, docked-target, and follow state while changing only its
  visibility. This family exposes a plain session-live/session-ended
  notification a shell client may subscribe to in order to fold its own
  panels by alpha while a placement session is live, exactly the peer-client
  direction LESSON-010 requires: this family's Unity adapter holds no
  reference to any XR UI shell type, and the shell decides its own fold
  behaviour from the notification.
- **AR Foundation / surface detection.** A real surface-detection pipeline
  (for example a plane-detection subsystem) is a candidate source a
  consumer's adapter feeds into the Core's `aim` and `snap` intents as a
  surface kind and a pose; the Core owns no raycast, plane, or tracking type
  of its own.

## Planned package boundary

| Layer | Owns | Must not own |
| --- | --- | --- |
| Engine-light Core `com.lingkyn.spatial-placement.core` (package id reserved only after admission) | placement and hand identity; the closed placement-phase set and its legal transitions; the validity policy declaration and validation; `begin`, `aim`, `adjust_distance`, `snap`, `commit`, and `cancel` intents on an immutable state with deterministic replay; the sticky aiming-hand rule; the ghost preview as plain data; the input-ownership declaration as plain data; structured results | Unity types, `UnityEngine` types, raycasts, colliders, surface-detection subsystems, renderers, materials, product placement content, Interaction routing, Locomotion comfort policy, XR UI shell panels |
| Unity adapter `com.lingkyn.spatial-placement.unity` (package id reserved only after admission) | explicit binding to an XR Interaction Toolkit ray or near-far interactor and an interactable attach transform; an injectable `IPlacementRaySource` and `IPlacementValidityProbe`; explicit distance-adjustment wiring from a secondary control; an injectable `IGhostPreviewRenderer` that only receives Core data; emission of the input-ownership declaration as a plain event with no dependency on Locomotion; a plain session-live/session-ended notification a shell client may subscribe to, with no dependency on the XR UI shell; explicit diagnostics for every by-name or optional resolution | identity rules, phase and validity semantics, intent semantics, product placement content, Locomotion suppression logic, XR UI shell panel logic, scene singletons, static instances, scene search, reflection discovery, platform detection |

## Evidence boundary

An EditMode run can prove identity and policy-declaration validation, phase
and hand-ownership rules, candidate validation against a fake validity probe,
ghost-preview data propagation, input-ownership declaration emission and
clearing, state immutability, and deterministic replay, for the declared
tuple. It proves the plumbing only. It cannot prove that a real surface was
detected, that a hand was tracked, that a reach distance is comfortable, or
that a placement reads as intended on any device; those are human judgements
recorded in a Device Lab receipt, as
[`verification-contract.md`](verification-contract.md) states in its claim
ceiling.

## Lessons register dispositions (to be added to the register when the family goes live)

| Lesson | Disposition | Rationale |
| --- | --- | --- |
| LESSON-001 closed classification vs open tags | adopted | `PlacementSessionId` is a validated closed identity, and `Hand`, the placement-phase set, the validity policy's surface-kind set and overlap rule, and the input-ownership declaration's arbitration kind are closed enums; the only opaque string is the input-ownership declaration's shared-control identity and a `named_device`-style surface-kind extension is not admitted, so no freeform metadata drives phase or validity resolution |
| LESSON-002 migration path | deferred | No release exists yet; the first release records its compatibility policy, and the first breaking change to `PlacementSessionId`, the phase set, the intent set, or a stable failure code ships a migration note in the same commit |
| LESSON-003 single-workstation gate | adopted | Tests will run in `run_unity_gates.py` and the consumer workflow like every family; no gate in this standard depends on a maintainer workstation |
| LESSON-004 by-name resolution fails closed | adopted | The Core resolves nothing by name. The Unity adapter gate requires an explicit diagnostic and a negative test for every by-name or optional resolution: the ray-interactor or near-far-interactor reference, the interactable attach-transform reference, and the distance-adjustment `InputActionReference`, each pinned to the validated XR Interaction Toolkit version |
| LESSON-005 clause coverage | deferred | No staged implementation or tests exist; a future staged-implementation item writes `coverage-map.json` mapping every Core and Unity adapter clause to named tests before any clause is cited as evidence, modelled on `docs/standards/haptics/coverage-map.json` |
| LESSON-006 sibling pinning | deferred | Applies at the first Git-consumer validation; the README install matrix will pin the Core and Unity adapter packages, and any Interaction, Locomotion, or XR UI shell package a composition wires through the seams above, to one full commit SHA |
| LESSON-007 skin seam | not_applicable | The family renders nothing and owns no visual skin or theme seam; the ghost preview is plain pose-and-validity data consumed by a consumer's own renderer, not a shell slot or design-language token this family maps |
| LESSON-008 version bump is a verification claim | adopted | No package version exists; the first scaffold enters at 0.1.0 and stays there until its first verified compatibility profile |
| LESSON-009 tunable surface as data | not_applicable | The family owns no injectable skin/theme asset or other design-time configuration asset with a live-apply entry point; a `PlacementValidityPolicy` is authored, explicitly-registered configuration consumed once at construction, not a seam a live-tuning host re-invokes after a change |
| LESSON-010 developer tool is a peer client, never a dependency | adopted | The family's Unity adapter exposes an input-ownership declaration and a session-live/session-ended notification as plain data and events, and holds no reference to the Locomotion or XR UI shell assemblies; a consumer's own composition wiring, or the shell's own client code, is the one that depends on this family's public seam, never the reverse, exactly the direction LESSON-010 requires |
| LESSON-011 one intent channel for people and agents | adopted | The Core gate requires every `PlacementIntent` (`begin`, `aim`, `adjust_distance`, `snap`, `commit`, `cancel`) to carry an `IntentActor` and an optional expected revision from its first version, with `state.stale` rejecting a stale write before the intent's own rule runs, so a player-issued and an agent-issued copy of the same intent take the same path from the start rather than being retrofitted later |

## Next steps

1. A person confirms every URL, page path, section anchor, version pin,
   license, and open-source maintenance state in
   [`source-manifest.json`](source-manifest.json), and pins an AR Foundation
   version in this repository's compatibility profiles if the plane-detection
   source is to remain admitted.
2. The maintainer copies [`admission.draft.json`](admission.draft.json) into
   `docs/foundry/admissions/` as the durable record through the
   [system admission gate](../../foundry/system-admission.md).
3. A staged Core and Unity adapter, modelled on `staging/haptics/`, is
   authored against [`verification-contract.md`](verification-contract.md)
   with `coverage-map.json` mapping every Core and Unity adapter clause to a
   named test (no partial or unmapped clause). The code stays authored and
   unexecuted until a Unity run produces a compatibility profile, and nothing
   enters `packages/` before admission and a green gate.
4. A person or Agent with a Unity Editor runs
   `python scripts/run_unity_gates.py --host com.lingkyn.spatial-placement.core`,
   records the resulting compatibility profile, and either confirms the
   coverage map or turns it red.
5. The first Device Lab session for this family records placement feel, reach
   comfort, and hand-tracking behaviour as human observations bound to the
   validity-policy fingerprint, and never as a package claim.

See also:

- [`verification-contract.md`](verification-contract.md)
- [`source-manifest.json`](source-manifest.json)
- [`admission.draft.json`](admission.draft.json)
- [`next-batch.json`](../../foundry/queue/next-batch.json), the queue this
  candidate joins, and the
  [system admission gate](../../foundry/system-admission.md)
- [`lessons-register.json`](../lessons/lessons-register.json)
- [`interaction/verification-contract.md`](../interaction/verification-contract.md),
  the ray and semantic-intent source this family's placement intents compose on
- [`locomotion/verification-contract.md`](../locomotion/verification-contract.md),
  the comfort-policy provider this family's input-ownership declaration is
  expressed against
- [`xr-ui-shell/verification-contract.md`](../xr-ui-shell/verification-contract.md),
  the fold-retains-state intent this family's yield seam maps to
- [`shipped-game-gap-matrix.md`](../../benchmarks/shipped-game-gap-matrix.md),
  which ranks this family sixteenth from the creation-tool shape
- [`haptics/README.md`](../haptics/README.md) and
  [`quality-tiers/README.md`](../quality-tiers/README.md), sibling standards
  this family is modelled on
