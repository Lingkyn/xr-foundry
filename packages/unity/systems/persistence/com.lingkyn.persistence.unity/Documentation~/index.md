# Lingkyn Persistence Unity

Unity adapter documentation for configuration authoring, JsonUtility DTO boundaries, persistent-data path policy, local-file commit capabilities, and recovery inspection.

## Supported JsonUtility DTO tuple

- Primitives, enums, strings, and arrays of supported element types.
- `[Serializable]` classes/structs whose serialized fields recursively satisfy the same tuple.
- Unsupported: `UnityEngine.Object`, `MonoBehaviour`, `ScriptableObject`, delegates, dictionaries, polymorphic interface roots, and cyclic graphs.

## Commit strategies

| Strategy | Advertised capabilities | Replacement behavior |
| --- | --- | --- |
| `AtomicFileReplace` | `BestEffortWrite`, `RecoverableReplace`, `AtomicReplace` | `File.Replace` with backup when replacing an existing primary |
| `RecoverableCopyReplace` | `BestEffortWrite`, `RecoverableReplace` | Copy primary to backup, then move staged file over primary |
| `BestEffortDirectWrite` | `BestEffortWrite` | Move staged file over primary without backup semantics |

Initial create commits always use staged move into an empty primary path.

## Recovery inspection

`LocalFileSaveStore.ReadCandidates` returns primary, backup, and staging candidates with stable ids. Core `SaveRecoveryCandidateSelector` never promotes staging; consumers inspect staging separately when needed.
