using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Lingkyn.Interaction.Core.Editor.Tests
{
    public sealed class InteractionCoreTests
    {
        [Test]
        public void RegistryRejectsDuplicateRouteAndMismatchedMembership()
        {
            var intent = Intent("ui.confirm", InteractionValueKind.Button, InteractionCapability.Digital);
            var a = Route("route.a", "context.a", "ui.confirm", "source.shared", 0);
            var duplicate = Route("route.a", "context.b", "ui.confirm", "source.shared", 0);
            var result = InteractionRegistry.Create(new[] { intent },
                new[] { Context("context.a", 0, a.Id), Context("context.b", 1, duplicate.Id) }, new[] { a, duplicate });
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(InteractionValidationCode.DuplicateIdentity));

            var missing = InteractionRegistry.Create(new[] { intent },
                new[] { Context("context.a", 0) }, new[] { a });
            Assert.That(missing.Succeeded, Is.False);
            Assert.That(missing.Error.Code, Is.EqualTo(InteractionValidationCode.InvalidDefinition));
        }

        [Test]
        public void FrameEnforcesObservationAndIngressContract()
        {
            var first = Signal("route.a", "source.shared", InteractionPhase.Started, 10, 0, 0);
            var candidate = Signal("route.b", "source.shared", InteractionPhase.Started, 10, 1, 0);
            Assert.That(InteractionFrame.Create(new[] { first, candidate }).Succeeded, Is.True);

            var inconsistent = Signal("route.b", "source.shared", InteractionPhase.Performed, 10, 1, 0);
            Assert.That(InteractionFrame.Create(new[] { first, inconsistent }).Succeeded, Is.False);
            var regressed = Signal("route.b", "source.shared", InteractionPhase.Started, 9, 1, 1);
            Assert.That(InteractionFrame.Create(new[] { first, regressed }).Succeeded, Is.False);
            var duplicateIngress = Signal("route.b", "source.shared", InteractionPhase.Started, 10, 0, 0);
            Assert.That(InteractionFrame.Create(new[] { first, duplicateIngress }).Succeeded, Is.False);
        }

        [Test]
        public void PriorityAndCollisionApplyOnlyWithinOneObservation()
        {
            var high = Route("route.high", "context.high", "ui.confirm", "source.shared", 0);
            var low = Route("route.low", "context.low", "ui.confirm", "source.shared", 0);
            var registry = Registry(new[] { high, low },
                Context("context.high", 10, high.Id), Context("context.low", 1, low.Id));
            var shared = Frame(
                Signal("route.low", "source.shared", InteractionPhase.Started, 10, 0, 0),
                Signal("route.high", "source.shared", InteractionPhase.Started, 10, 1, 0));
            var result = RouteFrame(registry, shared, InteractionRoutingState.Empty, null,
                Id<ContextId>("context.high"), Id<ContextId>("context.low"));
            Assert.That(result.Dispatches.Count(x => x.Status == InteractionDispatchStatus.Shadowed), Is.EqualTo(1));
            Assert.That(result.Events.Single().RouteId.Value, Is.EqualTo("route.high"));

            var separate = Frame(
                Signal("route.low", "source.shared", InteractionPhase.Started, 10, 0, 0),
                Signal("route.high", "source.shared", InteractionPhase.Started, 11, 1, 1));
            var separateResult = RouteFrame(registry, separate, InteractionRoutingState.Empty, null,
                Id<ContextId>("context.high"), Id<ContextId>("context.low"));
            Assert.That(separateResult.Events.Count, Is.EqualTo(2));

            var equalRegistry = Registry(new[] { high, low },
                Context("context.high", 5, high.Id), Context("context.low", 5, low.Id));
            var ambiguous = RouteFrame(equalRegistry, shared, InteractionRoutingState.Empty, null,
                Id<ContextId>("context.high"), Id<ContextId>("context.low"));
            Assert.That(ambiguous.Events, Is.Empty);
            Assert.That(ambiguous.Dispatches.All(x => x.Status == InteractionDispatchStatus.Ambiguous), Is.True);
        }

        [Test]
        public void RouterIsDeterministicAndDoesNotMutatePriorState()
        {
            var registry = BasicRegistry(); var frame = Frame(Signal("route.confirm", "source.button", InteractionPhase.Started, 10, 0, 0));
            var router = new InteractionRouter();
            var a = router.Route(registry, Active(), InteractionPolicySnapshot.Empty, frame, InteractionRoutingState.Empty);
            var b = router.Route(registry, Active(), InteractionPolicySnapshot.Empty, frame, InteractionRoutingState.Empty);
            Assert.That(a.Events.SequenceEqual(b.Events), Is.True);
            Assert.That(a.Dispatches.SequenceEqual(b.Dispatches), Is.True);
            Assert.That(a.Diagnostics.SequenceEqual(b.Diagnostics), Is.True);
            Assert.That(a.NextState.Equals(b.NextState), Is.True);
            Assert.That(InteractionRoutingState.Empty.PendingPhases, Is.Empty);
            Assert.That(a.NextState.PendingPhases.Count, Is.EqualTo(1));
        }

        [Test]
        public void PerformedAndCanceledRequireExplicitStartedState()
        {
            var registry = BasicRegistry();
            var performed = RouteFrame(registry,
                Frame(Signal("route.confirm", "source.button", InteractionPhase.Performed, 10, 0, 0)),
                InteractionRoutingState.Empty);
            Assert.That(performed.Events, Is.Empty);
            Assert.That(performed.Diagnostics.Single().Code, Is.EqualTo(InteractionValidationCode.InvalidPhaseTransition));

            var started = RouteFrame(registry,
                Frame(Signal("route.confirm", "source.button", InteractionPhase.Started, 10, 0, 0)),
                InteractionRoutingState.Empty);
            var canceled = RouteFrame(registry,
                Frame(Signal("route.confirm", "source.button", InteractionPhase.Canceled, 11, 0, 0)),
                started.NextState);
            Assert.That(canceled.Events.Single().Phase, Is.EqualTo(InteractionPhase.Canceled));
            Assert.That(canceled.NextState.PendingPhases, Is.Empty);
        }

        [Test]
        public void ScopedPendingResetClearsOnlyOwnedRoutesAndPreservesToggleState()
        {
            var first = Route("route.first", "context.first", "ui.confirm", "source.shared", 0);
            var second = Route("route.second", "context.second", "ui.confirm", "source.shared", 0);
            var other = Route("route.other", "context.other", "ui.confirm", "source.other", 0);
            var registry = Registry(
                new[] { first, second, other },
                Context("context.first", 2, first.Id),
                Context("context.second", 1, second.Id),
                Context("context.other", 0, other.Id));
            var toggle = Policy(new IntentPolicyEntry(
                Id<IntentId>("ui.confirm"),
                InteractionActivationMode.Toggle,
                true));
            var coordinator = new InteractionCoordinator(
                registry,
                new[]
                {
                    Id<ContextId>("context.first"),
                    Id<ContextId>("context.second"),
                    Id<ContextId>("context.other"),
                },
                toggle);

            Complete(coordinator, "route.first", "source.shared", 1);
            coordinator.RouteFrame(Frame(Signal("route.first", "source.shared", InteractionPhase.Started, 10, 0, 0)));
            coordinator.RouteFrame(Frame(Signal("route.second", "source.shared", InteractionPhase.Started, 11, 0, 0)));
            coordinator.RouteFrame(Frame(Signal("route.other", "source.other", InteractionPhase.Started, 12, 0, 0)));
            Assert.That(coordinator.State.PendingPhases, Has.Count.EqualTo(3));
            Assert.That(coordinator.State.ToggleStates.Single().Active, Is.True);

            var reset = coordinator.ResetPendingPhases(
                Id<SourceId>("source.shared"),
                new[] { first.Id });

            Assert.That(reset.Succeeded, Is.True, reset.Error.ToString());
            Assert.That(reset.Value, Is.EqualTo(1));
            Assert.That(coordinator.State.PendingPhases.Select(phase => phase.RouteId),
                Is.EquivalentTo(new[] { second.Id, other.Id }));
            Assert.That(coordinator.State.ToggleStates.Single().Active, Is.True);

            var stateBeforeRepeat = coordinator.State;
            var repeated = coordinator.ResetPendingPhases(
                Id<SourceId>("source.shared"),
                new[] { first.Id });
            Assert.That(repeated.Succeeded, Is.True, repeated.Error.ToString());
            Assert.That(repeated.Value, Is.Zero);
            Assert.That(coordinator.State, Is.SameAs(stateBeforeRepeat));
        }

        [Test]
        public void ScopedPendingResetValidatesTheEntireScopeBeforeChangingState()
        {
            var owned = Route("route.owned", "context.owned", "ui.confirm", "source.owner", 0);
            var foreign = Route("route.foreign", "context.foreign", "ui.confirm", "source.foreign", 0);
            var registry = Registry(
                new[] { owned, foreign },
                Context("context.owned", 1, owned.Id),
                Context("context.foreign", 0, foreign.Id));
            var coordinator = new InteractionCoordinator(
                registry,
                new[] { Id<ContextId>("context.owned"), Id<ContextId>("context.foreign") });
            coordinator.RouteFrame(Frame(Signal("route.owned", "source.owner", InteractionPhase.Started, 10, 0, 0)));
            coordinator.RouteFrame(Frame(Signal("route.foreign", "source.foreign", InteractionPhase.Started, 11, 0, 0)));
            var stateBefore = coordinator.State;
            var missing = Id<RouteId>("route.missing");

            var absent = coordinator.ResetPendingPhases(Id<SourceId>("source.owner"), null);
            var empty = coordinator.ResetPendingPhases(Id<SourceId>("source.owner"), Array.Empty<RouteId>());
            var duplicate = coordinator.ResetPendingPhases(
                Id<SourceId>("source.owner"),
                new[] { owned.Id, owned.Id });
            var unknownAfterValid = coordinator.ResetPendingPhases(
                Id<SourceId>("source.owner"),
                new[] { owned.Id, missing });
            var foreignAfterValid = coordinator.ResetPendingPhases(
                Id<SourceId>("source.owner"),
                new[] { owned.Id, foreign.Id });

            Assert.That(absent.Error.Code, Is.EqualTo(InteractionValidationCode.InvalidFrame));
            Assert.That(empty.Error.Code, Is.EqualTo(InteractionValidationCode.InvalidFrame));
            Assert.That(duplicate.Error.Code, Is.EqualTo(InteractionValidationCode.DuplicateIdentity));
            Assert.That(unknownAfterValid.Error.Code, Is.EqualTo(InteractionValidationCode.UnknownRoute));
            Assert.That(foreignAfterValid.Error.Code, Is.EqualTo(InteractionValidationCode.UnknownSource));
            Assert.That(coordinator.State, Is.SameAs(stateBefore));
            Assert.That(coordinator.State.PendingPhases, Has.Count.EqualTo(2));
        }

        [Test]
        public void HoldAndThresholdPoliciesAreAppliedFromExplicitState()
        {
            var registry = BasicRegistry();
            var hold = Policy(new IntentPolicyEntry(Id<IntentId>("ui.confirm"), InteractionActivationMode.Hold, true, 50, 0.5));
            var start = RouteFrame(registry, Frame(Signal("route.confirm", "source.button", InteractionPhase.Started, 100, 0, 0)),
                InteractionRoutingState.Empty, hold);
            var early = RouteFrame(registry, Frame(Signal("route.confirm", "source.button", InteractionPhase.Performed, 149, 0, 0)),
                start.NextState, hold);
            Assert.That(early.Events, Is.Empty);
            Assert.That(early.Diagnostics.Single().Kind, Is.EqualTo(InteractionDiagnosticKind.PolicyApplied));

            start = RouteFrame(registry, Frame(Signal("route.confirm", "source.button", InteractionPhase.Started, 200, 0, 0)),
                early.NextState, hold);
            var accepted = RouteFrame(registry, Frame(Signal("route.confirm", "source.button", InteractionPhase.Performed, 250, 0, 0)),
                start.NextState, hold);
            Assert.That(accepted.Events.Single().Phase, Is.EqualTo(InteractionPhase.Performed));
        }

        [Test]
        public void ToggleDispatchesFirstAndSecondActivationAndCancelPreservesChoice()
        {
            var registry = BasicRegistry();
            var toggle = Policy(new IntentPolicyEntry(Id<IntentId>("ui.confirm"), InteractionActivationMode.Toggle, true));
            var state = InteractionRoutingState.Empty;
            var first = Activate(registry, state, toggle, 10); state = first.NextState;
            Assert.That(first.Events.Single(x => x.Phase == InteractionPhase.Performed).Value.Button, Is.True);
            var started = RouteFrame(registry, Frame(Signal("route.confirm", "source.button", InteractionPhase.Started, 20, 0, 0)), state, toggle);
            var canceled = RouteFrame(registry, Frame(Signal("route.confirm", "source.button", InteractionPhase.Canceled, 21, 0, 0)), started.NextState, toggle);
            Assert.That(canceled.NextState.ToggleStates.Single().Active, Is.True);
            var second = Activate(registry, canceled.NextState, toggle, 30);
            Assert.That(second.Events.Single(x => x.Phase == InteractionPhase.Performed).Value.Button, Is.False);
        }

        [Test]
        public void PolicyChangeCancelsAffectedLifecycleAndToggleWithoutResurrectionOnRollback()
        {
            var registry = BasicRegistry();
            var enabled = Policy(new IntentPolicyEntry(
                Id<IntentId>("ui.confirm"),
                InteractionActivationMode.Toggle,
                true));
            var disabled = Policy(new IntentPolicyEntry(
                Id<IntentId>("ui.confirm"),
                InteractionActivationMode.Toggle,
                false));
            var coordinator = new InteractionCoordinator(registry, Active(), enabled);

            coordinator.RouteFrame(Frame(Signal("route.confirm", "source.button", InteractionPhase.Started, 10, 0, 0)));
            coordinator.RouteFrame(Frame(Signal("route.confirm", "source.button", InteractionPhase.Performed, 11, 0, 0)));
            coordinator.RouteFrame(Frame(Signal("route.confirm", "source.button", InteractionPhase.Started, 20, 0, 0)));
            Assert.That(coordinator.State.PendingPhases, Has.Count.EqualTo(1));
            Assert.That(coordinator.State.ToggleStates.Single().Active, Is.True);

            var equivalent = Policy(new IntentPolicyEntry(
                Id<IntentId>("ui.confirm"),
                InteractionActivationMode.Toggle,
                true));
            coordinator.SetPolicy(equivalent);
            Assert.That(coordinator.State.PendingPhases, Has.Count.EqualTo(1), "A semantic no-op must not interrupt a lifecycle.");
            Assert.That(coordinator.State.ToggleStates, Has.Count.EqualTo(1));

            coordinator.SetPolicy(disabled);
            Assert.That(coordinator.State.PendingPhases, Is.Empty);
            Assert.That(coordinator.State.ToggleStates, Is.Empty);

            coordinator.SetPolicy(enabled);
            Assert.That(coordinator.State.PendingPhases, Is.Empty, "Rolling policy back must not resurrect canceled work.");
            Assert.That(coordinator.State.ToggleStates, Is.Empty, "Rolling policy back must not resurrect a stale toggle.");

            var handlerCalls = 0;
            var stalePerformed = coordinator.RouteFrame(
                Frame(Signal("route.confirm", "source.button", InteractionPhase.Performed, 21, 0, 0)),
                _ => { handlerCalls++; return InteractionHandlerOutcome.Accepted; });
            Assert.That(stalePerformed.Events, Is.Empty);
            Assert.That(stalePerformed.Diagnostics.Single().Code,
                Is.EqualTo(InteractionValidationCode.InvalidPhaseTransition));
            Assert.That(handlerCalls, Is.Zero);

            var restarted = coordinator.RouteFrame(
                Frame(Signal("route.confirm", "source.button", InteractionPhase.Started, 30, 0, 0)),
                _ => { handlerCalls++; return InteractionHandlerOutcome.Accepted; });
            var completed = coordinator.RouteFrame(
                Frame(Signal("route.confirm", "source.button", InteractionPhase.Performed, 31, 0, 0)),
                _ => { handlerCalls++; return InteractionHandlerOutcome.Accepted; });
            Assert.That(restarted.Diagnostics, Is.Empty);
            Assert.That(completed.Events.Single().Value.Button, Is.True, "The cleared toggle must restart from its first activation.");
            Assert.That(handlerCalls, Is.EqualTo(1));
        }

        [Test]
        public void RoutePolicyReconciliationPreservesSharedIntentStateWhileAnotherRouteRemainsEnabled()
        {
            var intent = Intent("ui.confirm", InteractionValueKind.Button, InteractionCapability.Digital);
            var primaryContext = Id<ContextId>("context.primary");
            var secondaryContext = Id<ContextId>("context.secondary");
            var primaryRoute = Route("route.primary", "context.primary", "ui.confirm", "source.primary", 0);
            var secondaryRoute = Route("route.secondary", "context.secondary", "ui.confirm", "source.secondary", 0);
            var registry = Registry(
                new[] { primaryRoute, secondaryRoute },
                new[] { intent },
                Context("context.primary", 10, primaryRoute.Id),
                Context("context.secondary", 0, secondaryRoute.Id));
            var policy = PolicyEntries(new IntentPolicyEntry(intent.Id, InteractionActivationMode.Toggle, true));
            var coordinator = new InteractionCoordinator(
                registry,
                new[] { primaryContext, secondaryContext },
                policy);

            Complete(coordinator, "route.primary", "source.primary", 10);
            coordinator.RouteFrame(Frame(Signal("route.primary", "source.primary", InteractionPhase.Started, 20, 0, 0)));
            coordinator.RouteFrame(Frame(Signal("route.secondary", "source.secondary", InteractionPhase.Started, 22, 0, 0)));
            Assert.That(coordinator.State.PendingPhases, Has.Count.EqualTo(2));
            Assert.That(coordinator.State.ToggleStates.Single().Active, Is.True);

            var changed = MustPolicy(
                policy.IntentPolicies,
                new[] { new RoutePolicyEntry(primaryRoute.Id, true, 2.0, false) });
            coordinator.SetPolicy(changed);

            Assert.That(coordinator.State.PendingPhases.Select(x => x.RouteId),
                Is.EqualTo(new[] { secondaryRoute.Id }));
            Assert.That(coordinator.State.ToggleStates.Single().Active, Is.True,
                "A route-local transform change must not reset a shared intent toggle while another route remains enabled.");

            var allDisabled = MustPolicy(
                policy.IntentPolicies,
                new[]
                {
                    new RoutePolicyEntry(primaryRoute.Id, false, 2.0, false),
                    new RoutePolicyEntry(secondaryRoute.Id, false, 1.0, false),
                });
            coordinator.SetPolicy(allDisabled);
            Assert.That(coordinator.State.PendingPhases, Is.Empty);
            Assert.That(coordinator.State.ToggleStates, Is.Empty,
                "A toggle must be discarded once its intent has no active enabled route.");
        }

        [Test]
        public void ActiveContextChangesCancelUnsafeLifecycleAndOnlyResetUnreachableToggle()
        {
            var intent = Intent("ui.confirm", InteractionValueKind.Button, InteractionCapability.Digital);
            var primaryContext = Id<ContextId>("context.primary");
            var secondaryContext = Id<ContextId>("context.secondary");
            var primaryRoute = Route("route.primary", "context.primary", "ui.confirm", "source.primary", 0);
            var secondaryRoute = Route("route.secondary", "context.secondary", "ui.confirm", "source.secondary", 0);
            var registry = Registry(
                new[] { primaryRoute, secondaryRoute },
                new[] { intent },
                Context("context.primary", 10, primaryRoute.Id),
                Context("context.secondary", 0, secondaryRoute.Id));
            var toggle = Policy(new IntentPolicyEntry(intent.Id, InteractionActivationMode.Toggle, true));
            var coordinator = new InteractionCoordinator(
                registry,
                new[] { primaryContext, secondaryContext },
                toggle);
            Complete(coordinator, "route.primary", "source.primary", 10);
            coordinator.RouteFrame(Frame(Signal("route.primary", "source.primary", InteractionPhase.Started, 20, 0, 0)));
            coordinator.RouteFrame(Frame(Signal("route.secondary", "source.secondary", InteractionPhase.Started, 22, 0, 0)));
            Assert.That(coordinator.State.PendingPhases, Has.Count.EqualTo(2));
            Assert.That(coordinator.State.ToggleStates.Single().Active, Is.True);

            coordinator.SetActiveContexts(new[] { secondaryContext });
            Assert.That(coordinator.State.PendingPhases.Select(x => x.RouteId),
                Is.EqualTo(new[] { secondaryRoute.Id }));
            Assert.That(coordinator.State.ToggleStates.Single().Active, Is.True,
                "The shared toggle remains reachable through the retained context.");

            var handlerCalls = 0;
            var inactive = coordinator.RouteFrame(
                Frame(Signal("route.primary", "source.primary", InteractionPhase.Performed, 21, 0, 0)),
                _ => { handlerCalls++; return InteractionHandlerOutcome.Accepted; });
            Assert.That(inactive.Events, Is.Empty);
            Assert.That(inactive.Diagnostics.Single().Code, Is.EqualTo(InteractionValidationCode.InactiveContext));
            Assert.That(handlerCalls, Is.Zero);

            coordinator.SetActiveContexts(Array.Empty<ContextId>());
            Assert.That(coordinator.State.PendingPhases, Is.Empty);
            Assert.That(coordinator.State.ToggleStates, Is.Empty,
                "The toggle must reset after its final active context is removed.");

            coordinator.SetActiveContexts(new[] { secondaryContext });
            var restarted = coordinator.RouteFrame(
                Frame(Signal("route.secondary", "source.secondary", InteractionPhase.Started, 30, 0, 0)),
                _ => { handlerCalls++; return InteractionHandlerOutcome.Accepted; });
            var completed = coordinator.RouteFrame(
                Frame(Signal("route.secondary", "source.secondary", InteractionPhase.Performed, 31, 0, 0)),
                _ => { handlerCalls++; return InteractionHandlerOutcome.Accepted; });
            Assert.That(restarted.Diagnostics, Is.Empty);
            Assert.That(completed.Events.Single().Value.Button, Is.True);
            Assert.That(handlerCalls, Is.EqualTo(1));

            coordinator.RouteFrame(Frame(Signal("route.secondary", "source.secondary", InteractionPhase.Started, 40, 0, 0)));
            Assert.That(coordinator.State.PendingPhases, Has.Count.EqualTo(1));
            coordinator.SetActiveContexts(new[] { primaryContext, secondaryContext });
            Assert.That(coordinator.State.PendingPhases, Is.Empty,
                "Adding a context changes arbitration and cancels every in-flight lifecycle.");
            Assert.That(coordinator.State.ToggleStates.Single().Active, Is.True,
                "Adding a context does not make an existing toggle unreachable.");
            var staleAfterAddition = coordinator.RouteFrame(
                Frame(Signal("route.secondary", "source.secondary", InteractionPhase.Performed, 41, 0, 0)),
                _ => { handlerCalls++; return InteractionHandlerOutcome.Accepted; });
            Assert.That(staleAfterAddition.Events, Is.Empty);
            Assert.That(staleAfterAddition.Diagnostics.Single().Code,
                Is.EqualTo(InteractionValidationCode.InvalidPhaseTransition));
            Assert.That(handlerCalls, Is.EqualTo(1));
        }

        [Test]
        public void FullCapabilityAndModalityMustMatch()
        {
            var intent = Intent("point.select", InteractionValueKind.Pose, InteractionCapability.Pose | InteractionCapability.Pointing);
            var route = Route("route.point", "context.ui", "point.select", "source.pointer", 0,
                InteractionModality.TrackedController, InteractionCapability.Pose);
            var registry = Registry(new[] { route }, new[] { intent }, Context("context.ui", 0, route.Id));
            var pose = InteractionValue.FromPose(new InteractionPose(new InteractionVector3(0, 0, 0),
                new InteractionQuaternion(0, 0, 0, 1), true, true));
            var missing = Frame(new SourceSignal(route.Id, Id<SourceId>("source.pointer"), InteractionModality.TrackedController,
                InteractionCapability.Pose, pose, InteractionPhase.Started, 10, 0));
            Assert.That(RouteFrame(registry, missing, InteractionRoutingState.Empty).Diagnostics.Single().Code,
                Is.EqualTo(InteractionValidationCode.CapabilityMismatch));
            var wrongModality = Frame(new SourceSignal(route.Id, Id<SourceId>("source.pointer"), InteractionModality.Gaze,
                InteractionCapability.Pose | InteractionCapability.Pointing, pose, InteractionPhase.Started, 10, 0));
            Assert.That(RouteFrame(registry, wrongModality, InteractionRoutingState.Empty).Events, Is.Empty);
        }

        [Test]
        public void PostTransformOverflowFailsClosed()
        {
            var intent = Intent("axis.move", InteractionValueKind.Scalar, InteractionCapability.Scalar);
            var route = Route("route.axis", "context.ui", "axis.move", "source.axis", 0,
                InteractionModality.Gamepad, InteractionCapability.Scalar);
            var registry = Registry(new[] { route }, new[] { intent }, Context("context.ui", 0, route.Id));
            var policy = Policy(null, new RoutePolicyEntry(route.Id, true, double.MaxValue, false));
            var signal = new SourceSignal(route.Id, Id<SourceId>("source.axis"), InteractionModality.Gamepad,
                InteractionCapability.Scalar, InteractionValue.FromScalar(double.MaxValue), InteractionPhase.Started, 10, 0);
            var result = RouteFrame(registry, Frame(signal), InteractionRoutingState.Empty, policy);
            Assert.That(result.Events, Is.Empty);
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo(InteractionValidationCode.NonFiniteValue));
        }

        [Test]
        public void PolicyValidationRejectsInvalidIdsEnumsAndRanges()
        {
            Assert.That(InteractionPolicySnapshot.Create(new[] { new IntentPolicyEntry(default, InteractionActivationMode.Momentary, true) }, null).Succeeded, Is.False);
            Assert.That(InteractionPolicySnapshot.Create(new[] { new IntentPolicyEntry(Id<IntentId>("ui.confirm"), (InteractionActivationMode)99, true) }, null).Succeeded, Is.False);
            Assert.That(InteractionPolicySnapshot.Create(new[] { new IntentPolicyEntry(Id<IntentId>("ui.confirm"), InteractionActivationMode.Hold, true, -1, 2) }, null).Succeeded, Is.False);
            Assert.That(InteractionPolicySnapshot.Create(null, new[] { new RoutePolicyEntry(default, true, 1, false) }).Succeeded, Is.False);
        }

        [Test]
        public void BindingSuggestionIsImmutableAndNeverActivatesRoute()
        {
            var registry = BasicRegistry();
            var bytes = new byte[] { 1, 2 };
            var suggestion = BindingSuggestion.Create(Id<BindingSuggestionId>("suggest.confirm"), Id<IntentId>("ui.confirm"),
                Id<RouteId>("route.confirm"), "unity-input-system", bytes).Value;
            bytes[0] = 9;
            Assert.That(suggestion.OpaqueProposedBinding[0], Is.EqualTo(1));
            Assert.That(registry.BindingSuggestions, Is.Empty);
        }

        private static InteractionRoutingResult Activate(InteractionRegistry registry, InteractionRoutingState state,
            InteractionPolicySnapshot policy, long ticks)
        {
            var start = RouteFrame(registry, Frame(Signal("route.confirm", "source.button", InteractionPhase.Started, ticks, 0, 0)), state, policy);
            return RouteFrame(registry, Frame(Signal("route.confirm", "source.button", InteractionPhase.Performed, ticks + 1, 0, 0)), start.NextState, policy);
        }
        private static void Complete(InteractionCoordinator coordinator, string route, string source, long ticks)
        {
            coordinator.RouteFrame(Frame(Signal(route, source, InteractionPhase.Started, ticks, 0, 0)));
            coordinator.RouteFrame(Frame(Signal(route, source, InteractionPhase.Performed, ticks + 1, 0, 0)));
        }
        private static InteractionRegistry BasicRegistry()
        {
            var route = Route("route.confirm", "context.ui", "ui.confirm", "source.button", 0);
            return Registry(new[] { route }, Context("context.ui", 0, route.Id));
        }
        private static InteractionRegistry Registry(InteractionRoute[] routes, params InteractionContextDefinition[] contexts) =>
            Registry(routes, new[] { Intent("ui.confirm", InteractionValueKind.Button, InteractionCapability.Digital) }, contexts);
        private static InteractionRegistry Registry(InteractionRoute[] routes, IntentDefinition[] intents, params InteractionContextDefinition[] contexts)
        {
            var r = InteractionRegistry.Create(intents, contexts, routes); Assert.That(r.Succeeded, Is.True, r.Error.ToString()); return r.Value;
        }
        private static IntentDefinition Intent(string id, InteractionValueKind kind, InteractionCapability caps)
        { var r = IntentDefinition.Create(Id<IntentId>(id), kind, caps, 0); Assert.That(r.Succeeded, Is.True); return r.Value; }
        private static InteractionRoute Route(string id, string context, string intent, string source, int order,
            InteractionModality modality = InteractionModality.Gamepad, InteractionCapability caps = InteractionCapability.Digital)
        { var r = InteractionRoute.Create(Id<RouteId>(id), Id<ContextId>(context), Id<IntentId>(intent), Id<SourceId>(source), modality, caps, null, order); Assert.That(r.Succeeded, Is.True); return r.Value; }
        private static InteractionContextDefinition Context(string id, int priority, params RouteId[] routes)
        { var r = InteractionContextDefinition.Create(Id<ContextId>(id), priority, routes); Assert.That(r.Succeeded, Is.True); return r.Value; }
        private static SourceSignal Signal(string route, string source, InteractionPhase phase, long ticks, int ingress, int observation) =>
            new SourceSignal(Id<RouteId>(route), Id<SourceId>(source), InteractionModality.Gamepad, InteractionCapability.Digital,
                InteractionValue.FromButton(true), phase, ticks, ingress, observation);
        private static InteractionFrame Frame(params SourceSignal[] signals)
        { var r = InteractionFrame.Create(signals); Assert.That(r.Succeeded, Is.True, r.Error.ToString()); return r.Value; }
        private static InteractionPolicySnapshot Policy(IntentPolicyEntry? intent = null, RoutePolicyEntry? route = null)
        { var r = InteractionPolicySnapshot.Create(intent.HasValue ? new[] { intent.Value } : null, route.HasValue ? new[] { route.Value } : null); Assert.That(r.Succeeded, Is.True); return r.Value; }
        private static InteractionPolicySnapshot PolicyEntries(params IntentPolicyEntry[] intents) =>
            MustPolicy(intents, null);
        private static InteractionPolicySnapshot MustPolicy(IEnumerable<IntentPolicyEntry> intents, IEnumerable<RoutePolicyEntry> routes)
        { var r = InteractionPolicySnapshot.Create(intents, routes); Assert.That(r.Succeeded, Is.True); return r.Value; }
        private static InteractionRoutingResult RouteFrame(InteractionRegistry registry, InteractionFrame frame,
            InteractionRoutingState state, InteractionPolicySnapshot policy = null, params ContextId[] active) =>
            new InteractionRouter().Route(registry, active != null && active.Length > 0 ? active : Active(), policy, frame, state);
        private static ContextId[] Active() => new[] { Id<ContextId>("context.ui") };
        private static T Id<T>(string value) where T : struct
        {
            object result;
            if (typeof(T) == typeof(IntentId)) result = IntentId.TryCreate(value).Value;
            else if (typeof(T) == typeof(ContextId)) result = ContextId.TryCreate(value).Value;
            else if (typeof(T) == typeof(RouteId)) result = RouteId.TryCreate(value).Value;
            else if (typeof(T) == typeof(SourceId)) result = SourceId.TryCreate(value).Value;
            else if (typeof(T) == typeof(BindingSuggestionId)) result = BindingSuggestionId.TryCreate(value).Value;
            else throw new InvalidOperationException();
            return (T)result;
        }
    }
}
