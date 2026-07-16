# Inventory XR adapter

Install this package only in consumers that need XR presentation. It depends on
Inventory UGUI and XR Interaction Toolkit `3.3.2`; it has no direct XR Management,
OpenXR, or vendor SDK dependency.

## Scene integration

1. Add or reuse one consumer-owned XR Origin and tracked UI interactor setup.
2. Keep exactly one active EventSystem with exactly one active
   `XRUIInputModule`, with `enableXRInput` enabled.
3. Instantiate `Runtime/Prefabs/InventoryWorldSpaceSurface.prefab` as a scene root
   or beneath a non-camera world anchor.
4. Bind the XR camera through `InventoryWorldSpaceSurface.BindEventCamera`.
5. Render Inventory UGUI view models through `InventoryWorldSpaceSurface.Shell`.
6. Call `surface.ValidateSceneOrThrow()` after binding and again after relevant
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
[`Inventory XR Device Acceptance Receipt`](https://github.com/Lingkyn/xr-foundry/blob/main/docs/validation/inventory-xr-device-receipt-template.md)
defines the exact PICO tracked-controller sequence, immutable APK/package identity,
pass rule, optional direct-poke boundary, and machine-validation command.
