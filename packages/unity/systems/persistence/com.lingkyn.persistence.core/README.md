# Lingkyn Persistence Core

Engine-light persistence core with no `UnityEngine` dependency.

## Public surface

- `SaveSlotId`: validated logical slot identifier with strict character and length limits.
- `SaveEnvelope` + `SaveEnvelopeBinaryCodec`: versioned deterministic envelope with strict bounds for metadata, digest, and payload bytes.
- `SaveResult` / `SaveResult<T>`: stable stage and error-code result model for deterministic load and provider failure handling.
- `SaveCommitResult`: dedicated commit receipt with explicit `Committed` and `PriorCommittedRecordPreserved` semantics plus warning diagnostics.
- `IIntegrityProvider` + `Sha256IntegrityProvider`: payload digest abstraction and SHA-256 implementation when available.
- `ISaveMigration<T>` + `MigrationPipeline<T>`: ordered migration graph that rejects missing, ambiguous, non-monotonic, overshoot, and future-version paths; a back-edge (cycle attempt) is classified as non-monotonic by precedence.
- `ISaveStore` + `SaveCommitCapabilities`: opaque storage contract with declared commit capabilities and explicit commit outcomes.
- `ISaveCommitObserver<T>`: deterministic post-commit observer hook invoked only after a durable commit.
- `SaveCoordinator<T>`: orchestrates store, envelope, integrity, codec, migration, and validation with fail-closed load behavior; checks cancellation immediately before commit.

## Non-goals

- No filesystem provider implementation.
- No Unity API usage, ScriptableObject state serialization, UI, cloud, encryption, or product configuration.
