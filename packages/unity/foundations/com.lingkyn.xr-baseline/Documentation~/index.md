# Lingkyn XR Baseline

`com.lingkyn.xr-baseline` provides vendor-neutral XR Sandbox helpers without
depending on a consuming game's assemblies or product content.

## Evaluate locally

```json
"com.lingkyn.xr-baseline": "file:../../xr-foundry/packages/unity/foundations/com.lingkyn.xr-baseline"
```

Import the current XR Interaction Toolkit Starter Assets sample, then run:

```text
Tools/Lingkyn/XR Baseline/Initialize Sandbox
Tools/Lingkyn/XR Baseline/Apply Smoke Build Settings
```

The package creates generic greybox assets/configuration, a Sandbox hierarchy, and a
rig when a compatible Starter Assets source is available. Vendor SDK paths, platform
loaders, product UI/gameplay, and release configuration remain consumer-owned.

Editor/PlayMode and independent compile evidence do not prove headset behavior. Test
6DoF, controller input, grab, teleport, comfort, and other device claims on the target
headset before marking them stable.
