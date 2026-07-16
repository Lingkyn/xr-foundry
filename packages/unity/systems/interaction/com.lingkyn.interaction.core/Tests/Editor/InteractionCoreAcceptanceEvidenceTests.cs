using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Lingkyn.Interaction.Core.Editor.Tests
{
    /// <summary>
    /// Acceptance evidence for docs/standards/interaction/verification-contract.md Core gate.
    /// Each test asserts events, suppressed events, outcomes, diagnostics, active-context
    /// snapshot, handler calls, and ingress sequences on independent surfaces.
    /// </summary>
    [Category("interaction-core-acceptance")]
    public sealed class InteractionCoreAcceptanceEvidenceTests
    {
        [Test]
        [Category("verification:core:identities")]
        public void Acceptance_Identities_FailClosedOnIndependentValidationSurfaces()
        {
            var invalidIntent = IntentId.TryCreate(string.Empty);
            var defaultRoute = RouteId.TryCreate("default");
            var duplicateRegistry = InteractionRegistry.Create(
                new[] { MustIntent("ui.confirm", InteractionValueKind.Button, InteractionCapability.Digital, 0) },
                new[]
                {
                    MustContext("context.ui", 0, Array.Empty<RouteId>()),
                    MustContext("context.ui", 1, Array.Empty<RouteId>()),
                },
                Array.Empty<InteractionRoute>());

            Assert.That(invalidIntent.Succeeded, Is.False, "Invalid identity surface");
            Assert.That(invalidIntent.Error.Code, Is.EqualTo(InteractionValidationCode.InvalidIdentity));
            Assert.That(defaultRoute.Succeeded, Is.False, "Default identity surface");
            Assert.That(defaultRoute.Error.Code, Is.EqualTo(InteractionValidationCode.DefaultIdentity));
            Assert.That(duplicateRegistry.Succeeded, Is.False, "Duplicate identity surface");
            Assert.That(duplicateRegistry.Error.Code, Is.EqualTo(InteractionValidationCode.DuplicateIdentity));
        }

        [Test]
        [Category("verification:core:definition-validation")]
        public void Acceptance_DefinitionValidation_ReportsKindCapabilityAndFiniteSurfacesIndependently()
        {
            var capabilityMismatch = IntentDefinition.Create(
                MustIntentId("ui.confirm"),
                InteractionValueKind.Button,
                InteractionCapability.Scalar,
                0);
            var nonFiniteScalar = InteractionValue.Validate(
                InteractionValueKind.Scalar,
                InteractionValue.FromScalar(double.PositiveInfinity));
            var invalidPose = InteractionValue.Validate(
                InteractionValueKind.Pose,
                InteractionValue.FromPose(new InteractionPose(
                    new InteractionVector3(double.NaN, 0, 0),
                    new InteractionQuaternion(0, 0, 0, 1),
                    true,
                    true)));

            Assert.That(capabilityMismatch.Succeeded, Is.False);
            Assert.That(capabilityMismatch.Error.Code, Is.EqualTo(InteractionValidationCode.CapabilityMismatch));
            Assert.That(nonFiniteScalar.Succeeded, Is.False);
            Assert.That(nonFiniteScalar.Error.Code, Is.EqualTo(InteractionValidationCode.NonFiniteValue));
            Assert.That(invalidPose.Succeeded, Is.False);
            Assert.That(invalidPose.Error.Code, Is.EqualTo(InteractionValidationCode.InvalidPose));
        }

        [Test]
        [Category("verification:core:inactive-context")]
        public void Acceptance_InactiveContext_RejectsWithoutEventsOrHandlerCalls()
        {
            var registry = BuildBasicRegistry();
            var evidence = RoutingEvidenceSnapshot.Route(
                registry,
                Array.Empty<ContextId>(),
                InteractionPolicySnapshot.Empty,
                MustFrame(Signal("route.ui.confirm", InteractionPhase.Started, 0)));

            evidence.AssertIndependentSurfaces(
                expectedEventCount: 0,
                expectedDispatchCount: 1,
                expectedDiagnosticCount: 1,
                expectedHandlerCalls: 0,
                expectedDispatchIngressSequences: new[] { 0 },
                expectedActiveContexts: Array.Empty<string>());
            evidence.AssertNoEmittedEventMatches(
                e => e.IntentId.Value == "ui.confirm",
                "Inactive context must suppress semantic events.");
            Assert.That(evidence.Dispatches.Single().Status, Is.EqualTo(InteractionDispatchStatus.Rejected));
            evidence.AssertDiagnostic(
                InteractionDiagnosticKind.InactiveContext,
                InteractionValidationCode.InactiveContext,
                0);
        }

        [Test]
        [Category("verification:core:shadowing")]
        public void Acceptance_HigherPriorityShadowing_SeparatesWinnerShadowAndDiagnostics()
        {
            var registry = BuildOverlappingRegistry(samePriority: false);
            var evidence = RoutingEvidenceSnapshot.Route(
                registry,
                new[] { MustContextId("context.low"), MustContextId("context.high") },
                InteractionPolicySnapshot.Empty,
                MustFrame(Signal("route.ui.confirm", InteractionPhase.Started, 0)));

            evidence.AssertIndependentSurfaces(
                expectedEventCount: 1,
                expectedDispatchCount: 2,
                expectedDiagnosticCount: 1,
                expectedHandlerCalls: 0,
                expectedDispatchIngressSequences: new[] { 0, 0 },
                expectedActiveContexts: new[] { "context.high", "context.low" },
                expectedEventIngressSequences: new[] { 0 });
            Assert.That(evidence.EmittedEvents.Single().ContextId.Value, Is.EqualTo("context.high"));
            Assert.That(evidence.Dispatches.Any(d => d.Status == InteractionDispatchStatus.Shadowed), Is.True);
            Assert.That(evidence.Dispatches.Any(d =>
                d.Status == InteractionDispatchStatus.Routed
                && d.ContextId.Value == "context.high"), Is.True);
            evidence.AssertNoEmittedEventMatches(
                e => e.ContextId.Value == "context.low",
                "Shadowed lower-priority context must not emit events.");
            evidence.AssertDiagnostic(
                InteractionDiagnosticKind.ShadowedRoute,
                InteractionValidationCode.ShadowedContext,
                0);
        }

        [Test]
        [Category("verification:core:equal-priority-collision")]
        public void Acceptance_EqualPriorityCollision_FailsClosedWithoutEventsOrHandlers()
        {
            var registry = BuildOverlappingRegistry(samePriority: true);
            var evidence = RoutingEvidenceSnapshot.Route(
                registry,
                new[] { MustContextId("context.low"), MustContextId("context.high") },
                InteractionPolicySnapshot.Empty,
                MustFrame(Signal("route.ui.confirm", InteractionPhase.Started, 0)));

            evidence.AssertIndependentSurfaces(
                expectedEventCount: 0,
                expectedDispatchCount: 1,
                expectedDiagnosticCount: 1,
                expectedHandlerCalls: 0,
                expectedDispatchIngressSequences: new[] { 0 },
                expectedActiveContexts: new[] { "context.high", "context.low" });
            Assert.That(evidence.Dispatches.Single().Status, Is.EqualTo(InteractionDispatchStatus.Ambiguous));
            evidence.AssertDiagnostic(
                InteractionDiagnosticKind.AmbiguousRoute,
                InteractionValidationCode.AmbiguousContextCollision,
                0);
        }

        [Test]
        [Category("verification:core:ingress-order")]
        public void Acceptance_IngressOrder_PreservesAdapterSequenceOnEverySurface()
        {
            var registry = BuildBasicRegistry();
            var evidence = RoutingEvidenceSnapshot.Route(
                registry,
                new[] { MustContextId("context.ui") },
                InteractionPolicySnapshot.Empty,
                MustFrame(
                    Signal("route.ui.confirm", InteractionPhase.Started, 0),
                    Signal("route.ui.cancel", InteractionPhase.Started, 1),
                    Signal("route.ui.confirm", InteractionPhase.Performed, 2, true)));

            evidence.AssertIndependentSurfaces(
                expectedEventCount: 3,
                expectedDispatchCount: 3,
                expectedDiagnosticCount: 0,
                expectedHandlerCalls: 0,
                expectedDispatchIngressSequences: new[] { 0, 1, 2 },
                expectedActiveContexts: new[] { "context.ui" },
                expectedEventIngressSequences: new[] { 0, 1, 2 });
            Assert.That(evidence.Diagnostics.Select(d => d.IngressSequence), Is.Empty);
            Assert.That(
                evidence.EmittedEvents.Select(e => e.IntentId.Value),
                Is.EqualTo(new[] { "ui.confirm", "ui.cancel", "ui.confirm" }));
        }

        [Test]
        [Category("verification:core:phase-lifecycle")]
        public void Acceptance_PhaseLifecycle_RejectsInvalidTransitionsIndependently()
        {
            var registry = BuildBasicRegistry();
            var session = new InteractionRoutingSession();
            RoutingEvidenceSnapshot.Route(
                registry,
                new[] { MustContextId("context.ui") },
                InteractionPolicySnapshot.Empty,
                MustFrame(Signal("route.ui.confirm", InteractionPhase.Started, 0)),
                session: session);

            var duplicate = RoutingEvidenceSnapshot.Route(
                registry,
                new[] { MustContextId("context.ui") },
                InteractionPolicySnapshot.Empty,
                MustFrame(Signal("route.ui.confirm", InteractionPhase.Started, 1)),
                session: session);
            duplicate.AssertIndependentSurfaces(0, 1, 1, 0, new[] { 1 }, new[] { "context.ui" });
            duplicate.AssertDiagnostic(
                InteractionDiagnosticKind.ValidationFailure,
                InteractionValidationCode.DuplicatePhase,
                1);

            var nakedPerformed = RoutingEvidenceSnapshot.Route(
                registry,
                new[] { MustContextId("context.ui") },
                InteractionPolicySnapshot.Empty,
                MustFrame(Signal("route.ui.confirm", InteractionPhase.Performed, 2, true)),
                session: new InteractionRoutingSession());
            nakedPerformed.AssertIndependentSurfaces(0, 1, 1, 0, new[] { 2 }, new[] { "context.ui" });
            nakedPerformed.AssertDiagnostic(
                InteractionDiagnosticKind.ValidationFailure,
                InteractionValidationCode.InvalidPhaseTransition,
                2);
        }

        [Test]
        [Category("verification:core:multimodal")]
        public void Acceptance_MultiModalRouting_PreservesModalityAndIntentPerEvent()
        {
            var registry = BuildMultiModalRegistry();
            var evidence = RoutingEvidenceSnapshot.Route(
                registry,
                new[] { MustContextId("context.ui") },
                InteractionPolicySnapshot.Empty,
                MustFrame(
                    KeyboardSignal(InteractionPhase.Started, 0),
                    GamepadSignal(InteractionPhase.Started, 1)));

            evidence.AssertIndependentSurfaces(2, 2, 0, 0, new[] { 0, 1 }, new[] { "context.ui" });
            Assert.That(evidence.EmittedEvents.Select(e => e.Modality), Is.EqualTo(new[]
            {
                InteractionModality.KeyboardMouse,
                InteractionModality.Gamepad,
            }));
            Assert.That(evidence.EmittedEvents.Select(e => e.IntentId.Value), Is.EqualTo(new[]
            {
                "ui.confirm",
                "ui.confirm.gamepad",
            }));
            Assert.That(
                evidence.EmittedEvents.Select(e => e.SourceId.Value),
                Is.EqualTo(new[] { "source.keyboard.space", "source.gamepad.a" }));
        }

        [Test]
        [Category("verification:core:cancellation")]
        public void Acceptance_Cancellation_EmitsCanceledWithoutHandlerInvocation()
        {
            var registry = BuildBasicRegistry();
            var session = new InteractionRoutingSession();
            RoutingEvidenceSnapshot.Route(
                registry,
                new[] { MustContextId("context.ui") },
                InteractionPolicySnapshot.Empty,
                MustFrame(Signal("route.ui.confirm", InteractionPhase.Started, 0)),
                session: session);

            var evidence = RoutingEvidenceSnapshot.Route(
                registry,
                new[] { MustContextId("context.ui") },
                InteractionPolicySnapshot.Empty,
                MustFrame(Signal("route.ui.confirm", InteractionPhase.Canceled, 1)),
                _ => InteractionHandlerOutcome.Accepted,
                session);

            evidence.AssertIndependentSurfaces(1, 1, 0, 0, new[] { 1 }, new[] { "context.ui" });
            Assert.That(evidence.EmittedEvents.Single().Phase, Is.EqualTo(InteractionPhase.Canceled));
            Assert.That(evidence.Dispatches.Single().Status, Is.EqualTo(InteractionDispatchStatus.Canceled));
            evidence.AssertNoEmittedEventMatches(
                e => e.Phase == InteractionPhase.Performed,
                "Cancellation must not emit performed.");
        }

        [Test]
        [Category("verification:core:handler-outcomes")]
        public void Acceptance_HandlerOutcomes_AreRecordedSeparatelyFromLifecycleEvents()
        {
            var registry = BuildBasicRegistry();
            var session = new InteractionRoutingSession();
            RoutingEvidenceSnapshot.Route(
                registry,
                new[] { MustContextId("context.ui") },
                InteractionPolicySnapshot.Empty,
                MustFrame(Signal("route.ui.confirm", InteractionPhase.Started, 0)),
                session: session);

            var evidence = RoutingEvidenceSnapshot.Route(
                registry,
                new[] { MustContextId("context.ui") },
                InteractionPolicySnapshot.Empty,
                MustFrame(Signal("route.ui.confirm", InteractionPhase.Performed, 1, true)),
                _ => InteractionHandlerOutcome.Deferred,
                session);

            evidence.AssertIndependentSurfaces(1, 1, 1, 1, new[] { 1 }, new[] { "context.ui" });
            Assert.That(evidence.EmittedEvents.Single().Phase, Is.EqualTo(InteractionPhase.Performed));
            Assert.That(evidence.Dispatches.Single().Status, Is.EqualTo(InteractionDispatchStatus.HandlerOutcome));
            Assert.That(evidence.Dispatches.Single().HandlerOutcome, Is.EqualTo(InteractionHandlerOutcome.Deferred));
            Assert.That(evidence.HandlerEvents.Single().IntentId.Value, Is.EqualTo("ui.confirm"));
            evidence.AssertDiagnostic(
                InteractionDiagnosticKind.HandlerResult,
                InteractionValidationCode.None,
                1);
        }

        [Test]
        [Category("verification:core:binding-suggestion")]
        public void Acceptance_BindingSuggestions_DoNotActivateRoutesOrAlterRouting()
        {
            var registry = BuildRegistryWithSuggestion();
            var suggestion = registry.BindingSuggestions.Single();
            var withoutSuggestion = RoutingEvidenceSnapshot.Route(
                BuildBasicRegistry(),
                new[] { MustContextId("context.ui") },
                InteractionPolicySnapshot.Empty,
                MustFrame(Signal("route.ui.confirm", InteractionPhase.Started, 0)));
            var withSuggestion = RoutingEvidenceSnapshot.Route(
                registry,
                new[] { MustContextId("context.ui") },
                InteractionPolicySnapshot.Empty,
                MustFrame(Signal("route.ui.confirm", InteractionPhase.Started, 0)));

            withSuggestion.AssertIndependentSurfaces(1, 1, 0, 0, new[] { 0 }, new[] { "context.ui" });
            Assert.That(withSuggestion.EmittedEvents.Single().RouteId.Value, Is.EqualTo("route.ui.confirm"));
            Assert.That(withoutSuggestion.EmittedEvents.Single().RouteId.Value, Is.EqualTo("route.ui.confirm"));
            Assert.That(suggestion.OpaqueProposedBinding.Count, Is.GreaterThan(0));
            Assert.That(registry.TryGetBindingSuggestion(suggestion.Id, out _), Is.True);
        }

        [Test]
        [Category("verification:core:policy")]
        public void Acceptance_PolicyToggleHoldAndDisable_SuppressOrTransformWithoutRenamingIntent()
        {
            var registry = BuildBasicRegistry();
            var active = new[] { MustContextId("context.ui") };
            var toggleSession = new InteractionRoutingSession();
            var togglePolicy = MustPolicy(
                new[] { new IntentPolicyEntry(MustIntentId("ui.confirm"), InteractionActivationMode.Toggle, true) },
                Array.Empty<RoutePolicyEntry>());

            RoutingEvidenceSnapshot.Route(
                registry, active, togglePolicy, MustFrame(Signal("route.ui.confirm", InteractionPhase.Started, 0)), session: toggleSession);
            var suppressedPerformed = RoutingEvidenceSnapshot.Route(
                registry,
                active,
                togglePolicy,
                MustFrame(Signal("route.ui.confirm", InteractionPhase.Performed, 1, true)),
                session: toggleSession);

            suppressedPerformed.AssertIndependentSurfaces(0, 1, 1, 0, new[] { 1 }, new[] { "context.ui" });
            suppressedPerformed.AssertDiagnostic(
                InteractionDiagnosticKind.PolicyApplied,
                InteractionValidationCode.None,
                1);
            suppressedPerformed.AssertNoEmittedEventMatches(
                e => e.Phase == InteractionPhase.Performed,
                "Toggle policy must suppress first performed.");

            var disabledPolicy = MustPolicy(
                Array.Empty<IntentPolicyEntry>(),
                new[] { new RoutePolicyEntry(MustRouteId("route.ui.confirm"), false, 1, false) });
            var disabled = RoutingEvidenceSnapshot.Route(
                registry,
                active,
                disabledPolicy,
                MustFrame(Signal("route.ui.confirm", InteractionPhase.Started, 10)));
            disabled.AssertIndependentSurfaces(0, 1, 1, 0, new[] { 10 }, new[] { "context.ui" });
            disabled.AssertDiagnostic(
                InteractionDiagnosticKind.DisabledRoute,
                InteractionValidationCode.DisabledRoute,
                10);
            Assert.That(
                disabled.EmittedEvents.All(e => e.IntentId.Value == "ui.confirm"),
                Is.True,
                "Disabled route must not rename intent identity.");
        }

        [Test]
        [Category("verification:core:route-source-identity")]
        public void Acceptance_RouteAdmissionUsesRouteIdWhilePreservingObservedSourceId()
        {
            var registry = BuildBasicRegistry();
            var evidence = RoutingEvidenceSnapshot.Route(
                registry,
                new[] { MustContextId("context.ui") },
                InteractionPolicySnapshot.Empty,
                MustFrame(new SourceSignal(
                    MustRouteId("route.ui.confirm"),
                    MustSourceId("source.gamepad.a"),
                    InteractionModality.Gamepad,
                    InteractionCapability.Digital,
                    InteractionValue.FromButton(true),
                    InteractionPhase.Started,
                    1,
                    0)));

            evidence.AssertIndependentSurfaces(1, 1, 0, 0, new[] { 0 }, new[] { "context.ui" });
            Assert.That(evidence.EmittedEvents.Single().SourceId.Value, Is.EqualTo("source.gamepad.a"));
            Assert.That(evidence.EmittedEvents.Single().RouteId.Value, Is.EqualTo("route.ui.confirm"));
            Assert.That(registry.TryGetRoute(MustContextId("context.ui"), MustRouteId("route.ui.confirm"), out var route), Is.True);
            Assert.That(route.SourceSelector.Value, Is.EqualTo("source.keyboard.space"));
        }

        [Test]
        [Category("verification:core:gaze-boundary")]
        public void Acceptance_GazePointingWithoutDigitalActivation_FailsClosedIndependently()
        {
            var registry = BuildGazeRegistry();
            var evidence = RoutingEvidenceSnapshot.Route(
                registry,
                new[] { MustContextId("context.ui") },
                InteractionPolicySnapshot.Empty,
                MustFrame(new SourceSignal(
                    MustRouteId("route.ui.confirm"),
                    MustSourceId("source.gaze.reticle"),
                    InteractionModality.Gaze,
                    InteractionCapability.Pointing,
                    InteractionValue.FromButton(true),
                    InteractionPhase.Performed,
                    1,
                    0)));

            evidence.AssertIndependentSurfaces(0, 1, 1, 0, new[] { 0 }, new[] { "context.ui" });
            evidence.AssertDiagnostic(
                InteractionDiagnosticKind.ValidationFailure,
                InteractionValidationCode.CapabilityMismatch,
                0);
            evidence.AssertNoEmittedEventMatches(
                e => e.Modality == InteractionModality.Gaze && e.Phase == InteractionPhase.Performed,
                "Gaze pointing must not transfer digital activation evidence.");
        }

        private static InteractionRegistry BuildBasicRegistry()
        {
            var confirmIntent = MustIntent("ui.confirm", InteractionValueKind.Button, InteractionCapability.Digital, 0);
            var cancelIntent = MustIntent("ui.cancel", InteractionValueKind.Button, InteractionCapability.Digital, 1);
            var confirmRoute = MustRoute(
                "route.ui.confirm",
                "context.ui",
                "ui.confirm",
                "source.keyboard.space",
                InteractionModality.KeyboardMouse,
                0);
            var cancelRoute = MustRoute(
                "route.ui.cancel",
                "context.ui",
                "ui.cancel",
                "source.keyboard.escape",
                InteractionModality.KeyboardMouse,
                1);
            var context = MustContext("context.ui", 0, new[] { confirmRoute.Id, cancelRoute.Id });
            return MustRegistry(
                new[] { confirmIntent, cancelIntent },
                new[] { context },
                new[] { confirmRoute, cancelRoute });
        }

        private static InteractionRegistry BuildOverlappingRegistry(bool samePriority)
        {
            var intent = MustIntent("ui.confirm", InteractionValueKind.Button, InteractionCapability.Digital, 0);
            var lowRoute = MustRoute(
                "route.ui.confirm",
                "context.low",
                "ui.confirm",
                "source.shared.button",
                InteractionModality.Gamepad,
                0);
            var highRoute = MustRoute(
                "route.ui.confirm",
                "context.high",
                "ui.confirm",
                "source.shared.button",
                InteractionModality.Gamepad,
                1);
            var lowContext = MustContext("context.low", samePriority ? 5 : 1, new[] { lowRoute.Id });
            var highContext = MustContext("context.high", 5, new[] { highRoute.Id });
            return MustRegistry(new[] { intent }, new[] { lowContext, highContext }, new[] { lowRoute, highRoute });
        }

        private static InteractionRegistry BuildMultiModalRegistry()
        {
            var keyboardIntent = MustIntent("ui.confirm", InteractionValueKind.Button, InteractionCapability.Digital, 0);
            var gamepadIntent = MustIntent("ui.confirm.gamepad", InteractionValueKind.Button, InteractionCapability.Digital, 1);
            var keyboardRoute = MustRoute(
                "route.ui.confirm.keyboard",
                "context.ui",
                "ui.confirm",
                "source.keyboard.space",
                InteractionModality.KeyboardMouse,
                0);
            var gamepadRoute = MustRoute(
                "route.ui.confirm.gamepad",
                "context.ui",
                "ui.confirm.gamepad",
                "source.gamepad.a",
                InteractionModality.Gamepad,
                1);
            var context = MustContext("context.ui", 0, new[] { keyboardRoute.Id, gamepadRoute.Id });
            return MustRegistry(
                new[] { keyboardIntent, gamepadIntent },
                new[] { context },
                new[] { keyboardRoute, gamepadRoute });
        }

        private static InteractionRegistry BuildRegistryWithSuggestion()
        {
            var registry = BuildBasicRegistry();
            var suggestion = MustSuggestion(
                "suggestion.ui.confirm.keyboard",
                "ui.confirm",
                "route.ui.confirm",
                "unity.inputsystem",
                new byte[] { 0x01, 0x02 });
            return MustRegistry(registry.Intents, registry.Contexts, registry.Routes, new[] { suggestion });
        }

        private static InteractionRegistry BuildGazeRegistry()
        {
            var intent = MustIntent("ui.confirm", InteractionValueKind.Button, InteractionCapability.Digital, 0);
            var route = MustRoute(
                "route.ui.confirm",
                "context.ui",
                "ui.confirm",
                "source.gaze.reticle",
                InteractionModality.Gaze,
                0,
                InteractionCapability.Pointing);
            var context = MustContext("context.ui", 0, new[] { route.Id });
            return MustRegistry(new[] { intent }, new[] { context }, new[] { route });
        }

        private static SourceSignal KeyboardSignal(InteractionPhase phase, int sequence) =>
            new SourceSignal(
                MustRouteId("route.ui.confirm.keyboard"),
                MustSourceId("source.keyboard.space"),
                InteractionModality.KeyboardMouse,
                InteractionCapability.Digital,
                InteractionValue.FromButton(true),
                phase,
                sequence,
                sequence);

        private static SourceSignal GamepadSignal(InteractionPhase phase, int sequence) =>
            new SourceSignal(
                MustRouteId("route.ui.confirm.gamepad"),
                MustSourceId("source.gamepad.a"),
                InteractionModality.Gamepad,
                InteractionCapability.Digital,
                InteractionValue.FromButton(true),
                phase,
                sequence,
                sequence);

        private static SourceSignal Signal(string routeId, InteractionPhase phase, int sequence, bool pressed = true)
        {
            return new SourceSignal(
                MustRouteId(routeId),
                routeId.Contains("cancel") ? MustSourceId("source.keyboard.escape") : MustSourceId("source.keyboard.space"),
                InteractionModality.KeyboardMouse,
                InteractionCapability.Digital,
                InteractionValue.FromButton(pressed),
                phase,
                sequence,
                sequence);
        }

        private static InteractionFrame MustFrame(params SourceSignal[] signals)
        {
            var frame = InteractionFrame.Create(signals);
            Assert.That(frame.Succeeded, Is.True, frame.Error.Message);
            return frame.Value;
        }

        private static InteractionPolicySnapshot MustPolicy(
            IEnumerable<IntentPolicyEntry> intentPolicies,
            IEnumerable<RoutePolicyEntry> routePolicies)
        {
            var policy = InteractionPolicySnapshot.Create(intentPolicies, routePolicies);
            Assert.That(policy.Succeeded, Is.True, policy.Error.Message);
            return policy.Value;
        }

        private static IntentDefinition MustIntent(
            string id,
            InteractionValueKind kind,
            InteractionCapability capabilities,
            int dispatchOrder)
        {
            var result = IntentDefinition.Create(MustIntentId(id), kind, capabilities, dispatchOrder);
            Assert.That(result.Succeeded, Is.True, result.Error.Message);
            return result.Value;
        }

        private static InteractionRoute MustRoute(
            string routeId,
            string contextId,
            string intentId,
            string sourceId,
            InteractionModality modality,
            int routeOrder,
            InteractionCapability capabilities = InteractionCapability.Digital)
        {
            var result = InteractionRoute.Create(
                MustRouteId(routeId),
                MustContextId(contextId),
                MustIntentId(intentId),
                MustSourceId(sourceId),
                modality,
                capabilities,
                new byte[] { 0x10 },
                routeOrder);
            Assert.That(result.Succeeded, Is.True, result.Error.Message);
            return result.Value;
        }

        private static InteractionContextDefinition MustContext(string id, int priority, IEnumerable<RouteId> routes)
        {
            var result = InteractionContextDefinition.Create(MustContextId(id), priority, routes);
            Assert.That(result.Succeeded, Is.True, result.Error.Message);
            return result.Value;
        }

        private static BindingSuggestion MustSuggestion(
            string id,
            string intentId,
            string routeId,
            string adapterKind,
            byte[] binding)
        {
            var result = BindingSuggestion.Create(
                MustBindingSuggestionId(id),
                MustIntentId(intentId),
                MustRouteId(routeId),
                adapterKind,
                binding);
            Assert.That(result.Succeeded, Is.True, result.Error.Message);
            return result.Value;
        }

        private static InteractionRegistry MustRegistry(
            IEnumerable<IntentDefinition> intents,
            IEnumerable<InteractionContextDefinition> contexts,
            IEnumerable<InteractionRoute> routes,
            IEnumerable<BindingSuggestion> suggestions = null)
        {
            var result = InteractionRegistry.Create(intents, contexts, routes, suggestions);
            Assert.That(result.Succeeded, Is.True, result.Error.Message);
            return result.Value;
        }

        private static IntentId MustIntentId(string value) => IntentId.TryCreate(value).Value;
        private static ContextId MustContextId(string value) => ContextId.TryCreate(value).Value;
        private static RouteId MustRouteId(string value) => RouteId.TryCreate(value).Value;
        private static SourceId MustSourceId(string value) => SourceId.TryCreate(value).Value;
        private static BindingSuggestionId MustBindingSuggestionId(string value) => BindingSuggestionId.TryCreate(value).Value;
    }
}
