# Audio events verification contract

## Source gate

- Every derivation input is positive, public, and role-bounded in
  [`source-manifest.json`](source-manifest.json).
- No consumer project, private sound bank, mixer asset, or prior product audio
  implementation is a derivation input.
- A person confirms the source URLs and versions before the admission record is
  signed, because the authoring environment could not fetch them.

## Core gate

Deterministic tests must cover:

- event identity validation: canonical form, rejection of empty, whitespace, and
  malformed identifiers with a stable `identity.malformed` failure, and value
  equality between two identities with the same text;
- bus and snapshot identity validation with the same rules, and a mix graph that
  requires a root bus, rejects duplicate bus or snapshot ids, and rejects a
  snapshot that references a bus outside the graph;
- parameter contracts: a closed parameter kind (float, bool, enumerated), a declared
  range or value set per parameter, and fail-closed rejection of an unknown
  parameter (`parameter.unknown`), a wrong kind (`parameter.kind.mismatch`), and
  an out-of-range or undeclared value (`parameter.out_of_range`) without changing
  state;
- spatial attachment intents that reference an anchor id rather than an engine
  transform: attach, move between anchors, and detach as distinct intents, an
  unknown anchor rejected with `anchor.unknown`, and an event that is either
  attached to exactly one anchor or unattached;
- deterministic state snapshots: the same intent sequence produces an equal
  snapshot, a snapshot is immutable, a rejected intent leaves the prior snapshot
  intact, and a snapshot enumerates active events, the current mix snapshot, bus
  parameter values, and attachments; and
- structured results with stable failure codes for an unknown event, bus, snapshot,
  parameter, or anchor, plus a valid intent sequence passing clean.

## Unity adapter gate

EditMode tests must cover:

- binding validation of bus and parameter ids to an `AudioMixer` and its exposed
  parameters with stable codes, field paths, and the source asset for a missing
  mixer reference (`mixer.missing`), a parameter that is not exposed
  (`mixer.parameter.unexposed`), a duplicate binding, and a kind that the mixer
  cannot represent (`mixer.parameter.kind.mismatch`), and construction that throws
  with the same report;
- snapshot transition intents that map a `SnapshotId` to an `AudioMixerSnapshot`
  with an explicit transition duration, and a missing snapshot binding reported as
  `mixer.snapshot.missing` rather than ignored;
- an explicit diagnostic when a mixer, exposed parameter, or snapshot is missing at
  runtime, because the engine resolves exposed parameters and snapshots by name and
  reports an unknown name only through a return value (LESSON-004); silence or a
  reported success is a defect;
- a runtime constructed with explicit references that applies parameter values in
  the order they were accepted and reports direct and rejected state; and
- no scene singleton, static instance, scene search, or reflection discovery in the
  adapter, verified by a test that constructs two independent runtimes side by side.

No test claims audible output, spatializer behavior, or transition timing.

## Claim ceiling

An EditMode run proves identity validation, mix-graph construction, parameter
rejection, attachment state, snapshot determinism, and mixer binding validation for
the tuple recorded in the compatibility profile. It does not prove audible output,
spatialization, latency, loudness, mixer transition timing, spatializer plug-in
behavior, middleware runtime behavior, or headset comfort.
