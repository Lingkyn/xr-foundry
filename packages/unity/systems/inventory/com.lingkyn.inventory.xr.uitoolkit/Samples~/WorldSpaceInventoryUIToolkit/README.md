# World-Space Inventory UI Toolkit

After importing the sample, run **GameObject > XR Foundry > Inventory > Create
World-Space UI Toolkit Sample**.

The explicit command creates:

- one world-root Inventory `UIDocument`, `InventoryDocumentView`, trigger
  `BoxCollider`, fail-closed surface, and replay bootstrap;
- one scene-level `XRUIToolkitManager` only when none exists; and
- one `PanelInputConfiguration` only when an EventSystem exists and no panel
  configuration exists. With exactly one configuration, setup includes the
  surface layer and writes the profile's explicit maximum interaction distance.

It never creates an XR Origin, Camera, EventSystem, provider, input actions,
interactor, or vendor SDK. Add at least one active consumer-owned XRI
`IUIInteractor`/`IXRInteractor` (for example, an `XRRayInteractor` with
`enableUIInteraction=true`). Existing duplicate or incompatible global objects
are left visible to validation instead of being silently deleted or replaced.

The sample is an Editor configuration and state-replay aid. A successful gate
proves that a real enabled XRI UI interactor is present and statically in range;
it does not prove XR hover/activation or headset rendering, controller, hand,
poke, scroll, readability, scale, angle, occlusion, or comfort on any device.
