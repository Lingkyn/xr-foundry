# LocalFilePersistence

Minimal consumer-owned DTO snapshot and coordinator wiring.

1. Create a `PersistenceUnityConfig` asset in your project.
2. Assign schema id/version, storage subdirectory, commit strategy, and required capability.
3. Use `LocalFilePersistenceExample.Run(config)` as a reference for `PersistenceUnityFactory.CreateCoordinator(...)`.
4. Keep gameplay state in plain DTO snapshots; never serialize live scene objects or ScriptableObject assets as mutable player state.

Claim ceiling: Editor/local-file evidence only. No device, cloud, security, or crash-durability claim is made by this sample.
