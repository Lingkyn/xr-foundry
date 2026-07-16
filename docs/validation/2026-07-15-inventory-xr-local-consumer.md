# Inventory XR independent local-consumer receipt

Date: 2026-07-15  
Package: `com.lingkyn.inventory.xr@0.1.0`  
Unity: `6000.3.19f1`  
Status: local automated gates passed; immutable Git and device gates remain open

## Consumer boundary

The package was installed into a separate, persistently reused Unity validation project by
local `file:` reference. The consumer explicitly supplied its camera and
EventSystem; the package supplied no XR Origin, provider SDK, platform settings,
or consumer scene.

## Results

- EditMode: **38/38 passed** across the installed Inventory family, including
  world-space prefab structure, nested-prefab provenance, fail-closed input
  validation, physical-scale profile application, and non-head-locked placement.
- PlayMode: **6/6 passed** across UGUI and XR.
  - A registered `IUIInteractor` routed through the real `XRUIInputModule` and
    `TrackedDeviceGraphicRaycaster` to distinct left, center, and right shipped
    slot prefabs.
  - A real `XRPokeInteractor` approached over multiple frames, crossed the XRI
    poke threshold, released, and activated the intended shipped center slot.
  - Invalid or duplicate input configuration closed only the local Inventory
    surface; it did not rewrite consumer input globally.
- Package Manager discovered and imported the declared **World-Space Inventory**
  sample.
- The imported sample compiled and its setup command passed with exactly one
  `XRUIInputModule`, a bound consumer Main Camera, an enabled local interaction
  gate, and a world pose that did not follow later camera movement.

## Claim boundary

This receipt proves local package shape, compilation, UPM sample import, and
Editor/PlayMode interaction routes. It does **not** prove Android build behavior,
headset installation, controller hardware behavior, stereo readability, scale,
angle, reach, occlusion, targeting stability, interaction-state visibility, or
comfort. Those claims remain gated by an immutable Git consumer and a named Pico
device receipt.
