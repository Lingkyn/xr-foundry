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
| `com.lingkyn.xr-ui-shell.core/Runtime/*.cs` | Engine-light Core: `PanelId`, `WristMenuId`, `HandMenuId`, and `InputSourceId` with one canonical dotted-segment form and no cross-type equality; the closed `AnchorKind` set (world, head_locked, wrist, hand) and `InputSourceKind` set (ray, poke, gaze); the immutable `ShellLayout` and `ShellLayoutBuilder` built only by explicit panel/wrist-menu/hand-menu declaration and input-source registration; the closed placement-intent set (`OpenIntent`, `CloseIntent`, `FocusIntent`, `DockIntent`, `FollowIntent`, `FoldIntent`, `UnfoldIntent`) applied to the immutable `ShellState` with deterministic replay and a fingerprint (fold/unfold change only a surface's `IsFolded` visibility flag, never its open/docked/following state); typed pointer and gaze routing (`HoverIntent`, `SelectIntent`, `ScrollIntent`) resolved by the stateless `ShellRouter`; the skin contract (`DesignToken`, `ShellSlot`, `SkinMapping`, `SkinMappingBuilder`, `CanonicalSkinMapping`) mapping the shared design-language tokens to the closed shell-slot set; `ShellOrnament`, the one shell-owned fixed surface (`SurfaceId.TheOrnament`) that never enters a declared layout and that `FoldIntent`/`UnfoldIntent` always reject; the closed `VerbRegistry`/`VerbRegistryBuilder` (a verb's id and display word are fixed at registration, and registered ids partition into wired/unwired); the single-valued `FocusSubject`/`FocusTarget` claimed only by an explicit `Claim` (never a per-frame mirror); `VerbResolver` and `VerbAffordanceQuery`, which read only a registry and a `FocusSubject` to yield exactly one target, a named no-target result, or a pre-press affordance that is never available for a target a press would refuse; and `IShellPanelContent<TSlot>` (`ShellPanelContent.cs`), the one generic panel-content seam a client family adapts to, which the shell never implements or inspects |
| `com.lingkyn.xr-ui-shell.core/Tests/Editor/XrUiShellCoreContractTests.cs` | 61 EditMode tests mapped in `docs/standards/xr-ui-shell/coverage-map.json` |
| `com.lingkyn.xr-ui-shell.core/Tests/Editor/XrUiShellSourceRuleTests.cs` | 2 EditMode tests proving no asmdef under this family references any assembly outside `Lingkyn.XrUiShell.*` (plus the admitted engine/test assemblies) and no shell `Runtime` source mentions `LiveTuning`, `Inventory`, `Settings`, or another family namespace — the machine-checked half of the peer-client rule below |
| `com.lingkyn.xr-ui-shell.core/Samples~/ShellWalkthrough/` | Domain-only sample: a layout built by explicit declaration, a placement intent sequence, typed pointer and gaze routing including a gaze select rejected and then resolved by a commit source, the canonical skin mapping, and a replayed sequence; no asset, scene, or UnityEngine API |
| `com.lingkyn.xr-ui-shell.ugui/Runtime/*.cs` | UGUI sibling adapter: fail-closed binding of each declared panel to exactly one world-space `Canvas` subtree (`UguiPanelBindingEntry`, `UguiBindingValidation`, `UguiPanelBindingSet`) with stable codes for a missing Canvas, a non-world-space render mode, a duplicate Canvas, a missing event camera, a missing raycaster, a missing or non-unique active input module, and a missing wrist/hand anchor transform; the injectable `UguiShellSkin` `ScriptableObject` carrying the canonical token values; and `UguiShellRuntime`, a plain runtime that applies accepted placement intents to the bound Canvas subtrees and forwards a resolved routed event to the bound Canvas only. This package references only `Lingkyn.XrUiShell.Core`: it names no client family (see the peer-client rule below) |
| `com.lingkyn.xr-ui-shell.ugui/Tests/Editor/*.cs` | 18 EditMode tests for the adapter gate, driven through fakes for every seam (`IUguiPanelSkinTarget`, `IUguiRoutedEventTarget`) |
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

## The dock and its peer clients (LESSON-010)

The shell is the dock. Every system's panel — Inventory, Settings, the Live
Tuning developer scaffold, or any future family — is a peer client of the dock,
never the other way around. Concretely:

- the shell exposes exactly one generic seam a client family adapts to:
  `IShellPanelContent<TSlot>` (`ShellPanelContent.cs`) for panel content, plus the
  verb-registration entry point on `VerbRegistryBuilder` (`VerbRegistry.cs`) for
  a client's own verb (its constant id and display word, never a shell-side
  special case);
- a client receives the currently focused thing through the shell's
  `FocusSubject`, resolves its own verb through `VerbResolver`/`VerbAffordanceQuery`,
  and maps whatever it reads there through its own domain (for Live Tuning, the
  Core `BindingIndex`); and
- the dock never references a client: no asmdef under `staging/xr-ui-shell/`
  names an assembly outside `Lingkyn.XrUiShell.*` (plus the admitted engine/test
  assemblies), and no shell `Runtime` source mentions `LiveTuning`, `Inventory`,
  `Settings`, or another family namespace, proven by
  `XrUiShellSourceRuleTests.cs`.

Live Tuning is the first concrete peer client. Its panel host, its `tune` verb
registration, and its adapter of this shell's generic content seam
(`ShellTuningClient`) live entirely in
[`staging/live-tuning/com.lingkyn.live-tuning.unity/Runtime/Shell/ShellTuningClient.cs`](../live-tuning/com.lingkyn.live-tuning.unity/Runtime/Shell/ShellTuningClient.cs),
not here: this package used to carry a `Runtime/LiveTuning/` folder and asmdef
references onto `Lingkyn.LiveTuning.*` (the shell depending on one specific
tooling client), which was the defect LESSON-010 records and this change fixes.
No evidence transfers either way: the shell's own EditMode run proves only that
its generic seams exist and validate; Live Tuning's own coverage map proves that
a real `TuningPanelHost` attaches to them.

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
