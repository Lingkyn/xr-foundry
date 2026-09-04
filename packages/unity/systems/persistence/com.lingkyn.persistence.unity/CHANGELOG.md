# Changelog

## [Unreleased]

- Implement ScriptableObject authoring validation, JsonUtility plain-DTO codec boundary, persistent-data root policy, local-file `ISaveStore`, recovery inspection, injected file fault seams, and focused EditMode contract tests for checkpoint `PERSISTENCE-UNITY-BUILD`.
- Isolate zero-byte primary, backup, and staging content as individual recovery candidates so one damaged file cannot prevent another readable candidate from reaching the selector.
