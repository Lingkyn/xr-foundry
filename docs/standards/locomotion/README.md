# Locomotion and comfort package-family standard

Status: proposal in the source-gate queue (`NEXT-LOCOMOTION-COMFORT`); Core and Unity
adapter implementation staged in
[`staging/locomotion/`](../../../staging/locomotion/README.md); no package directory
or package id exists

This standard defines reusable locomotion mechanics: a closed set of locomotion
modes, a typed comfort policy, locomotion intents applied to an immutable state, and
deterministic replay, with a thin Unity XR Interaction Toolkit locomotion-provider
adapter planned separately. It does not define any product's comfort preset, level
layout, rig, or input bindings. It is derived only from the positive public sources
in [`source-manifest.json`](source-manifest.json).

## Capability boundary

The family separates:

- immutable locomotion mode identity (`teleport`, `snap_turn`, `smooth_turn`,
  `continuous_move`) from any provider, rig, or input that implements a mode;
- a comfort policy as a closed set of typed options (vignette enabled with an
  intensity range, turn mode as snap or smooth, a declared turn increment set, a
  declared movement speed range, and seated or standing posture) that rejects an
  unknown option, a wrong kind, or an out-of-range value from any product's preset
  values;
- locomotion intents that reference an anchor identity (`AnchorId`) for teleport, a
  declared increment for turn, and a normalized planar vector with a tick duration
  for movement, from any engine transform, scene object, or input axis;
- deterministic immutable state produced by a sequence of intents from any rig
  motion, physics, or rendering behaviour; and
- structured results with stable failure codes (`identity.malformed`,
  `option.unknown`, `option.kind.mismatch`, `option.out_of_range`,
  `anchor.unknown`, `mode.disabled`, `posture.mismatch`) from any settings screen,
  platform comfort rating, or editor tooling.

The family does not own the XR Origin, the body transformer, input reading,
collision or ground detection, the vignette rendering, boundary handling, fade art,
or any claim about motion sickness or comfort in a headset.

## Planned package boundary

| Layer | Owns | Must not own |
| --- | --- | --- |
| Engine-light Core (package id reserved only after admission) | mode and anchor identity; the comfort policy option set and its validation; teleport, turn, and move intents; immutable state; deterministic replay and fingerprint; structured results | Unity or toolkit types, transforms, input devices, rendering, product comfort presets, anchor placement |
| Unity XR Interaction Toolkit adapter (package id reserved only after admission) | binding of each mode to one toolkit locomotion provider and of the vignette option to the tunneling vignette controller with fail-closed validation and stable codes; explicit diagnostics for every by-name or optional resolution; application of an accepted policy to provider settings; a plain runtime constructed with explicit references | mode identity, option ranges, validation rules, intent semantics, scene singletons, static instances, scene search, reflection discovery |

## Evidence boundary

An EditMode test can prove mode identity validation, comfort-policy rejection,
intent application, state immutability, deterministic replay, and provider binding
validation for the declared tuple. It cannot prove that the rig moved, that the
vignette rendered, that input was read, or anything about motion sickness, comfort,
latency, frame timing, tracking, boundary, or named-device behaviour; those claims
require a Device Lab receipt as
[`verification-contract.md`](verification-contract.md) states in its claim ceiling.

## Lessons register dispositions (to be added to the register when the family goes live)

| Lesson | Disposition | Rationale |
| --- | --- | --- |
| LESSON-001 closed classification vs open tags | adopted | Locomotion modes, turn modes, postures, and option kinds are closed enums and anchor ids are validated identities; any consumer grouping of anchors or presets stays in open metadata outside Core |
| LESSON-002 migration path | deferred | No release exists yet; the first release records its compatibility policy and the first breaking change ships a migration note in the same commit |
| LESSON-003 single-workstation gate | adopted | Tests will run in `run_unity_gates.py` and the consumer workflow like every family; no gate depends on a maintainer workstation |
| LESSON-004 by-name resolution fails closed | adopted | The Core resolves nothing by name; the Unity adapter pins the toolkit version, reports every missing provider, input action reference, body transformer, or vignette controller with a stable code, and carries a negative test for each missing target |
| LESSON-005 clause coverage | deferred | [`coverage-map.json`](coverage-map.json) maps every Core and Unity adapter clause to named staged tests (18 of 19 covered, AU-01 partial); the tests are authored and unexecuted, so the disposition stays deferred until the first Unity gate runs them |
| LESSON-006 sibling pinning | deferred | Applies at the first Git-consumer validation; the README install matrix will pin every sibling to one full SHA |
| LESSON-007 skin seam | not_applicable | The family renders nothing; the vignette is a bound toolkit component, not a UI surface of this family |
| LESSON-008 version bump is a verification claim | adopted | No package version exists; the first scaffold enters at 0.1.0 and stays there until its first verified profile |

## Staged implementation

The first implementation of both layers lives in
[`staging/locomotion/`](../../../staging/locomotion/README.md) as staging material,
not a live package: `com.lingkyn.locomotion.core` (mode and anchor identity, the
comfort policy, the anchor registry, teleport/turn/move intents, immutable state,
results) and `com.lingkyn.locomotion.unity` (provider binding asset, binding
validation, provider surface seam, runtime). [`coverage-map.json`](coverage-map.json)
maps every Core and Unity adapter gate clause of the verification contract to named
tests. All of that code and every test is authored and unexecuted: nothing has
compiled or run until the first Unity gate records a receipt, and the coverage map is
a mapping, not execution evidence.

## Next steps

1. A person confirms every URL, page path, version pin, and open-source
   maintenance state in [`source-manifest.json`](source-manifest.json).
2. The maintainer copies [`admission.draft.json`](admission.draft.json) into
   `docs/foundry/admissions/` as the durable record through the
   [system admission gate](../../foundry/system-admission.md).
3. The staged implementation under `staging/locomotion/` and its
   [`coverage-map.json`](coverage-map.json) are authored against
   [`verification-contract.md`](verification-contract.md); nothing enters
   `packages/` before admission and a green Unity gate.

See also:

- [`verification-contract.md`](verification-contract.md)
- [`coverage-map.json`](coverage-map.json)
- [`source-manifest.json`](source-manifest.json)
- [`admission.draft.json`](admission.draft.json)
- [`next-batch.json`](../../foundry/queue/next-batch.json) queue entry and the
  [system admission gate](../../foundry/system-admission.md)
- [`lessons-register.json`](../lessons/lessons-register.json)
- [`audio/README.md`](../audio/README.md), the sibling family this standard is
  modelled on
