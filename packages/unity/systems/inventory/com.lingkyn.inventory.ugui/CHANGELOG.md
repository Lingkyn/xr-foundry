# Changelog

## 0.2.0 - 2026-07-15

- Established renderer-neutral view models, semantic states, stable slot intents,
  view port, and presenter in `com.lingkyn.inventory.presentation`.
- Runtime views, tests, and the State Gallery sample consume the
  `Lingkyn.Inventory.Presentation` namespace and assembly.
- Shipped nested prefabs with wired view references, text, layout, raycast
  targets, Selectables, and action controls.
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
- Kept Unity authoring, XR, product content, scenes, and styling outside the
  renderer package.
