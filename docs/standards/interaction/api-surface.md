# Interaction public API surface

Status: inventory for the public API compatibility review. The family is
incubating; nothing in this file is a stability promise. Every entry is derived
from the `Runtime/` sources at the revision that carries this file. Internal and
private members are omitted.

Packages inventoried:

| Package | Version | Assembly (asmdef) | References | Engine references | Dependencies |
| --- | --- | --- | --- | --- | --- |
| `com.lingkyn.interaction.core` | 0.1.0 | `Lingkyn.Interaction.Core` | none | `noEngineReferences: true` | none |
| `com.lingkyn.interaction.unity` | 0.1.0 | `Lingkyn.Interaction.Unity` | `Lingkyn.Interaction.Core`, `Unity.InputSystem` | `noEngineReferences: false` | `com.lingkyn.interaction.core` 0.1.0, `com.unity.inputsystem` 1.14.2 |

`Lingkyn.Interaction.Core` declares `InternalsVisibleTo("Lingkyn.Interaction.Core.Editor.Tests")`.
The Unity assembly declares no `InternalsVisibleTo`.

Consumer-facing: `yes` means a consumer calls or reads it directly; `seam` means
a consumer implements or extends it; `no` means it exists to support another
public member.

## `com.lingkyn.interaction.core` (namespace `Lingkyn.Interaction.Core`)

46 public types. `SemanticIdentityValidator`, `InteractionReadOnly` and
`InteractionRoutingStateBuilder` are internal and not part of the surface.

Identity structs (`IntentId`, `ContextId`, `RouteId`, `SourceId`,
`BindingSuggestionId`) share one shape: readonly struct, `IEquatable<T>`,
`IComparable<T>`, prop `Value`, static `TryCreate(string)` returning
`InteractionResult<T>`, `Equals`, `GetHashCode`, `CompareTo`, `ToString`.
Validation is shared: lower-case ASCII letters, digits, `.`, `_`, `-`; no empty
segments; not `default`; at most 128 characters.

| Type | Kind | Public members | Consumer-facing |
| --- | --- | --- | --- |
| `IntentId`, `ContextId`, `RouteId`, `SourceId`, `BindingSuggestionId` | readonly structs | as described above | yes |
| `InteractionCapability` | `[Flags]` enum | `None = 0`, `Digital = 1`, `Scalar = 2`, `Vector2 = 4`, `Vector3 = 8`, `Pose = 16`, `Pointing = 32`, `HapticOutput = 64`, `Text = 128` | yes |
| `InteractionModality` | enum | `Unknown = 0`, `KeyboardMouse`, `Gamepad`, `Touch`, `TrackedController`, `ArticulatedHand`, `Gaze`, `Voice`, `Assistive`, `Simulated = 9` (explicit values) | yes |
| `InteractionPhase` | enum | `Started = 0`, `Performed = 1`, `Canceled = 2` | yes |
| `InteractionValueKind` | enum | `Button = 0`, `Scalar`, `Vector2`, `Vector3`, `Pose`, `Text = 5` (explicit values) | yes |
| `InteractionActivationMode` | enum | `Momentary = 0`, `Toggle = 1`, `Hold = 2` | yes |
| `InteractionDispatchStatus` | enum | `Routed = 0`, `Canceled`, `Shadowed`, `Rejected`, `Ambiguous`, `HandlerOutcome = 5` (explicit values) | yes |
| `InteractionHandlerOutcome` | enum | `Accepted = 0`, `Rejected = 1`, `Deferred = 2`, `Failed = 3` | yes (handler return) |
| `InteractionDiagnosticKind` | enum | `ValidationFailure = 0`, `InactiveContext`, `ShadowedRoute`, `AmbiguousRoute`, `DisabledRoute`, `HandlerResult`, `PolicyApplied = 6` (explicit values) | yes |
| `InteractionValidationCode` | enum | `None = 0`, `InvalidIdentity`, `DuplicateIdentity`, `DefaultIdentity`, `InvalidDefinition`, `DuplicateDefinition`, `KindMismatch`, `CapabilityMismatch`, `NonFiniteValue`, `InvalidPose`, `InvalidPhaseTransition`, `DuplicatePhase`, `InactiveContext`, `ShadowedContext`, `AmbiguousContextCollision`, `UnknownRoute`, `UnknownIntent`, `UnknownContext`, `UnknownSource`, `DisabledRoute`, `InvalidIngressSequence`, `InvalidFrame`, `InvalidPolicy`, `InvalidBindingSuggestion`, `HandlerFailed` (implicit values after `None`) | yes |
| `InteractionError` | readonly struct, `IEquatable` | ctor `(InteractionValidationCode, string message, string subject = null)`; props `Code`, `Message`, `Subject`; `Equals`, `GetHashCode`, `ToString` | yes |
| `InteractionResult<T>` | readonly struct | props `Succeeded`, `Value`, `Error`; static `Success(T)`, `Fail(InteractionValidationCode, string, string subject = null)` | yes |
| `InteractionResult` | readonly struct | props `Succeeded`, `Error`; static `Success()`, `Fail(InteractionValidationCode, string, string subject = null)` | yes |
| `InteractionVector2` | readonly struct, `IEquatable` | ctor `(double x, double y)`; props `X`, `Y`; `IsFinite()`; `Equals`, `GetHashCode` | yes |
| `InteractionVector3` | readonly struct, `IEquatable` | ctor `(double, double, double)`; props `X`, `Y`, `Z`; `IsFinite()`; `Equals`, `GetHashCode` | yes |
| `InteractionQuaternion` | readonly struct, `IEquatable` | ctor `(double, double, double, double)`; props `X`, `Y`, `Z`, `W`; `IsFinite()`; `Equals`, `GetHashCode` | yes |
| `InteractionPose` | readonly struct, `IEquatable` | ctor `(InteractionVector3 position, InteractionQuaternion rotation, bool positionValid, bool rotationValid)`; props of the same names; `IsFinite()` (false when neither flag is set); `Equals`, `GetHashCode` | yes |
| `InteractionValue` | readonly struct, `IEquatable` (private ctor) | props `Kind`, `Button`, `Scalar`, `Vector2`, `Vector3`, `Pose`, `Text`; static `FromButton(bool)`, `FromScalar(double)`, `FromVector2`, `FromVector3`, `FromPose`, `FromText(string)`; static `Validate(InteractionValueKind expectedKind, InteractionValue)` returning `InteractionResult<InteractionValue>`; static `RequiredCapabilitiesForKind(InteractionValueKind)` returning `InteractionCapability`; `Equals`, `GetHashCode` | yes |
| `IntentDefinition` | sealed class (internal ctor) | props `Id`, `ValueKind`, `RequiredCapabilities`, `DispatchOrder`, `MetadataKeys`; static `Create(IntentId, InteractionValueKind, InteractionCapability, int dispatchOrder, IEnumerable<string> metadataKeys = null)` returning `InteractionResult<IntentDefinition>` | yes |
| `InteractionRoute` | sealed class (internal ctor) | props `Id`, `ContextId`, `IntentId`, `SourceSelector`, `SourceModality`, `SourceCapabilities`, `RouteOrder`, `OpaqueBindingDescriptor` (`IReadOnlyList<byte>`); static `Create(RouteId, ContextId, IntentId, SourceId sourceSelector, InteractionModality, InteractionCapability, byte[] opaqueBindingDescriptor, int routeOrder)` | yes |
| `InteractionContextDefinition` | sealed class (internal ctor) | props `Id`, `Priority`, `RouteIds`; static `Create(ContextId, int priority, IEnumerable<RouteId>)` | yes |
| `BindingSuggestion` | sealed class (internal ctor) | props `Id`, `IntentId`, `RouteId`, `AdapterKind`, `OpaqueProposedBinding` (`IReadOnlyList<byte>`); static `Create(BindingSuggestionId, IntentId, RouteId, string adapterKind, byte[] opaqueProposedBinding)` | yes |
| `InteractionRegistry` | sealed class (private ctor) | props `Intents`, `Contexts`, `Routes`, `BindingSuggestions` (all sorted deterministically); `TryGetIntent`, `TryGetContext`, `TryGetRoute`, `TryGetBindingSuggestion` (each `(id, out T)`); static `Create(IEnumerable<IntentDefinition>, IEnumerable<InteractionContextDefinition>, IEnumerable<InteractionRoute>, IEnumerable<BindingSuggestion> = null)` returning `InteractionResult<InteractionRegistry>` | yes |
| `IntentPolicyEntry` | readonly struct, `IEquatable` | ctor `(IntentId, InteractionActivationMode, bool enabled, long holdDurationTicks = 0, double activationThreshold = 0.5)`; props of the same names; `Equals`, `GetHashCode` | yes |
| `RoutePolicyEntry` | readonly struct, `IEquatable` | ctor `(RouteId, bool enabled, double sensitivity, bool invert)`; props of the same names; `Equals`, `GetHashCode` | yes |
| `InteractionPolicySnapshot` | sealed class (private ctor) | props `IntentPolicies`, `RoutePolicies`; `TryGetIntentPolicy(IntentId, out IntentPolicyEntry)`; `TryGetRoutePolicy(RouteId, out RoutePolicyEntry)`; static `Create(IEnumerable<IntentPolicyEntry>, IEnumerable<RoutePolicyEntry>)`; static `Empty` | yes |
| `IInteractionPolicyPort` | interface | prop `CurrentPolicy` (`InteractionPolicySnapshot`) | seam |
| `BindingOverride` | sealed class (internal ctor) | props `IntentId`, `RouteId`, `AdapterKind`, `OpaqueAdapterRouteToken` (`IReadOnlyList<byte>`); static `Create(IntentId, RouteId, string adapterKind, byte[] opaqueAdapterRouteToken)` (token required) | yes |
| `InteractionBindingOverrideSet` | sealed class (private ctor) | prop `Overrides`; static `Create(IEnumerable<BindingOverride>)` (rejects duplicate intent/route pairs); static `Empty` | yes |
| `SourceSignal` | readonly struct, `IEquatable` | ctor `(RouteId, SourceId, InteractionModality, InteractionCapability sourceCapabilities, InteractionValue, InteractionPhase, long timestampTicks, int ingressSequence, int observationSequence = 0)`; props of the same names; `Equals`, `GetHashCode` | yes (adapter output) |
| `InteractionFrame` | sealed class (private ctor) | prop `Signals`; static `Create(IEnumerable<SourceSignal>)` returning `InteractionResult<InteractionFrame>` (enforces strictly increasing ingress, non-decreasing observation and timestamp, identical physical facts within one observation) | yes |
| `InteractionRoutePhaseState` | readonly struct, `IEquatable` | ctor `(ContextId, RouteId, SourceId, long startedAtTicks)`; props of the same names; `Equals`, `GetHashCode` | yes (read) |
| `InteractionToggleState` | readonly struct, `IEquatable` | ctor `(IntentId, bool active)`; props `IntentId`, `Active`; `Equals`, `GetHashCode` | yes (read) |
| `InteractionRoutingState` | sealed class, `IEquatable` (internal ctor) | props `PendingPhases`, `ToggleStates`; static `Empty`; `Equals`, `GetHashCode` | yes (read) |
| `SemanticInteractionEvent` | readonly struct, `IEquatable` | ctor `(IntentId, ContextId, RouteId, SourceId, InteractionModality, InteractionValue, InteractionPhase, InteractionActivationMode, int ingressSequence, long timestampTicks)`; props of the same names; `Equals`, `GetHashCode` | yes (handler input) |
| `InteractionDiagnostic` | readonly struct, `IEquatable` | ctor `(InteractionDiagnosticKind, InteractionValidationCode, string message, RouteId, ContextId, IntentId, int ingressSequence)`; props of the same names; `Equals`, `GetHashCode` | yes (read) |
| `InteractionDispatchResult` | readonly struct, `IEquatable` | ctor `(InteractionDispatchStatus, RouteId, ContextId, IntentId, InteractionPhase, InteractionHandlerOutcome?, int ingressSequence, string message)`; props of the same names; `Equals`, `GetHashCode` | yes (read) |
| `ActiveContextSnapshot` | sealed class (internal ctor) | prop `ActiveContexts` | no |
| `InteractionRoutingResult` | sealed class (internal ctor) | props `Dispatches`, `Diagnostics`, `Events`, `ActiveContexts`, `NextState` | yes (read) |
| `InteractionIntentHandler` | delegate | `InteractionHandlerOutcome (SemanticInteractionEvent)` | seam |
| `InteractionRouter` | sealed class (implicit public ctor) | `Route(InteractionRegistry, IEnumerable<ContextId> activeContexts, InteractionPolicySnapshot, InteractionFrame, InteractionRoutingState priorState, InteractionIntentHandler = null)` returning `InteractionRoutingResult` (throws on null registry or frame) | no |
| `InteractionCoordinator` | sealed class | ctor `(InteractionRegistry, IEnumerable<ContextId> activeContexts = null, InteractionPolicySnapshot = null, InteractionRoutingState = null)` (throws on null registry); props `Registry`, `State`, `ActiveContexts`, `Policy`; `SetActiveContexts(IEnumerable<ContextId>)`; `SetPolicy(InteractionPolicySnapshot)`; `RouteFrame(InteractionFrame, InteractionIntentHandler = null)` returning `InteractionRoutingResult`; `ResetPendingPhases(SourceId, IEnumerable<RouteId>)` returning `InteractionResult<int>` | yes |

## `com.lingkyn.interaction.unity` (namespace `Lingkyn.Interaction.Unity`)

19 public types. Assets expose read-only properties over private
`[SerializeField]` fields (underscore-prefixed); there are no public setters.

| Type | Kind | Public members | Consumer-facing |
| --- | --- | --- | --- |
| `InteractionIntentAsset` | sealed class, `ScriptableObject`, `[CreateAssetMenu]` | props `IntentId`, `ValueKind`, `RequiredCapabilities`, `DispatchOrder`, `MetadataKeys` | yes (authoring) |
| `InteractionRouteAsset` | sealed class, `ScriptableObject`, `[CreateAssetMenu]` | props `RouteId`, `Intent` (`InteractionIntentAsset`), `SourceSelector`, `SourceModality`, `SourceCapabilities`, `Action` (`InputActionReference`), `RouteOrder` | yes (authoring) |
| `InteractionContextAsset` | sealed class, `ScriptableObject`, `[CreateAssetMenu]` | props `ContextId`, `Priority`, `Routes` | yes (authoring) |
| `InteractionBindingSuggestionAsset` | sealed class, `ScriptableObject`, `[CreateAssetMenu]` | props `SuggestionId`, `Route`, `ProposedBindingPath` | yes (authoring) |
| `InteractionRegistryAsset` | sealed class, `ScriptableObject`, `[CreateAssetMenu]` | props `Intents`, `Contexts`, `BindingSuggestions` | yes (authoring) |
| `InteractionAssetIssueCode` | enum | `NullAsset = 0`, `InvalidIdentity`, `DuplicateIdentity`, `InvalidDefinition`, `MissingReference`, `MissingInputAction`, `UnknownIntent`, `UnknownRoute`, `CoreConversionFailed = 8` (explicit values) | yes (read) |
| `InteractionAssetIssue` | readonly struct, `IEquatable` | ctor `(InteractionAssetIssueCode, string assetName, string fieldPath, int index, string subject, string message)`; props of the same names; `Equals`, `GetHashCode` | yes (read) |
| `InteractionAssetValidationReport` | sealed class (internal ctor) | props `IsValid`, `Issues` | yes (read) |
| `InteractionUnityRegistry` | sealed class (internal ctor) | props `CoreRegistry`, `RouteBindings`; `TryGetRouteBinding(RouteId, out InputRouteBinding)` | yes (read) |
| `InteractionAuthoringResult` | sealed class (internal ctor) | props `Succeeded`, `Registry`, `Validation` | yes (read) |
| `InteractionAuthoringConverter` | static class | `Validate(InteractionRegistryAsset)` returning `InteractionAssetValidationReport`; `Convert(InteractionRegistryAsset)` returning `InteractionAuthoringResult` | yes |
| `InputRouteBinding` | sealed class, `IEquatable` | ctor `(RouteId, IntentId, SourceId sourceSelector, InteractionModality, InteractionCapability, InteractionValueKind, InputActionReference)` (throws `ArgumentException` on empty ids or an unstable action); props of the same names plus `ActionId` (`Guid`); `Equals`, `GetHashCode` | yes |
| `InputObservationStamp` | readonly struct, `IEquatable` | ctor `(int observationSequence, int firstIngressSequence)` (throws on negatives); props of the same names; `Equals`, `GetHashCode` | yes |
| `InputSystemSignalAdapter` | static class | `CaptureCallback(InputAction.CallbackContext, IEnumerable<InputRouteBinding> orderedCandidates, Func<InputControl, bool> controlAdmission, string observedSourceId, InteractionModality, InteractionCapability, long timestampTicks, InputObservationStamp)` returning `InteractionResult<InteractionFrame>`; `CaptureRawObservation(IEnumerable<InputRouteBinding>, string observedSourceId, InteractionModality, InteractionCapability, InteractionPhase, object rawValue, long timestampTicks, InputObservationStamp)` returning `InteractionResult<InteractionFrame>`; `ConvertRawValue(InteractionValueKind, object rawValue)` returning `InteractionResult<InteractionValue>` (accepts `bool`, `float`, `double`, `Vector2`, `Vector3`; `Pose` and `Text` are rejected); `ConvertPhase(InputActionPhase)` returning `InteractionResult<InteractionPhase>`; `ValidateRouteBindingDescriptor(InteractionRoute, InputRouteBinding)` returning `InteractionResult` | yes |
| `InputBindingDisplayEntry` | readonly struct, `IEquatable` | ctor `(RouteId, Guid actionId, Guid bindingId, int bindingIndex, string displayString, string deviceLayout, string controlPath)`; props of the same names; `Equals`, `GetHashCode` | yes (read) |
| `InputBindingDisplayService` | static class | `GetEntries(InputRouteBinding, InputBinding.DisplayStringOptions = DontUseShortDisplayNames)` returning `InteractionResult<IReadOnlyList<InputBindingDisplayEntry>>` | yes |
| `InputBindingOverrideRecord` | `[Serializable]` sealed class, `IEquatable` | ctor `(Guid bindingId, string overridePath, string overrideProcessors, string overrideInteractions)` (throws on empty GUID); props `BindingId`, `OverridePath`, `OverrideProcessors`, `OverrideInteractions`; `Equals`, `GetHashCode`. Serialized fields are `_bindingId`, `_overridePath`, `_overrideProcessors`, `_overrideInteractions` | yes |
| `InputBindingOverrideSnapshot` | sealed class | ctor `(RouteId, Guid actionId, IEnumerable<InputBindingOverrideRecord>)` (throws on empty id or GUID); props `RouteId`, `ActionId`, `Records` (sorted by binding GUID) | yes |
| `InputBindingOverrideService` | static class | `Capture(InputRouteBinding)` returning `InteractionResult<InputBindingOverrideSnapshot>`; `Serialize(InputBindingOverrideSnapshot)` returning `InteractionResult<string>` (JSON via `JsonUtility`); `Deserialize(string)` returning `InteractionResult<InputBindingOverrideSnapshot>`; `Apply(InputRouteBinding, InputBindingOverrideSnapshot, bool replaceExisting = true)` returning `InteractionResult`; `ToCoreOverride(InputRouteBinding, InputBindingOverrideSnapshot)` returning `InteractionResult<BindingOverride>` | yes |

## Seams a consumer extends

The family exposes one interface and one delegate; there are no abstract classes.

| Seam | Implemented by a consumer when | Shipped implementations |
| --- | --- | --- |
| `InteractionIntentHandler` (delegate) | any semantic event must reach product code; returned outcome is recorded in `InteractionDispatchResult` | none |
| `IInteractionPolicyPort` | a Settings-family adapter or product supplies the current policy snapshot | none; no Core type consumes the port (the coordinator takes a snapshot through `SetPolicy`) |
| `Func<InputControl, bool>` on `InputSystemSignalAdapter.CaptureCallback` | the consumer decides which live control is admitted for the declared source | none |

Adapter-side extension is by construction rather than inheritance: an adapter
produces `SourceSignal` values and `InteractionFrame` instances for the Core,
and may produce `BindingSuggestion` and `BindingOverride` values with its own
`AdapterKind` string and opaque bytes.

## Types that must stay binary-compatible across a release

| Type | Reason |
| --- | --- |
| `IntentId`, `ContextId`, `RouteId`, `SourceId`, `BindingSuggestionId` | Stable identities referenced by persisted overrides, authored assets and every event. |
| `InteractionValue`, `InteractionVector2`, `InteractionVector3`, `InteractionQuaternion`, `InteractionPose`, `InteractionValueKind` | The closed value union every adapter and handler handles. |
| `InteractionCapability`, `InteractionModality`, `InteractionPhase`, `InteractionActivationMode` | Serialized in `InteractionIntentAsset` and `InteractionRouteAsset`; capability flags are combined bitwise. |
| `InteractionValidationCode`, `InteractionError`, `InteractionResult`, `InteractionResult<T>` | Returned by every public operation in both packages. |
| `SourceSignal`, `InteractionFrame` | The adapter-to-Core boundary. |
| `SemanticInteractionEvent`, `InteractionIntentHandler`, `InteractionHandlerOutcome` | The Core-to-consumer boundary. |
| `InteractionDispatchResult`, `InteractionDiagnostic`, `InteractionDispatchStatus`, `InteractionDiagnosticKind`, `InteractionRoutingResult` | The outcome contract consumers and tests inspect. |
| `InteractionRoutingState`, `InteractionRoutePhaseState`, `InteractionToggleState` | Prior/next routing state passed back into the coordinator across frames. |
| `IntentDefinition`, `InteractionRoute`, `InteractionContextDefinition`, `BindingSuggestion`, `InteractionRegistry` | Registry vocabulary produced by every adapter. |
| `IntentPolicyEntry`, `RoutePolicyEntry`, `InteractionPolicySnapshot`, `IInteractionPolicyPort` | The Settings integration seam. |
| `BindingOverride`, `InteractionBindingOverrideSet` | The persisted per-player override contract. |
| `InteractionCoordinator` | Primary consumer entry point. |
| Unity asset serialized field names, `InputRouteBinding`, `InputObservationStamp`, `InputSystemSignalAdapter`, `InteractionAuthoringConverter` | Renaming a `_field` breaks authored `.asset` files; the adapter statics are the documented Unity entry points. |
| `InputBindingOverrideRecord` serialized field names and the `Serialize` JSON shape | Consumers persist the JSON; the private field names are the JSON keys. |

## Candidates for internal or sealed before the first release

| Type or member | Observation |
| --- | --- |
| `InteractionRouter` | Stateless and fully wrapped by `InteractionCoordinator.RouteFrame`; outside `Runtime/` only the Core tests construct it. Keeping it public commits to two routing entry points. |
| `ActiveContextSnapshot` | Wraps one `IReadOnlyList<ContextId>` with an internal constructor; nothing outside `Runtime/` references it. `InteractionRoutingResult.ActiveContexts` could expose the list directly. |
| `InteractionRoutePhaseState` and `InteractionToggleState` public constructors | Nothing public accepts a consumer-built instance (`InteractionRoutingState` has an internal constructor), so the constructors serve only tests. |
| `IInteractionPolicyPort` | Declared but not consumed by any Core or Unity type, and not referenced by tests or samples. Either wire it into the coordinator or remove it before it becomes a compatibility obligation. |
| `InteractionBindingOverrideSet` | Consumed by nothing in `Runtime/`; only the Core tests build one. Same decision as the policy port. |
| `InteractionValue.RequiredCapabilitiesForKind` | Used by `IntentDefinition.Create`; useful to adapters, but nothing outside `Runtime/` calls it. |
| Route-descriptor and adapter-kind strings | `"input-system-action:{GUID}"` and `"unity-input-system/1.14"` are private constants or inline literals in `InteractionAuthoringConverter`, `InputSystemSignalAdapter` and `InputBindingOverrideService`, yet they travel in public `OpaqueBindingDescriptor`, `AdapterKind` and JSON values. They are a de facto public format without a public definition. |

All classes are already `sealed`; all structs are `readonly`.

## Open questions for the review

- Should `InteractionRouter` remain a public, purely functional entry point beside `InteractionCoordinator`, or become internal so that the coordinator is the single supported surface?
- `InteractionRoutingState` cannot be constructed by a consumer, so prior state cannot be restored from persistence or fabricated for tests without running frames; decide whether a public factory or a public constructor is wanted.
- `IInteractionPolicyPort` and `InteractionBindingOverrideSet` are defined but not consumed. Confirm which family owns the call that reads them before the first release freezes their shapes.
- The adapter kind string `"unity-input-system/1.14"` embeds the pinned Input System version; `package.json` pins `com.unity.inputsystem` to exactly `1.14.2`. Decide whether the adapter kind is a versioned public constant and what an Input System upgrade means for stored suggestions and overrides.
- Several enum members are never produced by `Runtime/` code: `InteractionValidationCode.InvalidIngressSequence`, `InteractionDiagnosticKind.InactiveContext` and `DisabledRoute` (admission failures are reported as `ValidationFailure`), and `InteractionAssetIssueCode.InvalidIdentity`, `InvalidDefinition` and `MissingReference` (the converter reports `CoreConversionFailed`). Decide whether they are reserved or removed.
- `InteractionValidationCode` has implicit numeric values after `None`; the other Core enums are explicit. Confirm an append-only rule or assign values.
- `IntentDefinition.Create`, `InteractionRoute.Create` and `BindingSuggestion.Create` return one fixed message for every argument failure ("Dispatch order must be non-negative.", "Route order must be non-negative.", "Adapter kind is required."). Confirm whether diagnostic messages are part of the surface, and whether these should be split per cause.
- `InputSystemSignalAdapter.CaptureRawObservation` and `ConvertRawValue` take `object rawValue`; the architecture contract rules out `object` payloads for values. Consider typed overloads before the signature freezes.
- `InputBindingDisplayService.GetEntries` places an Input System enum (`InputBinding.DisplayStringOptions`) in a public signature with a default value; a change to that enum in a future Input System release changes the surface.
- `InputRouteBinding` holds a live `InputActionReference` and validates `ActionId` against it on every service call; document the lifetime expectation for consumers that unload input assets.
- `InteractionCoordinator.SetActiveContexts` and `SetPolicy` reconcile pending phases and toggles in place; the coordinator is not documented as thread-affine or thread-safe.
- Toggle activation rejects non-`Button` intents at routing time (`InvalidPolicy`) rather than at `InteractionPolicySnapshot.Create`; decide where that validation belongs.
