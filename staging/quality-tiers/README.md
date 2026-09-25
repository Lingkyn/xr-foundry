# Quality tiers staging

Status: **staging material, not a live package. Authored, unexecuted.** Nothing
here is in the package catalog, a batch, a compatibility profile, or a release.
The manifest is named `package.staging.json` on purpose so repository
validation does not treat this directory as a live `com.lingkyn.*` package.

This directory holds the first implementation of the Quality Tiers family
named in `docs/standards/quality-tiers/README.md`. It exists because the
production line cannot register a new package without a verified Unity
compatibility profile, and no Unity run has happened yet. The code is written
and tested on paper against
[`verification-contract.md`](../../docs/standards/quality-tiers/verification-contract.md);
it has not compiled.

## What is here

| Path | Content |
| --- | --- |
| `com.lingkyn.quality-tiers.core/Runtime/*.cs` | Engine-light Core: `TierId` and `DeviceProfileId` with one canonical dotted-segment form (and no cross-check against this repository's own compatibility-profile ids), the closed per-tier declaration (refresh-rate, render-scale, shadow-budget, and post-processing-budget guard rails plus a single closed `FoveationLevel` and a single closed `MsaaSampleCount`) with fail-closed validation, the immutable `QualityTierRegistry` built only by explicit registration, a `DeviceCapabilityDescriptor` per device profile that gates a tier or an override closed at select time, the closed override-field set, `select_tier`/`set_override`/`reset`/`apply_preset` intents applied to the immutable, one-selection-per-device `QualityState` with deterministic replay and a fingerprint, and `FrameBudgetPolicy` as plain data |
| `com.lingkyn.quality-tiers.core/Tests/Editor/QualityTiersCoreContractTests.cs` | 52 EditMode tests mapped in `docs/standards/quality-tiers/coverage-map.json` |
| `com.lingkyn.quality-tiers.core/Samples~/QualityTiersWalkthrough/` | Domain-only sample: a tier registry and a device capability set built by explicit registration, a full `select_tier`/`set_override`/`reset`/`apply_preset` intent sequence, a replayed sequence proving deterministic state, and a capability-gated `select_tier` failing closed; no asset, scene, or engine API |
| `com.lingkyn.quality-tiers.unity/Runtime/*.cs` | Unity adapter: three explicit seams (`IXrDisplayControl` for refresh rate and render scale, `IFoveationControl` for foveation level, `IRendererAssetSwitch` for a bound `UniversalRenderPipelineAsset` and Unity quality level) each behind an injectable fake, an `IDeviceCapabilityProbe` queried before every render-scale or foveation write so a runtime-unsupported value degrades with a named `capability.unsupported.degraded` diagnostic instead of a silent no-op, a `QualityRendererAssetMap` binding every registered tier to an authoring-time asset reference with `renderer.asset.unbound` for a gap, a frame-time sampler reporting measured frame time as plain data, the pure Settings hook (`IQualityOptionSource` / `QualityOptionHook`) mapping a user-facing quality option's raw value to a `select_tier` intent with no reference to any Settings type, and an optional Adaptive Performance feedback route |
| `com.lingkyn.quality-tiers.unity/Tests/Editor/QualityTiersUnityContractTests.cs` | 20 EditMode tests for the adapter gate, driven through fakes for every seam (`FakeXrDisplayControl`, `FakeFoveationControl`, `FixedDeviceCapabilityProbe`, `RecordingRendererAssetSwitch`, `FixedFrameTimeSource`) — never a device |
| `docs/standards/quality-tiers/` | Standard README, source manifest, verification contract, coverage map, admission draft |

## One intent channel, and the LESSON-011 addition

`QualityState.Apply` is the one channel through which a person's control, an
agent adapter, a replay, or an import all change quality state: every
`QualityIntent` (`SelectTierIntent`, `SetOverrideIntent`, `ResetIntent`,
`ApplyPresetIntent`) carries an `IntentActor` (`Player`, the default, `Agent`,
`Replay`, or `Import`) used only for attribution and replay, and an optional
expected revision that a stale value rejects with `state.stale` before the
intent's own rule ever runs — never a second write path, and never a
different validation rule for a different actor. `QualityUnityRuntime.Apply`
calls this one entry point and no other. This is the one-intent-channel
Core-gate clause of
`docs/standards/quality-tiers/verification-contract.md` (LESSON-011), the
identical shape `staging/haptics` and `staging/live-tuning` carry;
`docs/standards/quality-tiers/coverage-map.json` rows QC-12 to QC-15 map it to
its tests.
`docs/standards/lessons/lessons-register.json` has no Quality Tiers row yet
for LESSON-011 (or for the other ten lessons); that disposition is a
maintainer record outside this package's own paths, made when the family is
admitted.

## How it moves into the tree

One Unity run turns this into a live package. The person or Agent with the
Editor:

1. Copies `docs/standards/quality-tiers/admission.draft.json` to
   `docs/foundry/admissions/quality-tiers.v1.json` (a maintainer decision) and
   writes the `quality-tiers-core` and `quality-tiers-unity` blueprints under
   `docs/foundry/blueprints/`.
2. Runs `python scripts/scaffold_unity_package.py docs/foundry/blueprints/quality-tiers-core.v1.json --output-root . --write`
   (and the same for the `quality-tiers-unity` blueprint), then replaces the
   generated scaffold sources with the files here and renames each
   `package.staging.json` to `package.json`, adding `.meta` files for every
   asset.
3. Adds the package to `package-catalog.json`, `component-catalog.json`,
   `capability-registry.json`, a building batch, and the lessons-register
   dispositions listed in `docs/standards/quality-tiers/README.md` (plus a
   LESSON-011 disposition, not yet recorded there).
4. Runs `python scripts/run_unity_gates.py --host com.lingkyn.quality-tiers.core`
   and records the compatibility profile from the receipt.
5. Runs the repository contract and the merge-readiness verdict, then opens
   the pull request.

Until step 4 happens, every claim about this code is "authored, unexecuted".

## Non-claims

- No test claims that a device held a stable or measured frame rate, that a
  render scale or foveation level looked correct on a real headset, or that a
  build passed store certification (the contract's claim ceiling).
  `FakeXrDisplayControl`, `FakeFoveationControl`, `FixedDeviceCapabilityProbe`,
  `RecordingRendererAssetSwitch`, and `FixedFrameTimeSource` are all in-memory
  fakes; nothing here has driven a real display, foveation provider, or
  renderer.
- `docs/standards/quality-tiers/coverage-map.json` marks QU-01 (the display
  seam's refresh-rate application) and QU-05 (the frame-time sampler)
  `partial`, each with a named `missing` entry: EditMode proves the
  resolution, degrade, and arithmetic plumbing against injected fakes, never
  that a real device changed its physical refresh rate or held a measured
  frame time. Every other clause in both gates needs no device and is fully
  proven against a fake.
- Whether a tier's refresh rate, render scale, or foveation level is
  comfortable, legible, or fast enough on a named device are human judgements
  recorded in a Device Lab receipt, never package claims. The first Device
  Lab plan for this family records a session as a human judgement bound to
  the tier-registry and capability-descriptor fingerprint, per
  `docs/standards/quality-tiers/README.md`'s next steps.
- No package id, catalog entry, maturity, release, or device status. Those
  exist only after admission and a green Unity gate.
