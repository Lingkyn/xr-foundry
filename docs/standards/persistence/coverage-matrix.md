# Persistence source coverage matrix

The matrix records which positive sources support each architecture decision. A
blank cell would block admission; context-only materials do not satisfy coverage.

| Capability | Primary positive sources | Admitted conclusion |
| --- | --- | --- |
| Persistent Unity location | `unity-6000.3-persistent-data-path` | Path selection is a Unity provider concern with platform-specific support and identity behavior. |
| DTO serialization | `unity-6000.3-json-utility`, `unity-6000.3-serialization-rules`, `bayat-save-game-free` | The codec is replaceable; save DTOs obey explicit constraints and remain separate from live objects. |
| Versioned envelope | `daggerfall-unity-save-versioning`, `openttd-saveload-evolution` | Schema identity/version and payload metadata precede decoding into live state; the format version remains distinct from a product build version. |
| Deterministic migration | `daggerfall-unity-save-versioning`, `openttd-saveload-evolution` | Migrations are explicit, ordered, version-bounded transformations. The Foundry contract narrows this to a reviewed one-step chain so missing, ambiguous, cyclic and future-version paths fail before a consumer snapshot is returned. |
| Integrity | `dotnet-sha256` | Verify stored payload bytes before decode; an unkeyed digest detects corruption but is not authentication. |
| Staged write and flush | `dotnet-file-stream-flush` | Serialization, staging write and flush are separate failure stages. |
| Replace and backup | `dotnet-file-replace`, `daggerfall-unity-save-versioning` | A provider may declare replace-with-backup only for a tested tuple; unexpected or superseded data is preserved rather than silently discarded, while weaker fallbacks use a different capability name. |
| Provider seams | `bayat-save-game-free`, `unity-6000.3-persistent-data-path` | Codec, path and storage concerns are independently replaceable. |
| Error and recovery model | `dotnet-file-replace`, `dotnet-file-stream-flush`, `daggerfall-unity-save-versioning`, `openttd-saveload-evolution` | Results distinguish write stages, preserve the prior committed save where the provider declares that capability, and fail closed on future, corrupt or incomplete migration paths instead of returning ambiguous state. |
| Async/cancellation boundary | `bayat-save-game-free` | Core operations expose async-friendly contracts; Unity object access and snapshot capture stay on the consumer-approved thread. |

## Explicitly uncovered by this source gate

The following require later, provider-specific source gates and evidence:

- cloud synchronization and conflict resolution;
- platform-holder save APIs and quotas;
- authenticated encryption, signing, anti-cheat or secret management;
- console certification behavior;
- WebGL IndexedDB transaction behavior;
- mobile background/termination guarantees;
- delta/incremental world saves; and
- multiplayer authoritative-state persistence.
