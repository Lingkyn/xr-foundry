using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Lingkyn.Interaction.Core.Editor.Tests
{
    public sealed class InteractionCoreContractTests
    {
        [Test]
        public void IdentitiesRejectInvalidDefaultAndDuplicateRegistryEntries()
        {
            Assert.That(IntentId.TryCreate(string.Empty).Succeeded, Is.False);
            Assert.That(IntentId.TryCreate("default").Succeeded, Is.False);
            Assert.That(IntentId.TryCreate("inventory.open").Succeeded, Is.True);
            Assert.That(RouteId.TryCreate("route.ui.confirm").Succeeded, Is.True);
            Assert.That(ContextId.TryCreate("context.gameplay").Succeeded, Is.True);
            Assert.That(SourceId.TryCreate("source.keyboard.space").Succeeded, Is.True);
            Assert.That(BindingSuggestionId.TryCreate("default").Succeeded, Is.False);

            var duplicateContext = InteractionRegistry.Create(
                new[] { MustIntent("ui.confirm", InteractionValueKind.Button, InteractionCapability.Digital, 0) },
                new[]
                {
                    MustContext("context.ui", 0, Array.Empty<RouteId>()),
                    MustContext("context.ui", 1, Array.Empty<RouteId>()),
                },
                Array.Empty<InteractionRoute>());
            Assert.That(duplicateContext.Succeeded, Is.False);
            Assert.That(duplicateContext.Error.Code, Is.EqualTo(InteractionValidationCode.DuplicateIdentity));

            var intent = MustIntent("ui.confirm", InteractionValueKind.Button, InteractionCapability.Digital, 0);
            var duplicateIntentRegistry = InteractionRegistry.Create(
                new[] { intent, intent },
                Array.Empty<InteractionContextDefinition>(),
                Array.Empty<InteractionRoute>());
            Assert.That(duplicateIntentRegistry.Succeeded, Is.False);
            Assert.That(duplicateIntentRegistry.Error.Code, Is.EqualTo(InteractionValidationCode.DuplicateIdentity));
        }

        [Test]
        public void DefinitionRejectsKindCapabilityMismatchAndNonFiniteValues()
        {
            var mismatch = IntentDefinition.Create(
                MustIntentId("ui.confirm"),
                InteractionValueKind.Button,
                InteractionCapability.Scalar,
                0);
            Assert.That(mismatch.Succeeded, Is.False);
            Assert.That(mismatch.Error.Code, Is.EqualTo(InteractionValidationCode.CapabilityMismatch));

            var scalarValidation = InteractionValue.Validate(
                InteractionValueKind.Scalar,
                InteractionValue.FromScalar(double.PositiveInfinity));
            Assert.That(scalarValidation.Succeeded, Is.False);
            Assert.That(scalarValidation.Error.Code, Is.EqualTo(InteractionValidationCode.NonFiniteValue));

            var poseValidation = InteractionValue.Validate(
                InteractionValueKind.Pose,
                InteractionValue.FromPose(new InteractionPose(
                    new InteractionVector3(double.NaN, 0, 0),
                    new InteractionQuaternion(0, 0, 0, 1),
                    true,
                    true)));
            Assert.That(poseValidation.Succeeded, Is.False);
            Assert.That(poseValidation.Error.Code, Is.EqualTo(InteractionValidationCode.InvalidPose));
        }

        [Test]
        public void InactiveContextRejectsRouteDispatch()
        {
            var registry = BuildBasicRegistry();
            var frame = MustFrame(new SourceSignal(
                MustRouteId("route.ui.confirm"),
                MustSourceId("source.keyboard.space"),
                InteractionModality.KeyboardMouse,
                InteractionCapability.Digital,
                InteractionValue.FromButton(true),
                InteractionPhase.Started,
                1,
                0));

            var result = Route(
                registry,
                Array.Empty<ContextId>(),
                InteractionPolicySnapshot.Empty,
                frame);

            Assert.That(result.Dispatches.Single().Status, Is.EqualTo(InteractionDispatchStatus.Rejected));
            Assert.That(result.Diagnostics.Any(d => d.Code == InteractionValidationCode.InactiveContext), Is.True);
            Assert.That(result.Events, Is.Empty);
        }

        [Test]
        public void HigherPriorityContextShadowsLowerPrioritySourceRoute()
        {
            var registry = BuildOverlappingRegistry(samePriority: false);
            var frame = MustFrame(
                new SourceSignal(
                    MustRouteId("route.ui.confirm"),
                    MustSourceId("source.shared.button"),
                    InteractionModality.Gamepad,
                    InteractionCapability.Digital,
                    InteractionValue.FromButton(true),
                    InteractionPhase.Started,
                    1,
                    0));

            var result = Route(
                registry,
                new[] { MustContextId("context.low"), MustContextId("context.high") },
                InteractionPolicySnapshot.Empty,
                frame);

            Assert.That(result.Dispatches.Any(d => d.Status == InteractionDispatchStatus.Shadowed), Is.True);
            Assert.That(result.Dispatches.Any(d =>
                d.RouteId.Value == "route.ui.confirm"
                && d.ContextId.Value == "context.high"
                && d.Status == InteractionDispatchStatus.Routed), Is.True);
            Assert.That(result.ActiveContexts.ActiveContexts.Select(c => c.Value), Is.EqualTo(new[] { "context.high", "context.low" }));
            Assert.That(result.Diagnostics.Any(d => d.Kind == InteractionDiagnosticKind.ShadowedRoute), Is.True);
        }

        [Test]
        public void EqualPriorityCollisionProducesAmbiguousDiagnostics()
        {
            var registry = BuildOverlappingRegistry(samePriority: true);
            var frame = MustFrame(
                new SourceSignal(
                    MustRouteId("route.ui.confirm"),
                    MustSourceId("source.shared.button"),
                    InteractionModality.Gamepad,
                    InteractionCapability.Digital,
                    InteractionValue.FromButton(true),
                    InteractionPhase.Started,
                    1,
                    0));

            var result = Route(
                registry,
                new[] { MustContextId("context.low"), MustContextId("context.high") },
                InteractionPolicySnapshot.Empty,
                frame);

            Assert.That(result.Dispatches.Single().Status, Is.EqualTo(InteractionDispatchStatus.Ambiguous));
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo(InteractionValidationCode.AmbiguousContextCollision));
            Assert.That(result.Events, Is.Empty);
        }

        [Test]
        public void FrameRejectsNonMonotonicIngressSequence()
        {
            var frame = InteractionFrame.Create(new[]
            {
                new SourceSignal(
                    MustRouteId("route.ui.confirm"),
                    MustSourceId("source.keyboard.space"),
                    InteractionModality.KeyboardMouse,
                    InteractionCapability.Digital,
                    InteractionValue.FromButton(true),
                    InteractionPhase.Started,
                    1,
                    1),
                new SourceSignal(
                    MustRouteId("route.ui.confirm"),
                    MustSourceId("source.keyboard.space"),
                    InteractionModality.KeyboardMouse,
                    InteractionCapability.Digital,
                    InteractionValue.FromButton(true),
                    InteractionPhase.Performed,
                    2,
                    1),
            });

            Assert.That(frame.Succeeded, Is.False);
            Assert.That(frame.Error.Code, Is.EqualTo(InteractionValidationCode.InvalidIngressSequence));
        }

        [Test]
        public void RoutingPreservesIngressOrderAndStableDiagnosticOrder()
        {
            var registry = BuildBasicRegistry();
            var frame = MustFrame(
                new SourceSignal(
                    MustRouteId("route.ui.confirm"),
                    MustSourceId("source.keyboard.space"),
                    InteractionModality.KeyboardMouse,
                    InteractionCapability.Digital,
                    InteractionValue.FromButton(true),
                    InteractionPhase.Started,
                    10,
                    0),
                new SourceSignal(
                    MustRouteId("route.ui.cancel"),
                    MustSourceId("source.keyboard.escape"),
                    InteractionModality.KeyboardMouse,
                    InteractionCapability.Digital,
                    InteractionValue.FromButton(true),
                    InteractionPhase.Started,
                    11,
                    1));

            var result = Route(
                registry,
                new[] { MustContextId("context.ui") },
                InteractionPolicySnapshot.Empty,
                frame);

            Assert.That(result.Dispatches.Select(d => d.IngressSequence), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(result.Events.Select(e => e.IngressSequence), Is.EqualTo(new[] { 0, 1 }));
        }

        [Test]
        public void PhaseTransitionsRejectDuplicateAndInvalidSequences()
        {
            var registry = BuildBasicRegistry();
            var active = new[] { MustContextId("context.ui") };
            var session = new InteractionRoutingSession();

            var started = MustFrame(Signal("route.ui.confirm", InteractionPhase.Started, 0));
            Assert.That(Route(registry, active, InteractionPolicySnapshot.Empty, started, session: session).Events.Count, Is.EqualTo(1));

            var duplicateStarted = MustFrame(Signal("route.ui.confirm", InteractionPhase.Started, 1));
            var duplicateResult = Route(registry, active, InteractionPolicySnapshot.Empty, duplicateStarted, session: session);
            Assert.That(duplicateResult.Dispatches.Single().Status, Is.EqualTo(InteractionDispatchStatus.Rejected));
            Assert.That(duplicateResult.Diagnostics.Single().Code, Is.EqualTo(InteractionValidationCode.DuplicatePhase));

            var performedWithoutStarted = Route(
                registry,
                active,
                InteractionPolicySnapshot.Empty,
                MustFrame(Signal("route.ui.confirm", InteractionPhase.Performed, 2, true)),
                session: new InteractionRoutingSession());
            Assert.That(performedWithoutStarted.Dispatches.Single().Status, Is.EqualTo(InteractionDispatchStatus.Rejected));
            Assert.That(performedWithoutStarted.Diagnostics.Single().Code, Is.EqualTo(InteractionValidationCode.InvalidPhaseTransition));

            var cancelWithoutStarted = Route(
                registry,
                active,
                InteractionPolicySnapshot.Empty,
                MustFrame(Signal("route.ui.cancel", InteractionPhase.Canceled, 0)),
                session: new InteractionRoutingSession());
            Assert.That(cancelWithoutStarted.Dispatches.Single().Status, Is.EqualTo(InteractionDispatchStatus.Rejected));
            Assert.That(cancelWithoutStarted.Diagnostics.Single().Code, Is.EqualTo(InteractionValidationCode.InvalidPhaseTransition));
        }

        [Test]
        public void PerformedDispatchesHandlerOutcomeWithoutRewritingHistory()
        {
            var registry = BuildBasicRegistry();
            var active = new[] { MustContextId("context.ui") };
            var session = new InteractionRoutingSession();
            Route(
                registry,
                active,
                InteractionPolicySnapshot.Empty,
                MustFrame(Signal("route.ui.confirm", InteractionPhase.Started, 0)),
                session: session);

            SemanticInteractionEvent captured = default;
            var result = Route(
                registry,
                active,
                InteractionPolicySnapshot.Empty,
                MustFrame(Signal("route.ui.confirm", InteractionPhase.Performed, 1, true)),
                evt =>
                {
                    captured = evt;
                    return InteractionHandlerOutcome.Accepted;
                },
                session);

            var dispatch = result.Dispatches.Single();
            Assert.That(dispatch.Status, Is.EqualTo(InteractionDispatchStatus.HandlerOutcome));
            Assert.That(dispatch.HandlerOutcome, Is.EqualTo(InteractionHandlerOutcome.Accepted));
            Assert.That(captured.IntentId.Value, Is.EqualTo("ui.confirm"));
            Assert.That(result.Diagnostics.Any(d => d.Kind == InteractionDiagnosticKind.HandlerResult), Is.True);
        }

        [Test]
        public void HandlerRecordsRejectedDeferredAndFailedOutcomes()
        {
            var registry = BuildBasicRegistry();
            var active = new[] { MustContextId("context.ui") };
            var session = new InteractionRoutingSession();
            Route(registry, active, InteractionPolicySnapshot.Empty, MustFrame(Signal("route.ui.confirm", InteractionPhase.Started, 0)), session: session);

            foreach (var outcome in new[]
                     {
                         InteractionHandlerOutcome.Rejected,
                         InteractionHandlerOutcome.Deferred,
                         InteractionHandlerOutcome.Failed,
                     })
            {
                var localSession = new InteractionRoutingSession();
                Route(registry, active, InteractionPolicySnapshot.Empty, MustFrame(Signal("route.ui.confirm", InteractionPhase.Started, 10)), session: localSession);
                var result = Route(
                    registry,
                    active,
                    InteractionPolicySnapshot.Empty,
                    MustFrame(Signal("route.ui.confirm", InteractionPhase.Performed, 11, true)),
                    _ => outcome,
                    localSession);

                Assert.That(result.Dispatches.Single().HandlerOutcome, Is.EqualTo(outcome));
                Assert.That(result.Events.Single().Phase, Is.EqualTo(InteractionPhase.Performed));
                Assert.That(result.Diagnostics.Any(d => d.Kind == InteractionDiagnosticKind.HandlerResult), Is.True);
            }
        }

        [Test]
        public void CancellationBeforeCompletionEmitsCanceledWithoutHandlerPerformed()
        {
            var registry = BuildBasicRegistry();
            var active = new[] { MustContextId("context.ui") };
            var session = new InteractionRoutingSession();
            Route(registry, active, InteractionPolicySnapshot.Empty, MustFrame(Signal("route.ui.confirm", InteractionPhase.Started, 0)), session: session);

            var handlerCalls = 0;
            var result = Route(
                registry,
                active,
                InteractionPolicySnapshot.Empty,
                MustFrame(Signal("route.ui.confirm", InteractionPhase.Canceled, 1)),
                _ =>
                {
                    handlerCalls++;
                    return InteractionHandlerOutcome.Accepted;
                },
                session);

            Assert.That(result.Dispatches.Single().Status, Is.EqualTo(InteractionDispatchStatus.Canceled));
            Assert.That(result.Events.Single().Phase, Is.EqualTo(InteractionPhase.Canceled));
            Assert.That(handlerCalls, Is.EqualTo(0));
        }

        [Test]
        public void MultiModalSourcesDoNotTransferEvidence()
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
            var registry = MustRegistry(
                new[] { keyboardIntent, gamepadIntent },
                new[] { context },
                new[] { keyboardRoute, gamepadRoute });

            var frame = MustFrame(
                new SourceSignal(
                    keyboardRoute.Id,
                    MustSourceId("source.keyboard.space"),
                    InteractionModality.KeyboardMouse,
                    InteractionCapability.Digital,
                    InteractionValue.FromButton(true),
                    InteractionPhase.Started,
                    1,
                    0),
                new SourceSignal(
                    gamepadRoute.Id,
                    MustSourceId("source.gamepad.a"),
                    InteractionModality.Gamepad,
                    InteractionCapability.Digital,
                    InteractionValue.FromButton(true),
                    InteractionPhase.Started,
                    2,
                    1));

            var result = Route(
                registry,
                new[] { MustContextId("context.ui") },
                InteractionPolicySnapshot.Empty,
                frame);

            Assert.That(result.Events.Select(e => e.Modality), Is.EqualTo(new[]
            {
                InteractionModality.KeyboardMouse,
                InteractionModality.Gamepad,
            }));
            Assert.That(result.Events.Select(e => e.IntentId.Value), Is.EqualTo(new[]
            {
                "ui.confirm",
                "ui.confirm.gamepad",
            }));
        }

        [Test]
        public void PublicCollectionsAreImmutableAgainstMutation()
        {
            var registry = BuildBasicRegistry();
            Assert.Throws<NotSupportedException>(() => ((IList)registry.Intents).Add(null));
            Assert.Throws<NotSupportedException>(() => ((IList)registry.Routes).Clear());

            var policy = MustPolicy(
                new[] { new IntentPolicyEntry(MustIntentId("ui.confirm"), InteractionActivationMode.Toggle, true) },
                Array.Empty<RoutePolicyEntry>());
            Assert.Throws<NotSupportedException>(() => ((IList)policy.IntentPolicies).Add(default));

            var frame = MustFrame(Signal("route.ui.confirm", InteractionPhase.Started, 0));
            Assert.Throws<NotSupportedException>(() => ((IList)frame.Signals).Add(default));
        }

        [Test]
        public void BindingSuggestionsRemainInertAndUseStableRouteIdentity()
        {
            var registry = BuildRegistryWithSuggestion();
            var suggestion = registry.BindingSuggestions.Single();
            Assert.That(suggestion.RouteId.Value, Is.EqualTo("route.ui.confirm"));
            Assert.That(suggestion.OpaqueProposedBinding.Count, Is.GreaterThan(0));

            var result = Route(
                registry,
                new[] { MustContextId("context.ui") },
                InteractionPolicySnapshot.Empty,
                MustFrame(Signal("route.ui.confirm", InteractionPhase.Started, 0)));
            Assert.That(result.Events, Has.Count.EqualTo(1));
            Assert.That(registry.TryGetBindingSuggestion(suggestion.Id, out _), Is.True);
        }

        [Test]
        public void PolicyAppliesToggleHoldAndRouteDisableWithoutRenamingIntent()
        {
            var registry = BuildBasicRegistry();
            var active = new[] { MustContextId("context.ui") };
            var toggleSession = new InteractionRoutingSession();

            var togglePolicy = MustPolicy(
                new[] { new IntentPolicyEntry(MustIntentId("ui.confirm"), InteractionActivationMode.Toggle, true) },
                Array.Empty<RoutePolicyEntry>());
            Route(registry, active, togglePolicy, MustFrame(Signal("route.ui.confirm", InteractionPhase.Started, 0)), session: toggleSession);
            var firstPerformed = Route(
                registry,
                active,
                togglePolicy,
                MustFrame(Signal("route.ui.confirm", InteractionPhase.Performed, 1, true)),
                session: toggleSession);
            Assert.That(firstPerformed.Events, Is.Empty);
            Assert.That(firstPerformed.Diagnostics.Any(d => d.Kind == InteractionDiagnosticKind.PolicyApplied), Is.True);

            var secondPerformed = Route(
                registry,
                active,
                togglePolicy,
                MustFrame(
                    Signal("route.ui.confirm", InteractionPhase.Started, 2),
                    Signal("route.ui.confirm", InteractionPhase.Performed, 3, true)),
                session: toggleSession);
            Assert.That(secondPerformed.Events.Single(e => e.Phase == InteractionPhase.Performed).IntentId.Value, Is.EqualTo("ui.confirm"));
            Assert.That(secondPerformed.Events.Single(e => e.Phase == InteractionPhase.Performed).ActivationMode, Is.EqualTo(InteractionActivationMode.Toggle));

            var holdPolicy = MustPolicy(
                new[] { new IntentPolicyEntry(MustIntentId("ui.confirm"), InteractionActivationMode.Hold, true) },
                Array.Empty<RoutePolicyEntry>());
            var holdSession = new InteractionRoutingSession();
            Route(registry, active, holdPolicy, MustFrame(Signal("route.ui.confirm", InteractionPhase.Started, 10)), session: holdSession);
            var holdPerformed = Route(
                registry,
                active,
                holdPolicy,
                MustFrame(Signal("route.ui.confirm", InteractionPhase.Performed, 11, true)),
                session: holdSession);
            Assert.That(holdPerformed.Events.Single().ActivationMode, Is.EqualTo(InteractionActivationMode.Hold));

            var disabledPolicy = MustPolicy(
                Array.Empty<IntentPolicyEntry>(),
                new[] { new RoutePolicyEntry(MustRouteId("route.ui.confirm"), false, 1, false) });
            var disabled = Route(
                registry,
                active,
                disabledPolicy,
                MustFrame(Signal("route.ui.confirm", InteractionPhase.Started, 20)));
            Assert.That(disabled.Dispatches.Single().Status, Is.EqualTo(InteractionDispatchStatus.Rejected));
            Assert.That(disabled.Diagnostics.Single().Code, Is.EqualTo(InteractionValidationCode.DisabledRoute));
        }

        [Test]
        public void RegistryAllowsSharedRouteIdAcrossContextsAndRejectsOrphanRoutes()
        {
            var intent = MustIntent("ui.confirm", InteractionValueKind.Button, InteractionCapability.Digital, 0);
            var route = MustRoute("route.ui.confirm", "context.ui", "ui.confirm", "source.shared.button", InteractionModality.Gamepad, 0);
            var shared = MustRoute("route.ui.confirm", "context.other", "ui.confirm", "source.shared.button", InteractionModality.Gamepad, 1);
            var sharedRegistry = MustRegistry(
                new[] { intent },
                new[]
                {
                    MustContext("context.ui", 0, new[] { route.Id }),
                    MustContext("context.other", 1, new[] { shared.Id }),
                },
                new[] { route, shared });
            Assert.That(sharedRegistry.TryGetRoutes(MustRouteId("route.ui.confirm"), out var routes), Is.True);
            Assert.That(routes.Count, Is.EqualTo(2));

            var orphanRouteRegistry = InteractionRegistry.Create(
                new[] { intent },
                new[] { MustContext("context.ui", 0, new[] { route.Id }) },
                new[] { route, MustRoute("route.ui.confirm", "context.other", "ui.confirm", "source.shared.button", InteractionModality.Gamepad, 1) });
            Assert.That(orphanRouteRegistry.Succeeded, Is.False);
            Assert.That(orphanRouteRegistry.Error.Code, Is.EqualTo(InteractionValidationCode.UnknownContext));
        }

        [Test]
        public void BindingOverridesRemainInertDuringRouting()
        {
            var registry = BuildBasicRegistry();
            var overrides = MustOverrides(BindingOverride.Create(
                MustIntentId("ui.confirm"),
                MustRouteId("route.ui.confirm"),
                "unity.inputsystem",
                new byte[] { 0x42 }).Value);

            var withoutOverride = Route(
                registry,
                new[] { MustContextId("context.ui") },
                InteractionPolicySnapshot.Empty,
                MustFrame(Signal("route.ui.confirm", InteractionPhase.Started, 0)));

            Assert.That(overrides.Overrides.Single().OpaqueAdapterRouteToken.Count, Is.EqualTo(1));
            Assert.That(withoutOverride.Events.Single().RouteId.Value, Is.EqualTo("route.ui.confirm"));
        }

        [Test]
        public void RouteAdmissionUsesRouteIdWithoutRewritingObservedSourceId()
        {
            var registry = BuildBasicRegistry();
            var frame = MustFrame(new SourceSignal(
                MustRouteId("route.ui.confirm"),
                MustSourceId("source.gamepad.a"),
                InteractionModality.Gamepad,
                InteractionCapability.Digital,
                InteractionValue.FromButton(true),
                InteractionPhase.Started,
                1,
                0));

            var result = Route(
                registry,
                new[] { MustContextId("context.ui") },
                InteractionPolicySnapshot.Empty,
                frame);

            Assert.That(result.Dispatches.Single().Status, Is.EqualTo(InteractionDispatchStatus.Routed));
            Assert.That(result.Events.Single().SourceId.Value, Is.EqualTo("source.gamepad.a"));
            Assert.That(result.Events.Single().RouteId.Value, Is.EqualTo("route.ui.confirm"));
            Assert.That(registry.TryGetRoute(MustContextId("context.ui"), MustRouteId("route.ui.confirm"), out var route), Is.True);
            Assert.That(route.SourceSelector.Value, Is.EqualTo("source.keyboard.space"));
        }

        [Test]
        public void GazePointingWithoutDigitalActivationFailsClosed()
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
            var registry = MustRegistry(new[] { intent }, new[] { context }, new[] { route });
            var frame = MustFrame(new SourceSignal(
                route.Id,
                MustSourceId("source.gaze.reticle"),
                InteractionModality.Gaze,
                InteractionCapability.Pointing,
                InteractionValue.FromButton(true),
                InteractionPhase.Performed,
                1,
                0));

            var result = Route(registry, new[] { MustContextId("context.ui") }, InteractionPolicySnapshot.Empty, frame);
            Assert.That(result.Events, Is.Empty);
            Assert.That(result.Dispatches.Single().Status, Is.EqualTo(InteractionDispatchStatus.Rejected));
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo(InteractionValidationCode.CapabilityMismatch));
        }

        [Test]
        public void InterleavedSourceRoutesPreserveIngressOrder()
        {
            var registry = BuildBasicRegistry();
            var frame = MustFrame(
                Signal("route.ui.confirm", InteractionPhase.Started, 0),
                Signal("route.ui.cancel", InteractionPhase.Started, 1),
                Signal("route.ui.confirm", InteractionPhase.Performed, 2, true));

            var result = Route(registry, new[] { MustContextId("context.ui") }, InteractionPolicySnapshot.Empty, frame);
            Assert.That(result.Dispatches.Select(d => d.IngressSequence), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(result.Events.Select(e => e.IngressSequence), Is.EqualTo(new[] { 0, 1, 2 }));
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

        private static InteractionRoutingResult Route(
            InteractionRegistry registry,
            IEnumerable<ContextId> active,
            InteractionPolicySnapshot policy,
            InteractionFrame frame,
            InteractionIntentHandler handler = null,
            InteractionRoutingSession session = null)
        {
            return new InteractionCoordinator(registry, active, policy, session).RouteFrame(frame, handler);
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

        private static InteractionBindingOverrideSet MustOverrides(params BindingOverride[] overrides)
        {
            var result = InteractionBindingOverrideSet.Create(overrides);
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
