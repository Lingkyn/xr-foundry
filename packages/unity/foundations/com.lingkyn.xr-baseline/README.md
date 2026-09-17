# com.lingkyn.xr-baseline

Vendor-neutral XR greybox runtime and editor helpers for Sandbox rigs, props,
configuration, interaction affordances, and smoke-build setup.

Status: **incubating**. Exact automated validation and immutable-consumer evidence
are recorded in the repository catalogs and compatibility profiles. Real-device
evidence remains required for every headset behavior claim.

## Quick start

1. Install the package and import the current XR Interaction Toolkit Starter
   Assets sample into the consumer project.
2. Run `Tools > Lingkyn > XR Baseline > Initialize Sandbox`. The menu creates or
   opens `Assets/_Project/Scenes/Sandbox.unity`, a `Scene_Root` hierarchy, the
   greybox materials and props, the `VrBaselineConfig` asset, and an
   `XROriginRig` under `_Actors/Player` when a Starter Assets rig source resolves.
3. Edit the generated `VrBaselineConfig` asset, then run `Apply Config` to push
   material, hover, lighting, ray, and locomotion tuning back into the Sandbox.
4. Run `Apply Smoke Build Settings` before a headset smoke build. It makes the
   Sandbox the only build scene; restore the consumer's normal Build Settings
   afterwards.

`Enable Continuous Move` is a separate opt-in that turns stick locomotion back on
for the active Sandbox rig and records the choice in the config asset.

## Public surface

| Area | Types |
| --- | --- |
| Runtime configuration | `VrBaselineConfig` (`Lingkyn/XR Baseline Config` asset) for greybox colors, hover visuals, Sandbox lighting, interaction rays, and locomotion |
| Runtime conventions | `VrBaselineProjectPaths` and `VrBaselineVisualPaths` constants for the generated `Assets/_Project` layout, materials, props, and scene object names |
| Runtime interaction | `XrRigAnchor` marker on the rig root; `GrabbableHoverVisual` hover glow that reads `XRGrabInteractable.isHovered` through reflection so the runtime assembly needs no XRI reference |
| Editor menu | `XrBaselineMenu` with the four `Tools/Lingkyn/XR Baseline` items plus a batch-mode `InitializeFromCommandLine` entry |
| Editor setup tools | `GenericXrRigFactory`, `XrCameraTrackingRepair`, `XrRigInteractionRepair`, `XrLocomotionSetupTool`, `XrContinuousMoveSetupTool`, `VrSmokeBuildSettings`, and the `VrBaseline*Setup` asset, scene, lighting, enclosure, and affordance helpers |

Namespaces start with `Lingkyn.Unity.XrBaseline`. Generated paths are consumer
conventions under `Assets/_Project`; a consumer adapter may wrap them without
changing the package identity or dependencies.

## Non-goals

- No vendor SDK, platform loader, or provider-specific API.
- No product UI, gameplay, release build configuration, or consumer assembly
  reference.
- No proof of 6DoF tracking, controller input, grab, teleport, comfort, or any
  other headset behavior. EditMode tests and independent compile evidence prove
  only that the package resolves and that its defaults stay consumer-neutral.

See [Documentation~/index.md](Documentation~/index.md) and the `XR Sandbox` sample.
