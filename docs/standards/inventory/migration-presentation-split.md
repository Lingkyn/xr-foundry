# Migration: single-renderer layout to the presentation-split graph

Applies to consumers pinned at or before commit
`d749d8b` (`Inventory XR: world-space XRI adapter and Pico acceptance`, PR #20)
that move to the current canonical graph. This note closes the missing migration
path recorded as [`LESSON-002`](../lessons/README.md); it does not record an
executed upgrade or rollback, which remains the `upgrade_and_rollback_across_a_release`
gate.

## What changed

| Before (`d749d8b`) | After (current `main`) |
| --- | --- |
| Packages at the repository root: `com.lingkyn.inventory.core` `0.1.0`, `com.lingkyn.inventory.unity` `0.1.0`, `com.lingkyn.inventory.ugui` `0.1.1`, `com.lingkyn.inventory.xr` `0.1.0` | Packages under `packages/unity/systems/inventory/`: `core` `0.1.1`, `unity` `0.1.1`, `presentation` `0.1.0`, `ugui` `0.2.0`, `uitoolkit` `0.1.0`, `xr.ugui` `0.1.0`, `xr.uitoolkit` `0.1.0` |
| View models, semantic states, intents, `IInventoryView`, and `InventoryPresenter` lived in the UGUI package (`Lingkyn.Inventory.UGUI` assembly, file `InventoryPresentation.cs`) | The same public types moved unchanged in shape to `com.lingkyn.inventory.presentation`, assembly and namespace `Lingkyn.Inventory.Presentation` |
| UGUI depended on Core and Unity authoring | UGUI depends on Core and Presentation; it no longer references Unity authoring |
| One XR package, `com.lingkyn.inventory.xr`, namespace and assembly `Lingkyn.Inventory.XR`, depending on UGUI `0.1.1` and XRI `3.3.2` | Renderer-explicit `com.lingkyn.inventory.xr.ugui`, namespace and assembly `Lingkyn.Inventory.XR.UGUI`, depending on Presentation, UGUI `0.2.0`, and XRI `3.5.1`; a sibling `xr.uitoolkit` exists for the UI Toolkit route |
| Git install selectors pointed the `path` query at a package directory in the repository root | Git install selectors point the `path` query at the nested `packages/unity/systems/inventory/` directory; copy the exact selectors from the README install matrix |

The XR runtime type names (`InventoryWorldSpaceProfile`, `InventoryWorldSpaceSurface`,
`InventoryXrSceneValidator`, `InventoryXrValidationIssue`, `InventoryXrValidationReport`)
and the UGUI view type names are unchanged. What moved is the package, assembly,
namespace, and dependency edge, not the member shapes.

## Consumer steps

1. **Replace every Git selector.** Change the `path` query to the nested layout and
   pin every Inventory sibling to the same full 40-character commit SHA. Unity
   Package Manager rejects short SHAs and cannot resolve Git package-to-package
   dependencies, so list Core, Unity authoring, Presentation, UGUI, and the XR
   renderer package explicitly.
2. **Add `com.lingkyn.inventory.presentation`.** Any assembly that references
   `InventoryPresenter`, `InventoryViewModel`, `InventorySlotViewModel`,
   `InventorySlotIntent`, `InventoryUiState`, or `IInventoryView` now needs a
   reference to `Lingkyn.Inventory.Presentation` in its `.asmdef` and
   `using Lingkyn.Inventory.Presentation;` in source.
3. **Rename the XR package and namespace.** Replace `com.lingkyn.inventory.xr` with
   `com.lingkyn.inventory.xr.ugui`, `Lingkyn.Inventory.XR` with
   `Lingkyn.Inventory.XR.UGUI` in `.asmdef` references and `using` directives, and
   move to XR Interaction Toolkit `3.5.1`. Prefab and profile assets are re-shipped
   under the new package; re-link consumer references to them.
4. **Restore the Unity authoring reference where UGUI code relied on it.** UGUI no
   longer pulls `Lingkyn.Inventory.Unity` transitively. A consumer assembly that used
   `ItemCatalogAsset` or `InventoryDefinitionAsset` through the UGUI reference must
   reference `Lingkyn.Inventory.Unity` directly.
5. **Do not bring the old `com.lingkyn.inventory.xr` package forward.** The
   repository keeps no compatibility layer, redirect, or shim for the old path, and
   evidence recorded for the old package does not transfer.
6. **Re-run the consumer's own compile and tests** and record the tuple. This note
   grants no compatibility claim; the consumer's clean compile at the new pin is the
   evidence.

## Rollback

Rollback is a pin change back to the previous full SHA plus reversal of steps 2
through 4. Because the old layout lives only in Git history, the rollback target is
the commit, never a branch.

## What this note does not cover

- Behavior changes inside UGUI `0.1.1` to `0.2.0` (functional nested prefabs,
  stable `SlotAddress` intents, bounded scrolling) are recorded in the UGUI package
  changelog; adopting them may require consumer prefab re-linking.
- The UI Toolkit route is new in this graph and has no predecessor to migrate from.
