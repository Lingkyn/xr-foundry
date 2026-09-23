# Live tuning staging

Status: **staging material, not a live package.** Nothing here is in the package
catalog, a batch, a compatibility profile, or a release. The manifest is named
`package.staging.json` on purpose so repository validation does not treat this
directory as a live `com.lingkyn.*` package.

This directory holds the first implementation of the Live Tuning family named in
`docs/standards/live-tuning/README.md` (source-gate queue item `NEXT-LIVE-TUNING`).
It exists because the production line cannot register a new package without a
verified Unity compatibility profile, and no Unity run has happened since the last
recorded evidence commit. The code is written and tested on paper against
[`verification-contract.md`](../../docs/standards/live-tuning/verification-contract.md);
it has not compiled.

## What is here

| Path | Content |
| --- | --- |
| `com.lingkyn.live-tuning.core/Runtime/*.cs` | Engine-light Core: `TunableId` and `SnapshotId` with one canonical dotted-segment form, the closed `TunableKind` set (float, integer, bool, enumerated, colour, vector2, vector3) with fail-closed kind declarations, the immutable `TunableRegistry` and `TunableRegistryBuilder` built only by explicit registration, the closed `EditorKind` set and `ResolveEditorKind`, tuning intents (`SetIntent`, `ResetIntent`, `ResetAllIntent`, `SnapshotIntent`, `ApplySnapshotIntent`) applied to the immutable, override-only `TuningState` with deterministic replay and a fingerprint, engine-free `BindingRecord` and `BindingSet` validation, the immutable `BindingIndex` (`BindingIndex.cs`) that answers "point at it, tune it", a self-contained JSON reader/writer (`LiveTuningJson.cs`), and the two-way `TokenBridge` to and from a `TokenOverrideDocument` |
| `com.lingkyn.live-tuning.core/Tests/Editor/LiveTuningCoreContractTests.cs` | 49 EditMode tests mapped in `docs/standards/live-tuning/coverage-map.json` |
| `com.lingkyn.live-tuning.core/Samples~/TuningWalkthrough/` | Domain-only sample: a registry built by explicit registration and by the token bridge, a full set/snapshot/reset/apply_snapshot/export intent sequence, and a replayed sequence; no asset, scene, or UnityEngine API |
| `com.lingkyn.live-tuning.unity/Runtime/*.cs` | Unity adapter: `ISkinBinder` and the opaque `asset_key#member` target-path convention (`SkinTargetPath`), `SkinBindingValidation`/`SkinBindingSet`/`SkinBindingException` with stable codes and field paths, a self-contained reference skin asset and binder pair for testing (`DemoSkin.cs`), `TuningRuntime` (live application through the skin seam), the injectable `ITuningExportSink` (with `DescribeDestination()`, a read-only peek at where an export would land) with a device JSON writer (`DeviceTokenExportSink`) and an Editor-only asset writer (`EditorAssetExportSink`, guarded by `#if UNITY_EDITOR`), explicit device-override import (`TuningImport`), the closed set of editor controls and `TuningEditorFactory` (one per `EditorKind`), `TuningPanelHost` with the `ITuningPanelSurface` seam and its `UguiFallbackPanelSurface`, pick-to-tune selection (`TuningSelection.cs`: `TuningScope`, `SelectionDiagnostic`, and the host's `Select`/`ClearSelection`), and the two view presets (`TuningView.cs`: `TuningViewPreset`, `TuningSlotView`, `TuningHostView`, `TuningView.Describe`) |
| `com.lingkyn.live-tuning.unity/Runtime/Shell/ShellTuningClient.cs` | This family as a peer client of the XR UI shell (LESSON-010), never a shell dependency: `TuningVerb` registers the constant `tune` verb id and display word into a shell `VerbRegistryBuilder` exactly the entry point any other family would use; `ShellTuningClient` adapts the shell's generic `IShellPanelContent<TuningSlot>` seam to this package's own `ITuningPanelSurface`, keyed only by the panel's `SurfaceId`; and `ShellTuningClient.ApplyFocus` maps the shell's `FocusSubject` to a `TuningPanelHost` scope through `TuningPanelHost.Select`, and so through the Core `BindingIndex` |
| `com.lingkyn.live-tuning.unity/Tests/Editor/LiveTuningUnityContractTests.cs` | 38 EditMode tests for the adapter gate, driven through fakes for every seam (`ISkinApplyTarget`, `ITuningFileSystem`, `ITuningFileReader`, `ITuningPanelSurface`) |
| `com.lingkyn.live-tuning.unity/Tests/Editor/ShellTuningClientTests.cs` | 5 EditMode tests (listed under the coverage map's `additional_tests_outside_clauses`) proving the verb registers like any peer, a real `TuningPanelHost` attaches through `ShellTuningClient`, and `ApplyFocus` maps a panel target through the `BindingIndex`, restores the previous scope on no target, and reports `selection.unbound` rather than a silent empty panel. Moved here from the shell's own (now deleted) UGUI-adapter integration test |
| `docs/standards/live-tuning/` | Standard README, source manifest, verification contract, coverage map, admission draft |

## The five scaffold rules this package follows

Named in [`docs/standards/live-tuning/README.md`](../../docs/standards/live-tuning/README.md#scaffold-decoupled-by-kind-never-by-location)
and each a clause in the verification contract:

1. **One editor per kind.** `TuningEditorFactory` resolves exactly one editor control
   type per `TunableKind`, through the closed `EditorKind` set. A corner radius, a
   spacing value, and a hit-target size are all `float` or `integer` tunables edited
   by the same editor; a colour anywhere is edited by the same colour editor.
2. **Targets attach through binding records.** A `BindingRecord` is data: a
   `TunableId`, its kind, an opaque target path (`asset_key#member`), and scope
   metadata. `SkinBindingSet` resolves it through a hand-written `ISkinBinder`, never
   through reflection.
3. **The panel is a host with one slot per bound tunable.** `TuningPanelHost`
   resolves each slot's editor by kind at attach time and contains no per-package,
   per-skin, or per-screen branch.
4. **Unknown kind fails closed.** A kind outside the closed set never resolves to an
   empty or placeholder editor; `tunable.kind.unsupported` is returned instead.
5. **Scope is metadata.** A package, skin, or screen label on a binding record
   filters `TuningPanelHost.WhereScope` and `ChangedOnly` and never changes which
   editor a slot holds.

## Point at it, tune it

The scaffold's targets are the variable part; the scaffold itself (one editor per
kind, one host, engine-free binding records) is fixed. "Point at it, tune it" is
what lets a target be selected, not another special case bolted onto the scaffold:

- The Core `BindingIndex` is built once, from a validated `BindingSet`. Given one
  opaque target path, `Lookup` returns exactly the binding records whose target
  path equals it or has it as a segment-wise prefix (segments split on `/`, so
  `panel` matches `panel/surface` but never `panel/surfacex`), in the validated
  set's own order. It never fails, holds no reflection or engine type, and is the
  only lookup the Unity adapter uses to go from a selected thing back to its
  tunables.
- `TuningPanelHost.Select` accepts either a shell `SurfaceId` (the identity the XR
  UI shell's own typed routing already resolves) or an explicit opaque target
  path, maps it through the `BindingIndex`, and shows exactly the matched slots as
  a scope named `selection`. A path nothing targets still switches to that
  (empty) scope and sets `ActiveSelectionDiagnostic` to `selection.unbound` with
  the path, never a silently empty panel. `ClearSelection` restores whichever
  scope was active immediately before.
- Selecting changes which slots are shown and never which editor a slot holds: a
  `colour` slot resolves to `colour_editor` before, during, and after a selection,
  because selection only ever narrows `TuningPanelHost`'s own already-attached
  slots by target path. The family performs no raycast, reads no pose, and holds
  no reference to a camera, ray, or input device; a source rule in the test
  assembly reads the Unity Runtime folder's own source files and asmdef as text
  and asserts none of them mention a physics, input, or camera type.
- Reference decision: `Lingkyn.LiveTuning.Unity`'s asmdef references
  `Lingkyn.XrUiShell.Core` directly, so `Select` can accept a real `SurfaceId`
  and `ShellTuningClient.ApplyFocus` can read a real `FocusSubject`. This is the
  only direction the dependency ever runs: `Lingkyn.XrUiShell.Core` itself
  references nothing, and no `Lingkyn.XrUiShell.*` assembly references
  `Lingkyn.LiveTuning.*` (a source rule in the shell's own Core test assembly
  proves it), so the graph is a DAG with the shell strictly upstream
  (`LiveTuning.Unity` → `{LiveTuning.Core, XrUiShell.Core}`; nothing points the
  other way). This family previously depended on a shell UGUI-adapter type that
  itself referenced `Lingkyn.LiveTuning.*` — a real cycle in spirit, since the
  shell (meant to be referenced only) referenced a client back — which
  LESSON-010 records and `ShellTuningClient` (`Runtime/Shell/ShellTuningClient.cs`)
  now fixes by moving the join point into this package as a peer client.

## The dock and its peer clients (LESSON-010)

In plain words: the XR UI shell is the dock, and every system's panel — Inventory,
Settings, or this family's developer scaffold — is a peer client of the dock. The
dock never references a client. This family registers its own `tune` verb into
the shell's verb registry and adapts the shell's generic panel-content seam
(`Runtime/Shell/ShellTuningClient.cs`), exactly like any other peer would; it does
not, and could not, make the shell aware that Live Tuning exists.

## One host, two presets

Two named `TuningViewPreset` values, `Designer` and `Engineer`, read the one
host's own slots; there are never two panels and never a second copy of a slot's
editor. `TuningView.Describe(host, preset)` is a pure function that builds a
disposable display descriptor (`TuningHostView` of `TuningSlotView`s) with no
rendering, no rendering surface, and no state of its own:

- **Designer** shows a slot's label, its editor kind, and that it carries a
  per-slot reset. Nothing else.
- **Engineer** shows the same slots plus, per slot, the registered range and
  step as text, the binding's own opaque target path, and the destination the
  runtime's export sink reports (read through `ITuningExportSink.DescribeDestination()`,
  which never writes anything); and, per host, every `TuningSlotDiagnostic` the
  host recorded, each still carrying its stable code.
- Switching presets changes only what a slot displays. It raises no Core intent,
  adds no slot, and cannot change a registered range, step, or value set: no
  method on `TuningViewPreset`, `TuningSlotView`, or `TuningHostView` writes back
  to a registration, and a declaration's own range/step properties (for example
  `FloatDeclaration.Min/Max/Step`) expose no setter at all. A range, step, or
  value set stays exactly where it always was: declared in code, changed only
  through a reviewed change.

## Why the Unity adapter has a skin-apply-target seam

A binder writes one member at a time; the renderer adapter's own skin entry point
(for example `ApplySkin` on a shell view) still needs to run once per changed skin so
every other view stays consistent with it. `TuningRuntime` never writes to a view
field: it writes through `ISkinBinder.Apply` and then calls `ISkinApplyTarget.ApplySkin`
on the same skin instance, exactly as the contract requires. Tests use a recording
fake instead of a real shell view, so an EditMode run never needs a scene.

The reference skin asset and binder (`LiveTuningDemoSkinAsset`,
`LiveTuningDemoSkinBinder`, and a second, unrelated `LiveTuningSecondaryDemoSkinAsset`
pair) are this package's own test fixtures, not a dependency on any Inventory or
other renderer package. A real consumer binds to its own renderer adapter's skin
asset by writing a binder shaped the same way; the family's scaffold code never
imports that binder or that skin type.

## How it moves into the tree

One Unity run turns this into a live package. The person or Agent with the Editor:

1. Copies `docs/standards/live-tuning/admission.draft.json` to
   `docs/foundry/admissions/live-tuning.v1.json` (a maintainer decision, after the XR
   UI shell candidate this family's panel host depends on has been decided) and
   writes the `live-tuning-core` and `live-tuning-unity` blueprints under
   `docs/foundry/blueprints/`.
2. Runs `python scripts/scaffold_unity_package.py docs/foundry/blueprints/live-tuning-core.v1.json --output-root . --write`
   (and the same for the `live-tuning-unity` blueprint), then replaces the generated
   scaffold sources with the files here and renames each `package.staging.json` to
   `package.json`, adding `.meta` files for every asset.
3. Adds the package to `package-catalog.json`, `component-catalog.json`,
   `capability-registry.json`, a building batch, and the lessons-register
   dispositions listed in `docs/standards/live-tuning/README.md`.
4. Runs `python scripts/run_unity_gates.py --host com.lingkyn.live-tuning.core`
   and records the compatibility profile from the receipt. This is also the first
   point at which CU-06's and CU-09's rendered-widget clauses can be closed, since
   they need an Editor GUI or a scene to inspect.
5. Runs the repository contract and the merge-readiness verdict, then opens the
   pull request.

Until step 4 happens, every claim about this code is "authored, unexecuted".

## Non-claims

- No claim that any control was visible or reachable, that a colour looked right, or
  that any input was read from a device (the contract's claim ceiling).
- No claim about the Inventory family's actual skin seam shape: the reference binder
  and skin asset here are this package's own test fixtures.
- No claim that the panel host's own look currently comes from a real XR UI shell
  skin: the shell does not exist yet, so `UguiFallbackPanelSurface` is a plain seam
  implementation with no rendered geometry (`CU-09`, marked `partial` in the coverage
  map).
- No claim that the seven editor controls render an actual slider, toggle, picker, or
  colour control: they are data-only stand-ins proven to attach one per kind and fail
  closed for an unsupported kind (`CU-06`, marked `partial`).
- No package id, catalog entry, maturity, release, or device status. Those exist only
  after admission and a green Unity gate.
