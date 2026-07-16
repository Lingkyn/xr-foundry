# Lingkyn Persistence Unity

Incubating Unity adapter for `com.lingkyn.persistence.core`.

## Claim ceiling

This package proves only what its exact consumer tuple validates:

- ScriptableObject authoring for schema id/version, safe path policy, migration-edge shape, integrity provider, and required commit capability.
- JsonUtility encoding for explicitly supported plain DTO snapshots.
- Local-file staging, flush, commit, backup, and recovery inspection under a persistent-data root policy.
- EditMode tests with injected temporary directories and file-operation fault seams.

It does **not** claim crash durability, mobile/device behavior, cloud sync, authentication, encryption, cross-version release support, or catalog maturity.

## Consumer wiring

1. Create a `PersistenceUnityConfig` asset with schema id, current schema version, file extension, storage subdirectory, commit strategy, required capability, and declared migration edges.
2. Freeze consumer-owned plain DTO snapshots: concrete `[Serializable]` types with explicitly serialized fields only (public fields or `[SerializeField]`). Require strict UTF-8 payloads at decode. Unsupported shapes fail closed, including `UnityEngine.Object`, dictionaries, delegates, generic/polymorphic roots, cyclic graphs, and readonly serialized fields.
3. Build `JsonUtilitySaveCodec<TState>` and consumer migrations implementing `ISaveMigration<TState>`.
4. Create a coordinator through `PersistenceUnityFactory.CreateCoordinator(...)` with `PersistentDataRootProvider` or an injected test root.
5. Call `SaveCoordinator<TState>.Save` / `LoadValidated` / `LoadAndApply`; let Core decide recovery between primary and backup. Staging is exposed for inspection only and is never promoted by Core policy.

## Capability semantics

- `AtomicReplace` is advertised only when the configured strategy uses supported `File.Replace` semantics with backup preconditions on replacement commits.
- Initial create commits use staged move semantics. `AtomicReplace` fails closed when no primary exists yet.
- Required commit capabilities are a minimum gate; the configured strategy selects the commit algorithm.
- `RecoverableReplace` and `BestEffortWrite` are separate, conservative claims. Preservation is verified from actual primary/backup bytes after failure.

See `Samples~/LocalFilePersistence` for a minimal DTO snapshot and coordinator wiring example.
