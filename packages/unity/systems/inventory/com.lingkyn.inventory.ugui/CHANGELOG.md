# Changelog

## 0.3.0 - 2026-07-18

- Added `InventorySkin`, the first injectable visual seam for the UGUI adapter:
  a ScriptableObject carrying surface/text/slot-state colors, an optional font,
  and optional rounded 9-slice sprites. The renderer-neutral presentation
  contract still carries no visual vocabulary; all look-and-feel stays in the
  adapter or a consumer-authored skin.
- Views (`Item`, `Slot`, `Details`, `ActionMenu`, `Grid`, `Panel`, `Shell`) now
  expose `ApplySkin`; the shell resolves and applies a translucent spatial-glass
  default at runtime when no skin is injected, and re-skins pooled slots.
- Default palette aligns with the UI Toolkit sibling (`InventoryDocument.uss`) and
  public spatial-UI design language (visionOS windows/materials, PICO OS spatial
  UI, Meta Horizon MR); no consumer product, scene, or brand is hardcoded.
- Consumers can fully restyle by injecting their own `InventorySkin` asset without
  forking prefabs or code.

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
