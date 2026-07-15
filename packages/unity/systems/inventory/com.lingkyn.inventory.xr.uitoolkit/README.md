# Lingkyn Inventory XR UI Toolkit

Status: **incubating**. This package composes
`com.lingkyn.inventory.uitoolkit` as a Unity 6.3 world-space `UIDocument` using
XR Interaction Toolkit `3.5.1` support. It contains no XR Origin, Camera,
EventSystem, input actions, OpenXR provider, or vendor SDK.

The runtime composition provides:

- a ScriptableObject profile for document size, physical scale, collider depth,
  interaction distance, and initial placement;
- a world-space `PanelSettings` asset configured to keep an explicit trigger
  collider;
- a same-GameObject structural contract for `UIDocument`,
  `InventoryDocumentView`, and the profile-sized trigger `BoxCollider`;
- a surface that starts interaction closed, applies the profile, and enables
  its collider and semantic UI intents only after validation;
- strict validation for world-space mode, fixed size, bindings, non-head-locked
  placement, one active `XRUIToolkitManager`, at least one active real XRI UI
  interactor with UI interaction enabled, and EventSystem interoperability;
  and
- one-time placement at a world-root pose in front of a supplied Camera. The
  surface never parents to or follows that Camera.

When a scene has an EventSystem, exactly one enabled
`PanelInputConfiguration` must use `PanelInputRedirection.Never` and process
world-space input. Its interaction layers must include the surface layer, and
its finite positive maximum distance must reach the collider from at least one
active UI interactor. Any UI input module must leave
`bypassUIToolkitEvents` disabled. These are Unity/XRI interoperability
requirements, not provider preferences.

The package requires Unity `6000.3.8f1` or newer because the shipped document
uses a `ScrollView` and that patch line contains XRI's documented world-space
drag-scroll support. It makes no claim that a particular headset, controller,
hand, poke, readability, scale, angle, reach, occlusion, or comfort test has
passed. Those require separate named-device evidence.

The public dependency baseline is XRI `3.5.1`. A compile against an older local
XRI API surface may provide compatibility evidence, but it is not a substitute
for resolving and validating this package against the declared `3.5.1`
dependency in an immutable Unity consumer project.

The automated gate and PlayMode test establish component identity,
configuration, static reach, and fail-closed behavior. They do **not** synthesize
an XR pointer click and therefore do not claim hover or activation through the
XR route; those claims remain pending Android and named-device evidence. Exact
automated validation and immutable-consumer evidence are recorded in the
repository catalogs and compatibility profiles.

## Git installation

For Git evaluation, explicitly pin Core, Presentation, UI Toolkit, and XR UI
Toolkit to the same full repository commit SHA. Package manifest dependency
versions express compatibility; they cannot fetch sibling Git packages from this
monorepo automatically.
