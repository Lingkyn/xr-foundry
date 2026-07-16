# Changelog

## [0.1.0] - 2026-07-16

### Added

- ScriptableObject authoring assets, deterministic Core conversion, validation, and explicit factory wiring.
- EditMode contract tests for conversion determinism, validation issues, asset immutability, and applicator wiring.

### Changed

- Package-root `Runtime.meta` replaces nested `Runtime/Runtime.meta` for correct Unity folder import.
- `SettingsUnityFactory.UseDefaultsOnRepositoryLoadFailure` must be set explicitly to fall back when repository load fails.
