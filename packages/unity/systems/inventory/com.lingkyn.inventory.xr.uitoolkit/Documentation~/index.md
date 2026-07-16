# Inventory XR UI Toolkit composition

## Scene contract

The reusable surface owns a `UIDocument`, `InventoryDocumentView`, and explicit
`BoxCollider`, all on the same GameObject. The collider is a finite, non-zero
trigger whose center and local size exactly match the profile. Its
`PanelSettings` uses `ColliderUpdateMode.Keep` and `colliderIsTrigger=true`.
A consumer scene owns the XR rig and provider. It must contain exactly one
active `XRUIToolkitManager` and at least one active XRI component implementing
both `IXRInteractor` and `IUIInteractor`, with UI interaction enabled.

An EventSystem is optional for a UI-Toolkit-only scene. If one exists (for
example, because UI Toolkit and uGUI coexist), add exactly one active
`PanelInputConfiguration`, set redirection to `Never`, and enable world-space
input. Its interaction layer mask includes the surface layer. Its finite,
positive `maxInteractionDistance` is a separate interaction budget—not the
surface placement distance—and must reach the collider from an active UI
interactor. Do not enable `UIInputModule.bypassUIToolkitEvents`.

Call `ApplyProfile`, place the surface beneath a world anchor or call
`PlaceInFrontOf` once, then call `Revalidate`. Until the report is valid, the
surface collider is disabled and its renderer adapter suppresses semantic
intents.

## Device evidence boundary

EditMode and PlayMode tests verify asset bindings, placement invariants,
configuration gates, and the presence/static reach of a real enabled
`XRRayInteractor`. They do not dispatch a synthetic XR pointer activation.
XR hover/activation, controller, hand, poke, scroll, physical scale,
readability, occlusion, operational reach, and comfort remain unverified until
an immutable-consumer or device receipt names the exact commit, build, runtime,
device, input path, steps, and observations.
