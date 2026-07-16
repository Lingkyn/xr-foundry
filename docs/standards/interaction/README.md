# Semantic Interaction family standard

This standard separates what a person intends to do from the keyboard key,
controller control, tracked pose, gesture, gaze, speech route, renderer, runtime,
or device that may express it. It is a reusable routing and evidence substrate,
not a complete input framework, rebinding screen, interaction technique, or
headset integration.

## Planned package boundary

| Layer | Owns | Does not own |
| --- | --- | --- |
| Engine-light Core | Stable intent/context/route/source/binding-suggestion identities, observation grouping, typed values, phases, capability requirements, route admission, context priority, immutable prior/next routing state, conflict diagnostics, deterministic frame routing, cancellation, outcomes, and policy snapshots | Physical control paths, Unity/OpenXR types, polling, UI, locomotion, object manipulation, device discovery, platform settings |
| Unity Input System adapter | ScriptableObject authoring, Input Action identity mapping, Input System phase/value conversion, binding-display and override seams, and actionable validation | Product actions, automatic scene search, XRI interactors, OpenXR profiles, vendor SDKs, runtime rebinding UI, named-device claims |

Future tracked-ray, hand, gaze, voice, assistive-input, XRI, OpenXR, or vendor
adapters are separate evidence boundaries. The Core can describe their source
modality and required capabilities without claiming that any implementation or
device supports them.

The admitted Settings family can provide an immutable policy snapshot through a
public port for activation alternatives, sensitivity, inversion, or route
selection. That is an integration seam, not a derivation source or permission to
couple semantic intent identity to a Settings package, UI, or persisted key.

No package identifier or directory is admitted until the positive-source gate
receives an independent PASS. Read the [architecture contract](architecture-contract.md),
[verification contract](verification-contract.md), and
[positive-source manifest](source-manifest.json) before implementation or reuse.
