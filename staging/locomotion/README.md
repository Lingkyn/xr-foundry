# Locomotion staging

Status: **staging material, not a live package.** Nothing here is in the package
catalog, a batch, a compatibility profile, or a release. The manifest is named
`package.staging.json` on purpose so repository validation does not treat this
directory as a live `com.lingkyn.*` package.

This directory holds the first implementation of the Locomotion and comfort family
named in `docs/foundry/queue/next-batch.json` (`NEXT-LOCOMOTION-COMFORT`). It exists
because the production line cannot register a new package without a verified Unity
compatibility profile, and no Unity run has happened since the last recorded
evidence commit. The code is written and tested on paper; it has not compiled.

## What is here

| Path | Content |
| --- | --- |
| `com.lingkyn.locomotion.core/Runtime/LocomotionCore.cs` | Engine-light Core: a closed `ModeId` (`teleport`, `snap_turn`, `smooth_turn`, `continuous_move`) and an open `AnchorId` sharing one canonical form, the closed typed `ComfortPolicy` (vignette, turn mode, turn increment, movement speed, posture) with fail-closed rejection, an anchor registry, teleport/turn/move/set-comfort-option intents applied to an immutable `LocomotionState`, deterministic replay with a fingerprint, and `LocomotionResult<T>` with stable `LocomotionFailure` codes |
| `com.lingkyn.locomotion.core/Tests/Editor/LocomotionCoreContractTests.cs` | 32 EditMode tests mapped in `docs/standards/locomotion/coverage-map.json` |
| `com.lingkyn.locomotion.core/Samples~/ComfortPolicy/` | Domain-only sample: comfort policy, anchor registry, one rejected intent, replayed sequence; no scene |
| `com.lingkyn.locomotion.unity/Runtime/LocomotionUnity.cs` | Unity adapter: `LocomotionProviderBindingAsset` (mode ids to XR Interaction Toolkit locomotion providers, an optional body transformer, an optional input action reference, and a tunneling vignette controller reference), `LocomotionBindingValidation` with stable codes, the `ILocomotionProviderSurface` seam with the real `LocomotionProviderSurface`, immutable `LocomotionProviderBinding`, `LocomotionProviderRuntime` built from explicit references |
| `com.lingkyn.locomotion.unity/Tests/Editor/LocomotionUnityBindingTests.cs` | 22 EditMode tests for the adapter gate, driven through a fake `ILocomotionProviderSurface` |
| `docs/standards/locomotion/` | Standard README, source manifest, verification contract, coverage map, admission draft |

## Why the adapter has a provider surface seam

No XR Interaction Toolkit locomotion provider, body transformer, or input action can
be constructed in EditMode without a rig, so an EditMode test cannot hold a real
bound provider. Instead the adapter routes every engine call through
`ILocomotionProviderSurface`: presence and mode-match checks, turn enable/disable,
turn increment and move speed writes, vignette enable and intensity writes, and
teleport/turn/move forwarding each mirror a real by-name or optional-lookup call to
the toolkit and report failure only through the return value.
`LocomotionProviderSurface` is the real implementation; the tests use a fake that
follows the same contract and is exercised against the real surface only in its
provider-absent branch — the coverage map records that gap against AU-01 rather than
hiding it.

The Core decides whether an intent is accepted. When the provider then refuses a
by-name write or optional resolution, the runtime keeps the Core state (the intent
was valid) and records a `ProviderDiagnostic` with a stable code; it never reports a
provider write or forwarded intent it did not confirm (LESSON-004).

## How it moves into the tree

One Unity run turns this into a live package. The person or Agent with the Editor:

1. Copies `docs/standards/locomotion/admission.draft.json` to
   `docs/foundry/admissions/locomotion.v1.json` (a maintainer decision) and writes
   the `locomotion-core` and `locomotion-unity` blueprints under
   `docs/foundry/blueprints/`.
2. Runs `python scripts/scaffold_unity_package.py docs/foundry/blueprints/locomotion-core.v1.json --output-root . --write`
   (and the same for the `locomotion-unity` blueprint), then replaces the generated
   scaffold sources with the files here and renames each `package.staging.json` to
   `package.json`, adding `.meta` files for every asset.
3. Adds the package to `package-catalog.json`, `component-catalog.json`,
   `capability-registry.json`, a building batch, and the lessons-register
   dispositions listed in `docs/standards/locomotion/README.md`.
4. Runs `python scripts/run_unity_gates.py --host com.lingkyn.locomotion.core`
   and records the compatibility profile from the receipt.
5. Runs the repository contract and the merge-readiness verdict, then opens the
   pull request.

Until step 4 happens, every claim about this code is "authored, unexecuted".
