# Persistence architecture contract

## Invariants

1. Consumers provide immutable, plain-data snapshots. The package never discovers
   authoritative state by crawling scenes or serializing arbitrary live objects.
2. A stored record is decoded only after its envelope and integrity metadata pass
   validation.
3. A migration chain is deterministic, monotonic and complete from the stored
   schema version to the requested version.
4. No live state changes until read, integrity, decode, migration and consumer
   validation all succeed.
5. A failed save cannot replace the last committed primary record.
6. Storage providers declare capabilities. `atomic_replace`, `recoverable_replace`
   and `best_effort_write` are different claims.
7. Authored Unity assets describe configuration and codecs; they are never mutable
   player-save state.

## Core concepts

| Concept | Responsibility |
| --- | --- |
| `SaveSlotId` | Validated logical identity; never an unchecked filesystem path. |
| `SaveEnvelope` | Format version, schema ID/version, commit ID, timestamp metadata, payload length, integrity algorithm/digest and payload bytes. |
| `ISaveCodec<T>` | Deterministic conversion between one DTO contract and bytes. |
| `IIntegrityProvider` | Computes and verifies a named digest over exact payload bytes. |
| `ISaveMigration` | One explicit schema-version step with stable source/target versions. |
| `MigrationPipeline` | Validates and executes exactly one unambiguous ordered chain. |
| `ISaveStore` | Reads and commits opaque envelope bytes and reports capabilities/errors. |
| `SaveCoordinator<T>` | Orchestrates codec, integrity, migration and store without owning game state. |
| `SaveResult<T>` | Structured success/failure with stable stage and error code; exceptions remain diagnostic causes, not public control flow. |

## Save flow

1. Consumer freezes a DTO snapshot.
2. Codec produces payload bytes.
3. Integrity provider hashes those exact bytes.
4. Core builds and serializes the envelope.
5. Store writes a unique staging file in the destination directory and flushes it.
6. Store commits using a capability-compatible strategy.
7. Only a successful commit becomes the current record; callbacks/events follow
   commit and use deterministic order.

## Load flow

1. Store selects primary or an explicitly identified recovery candidate.
2. Envelope parser validates format, bounds, schema identity and metadata.
3. Integrity provider verifies payload bytes before decoding.
4. Codec decodes the stored DTO version.
5. Migration pipeline produces the requested current DTO version.
6. Consumer validator accepts or rejects the candidate.
7. Consumer applies the accepted snapshot atomically to its own domain state.

## Unity adapter

The first Unity adapter may provide:

- a serializable envelope DTO and JsonUtility codec for supported plain DTOs;
- a validated `Application.persistentDataPath` root policy;
- local file staging, flush, primary/backup names and recovery inspection;
- ScriptableObject configuration for schema ID, current version, file extension,
  integrity provider and required commit capability; and
- Editor validation that rejects empty paths, unsafe slot IDs, duplicate migration
  edges, unsupported DTO shapes and misleading capability claims.

It must not call Unity APIs from background threads, serialize MonoBehaviours,
Scenes or ScriptableObjects as player state, or claim a platform capability not
proved by the exact compatibility profile.

## Failure model

Stable stages include `snapshot`, `encode`, `integrity`, `stage_write`, `flush`,
`commit`, `read`, `envelope`, `verify`, `decode`, `migrate`, `validate` and
`apply`. Stable error codes include invalid slot, not found, unsupported format,
future schema, missing/ambiguous migration, corrupt payload, unsupported commit
capability, I/O denied, out of space, cancelled and provider failure.

Checksums are corruption evidence only. Encryption, authentication, compression,
cloud conflict, platform quotas and user-facing recovery decisions are optional
future adapters/policies, never implicit Core behavior.
