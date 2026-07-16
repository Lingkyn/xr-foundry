# Inventory XR immutable Git-consumer receipt

Date: 2026-07-15  
Package: `com.lingkyn.inventory.xr@0.1.0`  
Source revision: `55b419bb010c253ce130a09e80652eba9744424b`  
Unity: `6000.3.19f1`  
Status: immutable Git package and automated consumer gates passed

## Immutable installation

A separate, persistently reused Unity validation project pinned all four Inventory
packages to the same full repository revision:

- `com.lingkyn.inventory.core`
- `com.lingkyn.inventory.unity`
- `com.lingkyn.inventory.ugui`
- `com.lingkyn.inventory.xr`

The generated Package Manager lock recorded `source: git` and the exact full hash
for every package. No Inventory package resolved from a local `file:` reference.

## Results

- Package Manager discovered and re-imported the XR package's declared
  **World-Space Inventory** sample from the immutable Git package.
- The imported sample compiled and its setup validation passed with a world-space
  Canvas, enabled local fail-closed gate, bound consumer camera, exactly one active
  `XRUIInputModule`, and non-head-locked placement.
- EditMode: **38/38 passed**.
- PlayMode: **6/6 passed**, including distinct left/center/right tracked-ray hits
  and the real `XRPokeInteractor` press/release route.

## Claim boundary

This closes the immutable Git-consumer gate for the XR adapter source revision.
It does not close Android build/install/open or real Pico evidence. No headset
readability, controller-hardware, targeting-stability, scale, angle, reach,
occlusion, interaction-state visibility, or comfort claim is promoted here.
