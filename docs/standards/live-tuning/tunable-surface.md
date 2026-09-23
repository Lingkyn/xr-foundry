# Tunable surface protocol

## Why this exists

A developer scaffold (an in-headset or Editor tuning panel that lets a
developer point at something and change it) is fixed code: one host, one
editor per kind, one set of intents. What varies from package to package is
never the scaffold, it is the targets — which values on which asset a given
package is willing to expose. Today that variation would have to live in
per-family, per-package code inside the scaffold itself: a `switch` on package
identity, or a hand-maintained list of "the values Inventory happens to
expose" baked into a host that was supposed to be package-agnostic. That is
exactly the coupling `docs/standards/live-tuning/verification-contract.md`
rules out ("the scaffold ... never references a binder, a skin type, or a
package type").

The tunable surface protocol is how a package tells the scaffold what may be
tuned without the scaffold ever knowing the package exists. A package that
owns an explicit injectable seam — a skin/theme asset or any other plain
design-time configuration asset with an entry point a host re-invokes —
publishes a small JSON file next to its component manifest. The live-tuning
Unity adapter's binding index, binders, and panel host read that file (and the
`BindingRecord`s an integrator derives from it) the same way for every
package. Adding a new tunable target, in any package, is a data change to
that package's manifest, never a change to the scaffold, the binder base
classes, or the panel host.

This file is data about what *may* be tuned. It is not itself a tuning
session, an executed binding, or a visual result; see "What this file does not
claim" below.

## What can be tuned: any explicit injectable seam, not only a skin

The protocol is not skin-specific. Anything a package exposes through an
explicit injectable configuration asset with a live-apply entry point may
declare tunables: a skin or theme asset, a comfort or locomotion profile, an
audio mix profile, an interaction profile, a layout config (grid columns, slot
size, spacing), a scene-flow timing config. A visual value (a colour, a corner
radius) and a behaviour value (a snap-turn angle, a fade duration) are the
same kind of thing to the scaffold: both are a `(kind, default, guard rail,
target)` tuple bound through a seam. The scaffold's editor-per-kind resolution
(`ResolveEditorKind`) depends only on the tunable's `kind`, never on whether
the seam it targets happens to be called a skin.

The one hard requirement, for either seam kind, is an **entry point**: a
method the runtime host calls to re-apply the seam asset after a tunable
changes (`ApplySkin` for a skin seam; the equivalent apply method for a config
seam). An asset that is only read once at start-up, or only edited through a
one-shot Editor tool with no live-apply path a host can re-invoke, is not a
seam under this protocol yet, however many plain serialized fields it has;
see the Foundations, Interaction, and Persistence dispositions for LESSON-009
for named examples.

### Difference from the Settings family

Settings and the tunable surface can look similar — both end in a typed value
sitting on some target — but they answer different questions and never share
storage:

- **Settings** (`com.lingkyn.settings.*`) owns the **user's runtime
  preference**: a value a player changes at runtime, that persists across
  sessions on their device, validated and rolled back through the Settings
  domain's own transaction model.
- **The tunable surface** owns the **developer's design-time default**: a
  value a developer changes while iterating, that exports back into the asset
  the value was declared on (a skin ScriptableObject, a config
  ScriptableObject, or a token-override document), so the next build already
  carries it as the new default. It is a design-time authoring loop, not a
  per-user preference.

The two may present through the same live-tuning panel host and the same
editor-per-kind controls — a slider is a slider — but a tunable surface
manifest never feeds the Settings persistence path, and a Settings profile is
never listed as a tunable's `target`. Settings has no tunable surface of its
own today (see the LESSON-009 disposition below); if a Settings UI adapter
ships in the future, its own settings-screen skin seam could gain a
`foundry.tunables.json` the same way Inventory's did, without changing this
rule.

## File placement

A package that owns at least one qualifying seam ships a file named
`foundry.tunables.json` **beside its `foundry.component.json`**, at the
package root (for example
`packages/unity/systems/inventory/com.lingkyn.inventory.ugui/foundry.tunables.json`).
A package with no qualifying seam ships no such file. The scaffold consumes
only these files (and the component/capability graph that says which packages
provide `xr-foundry.tuning.surface`) — never per-family code, a reflection
scan, or a hand-maintained list.

## Schema

`docs/standards/live-tuning/tunable-surface.schema.json`, schema id
`xr-foundry.tunable_surface.v1`. Top level:

| Field | Meaning |
| --- | --- |
| `schema` | Always `xr-foundry.tunable_surface.v1`. |
| `package_id` | Must equal the `id` in the sibling `foundry.component.json`. |
| `family` | Must equal that manifest's `family`. |
| `seams` | One or more seam declarations (below). A package may declare more than one seam, for example a `skin` seam and a `config` seam side by side. |
| `export` | Where an accepted override is written back to (below). |
| `tunables` | One or more tunable declarations (below). |

### `seams[]`

| Field | Meaning |
| --- | --- |
| `id` | A short lower-case identifier for this seam, unique within the manifest (for example `skin`). A tunable's `target.seam` names this id. |
| `kind` | `skin` (an injectable visual/theme asset applied through a renderer adapter's own skin entry point) or `config` (any other explicit injectable design-time configuration asset applied through its own entry point). |
| `type` | The seam asset's fully-qualified C# type, for example `Lingkyn.Inventory.UGUI.InventorySkin`. |
| `entry_point` | The method a runtime host calls to re-apply the seam after a tunable changes, for example `ApplySkin`. This is the Core Unity gate's "re-invokes the renderer adapter's own skin entry point ... never a view field directly," generalised to any seam kind. |
| `member_set` | The closed list of member names on `type` that a tunable's `target.member` may name. This is the "closed member set a hand-written binder ... declares" from the verification contract; it is a reviewed change to widen, never something the scaffold infers by reflection. |

### `export`

| Field | Meaning |
| --- | --- |
| `destination_kind` | `token_document` (overrides export as an `xr-foundry.token_overrides.v1` document, the Core token-bridge round-trip format) or `asset` (overrides export back into the bound seam asset itself, the Editor sink's `ScriptableObject` path). |
| `path` | The conventional export location in a *consumer* Unity project (a persistent-data-path-relative file, or the seam asset's own project path). This is not a path inside this repository and is never checked for existence here; the family's `ITuningExportSink` implementations are what actually write it. |

### `tunables[]`

| Field | Meaning |
| --- | --- |
| `id` | The canonical `TunableId` form from the Core contract: lower-case segments joined by `.` (for example `inventory.ugui.skin.surface_panel`). Unique across every `foundry.tunables.json` in the repository, not only within one manifest — the scaffold's registry is one flat namespace. |
| `kind` | One of the closed set `float`, `int`, `bool`, `enum`, `colour`, `vector2`, `vector3` — the same closed kind set the Core `TunableKind` and `EditorKind` enumerations use, so a new kind is a reviewed change to the family standard, never a manifest-only addition. |
| `default` | The tunable's default value in its kind's shape: a number for `float`/`int`, `true`/`false` for `bool`, one of `values` for `enum`, a 4-element `[r, g, b, a]` array with every channel in `[0, 1]` for `colour`, a 2- or 3-element number array for `vector2`/`vector3`. |
| `range` | `{min, max, step}`, required for `float` and `int`. `step` must be positive. |
| `values` | A non-empty list of allowed strings, required for `enum`. |
| `target` | `{seam, member}`: `seam` names one of this manifest's `seams[].id`; `member` must be one of that seam's `member_set`. |
| `label` | The human-readable label the scaffold shows on this tunable's slot. |
| `group` | The label the scaffold groups slots by. |
| `scope` | `{package, skin, screen?}` — open scope metadata for filtering, reusing the Core `BindingRecord` scope vocabulary verbatim (`package`, `skin`, or `screen` labels) even when the seam it describes is a `config` seam rather than a `skin` seam; `skin` here names which seam instance/group the tunable belongs to. `screen` is optional. |
| `token` | Optional. A dotted path under `design_tokens` in `docs/standards/design-language/ui-design-language-standard.json` (for example `surface.panel`, `slot_states.hover`) that this tunable's default is drawn from. Only meaningful when the value in fact is a design-language token; a behaviour tunable (a duration, an angle) never carries one. |

## Guard rails are a reviewed change, not a scaffold decision

A tunable's `range`, `step`, or `values` is a declared guard rail, exactly as
the Core contract's "two view presets over one host" clause states: "neither
preset can change a registered range, step, or value set, which stay declared
in code and reach the tree only through a reviewed change." The manifest is
where that declaration lives for the scaffold's purposes; widening a range,
loosening a step, or adding a value to an enum's set is a pull request against
this file (and, where the value binds to a `member_set` entry, against the
seam's binder), never something a running scaffold, a preset, or a developer's
in-headset session can do on its own.

## The `xr-foundry.tuning.surface` capability

A package that ships `foundry.tunables.json` also lists
`{"id": "xr-foundry.tuning.surface", "version": "1.0.0"}` in its
`foundry.component.json` `provides`, and vice versa: a package that provides
the capability without shipping the file, or ships the file without providing
the capability, is a repository-contract error
(`validate_tunable_surfaces`). The capability is registered in
`capability-registry.json` the same way every other capability is; it carries
no multiplicity or slot meaning beyond "this package publishes tunable-surface
data," so more than one package may provide it at once.

## What this file does not claim

Exactly the claim ceiling in
`docs/standards/live-tuning/verification-contract.md` applies to this file, a
level further back:

- It does not claim that any binder, host, or panel has executed. Declaring a
  tunable is a data statement about what a scaffold *may* bind to, not a
  record that a binder was constructed or a `set` intent applied.
- It does not claim that a value looks right, is legible, has sufficient
  contrast, is comfortable, or is reachable in a headset. That is a Device Lab
  receipt, never a manifest.
- It does not claim any device, controller, or runtime behaviour. `kind`,
  `default`, `range`, and `token` are declared guard rails and starting
  values, not measurements.
- A `token` reference proves only that the dotted path resolves inside
  `ui-design-language-standard.json`; it does not prove the tunable's current
  default is still byte-equal to that token (the two are written by hand in
  parallel today) or that the token itself is device-verified.

## Related files

- `docs/standards/live-tuning/verification-contract.md` — the Core and Unity
  gates this protocol's manifest is the data form of.
- `docs/standards/design-language/ui-design-language-standard.json` — the
  token document a `token` reference resolves against.
- `scripts/validate_repository.py::validate_tunable_surfaces` — the rule that
  checks every `foundry.tunables.json` in the tree against this schema and
  these cross-references.
