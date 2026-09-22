# Live tuning package-family standard

Status: proposal in the source-gate queue (`NEXT-LIVE-TUNING`), depending on
`NEXT-XR-UI-SHELL` for the panel host; the Core and Unity adapter implementation is
staged under [`staging/live-tuning/`](../../../staging/live-tuning/README.md),
authored and unexecuted (no Unity run has produced a receipt yet), and no package
directory or package id exists

This standard defines an in-headset developer tool for tuning typed parameters,
above all the shared [design-language](../design-language/README.md) tokens
(colour, size, corner radius, spacing, opacity, type scale) and any family's skin
values, while wearing the headset and seeing the effect immediately, with the
result exported back into the token file or the asset instead of being lost when
Play mode ends. A colour, a size, or a corner radius cannot be judged on a monitor:
a translucent spatial surface takes its appearance from the environment behind it
and from depth, and the engine discards Play-mode changes on exit. It does not
define any product's token values, skin, tuning session, or panel layout. It is
derived only from the positive public sources in
[`source-manifest.json`](source-manifest.json).

## Capability boundary

The family separates:

- immutable tunable identity (`TunableId`, `SnapshotId`) from any asset, field,
  token path, or control that implements a tunable;
- a closed set of tunable kinds (`float` with range and step, `integer` with range,
  `bool`, `enumerated` with a value set, `colour` as four unit floats, `vector2`
  and `vector3` with per-axis ranges) from any engine colour, vector, or property
  type;
- an immutable `TunableRegistry` built by explicit registration (id, kind, default,
  range or value set, group, label) that rejects a duplicate id and a default
  outside its own range, from any reflection, attribute scan, or field discovery;
- tuning intents (`set`, `reset`, `reset_all`, `snapshot`, `apply_snapshot`,
  `export`) applied to an immutable `TuningState` whose overrides are the only diff
  from defaults, from any control, panel, or input that raises them;
- a closed editor-kind set with exactly one editor kind per tunable kind, resolved
  by kind and never by location, from any package, skin, or scene that owns a
  target;
- engine-free binding records (tunable id, kind, opaque target path, scope
  metadata) from the binder that resolves a target in an engine;
- a token bridge that turns the design-language token document into a registry
  deterministically and a state's overrides back into a token-override document,
  from any file, asset, or codec that stores either;
- deterministic immutable state produced by a sequence of intents from any live
  application, export, or rendering behaviour; and
- structured results with stable failure codes (`identity.malformed`,
  `kind.declaration.invalid`, `tunable.duplicate`, `default.out_of_range`,
  `tunable.unknown`, `tunable.kind.mismatch`, `tunable.out_of_range`,
  `tunable.kind.unsupported`, `snapshot.unknown`, `binding.kind.mismatch`,
  `binding.duplicate`, `token.unsupported`, `token.range.missing`) from any editor
  tooling or platform review.

The family is a developer tool. It is not a runtime settings menu for players
(that is the [Settings family](../settings/README.md)), not a profiler or metrics
overlay, and not an inspector of arbitrary objects: it tunes only what has been
explicitly registered and bound, discovers nothing by reflection or scene search,
and must never ship to players. It does not own panel placement, anchoring, or
pointer routing (the [XR UI shell](../xr-ui-shell/README.md)), the skin seams it
writes through (each renderer adapter), the token document (the design language),
or any claim that a tuned value looks right in a headset.

## Scaffold decoupled by kind, never by location

The scaffold (the panel host and its editors) is the fixed part; targets are the
variable part. The same scaffold serves every building: you carry the colour
scaffold to whichever wall needs paint. The rules, each a named clause in
[`verification-contract.md`](verification-contract.md):

1. **One editor per kind.** Exactly one editor exists per `TunableKind`: one
   colour editor, one float editor (a size, a spacing value, a corner radius, a
   type-scale value, and an opacity are all floats or integers edited by that
   float or integer editor), one bool editor, one enumerated editor, one vector
   editor per vector kind. A colour anywhere in any package, skin, or scene is
   tuned by the same colour editor. Registering N colour tunables yields one
   editor kind resolved N times.
2. **Targets attach through binding records.** A binding record is data: a
   `TunableId`, its kind, an opaque target path, and scope metadata. The scaffold
   never knows what it is tuning. Adding a new tunable target adds a binding
   record, and for a new asset type a hand-written binder, never a new control,
   panel, or editor class.
3. **The panel is a host with one slot per bound tunable.** The host resolves
   each slot's editor by kind at attach time; any content attaches to the same
   host. The host has no per-family, per-package, per-skin, or per-screen code
   path.
4. **Editors are a closed set matching `TunableKind`.** A kind without an editor
   fails closed at attach time with `tunable.kind.unsupported` and a diagnostic
   naming the tunable, never a silent empty control.
5. **Grouping and scoping are metadata.** A package, skin, or screen label on the
   binding record filters the view and never changes which editor a slot holds.

## Planned package boundary

| Layer | Owns | Must not own |
| --- | --- | --- |
| Engine-light Core `com.lingkyn.live-tuning.core` (package id reserved only after admission) | tunable and snapshot identity; the closed kind set and kind-declaration validation; the immutable registry and explicit registration; intents, immutable state, override-only diff, deterministic replay, and fingerprint; the closed editor-kind set and `ResolveEditorKind`; engine-free binding records and their validation; the token bridge in both directions; structured results | Unity types, `UnityEngine.Color`, assets, files, codecs, controls, panels, product token values, skins, target resolution |
| Unity adapter `com.lingkyn.live-tuning.unity` (package id reserved only after admission) | explicit target resolution from binding records through hand-written binders per asset type with fail-closed validation and stable codes; explicit diagnostics for every by-name or optional resolution; a runtime constructed with explicit references that applies accepted overrides live through each renderer adapter's existing skin seam; the injectable `ITuningExportSink` with a device-side JSON override sink under the persistent data path and an Editor-side writer into the token JSON or the `ScriptableObject` asset; explicit import of an override file; exactly one editor implementation per editor kind; the panel host as one client of the XR UI shell with a minimal UGUI world-space fallback as the interim | identity rules, kind semantics, intent semantics, editor-kind resolution, binding validation rules, panel placement or routing, the shell's skin, product token values, scene singletons, static instances, scene search, reflection discovery, platform detection |

## Why it composes rather than duplicates

- **Design language.** The token document in
  [`ui-design-language-standard.json`](../design-language/ui-design-language-standard.json)
  is the primary input and output: the bridge derives every tunable id from a
  token path, the Editor writer writes overrides back into that document, and the
  family holds no token value of its own. The design language stays the single
  source of canonical values; live tuning changes them only through an exported
  override document a person reviews.
- **Skin seams.** Each renderer adapter already exposes one injectable skin seam
  ([`InventorySkin`](../../../packages/unity/systems/inventory/com.lingkyn.inventory.ugui/Runtime/InventorySkin.cs),
  [`InventoryUiToolkitSkin`](../../../packages/unity/systems/inventory/com.lingkyn.inventory.uitoolkit/Runtime/InventoryUiToolkitSkin.cs))
  and one apply entry point. Live tuning writes the accepted value into the
  injected skin instance through a binder's closed member set and re-invokes that
  entry point; it never reaches a view, a field, or a stylesheet directly, so the
  seam remains the only place visual values enter a renderer (LESSON-007). The
  two seams are sibling targets and evidence never transfers between them.
- **Settings.** The Settings family owns player-facing typed settings with
  profiles, transactions, applicators, and persistence. Live tuning shares its
  fail-closed typed-definition discipline but is developer-facing, has no
  profiles, scopes, or transactions, and must never ship to players, so it reuses
  none of Settings' storage or application logic. A Settings definition may be
  exposed as a tunable only through an explicit bridge that forwards accepted
  `set` intents as Settings intents; that bridge is a later proposal, not part of
  this gate.
- **XR UI shell.** The panel host is one shell panel: the shell owns its
  identity, anchor, open, close, focus, dock, and follow state, routes the pointer
  to it, and skins it. Live tuning owns only the slots and editors inside. The
  family therefore depends on `NEXT-XR-UI-SHELL`; until the shell's UGUI adapter
  is live, a minimal UGUI world-space fallback host constructed with explicit
  references satisfies the same clauses and is replaced without a Core, editor, or
  binding change.

## Evidence boundary

An EditMode test can prove identity and kind validation, registry construction,
intent application, override-only state, immutability, deterministic replay,
editor-kind resolution, binding validation, token-bridge round trip, live
application through the skin seam, export and import through the sinks, and
host-to-intent translation for the declared tuple. It proves the plumbing only.
It cannot prove that a colour, size, corner radius, spacing, opacity, or type
scale looks right, is legible, has sufficient contrast, is comfortable, or is
reachable, that the host responds with any latency, or anything about
named-device behaviour; whether a tuned value looks right is a human judgement
recorded in a Device Lab receipt, as [`verification-contract.md`](verification-contract.md)
states in its claim ceiling.

## Lessons register dispositions (to be added to the register when the family goes live)

| Lesson | Disposition | Rationale |
| --- | --- | --- |
| LESSON-001 closed classification vs open tags | adopted | Tunable kinds and editor kinds are closed enums and tunable and snapshot ids are validated identities; the group, label, and scope metadata on registrations and binding records are open metadata that filter a view and never drive validation or editor resolution |
| LESSON-002 migration path | deferred | No release exists yet; the first release records its compatibility policy, the override document carries a schema id from the first version, and the first breaking change ships a migration note in the same commit |
| LESSON-003 single-workstation gate | adopted | Tests will run in `run_unity_gates.py` and the consumer workflow like every family; the Editor sink's tests run in EditMode against a temporary asset, and no gate depends on a maintainer workstation |
| LESSON-004 by-name resolution fails closed | adopted | The Core resolves nothing by name and treats a target path as opaque; the Unity adapter pins the editor, UGUI, and toolkit versions, reports every missing asset, member, file path, input module, event camera, or Editor lookup with a stable code, and carries a negative test for each missing target; an unsupported editor kind fails closed at attach time |
| LESSON-005 clause coverage | deferred | No staged tests exist; the staged implementation item writes `coverage-map.json` mapping every Core and Unity adapter clause, including the five scaffold rules, to named tests before any clause is cited as evidence |
| LESSON-006 sibling pinning | deferred | Applies at the first Git-consumer validation; the README install matrix will pin the Core, the Unity adapter, and the shell and skin packages a composition binds to one full SHA |
| LESSON-007 skin seam | adopted | The family writes only through each renderer adapter's existing skin seam and apply entry point and adds no visual value of its own; the host's own look comes from the shell's skin seam, never from the tunables it edits, and no colour, radius, or size lives in an editor, host, or serialized per-view field |
| LESSON-008 version bump is a verification claim | adopted | No package version exists; the first scaffold enters at 0.1.0 and stays there until its first verified profile |

## Next steps

1. A person confirms every URL, page path, version pin, license, and open-source
   maintenance state in [`source-manifest.json`](source-manifest.json).
2. The maintainer copies [`admission.draft.json`](admission.draft.json) into
   `docs/foundry/admissions/` as the durable record through the
   [system admission gate](../../foundry/system-admission.md), after the XR UI
   shell candidate this family's panel host depends on has been decided.
3. Done: the staged implementation authors the Core and the Unity adapter under
   [`staging/live-tuning/`](../../../staging/live-tuning/README.md) against
   [`verification-contract.md`](verification-contract.md) and writes
   [`coverage-map.json`](coverage-map.json); the code is authored and unexecuted
   (no Unity run has produced a compatibility profile), binds to a self-contained
   reference skin asset rather than the Inventory skin seams (which stay the first
   bound target once a Unity run is available to prove it), and nothing enters
   `packages/` before admission and a green gate.
4. The first Device Lab plan for this family records a tuning session as a human
   judgement of token values, bound to the override document digest, and never as
   a legibility, contrast, comfort, or latency claim.

See also:

- [`verification-contract.md`](verification-contract.md)
- [`source-manifest.json`](source-manifest.json)
- [`admission.draft.json`](admission.draft.json)
- [`next-batch.json`](../../foundry/queue/next-batch.json), the queue this
  candidate joins, and the [system admission gate](../../foundry/system-admission.md)
- [`lessons-register.json`](../lessons/lessons-register.json)
- [`design-language/README.md`](../design-language/README.md), the token document
  this family tunes
- [`xr-ui-shell/README.md`](../xr-ui-shell/README.md), the panel host's shell
- [`settings/README.md`](../settings/README.md), the player-facing sibling this
  family does not duplicate
- [`inventory/README.md`](../inventory/README.md), whose renderer adapters expose
  the first bound skin seams
- [`locomotion/README.md`](../locomotion/README.md), the sibling family this
  standard is modelled on
