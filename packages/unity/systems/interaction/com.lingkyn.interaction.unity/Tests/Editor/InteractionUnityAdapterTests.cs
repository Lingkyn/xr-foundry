using System;
using System.Collections.Generic;
using System.Linq;
using Lingkyn.Interaction.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lingkyn.Interaction.Unity.Editor.Tests
{
    public sealed class InteractionUnityAdapterTests : InputTestFixture
    {
        private readonly List<UnityEngine.Object> _ownedObjects = new List<UnityEngine.Object>();

        [SetUp]
        public override void Setup()
        {
            base.Setup();
        }

        [TearDown]
        public override void TearDown()
        {
            foreach (var item in _ownedObjects.Where(item => item != null).Reverse<UnityEngine.Object>())
            {
                if (item is InputActionAsset actionAsset)
                    actionAsset.Disable();
                UnityEngine.Object.DestroyImmediate(item);
            }
            _ownedObjects.Clear();
            base.TearDown();
        }

        [Test]
        public void AuthoringConvertsDeterministicallyWithoutMutatingAssets()
        {
            var graph = CreateGraph(includeSuggestion: true);
            var routeIdBefore = graph.Route.RouteId;
            var actionBefore = graph.Route.Action;

            var first = InteractionAuthoringConverter.Convert(graph.Registry);
            var second = InteractionAuthoringConverter.Convert(graph.Registry);

            Assert.That(first.Succeeded, Is.True, FormatIssues(first.Validation));
            Assert.That(second.Succeeded, Is.True, FormatIssues(second.Validation));
            Assert.That(first.Registry.CoreRegistry.Routes.Select(item => item.Id.Value),
                Is.EqualTo(second.Registry.CoreRegistry.Routes.Select(item => item.Id.Value)));
            Assert.That(first.Registry.RouteBindings[0].ActionId, Is.EqualTo(graph.Action.id));
            Assert.That(first.Registry.CoreRegistry.BindingSuggestions.Count, Is.EqualTo(1));
            Assert.That(first.Registry.CoreRegistry.Routes.Count, Is.EqualTo(1));
            Assert.That(graph.Route.RouteId, Is.EqualTo(routeIdBefore));
            Assert.That(graph.Route.Action, Is.SameAs(actionBefore));

            SetString(graph.Route, "_routeId", "ui.confirm.changed-after-conversion");
            Assert.That(first.Registry.CoreRegistry.Routes[0].Id.Value, Is.EqualTo(routeIdBefore));
        }

        [Test]
        public void ValidationReportsAssetIndexAndFieldPath()
        {
            var graph = CreateGraph();
            SetObjectArray(graph.Registry, "_intents", null, graph.Intent);

            var report = InteractionAuthoringConverter.Validate(graph.Registry);

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Issues.Any(issue =>
                issue.Code == InteractionAssetIssueCode.NullAsset
                && issue.FieldPath == "intents[0]"
                && issue.Index == 0
                && issue.AssetName == graph.Registry.name), Is.True);
        }

        [Test]
        public void RouteRequiresExplicitInputActionReference()
        {
            var graph = CreateGraph();
            SetObject(graph.Route, "_action", null);

            var report = InteractionAuthoringConverter.Validate(graph.Registry);

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Issues.Any(issue =>
                issue.Code == InteractionAssetIssueCode.MissingInputAction
                && issue.FieldPath.EndsWith(".action", StringComparison.Ordinal)
                && issue.Subject == "ui.confirm.primary"), Is.True);
        }

        [Test]
        public void ValidationReportsDuplicateIntentContextRouteAndUnknownIntentPaths()
        {
            var graph = CreateGraph();

            SetObjectArray(graph.Registry, "_intents", graph.Intent, graph.Intent);
            Assert.That(InteractionAuthoringConverter.Validate(graph.Registry).Issues.Any(issue =>
                issue.Code == InteractionAssetIssueCode.DuplicateIdentity
                && issue.FieldPath == "intents[1].intentId"), Is.True);

            SetObjectArray(graph.Registry, "_intents", graph.Intent);
            SetObjectArray(graph.Registry, "_contexts", graph.Context, graph.Context);
            Assert.That(InteractionAuthoringConverter.Validate(graph.Registry).Issues.Any(issue =>
                issue.Code == InteractionAssetIssueCode.DuplicateIdentity
                && issue.FieldPath == "contexts[1].contextId"), Is.True);

            var secondContext = Own(ScriptableObject.CreateInstance<InteractionContextAsset>());
            secondContext.name = "OverlayContext";
            SetString(secondContext, "_contextId", "overlay");
            SetObjectArray(secondContext, "_routes", graph.Route);
            SetObjectArray(graph.Registry, "_contexts", graph.Context, secondContext);
            Assert.That(InteractionAuthoringConverter.Validate(graph.Registry).Issues.Any(issue =>
                issue.Code == InteractionAssetIssueCode.DuplicateIdentity
                && issue.FieldPath == "contexts[1].routes[0].routeId"), Is.True);

            SetObjectArray(graph.Registry, "_contexts", graph.Context);
            var orphanIntent = Own(ScriptableObject.CreateInstance<InteractionIntentAsset>());
            orphanIntent.name = "OrphanIntent";
            SetString(orphanIntent, "_intentId", "ui.orphan");
            SetObject(graph.Route, "_intent", orphanIntent);
            Assert.That(InteractionAuthoringConverter.Validate(graph.Registry).Issues.Any(issue =>
                issue.Code == InteractionAssetIssueCode.UnknownIntent
                && issue.FieldPath == "contexts[0].routes[0].intent"), Is.True);
        }

        [Test]
        public void BindingSuggestionValidationIsIndexedAndDuplicateSafe()
        {
            var graph = CreateGraph(includeSuggestion: true);
            var first = graph.Registry.BindingSuggestions[0];
            var duplicate = Own(ScriptableObject.CreateInstance<InteractionBindingSuggestionAsset>());
            duplicate.name = "DuplicateSuggestion";
            SetString(duplicate, "_suggestionId", first.SuggestionId);
            SetObject(duplicate, "_route", graph.Route);
            SetString(duplicate, "_proposedBindingPath", "<Gamepad>/buttonSouth");
            SetObjectArray(graph.Registry, "_bindingSuggestions", first, duplicate);

            var duplicateReport = InteractionAuthoringConverter.Validate(graph.Registry);
            Assert.That(duplicateReport.Issues.Any(issue =>
                issue.Code == InteractionAssetIssueCode.DuplicateIdentity
                && issue.FieldPath == "bindingSuggestions[1].suggestionId"
                && issue.Index == 1), Is.True);

            var orphanRoute = Own(ScriptableObject.CreateInstance<InteractionRouteAsset>());
            orphanRoute.name = "OrphanRoute";
            SetString(duplicate, "_suggestionId", "ui.confirm.secondary");
            SetObject(duplicate, "_route", orphanRoute);
            var unknownReport = InteractionAuthoringConverter.Validate(graph.Registry);
            Assert.That(unknownReport.Issues.Any(issue =>
                issue.Code == InteractionAssetIssueCode.UnknownRoute
                && issue.FieldPath == "bindingSuggestions[1].route"), Is.True);
        }

        [Test]
        public void RawValueConversionCoversSupportedKindsAndFailsClosed()
        {
            var button = InputSystemSignalAdapter.ConvertRawValue(InteractionValueKind.Button, true);
            var scalar = InputSystemSignalAdapter.ConvertRawValue(InteractionValueKind.Scalar, 0.25f);
            var vector2 = InputSystemSignalAdapter.ConvertRawValue(InteractionValueKind.Vector2, new Vector2(1, -2));
            var vector3 = InputSystemSignalAdapter.ConvertRawValue(InteractionValueKind.Vector3, new Vector3(1, 2, 3));

            Assert.That(button.Succeeded && button.Value.Button, Is.True);
            Assert.That(scalar.Succeeded, Is.True);
            Assert.That(scalar.Value.Scalar, Is.EqualTo(0.25d));
            Assert.That(vector2.Succeeded, Is.True);
            Assert.That(vector2.Value.Vector2.X, Is.EqualTo(1d));
            Assert.That(vector2.Value.Vector2.Y, Is.EqualTo(-2d));
            Assert.That(vector3.Succeeded, Is.True);
            Assert.That(vector3.Value.Vector3.Z, Is.EqualTo(3d));
            Assert.That(InputSystemSignalAdapter.ConvertRawValue(InteractionValueKind.Scalar, double.NaN).Succeeded, Is.False);
            Assert.That(InputSystemSignalAdapter.ConvertRawValue(InteractionValueKind.Button, 1f).Succeeded, Is.False);
            Assert.That(InputSystemSignalAdapter.ConvertRawValue(InteractionValueKind.Pose, Vector3.zero).Succeeded, Is.False);
        }

        [Test]
        public void ObservationUsesExplicitCandidateAndSequenceOrder()
        {
            var graph = CreateGraph();
            var first = Binding("route.z", graph.ActionReference, InteractionValueKind.Button);
            var second = Binding("route.a", graph.ActionReference, InteractionValueKind.Button);

            var result = InputSystemSignalAdapter.CaptureRawObservation(
                new[] { first, second },
                "player.primary",
                InteractionModality.Simulated,
                InteractionCapability.Digital,
                InteractionPhase.Performed,
                true,
                100,
                new InputObservationStamp(7, 12));

            Assert.That(result.Succeeded, Is.True, result.Error.ToString());
            Assert.That(result.Value.Signals.Select(item => item.RouteId.Value),
                Is.EqualTo(new[] { "route.z", "route.a" }));
            Assert.That(result.Value.Signals.Select(item => item.ObservationSequence),
                Is.EqualTo(new[] { 7, 7 }));
            Assert.That(result.Value.Signals.Select(item => item.IngressSequence),
                Is.EqualTo(new[] { 12, 13 }));
        }

        [Test]
        public void ObservationRejectsMixedPhysicalFactsAndMismatchedValues()
        {
            var graph = CreateGraph();
            var digital = Binding("route.digital", graph.ActionReference, InteractionValueKind.Button);
            var wrongSource = new InputRouteBinding(
                Id<RouteId>("route.other", RouteId.TryCreate),
                Id<IntentId>("ui.confirm", IntentId.TryCreate),
                Id<SourceId>("player.secondary", SourceId.TryCreate),
                InteractionModality.Simulated,
                InteractionCapability.Digital,
                InteractionValueKind.Button,
                graph.ActionReference);

            var mixed = InputSystemSignalAdapter.CaptureRawObservation(
                new[] { digital, wrongSource }, "player.primary", InteractionModality.Simulated,
                InteractionCapability.Digital, InteractionPhase.Performed, true, 1,
                new InputObservationStamp(0, 0));
            var mismatch = InputSystemSignalAdapter.CaptureRawObservation(
                new[] { digital }, "player.primary", InteractionModality.Simulated,
                InteractionCapability.Digital, InteractionPhase.Performed, 1f, 1,
                new InputObservationStamp(0, 0));

            Assert.That(mixed.Succeeded, Is.False);
            Assert.That(mismatch.Succeeded, Is.False);
        }

        [Test]
        public void InputSystemPhasesMapOnlyToNeutralLifecycle()
        {
            Assert.That(InputSystemSignalAdapter.ConvertPhase(InputActionPhase.Started).Value, Is.EqualTo(InteractionPhase.Started));
            Assert.That(InputSystemSignalAdapter.ConvertPhase(InputActionPhase.Performed).Value, Is.EqualTo(InteractionPhase.Performed));
            Assert.That(InputSystemSignalAdapter.ConvertPhase(InputActionPhase.Canceled).Value, Is.EqualTo(InteractionPhase.Canceled));
            Assert.That(InputSystemSignalAdapter.ConvertPhase(InputActionPhase.Waiting).Succeeded, Is.False);
            Assert.That(InputSystemSignalAdapter.ConvertPhase(InputActionPhase.Disabled).Succeeded, Is.False);
        }

        [Test]
        public void LiveCallbackCapturesStartedPerformedAndCanceledFrames()
        {
            var graph = CreateGraph(actionType: InputActionType.Value);
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var binding = KeyboardBinding("ui.confirm.primary", graph.ActionReference);
            var sequence = 0;
            InteractionResult<InteractionFrame> started = default;
            InteractionResult<InteractionFrame> performed = default;
            InteractionResult<InteractionFrame> canceled = default;

            InteractionResult<InteractionFrame> Capture(InputAction.CallbackContext callback)
            {
                var current = sequence++;
                return InputSystemSignalAdapter.CaptureCallback(
                    callback,
                    new[] { binding },
                    "player.primary",
                    InteractionModality.KeyboardMouse,
                    InteractionCapability.Digital,
                    current,
                    new InputObservationStamp(current, current));
            }

            graph.Action.started += callback => started = Capture(callback);
            graph.Action.performed += callback => performed = Capture(callback);
            graph.Action.canceled += callback => canceled = Capture(callback);
            graph.Action.Enable();
            Press(keyboard.spaceKey);
            Release(keyboard.spaceKey);

            Assert.That(started.Succeeded, Is.True, started.Error.ToString());
            Assert.That(performed.Succeeded, Is.True, performed.Error.ToString());
            Assert.That(canceled.Succeeded, Is.True, canceled.Error.ToString());
            Assert.That(started.Value.Signals[0].Phase, Is.EqualTo(InteractionPhase.Started));
            Assert.That(performed.Value.Signals[0].Phase, Is.EqualTo(InteractionPhase.Performed));
            Assert.That(canceled.Value.Signals[0].Phase, Is.EqualTo(InteractionPhase.Canceled));
            Assert.That(started.Value.Signals[0].Value.Button, Is.True);
            Assert.That(performed.Value.Signals[0].Value.Button, Is.True);
            Assert.That(canceled.Value.Signals[0].Value.Button, Is.False);
            Assert.That(sequence, Is.EqualTo(3));
        }

        [Test]
        public void LiveCallbackRejectsMismatchedActionGuid()
        {
            var observed = CreateGraph(actionType: InputActionType.Value);
            var other = CreateGraph(actionType: InputActionType.Value);
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var binding = KeyboardBinding("ui.confirm.other", other.ActionReference);
            InteractionResult<InteractionFrame> captured = default;

            observed.Action.performed += callback => captured = InputSystemSignalAdapter.CaptureCallback(
                callback,
                new[] { binding },
                "player.primary",
                InteractionModality.KeyboardMouse,
                InteractionCapability.Digital,
                1,
                new InputObservationStamp(0, 0));
            observed.Action.Enable();
            Press(keyboard.spaceKey);

            Assert.That(captured.Succeeded, Is.False);
            Assert.That((captured.Error.Message ?? string.Empty).IndexOf("GUID", StringComparison.Ordinal) >= 0, Is.True);
        }

        [Test]
        public void ObservationRejectsDuplicateRouteCandidate()
        {
            var graph = CreateGraph();
            var binding = Binding("route.duplicate", graph.ActionReference, InteractionValueKind.Button);

            var result = InputSystemSignalAdapter.CaptureRawObservation(
                new[] { binding, binding }, "player.primary", InteractionModality.Simulated,
                InteractionCapability.Digital, InteractionPhase.Performed, true, 1,
                new InputObservationStamp(0, 0));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(InteractionValidationCode.DuplicateIdentity));
        }

        [Test]
        public void BindingDisplayEnumeratesEveryBindingByStableGuid()
        {
            var graph = CreateGraph();
            var converted = InteractionAuthoringConverter.Convert(graph.Registry);

            var display = InputBindingDisplayService.GetEntries(converted.Registry.RouteBindings[0]);

            Assert.That(display.Succeeded, Is.True, display.Error.ToString());
            Assert.That(display.Value.Count, Is.EqualTo(2));
            Assert.That(display.Value.Select(item => item.BindingId),
                Is.EqualTo(graph.Action.bindings.Select(item => item.id)));
            Assert.That(display.Value.All(item => item.ActionId == graph.Action.id), Is.True);
            Assert.That(display.Value.All(item => item.RouteId.Value == "ui.confirm.primary"), Is.True);
        }

        [Test]
        public void OverrideRoundTripIsDeterministicAndAppliesByBindingGuid()
        {
            var graph = CreateGraph();
            var binding = InteractionAuthoringConverter.Convert(graph.Registry).Registry.RouteBindings[0];
            graph.Action.ApplyBindingOverride(1, new InputBinding { overridePath = "<Keyboard>/enter" });

            var captured = InputBindingOverrideService.Capture(binding);
            var firstJson = InputBindingOverrideService.Serialize(captured.Value);
            var secondJson = InputBindingOverrideService.Serialize(captured.Value);
            graph.Action.RemoveAllBindingOverrides();
            var decoded = InputBindingOverrideService.Deserialize(firstJson.Value);
            var applied = InputBindingOverrideService.Apply(binding, decoded.Value);
            var core = InputBindingOverrideService.ToCoreOverride(binding, decoded.Value);

            Assert.That(captured.Succeeded, Is.True, captured.Error.ToString());
            Assert.That(captured.Value.Records.Count, Is.EqualTo(1));
            Assert.That(firstJson.Succeeded && secondJson.Succeeded, Is.True);
            Assert.That(firstJson.Value, Is.EqualTo(secondJson.Value));
            Assert.That(decoded.Succeeded, Is.True, decoded.Error.ToString());
            Assert.That(applied.Succeeded, Is.True, applied.Error.ToString());
            Assert.That(graph.Action.bindings[1].overridePath, Is.EqualTo("<Keyboard>/enter"));
            Assert.That(core.Succeeded, Is.True, core.Error.ToString());
            Assert.That(core.Value.RouteId, Is.EqualTo(binding.RouteId));
            Assert.That(core.Value.OpaqueAdapterRouteToken.Count, Is.GreaterThan(0));
        }

        [Test]
        public void OverrideValidationIsAtomicBeforeExistingOverridesAreReplaced()
        {
            var graph = CreateGraph();
            var binding = InteractionAuthoringConverter.Convert(graph.Registry).Registry.RouteBindings[0];
            graph.Action.ApplyBindingOverride(0, new InputBinding { overridePath = "<Keyboard>/escape" });
            var unknown = new InputBindingOverrideSnapshot(
                binding.RouteId,
                binding.ActionId,
                new[] { new InputBindingOverrideRecord(Guid.NewGuid(), "<Keyboard>/enter", string.Empty, string.Empty) });

            var result = InputBindingOverrideService.Apply(binding, unknown, replaceExisting: true);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(graph.Action.bindings[0].overridePath, Is.EqualTo("<Keyboard>/escape"));
        }

        private Graph CreateGraph(
            bool includeSuggestion = false,
            InputActionType actionType = InputActionType.Button)
        {
            var actionAsset = Own(ScriptableObject.CreateInstance<InputActionAsset>());
            var map = actionAsset.AddActionMap("UI");
            var action = map.AddAction("Confirm", actionType, expectedControlLayout: "Button");
            action.AddBinding("<Keyboard>/space");
            action.AddBinding("<Gamepad>/buttonSouth");
            var reference = Own(InputActionReference.Create(action));

            var intent = Own(ScriptableObject.CreateInstance<InteractionIntentAsset>());
            intent.name = "ConfirmIntent";
            SetString(intent, "_intentId", "ui.confirm");
            SetInt(intent, "_valueKind", (int)InteractionValueKind.Button);
            SetInt(intent, "_requiredCapabilities", (int)InteractionCapability.Digital);

            var route = Own(ScriptableObject.CreateInstance<InteractionRouteAsset>());
            route.name = "ConfirmRoute";
            SetString(route, "_routeId", "ui.confirm.primary");
            SetObject(route, "_intent", intent);
            SetString(route, "_sourceSelector", "player.primary");
            SetInt(route, "_sourceModality", (int)InteractionModality.Simulated);
            SetInt(route, "_sourceCapabilities", (int)InteractionCapability.Digital);
            SetObject(route, "_action", reference);

            var context = Own(ScriptableObject.CreateInstance<InteractionContextAsset>());
            context.name = "UiContext";
            SetString(context, "_contextId", "ui");
            SetObjectArray(context, "_routes", route);

            var registry = Own(ScriptableObject.CreateInstance<InteractionRegistryAsset>());
            registry.name = "InteractionRegistry";
            SetObjectArray(registry, "_intents", intent);
            SetObjectArray(registry, "_contexts", context);

            if (includeSuggestion)
            {
                var suggestion = Own(ScriptableObject.CreateInstance<InteractionBindingSuggestionAsset>());
                suggestion.name = "ConfirmSuggestion";
                SetString(suggestion, "_suggestionId", "ui.confirm.default");
                SetObject(suggestion, "_route", route);
                SetString(suggestion, "_proposedBindingPath", "<Keyboard>/enter");
                SetObjectArray(registry, "_bindingSuggestions", suggestion);
            }

            return new Graph(registry, intent, route, context, actionAsset, action, reference);
        }

        private static InputRouteBinding Binding(
            string routeId,
            InputActionReference reference,
            InteractionValueKind valueKind) =>
            new InputRouteBinding(
                Id<RouteId>(routeId, RouteId.TryCreate),
                Id<IntentId>("ui.confirm", IntentId.TryCreate),
                Id<SourceId>("player.primary", SourceId.TryCreate),
                InteractionModality.Simulated,
                InteractionCapability.Digital,
                valueKind,
                reference);

        private static InputRouteBinding KeyboardBinding(
            string routeId,
            InputActionReference reference) =>
            new InputRouteBinding(
                Id<RouteId>(routeId, RouteId.TryCreate),
                Id<IntentId>("ui.confirm", IntentId.TryCreate),
                Id<SourceId>("player.primary", SourceId.TryCreate),
                InteractionModality.KeyboardMouse,
                InteractionCapability.Digital,
                InteractionValueKind.Button,
                reference);

        private static T Id<T>(string value, Func<string, InteractionResult<T>> factory)
        {
            var result = factory(value);
            Assert.That(result.Succeeded, Is.True, result.Error.ToString());
            return result.Value;
        }

        private T Own<T>(T item) where T : UnityEngine.Object
        {
            _ownedObjects.Add(item);
            return item;
        }

        private static void SetString(UnityEngine.Object target, string propertyName, string value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInt(UnityEngine.Object target, string propertyName, int value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObject(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectArray(UnityEngine.Object target, string propertyName, params UnityEngine.Object[] values)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            property.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++)
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string FormatIssues(InteractionAssetValidationReport report) =>
            string.Join("\n", report.Issues.Select(issue =>
                $"{issue.Code} {issue.AssetName} {issue.FieldPath}: {issue.Message}"));

        private sealed class Graph
        {
            public Graph(
                InteractionRegistryAsset registry,
                InteractionIntentAsset intent,
                InteractionRouteAsset route,
                InteractionContextAsset context,
                InputActionAsset actionAsset,
                InputAction action,
                InputActionReference actionReference)
            {
                Registry = registry;
                Intent = intent;
                Route = route;
                Context = context;
                ActionAsset = actionAsset;
                Action = action;
                ActionReference = actionReference;
            }

            public InteractionRegistryAsset Registry { get; }
            public InteractionIntentAsset Intent { get; }
            public InteractionRouteAsset Route { get; }
            public InteractionContextAsset Context { get; }
            public InputActionAsset ActionAsset { get; }
            public InputAction Action { get; }
            public InputActionReference ActionReference { get; }
        }
    }
}
