# Audio staging

Status: **staging material, not a live package.** Nothing here is in the package
catalog, a batch, a compatibility profile, or a release. The manifest is named
`package.staging.json` on purpose so repository validation does not treat this
directory as a live `com.lingkyn.*` package.

This directory holds the first implementation of the Audio events family named in
`docs/foundry/queue/next-batch.json` (`NEXT-AUDIO-EVENTS`). It exists because the
production line cannot register a new package without a verified Unity
compatibility profile, and no Unity run has happened since the last recorded
evidence commit. The code is written and tested on paper; it has not compiled.

## What is here

| Path | Content |
| --- | --- |
| `com.lingkyn.audio.core/Runtime/AudioCore.cs` | Engine-light Core: `AudioEventId`, `BusId`, `SnapshotId`, `AnchorId`, `ParameterId` with one canonical form, `ParameterContract` (float range, bool, enumerated set), `MixGraph` and `MixGraphBuilder`, the closed `AudioIntent` set (post, stop, set parameter, register and unregister anchor, attach, move, detach, transition snapshot), immutable `AudioState`, `AudioResult<T>` with stable `AudioFailure` codes |
| `com.lingkyn.audio.core/Tests/Editor/AudioCoreContractTests.cs` | 35 EditMode tests mapped in `docs/standards/audio/coverage-map.json` |
| `com.lingkyn.audio.core/Samples~/MixGraph/` | Domain-only sample: graph, contracts, anchor attachment, one rejected intent, replayed sequence; no scene |
| `com.lingkyn.audio.unity/Runtime/AudioUnity.cs` | Unity adapter: `AudioMixerBindingAsset` (bus/parameter ids to exposed names, snapshot ids to `AudioMixerSnapshot` references or a fallback name), `AudioBindingValidation` with stable codes, `IMixerSurface` seam with the real `AudioMixerSurface`, immutable `AudioMixerBinding`, `AudioMixerRuntime` built from explicit references |
| `com.lingkyn.audio.unity/Tests/Editor/AudioUnityBindingTests.cs` | 18 EditMode tests for the adapter gate, driven through a fake `IMixerSurface` |
| `docs/standards/audio/` | Standard README, source manifest, verification contract, coverage map, admission draft |

## Why the adapter has a mixer surface seam

`AudioMixer` and `AudioMixerSnapshot` cannot be created from script, so an EditMode
test cannot hold a real mixer unless one is checked in as an asset. Instead the
adapter routes every engine call through `IMixerSurface`: `TryGetFloat`,
`TrySetFloat`, `HasSnapshot`, and `TryTransitionToSnapshot` mirror
`AudioMixer.GetFloat`, `SetFloat`, and `FindSnapshot`, each of which reports an
unknown name only through its return value. `AudioMixerSurface` is the real
implementation; the tests use a fake that follows the same contract and is exercised
against the real surface only in its mixer-absent branch. A snapshot binding entry
carries an `AudioMixerSnapshot` reference for Editor authoring and a fallback
`snapshotName` for hosts without a reference; the reference's name wins when set.
Bool parameters are written as 1 or 0; enumerated parameters cannot bind to an exposed
float and are rejected with `mixer.parameter.kind.mismatch`.

The Core decides whether an intent is accepted. When the mixer then refuses the
by-name call, the runtime keeps the Core state (the intent was valid) and records a
`MixerDiagnostic` with a stable code; it never reports a write or transition it did
not confirm (LESSON-004).

## How it moves into the tree

One Unity run turns this into a live package. The person or Agent with the Editor:

1. Copies `docs/standards/audio/admission.draft.json` to
   `docs/foundry/admissions/audio.v1.json` (a maintainer decision) and writes the
   `audio-core` and `audio-unity` blueprints under `docs/foundry/blueprints/`.
2. Runs `python scripts/scaffold_unity_package.py docs/foundry/blueprints/audio-core.v1.json --output-root . --write`
   (and the same for the `audio-unity` blueprint), then replaces the generated
   scaffold sources with the files here and renames each `package.staging.json` to
   `package.json`, adding `.meta` files for every asset.
3. Adds the package to `package-catalog.json`, `component-catalog.json`,
   `capability-registry.json`, a building batch, and the lessons-register
   dispositions listed in `docs/standards/audio/README.md`.
4. Runs `python scripts/run_unity_gates.py --host com.lingkyn.audio.core`
   and records the compatibility profile from the receipt.
5. Runs the repository contract and the merge-readiness verdict, then opens the
   pull request.

Until step 4 happens, every claim about this code is "authored, unexecuted".
