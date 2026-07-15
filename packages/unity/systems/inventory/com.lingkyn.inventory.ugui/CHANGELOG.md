# Changelog

## 0.2.0 - 2026-07-15

- Moved renderer-neutral view models, semantic states, stable slot intents, view port,
  and presenter into `com.lingkyn.inventory.presentation`.
- Updated all runtime views, tests, and the State Gallery sample to consume the
  `Lingkyn.Inventory.Presentation` namespace and assembly.
- Removed the unused `com.lingkyn.inventory.unity` authoring dependency.
- Breaking: consumers that directly use presentation contracts must update their
  namespace import and assembly definition reference.

## 0.1.1 - 2026-07-15

- Corrected shipped nested prefabs so required view references, text, layout,
  raycast targets, Selectables, and action controls are present and wired.
- Added runtime slot generation, visible selected/disabled states, and intent
  forwarding from the shipped grid.
- Added stable `SlotAddress` intents, fail-fast template validation, bounded scrolling
  for large capacities, complete pooled-slot unbinding, and transient hover/selection
  reset on slot reuse.
- Made grid cell layout adopt the selected slot template's preferred size so compact
  prefab variants retain a measurable layout difference.
- Corrected disabled presenter execution to throw explicitly instead of returning null,
  kept disabled presentation sticky across refresh/external aggregate changes, guarded
  selection and replay while disabled, and restricted pointer activation to the primary button.
- Replaced synthetic-object interaction coverage with prefab-backed layout,
  GraphicRaycaster, pointer, submit, and disabled-state tests.
- Added semantically distinct State Gallery models, a runnable bootstrap, sample
  assemblies, and input-backend-aware scene setup.

## 0.1.0 - 2026-07-15

- Added presenter/view-model boundaries and structural nested prefab assets.
- Known limitation: the shipped prefab references and visual controls were not
  functionally wired. Use `0.1.1` or later for a renderable composition.
