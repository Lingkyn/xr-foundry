# Lingkyn Inventory XR

Status: **incubating** until Android and real-device gates pass. Immutable Git
consumer validation has passed for the source revision recorded in the public
validation receipt.

This optional package composes `com.lingkyn.inventory.ugui` on a world-space
Canvas using XR Interaction Toolkit `3.3.2`. It does not redefine Inventory
domain types and does not add XR dependencies to Core, Unity authoring, or UGUI.

The shipped `InventoryWorldSpaceSurface` prefab contains:

- a world-space Canvas, fail-closed CanvasGroup, and configurable physical scale;
- `TrackedDeviceGraphicRaycaster`;
- the nested UGUI `InventoryShell`; and
- `InventoryWorldSpaceSurface` validation and placement bindings.

It deliberately contains no Camera, XR Origin, EventSystem, input module, OpenXR
provider, PICO SDK, scene, or head-follow component. A consumer scene must have
exactly one active EventSystem with exactly one active `XRUIInputModule` whose
`enableXRInput` gate is enabled. Missing,
duplicate, or incompatible modules fail validation rather than silently falling
back to desktop UI input. The prefab starts with interaction and its tracked
raycaster disabled; `Revalidate()` opens only that local surface after every gate
passes and closes it again if scene configuration later becomes invalid.

`InventoryWorldSpaceProfile` is a ScriptableObject seam for reference resolution,
meters-per-pixel, dynamic pixels per unit, initial placement distance/offset, and
raycaster options. The defaults are starting values for validation, not a comfort
or readability claim. Use a consumer-owned profile for product tuning.

The default placement helper positions the surface once in front of a supplied
camera and leaves it at a scene-root world pose. It never parents to or follows the
camera. Consumers may instead place it beneath a non-camera world anchor.

Automated tests prove package structure, strict input-module validation, tracked
raycasts into distinct shipped slots, a real `XRPokeInteractor` release route,
and non-head-locked behavior. Only real headset evidence can prove controller
usability, readability, scale, angle, occlusion, reach, or comfort.

Use the repository's
[`Inventory XR Device Acceptance Receipt`](https://github.com/Lingkyn/xr-foundry/blob/main/docs/validation/inventory-xr-device-receipt-template.md)
for the named-device gate. The first profile covers a PICO headset with tracked
controllers while keeping the package itself independent of a vendor SDK. Every
required observation must pass before XR candidate promotion; automated poke
coverage does not create a direct-poke device claim.

## Git installation

When evaluating this package from Git, explicitly pin Core, Unity authoring,
UGUI, and XR to the same full repository commit SHA in the consumer manifest.
The internal `com.lingkyn.*` dependency versions describe compatibility, but they
are not resolved from a public scoped registry by an XR-only Git URL.
