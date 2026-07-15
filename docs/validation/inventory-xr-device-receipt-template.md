# Inventory XR Device Acceptance Receipt

Use this checklist only after an immutable package revision has produced an exact
Android APK. It is a real-device acceptance gate, not an Editor, simulator, or
PlayMode checklist.

The package family is provider-neutral and this receipt is renderer-neutral. Each
completed receipt selects exactly one renderer-specific XR composition:

- `com.lingkyn.inventory.xr.ugui` with `package.renderer` set to `ugui`; or
- `com.lingkyn.inventory.xr.uitoolkit` with `package.renderer` set to
  `uitoolkit`.

The first acceptance profile is deliberately device-specific:
`pico_tracked_controller_v1`. A PICO pass proves only the selected renderer, named
headset, firmware, runtime stack, APK, package revision, and input modality recorded
in that receipt. It does not prove the sibling renderer, every OpenXR device, or
every input modality.

PICO's official Unity documentation separates project configuration validation
from runtime use, and PICO release notes record device-system-specific interaction
limitations. Unity's XR Interaction Toolkit likewise supplies the interaction
route, but it cannot establish binocular readability, targeting stability, or
comfort on a physical headset. These boundaries are why the device gate cannot be
replaced by a visible Game view or automated test.

## Prepare the receipt

1. Read `inventory-xr-device-receipt.schema.json`, then copy
   `inventory-xr-device-receipt.template.json` to a dated receipt file.
2. Choose one supported XR package ID and set `package.renderer` to its matching
   renderer. Never combine evidence for both renderers in one receipt.
3. Replace every placeholder and all-zero hash with the exact tested values.
4. Use public repository-relative paths, immutable URLs, or artifact identifiers
   for evidence references. Do not publish local machine paths, consumer names,
   private scenes, credentials, or internal issue links.
5. Confirm that the APK SHA-256 and the 40-character package revision match the
   artifact installed on the headset.
6. Record both used and unused runtime components. For example, write `not_used`
   instead of omitting OpenXR or a vendor integration version.

Human device evidence may be deferred while repository infrastructure, automated
tests, immutable installation, or another renderer route continues. Leave every
check as `not_tested`, every claim boundary `false`, and `overall_result` as
`not_run` until a person runs the profile. Deferral does not fail the repository
contract, but it keeps the selected renderer/device claim and candidate promotion
pending.

## Run the PICO tracked-controller profile

Perform the following on the named PICO headset with its normal tracked
controllers. Record a concrete observation and at least one evidence reference for
every required check.

1. Install the exact APK, launch it, and open the sample belonging to the selected
   Inventory XR renderer package.
2. Read the surface through both eyes. Confirm text and state changes are legible,
   not doubled, clipped, or blurred beyond normal headset optics.
3. Turn the head left/right and lean laterally. Confirm the Inventory remains at
   its world pose and does not follow the head.
4. With the left controller, hold the ray on the left, center, and right target for
   about one second each, then press each target twice.
5. Repeat the same hover and press sequence with the right controller.
6. Confirm each press affects only the intended target: no miss, neighboring
   target, or duplicate activation.
7. Confirm hover, selected, and disabled states are visibly distinct. Press the
   disabled target and confirm its counter/state does not mutate.
8. Inspect scene occlusion, panel scale, facing angle, and controller reach. A
   preference-only adjustment may be scheduled separately only when the current
   result is already readable, reachable, stable, and comfortable.
9. Remain in the sample and interact normally for at least two minutes. Record any
   eye strain, neck/arm strain, nausea, instability, or discomfort.

Direct poke is an optional, separate device claim. Automated `XRPokeInteractor`
coverage does not authorize it. Set `direct_poke_device_claim_allowed` only when
the optional `direct_poke_device` check was actually performed and passed on the
named headset.

## Pass rule

Every required check must be `pass`; `partial`, `fail`, and `not_tested` all block
candidate promotion for the selected XR renderer/device tuple. `install_result`,
`open_result`, and `overall_result` must also be `pass`, the session must last
at least 120 seconds, and all required device claim flags must be true. A passing
receipt establishes only that exact tuple's device gate; candidate promotion still
requires every earlier package and consumer gate plus a reviewed maturity-ledger
update. The sibling renderer requires its own receipt.

Validate a completed receipt with:

```powershell
python scripts/validate_repository.py --device-receipt docs/validation/<receipt>.json --json
```

This command validates completeness and claim boundaries. It does not independently
prove that a human performed the observations; reviewer-visible evidence remains
required.

## Positive public references

- [PICO Project Validation](https://developer.picoxr.com/llmstxt/document/unity-integration/en/en_project-validation.md)
- [PICO controller ray and World Space Canvas](https://developer.picoxr.com/llmstxt/document/unity-integration/en/en_create-interactive-ui.md)
- [PICO Build and run the scene](https://developer.picoxr.com/llmstxt/document/unity-integration/en/en_build-and-run-the-scene.md)
- [PICO controller and HMD input mapping](https://developer.picoxr.com/llmstxt/document/unity-integration/en/en_input-mapping.md)
- [PICO Unity OpenXR SDK release notes](https://developer.picoxr.com/document/updates-unity-openxr/)
- [Unity XR Interaction Toolkit](https://docs.unity3d.com/Manual/com.unity.xr.interaction.toolkit.html)
