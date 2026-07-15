# Changelog

## Unreleased

- Set this package revision's current implementation target to XR Interaction
  Toolkit `3.5.1`; exact immutable-revision evidence remains pending in the
  compatibility profile catalog.
- Established `com.lingkyn.inventory.xr.ugui` and
  `Lingkyn.Inventory.XR.UGUI` as the renderer-explicit UGUI Canvas/XRI
  composition.
- Renamed every runtime, editor, sample, and test Assembly Definition file to its
  renderer-explicit declared assembly name while preserving Unity asset GUIDs.
- Added a fail-closed scene gate for an active UI-capable XRI interactor, including
  ray layer/range checks, rather than treating an empty `XRUIInputModule` as route-ready.
- Bound the UGUI dependency to the renderer-neutral presentation package.
- Linked the generic public Device Lab contract and Inventory world-space UI plan.
  Named-device evidence remains independent for each renderer, dependency, build,
  input, and device tuple; direct-poke claims remain separate from controller-ray
  coverage.

## 0.1.0 - 2026-07-15

- Added provider-neutral world-space Inventory UGUI surface and profile.
- Added strict Canvas, tracked-raycaster, event-camera, EventSystem, and
  `XRUIInputModule` validation.
- Added non-head-locked initial placement and consumer-owned configuration seams.
- Added a Package Manager sample, prefab-backed tracked-ray coverage for distinct
  shipped slots, and a real `XRPokeInteractor` release path.
