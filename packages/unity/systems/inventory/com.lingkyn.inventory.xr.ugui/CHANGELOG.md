# Changelog

## Unreleased

- Established `com.lingkyn.inventory.xr.ugui` and
  `Lingkyn.Inventory.XR.UGUI` as the renderer-explicit UGUI Canvas/XRI
  composition.
- Added a fail-closed scene gate for an active UI-capable XRI interactor, including
  ray layer/range checks, rather than treating an empty `XRUIInputModule` as route-ready.
- Bound the UGUI dependency to the renderer-neutral presentation package.
- Added the public Inventory XR device-acceptance receipt and machine validation
  contract. The first profile requires named PICO tracked-controller evidence and
  keeps direct-poke device claims separate from automated coverage.

## 0.1.0 - 2026-07-15

- Added provider-neutral world-space Inventory UGUI surface and profile.
- Added strict Canvas, tracked-raycaster, event-camera, EventSystem, and
  `XRUIInputModule` validation.
- Added non-head-locked initial placement and consumer-owned configuration seams.
- Added a Package Manager sample, prefab-backed tracked-ray coverage for distinct
  shipped slots, and a real `XRPokeInteractor` release path.
