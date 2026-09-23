# XR UI shell verification contract

## Source gate

- Every derivation input is positive, public, and role-bounded in
  [`source-manifest.json`](source-manifest.json).
- No consumer project, private panel prefab, product menu layout, product skin, or
  prior product world-space UI implementation is a derivation input. The Inventory
  renderer adapters and XR compositions in this repository are context only: they
  become clients of the shell and contribute no derivation input and no evidence.
- A person confirms the source URLs, page paths, versions, and the maintenance
  state of each open-source implementation before the admission record is signed,
  because the authoring environment could not fetch them.

## Core gate

Deterministic tests must cover:

- shell surface identity: `PanelId`, `WristMenuId`, and `HandMenuId` with a
  canonical form, rejection of empty, whitespace, and malformed identifiers with a
  stable `identity.malformed` failure, value equality between two identities of the
  same type with the same text, and no equality across types; `InputSourceId`
  shares the same rules;
- a shell layout model: a panel declaration names one closed anchor kind (`world`,
  `head_locked`, `wrist`, `hand`) and the closed set of anchor kinds it admits; a
  wrist menu admits only `wrist` and a hand menu admits only `hand`; placement
  intents are a closed set (`open`, `close`, `focus`, `dock`, `follow`, `fold`,
  `unfold`) and each intent names exactly one declared panel, plus a target panel
  for `dock` and a target anchor kind for `follow`;
- intent application to an immutable shell state with stable failure codes: an
  intent naming an undeclared panel is rejected with `panel.unknown`; a `dock` or
  `follow` intent whose target anchor kind the panel does not admit is rejected
  with `anchor.kind.unsupported`; a `focus` intent while another open panel holds
  exclusive focus is rejected with `focus.conflict`; declaring a panel id twice is
  rejected with `panel.duplicate`; at most one panel is focused at any time;
- state immutability: a state is immutable, an accepted intent produces a new state
  and leaves the prior state intact, and a rejected intent leaves the prior state
  intact and returns it unchanged with the failure code;
- deterministic replay: the same intent sequence over the same declarations and
  initial state produces an equal final state and an equal fingerprint, and a state
  enumerates every declared panel with its current anchor kind, open flag, docked
  target, follow flag, the focused panel, and the registered input sources;
- pointer and gaze routing as typed intents: `hover`, `select`, and `scroll` each
  carry an `InputSourceId` whose registered kind is `ray`, `poke`, or `gaze`, plus
  the candidate panel set the adapter observed for that source; routing resolves to
  exactly one open panel, prefers the exclusively focused panel when it is a
  candidate, and otherwise returns `route.ambiguous` for two or more candidates and
  `route.none` for zero; a source that is not registered is rejected with
  `source.unknown`; a `select` or `scroll` from a `gaze` source without a
  registered commit source is rejected with `source.kind.unsupported`, because the
  head ray is not the primary pointer; a rejected routing intent changes no state;
- a skin contract: the design-language tokens (`surface.panel`,
  `surface.section`, `surface.accent`, `text.primary`, `text.muted`,
  `text.weight.body`, `text.weight.title`, `slot_states.normal`,
  `slot_states.hover`, `slot_states.selected`, `slot_states.disabled`,
  `shape.corner_radius.small`, `shape.corner_radius.medium`,
  `shape.corner_radius.large`, `hit_target.minimum`, `hit_target.primary`) map to a
  closed set of named shell slots (`panel.background`, `panel.section`,
  `panel.title.text`, `panel.body.text`, `panel.muted.text`, `panel.corner`,
  `menu.wrist.background`, `menu.hand.background`, `control.normal`,
  `control.hover`, `control.selected`, `control.disabled`, `control.accent`,
  `control.corner`, `control.hit.minimum`, `control.hit.primary`); a mapping that
  names a token outside the design-language set is rejected with `token.unknown`,
  a mapping that names a slot outside the closed set is rejected with
  `slot.unknown`, and a skin that leaves any slot without a mapping is rejected
  with `slot.unmapped`; the canonical mapping resolves every slot;
- an ornament: one shell-owned fixed surface that is never folded. It is not
  declared by a consumer, it never enters a built layout's declared-surface set or
  surface count, and a `fold` or `unfold` intent naming it is always rejected with
  a stable code, because it is the one surface the shell itself owns;
- a verb registry: a closed, explicitly registered id set that any client family
  registers into through the same entry point; an unregistered verb is rejected
  with a stable code and never silently absorbed into another verb or dropped;
  registered ids partition into wired (a resolver backs them) and unwired
  (reserved only); and a verb has one constant id and one constant display word,
  fixed at registration, because what varies from one press to the next is only
  the resolved target, never the verb's own word;
- a single-valued `FocusSubject` claimed only by an explicit focus intent: a
  per-frame mirror or sync of its current value never claims it, an assignment
  always replaces the whole value rather than merging with the prior one (so no
  target is special-cased), and it names exactly one current target — a panel, a
  panel item, or an external target path the consumer routes in — or none;
- verb resolution that reads that one `FocusSubject` value and nothing else, with
  no precedence chain and no branch order over any other source, and yields
  exactly one resolved target or a named no-target result; two different verbs
  resolved against the same `FocusSubject` value always agree on the target;
- a queryable pre-press affordance for every registered verb: the resolved
  target's name, whether it is available or unavailable, and a stable reason code,
  so a verb is never shown available for a target it would in fact refuse;
- fold retains state: a folded panel keeps its full open, docked-target, and
  follow state, because `fold` and `unfold` are intents that change only
  visibility, exactly like `open` and `close` change nothing else about a panel's
  placement; and
- structured results with stable failure codes for a malformed identity, an unknown
  or duplicate panel, an unsupported anchor kind, a focus conflict, an unknown
  source, an unsupported source kind, an ambiguous or empty route, an unknown token,
  an unknown slot, an unmapped slot, an ornament fold rejection, an unknown or
  duplicate verb, an unwired verb, and a verb with no current target, each carrying
  the offending field path, plus a valid intent sequence passing clean with every
  outcome accepted.

The Core references no engine type, reads no input device, computes no pose,
raycast, or world-space transform, holds no colour or size value, and renders
nothing.

## UGUI adapter gate

EditMode tests must cover:

- a thin binding of each declared panel to exactly one world-space Canvas subtree,
  with stable codes, field paths, and the source asset for a missing Canvas
  reference (`surface.missing`), a Canvas whose render mode is not world space
  (`surface.mode.mismatch`), a Canvas bound to two panels (`surface.duplicate`), a
  missing event camera (`camera.missing`), a missing tracked-device graphic
  raycaster (`raycaster.missing`), a scene without exactly one active XR UI input
  module (`input.module.missing`), and a wrist or hand anchor whose transform
  reference is missing (`anchor.target.missing`), and construction that throws with
  the same report;
- an explicit diagnostic for every by-name or optional resolution: an interactor
  member, an input action reference, an anchor transform, a Canvas component, or a
  sprite that the adapter resolves by name, by serialized field, or by an optional
  lookup must be pinned to the validated UGUI and XR Interaction Toolkit versions,
  must report an explicit failure with a stable code when the target is missing or
  incompatible, and must carry a negative test for the missing target (LESSON-004);
  silence or a reported success is a defect;
- one injectable skin seam, a `ScriptableObject` mapping the shared design-language
  tokens to the closed slot set, applied through one entry point on the panel
  runtime and propagated to every bound Canvas subtree, with a default skin asset
  carrying the canonical token values so an un-themed install already matches the
  shared look (LESSON-007); no colour, radius, or hit-size value lives in a
  serialized per-view field or an editor factory constant;
- translation of accepted routing results: the adapter reports the candidate panel
  set from the raycaster to the Core, forwards a resolved `hover`, `select`, or
  `scroll` to the bound Canvas only, and forwards nothing on `route.ambiguous`,
  `route.none`, or any rejection;
- a runtime constructed with explicit references that applies accepted placement
  intents to the bound anchors in the order they were accepted and reports accepted
  and rejected state, with no scene singleton, static instance, scene search, or
  reflection discovery; and
- two independent runtimes constructed side by side over two Canvas sets that share
  no state and do not interfere with each other's anchors, skins, or routing.

## UI Toolkit adapter gate

EditMode tests must cover:

- a thin binding of each declared panel to exactly one world-space `UIDocument`,
  with stable codes, field paths, and the source asset for a missing document
  reference (`surface.missing`), `PanelSettings` whose render mode is not world
  space (`surface.mode.mismatch`), a document bound to two panels
  (`surface.duplicate`), a document without a collider or with a collider update
  mode the composition does not admit (`collider.missing`), a scene without exactly
  one active XR UI Toolkit manager (`input.module.missing`), a manager or panel that
  bypasses UI Toolkit events (`input.bypassed`), and a wrist or hand anchor whose
  transform reference is missing (`anchor.target.missing`), and construction that
  throws with the same report;
- an explicit diagnostic for every by-name or optional resolution: a `VisualElement`
  looked up by name, a USS class or variable applied by name, a `PanelSettings`
  property read by reflection, or an interactor member resolved by an optional
  lookup must be pinned to the validated editor and XR Interaction Toolkit versions,
  must report an explicit failure with a stable code when the target is missing or
  incompatible, and must carry a negative test for the missing target (LESSON-004);
  silence or a reported success is a defect;
- one injectable skin seam, a skin object that maps the shared design-language
  tokens to the closed slot set and applies them as USS variables or classes on the
  bound document root, with a default skin carrying the canonical token values
  (LESSON-007); the seam is the only place that writes visual values into the
  document;
- translation of accepted routing results, a runtime constructed with explicit
  references, and two independent runtimes side by side, under the same clauses as
  the UGUI adapter gate, against `UIDocument` roots instead of Canvas subtrees.

## Sibling adapter rule

UGUI and UI Toolkit are sibling adapters of one renderer-neutral Core. Each has its
own package identity, assembly, namespace, skin seam, default skin, tests, and
compatibility profile. Automated or device evidence recorded for one adapter never
transfers to the other: a green UGUI EditMode run proves nothing about the UI
Toolkit adapter, and a Device Lab receipt for a UGUI composition proves nothing
about a UI Toolkit composition, even on the same device and commit.

No test claims that a panel was visible, legible, or reachable, that an anchor
followed a wrist or hand, that a ray or gaze hit a panel on a device, or that any
input was read from a device.

## Claim ceiling

An EditMode run proves identity validation, layout-intent application, state
immutability, deterministic replay, routing resolution, skin-contract validation,
and renderer binding validation for the tuple recorded in the compatibility
profile. It does not prove legibility, comfort, reach, hand-tracking behaviour,
eye-tracking behaviour, pointer precision, latency, frame timing, panel visibility
on a headset, or any named-device behaviour. Every claim about legibility, comfort,
reach, hand-tracking, or device behaviour requires a Device Lab receipt that binds
the observation to a full commit SHA, the exact dependency tuple, the renderer
composition, the posture, the measured duration, the named input sources, and the
tester identity; `not_tested` is never evidence.
