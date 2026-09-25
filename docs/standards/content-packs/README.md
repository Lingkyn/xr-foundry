# Content packs package-family standard

Status: incubating reference, source gate only

This standard defines data-driven content delivery as a validated `PackId`
and semantic `PackVersion`, a closed non-executable entry-kind set (scene,
prefab, audio, text table, data), a `PackManifest` declaring dependencies and
a content digest, an immutable `PackCatalog` built by explicit registration
that resolves a pack id and version to a manifest, a closed trust policy over
where a pack came from (bundled, first-party remote, user) that fails closed
on an untrusted source, and a small set of intents applied to an immutable
state, with a thin Unity adapter over Addressables planned separately. It
does not define any product's level content, downloadable-content catalog, or
judgement that a pack downloads within a stated size or time on any device.
It is derived only from the positive public sources in
[`source-manifest.json`](source-manifest.json), and no package id, staged
implementation, or catalog entry exists yet.

[`docs/benchmarks/shipped-game-gap-matrix.md`](../../benchmarks/shipped-game-gap-matrix.md)
ranks content packs fifteenth and records why every one of the three
benchmark product shapes (rhythm action, object interaction, spatial
creation) needs some form of it: custom levels, downloadable packs, or user
scenes are all a data-driven content-loading problem, and a title that hand
writes a separate loader for bundled, remotely hosted, and user-supplied
content invents the same trust and dependency problem three times.

## Capability boundary

The family separates:

- immutable pack and version identity (`PackId`, `PackVersion`) from any file,
  asset, or hosting mechanism that delivers them;
- a closed, non-executable entry-kind set (`scene`, `prefab`, `audio`,
  `text_table`, `data`) from any script, assembly, or reflection-discovered
  type a downloaded pack could otherwise carry;
- a `PackManifest` declaring dependencies on other packs by id and version
  range, and a content digest, from any engine asset, bundle, or catalog
  format;
- an immutable `PackCatalog` built by explicit registration that resolves a
  pack id and version to a manifest and fails closed on a missing or
  unsatisfied dependency, from any reflection, attribute scan, or asset
  discovery;
- a closed trust-policy source set (`bundled`, `first_party_remote`, `user`)
  that fails closed on an untrusted source, from any implicit allow-by-default
  behavior;
- content digest verification performed before any load, from any silent
  trust in a manifest's own declared content;
- register-pack, resolve, load, unload, and verify intents applied to an
  immutable `PackState` whose only content is, per pack version, whether it
  is loaded and its resolved dependency and entry set, from any control,
  route, or device that raises them; and
- structured results with stable failure codes (`identity.malformed`,
  `manifest.invalid`, `pack.duplicate`, `pack.unknown`,
  `version.unsatisfied`, `dependency.missing`, `kind.unsupported`,
  `source.untrusted`, `digest.mismatch`, `state.stale`) from any editor
  tooling, platform review, or store certification process.

The family is a content-loading and trust substrate, not a package manager, a
mod-loading framework, an asset importer, or a content-authoring tool. It
does not author, import, or edit pack content, does not own the Scene flow
family's transition model or the Persistence family's save/load model, and
makes no claim that a pack downloads within a stated size or time, or that a
build is store-certifiable, on any device.

## Why it composes

- **Scene flow.** The Scene flow family
  ([`docs/standards/scene-flow/verification-contract.md`](../scene-flow/verification-contract.md))
  owns scene set identity, the transition state machine, and scene loading
  itself. A content pack's `scene` entry names a scene this family's catalog
  and trust policy resolve and verify, but the loaded scene id is handed to
  Scene flow's own binding and loader; this family reimplements no transition,
  fade, or recovery logic, and Scene flow's model contributes no evidence for
  pack, catalog, or trust-policy semantics here.
- **Persistence.** The Persistence family
  ([`docs/standards/persistence/verification-contract.md`](../persistence/verification-contract.md))
  owns save orchestration, schema versioning, and migration. A content pack's
  `text_table` or `data` entry delivers bytes a consumer may deserialize
  through Persistence's own model; this family owns no save format and reads
  no save file, and Persistence's model contributes no evidence for pack,
  catalog, or trust-policy semantics here.
- **Localization.** A pack's `text_table` entry is a plain data payload this
  family verifies and delivers; any localization-specific fallback,
  formatting, or locale-selection behavior over that payload belongs to a
  future Localization family, never to this one.
- **XR UI shell / Live tuning.** Neither is a Core or mandatory Unity
  dependency here: the family renders no panel of its own and exposes no
  tunable surface under LESSON-009.

## Planned package boundary

| Layer | Owns | Must not own |
| --- | --- | --- |
| Engine-light Core `com.lingkyn.content-packs.core` (package id reserved only after admission) | pack and version identity; the closed non-executable entry-kind set; manifest declaration and validation (dependencies, content digest, entry list); the immutable catalog and explicit registration; catalog and dependency-graph resolution; the closed trust-policy source set and fail-closed gating; digest verification; register-pack, resolve, load, unload, and verify intents on an immutable per-pack-version state with deterministic replay and fingerprint; structured results | Unity types, `UnityEngine` types, Addressables types, file or network I/O, product pack content, Scene flow's transition model, Persistence's save format |
| Unity adapter `com.lingkyn.content-packs.unity` (package id reserved only after admission) | explicit Addressables key/reference bindings per entry; an injectable `IPackLoader` with route implementations for bundled, first-party-remote, and user-content sources; digest verification ahead of the loader; explicit handle and memory-budget tracking with release on unload; explicit diagnostics for every by-name or optional resolution | identity rules, manifest and catalog semantics, trust-policy semantics, intent semantics, product pack content, scene singletons, static instances, scene search, reflection discovery, platform detection, any Scene flow or Persistence type |

## Evidence boundary

An EditMode run can prove identity and version validation, manifest and
entry-kind declaration validation, catalog construction and resolution,
dependency-graph resolution, trust-policy source gating, digest verification,
intent application, one-loaded-record-per-pack-version state, immutability,
deterministic replay, Addressables binding resolution, and handle and
memory-budget tracking with release on unload, for the declared tuple. It
proves the plumbing only. It cannot prove that a pack downloads within a
stated size or time, that a load ran without hitching, that memory behaved
acceptably on a device, or that a build passes store certification; those are
human judgements or real store-submission outcomes recorded in a Device Lab
receipt, as [`verification-contract.md`](verification-contract.md) states in
its claim ceiling.

## Lessons register dispositions (to be added to the register when the family goes live)

| Lesson | Disposition | Rationale |
| --- | --- | --- |
| LESSON-001 closed classification vs open tags | adopted | `PackId`, `PackVersion`, and the entry-kind and trust-policy-source sets are validated closed identities and closed enums; the only open metadata is an entry's opaque path string, which drives no classification |
| LESSON-002 migration path | deferred | No release exists yet; the first release records its compatibility policy, and the first breaking change to `PackId`, `PackVersion`, the entry-kind set, the trust-policy source set, or a stable failure code ships a migration note in the same commit |
| LESSON-003 single-workstation gate | adopted | Tests will run in `run_unity_gates.py` and the consumer workflow like every family; no gate in this standard depends on a maintainer workstation |
| LESSON-004 by-name resolution fails closed | adopted | The Core resolves nothing by name. The Unity adapter gate requires an explicit diagnostic and a negative test for every by-name or optional resolution: an Addressables key, a label, a remote catalog location, and a content-state hash, each pinned to the validated Unity and Addressables versions |
| LESSON-005 clause coverage | deferred | No staged implementation or tests exist; a future staged-implementation item writes `coverage-map.json` mapping every Core and Unity adapter clause to named tests before any clause is cited as evidence, modelled on `docs/standards/haptics/coverage-map.json` |
| LESSON-006 sibling pinning | deferred | Applies at the first Git-consumer validation; the README install matrix will pin the Core and Unity adapter packages, and any Scene flow, Persistence, or hosting-service wiring a composition binds, to one full commit SHA |
| LESSON-007 skin seam | not_applicable | The family renders no panel and owns no visual skin or theme seam; it resolves and verifies packs, it does not draw one |
| LESSON-008 version bump is a verification claim | adopted | No package version exists; the first scaffold enters at 0.1.0 and stays there until its first verified compatibility profile |
| LESSON-009 tunable surface as data | not_applicable | The family is not UI-bearing and owns no injectable skin/theme seam a developer-tuning scaffold would attach to; `PackManifest` and `TrustPolicy` are content-loading configuration applied through the family's own intents, not a design-visual tunable under this protocol's definition |
| LESSON-010 developer tool is a peer client, never a dependency | not_applicable | The family owns pack, catalog, and trust identity and a content-loading adapter, not a shell, dock, or UI panel a developer tool would operate on; it holds no dependency on Live Tuning, the XR UI shell, or any other developer-tooling assembly, so this lesson's dependency-direction question does not arise here yet |
| LESSON-011 one intent channel for people and agents | adopted | The Core gate requires every `PackIntent` (`register_pack`, `resolve`, `load`, `unload`, `verify`) to carry an `IntentActor` and an optional expected revision from its first version, with `state.stale` rejecting a stale write before the intent's own rule runs, so a player-issued and an agent-issued copy of the same intent take the same path from the start rather than being retrofitted later |

## Next steps

1. A person confirms every URL, page path, section anchor, version pin,
   license, and open-source maintenance state in
   [`source-manifest.json`](source-manifest.json).
2. The maintainer copies [`admission.draft.json`](admission.draft.json) into
   `docs/foundry/admissions/` as the durable record through the
   [system admission gate](../../foundry/system-admission.md).
3. A staged Core and Unity adapter, modelled on `staging/haptics/`, is
   authored against [`verification-contract.md`](verification-contract.md)
   with `coverage-map.json` mapping every Core and Unity adapter clause to a
   named test (no partial or unmapped clause). The code stays authored and
   unexecuted until a Unity run produces a compatibility profile, and nothing
   enters `packages/` before admission and a green gate.
4. A person or Agent with a Unity Editor runs
   `python scripts/run_unity_gates.py --host com.lingkyn.content-packs.core`,
   records the resulting compatibility profile, and either confirms the
   coverage map or turns it red.
5. The first Device Lab session for this family records a measured download
   size, load time, and any store-submission outcome as human observations
   and real records bound to the pack-catalog fingerprint, and never as a
   package claim.

See also:

- [`verification-contract.md`](verification-contract.md)
- [`source-manifest.json`](source-manifest.json)
- [`admission.draft.json`](admission.draft.json)
- [`next-batch.json`](../../foundry/queue/next-batch.json), the queue this
  candidate joins, and the
  [system admission gate](../../foundry/system-admission.md)
- [`lessons-register.json`](../lessons/lessons-register.json)
- [`scene-flow/verification-contract.md`](../scene-flow/verification-contract.md),
  the scene-delivery family this family's `scene` entry hands a resolved
  scene id to, as context only
- [`persistence/verification-contract.md`](../persistence/verification-contract.md),
  the save/load family a consumer routes a pack's `data` or `text_table`
  entry through, as context only
- [`shipped-game-gap-matrix.md`](../../benchmarks/shipped-game-gap-matrix.md),
  which ranks this family fifteenth and states why every product shape needs
  some form of it
- [`quality-tiers/README.md`](../quality-tiers/README.md) and
  [`haptics/README.md`](../haptics/README.md), sibling standards this family
  is modelled on
