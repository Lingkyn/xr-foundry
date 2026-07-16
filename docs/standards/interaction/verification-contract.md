# Semantic Interaction verification contract

## Source gate

- Derivation inputs are positive, public, role-bounded, and license/terms aware.
- Engine/platform behavior constrains only its matching adapter.
- Accessibility guidance supplies barrier and alternative-route coverage, not a
  compliance claim or mandatory universal feature set.
- No consumer project, private prototype, rejected candidate, or prior improvised
  input code is derivation material.

## Core gate

Deterministic tests cover:

- invalid/default/duplicate intent, context, route, source, and binding-suggestion
  identities;
- definition/value/capability mismatch and non-finite numeric/pose data;
- inactive context rejection, higher-priority shadowing, and equal-priority
  collision diagnostics;
- ordered ingress validation and stable routing/diagnostic order;
- started, performed, canceled, duplicate, and invalid phase transitions;
- multi-route and multi-modal sources without transferring evidence;
- immutable registry, frame, policy, route, event, and diagnostic collections;
- cancellation before completion and explicit handler accepted/rejected/deferred/
  failed outcomes;
- rebinding-compatible stable identity using opaque adapter route tokens; and
- inert binding suggestions that cannot activate routes, override user choices,
  or grant device evidence; and
- policy alternatives such as momentary/toggle/hold without rewriting intent IDs.

Tests must assert emitted events, suppressed events, outcomes, diagnostics, active
context snapshot, handler calls, and sequence values independently. One boolean
success assertion is insufficient.

## Unity adapter gate

- ScriptableObject definitions, contexts, and routes convert deterministically to
  Core without mutating authored assets.
- Validation reports asset/index/intent/context/route-specific errors.
- `InputActionReference` identity is mapped explicitly; no scene search or
  name-only lookup is required.
- Button, scalar, vector2, vector3, started, performed, and canceled conversion is
  covered. Unsupported or mismatched values fail closed.
- Multiple bindings, binding display, binding suggestions, and override
  serialization remain adapter seams; no runtime rebinding UI is claimed.
- Same-update callback order is captured explicitly by the adapter rather than
  inferred from asset order.

## Exact-consumer gate

- Install Core and Unity packages from the same immutable full commit SHA.
- Record Unity, Input System, OS, dependency lock, package versions, testable
  assemblies, and machine-readable results.
- Compile both packages and run all applicable tests in a clean consumer.

## Modality and Device Lab gate

Automated Core/Unity evidence proves no keyboard, mouse, gamepad, controller,
tracked-ray, hand, gaze, voice, assistive, OpenXR, XRI, renderer, comfort, or
headset behavior. Each claimed adapter/modality tuple needs its own dependency,
runtime, input-source, artifact, and named-device receipt. Simulated input is
recorded as simulated and cannot become real-device evidence.

## Claim ceiling

Passing the first package gates proves only the named semantic-routing and Unity
Input System authoring/conversion tuple. It does not prove gameplay correctness,
input latency, rebinding UX, localization, prompts, haptics, target acquisition,
locomotion, manipulation, accessibility, comfort, platform certification, or
device support.
