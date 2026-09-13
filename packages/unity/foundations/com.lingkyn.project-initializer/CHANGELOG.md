# Changelog

## Unreleased

- Added the EditMode test `RequiredFoldersAreUnderProjectRootAndUnique` for the
  `IndieDirectoryContract.RequiredFolders` clause of the new foundations
  verification contract (`docs/standards/foundations/`). Repository validation
  now limits every foundation asmdef reference to Lingkyn or Unity-owned
  assemblies. No runtime or editor change. Editor execution is pending.

## 0.1.0

- Introduced the consumer-neutral `com.lingkyn.project-initializer` identity.
- Removed consumer-project assembly and runtime-type dependencies.
- Added opt-in build validation, tests, samples, documentation, and package maturity.
