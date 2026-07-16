# Lingkyn Persistence Core

Engine-light persistence core with no `UnityEngine` dependency.

## Public surface

- `SaveSlotId`: validated logical slot identifier with strict character and length limits.
- `SaveEnvelope` + `SaveEnvelopeBinaryCodec`: versioned deterministic envelope with strict bounds for metadata, digest, and payload bytes.
- `SaveResult` / `SaveResult<T>`: stable stage and error-code result model for deterministic failure handling.
- `IIntegrityProvider` + `Sha256IntegrityProvider`: payload digest abstraction and SHA-256 implementation when available.
- `ISaveMigration<T>` + `MigrationPipeline<T>`: ordered migration graph that rejects missing, ambiguous, non-monotonic, overshoot, and future-version paths; a back-edge (cycle attempt) is classified as non-monotonic by precedence.
- `ISaveStore` + `SaveCommitCapabilities`: opaque storage contract with declared commit capabilities.
- `SaveCoordinator<T>`: orchestrates store, envelope, integrity, codec, migration, and validation with fail-closed load behavior.

## Non-goals

- No filesystem provider implementation.
- No Unity API usage, ScriptableObject state serialization, UI, cloud, encryption, or product configuration.
