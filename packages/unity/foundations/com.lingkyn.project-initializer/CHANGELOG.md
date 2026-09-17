# Changelog

## Unreleased

- Added `ProjectFolderScaffold.EnsureDirectories(projectRoot, folders)`;
  `EnsureIndieDirectories()` now delegates to it with the contract root and
  folders, so behavior is unchanged. Added
  `IndieProjectValidator.ValidateIndieBaseline(projectRoot)`, which runs only the
  directory-contract checks (marker, folders, scenes) under the given root; the
  parameterless `ValidateIndieBaseline()` runs the same checks against
  `Assets/_Project` in the same issue order as before, followed by the project-wide
  rules. Added `IndieDirectoryContract.RequiredFoldersUnder`,
  `BaselineScenesUnder`, and `ActivationMarkerUnder` to rebase the contract paths.
  Added two EditMode tests that work in a disposable
  `Assets/__FoundationsTests_<guid>` root and delete it in `finally`:
  `ScaffoldingTwiceReusesEveryFolderAndCreatesNothingNew` (PI-03) and
  `ValidateReportsMissingFoldersScenesAndMarkerWithStableCodes` (PI-04). The test
  assembly now holds four tests. Not yet executed in a Unity Editor.
- Added the EditMode test `RequiredFoldersAreUnderProjectRootAndUnique` for the
  `IndieDirectoryContract.RequiredFolders` clause of the new foundations
  verification contract (`docs/standards/foundations/`). Repository validation
  now limits every foundation asmdef reference to Lingkyn or Unity-owned
  assemblies. No runtime or editor change. Editor execution is pending.

## 0.1.0

- Introduced the consumer-neutral `com.lingkyn.project-initializer` identity.
- Removed consumer-project assembly and runtime-type dependencies.
- Added opt-in build validation, tests, samples, documentation, and package maturity.
