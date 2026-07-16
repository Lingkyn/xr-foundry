# Semantic Interaction source coverage matrix

Only admitted positive sources participate in derivation.

| Capability | Positive sources | Foundry decision | Boundary |
| --- | --- | --- | --- |
| Semantic action identity | `openxr-1.1-actions`, `unity-input-system-1.14-actions`, `epic-enhanced-input-5.8` | Stable intent IDs are separate from device controls and physical bindings. | No source API or naming convention is copied. |
| Context activation and priority | `openxr-1.1-actions`, `epic-enhanced-input-5.8` | Contexts activate as units; higher priority shadows lower priority; equal-priority conflicts fail closed. | Foundry collision behavior requires its own tests. |
| Typed values and phases | `openxr-1.1-actions`, `unity-input-system-1.14-actions`, `epic-enhanced-input-5.8` | Closed value union and neutral started/performed/canceled lifecycle. | Platform-specific phases and conversions do not become universal facts. |
| Multiple bindings and rebinding | `unity-input-system-1.14-bindings`, `microsoft-xag-input-107` | Stable intent, route, and advisory binding-suggestion IDs support multiple routes and non-destructive binding overrides. | Core stores no Unity control path or platform layout; a suggestion is inert until admitted. |
| Input accessibility policy | `microsoft-xag-input-107`, `apple-hig-spatial-interaction` | Action-level alternatives, cancellation, toggle/hold policy, and alternative routes remain possible without renaming intents. | No compliance or feature-support claim. |
| Controller, hand, and gaze boundaries | `openxr-1.1-actions`, `openxr-1.1-interaction-profiles`, `apple-hig-spatial-interaction` | Modality, capability, interaction profile, and named device are recorded independently. | One modality's test never proves another; gaze does not imply activation. |
| Unity adapter | `unity-input-system-1.14-actions`, `unity-input-system-1.14-bindings`, `unity-xri-action-based-guidance` | Use action identity and callbacks through a thin adapter; keep XRI/OpenXR outside the base Unity adapter. | No scene, XRI, runtime, or headset behavior is claimed. |
| Comfort and ergonomics | `apple-hig-spatial-interaction`, `microsoft-xag-input-107` | Repetitive, prolonged, simultaneous, path-based, direct, and large gestures require explicit alternatives and device evaluation when claimed. | No numeric comfort threshold or universal gesture prescription. |

## Deferred coverage

- renderer and target acquisition;
- object selection, manipulation, locomotion, teleport, and UI navigation;
- concrete rebinding screen and conflict-resolution UX;
- haptic waveform/output adapters;
- XRI and OpenXR runtime adapters;
- hand skeleton/gesture recognition and eye-gaze permission/targeting;
- voice recognition, assistive technology, and switch-access adapters; and
- named-device performance, usability, accessibility, or comfort evidence.
