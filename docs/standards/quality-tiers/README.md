# Quality tiers package-family standard

Status: the staged implementation exists under `staging/quality-tiers/`
(`com.lingkyn.quality-tiers.core` and `com.lingkyn.quality-tiers.unity`),
authored and unexecuted against
[`verification-contract.md`](verification-contract.md), with
`coverage-map.json` mapping every clause to a named test (two Unity adapter
clauses, QU-01 and QU-05, are `partial` with a named Device Lab receipt gap;
every other clause is `covered`). No Unity run has produced a compatibility
profile yet, so no package directory, package id, catalog entry, maturity,
release, or device status exists in the tree.

This standard defines performance and quality-tier management as a validated
tier identity, a closed per-tier declaration (refresh rate, render scale,
foveation level, MSAA, shadow and post-processing budgets, each with a guard
rail), per-device-profile capability descriptors that gate a tier selection
closed rather than letting a device silently ignore an unsupported request,
and a small set of intents applied to an immutable state, with a frame-budget
policy expressed as plain data and a hook the Settings family may consume so a
user-facing quality option maps to a tier. It does not define any product's
frame-rate target, device support list, or judgement that a build holds a
stable frame rate or passes store certification. It is derived only from the
positive public sources in [`source-manifest.json`](source-manifest.json), and
no package id, staged implementation, or catalog entry exists yet.

[`docs/benchmarks/shipped-game-gap-matrix.md`](../../benchmarks/shipped-game-gap-matrix.md)
ranks quality tiers fourteenth and records why every one of the three
benchmark product shapes (rhythm action, object interaction, spatial
creation) needs it: store certification requires a stable frame rate on every
headset a title ships on, and a title that does not manage render scale,
foveation, and rendering budgets per device fails that requirement on some
device generation even when it holds the target on others.

## Capability boundary

The family separates:

- immutable tier and device identity (`TierId`, `DeviceProfileId`) from any
  renderer, subsystem, or device that applies a tier;
- a closed per-tier declaration (refresh rate, render scale, foveation level,
  MSAA, shadow budget, post-processing budget, each with a guard rail) from
  any engine setting, asset, or shader type;
- an immutable `TierRegistry` built by explicit registration that rejects a
  duplicate id and an invalid guard-rail declaration, from any reflection,
  attribute scan, or asset discovery;
- a `DeviceCapabilityDescriptor` per device profile (closed supported
  refresh-rate, render-scale, foveation-level, and MSAA sets) that fails a
  tier selection closed at select time when a device does not support what a
  tier asks for, from any silent runtime clamp or ignore;
- select-tier, set-override, reset, and apply-preset intents applied to an
  immutable `QualityState` whose only content is, per device profile, the
  selected tier and its overrides, from any control, route, or device that
  raises them;
- a frame-budget policy (target frame time, drop threshold) as plain data,
  from any renderer, subsystem, or measurement type; and
- structured results with stable failure codes (`identity.malformed`,
  `tier.declaration.invalid`, `capability.declaration.invalid`,
  `budget.declaration.invalid`, `tier.duplicate`, `tier.unknown`,
  `device.unknown`, `capability.unsupported`, `value.out_of_range`,
  `state.stale`) from any editor tooling, platform review, or store
  certification process.

The family is a performance-configuration substrate, not a benchmarking tool,
a frame-rate optimizer, or an automatic quality controller. It does not
measure a real device's frame rate as a claim, does not own the Settings
family's option/profile/applicator model or the Persistence family's
save/load model, and makes no claim that a tier, a render scale, or a
foveation level is comfortable, legible, fast enough, or store-certifiable on
any device.

## Why it composes

- **Settings.** The Settings family
  ([`docs/standards/settings/verification-contract.md`](../settings/verification-contract.md))
  owns the user-facing option, profile, and applicator model. This family's
  Unity adapter gate exposes one pure hook from a Settings-owned option value
  to a `select_tier` or `set_override` intent; the Settings family's screen,
  profile layering, and applicator rollback are not reimplemented here, and
  this family's Unity assembly references no Settings type.
- **Persistence.** The Persistence family
  ([`docs/standards/persistence/verification-contract.md`](../persistence/verification-contract.md))
  owns save orchestration, schema versioning, and migration. A consumer that
  persists a player's selected tier or override saves it through Persistence's
  own model; this family owns no save format and reads no save file.
- **XR UI shell / Live tuning.** Neither is a Core or mandatory Unity
  dependency here: the family renders no panel of its own. A future
  in-headset performance overlay, if built, is a peer client of the XR UI
  shell and the tuning scaffold under LESSON-010, never a dependency this
  family carries.
- **Interaction / Audio / Haptics.** None of these families is a source or a
  sink for this family; a tier change is not routed through the Interaction
  family's semantic intents, and this family raises no audio or haptic event
  of its own.

## Planned package boundary

| Layer | Owns | Must not own |
| --- | --- | --- |
| Engine-light Core `com.lingkyn.quality-tiers.core` (package id reserved only after admission) | tier and device-profile identity; the closed per-tier declaration and guard-rail validation; the immutable tier registry and explicit registration; per-device capability descriptors and closed-at-select-time gating; the closed override-field set; select-tier, set-override, reset, and apply-preset intents on an immutable per-device state with deterministic replay and fingerprint; the frame-budget policy as plain data; structured results | Unity types, `UnityEngine` types, render-pipeline assets, subsystems, renderer swaps, device measurement, product frame-rate targets, Settings screens, Persistence save formats |
| Unity adapter `com.lingkyn.quality-tiers.unity` (package id reserved only after admission) | explicit application through the XR display subsystem (refresh rate), XR settings (render scale, foveation level), and a URP renderer-asset map (MSAA, shadow, and post-processing budgets), each behind an injectable output sink and device-capability probe; explicit diagnostics for every by-name or optional resolution; a frame-time sampler reporting plain measured data; the pure Settings-hook conversion function; an optional Adaptive Performance feedback route | identity rules, tier and capability semantics, intent semantics, frame-budget semantics, product tier or device values, scene singletons, static instances, scene search, reflection discovery, platform detection, any Settings or Persistence type |

## Evidence boundary

An EditMode run can prove identity and tier/capability/budget-declaration
validation, registry and capability-descriptor-set construction, intent
application, one-selection-per-device state, immutability, deterministic
replay, display-subsystem and render-scale/foveation application to injected
fakes, renderer-asset-map resolution, and the Settings hook's pure
conversion, for the declared tuple. It proves the plumbing only. It cannot
prove that a named device actually holds a stated refresh rate, that a render
scale or foveation level looks correct, that a frame budget was held in play,
or that a build passes store certification; those are human judgements or
real store-submission outcomes recorded in a Device Lab receipt, as
[`verification-contract.md`](verification-contract.md) states in its claim
ceiling.

## Lessons register dispositions (to be added to the register when the family goes live)

| Lesson | Disposition | Rationale |
| --- | --- | --- |
| LESSON-001 closed classification vs open tags | adopted | `TierId` and `DeviceProfileId` are validated closed identities, and foveation level, MSAA sample count, and the override-field set are closed enums; the only open metadata is a label on a tier declaration, which drives no validation or intent resolution |
| LESSON-002 migration path | deferred | No release exists yet; the first release records its compatibility policy, and the first breaking change to `TierId`, `DeviceProfileId`, the override-field set, or a stable failure code ships a migration note in the same commit |
| LESSON-003 single-workstation gate | adopted | Tests will run in `run_unity_gates.py` and the consumer workflow like every family; no gate in this standard depends on a maintainer workstation |
| LESSON-004 by-name resolution fails closed | adopted | The Core resolves nothing by name. The Unity adapter gate requires an explicit diagnostic and a negative test for every by-name or optional resolution: the display-subsystem accessor, the `XRSettings` accessor, an unbound renderer-asset map entry, and an Adaptive Performance provider query, each pinned to the validated engine, XR display, URP, and Adaptive Performance versions |
| LESSON-005 clause coverage | deferred | No staged implementation or tests exist; a future staged-implementation item writes `coverage-map.json` mapping every Core and Unity adapter clause to named tests before any clause is cited as evidence, modelled on `docs/standards/haptics/coverage-map.json` |
| LESSON-006 sibling pinning | deferred | Applies at the first Git-consumer validation; the README install matrix will pin the Core and Unity adapter packages, and any renderer-asset or Settings-hook wiring a composition binds, to one full commit SHA |
| LESSON-007 skin seam | not_applicable | The family renders no panel and owns no visual skin or theme seam; it drives a display subsystem and a render-pipeline asset, not a renderer's visual vocabulary |
| LESSON-008 version bump is a verification claim | adopted | No package version exists; the first scaffold enters at 0.1.0 and stays there until its first verified compatibility profile |
| LESSON-009 tunable surface as data | not_applicable | The family is not UI-bearing and owns no injectable skin/theme seam a developer-tuning scaffold would attach to; a `QualityTierPreset` is a performance configuration applied through the `apply_preset` intent, not a design-visual tunable under this protocol's definition. If a future in-headset performance overlay needs live tier switching, it composes through the XR UI shell as its own client (LESSON-010) and ships its own `foundry.tunables.json` at that time |
| LESSON-010 developer tool is a peer client, never a dependency | not_applicable | The family owns tier, device-profile, and capability identity and a renderer/XR-settings adapter, not a shell, dock, or UI panel a developer tool would operate on; it holds no dependency on Live Tuning, the XR UI shell, or any other developer-tooling assembly, so this lesson's dependency-direction question does not arise here yet |
| LESSON-011 one intent channel for people and agents | adopted | The Core gate requires every `QualityIntent` (`select_tier`, `set_override`, `reset`, `apply_preset`) to carry an `IntentActor` and an optional expected revision from its first version, with `state.stale` rejecting a stale write before the intent's own rule runs, so a player-issued and an agent-issued copy of the same intent take the same path from the start rather than being retrofitted later |

## Next steps

1. A person confirms every URL, page path, section anchor, version pin,
   license, and open-source maintenance state in
   [`source-manifest.json`](source-manifest.json).
2. The maintainer copies [`admission.draft.json`](admission.draft.json) into
   `docs/foundry/admissions/` as the durable record through the
   [system admission gate](../../foundry/system-admission.md).
3. Done: the staged implementation authors the Core and the Unity adapter
   under `staging/quality-tiers/`, modelled on `staging/haptics/`, against
   [`verification-contract.md`](verification-contract.md), with
   `coverage-map.json` mapping every Core and Unity adapter clause to a named
   test (QU-01 and QU-05 are `partial`, each with a named Device Lab receipt
   gap; every other clause is `covered`). The code stays authored and
   unexecuted until a Unity run produces a compatibility profile, and nothing
   enters `packages/` before admission and a green gate.
4. A person or Agent with a Unity Editor runs
   `python scripts/run_unity_gates.py --host com.lingkyn.quality-tiers.core`,
   records the resulting compatibility profile, and either confirms the
   coverage map or turns it red.
5. The first Device Lab session for this family records a held frame rate, a
   render-scale or foveation judgement, and any store-submission outcome as
   human observations and real records bound to the tier-registry and
   capability-descriptor fingerprint, and never as a package claim.

See also:

- [`verification-contract.md`](verification-contract.md)
- [`source-manifest.json`](source-manifest.json)
- [`admission.draft.json`](admission.draft.json)
- [`next-batch.json`](../../foundry/queue/next-batch.json), the queue this
  candidate joins, and the
  [system admission gate](../../foundry/system-admission.md)
- [`lessons-register.json`](../lessons/lessons-register.json)
- [`settings/verification-contract.md`](../settings/verification-contract.md),
  the option/profile/applicator family this family's Unity hook composes on
- [`persistence/verification-contract.md`](../persistence/verification-contract.md),
  the save/load family a consumer routes a persisted selection through
- [`shipped-game-gap-matrix.md`](../../benchmarks/shipped-game-gap-matrix.md),
  which ranks this family fourteenth and states why every product shape needs
  it
- [`haptics/README.md`](../haptics/README.md), a sibling standard this family
  is modelled on
