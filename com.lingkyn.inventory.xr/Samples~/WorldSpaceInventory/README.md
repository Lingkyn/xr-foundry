# World-Space Inventory sample

Import this sample, then use:

`Tools > XR Foundry > Inventory > Create World-Space Inventory Sample`

The command requires an existing consumer Main Camera. It creates an EventSystem
with `XRUIInputModule` only when none exists; it refuses to repair an existing
missing, duplicate, disabled, or incompatible input-module setup. It instantiates
the shipped surface at a one-time world pose and never parents it to the camera.

The sample replays neutral left/center/right Inventory states. It does not create an
XR Origin, configure OpenXR, include a platform SDK, or prove headset comfort.
