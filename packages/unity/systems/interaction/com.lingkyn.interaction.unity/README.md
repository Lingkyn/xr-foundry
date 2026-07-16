# Lingkyn Interaction Unity

Status: implemented checkpoint candidate; catalog admission requires exact-consumer evidence.

This package is the thin Unity Input System adapter for
`com.lingkyn.interaction.core`. It adds reusable ScriptableObject authoring,
deterministic Core conversion, explicit `InputActionReference` bindings, typed
callback/value conversion, binding display data, advisory binding suggestions,
and serializable per-player override records.

## Authoring graph

Create independent Intent, Route, Context, optional Binding Suggestion, and
Registry assets. A Route must reference an Intent and an `InputActionReference`;
the Registry owns the admitted lists. `InteractionAuthoringConverter.Convert`
returns a frozen Core registry plus stable action-GUID route bindings. Validation
issues include the asset name, field/index path, subject, and error code.

## Runtime boundary

`InputSystemSignalAdapter` maps only Input System `started`, `performed`, and
`canceled` phases and button/scalar/Vector2/Vector3 values. The caller supplies an
explicit `InputObservationStamp`, timestamp, source, modality, capability, and
ordered route candidates. The adapter never infers callback ordering, modality,
or capability from asset order, action names, or control paths.

`InputBindingDisplayService` exposes all action bindings by stable GUID.
`InputBindingOverrideService` captures, validates, serializes, and applies
override records without modifying authored ScriptableObjects. It validates the
complete snapshot before replacing existing overrides.

## Non-claims

- No scene search, product command invocation, renderer, rebinding UI, prompt,
  localization, persistence provider, XRI, OpenXR, vendor SDK, or device support.
- Binding suggestions are inert data; they do not activate routes or override
  player choices.
- Automated Editor evidence does not prove keyboard, controller, hand, gaze,
  assistive, headset, comfort, latency, or accessibility behavior.
