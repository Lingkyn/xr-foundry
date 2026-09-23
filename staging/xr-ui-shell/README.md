# XR UI shell staging

Status: **staging material, not a live package.** Nothing here is in the package
catalog, a batch, a compatibility profile, or a release. Every manifest is named
`package.staging.json` on purpose so repository validation does not treat any of
these directories as a live `com.lingkyn.*` package.

This directory holds the first implementation of the XR UI Shell family named in
`docs/standards/xr-ui-shell/README.md` (source-gate queue item `NEXT-XR-UI-SHELL`).
It exists because the production line cannot register a new package without a
verified Unity compatibility profile, and no Unity run has happened since the last
recorded evidence commit. The code is written and tested on paper against
[`verification-contract.md`](../../docs/standards/xr-ui-shell/verification-contract.md);
it has not compiled.

## What is here

| Path | Content |
| --- | --- |
| `com.lingkyn.xr-ui-shell.core/Runtime/*.cs` | Engine-light Core: `PanelId`, `WristMenuId`, `HandMenuId`, and `InputSourceId` with one canonical dotted-segment form and no cross-type equality; the closed `AnchorKind` set (world, head_locked, wrist, hand) and `InputSourceKind` set (ray, poke, gaze); the immutable `ShellLayout` and `ShellLayoutBuilder` built only by explicit panel/wrist-menu/hand-menu declaration and input-source registration; the closed placement-intent set (`OpenIntent`, `CloseIntent`, `FocusIntent`, `DockIntent`, `FollowIntent`) applied to the immutable `ShellState` with deterministic replay and a fingerprint; typed pointer and gaze routing (`HoverIntent`, `SelectIntent`, `ScrollIntent`) resolved by the stateless `ShellRouter`; and the skin contract (`DesignToken`, `ShellSlot`, `SkinMapping`, `SkinMappingBuilder`, `CanonicalSkinMapping`) mapping the shared design-language tokens to the closed shell-slot set |
| `com.lingkyn.xr-ui-shell.core/Tests/Editor/XrUiShellCoreContractTests.cs` | 42 EditMode tests mapped in `docs/standards/xr-ui-shell/coverage-map.json` |
| `com.lingkyn.xr-ui-shell.core/Samples~/ShellWalkthrough/` | Domain-only sample: a layout built by explicit declaration, a placement intent sequence, typed pointer and gaze routing including a gaze select rejected and then resolved by a commit source, the canonical skin mapping, and a replayed sequence; no asset, scene, or UnityEngine API |
| `com.lingkyn.xr-ui-shell.ugui/Runtime/*.cs` | UGUI sibling adapter: fail-closed binding of each declared panel to exactly one world-space `Canvas` subtree (`UguiPanelBindingEntry`, `UguiBindingValidation`, `UguiPanelBindingSet`) with stable codes for a missing Canvas, a non-world-space render mode, a duplicate Canvas, a missing event camera, a missing raycaster, a missing or non-unique active input module, and a missing wrist/hand anchor transform; the injectable `UguiShellSkin` `ScriptableObject` carrying the canonical token values; `UguiShellRuntime`, a plain runtime that applies accepted placement intents to the bound Canvas subtrees and forwards a resolved routed event to the bound Canvas only; and, under `Runtime/LiveTuning/`, `ShellTuningPanelSurfaceAdapter`, a small adapter satisfying the Live Tuning family's `ITuningPanelSurface` seam as one client of the shell |
| `com.lingkyn.xr-ui-shell.ugui/Tests/Editor/*.cs` | 19 EditMode tests: 18 for the adapter gate driven through fakes for every seam (`IUguiPanelSkinTarget`, `IUguiRoutedEventTarget`), plus one integration test that attaches a real Live Tuning panel host to a shell panel through `ShellTuningPanelSurfaceAdapter` |
| `com.lingkyn.xr-ui-shell.ui-toolkit/Runtime/*.cs` | UI Toolkit sibling adapter: fail-closed binding of each declared panel to exactly one world-space `UIDocument` (`UiToolkitPanelBindingEntry`, `UiToolkitBindingValidation`, `UiToolkitPanelBindingSet`) with stable codes for a missing document, a non-world-space `PanelSettings` render mode, a duplicate document, a missing or unadmitted-mode collider, a missing or non-unique active XR UI Toolkit manager, a manager that bypasses UI Toolkit events, and a missing wrist/hand anchor transform; the injectable `UiToolkitShellSkin` `ScriptableObject` carrying the canonical token values as USS-facing values; and `UiToolkitShellRuntime`, the UI Toolkit sibling of `UguiShellRuntime` |
| `com.lingkyn.xr-ui-shell.ui-toolkit/Tests/Editor/*.cs` | 19 EditMode tests for the adapter gate, driven through fakes for every seam (`IUiToolkitPanelSkinTarget`, `IUiToolkitRoutedEventTarget`, `IUiToolkitInputManager`) |
| `docs/standards/xr-ui-shell/` | Standard README, source manifest, verification contract, coverage map, admission draft |

## The sibling adapter rule

UGUI and UI Toolkit are sibling adapters of one renderer-neutral Core, exactly as
[`verification-contract.md`](../../docs/standards/xr-ui-shell/verification-contract.md#sibling-adapter-rule)
states it. Each package:

- has its own package identity, assembly, namespace, skin seam, default skin,
  tests, and compatibility profile;
- shares nothing with the other adapter's Runtime code, only the Core
  (`com.lingkyn.xr-ui-shell.ugui` and `com.lingkyn.xr-ui-shell.ui-toolkit` name no
  type, no test double, and no fake from one another); and
- never transfers evidence to the other: a green UGUI EditMode run proves nothing
  about the UI Toolkit adapter, and vice versa, even at the same commit. Their
  coverage-map gates, `ugui` and `ui-toolkit`, are recorded and read independently
  and each map row cites only its own adapter's tests.

The two adapters also read as siblings by construction: `UguiPanelBindingEntry` and
`UiToolkitPanelBindingEntry` carry the same shape of explicit reference (a surface,
an event/collider input path, an anchor transform, a skin target, a routed-event
target) translated into each renderer's own vocabulary (`Canvas`/`Camera`/raycaster
for UGUI, `UIDocument`/`PanelSettings`/collider for UI Toolkit), and
`UguiShellRuntime`/`UiToolkitShellRuntime` apply the same placement- and
routing-intent shape against their own bound surfaces. This parallel structure is
a design choice for legibility, not shared code: nothing in one package's assembly
references a type in the other's.

## How the Live Tuning panel host becomes a client without transferring evidence

`ShellTuningPanelSurfaceAdapter` (in `com.lingkyn.xr-ui-shell.ugui/Runtime/LiveTuning/`)
implements the Live Tuning family's `ITuningPanelSurface` seam
(`staging/live-tuning/com.lingkyn.live-tuning.unity/Runtime/TuningPanelHost.cs`) by
wrapping one bound `UguiPanelBindingEntry`. A real `TuningPanelHost` can attach its
per-tunable editor slots to this adapter exactly as it would to the Live Tuning
package's own `UguiFallbackPanelSurface`, proven by
`AShellPanelAdapterAttachesOneSlotFromARealLiveTuningPanelHost`, which constructs a
real `TuningRuntime` and calls `TuningPanelHost.AttachAll()` against it. Neither
family's assembly references the other's concrete skin or panel type: the join
happens only in this one adapter type, through the seam's own interface. This
package references `Lingkyn.LiveTuning.Core` and `Lingkyn.LiveTuning.Unity` directly
in its asmdef, because that reference is not circular (Live Tuning's own asmdefs
never reference this shell) and is small; the heavier fallback the work item allows
for — a small `ITuningPanelSurface`-compatible type built without referencing
`Lingkyn.LiveTuning.Unity` at all, kept behind a compile define — was not needed,
and the `Runtime/LiveTuning/` folder is kept separate so that fallback stays a
one-file change if a future dependency shape ever makes the direct reference
circular or heavy.

No evidence transfers either way: this integration test proves that the seam's
shape is satisfied in isolation from both families' own EditMode runs; it is not a
Live Tuning coverage claim (Live Tuning's own coverage map and its `CU-09` partial
clause are unaffected) and it is not a claim that a real Live Tuning panel is
reachable, legible, or visible in this shell.

## How the Inventory presentation adapters become clients

`docs/standards/xr-ui-shell/README.md` states the join in full. In short: an
Inventory renderer adapter (UGUI or UI Toolkit) becomes one client of the matching
shell adapter only inside a renderer-named XR composition, constructed with
explicit references and never through a scene search or a singleton. The shell
owns a panel's identity, anchor, open/close/focus/dock/follow state, and which
panel a pointer or gaze source targets; an Inventory adapter keeps
`IInventoryView.Render` and `InventorySlotIntent` unchanged and owns what is
rendered inside the panel the shell resolves for it. No Inventory evidence
transfers to the shell and no shell evidence transfers to Inventory: each
composition, EditMode receipt, and Device Lab plan proves only its own tuple.

## How it moves into the tree

One Unity run turns this into three live packages. The person or Agent with the
Editor:

1. Copies `docs/standards/xr-ui-shell/admission.draft.json` to
   `docs/foundry/admissions/xr-ui-shell.v1.json` (a maintainer decision) and writes
   the `xr-ui-shell-core`, `xr-ui-shell-ugui`, and `xr-ui-shell-ui-toolkit`
   blueprints under `docs/foundry/blueprints/`.
2. Runs `python scripts/scaffold_unity_package.py docs/foundry/blueprints/xr-ui-shell-core.v1.json --output-root . --write`
   (and the same for the `ugui` and `ui-toolkit` blueprints), then replaces the
   generated scaffold sources with the files here and renames each
   `package.staging.json` to `package.json`, adding `.meta` files for every asset.
3. Adds the three packages to `package-catalog.json`, `component-catalog.json`,
   `capability-registry.json`, a building batch, and the lessons-register
   dispositions listed in `docs/standards/xr-ui-shell/README.md`.
4. Runs `python scripts/run_unity_gates.py --host com.lingkyn.xr-ui-shell.core` and
   the same for the `ugui` and `ui-toolkit` hosts, and records a separate
   compatibility profile for each package from its own receipt; a green Core
   receipt proves nothing about either adapter, and a green adapter receipt proves
   nothing about the other adapter.
5. Runs the repository contract and the merge-readiness verdict, then opens the
   pull request.

Until step 4 happens for a given package, every claim about that package's code is
"authored, unexecuted".

## Non-claims

- No claim that any panel, wrist menu, or hand menu was visible, legible, or
  reachable, that an anchor followed a wrist or hand, that a ray or gaze hit a
  panel on a device, or that any input was read from a device (the contract's
  claim ceiling).
- No claim that the two adapters' skin seams produce the same rendered look: each
  skin's default values are this package's own canonical values, applied through
  its own renderer's own view code, which does not exist yet.
- No claim about Inventory's or Live Tuning's actual panel look: the join points
  described above are proven only as far as an EditMode test can prove a seam is
  satisfied, never as a rendered composition.
- No claim that the tracked-device graphic raycaster type this scaffold stands in
  for (`UnityEngine.EventSystems.BaseRaycaster` in the UGUI adapter) is the exact
  XR Interaction Toolkit type a real binder will target; a real binder pins the
  validated XR Interaction Toolkit version and targets its concrete raycaster type.
- No claim about the collider update mode a real world-space UI Toolkit
  composition needs beyond the one admitted value this scaffold checks for
  (`ColliderUpdateMode.Automatic`); a real composition's admitted set is set at its
  own gate.
- No package id, catalog entry, maturity, release, or device status. Those exist
  only after admission and a green Unity gate.
