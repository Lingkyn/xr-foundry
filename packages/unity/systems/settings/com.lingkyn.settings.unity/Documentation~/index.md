# Lingkyn Settings Unity

`com.lingkyn.settings.unity` converts ScriptableObject catalogs and profiles into
immutable Core registries and coordinates explicit consumer-provided applicators.
Its namespaces start with `Lingkyn.Settings.Unity`, and its only package
dependency is `com.lingkyn.settings.core`.

Runtime player choices remain in Core snapshots and persistence providers, not in
authored assets.

## Authoring assets

| Asset | Create menu | Holds |
| --- | --- | --- |
| `SettingDefinitionAsset` | `Lingkyn/Settings/Setting Definition` | Key, value kind, typed default, default scope, application order, restart flag, numeric/string/option constraint records, and an accessibility metadata record |
| `SettingsCatalogAsset` | `Lingkyn/Settings/Settings Catalog` | An ordered array of definition assets that converts into one `SettingsRegistry` |
| `SettingsProfileAsset` | `Lingkyn/Settings/Settings Profile` | A profile id plus ordered `ProfileLayerRecord` layers of typed overrides that reference definition assets |

Assets are authoring input only. Conversion produces new Core values; the
coordinator never reads a `ScriptableObject` at apply time and never writes player
state back into an asset.

## Conversion and validation

- `SettingsUnityConverter.ConvertCatalog(catalog)` returns
  `SettingsResult<SettingsRegistry>`, and `ConvertDefinition(asset)` converts one
  definition. Conversion is deterministic for identical asset content.
- A conversion failure carries the Core `SettingsValidationCode` and a message
  that names the asset path, definition index, and key, so an author can fix the
  exact record.
- `SettingsUnityValidator.ValidateCatalog(catalog)` returns every
  `SettingsUnityValidationIssue` (asset path, index, key, message) instead of
  stopping at the first failure, and additionally reports duplicate definition
  keys across the catalog. Use it for editor tooling and tests.

## Factory wiring

`SettingsUnityFactory.CreateCoordinator(config)` takes a
`SettingsUnityFactoryConfig` and returns `SettingsResult<SettingsCoordinator>`.

| Field | Purpose |
| --- | --- |
| `Catalog` | Required catalog asset converted into the registry |
| `Applicators` | Consumer-provided `ISettingApplicator` list; the package ships none |
| `Constraints` | Optional whole-snapshot `ISettingsConstraint` list |
| `Repository` | Optional `ISettingsSnapshotRepository`; when set, the factory loads and validates the stored snapshot before constructing the coordinator |
| `InitialRevision` | Revision used for a fresh default snapshot |
| `UseDefaultsOnRepositoryLoadFailure` | Must be set to `true` to fall back to registry defaults when the repository load fails; otherwise the factory fails closed with the load error |

A loaded snapshot that fails registry or constraint validation is rejected rather
than partially adopted, regardless of the fallback flag.

## Non-goals

- No scene search, PlayerPrefs, renderer, UI, or concrete graphics/audio/input
  policy. Applicators that touch Unity subsystems belong to the consumer.
- No mutable player choices stored in assets.
- No device, certification, or accessibility compliance claims.

## Sample

Import the `SettingsAuthoring` sample and call
`SettingsAuthoringExample.Run(catalog)` with a created or loaded
`SettingsCatalogAsset` to see catalog conversion and explicit applicator
registration.
