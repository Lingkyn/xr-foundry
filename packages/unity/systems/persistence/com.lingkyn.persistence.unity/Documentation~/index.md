# Lingkyn Persistence Unity

Unity adapter documentation for configuration authoring, JsonUtility DTO boundaries, persistent-data path policy, local-file commit capabilities, and recovery inspection.

## Supported JsonUtility DTO tuple

Supported shapes must satisfy all of the following:

- Root and nested snapshot types are concrete classes or structs marked with `[Serializable]`.
- Only explicitly serialized instance fields are allowed: public fields, or non-public fields marked with `[SerializeField]`.
- Field types must recursively satisfy the same tuple.
- Supported leaf types: primitives, enums, strings, and arrays of supported element types.

Non-goals (fail closed):

- `UnityEngine.Object`, `MonoBehaviour`, `ScriptableObject`, and other Unity asset references.
- Delegates, dictionaries, generic collection shapes, polymorphic interface or abstract roots, and cyclic graphs.
- Readonly serialized fields (`readonly` / init-only fields that would be skipped silently by JsonUtility).
- Types without at least one explicitly serialized field.

Decode uses strict UTF-8 (`throwOnInvalidBytes: true`). Invalid UTF-8 payloads fail closed at decode.

## Commit strategies

| Strategy | Advertised capabilities | Replacement behavior |
| --- | --- | --- |
| `AtomicFileReplace` | `BestEffortWrite`, `RecoverableReplace`, `AtomicReplace` | `File.Replace` with backup whenever a primary already exists, regardless of the caller's minimum capability |
| `RecoverableCopyReplace` | `BestEffortWrite`, `RecoverableReplace` | Copy primary to backup, then move staged file over primary |
| `BestEffortDirectWrite` | `BestEffortWrite` | Move staged file over primary without backup semantics |

Initial create commits use staged move into an empty primary path. `AtomicReplace` is rejected when no primary exists yet.

Required commit capabilities are a minimum gate only; the configured strategy always selects the commit algorithm.

`PriorCommittedRecordPreserved` is derived from verified post-failure primary and backup bytes. When verification cannot be established, the adapter reports `false`.

## Recovery inspection

`LocalFileSaveStore.ReadCandidates` returns primary, backup, and staging candidates with stable ids. Staging enumeration is routed through the file-operation seam and sorted deterministically. Core `SaveRecoveryCandidateSelector` never promotes staging; consumers inspect staging separately when needed.
