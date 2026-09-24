# Haptics staging

Status: **staging material, not a live package. Authored, unexecuted.** Nothing
here is in the package catalog, a batch, a compatibility profile, or a release.
The manifest is named `package.staging.json` on purpose so repository
validation does not treat this directory as a live `com.lingkyn.*` package.

This directory holds the first implementation of the Haptics family named in
`docs/standards/haptics/README.md`. It exists because the production line
cannot register a new package without a verified Unity compatibility profile,
and no Unity run has happened yet. The code is written and tested on paper
against
[`verification-contract.md`](../../docs/standards/haptics/verification-contract.md);
it has not compiled.

## What is here

| Path | Content |
| --- | --- |
| `com.lingkyn.haptics.core/Runtime/*.cs` | Engine-light Core: `HapticEventId` and `HapticProfileId` with one canonical dotted-segment form, the closed `HapticKind` set (transient, continuous, envelope) with fail-closed amplitude/duration/frequency guard-rail declarations, the immutable `HapticEventRegistry` and `HapticEventRegistryBuilder` built only by explicit registration, the closed `HapticTarget` set (left, right, both, named_device) and `HapticProfile`/`HapticProfileBuilder`/`HapticProfileSet`, tuning intents (`PlayIntent`, `StopIntent`, `StopAllIntent`, `SetProfileIntent`) applied to the immutable, last-one-wins `HapticState` with deterministic replay and a fingerprint, and the engine-free `HapticBindingTable` resolving another family's source kind and id to a haptic `PlayIntent` or a named `binding.unbound` result |
| `com.lingkyn.haptics.core/Tests/Editor/HapticsCoreContractTests.cs` | 55 EditMode tests mapped in `docs/standards/haptics/coverage-map.json` |
| `com.lingkyn.haptics.core/Samples~/HapticsWalkthrough/` | Domain-only sample: a registry and profile set built by explicit registration, a full play/stop/stop_all/set_profile intent sequence, a binding table resolving one bound source and reporting `binding.unbound` for one unbound source, and a replayed sequence; no asset, scene, or engine API |
| `com.lingkyn.haptics.unity/Runtime/*.cs` | Unity adapter: the explicit, injected `HapticRouteMap` from a Core `HapticTarget` to one `HapticRouteHandle` (an XRI impulse channel name or an OpenXR action-and-subaction-path pair), the injectable `IHapticOutputSink` with the in-memory `RecordingHapticSink`, three real routes behind that seam (`XriImpulseChannelSink`, `OpenXrHapticActionSink`, `InputSystemRumbleFallbackSink`) each resolving its device through an explicit probe and reporting `channel.missing`/`device.missing`/`action.missing` rather than staying silent, the `OrderedFallbackHapticSink` for explicit, consumer-wired fallback ordering, `HapticProfileAsset` as a `ScriptableObject` converted deterministically through `HapticProfileAssetConverter`, and the envelope-to-transient degrade path (`HapticEnvelopeDegrade`, `IHapticEnvelopeSupport`) with a named `envelope.unsupported.degraded` diagnostic |
| `com.lingkyn.haptics.unity/Tests/Editor/HapticsUnityContractTests.cs` | 31 EditMode tests for the adapter gate, driven through fakes for every seam (`IXriChannelProbe`, `IOpenXrDeviceProbe`, `IInputSystemRumbleProbe`, `IHapticEnvelopeSupport`) and the recording sink — never a device |
| `docs/standards/haptics/` | Standard README, source manifest, verification contract, coverage map, admission draft |

## One intent channel, and the LESSON-011 addition

`HapticState.Apply` is the one channel through which a person's control, an
agent adapter, a replay, or an import all change haptic state: every
`HapticIntent` (`PlayIntent`, `StopIntent`, `StopAllIntent`, `SetProfileIntent`)
carries an `IntentActor` (`player`, the default, `agent`, `replay`, or
`import`) used only for attribution and replay, and an optional expected
revision that a stale value rejects with `state.stale` before the intent's own
rule ever runs — never a second write path, and never a different validation
rule for a different actor. `HapticUnityRuntime.Apply` calls this one entry
point and no other. This shape (and the `state.stale` code itself) is not in
`docs/standards/haptics/verification-contract.md`'s literal Core-gate text,
which predates LESSON-011; it is the identical one-intent-channel shape
`staging/live-tuning` already carries, added here because every live family
answers LESSON-011 (see `docs/standards/haptics/coverage-map.json`'s summary
note and `HapticFailure.StateStale`'s doc comment for the full accounting).
`docs/standards/lessons/lessons-register.json` has no Haptics row yet for
LESSON-011 (or for the other ten lessons); that disposition is a maintainer
record outside this package's own paths, made when the family is admitted.

## How it moves into the tree

One Unity run turns this into a live package. The person or Agent with the
Editor:

1. Copies `docs/standards/haptics/admission.draft.json` to
   `docs/foundry/admissions/haptics.v1.json` (a maintainer decision) and writes
   the `haptics-core` and `haptics-unity` blueprints under
   `docs/foundry/blueprints/`.
2. Runs `python scripts/scaffold_unity_package.py docs/foundry/blueprints/haptics-core.v1.json --output-root . --write`
   (and the same for the `haptics-unity` blueprint), then replaces the
   generated scaffold sources with the files here and renames each
   `package.staging.json` to `package.json`, adding `.meta` files for every
   asset.
3. Adds the package to `package-catalog.json`, `component-catalog.json`,
   `capability-registry.json`, a building batch, and the lessons-register
   dispositions listed in `docs/standards/haptics/README.md` (plus a
   LESSON-011 disposition, not yet recorded there).
4. Runs `python scripts/run_unity_gates.py --host com.lingkyn.haptics.core`
   and records the compatibility profile from the receipt.
5. Runs the repository contract and the merge-readiness verdict, then opens
   the pull request.

Until step 4 happens, every claim about this code is "authored, unexecuted".

## Non-claims

- No test claims that a controller vibrated, that an amplitude or a pattern
  was felt, or that any input was read from a device (the contract's claim
  ceiling). `RecordingHapticSink`, `FixedXriChannelProbe`,
  `FixedOpenXrDeviceProbe`, `FixedInputSystemRumbleProbe`, and
  `FixedHapticEnvelopeSupport` are all in-memory fakes; nothing here has
  driven a real actuator.
- Felt intensity, comfort, fatigue, and whether a pattern reads as intended
  are Device Lab receipts, never package claims. The first Device Lab plan
  for this family records a haptic session as a human judgement bound to the
  profile and event-registry fingerprint, per
  `docs/standards/haptics/README.md`'s next steps.
- No claim that the envelope-to-transient degrade path, the XRI route, the
  OpenXR route, or the Input System rumble fallback has run against a named
  runtime or controller: the probes behind each are fixed fakes, and the
  real device/runtime queries they stand in for are unexecuted.
- No package id, catalog entry, maturity, release, or device status. Those
  exist only after admission and a green Unity gate.
