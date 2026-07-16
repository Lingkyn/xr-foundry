# Lingkyn Inventory XR UGUI

Status: **incubating**. Exact automated validation and immutable-consumer evidence
are recorded in the repository catalogs and compatibility profiles. Android and
renderer-scoped named-device evidence remain separate promotion gates.

This optional, renderer-explicit package composes `com.lingkyn.inventory.ugui` on a world-space
Canvas using XR Interaction Toolkit `3.5.1`. It does not redefine Inventory
domain types and does not add XR dependencies to Core, Unity authoring, or UGUI.

The package/dependency ID is `com.lingkyn.inventory.xr.ugui`; its runtime assembly
and namespace are renderer-explicit as `Lingkyn.Inventory.XR.UGUI`. The Git UPM
subfolder selector is a separate repository concern:

```text
?path=/packages/unity/systems/inventory/com.lingkyn.inventory.xr.ugui
```

The shipped `InventoryWorldSpaceSurface` prefab contains:

- a world-space Canvas, fail-closed CanvasGroup, and configurable physical scale;
- `TrackedDeviceGraphicRaycaster`;
- the nested UGUI `InventoryShell`; and
- `InventoryWorldSpaceSurface` validation and placement bindings.

It deliberately contains no Camera, XR Origin, EventSystem, input module, OpenXR
provider, PICO SDK, scene, or head-follow component. A consumer scene must have
exactly one active EventSystem with exactly one active `XRUIInputModule` whose
`enableXRInput` gate is enabled, plus at least one active XRI UI interactor with
UI interaction enabled and a layer/range route that can reach this Canvas. Missing,
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

The package test contract covers structure, strict scene-route validation, a real
`XRRayInteractor` aimed at distinct shipped slots, a real `XRPokeInteractor`
release route, and non-head-locked behavior. These tests become evidence only when
they pass in the recorded immutable consumer. Only real headset evidence can prove controller
usability, readability, scale, angle, occlusion, reach, or comfort.

Use the repository's
[`Public Device Lab V1`](https://github.com/Lingkyn/xr-foundry/blob/main/docs/device-lab/README.md)
and
[`Inventory world-space UI plan`](https://github.com/Lingkyn/xr-foundry/blob/main/docs/device-lab/test-plans/inventory-world-space-ui-v1.json)
for the named-device gate, then validate the completed generic receipt with
`--device-lab-receipt`. The first admitted profile covers a PICO headset with
tracked controllers while keeping the package itself independent of a vendor SDK.
Every required observation must pass before XR candidate promotion; automated
poke coverage does not create a direct-poke device claim.

## Git installation

When evaluating this package from Git, explicitly pin Core, Presentation, UGUI,
and XR UGUI to the same full repository commit SHA in the consumer manifest.
The internal `com.lingkyn.*` dependency versions describe compatibility, but they
are not resolved from a public scoped registry by an XR-only Git URL.
