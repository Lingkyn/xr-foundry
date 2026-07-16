# Semantic Interaction architecture contract

## Invariants

1. An `IntentId` names application meaning such as `inventory.open` or
   `ui.confirm`; a key, button, control path, gesture, device, localized label,
   scene object, or platform action name is never the identity.
2. A physical or platform adapter emits a typed source signal. Only an admitted
   route may translate that signal into a semantic intent event.
3. Intent definitions declare value kind and required capabilities. A route with
   an incompatible value or capability fails closed before dispatch.
4. `RouteId` is globally unique and one route belongs to exactly one context.
   Contexts are explicit, independently activated, and priority ordered.
5. One physical observation may nominate multiple route candidates. A
   higher-priority active context shadows lower-priority candidates only inside
   that observation; equal-priority candidates fail closed as ambiguous.
6. Phases are neutral lifecycle facts: `started`, `performed`, and `canceled`.
   Adapter-specific phases may map into them but cannot silently add a performed
   outcome or erase cancellation.
7. One submitted frame is immutable. Given the same registry, active contexts,
   policy snapshot, prior routing-state snapshot, and ordered observations,
   routing, diagnostics, and next routing state are deterministic. Core does not
   invent an order for events an adapter did not order or hide mutable state in
   the router.
8. Per-player binding overrides refer to stable intent and adapter route IDs.
   Adapter-specific control paths remain opaque adapter data and are never Core
   action identity.
9. A binding suggestion has its own stable identity and is advisory. It can
   describe a proposed route through opaque adapter data, but cannot activate a
   route, override a user choice, or prove that a device supports the binding.
10. Policy can replace hold with toggle, alter thresholds, or disable a route
   without renaming the intent. Settings supplies reviewed policy snapshots; it
   does not mutate the interaction registry or own physical bindings.
11. A successful route means a handler received a semantic event. It does not
    prove the game action succeeded, the interaction was comfortable, or a device
    supports the modality. The handler returns an explicit outcome.
12. Source modality, capability, interaction profile, and named device are
    different facts. Evidence never transfers between keyboard, gamepad,
    tracked-controller, hand, gaze, voice, assistive, simulated, or unknown
    sources.
13. Core imports no Unity, Input System, XRI, OpenXR, renderer, vendor, scene, or
    device API.

## Core concepts

| Concept | Responsibility |
| --- | --- |
| `IntentId` | Validated stable application-intent identity. |
| `ContextId` | Stable identity for an independently activated action context. |
| `RouteId` | Stable adapter-owned route identity used for admission, conflicts, and persisted overrides. |
| `SourceId` | One observed source instance without assuming a vendor or device class. |
| `ObservationSequence` | Non-negative frame-local identity grouping route candidates derived from one physical observation. |
| `BindingSuggestionId` | Stable identity for one advisory default-binding proposal. |
| `InteractionModality` | Declared class such as keyboard/mouse, gamepad, touch, tracked controller, articulated hand, gaze, voice, assistive, simulated, or unknown. |
| `InteractionCapability` | Small composable facts such as digital, scalar, vector2, vector3, pose, pointing, haptic-output, or text. |
| `InteractionValue` | Closed typed value union; no `object` payload. |
| `InteractionPhase` | Neutral `started`, `performed`, or `canceled` lifecycle fact. |
| `IntentDefinition` | Value kind, required capabilities, dispatch order, and non-localized metadata keys. |
| `InteractionContext` | Priority-ordered set of intent routes activated as one unit. |
| `InteractionRoute` | Intent/context/source-selector association plus an adapter-owned opaque binding descriptor. |
| `BindingSuggestion` | Intent/route association plus an adapter kind and opaque proposed binding; it is inert until a consumer admits a route or override. |
| `InteractionPolicySnapshot` | Immutable per-intent activation alternatives and route availability supplied by a consumer. |
| `SourceSignal` | Adapter-provided route, source, value, phase, timestamp, and ingress sequence. |
| `InteractionFrame` | Immutable ordered source signals submitted for one routing boundary. |
| `InteractionRoutingState` | Immutable pending-phase timestamps and toggle facts supplied as prior state and returned as next state. |
| `SemanticInteractionEvent` | Validated intent event delivered to a consumer handler. |
| `InteractionDispatchResult` | Routed, canceled, shadowed, rejected, ambiguous, or handler outcome with diagnostics. |

## Context and conflict flow

1. Freeze the registry, active-context set, policy snapshot, prior routing state,
   and ordered frame.
2. Validate source, route, phase, value kind, capabilities, and monotonic ingress
   and observation sequence for every signal. Signals sharing one observation
   must carry identical physical facts.
3. Resolve only globally unique routes in active contexts.
4. Within one observation, retain the highest-priority route candidate. If more
   than one candidate remains at that priority, reject the observation as
   ambiguous. Never compare candidates from different observations.
5. Preserve observation and ingress sequence, then use declared order and stable
   IDs only as deterministic tie breakers.
6. Emit `started` and `canceled` lifecycle events without treating them as a
   completed action. Dispatch `performed` to the matching handler.
7. Record the handler's accepted, rejected, deferred, or failed outcome without
   rewriting the input history, and return the immutable next routing state.

OpenXR action sets and Unreal mapping contexts establish useful context and
priority patterns, but the Core does not copy either runtime's suppression rules.
The fail-closed equal-priority collision rule is an XR Foundry safeguard and must
be proven directly by tests.

## Value, source, and modality boundary

The first value union supports button, scalar, vector2, vector3, pose, and text.
Pose uses engine-light numeric structs and explicit validity flags; it is not an
OpenXR space, Unity `Pose`, raycast hit, hand skeleton, gaze target, or proof of
tracking quality.

A modality is descriptive evidence metadata, not routing permission. Capabilities
are admitted separately. For example, a tracked controller may expose digital,
scalar, vector2, pose, pointing, and haptic-output capabilities, while a gaze
source may expose pose or pointing but no activation capability. A gaze direction
alone must never be converted into `performed` without a separately admitted
activation route and matching platform/device evidence.

## Accessibility and Settings boundary

XAG 107 supports action-level remapping, alternative digital routes, cancellation,
and alternatives to prolonged, repeated, simultaneous, motion, or speech input.
The Core therefore preserves intent identity across routes and exposes immutable
policy alternatives. It does not claim that every title must implement every
alternative or that an exposed policy is usable or compliant.

Settings may persist a selected activation policy, sensitivity, inversion, or
binding override through public ports. Interaction validates and applies the
policy. Neither family owns a rebinding UI, localized prompts, platform assistive
technology, or device certification.

Hold duration and activation threshold are explicit, validated policy values.
Toggle state is explicit routing state: the first admitted activation dispatches
the new value, and cancellation clears only its pending phase rather than an
existing toggle choice. Every subsequent admitted activation follows the same
dispatch rule and emits the newly toggled value.

## Unity adapter

The first Unity adapter may provide ScriptableObject intent/context/route
authoring; deterministic Core conversion; `InputActionReference` mapping;
started/performed/canceled and typed-value conversion; binding-display and
binding-suggestion/override ports; and asset-specific validation.

It must not search scenes, directly invoke product gameplay, store mutable player
choices in authored assets, assume callback order for same-update actions, hard-
code one control layout, depend on XRI/OpenXR/vendor SDKs, or infer support for a
modality from a control path string. XRI, OpenXR interaction profiles, tracked
ray, hand, and gaze remain separately installable and separately evidenced future
adapters.

The adapter assigns observation sequence and verifies that all route candidates
in one observation carry identical source, modality, capability, value, phase,
and timestamp facts before Core routing.
