# Persistence package-family standard

Status: source-gate review

Implementation Issue: [#54](https://github.com/Lingkyn/xr-foundry/issues/54)

This standard defines reusable save-data mechanics, not the game-specific state
that a title chooses to save. It is derived only from the positive public sources
in [`source-manifest.json`](source-manifest.json). Consumer/private projects,
course work, private prompts, and previously improvised save code are excluded
from derivation material.

## Capability boundary

The family separates:

- immutable domain snapshots supplied by consumers;
- a versioned persistence envelope;
- replaceable payload codecs;
- deterministic, explicit migrations;
- integrity verification;
- replaceable storage providers and declared commit capabilities;
- orchestration that validates everything before returning a load candidate; and
- consumer-controlled application of the candidate to live state.

The family does not decide which gameplay state is authoritative, serialize a live
scene graph, mutate authored ScriptableObject assets, provide a save-slot UI, sync
cloud accounts, resolve multiplayer authority, or claim encryption/tamper
resistance.

## Planned package boundary

Package identifiers remain unreserved until this source gate is independently
reviewed. The admitted blueprint is expected to separate an engine-light Core from
a thin Unity adapter:

| Layer | Owns | Must not own |
| --- | --- | --- |
| Engine-light Core | envelopes, codecs, integrity contracts, migrations, storage capabilities, save/load orchestration and structured results | Unity types, file paths, scenes, UI, cloud SDKs, platform assumptions |
| Unity adapter | Unity-compatible JSON DTO codec, `persistentDataPath` path policy, local file provider and ScriptableObject configuration | domain snapshot selection, live-object serialization, universal atomicity/security claims |

## Evidence boundary

An Editor test can prove deterministic orchestration and one concrete file-provider
tuple. It cannot prove Android/iOS/WebGL/tvOS/console behavior, crash durability,
cloud synchronization, security, or every Unity version. Unsupported atomic file
replacement must be reported as a capability failure or a separately named weaker
commit mode; it must never inherit an `atomic` claim.

See also:

- [`architecture-contract.md`](architecture-contract.md)
- [`coverage-matrix.md`](coverage-matrix.md)
- [`persistence-standard.json`](persistence-standard.json)
- [`verification-contract.md`](verification-contract.md)
