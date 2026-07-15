# Inventory XR UGUI adapter

Install this package only in consumers that need UGUI-based XR presentation. It depends on
Inventory UGUI and XR Interaction Toolkit `3.5.1`; it has no direct XR Management,
OpenXR, or vendor SDK dependency.

The package ID is `com.lingkyn.inventory.xr.ugui`. For a Git install, use the
canonical selector
`?path=/packages/unity/systems/inventory/com.lingkyn.inventory.xr.ugui` and pin
it to a full repository revision. Its public API namespace is
`Lingkyn.Inventory.XR.UGUI`.

## Scene integration

1. Add or reuse one consumer-owned XR Origin and tracked UI interactor setup.
2. Keep exactly one active EventSystem with exactly one active
   `XRUIInputModule`, with `enableXRInput` enabled.
3. Keep at least one active `XRRayInteractor`, `NearFarInteractor`,
   `XRPokeInteractor`, or compatible `IUIInteractor` with UI interaction enabled.
   Ray routes must include the Canvas layer and reach it with their configured range.
4. Instantiate `Runtime/Prefabs/InventoryWorldSpaceSurface.prefab` as a scene root
   or beneath a non-camera world anchor.
5. Bind the XR camera through `InventoryWorldSpaceSurface.BindEventCamera`.
6. Render Inventory UGUI view models through `InventoryWorldSpaceSurface.Shell`.
7. Call `surface.ValidateSceneOrThrow()` after binding and again after relevant
   scene/input configuration changes. This revalidates the scene and opens or
   closes the prefab's local fail-closed interaction gate.

`Prepare(camera, true)` applies the profile, performs a one-time world placement,
and binds the event camera. It detaches the surface from any prior parent so its
default result cannot be head locked.

The sample setup creates an EventSystem only when none exists. It refuses to alter
an existing missing, duplicate, or incompatible input-module configuration.

For Git installs, explicitly pin all required `com.lingkyn.inventory.*` packages
to the same full repository revision; do not rely on custom transitive semver
resolution without a configured registry.

## Claim boundary

Editor and PlayMode tests are not headset evidence. Record a named device, runtime
versions, build revision, tester, and observations before claiming readability,
targeting, reach, occlusion, scale, angle, or comfort.

The public
[`Device Lab`](https://github.com/Lingkyn/xr-foundry/blob/main/docs/device-lab/README.md)
and
[`Inventory world-space UI plan`](https://github.com/Lingkyn/xr-foundry/blob/main/docs/device-lab/test-plans/inventory-world-space-ui-v1.json)
define the exact renderer/device/input composition, immutable build/package
identity, required checks, optional-claim boundary, and the generic
`--device-lab-receipt` validation route.
