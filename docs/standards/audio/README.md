# Audio events package-family standard

Status: proposal in the source-gate queue (`NEXT-AUDIO-EVENTS`); no code exists yet

This standard defines reusable audio-intent mechanics: stable audio event identity,
bus and snapshot state, typed parameter contracts, and spatial attachment intents,
with a thin Unity AudioMixer adapter planned separately. It does not define any
product's sound content, mix, or emotional tone. It is derived only from the
positive public sources in [`source-manifest.json`](source-manifest.json).

## Capability boundary

The family separates:

- immutable audio event identity (`AudioEventId`) from any clip, asset, or
  middleware event that a consumer binds to it;
- bus identity (`BusId`) and snapshot identity (`SnapshotId`) in a declared mix
  graph from the engine's or middleware's mixer objects;
- typed parameter contracts (`ParameterId` with a closed kind and a declared range)
  that reject an unknown parameter, a wrong kind, or an out-of-range value from any
  product's tuning values;
- spatial attachment intents that reference an anchor identity (`AnchorId`) from
  an engine transform, a scene object, or a tracked device;
- deterministic state snapshots produced by a sequence of intents from any
  playback, DSP, or scheduling behavior; and
- structured results with stable failure codes (`identity.malformed`,
  `event.unknown`, `bus.unknown`, `snapshot.unknown`, `parameter.unknown`,
  `parameter.kind.mismatch`, `parameter.out_of_range`, `anchor.unknown`) from any
  editor, console, or mixing UI.

The family does not own DSP, codecs, decoding, streaming, middleware runtimes or
their banks, mixing or authoring UI, occlusion or reverb models, head-related
transfer functions, loudness measurement, or any claim about listener comfort in a
headset.

## Planned package boundary

| Layer | Owns | Must not own |
| --- | --- | --- |
| Engine-light Core (package id reserved only after admission) | event, bus, snapshot, parameter, and anchor identity; the declared mix graph; parameter contracts and validation; spatial attachment intents; deterministic state snapshots; structured results | Unity types, audio clips, playback, DSP, middleware bindings, product sound content |
| Unity AudioMixer adapter (package id reserved only after admission) | binding of bus and parameter identity to an `AudioMixer` and its exposed parameters with fail-closed authoring validation and stable codes, snapshot transition intents mapped to `AudioMixerSnapshot` with an explicit duration, a plain runtime constructed with explicit references; a bridge to a middleware runtime is a later seam | domain event identity, parameter ranges, validation rules, spatializer selection, scene singletons, static instances |

## Evidence boundary

An EditMode test can prove identity validation, mix-graph construction, parameter
rejection, anchor attachment state, snapshot determinism, and adapter binding
validation for the declared tuple. It cannot prove audible output, spatialization,
latency, loudness, mixer transition timing, spatializer plug-in behavior, or headset
comfort; those claims require a Device Lab receipt or a measurement procedure that
this family does not yet define.

## Lessons register dispositions (to be added to the register when the family goes live)

| Lesson | Disposition | Rationale |
| --- | --- | --- |
| LESSON-001 closed classification vs open tags | adopted | Event, bus, snapshot, parameter, and anchor ids are validated closed identities and parameter kinds are a closed enum; any consumer grouping stays in open metadata outside Core |
| LESSON-002 migration path | deferred | No release exists yet; the first release records its compatibility policy |
| LESSON-003 single-workstation gate | adopted | Tests run in `run_unity_gates.py` and the consumer workflow like every family |
| LESSON-004 by-name resolution fails closed | adopted | The Core resolves nothing by name; the Unity adapter resolves exposed parameters and snapshots by name and must report a missing mixer, parameter, or snapshot explicitly |
| LESSON-005 clause coverage | deferred | No tests exist yet; the blueprint adds a coverage map that maps every contract clause to named tests before a scaffold is generated |
| LESSON-006 sibling pinning | deferred | Applies at the first Git-consumer validation |
| LESSON-007 skin seam | not_applicable | The family renders nothing; mixing and authoring UI is excluded |
| LESSON-008 version bump is a verification claim | adopted | No package version exists; the first scaffold enters at 0.1.0 and stays there until its first verified profile |

See also:

- [`verification-contract.md`](verification-contract.md)
- [`source-manifest.json`](source-manifest.json)
- [`admission.draft.json`](admission.draft.json)
- [`next-batch.json`](../../foundry/queue/next-batch.json) queue entry and the
  [system admission gate](../../foundry/system-admission.md)
- [`lessons-register.json`](../lessons/lessons-register.json)
