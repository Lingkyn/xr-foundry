# Lingkyn Settings Unity

Unity ScriptableObject authoring adapter for `com.lingkyn.settings.core`.

## Public surface

- `SettingDefinitionAsset`, `SettingsCatalogAsset`, `SettingsProfileAsset`: immutable authoring assets for definitions, catalogs, and profile layers.
- `SettingsUnityConverter` / `SettingsUnityValidator`: deterministic Core conversion and actionable asset/index/key validation errors.
- `SettingsUnityFactory`: explicit consumer-provided applicator and persistence wiring into `SettingsCoordinator`.

## Non-goals

- No scene search, PlayerPrefs, renderer, concrete graphics/audio/input policy, or mutable player choices stored in assets.
- No device, certification, or accessibility compliance claims.

## Sample

Import the `SettingsAuthoring` sample for ScriptableObject catalog conversion and explicit applicator registration.
