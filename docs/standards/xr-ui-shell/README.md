# XR UI shell package-family standard

Status: proposal in the source-gate queue (`NEXT-XR-UI-SHELL`); the Core and both
renderer adapters are staged, authored and unexecuted, under `staging/xr-ui-shell/`
against the verification contract below, with a coverage map; no package directory
or package id exists yet, and nothing here has compiled or run in a Unity Editor

This standard defines a reusable world-space UI shell: panel, wrist-menu, and
hand-menu identity, a shell layout model (anchor kinds and placement intents)
applied to an immutable state with deterministic replay, pointer and gaze routing
that resolves a named input source to exactly one panel, and a skin contract that
maps the shared [design language](../design-language/README.md) tokens to a closed
set of named slots, with sibling UGUI and UI Toolkit adapters planned separately.
It does not define any product's menu structure, panel content, brand skin, prefab
hierarchy, or placement values. It is derived only from the positive public sources
in [`source-manifest.json`](source-manifest.json).

## Capability boundary

The family separates:

- immutable shell surface identity (`PanelId`, `WristMenuId`, `HandMenuId`) and
  input source identity (`InputSourceId`) from any Canvas, document, transform,
  interactor, or hand pose that implements them;
- a shell layout model as a closed set of anchor kinds (`world`, `head_locked`,
  `wrist`, `hand`) and placement intents (`open`, `close`, `focus`, `dock`,
  `follow`) that a panel declaration admits or rejects, from any solver, follow
  script, or scene hierarchy that moves a surface;
- pointer and gaze routing as typed intents (`hover`, `select`, `scroll` from a
  registered `ray`, `poke`, or `gaze` source with the candidate panel set the
  adapter observed) that resolve to exactly one open panel, from any raycaster,
  event system, input module, or eye-tracking runtime;
- a skin contract that maps the design-language tokens to a closed set of named
  slots and rejects an unknown token, an unknown slot, or an unmapped slot, from
  any colour, sprite, stylesheet, or font that renders a slot;
- deterministic immutable state produced by a sequence of intents from any
  rendering, animation, or pose behaviour; and
- structured results with stable failure codes (`identity.malformed`,
  `panel.unknown`, `panel.duplicate`, `anchor.kind.unsupported`, `focus.conflict`,
  `source.unknown`, `source.kind.unsupported`, `route.ambiguous`, `route.none`,
  `token.unknown`, `slot.unknown`, `slot.unmapped`) from any editor tooling or
  platform review.

The family does not own the XR Origin, the camera, hand or eye tracking, pose
thresholds, raycasting, the event system, the input module, rendering, fonts,
sprites, stylesheets, panel content, or any claim about legibility, comfort, reach,
or hand-tracking behaviour in a headset.

## Planned package boundary

| Layer | Owns | Must not own |
| --- | --- | --- |
| Engine-light Core (package id reserved only after admission) | panel, menu, and source identity; anchor kinds and placement intents; immutable shell state, deterministic replay, and fingerprint; routing resolution over adapter-observed candidates; the token-to-slot skin contract and its validation; structured results | Unity or toolkit types, transforms, poses, raycasts, colours, sizes, stylesheets, product menus, content, placement values |
| UGUI adapter (package id reserved only after admission) | binding of each panel to one world-space Canvas subtree with fail-closed validation and stable codes; explicit diagnostics for every by-name or optional resolution; one injectable `ScriptableObject` skin seam mapping the shared tokens and a default skin with the canonical values; forwarding of resolved routes to the bound Canvas; a plain runtime constructed with explicit references | identity rules, anchor semantics, routing resolution, slot set, scene singletons, static instances, scene search, reflection discovery, UI Toolkit types or evidence |
| UI Toolkit adapter (package id reserved only after admission) | binding of each panel to one world-space `UIDocument` with fail-closed validation and stable codes; explicit diagnostics for every by-name or optional resolution; one injectable skin seam that writes the shared tokens as USS variables or classes and a default skin with the canonical values; forwarding of resolved routes to the bound document; a plain runtime constructed with explicit references | identity rules, anchor semantics, routing resolution, slot set, scene singletons, static instances, scene search, reflection discovery, UGUI types or evidence |

The two adapters are siblings of one Core, each with its own package identity,
assembly, namespace, tests, and compatibility profile, following the
renderer-explicit rule of the
[Inventory renderer-neutral architecture](../inventory/renderer-neutral-architecture.md).
Automated or device evidence never transfers between them.

## Design-language adoption

The shell adopts the shared design language as its visual and interaction
reference. The Core carries the token names of
[`ui-design-language-standard.json`](../design-language/ui-design-language-standard.json)
(surface, text, slot-state, corner-radius, and hit-target tokens) and the closed
slot set, but no value. Each adapter's default skin carries the canonical values,
and every hover, selected, and disabled visual follows the design-language state
model. Vision Pro is the visual reference for the panel as a placed, translucent,
stable window with docked secondary surfaces; PICO and Meta Horizon OS are the
interaction references for world-anchored placement, transient head-locked use,
lagged follow, palm-summoned hand menus, and controller-ray pointing with the head
ray never the primary pointer.

## Inventory presentation as one client

The Inventory family already ships a renderer-neutral presentation contract in
[`com.lingkyn.inventory.presentation`](../../../packages/unity/systems/inventory/com.lingkyn.inventory.presentation/README.md)
whose runtime is engine-free and whose UGUI and UI Toolkit adapters each implement
`IInventoryView` and today host their own world-space surface inside the
renderer-named XR compositions. When this family goes live, an Inventory renderer
adapter becomes one client of the shell:

- the shell owns the panel's identity, anchor, open, close, focus, dock, and follow
  state and resolves which panel a pointer or gaze source targets; the Inventory
  adapter owns what is rendered inside that panel and keeps `IInventoryView.Render`
  and `InventorySlotIntent` unchanged;
- the join happens only in a renderer-named XR composition: the UGUI Inventory
  composition pairs with the shell's UGUI adapter, and the UI Toolkit Inventory
  composition pairs with the shell's UI Toolkit adapter, each constructed with
  explicit references and never through a scene search or a singleton;
- the Inventory presentation package never references the shell, keeps
  `noEngineReferences`, and stays installable without it; the Inventory renderer
  adapters do not depend on the shell either, so a consumer that wants no shell
  loses nothing;
- the shell's slot set is the shell's own; an Inventory adapter keeps its own skin
  seam for grid, slot, and details visuals and may map the same design-language
  tokens, but neither skin is applied through the other; and
- no Inventory evidence transfers to the shell: Inventory EditMode receipts,
  coverage maps, compatibility profiles, and the Device Lab plan
  [`inventory-world-space-ui-v1.json`](../../device-lab/test-plans/inventory-world-space-ui-v1.json)
  prove Inventory compositions without the shell. A composition that includes the
  shell is a new tuple with its own profile, its own gate, and its own device
  receipt, and the shell's own evidence proves nothing about Inventory.

The [Interaction family](../interaction/README.md) routes semantic intents to
handlers; the shell resolves which panel a pointer event targets. The two do not
duplicate each other, and a seam that lets a shell panel act as an Interaction
context is a later proposal, not part of this gate.

## Evidence boundary

An EditMode test can prove identity validation, layout-intent application, state
immutability, deterministic replay, routing resolution, skin-contract validation,
and renderer binding validation for the declared tuple. It cannot prove that a
panel was visible, legible, or reachable, that an anchor followed a wrist or hand,
that a ray or gaze hit a panel on a device, or anything about comfort, precision,
latency, hand tracking, eye tracking, or named-device behaviour; those claims
require a Device Lab receipt as [`verification-contract.md`](verification-contract.md)
states in its claim ceiling.

## Lessons register dispositions (to be added to the register when the family goes live)

| Lesson | Disposition | Rationale |
| --- | --- | --- |
| LESSON-001 closed classification vs open tags | adopted | Anchor kinds, placement intents, source kinds, and the skin slot set are closed enums and panel, menu, and source ids are validated identities; any consumer grouping of panels or menu items stays in open metadata outside Core |
| LESSON-002 migration path | deferred | No release exists yet; the first release records its compatibility policy and the first breaking change ships a migration note in the same commit |
| LESSON-003 single-workstation gate | adopted | Tests will run in `run_unity_gates.py` and the consumer workflow like every family; no gate depends on a maintainer workstation |
| LESSON-004 by-name resolution fails closed | adopted | The Core resolves nothing by name; each adapter pins the editor, UGUI, and toolkit versions, reports every missing Canvas, document, event camera, raycaster, input module, manager, anchor transform, element name, or stylesheet variable with a stable code, and carries a negative test for each missing target |
| LESSON-005 clause coverage | deferred | No staged tests exist; the staged implementation item writes `coverage-map.json` mapping every Core, UGUI adapter, and UI Toolkit adapter clause to named tests before any clause is cited as evidence |
| LESSON-006 sibling pinning | deferred | Applies at the first Git-consumer validation; the README install matrix will pin the Core and each adapter to one full SHA |
| LESSON-007 skin seam | adopted | Both adapters ship one injectable skin seam mapping the shared design-language tokens and a default skin with the canonical values in their first version; the Core holds token and slot names and no value, and no visual value lives in a serialized per-view field or an editor factory constant |
| LESSON-008 version bump is a verification claim | adopted | No package version exists; the first scaffold enters at 0.1.0 and stays there until its first verified profile |

## Next steps

1. A person confirms every URL, page path, version pin, and open-source
   maintenance state in [`source-manifest.json`](source-manifest.json).
2. The maintainer copies [`admission.draft.json`](admission.draft.json) into
   `docs/foundry/admissions/` as the durable record through the
   [system admission gate](../../foundry/system-admission.md).
3. The staged implementation exists: the Core and both renderer adapters are
   authored under `staging/xr-ui-shell/` against
   [`verification-contract.md`](verification-contract.md), with
   [`coverage-map.json`](coverage-map.json) mapping every Core, UGUI, and UI
   Toolkit clause to named tests. The code is authored and unexecuted — it has not
   compiled in a Unity Editor — so nothing enters `packages/` before admission and
   a green gate, and each adapter will be gated on its own tuple; see
   [`staging/xr-ui-shell/README.md`](../../../staging/xr-ui-shell/README.md) for
   what is staged and the promotion steps.

See also:

- [`verification-contract.md`](verification-contract.md)
- [`source-manifest.json`](source-manifest.json)
- [`admission.draft.json`](admission.draft.json)
- [`next-batch.json`](../../foundry/queue/next-batch.json) queue entry and the
  [system admission gate](../../foundry/system-admission.md)
- [`lessons-register.json`](../lessons/lessons-register.json)
- [`design-language/README.md`](../design-language/README.md), the visual and
  interaction reference this family adopts
- [`inventory/README.md`](../inventory/README.md), the first client family
- [`locomotion/README.md`](../locomotion/README.md), the sibling family this
  standard is modelled on
