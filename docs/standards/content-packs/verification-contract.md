# Content packs verification contract

## Source gate

- Every derivation input is positive, public, and role-bounded in
  [`source-manifest.json`](source-manifest.json).
- No consumer project, private content pack, product level file, or prior
  product content-loading or mod-loading code is a derivation input. The Scene
  flow family's scene-set and transition model and the Persistence family's
  save/load and migration model in this repository are context only: a pack
  delivers scene entries and document entries to those families' own load and
  save paths, and neither family's model is re-derived here or contributes
  evidence for pack, catalog, or trust-policy semantics.
- A person confirms the source URLs, page paths, section anchors, versions,
  the license, and the maintenance state of each open-source implementation
  before the admission record is signed, because the authoring environment
  could not fetch them.

## Core gate

Deterministic tests must cover:

- pack and version identity: `PackId` with a canonical form (lower-case
  segments joined by `.`), rejection of empty, whitespace, and malformed
  identifiers with a stable `identity.malformed` failure, and value equality
  between two identities with the same text; `PackVersion` parses a closed
  `major.minor.patch` form, rejects a malformed or negative component with the
  same `identity.malformed` code, orders by precedence (major, then minor,
  then patch), and a version range (a minimum and an optional maximum, no
  wildcard or floating bound) either accepts or rejects a candidate version;
- a closed, non-executable entry-kind set: `scene`, `prefab`, `audio`,
  `text_table`, and `data`; a `PackEntry` declares an opaque entry path within
  the pack, one kind from this set, and a content digest for that entry; a
  kind outside this set is rejected with `kind.unsupported` naming the
  offending entry path, and no kind in the closed set names a script, an
  assembly, or a reflection-discovered type;
- a `PackManifest` declaration built by explicit construction: id, version, a
  declared dependency list of (`PackId`, version range) pairs, a whole-pack
  content digest, and the closed entry list; a manifest with a duplicate entry
  path, an empty entry list, or a dependency naming its own id is rejected
  with `manifest.invalid`;
- an immutable `PackCatalog` built by explicit registration of a validated
  manifest through `register_pack`: registering the same id and version twice
  is rejected with `pack.duplicate`, a rejected registration leaves the
  catalog unchanged, and a built catalog enumerates its packs in canonical
  id-then-version order and cannot be mutated afterwards;
- catalog and dependency resolution as an explicit graph walk, never an
  implicit one: `resolve` (pack id, and a version or a version range) returns
  the exact matching manifest; naming an id the catalog has never registered
  is rejected with `pack.unknown`, naming a registered id with no registered
  version satisfying the requested version or range is rejected with
  `version.unsatisfied`, a manifest whose declared dependency names an
  unregistered pack id is rejected with `dependency.missing` naming the
  missing id, and a declared dependency whose version range no registered
  version satisfies is rejected with `version.unsatisfied` naming the
  dependency; resolution never partially returns a manifest with an
  unresolved dependency;
- a closed trust-policy source set with a fail-closed default: `bundled`,
  `first_party_remote`, and `user`; a `TrustPolicy` declares which of these
  sources a `load` may accept; `load` naming a pack whose registered source is
  not in the policy's allowed set is rejected with `source.untrusted` before
  any digest check or entry read, and a policy with an empty allowed-source
  set accepts nothing rather than defaulting to allow-all;
- content digest verification before any load: `verify` recomputes each
  declared entry's digest and the manifest's whole-pack digest from the pack's
  actual content and compares them to the declared values; any mismatch is
  rejected with `digest.mismatch` naming the offending entry path, or the
  manifest itself for a whole-pack mismatch, and `load` never returns an entry
  from a pack that failed `verify`;
- typed intents applied to an immutable `PackState`: `register_pack`
  (manifest), `resolve` (pack id, version or range), `load` (pack id,
  version), `unload` (pack id, version), and `verify` (pack id, version);
  `load` or `unload` naming a pack the catalog has not registered is rejected
  with `pack.unknown`, and `unload` of a pack not currently loaded is a no-op
  returning success;
- one loaded-or-not record per pack version as the only content: a state
  holds, per (pack id, version), whether it is loaded and, if so, its
  resolved dependency set and its verified entry list; `load` accepted for a
  pack already loaded is a no-op returning the existing loaded record, and
  `unload` clears exactly the named pack version's loaded record and leaves
  every other pack's record untouched;
- state immutability: a state is immutable, an accepted intent produces a new
  state and leaves the prior state intact, and a rejected intent leaves the
  prior state intact and returns it unchanged with the failure code;
- deterministic replay: the same intent sequence over the same catalog and
  initial state produces an equal final state and an equal fingerprint; the
  fingerprint covers the catalog fingerprint, the trust policy, and the
  per-pack-version loaded-record entries in canonical pack-id-then-version
  order; two states reached by different intent sequences with the same net
  loaded packs have equal fingerprints;
- structured results with stable failure codes for a malformed identity, an
  invalid manifest declaration, a duplicate pack registration, an unknown
  pack, an unsatisfied version, a missing dependency, an unsupported entry
  kind, an untrusted source, a digest mismatch, and a stale expected
  revision, each carrying the offending id, version, entry path, or source,
  plus a valid intent sequence passing clean with every outcome accepted;
- one intent channel for people and agents (LESSON-011): every `PackIntent`
  (`register_pack`, `resolve`, `load`, `unload`, `verify`) carries an
  `IntentActor` from the closed set `player`, `agent`, `replay`, `import`, and
  an optional expected revision; `PackState` carries a monotonically
  increasing revision; a stale expected revision is rejected with
  `state.stale` before the intent's own rule runs and changes nothing; the
  actor never changes validation, so a player-issued and an agent-issued copy
  of the same intent take the same path and yield the same outcome and the
  same resulting state; and every outcome in the replay log carries the
  issuing actor and the revision after it.

The Core references no engine type, holds no `UnityEngine` type, performs no
file or network I/O, computes digests only through a pure function it defines
itself, and declares no entry kind that names a script, an assembly, or a
reflection-discovered type; an entry path enters and leaves the Core as an
opaque, validated string, and dependency, version, and digest values are
plain data.

## Unity adapter gate

EditMode tests must cover:

- a thin adapter over Addressables with explicit catalog references, never a
  by-name or dynamic key lookup: the adapter resolves each Core `PackEntry` to
  one Addressables key or asset reference through an explicit, injected
  binding rather than a scene search, a `Resources.Load` call, or a string
  built at runtime, pinned to the validated Unity and Addressables versions; a
  binding the adapter cannot resolve reports `binding.unresolved` naming the
  entry path, and construction throws with the same report, carrying a
  negative test for the missing binding (LESSON-004);
- an injectable `IPackLoader` with an in-memory test double so tests assert
  exactly what was requested and what the fake reported as loaded for every
  accepted intent — the resolved pack id, version, entry list, and computed
  digests — without a device, a network, or a real Addressables catalog
  dependency; the runtime never calls Addressables directly, only through the
  loader, and a rejected intent reaches no loader;
- digest verification before any load reaches the loader: the adapter's
  `verify` path computes each entry's digest through an injected hash
  provider and compares it to the manifest's declared value before the same
  entry is handed to the loader for `load`; a mismatch reports the Core's
  `digest.mismatch` and the loader is never invoked for that entry;
- an explicit handle and memory budget with release on unload: the adapter
  tracks every `AsyncOperationHandle` (or equivalent) it opened for a loaded
  pack, exposes the handle count and an approximate byte estimate as plain
  data the caller reads, and releases every tracked handle for a pack when
  `unload` is accepted for it; a pack unloaded twice releases nothing the
  second time and reports success as a no-op, and a handle still tracked
  after release is a defect the adapter's own test double surfaces, never a
  silent condition;
- an explicit diagnostic for every by-name or optional resolution: an
  Addressables key, a label, a remote catalog location, or a content-state
  hash the adapter resolves by name, by serialized field, or by an optional
  lookup must report an explicit failure with a stable code when the target
  is missing or unresolved, carrying a negative test for the missing target;
  silence or a reported success is a defect (LESSON-004);
- construction with explicit references only and no polling: a runtime is
  built from an explicit catalog, an explicit trust policy, an explicit
  entry-binding table, and an explicit loader; no scene singleton, static
  instance, scene search, or reflection discovery resolves any of them; the
  runtime is driven only by explicit `register_pack`, `resolve`, `load`,
  `unload`, and `verify` calls it receives and holds no per-frame update loop
  that re-loads or re-verifies a pack on its own; two independently
  constructed runtimes over two loaders that share no state are tested side
  by side, so a `load` in one changes no pack, handle, or loader call of the
  other;
- explicit route selection as configuration, never platform or environment
  detection inside the family: a bundled-local route, a first-party-remote
  catalog route, and a user-content route are separate `IPackLoader`
  implementations behind the one seam, and which one (or which ordered set) a
  runtime uses for a given trust-policy source is the consumer's explicit
  wiring at construction time, never a runtime check of the active platform,
  build target, or network reachability inside the adapter;
- no scene, prefab, audio, or text-table entry is instantiated, played, or
  parsed by this family's own code beyond resolving its Addressables
  reference and confirming its digest: a `scene` entry's key is handed to the
  Scene flow family's own loader, and a `data` or `text_table` entry's bytes
  are handed to the caller or the Persistence family's own deserializer, never
  opened, executed, or interpreted inside this adapter.

No test claims that a download completed, that a file exists at a path on a
device, how long a load took, or anything about frame timing during a load.

## Claim ceiling

An EditMode run proves identity and version validation, manifest and
entry-kind declaration validation, catalog construction and resolution,
dependency-graph resolution, trust-policy source gating, digest verification,
intent application, one-loaded-record-per-pack-version state, state
immutability, deterministic replay, Addressables binding resolution, handle
and memory-budget tracking, and release on unload, for the tuple recorded in
the compatibility profile. It proves the plumbing only: that an accepted
register_pack, resolve, load, unload, or verify intent reached the injected
loader and digest verifier with the exact pack id, version, dependency set,
entry list, and computed digests the Core resolved. Download size, download
time, memory footprint on a device, hitching during a load, and store
certification outcome are receipts from a device build or a real store
submission, never package claims; no test in this family's gates claims a
measured download size, a measured load time, or a passed certification.
Whether a pack's declared size or load time is acceptable on a named device,
and any named-device or named-runtime hosting or caching behavior, are human
judgements or real store-submission outcomes recorded in a Device Lab receipt
that binds the observation to a full commit SHA, the exact dependency-lock
digest and versions, the named device and runtime, the pack-catalog
fingerprint, the posture, the measured duration, the named build target and
graphics API, and the tester identity; `not_tested` is never evidence.
