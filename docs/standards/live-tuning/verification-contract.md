# Live tuning verification contract

## Source gate

- Every derivation input is positive, public, and role-bounded in
  [`source-manifest.json`](source-manifest.json).
- No consumer project, private skin asset, product token values, product tuning
  session, or prior product tweak panel is a derivation input. The shared
  design-language token document, the Inventory renderer adapters' skin seams, the
  XR UI shell standard, and the Settings standard in this repository are context
  only: they are the things this family tunes, hosts in, or bridges to, and they
  contribute no derivation input and no evidence.
- A person confirms the source URLs, page paths, versions, the license, and the
  maintenance state of each open-source implementation before the admission record
  is signed, because the authoring environment could not fetch them.

## Core gate

Deterministic tests must cover:

- tunable identity: `TunableId` with a canonical form (lower-case segments joined
  by `.`), rejection of empty, whitespace, and malformed identifiers with a stable
  `identity.malformed` failure, and value equality between two identities with the
  same text; `SnapshotId` shares the same rules and has no equality with a
  `TunableId` of the same text;
- a closed set of tunable kinds: `float` with a closed numeric range and a positive
  step, `integer` with a closed integer range, `bool`, `enumerated` with a
  non-empty closed value set, `colour` as four unit floats (red, green, blue,
  alpha, each in `[0, 1]`), `vector2` and `vector3` with a closed range per axis; a
  kind declaration with an inverted range, a non-positive step, a non-finite
  bound, or an empty value set is rejected with `kind.declaration.invalid`;
- an immutable `TunableRegistry` built by explicit registration of (id, kind,
  default, range or value set, group, label): registering an id twice is rejected
  with `tunable.duplicate`, a default outside its own range or value set is
  rejected with `default.out_of_range`, a rejected registration leaves the registry
  unchanged, and a built registry enumerates its tunables in canonical id order
  with their group and label and cannot be mutated afterwards;
- tuning intents applied to an immutable `TuningState`: `set` (id, value), `reset`
  (id), `reset_all`, `snapshot` (snapshot id), `apply_snapshot` (snapshot id), and
  `export`; an intent naming an unregistered id is rejected with
  `tunable.unknown`, a `set` whose value kind differs from the registered kind is
  rejected with `tunable.kind.mismatch`, a `set` whose value is outside the
  registered range or value set (per axis for vectors, per channel for colours) is
  rejected with `tunable.out_of_range`, an `apply_snapshot` naming a snapshot the
  state does not hold is rejected with `snapshot.unknown`, and `snapshot` with an
  id the state already holds replaces that snapshot;
- overrides as the only diff: a state holds overrides and snapshots only; a `set`
  equal to the registered default removes the override rather than storing it; a
  `reset` removes one override and a `reset_all` removes every override while
  snapshots stay; `apply_snapshot` replaces the override set with the snapshot's
  override set; the effective value of every tunable is its override when present
  and its default otherwise;
- state immutability: a state is immutable, an accepted intent produces a new state
  and leaves the prior state intact, and a rejected intent leaves the prior state
  intact and returns it unchanged with the failure code;
- deterministic replay: the same intent sequence over the same registry and
  initial state produces an equal final state and an equal fingerprint; the
  fingerprint covers the registry fingerprint, the overrides in canonical id order,
  and the snapshots in canonical id order; two states with the same overrides
  reached by different sequences have equal fingerprints;
- a closed editor-kind set decoupled by kind, never by location: `EditorKind` is a
  closed set with exactly one member per `TunableKind` (`float_editor`,
  `integer_editor`, `bool_editor`, `enumerated_editor`, `colour_editor`,
  `vector2_editor`, `vector3_editor`), and `ResolveEditorKind(TunableKind)` is a
  total, pure function over the closed set; registering N `colour` tunables from
  any number of packages, skins, groups, or token paths and resolving each one
  yields the single `colour_editor` kind N times and no other kind; a kind value
  outside the closed set (a defect in an adapter or a future kind added without an
  editor) is rejected with `tunable.kind.unsupported` and never resolves to an
  empty or default editor;
- engine-free binding records: a `BindingRecord` names one `TunableId`, the
  tunable's kind, a target path (an opaque locator string the adapter resolves,
  never interpreted by the Core), and open scope metadata (package, skin, or
  screen labels) for filtering; a record whose id is not registered is rejected
  with `tunable.unknown`, whose kind differs from the registered kind with
  `binding.kind.mismatch`, and a second record for the same id with
  `binding.duplicate`; a validated binding set enumerates in canonical id order;
  scope metadata changes which records a filter returns and never changes the
  editor kind a record resolves to, tested by resolving the same `colour` record
  under every scope label to `colour_editor`;
- a token bridge from the design-language document to a registry: given the
  token document, a closed annotation-key set, and an explicit range policy, every
  token leaf becomes exactly one tunable whose id is derived from the token path
  (`surface.panel`, `slot_states.hover`, `shape.corner_radius_scale_px.small`,
  `hit_target_dp.minimum`), a four-element unit-float array becomes a `colour`, an
  integer leaf an `integer`, a non-integer number a `float`, a boolean a `bool`,
  and a string leaf with a declared value set an `enumerated`; a leaf under an
  annotation key (`role`, `intent`, `note`, `technique`, `rounded_via`,
  `small_visual_rule`, `slot`, `bundled`) is skipped; a leaf that is neither a
  tunable kind nor an annotation is rejected with `token.unsupported`; a numeric
  leaf without a range-policy entry is rejected with `token.range.missing`; the
  same document, annotation set, and policy always produce an equal registry
  fingerprint, and a document that would yield two tunables with one id is
  rejected with `tunable.duplicate`;
- a token bridge from a state back to a token-override document: the document
  carries the schema id `xr-foundry.token_overrides.v1`, the registry fingerprint,
  and the overrides in canonical id order with each value in its kind's canonical
  form; a state with no overrides yields an empty override list; the same state
  always yields byte-equal output; and applying the document's overrides to a
  fresh state over the same registry yields an equal fingerprint (round trip);
- structured results with stable failure codes for a malformed identity, an
  invalid kind declaration, a duplicate tunable, a default out of range, an unknown
  tunable, a kind mismatch, an out-of-range value, an unknown snapshot, an
  unsupported kind, a binding kind mismatch, a duplicate binding, an unsupported
  token, and a missing token range, each carrying the offending id or token path,
  plus a valid intent sequence passing clean with every outcome accepted;
- a binding index for "point at it, tune it": a `BindingIndex` built from a
  validated binding set answers, for one opaque target path, exactly the binding
  records whose target path equals it or whose target path has it as a
  segment-wise prefix, in registration order, and an empty list for a path no
  record targets; the index is immutable, is built without reflection or any
  engine type, and is the only lookup the adapter uses to go from a selected
  thing back to its tunables, so selecting a thing changes which slots are shown
  and never which editor a slot holds.

The Core references no engine type, holds no `UnityEngine.Color`, reads no asset,
writes no file, and renders nothing; the token document enters and leaves it as
text, and a target path enters and leaves it as an opaque string.

## Unity adapter gate

EditMode tests must cover:

- explicit target resolution from binding records: the adapter resolves each
  Core `BindingRecord` target path to one `ScriptableObject` reference and one
  member from a closed member set that a hand-written binder for that asset type
  declares (for the Inventory skin seams, the surface, text, and slot-state colour
  members); a target path whose asset reference is missing is rejected with
  `binding.target.missing`, whose member is not in the binder's closed set with
  `binding.member.unknown`, and whose tunable kind differs from the member's kind
  with `binding.kind.mismatch`; each rejection carries the stable code, the field
  path, and the source asset, and construction throws with the same report; no
  binder resolves a member by reflection over fields or properties; the scaffold
  (panel host and editors) never references a binder, a skin type, or a package
  type, and adding a new target (a colour in any package, skin, or scene) adds a
  binding record and, for a new asset type, a binder, never a control, panel, or
  editor class, tested by a source rule that no editor or host assembly references
  a binder or skin assembly;
- an explicit diagnostic for every by-name or optional resolution: a token JSON
  file path, a device override file path, an asset path, a binding target path, an
  input module or event camera of the interim UGUI fallback, or an Editor asset
  lookup that the adapter resolves by name, by serialized field, or by an optional
  lookup must be pinned to the validated editor, UGUI, and toolkit versions, must
  report an explicit failure with a stable code when the target is missing or
  incompatible, and must carry a negative test for the missing target (LESSON-004);
  silence or a reported success is a defect;
- live application through the existing skin seam: a runtime constructed with
  explicit references (registry, initial state, validated bindings, binders,
  export sink, panel host) applies every accepted `set`, `reset`, `reset_all`, and
  `apply_snapshot` to the bound skin instance and then re-invokes the renderer
  adapter's own skin entry point (`ApplySkin` on the Inventory UGUI shell view or
  UI Toolkit document view), never a view field directly; a rejected intent
  reaches no binder; the values written equal the Core's effective values; the
  skin asset on disk is unchanged by application alone; and the bound asset and the
  view are constructed with explicit references, with no scene singleton, static
  instance, scene search, or reflection discovery;
- an injectable `ITuningExportSink` with two implementations: the device sink
  writes the Core's token-override document under the persistent data path at a
  path the runtime passes explicitly and returns the resolved path, platform, and
  byte count in a structured result, and an unwritable path is reported with
  `export.write.failed`; the Editor sink (Editor assembly only) writes overrides
  into the token JSON document or into the bound `ScriptableObject` asset, marks the
  asset dirty, saves it, and returns the asset or file path and the ids written;
  each sink writes only the ids the state overrides; a sink is never chosen by
  platform detection inside the family, only by the explicit reference the
  consumer passes; and `export` on a state with no overrides writes an empty
  override list rather than skipping;
- import of a device override file only when its path is passed explicitly: every
  override that names an unknown id, a mismatched kind, or an out-of-range value is
  reported with the Core's code and skipped, the remaining overrides are applied,
  and nothing is imported without an explicit path;
- one editor per kind: the adapter ships exactly one editor implementation per
  `EditorKind` (a slider with the registered range and step for `float_editor`
  and `integer_editor`, a toggle for `bool_editor`, a picker over the registered
  value set for `enumerated_editor`, a hue-saturation-value control with an alpha
  channel for `colour_editor`, per-axis sliders for `vector2_editor` and
  `vector3_editor`); a corner radius, a spacing value, a type-scale value, and a
  hit-target size are all `float` or `integer` tunables edited by the same float
  or integer editor, and a colour anywhere in any package, skin, or scene is edited
  by the same colour editor; registering N `colour` tunables attaches the one
  colour editor implementation N times, tested by asserting one editor type and N
  attachments; an `EditorKind` the adapter does not implement fails closed at
  attach time with `tunable.kind.unsupported` and a diagnostic naming the tunable,
  and never yields an empty or placeholder control;
- a panel host with one slot per bound tunable: the host is declared as one XR UI
  shell panel, creates exactly one slot per validated binding record, and resolves
  the slot's editor by kind at attach time through `ResolveEditorKind`; the host
  contains no per-family, per-package, per-skin, or per-screen code path, tested by
  attaching bindings from two different skin types to one host and asserting that
  the host's attach path is identical for both; every editor change raises exactly
  one Core `set` intent through the host and writes to no target directly; a
  rejected intent restores the editor to the effective value; the host labels each
  slot by the registered label and groups slots by the registered group;
- scope filtering as metadata only: a "changed only" view lists exactly the
  tunables with an override, and a scope filter (by package, skin, or screen label
  from the binding record) lists exactly the records carrying that label; changing
  the active filter changes which slots are shown and never which editor a slot
  holds, tested by asserting the same editor instance kind for a `colour` slot
  under every filter; a reset control per slot raises `reset` and a host-level
  reset raises `reset_all`;
- the host's own look comes from the shell's skin seam and never from the
  tunables it edits; until the shell's UGUI adapter is live, a minimal UGUI
  world-space fallback host constructed with explicit references satisfies the
  same slot, editor-per-kind, intent, filter, and reset clauses and is replaced
  without a Core, editor, or binding change;
- two independent runtimes constructed side by side over two skin instances and
  two sinks that share no state, so a `set` in one changes no value, override, or
  export of the other.
- the panel host is a peer client of the XR UI shell, never a shell dependency
  (LESSON-010): it registers its own verb (`tune`) into the shell's verb registry
  exactly the entry point any other family would use, and it adapts the shell's
  one generic panel-content surface seam to its own `ITuningPanelSurface`; the
  shell's own assemblies never reference this family, proven by a source rule in
  the shell's Core test assembly.
- pick-to-tune through the shell's routing, never the family's own raycast: the
  host accepts a selection from the XR UI shell's typed routing (a `SurfaceId` or
  an explicit target path the consumer passes), maps it through the Core
  `BindingIndex` to the bindings targeting that thing, and shows exactly those
  slots as a scope named `selection`; a selection no record targets shows zero
  slots and reports `selection.unbound` with the path, never an empty panel with no
  diagnostic; clearing the selection restores the previous scope; the family
  performs no raycast, reads no pose, and holds no reference to a camera, ray, or
  input device, tested by a source rule that the adapter assembly references no
  physics, input, or camera type; and a selection changes which slots are shown
  and never which editor a slot holds, tested by asserting the same editor
  instance kind for a `colour` slot before, during, and after a selection;
- two view presets over one host, never two panels: a `designer` preset shows
  each slot's label, editor, and per-slot reset, and an `engineer` preset shows the
  same slots plus, per slot, the registered range and step, the binding's target
  path, and the export destination the sink reports, and, per host, every
  `TuningSlotDiagnostic` and binding rejection with its stable code; switching the
  preset changes what a slot displays and never which editor it holds, raises no
  intent, and adds no slot, tested by asserting the same slot list and editor kinds
  under both presets; neither preset can change a registered range, step, or
  value set, which stay declared in code and reach the tree only through a
  reviewed change.

No test claims that a control was visible or reachable, that a colour looked
right, that a slider was usable with a controller, or that any input was read
from a device.

## Renderer rule

The Inventory UGUI and UI Toolkit skin seams are sibling targets. A binder,
binding test, or Device Lab receipt for one renderer adapter's skin seam proves
nothing about the other, even on the same device and commit, and a composition
that tunes one renderer's skin is a different tuple from one that tunes the other.

## Claim ceiling

An EditMode run proves identity validation, kind validation, registry
construction, intent application, override-only state, state immutability,
deterministic replay, editor-kind resolution, binding validation, token-bridge
round trip, live application through the skin seam, export and import through the
sinks, and host-to-intent translation for the tuple recorded in the compatibility
profile. It proves the plumbing only: that an accepted override reached the bound
skin seam and that an export wrote the override document it reports. It does not
prove that a value looks right, that a colour, size, corner radius, spacing,
opacity, or type scale is legible, has sufficient contrast, is comfortable, or is
reachable in a headset, that the host responds with any latency, or any
named-device behaviour. Whether a tuned value looks right is a human judgement
recorded in a Device Lab receipt that binds the observation to a full commit SHA,
the exact dependency tuple, the renderer composition, the override document
digest, the posture, the measured duration, the named input sources, and the
tester identity; `not_tested` is never evidence.
