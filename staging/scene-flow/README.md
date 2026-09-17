# Scene flow staging

Status: **staging material, not a live package.** Nothing here is in the package
catalog, a batch, a compatibility profile, or a release. The manifest is named
`package.staging.json` on purpose so repository validation does not treat this
directory as a live `com.lingkyn.*` package.

This directory holds the first implementation of the Scene flow family named in
`docs/foundry/queue/next-batch.json` (`NEXT-SCENE-FLOW`). It exists because the
production line cannot register a new package without a verified Unity
compatibility profile, and no Unity run has happened since the last recorded
evidence commit. The code is written and tested on paper; it has not compiled.

## What is here

| Path | Content |
| --- | --- |
| `com.lingkyn.scene-flow.core/Runtime/SceneFlowCore.cs` | Engine-light Core: `SceneSetId` and `SceneId` with one canonical form, the immutable `SceneGraph` and `SceneGraphBuilder` (one active scene per set, one declared fallback set), typed `SceneFlowDuration` and `TransitionOptions`, the closed intent set (load, unload, switch, elapsed-time, load-completed, load-failed), the `TransitionPhase` state machine (`idle`, `fading_out`, `loading`, `holding`, `fading_in`), immutable `SceneFlowState`, fail-closed error recovery to the declared fallback set, deterministic replay with a fingerprint, and `SceneFlowResult<T>` with stable `SceneFlowFailure` codes |
| `com.lingkyn.scene-flow.core/Tests/Editor/SceneFlowCoreContractTests.cs` | 32 EditMode tests mapped in `docs/standards/scene-flow/coverage-map.json` |
| `com.lingkyn.scene-flow.core/Samples~/SceneGraph/` | Domain-only sample: graph, a full load and switch cycle, one failed load recovering to the fallback set, and a replayed sequence; no scene |
| `com.lingkyn.scene-flow.unity/Runtime/SceneFlowUnity.cs` | Unity adapter: `SceneFlowBindingAsset` (scene ids to build-list scene paths, a fade surface reference), `SceneBindingValidation` with stable codes, an `ISceneLoaderSurface` seam with the real `UnitySceneLoaderSurface`, immutable `SceneFlowBinding`, and `SceneFlowRuntime` built from explicit references that maps Core phases to additive loads, activation, unloads, and a fade surface |
| `com.lingkyn.scene-flow.unity/Tests/Editor/SceneFlowUnityBindingTests.cs` | 21 EditMode tests for the adapter gate, driven through a fake `ISceneLoaderSurface` |
| `docs/standards/scene-flow/` | Standard README, source manifest, verification contract, coverage map, admission draft |

## Why the adapter has a scene loader seam

A build-list scene cannot be registered, loaded, or activated from an EditMode test
without a checked-in scene asset, so an EditMode test cannot exercise
`SceneManager.LoadSceneAsync`, `UnloadSceneAsync`, or `SetActiveScene` directly.
Instead the adapter routes every engine call through `ISceneLoaderSurface`:
`TryGetBuildSceneInfo`, `LoadAdditive`, `Unload`, and `TrySetActiveScene` mirror the
build-settings lookup and `SceneManager`'s by-name calls, each of which reports an
unknown or refused target only through its return value. `UnitySceneLoaderSurface` is
the real implementation; the tests use a fake that follows the same contract and is
exercised against the real surface only in its not-registered branch. A fade surface
is injected the same way through `IFadeSurface`, so the adapter owns no visual
vocabulary of its own.

The Core decides whether an intent is accepted. When the engine then refuses a
by-name call — an unregistered scene path, a refused activation, or a refused
unload — the runtime keeps the Core state (the intent was valid) and records a
`SceneFlowDiagnostic` with a stable code; it never reports a load, activation, or
unload it did not confirm (LESSON-004).

## How it moves into the tree

One Unity run turns this into a live package. The person or Agent with the Editor:

1. Copies `docs/standards/scene-flow/admission.draft.json` to
   `docs/foundry/admissions/scene-flow.v1.json` (a maintainer decision) and writes the
   `scene-flow-core` and `scene-flow-unity` blueprints under
   `docs/foundry/blueprints/`.
2. Runs `python scripts/scaffold_unity_package.py docs/foundry/blueprints/scene-flow-core.v1.json --output-root . --write`
   (and the same for the `scene-flow-unity` blueprint), then replaces the generated
   scaffold sources with the files here and renames each `package.staging.json` to
   `package.json`, adding `.meta` files for every asset.
3. Adds the package to `package-catalog.json`, `component-catalog.json`,
   `capability-registry.json`, a building batch, and the lessons-register
   dispositions listed in `docs/standards/scene-flow/README.md`.
4. Runs `python scripts/run_unity_gates.py --host com.lingkyn.scene-flow.core`
   and records the compatibility profile from the receipt.
5. Runs the repository contract and the merge-readiness verdict, then opens the
   pull request.

Until step 4 happens, every claim about this code is "authored, unexecuted".
