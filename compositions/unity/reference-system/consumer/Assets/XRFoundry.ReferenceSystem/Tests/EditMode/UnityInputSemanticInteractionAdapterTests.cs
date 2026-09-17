using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lingkyn.Interaction.Core;
using Lingkyn.Interaction.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using XRFoundry.ReferenceSystem.Bindings;

namespace XRFoundry.ReferenceSystem.Tests
{
    public sealed class UnityInputSemanticInteractionAdapterTests : InputTestFixture
    {
        private readonly List<InputDevice> _ownedDevices = new List<InputDevice>();

        [SetUp]
        public override void Setup()
        {
            base.Setup();
            _ownedDevices.Clear();
        }

        [TearDown]
        public override void TearDown()
        {
            _ownedDevices.Clear();
            base.TearDown();
        }

        [Test]
        public void RawStartedAndPerformedObservationsReachTheSemanticHandler()
        {
            using var scenario = new Scenario();
            var adapter = scenario.CreateAdapter();
            var handled = new List<SemanticInteractionEvent>();

            InteractionHandlerOutcome Handle(SemanticInteractionEvent semanticEvent)
            {
                handled.Add(semanticEvent);
                return InteractionHandlerOutcome.Accepted;
            }

            var started = adapter.RouteRawObservation(
                InteractionPhase.Started,
                true,
                100,
                new InputObservationStamp(0, 0),
                Handle);

            Assert.That(started.Succeeded, Is.True, started.Error.ToString());
            Assert.That(started.Value.Events.Single().Phase, Is.EqualTo(InteractionPhase.Started));
            Assert.That(started.Value.Dispatches.Single().Status, Is.EqualTo(InteractionDispatchStatus.Routed));
            Assert.That(started.Value.Dispatches.Single().HandlerOutcome, Is.Null);
            Assert.That(scenario.Coordinator.State.PendingPhases.Count, Is.EqualTo(1));
            Assert.That(handled, Is.Empty, "Started is lifecycle evidence, not a performed intent callback.");

            var performed = adapter.RouteRawObservation(
                InteractionPhase.Performed,
                true,
                101,
                new InputObservationStamp(1, 1),
                Handle);

            Assert.That(performed.Succeeded, Is.True, performed.Error.ToString());
            Assert.That(performed.Value.Dispatches.Single().Status, Is.EqualTo(InteractionDispatchStatus.HandlerOutcome));
            Assert.That(performed.Value.Dispatches.Single().HandlerOutcome, Is.EqualTo(InteractionHandlerOutcome.Accepted));
            Assert.That(performed.Value.Events.Single().IntentId, Is.EqualTo(scenario.IntentId));
            Assert.That(performed.Value.Events.Single().RouteId, Is.EqualTo(scenario.RouteId));
            Assert.That(performed.Value.Events.Single().SourceId, Is.EqualTo(scenario.SourceId));
            Assert.That(handled.Select(item => item.Phase), Is.EqualTo(new[] { InteractionPhase.Performed }));
            Assert.That(scenario.Coordinator.State.PendingPhases, Is.Empty);
        }

        [Test]
        public void MismatchedRawValueFailsBeforeRoutingAndValidRetryCompletesPendingPhase()
        {
            using var scenario = new Scenario();
            var adapter = scenario.CreateAdapter();
            var handlerCalls = 0;
            InteractionHandlerOutcome Handle(SemanticInteractionEvent semanticEvent)
            {
                handlerCalls++;
                return InteractionHandlerOutcome.Accepted;
            }

            var started = adapter.RouteRawObservation(
                InteractionPhase.Started,
                true,
                200,
                new InputObservationStamp(2, 2),
                Handle);
            Assert.That(started.Succeeded, Is.True, started.Error.ToString());
            var stateBeforeMismatch = scenario.Coordinator.State;
            Assert.That(stateBeforeMismatch.PendingPhases.Count, Is.EqualTo(1));

            var mismatch = adapter.RouteRawObservation(
                InteractionPhase.Performed,
                1f,
                201,
                new InputObservationStamp(3, 3),
                Handle);

            Assert.That(mismatch.Succeeded, Is.False);
            Assert.That(mismatch.Error.Code, Is.EqualTo(InteractionValidationCode.KindMismatch));
            Assert.That(scenario.Coordinator.State, Is.SameAs(stateBeforeMismatch));
            Assert.That(scenario.Coordinator.State.PendingPhases.Count, Is.EqualTo(1));
            Assert.That(handlerCalls, Is.Zero);

            var recovered = adapter.RouteRawObservation(
                InteractionPhase.Performed,
                true,
                202,
                new InputObservationStamp(4, 4),
                Handle);

            Assert.That(recovered.Succeeded, Is.True, recovered.Error.ToString());
            Assert.That(recovered.Value.Dispatches.Single().HandlerOutcome, Is.EqualTo(InteractionHandlerOutcome.Accepted));
            Assert.That(scenario.Coordinator.State.PendingPhases, Is.Empty);
            Assert.That(handlerCalls, Is.EqualTo(1));
        }

        [Test]
        public void BindingIntentDriftIsRejectedBeforeTheFirstObservation()
        {
            using var scenario = new Scenario();
            var driftedBinding = scenario.CreateBinding(
                CreateId("inventory.other", IntentId.TryCreate));

            var exception = Assert.Throws<ArgumentException>(() =>
                new UnityInputSemanticInteractionAdapter(
                    scenario.Coordinator,
                    new[] { driftedBinding },
                    _ => true,
                    scenario.SourceId.Value,
                    InteractionModality.KeyboardMouse,
                    InteractionCapability.Digital));

            Assert.That(exception.Message, Does.Contain("do not match semantic route"));
            Assert.That(scenario.Coordinator.State.PendingPhases, Is.Empty);
        }

        [Test]
        public void LiveButtonStartedPerformedCanceledNormalizesProviderCompletion()
        {
            using var scenario = new Scenario();
            var keyboard = AddDevice<Keyboard>();
            var adapter = scenario.CreateAdapter();
            var handlerCalls = 0;
            var sequence = 0;
            InteractionResult<InteractionRoutingResult> started = default;
            InteractionResult<InteractionRoutingResult> performed = default;
            InteractionResult<InteractionRoutingResult> canceled = default;

            InteractionResult<InteractionRoutingResult> Route(InputAction.CallbackContext callback)
            {
                var current = sequence++;
                return adapter.RouteCallback(
                    callback,
                    current,
                    new InputObservationStamp(current, current),
                    semanticEvent =>
                    {
                        handlerCalls++;
                        return InteractionHandlerOutcome.Accepted;
                    });
            }

            scenario.Action.started += callback => started = Route(callback);
            scenario.Action.performed += callback => performed = Route(callback);
            scenario.Action.canceled += callback => canceled = Route(callback);
            scenario.Action.Enable();

            SetKeyboard(keyboard, Key.Enter);
            SetKeyboard(keyboard);

            Assert.That(started.Succeeded, Is.True, started.Error.ToString());
            Assert.That(performed.Succeeded, Is.True, performed.Error.ToString());
            Assert.That(canceled.Succeeded, Is.True, canceled.Error.ToString());
            Assert.That(handlerCalls, Is.EqualTo(1));
            Assert.That(canceled.Value.Dispatches, Is.Empty);
            Assert.That(canceled.Value.Events, Is.Empty);
            Assert.That(canceled.Value.Diagnostics.Any(diagnostic =>
                diagnostic.Code == InteractionValidationCode.InvalidPhaseTransition), Is.False);
            Assert.That(scenario.Coordinator.State.PendingPhases, Is.Empty);
        }

        [Test]
        public void LiveButtonStartedThenCanceledBeforePerformedRetainsSemanticCancel()
        {
            using var scenario = new Scenario(interactions: "Hold(duration=1)");
            var keyboard = AddDevice<Keyboard>();
            var adapter = scenario.CreateAdapter();
            var performedCalls = 0;
            InteractionResult<InteractionRoutingResult> canceled = default;
            var sequence = 0;

            InteractionResult<InteractionRoutingResult> Route(InputAction.CallbackContext callback)
            {
                var current = sequence++;
                return adapter.RouteCallback(
                    callback,
                    current,
                    new InputObservationStamp(current, current),
                    semanticEvent =>
                    {
                        performedCalls++;
                        return InteractionHandlerOutcome.Accepted;
                    });
            }

            scenario.Action.started += callback => Route(callback);
            scenario.Action.performed += callback => Route(callback);
            scenario.Action.canceled += callback => canceled = Route(callback);
            scenario.Action.Enable();

            SetKeyboard(keyboard, Key.Enter);
            SetKeyboard(keyboard);

            Assert.That(canceled.Succeeded, Is.True, canceled.Error.ToString());
            Assert.That(canceled.Value.Dispatches.Single().Status, Is.EqualTo(InteractionDispatchStatus.Canceled));
            Assert.That(canceled.Value.Events.Single().Phase, Is.EqualTo(InteractionPhase.Canceled));
            Assert.That(performedCalls, Is.Zero);
            Assert.That(scenario.Coordinator.State.PendingPhases, Is.Empty);
        }

        [Test]
        public void DuplicateLiveStartedFailsWithoutChangingThePendingWinner()
        {
            using var scenario = new Scenario();
            scenario.Coordinator.RouteFrame(CoreFrame(
                scenario.RouteId,
                scenario.SourceId,
                InteractionPhase.Started,
                500));
            var stateBefore = scenario.Coordinator.State;
            var pendingBefore = stateBefore.PendingPhases.Single();
            var keyboard = AddDevice<Keyboard>();
            var adapter = scenario.CreateAdapter(control => ReferenceEquals(control.device, keyboard));
            var handlerCalls = 0;
            InteractionResult<InteractionRoutingResult> duplicate = default;

            scenario.Action.started += callback => duplicate = adapter.RouteCallback(
                callback,
                501,
                new InputObservationStamp(501, 501),
                semanticEvent =>
                {
                    handlerCalls++;
                    return InteractionHandlerOutcome.Accepted;
                });
            scenario.Action.Enable();

            SetKeyboard(keyboard, Key.Enter);

            Assert.That(duplicate.Succeeded, Is.False);
            Assert.That(duplicate.Error.Code, Is.EqualTo(InteractionValidationCode.DuplicatePhase));
            Assert.That(scenario.Coordinator.State, Is.SameAs(stateBefore));
            var pendingAfter = scenario.Coordinator.State.PendingPhases.Single();
            Assert.That(pendingAfter.RouteId, Is.EqualTo(pendingBefore.RouteId));
            Assert.That(pendingAfter.SourceId, Is.EqualTo(pendingBefore.SourceId));
            Assert.That(pendingAfter.StartedAtTicks, Is.EqualTo(pendingBefore.StartedAtTicks));
            Assert.That(handlerCalls, Is.Zero);
        }

        [Test]
        public void RawStartedPerformedCanceledSequenceRemainsStrict()
        {
            using var scenario = new Scenario();
            var adapter = scenario.CreateAdapter();
            var handlerCalls = 0;

            var started = adapter.RouteRawObservation(
                InteractionPhase.Started,
                true,
                300,
                new InputObservationStamp(10, 10));
            var performed = adapter.RouteRawObservation(
                InteractionPhase.Performed,
                true,
                301,
                new InputObservationStamp(11, 11),
                semanticEvent =>
                {
                    handlerCalls++;
                    return InteractionHandlerOutcome.Accepted;
                });
            var canceled = adapter.RouteRawObservation(
                InteractionPhase.Canceled,
                false,
                302,
                new InputObservationStamp(12, 12));

            Assert.That(started.Succeeded && performed.Succeeded && canceled.Succeeded, Is.True);
            Assert.That(handlerCalls, Is.EqualTo(1));
            Assert.That(canceled.Value.Dispatches.Single().Status, Is.EqualTo(InteractionDispatchStatus.Rejected));
            Assert.That(canceled.Value.Diagnostics.Single().Code,
                Is.EqualTo(InteractionValidationCode.InvalidPhaseTransition));
            Assert.That(scenario.Coordinator.State.PendingPhases, Is.Empty);
        }

        [Test]
        public void LiveCallbackValueTypeMismatchFailsBeforeCoordinatorAndHandler()
        {
            using var scenario = new Scenario(
                expectedControlLayout: null,
                bindingPaths: new[] { "<Gamepad>/leftStick" });
            var gamepad = AddDevice<Gamepad>();
            var adapter = scenario.CreateAdapter(control => ReferenceEquals(control.device, gamepad));
            var stateBefore = scenario.Coordinator.State;
            var handlerCalls = 0;
            InteractionResult<InteractionRoutingResult> captured = default;

            scenario.Action.performed += callback => captured = adapter.RouteCallback(
                callback,
                400,
                new InputObservationStamp(20, 20),
                semanticEvent =>
                {
                    handlerCalls++;
                    return InteractionHandlerOutcome.Accepted;
                });
            scenario.Action.Enable();

            Assert.DoesNotThrow(() => SetGamepad(
                gamepad,
                new GamepadState { leftStick = Vector2.one }));
            Assert.That(captured.Succeeded, Is.False);
            Assert.That(captured.Error.Code, Is.EqualTo(InteractionValidationCode.KindMismatch));
            Assert.That(scenario.Coordinator.State, Is.SameAs(stateBefore));
            Assert.That(handlerCalls, Is.Zero);
        }

        [Test]
        public void SameActionControlsAreIsolatedByExplicitSourceAdmission()
        {
            using var scenario = new Scenario();
            var keyboard = AddDevice<Keyboard>();
            var gamepad = AddDevice<Gamepad>();
            var adapter = scenario.CreateAdapter(control => ReferenceEquals(control.device, keyboard));
            var gamepadResults = new List<InteractionResult<InteractionRoutingResult>>();
            var keyboardResults = new List<InteractionResult<InteractionRoutingResult>>();
            var handlerCalls = 0;
            var sequence = 0;

            void Route(InputAction.CallbackContext callback)
            {
                var current = sequence++;
                var result = adapter.RouteCallback(
                    callback,
                    current,
                    new InputObservationStamp(current, current),
                    semanticEvent =>
                    {
                        handlerCalls++;
                        return InteractionHandlerOutcome.Accepted;
                    });
                if (ReferenceEquals(callback.control.device, gamepad))
                    gamepadResults.Add(result);
                else if (ReferenceEquals(callback.control.device, keyboard))
                    keyboardResults.Add(result);
            }

            scenario.Action.started += Route;
            scenario.Action.performed += Route;
            scenario.Action.canceled += Route;
            scenario.Action.Enable();

            SetGamepad(gamepad, new GamepadState(GamepadButton.South));
            SetGamepad(gamepad, new GamepadState());

            Assert.That(gamepadResults, Has.Count.EqualTo(3));
            Assert.That(gamepadResults.All(result => !result.Succeeded
                && result.Error.Code == InteractionValidationCode.UnknownSource), Is.True);
            Assert.That(handlerCalls, Is.Zero);
            Assert.That(scenario.Coordinator.State.PendingPhases, Is.Empty);

            SetKeyboard(keyboard, Key.Enter);
            SetKeyboard(keyboard);

            Assert.That(keyboardResults, Has.Count.EqualTo(3));
            Assert.That(keyboardResults.All(result => result.Succeeded), Is.True);
            Assert.That(keyboardResults.SelectMany(result => result.Value.Diagnostics).Any(diagnostic =>
                diagnostic.Code == InteractionValidationCode.InvalidPhaseTransition), Is.False);
            Assert.That(handlerCalls, Is.EqualTo(1));
            Assert.That(scenario.Coordinator.State.PendingPhases, Is.Empty);
        }

        [Test]
        public void AdmittedControlHandoffUsesOneActionScopedButtonLifecycle()
        {
            using var scenario = new Scenario(
                bindingPaths: new[]
                {
                    "<Gamepad>/leftTrigger",
                    "<Gamepad>/rightTrigger",
                });
            var gamepad = AddDevice<Gamepad>();
            var adapter = scenario.CreateAdapter(control => ReferenceEquals(control.device, gamepad));
            var results = new List<InteractionResult<InteractionRoutingResult>>();
            var callbackControls = new List<InputControl>();
            var handlerCalls = 0;
            var sequence = 0;

            void Route(InputAction.CallbackContext callback)
            {
                callbackControls.Add(callback.control);
                var current = sequence++;
                results.Add(adapter.RouteCallback(
                    callback,
                    current,
                    new InputObservationStamp(current, current),
                    semanticEvent =>
                    {
                        handlerCalls++;
                        return InteractionHandlerOutcome.Accepted;
                    }));
            }

            scenario.Action.started += Route;
            scenario.Action.performed += Route;
            scenario.Action.canceled += Route;
            scenario.Action.Enable();

            SetGamepad(gamepad, new GamepadState { leftTrigger = 0.75f });
            SetGamepad(gamepad, new GamepadState { leftTrigger = 0.75f, rightTrigger = 1f });
            SetGamepad(gamepad, new GamepadState { rightTrigger = 1f });
            SetGamepad(gamepad, new GamepadState());

            Assert.That(results, Has.Count.EqualTo(3));
            Assert.That(callbackControls.First(), Is.Not.SameAs(callbackControls.Last()));
            Assert.That(results.All(result => result.Succeeded), Is.True);
            Assert.That(results.SelectMany(result => result.Value.Diagnostics).Any(diagnostic =>
                diagnostic.Code == InteractionValidationCode.InvalidPhaseTransition), Is.False);
            Assert.That(handlerCalls, Is.EqualTo(1));
            Assert.That(scenario.Coordinator.State.PendingPhases, Is.Empty);
        }

        [Test]
        public void PressAndReleaseSecondPerformedGetsAFreshStrictCycle()
        {
            using var scenario = new Scenario(
                interactions: "Press(behavior=2)",
                bindingPaths: new[] { "<Keyboard>/enter" });
            var keyboard = AddDevice<Keyboard>();
            var adapter = scenario.CreateAdapter(control => ReferenceEquals(control.device, keyboard));
            var callbacks = new List<InputActionPhase>();
            var results = new List<InteractionResult<InteractionRoutingResult>>();
            var handlerCalls = 0;
            var sequence = 0;

            void Route(InputAction.CallbackContext callback)
            {
                callbacks.Add(callback.phase);
                var current = sequence++;
                results.Add(adapter.RouteCallback(
                    callback,
                    current,
                    new InputObservationStamp(current, current),
                    semanticEvent =>
                    {
                        handlerCalls++;
                        return InteractionHandlerOutcome.Accepted;
                    }));
            }

            scenario.Action.started += Route;
            scenario.Action.performed += Route;
            scenario.Action.canceled += Route;
            scenario.Action.Enable();

            SetKeyboard(keyboard, Key.Enter);
            SetKeyboard(keyboard);

            Assert.That(callbacks.Count(phase => phase == InputActionPhase.Performed), Is.EqualTo(2));
            Assert.That(results.All(result => result.Succeeded), Is.True);
            Assert.That(results.SelectMany(result => result.Value.Diagnostics).Any(diagnostic =>
                diagnostic.Code == InteractionValidationCode.InvalidPhaseTransition), Is.False);
            Assert.That(handlerCalls, Is.EqualTo(2),
                "Every live provider Performed is normalized to a true Button pulse.");
            Assert.That(scenario.Coordinator.State.PendingPhases, Is.Empty);
        }

        [TestCase("Press(behavior=1)")]
        [TestCase("Tap(duration=10)")]
        public void ReleaseOnlyAndSuccessfulTapEmitOneTrueButtonPulse(string interactions)
        {
            using var scenario = new Scenario(
                interactions: interactions,
                bindingPaths: new[] { "<Keyboard>/enter" });
            var keyboard = AddDevice<Keyboard>();
            var adapter = scenario.CreateAdapter(control => ReferenceEquals(control.device, keyboard));
            var results = new List<InteractionResult<InteractionRoutingResult>>();
            var performedCallbacks = 0;
            var handlerValues = new List<bool>();
            var sequence = 0;

            void Route(InputAction.CallbackContext callback)
            {
                if (callback.phase == InputActionPhase.Performed)
                    performedCallbacks++;
                var current = sequence++;
                results.Add(adapter.RouteCallback(
                    callback,
                    current,
                    new InputObservationStamp(current, current),
                    semanticEvent =>
                    {
                        handlerValues.Add(semanticEvent.Value.Button);
                        return InteractionHandlerOutcome.Accepted;
                    }));
            }

            scenario.Action.started += Route;
            scenario.Action.performed += Route;
            scenario.Action.canceled += Route;
            scenario.Action.Enable();

            SetKeyboard(keyboard, Key.Enter);
            SetKeyboard(keyboard);

            Assert.That(performedCallbacks, Is.EqualTo(1));
            Assert.That(handlerValues, Is.EqualTo(new[] { true }));
            Assert.That(results.All(result => result.Succeeded), Is.True);
            Assert.That(results.SelectMany(result => result.Value.Diagnostics).Any(diagnostic =>
                diagnostic.Code == InteractionValidationCode.InvalidPhaseTransition), Is.False);
            Assert.That(scenario.Coordinator.State.PendingPhases, Is.Empty);
        }

        [Test]
        public void TimedOutTapCancelsItsPendingCycleWithoutActivation()
        {
            using var scenario = new Scenario(
                interactions: "Tap(duration=0.000001)",
                bindingPaths: new[] { "<Keyboard>/enter" });
            var keyboard = AddDevice<Keyboard>();
            var adapter = scenario.CreateAdapter(control => ReferenceEquals(control.device, keyboard));
            var handlerCalls = 0;
            var sequence = 0;
            InteractionResult<InteractionRoutingResult> canceled = default;

            void Route(InputAction.CallbackContext callback)
            {
                var current = sequence++;
                var result = adapter.RouteCallback(
                    callback,
                    current,
                    new InputObservationStamp(current, current),
                    semanticEvent =>
                    {
                        handlerCalls++;
                        return InteractionHandlerOutcome.Accepted;
                    });
                if (callback.phase == InputActionPhase.Canceled)
                    canceled = result;
            }

            scenario.Action.started += Route;
            scenario.Action.performed += Route;
            scenario.Action.canceled += Route;
            scenario.Action.Enable();

            SetKeyboard(keyboard, Key.Enter);
            for (var update = 0; update < 3 && !canceled.Succeeded; update++)
                InputSystem.Update();

            Assert.That(canceled.Succeeded, Is.True,
                "Tap must time out before the control is released.");
            Assert.That(canceled.Value.Events.Single().Phase, Is.EqualTo(InteractionPhase.Canceled));
            Assert.That(handlerCalls, Is.Zero);
            Assert.That(scenario.Coordinator.State.PendingPhases, Is.Empty);
            SetKeyboard(keyboard);
        }

        [Test]
        public void ResetAfterUnsubscribeAndDisableClearsPendingAndIsIdempotent()
        {
            using var scenario = new Scenario(
                interactions: "Hold(duration=10)",
                bindingPaths: new[] { "<Keyboard>/enter" });
            var keyboard = AddDevice<Keyboard>();
            var adapter = scenario.CreateAdapter(control => ReferenceEquals(control.device, keyboard));
            var sequence = 0;

            void Route(InputAction.CallbackContext callback)
            {
                var current = sequence++;
                adapter.RouteCallback(
                    callback,
                    current,
                    new InputObservationStamp(current, current));
            }

            scenario.Action.started += Route;
            scenario.Action.performed += Route;
            scenario.Action.canceled += Route;
            scenario.Action.Enable();

            SetKeyboard(keyboard, Key.Enter);
            Assert.That(scenario.Coordinator.State.PendingPhases, Has.Count.EqualTo(1));

            scenario.Action.started -= Route;
            scenario.Action.performed -= Route;
            scenario.Action.canceled -= Route;
            scenario.Action.Disable();
            Assert.That(scenario.Coordinator.State.PendingPhases, Has.Count.EqualTo(1),
                "No callback reaches the adapter after unsubscribe.");

            var reset = adapter.ResetLiveCallbackLifecycle();
            var stateAfterReset = scenario.Coordinator.State;
            var repeated = adapter.ResetLiveCallbackLifecycle();

            Assert.That(reset.Succeeded, Is.True, reset.Error.ToString());
            Assert.That(reset.Value, Is.EqualTo(1));
            Assert.That(repeated.Succeeded, Is.True, repeated.Error.ToString());
            Assert.That(repeated.Value, Is.Zero);
            Assert.That(scenario.Coordinator.State, Is.SameAs(stateAfterReset));
            Assert.That(scenario.Coordinator.State.PendingPhases, Is.Empty);
        }

        [Test]
        public void PendingLowWinnerCompletesAfterHighRouteIsEnabledThenHighWinsNextCycle()
        {
            using var scenario = new ArbitrationScenario(disableHighRoute: true);
            var keyboard = AddDevice<Keyboard>();
            var adapter = scenario.CreateAdapter(control => ReferenceEquals(control.device, keyboard));
            var handledRoutes = new List<RouteId>();
            var results = new List<InteractionResult<InteractionRoutingResult>>();
            var sequence = 0;
            var enableHighAfterStarted = true;
            RouteId firstPending = default;

            InteractionResult<InteractionRoutingResult> Route(InputAction.CallbackContext callback)
            {
                var current = sequence++;
                return adapter.RouteCallback(
                    callback,
                    current,
                    new InputObservationStamp(current, current),
                    semanticEvent =>
                    {
                        handledRoutes.Add(semanticEvent.RouteId);
                        return InteractionHandlerOutcome.Accepted;
                    });
            }

            scenario.Action.started += callback =>
            {
                results.Add(Route(callback));
                if (!enableHighAfterStarted)
                    return;
                firstPending = scenario.Coordinator.State.PendingPhases.Single().RouteId;
                scenario.EnableHighRoute();
                enableHighAfterStarted = false;
            };
            scenario.Action.performed += callback => results.Add(Route(callback));
            scenario.Action.canceled += callback => results.Add(Route(callback));
            scenario.Action.Enable();

            SetKeyboard(keyboard, Key.Enter);
            SetKeyboard(keyboard);
            SetKeyboard(keyboard, Key.Enter);
            SetKeyboard(keyboard);

            Assert.That(firstPending, Is.EqualTo(scenario.LowRouteId));
            Assert.That(handledRoutes, Is.EqualTo(new[]
            {
                scenario.LowRouteId,
                scenario.HighRouteId,
            }));
            Assert.That(results.All(result => result.Succeeded), Is.True);
            Assert.That(results.SelectMany(result => result.Value.Diagnostics).Any(diagnostic =>
                diagnostic.Code == InteractionValidationCode.InvalidPhaseTransition), Is.False);
            Assert.That(scenario.Coordinator.State.PendingPhases, Is.Empty);
        }

        [Test]
        public void AmbiguousSyntheticStartedDoesNotContinueToPerformed()
        {
            using var scenario = new ArbitrationScenario(
                highPriority: 5,
                lowPriority: 5,
                disableHighRoute: false);
            var keyboard = AddDevice<Keyboard>();
            var adapter = scenario.CreateAdapter(control => ReferenceEquals(control.device, keyboard));
            var handlerCalls = 0;
            var sequence = 0;
            InteractionResult<InteractionRoutingResult> performed = default;

            void Route(InputAction.CallbackContext callback)
            {
                var current = sequence++;
                var result = adapter.RouteCallback(
                    callback,
                    current,
                    new InputObservationStamp(current, current),
                    semanticEvent =>
                    {
                        handlerCalls++;
                        return InteractionHandlerOutcome.Accepted;
                    });
                if (callback.phase == InputActionPhase.Performed)
                    performed = result;
            }

            scenario.Action.started += Route;
            scenario.Action.performed += Route;
            scenario.Action.canceled += Route;
            scenario.Action.Enable();

            SetKeyboard(keyboard, Key.Enter);
            SetKeyboard(keyboard);

            Assert.That(performed.Succeeded, Is.True, performed.Error.ToString());
            Assert.That(performed.Value.Dispatches, Has.Count.EqualTo(2));
            Assert.That(performed.Value.Dispatches.All(dispatch =>
                dispatch.Status == InteractionDispatchStatus.Ambiguous), Is.True);
            Assert.That(handlerCalls, Is.Zero);
            Assert.That(scenario.Coordinator.State.PendingPhases, Is.Empty);
        }

        [Test]
        public void RejectedSyntheticStartedDoesNotContinueToPerformed()
        {
            using var scenario = new Scenario(bindingPaths: new[] { "<Keyboard>/enter" });
            var disabled = InteractionPolicySnapshot.Create(
                null,
                new[] { new RoutePolicyEntry(scenario.RouteId, false, 1d, false) });
            Assert.That(disabled.Succeeded, Is.True, disabled.Error.ToString());
            scenario.Coordinator.SetPolicy(disabled.Value);
            var keyboard = AddDevice<Keyboard>();
            var adapter = scenario.CreateAdapter(control => ReferenceEquals(control.device, keyboard));
            var handlerCalls = 0;
            var sequence = 0;
            InteractionResult<InteractionRoutingResult> performed = default;

            void Route(InputAction.CallbackContext callback)
            {
                var current = sequence++;
                var result = adapter.RouteCallback(
                    callback,
                    current,
                    new InputObservationStamp(current, current),
                    semanticEvent =>
                    {
                        handlerCalls++;
                        return InteractionHandlerOutcome.Accepted;
                    });
                if (callback.phase == InputActionPhase.Performed)
                    performed = result;
            }

            scenario.Action.started += Route;
            scenario.Action.performed += Route;
            scenario.Action.canceled += Route;
            scenario.Action.Enable();

            SetKeyboard(keyboard, Key.Enter);
            SetKeyboard(keyboard);

            Assert.That(performed.Succeeded, Is.True, performed.Error.ToString());
            Assert.That(performed.Value.Diagnostics.Single().Code,
                Is.EqualTo(InteractionValidationCode.DisabledRoute));
            Assert.That(handlerCalls, Is.Zero);
            Assert.That(scenario.Coordinator.State.PendingPhases, Is.Empty);
        }

        [Test]
        public void MultipleOwnedPendingWinnersFailClosedBeforeRouting()
        {
            using var scenario = new ArbitrationScenario(disableHighRoute: false);
            scenario.Coordinator.RouteFrame(CoreFrame(
                scenario.LowRouteId,
                scenario.SourceId,
                InteractionPhase.Started,
                10));
            scenario.Coordinator.RouteFrame(CoreFrame(
                scenario.HighRouteId,
                scenario.SourceId,
                InteractionPhase.Started,
                11));
            Assert.That(scenario.Coordinator.State.PendingPhases, Has.Count.EqualTo(2));
            var stateBefore = scenario.Coordinator.State;
            var keyboard = AddDevice<Keyboard>();
            var adapter = scenario.CreateAdapter(control => ReferenceEquals(control.device, keyboard));
            InteractionResult<InteractionRoutingResult> performed = default;

            scenario.Action.performed += callback => performed = adapter.RouteCallback(
                callback,
                12,
                new InputObservationStamp(12, 12),
                semanticEvent => InteractionHandlerOutcome.Accepted);
            scenario.Action.Enable();

            SetKeyboard(keyboard, Key.Enter);

            Assert.That(performed.Succeeded, Is.False);
            Assert.That(performed.Error.Code,
                Is.EqualTo(InteractionValidationCode.AmbiguousContextCollision));
            Assert.That(scenario.Coordinator.State, Is.SameAs(stateBefore));
        }

        [TestCase(InputActionType.Value)]
        [TestCase(InputActionType.PassThrough)]
        public void NonButtonInputActionsAreRejectedAtConstruction(InputActionType actionType)
        {
            using var scenario = new Scenario(actionType: actionType);

            var exception = Assert.Throws<ArgumentException>(() => scenario.CreateAdapter());

            Assert.That(exception.Message, Does.Contain("Button"));
            Assert.That(scenario.Coordinator.State.PendingPhases, Is.Empty);
        }

        [Test]
        public void CoreRouteDescriptorForAnotherActionIsRejectedAtConstruction()
        {
            using var scenario = new Scenario(descriptorReferencesDifferentAction: true);

            var exception = Assert.Throws<ArgumentException>(() => scenario.CreateAdapter());

            Assert.That(exception.Message, Does.Contain("descriptor"));
            Assert.That(scenario.Coordinator.State.PendingPhases, Is.Empty);
        }

        [Test]
        public void UnsatisfiedIntentCapabilitiesAreRejectedAtConstruction()
        {
            using var scenario = new Scenario(
                requiredCapabilities: InteractionCapability.Digital | InteractionCapability.Pointing);

            var exception = Assert.Throws<ArgumentException>(() => scenario.CreateAdapter());

            Assert.That(exception.Message, Does.Contain("required capabilities"));
            Assert.That(scenario.Coordinator.State.PendingPhases, Is.Empty);
        }

        [Test]
        public void CandidatesWithTheSameActionIdButDifferentInstancesAreRejected()
        {
            using var scenario = new ArbitrationScenario();
            var cloned = scenario.CreateBindingWithClonedAction(scenario.LowRouteId);

            Assert.That(cloned.ActionId, Is.EqualTo(scenario.Action.id));
            Assert.That(cloned.ActionReference.action, Is.Not.SameAs(scenario.Action));
            var exception = Assert.Throws<ArgumentException>(() =>
                new UnityInputSemanticInteractionAdapter(
                    scenario.Coordinator,
                    new[]
                    {
                        scenario.CreateBinding(scenario.HighRouteId),
                        cloned,
                    },
                    _ => true,
                    scenario.SourceId.Value,
                    InteractionModality.KeyboardMouse,
                    InteractionCapability.Digital));

            Assert.That(exception.Message, Does.Contain("shared InputAction instance"));
            Assert.That(scenario.Coordinator.State.PendingPhases, Is.Empty);
        }

        [Test]
        public void SameGuidCallbackFromAnotherActionInstanceFailsBeforeRouting()
        {
            using var scenario = new ArbitrationScenario();
            var clonedBinding = scenario.CreateBindingWithClonedAction(scenario.LowRouteId);
            var clonedAction = clonedBinding.ActionReference.action;
            var keyboard = AddDevice<Keyboard>();
            var admissionCalls = 0;
            var adapter = scenario.CreateAdapter(control =>
            {
                admissionCalls++;
                return ReferenceEquals(control.device, keyboard);
            });
            var stateBefore = scenario.Coordinator.State;
            var results = new List<InteractionResult<InteractionRoutingResult>>();
            var callbackActions = new List<InputAction>();
            var handlerCalls = 0;
            var sequence = 0;

            void Route(InputAction.CallbackContext callback)
            {
                callbackActions.Add(callback.action);
                var current = sequence++;
                results.Add(adapter.RouteCallback(
                    callback,
                    current,
                    new InputObservationStamp(current, current),
                    semanticEvent =>
                    {
                        handlerCalls++;
                        return InteractionHandlerOutcome.Accepted;
                    }));
            }

            Assert.That(clonedAction.id, Is.EqualTo(adapter.Action.id));
            Assert.That(clonedAction, Is.Not.SameAs(adapter.Action));
            clonedAction.started += Route;
            clonedAction.performed += Route;
            clonedAction.canceled += Route;
            clonedAction.Enable();

            SetKeyboard(keyboard, Key.Enter);
            SetKeyboard(keyboard);

            Assert.That(results, Is.Not.Empty);
            Assert.That(callbackActions, Has.Count.EqualTo(results.Count));
            Assert.That(callbackActions.All(action => ReferenceEquals(action, clonedAction)), Is.True);
            Assert.That(callbackActions.All(action => !ReferenceEquals(action, adapter.Action)), Is.True);
            Assert.That(callbackActions.All(action => action.id == adapter.Action.id), Is.True);
            Assert.That(results.All(result => !result.Succeeded
                && result.Error.Code == InteractionValidationCode.UnknownRoute), Is.True);
            Assert.That(admissionCalls, Is.Zero,
                "Action-instance identity must fail before callback-control admission.");
            Assert.That(scenario.Coordinator.State, Is.SameAs(stateBefore));
            Assert.That(scenario.Coordinator.State.PendingPhases, Is.Empty);
            Assert.That(handlerCalls, Is.Zero);
        }

        [Test]
        public void ConstructorFreezesOrderedCandidates()
        {
            using var scenario = new Scenario();
            var candidates = new List<InputRouteBinding> { scenario.CreateBinding() };
            var adapter = new UnityInputSemanticInteractionAdapter(
                scenario.Coordinator,
                candidates,
                _ => true,
                scenario.SourceId.Value,
                InteractionModality.KeyboardMouse,
                InteractionCapability.Digital);

            candidates.Clear();

            Assert.That(adapter.OrderedCandidates.Count, Is.EqualTo(1));
            Assert.That(adapter.OrderedCandidates[0].RouteId, Is.EqualTo(scenario.RouteId));
            Assert.That(adapter.Action, Is.SameAs(scenario.Action));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<InputRouteBinding>)adapter.OrderedCandidates).Clear());
        }

        [Test]
        public void ConstructorRequiresExplicitCallbackControlAdmission()
        {
            using var scenario = new Scenario();

            Assert.Throws<ArgumentNullException>(() =>
                new UnityInputSemanticInteractionAdapter(
                    scenario.Coordinator,
                    new[] { scenario.CreateBinding() },
                    null,
                    scenario.SourceId.Value,
                    InteractionModality.KeyboardMouse,
                    InteractionCapability.Digital));
        }

        private sealed class ArbitrationScenario : IDisposable
        {
            private readonly InputActionAsset _actionAsset;
            private readonly InputActionReference _actionReference;
            private InputActionAsset _clonedActionAsset;
            private InputActionReference _clonedActionReference;

            public ArbitrationScenario(
                int highPriority = 10,
                int lowPriority = 1,
                bool disableHighRoute = false)
            {
                IntentId = CreateId("inventory.arbitrated-select", IntentId.TryCreate);
                HighContextId = CreateId("inventory.context.high", ContextId.TryCreate);
                LowContextId = CreateId("inventory.context.low", ContextId.TryCreate);
                HighRouteId = CreateId("inventory.select.keyboard.high", RouteId.TryCreate);
                LowRouteId = CreateId("inventory.select.keyboard.low", RouteId.TryCreate);
                SourceId = CreateId("player.keyboard", SourceId.TryCreate);

                _actionAsset = ScriptableObject.CreateInstance<InputActionAsset>();
                var actionMap = _actionAsset.AddActionMap("Inventory");
                Action = actionMap.AddAction(
                    "Select",
                    InputActionType.Button,
                    expectedControlLayout: "Button");
                Action.AddBinding("<Keyboard>/enter");
                _actionReference = InputActionReference.Create(Action);

                var intent = IntentDefinition.Create(
                    IntentId,
                    InteractionValueKind.Button,
                    InteractionCapability.Digital,
                    0);
                Assert.That(intent.Succeeded, Is.True, intent.Error.ToString());
                var descriptor = Encoding.UTF8.GetBytes(
                    "input-system-action:" + Action.id.ToString("D"));
                var highRoute = InteractionRoute.Create(
                    HighRouteId,
                    HighContextId,
                    IntentId,
                    SourceId,
                    InteractionModality.KeyboardMouse,
                    InteractionCapability.Digital,
                    descriptor,
                    0);
                var lowRoute = InteractionRoute.Create(
                    LowRouteId,
                    LowContextId,
                    IntentId,
                    SourceId,
                    InteractionModality.KeyboardMouse,
                    InteractionCapability.Digital,
                    descriptor,
                    1);
                Assert.That(highRoute.Succeeded, Is.True, highRoute.Error.ToString());
                Assert.That(lowRoute.Succeeded, Is.True, lowRoute.Error.ToString());
                var highContext = InteractionContextDefinition.Create(
                    HighContextId,
                    highPriority,
                    new[] { HighRouteId });
                var lowContext = InteractionContextDefinition.Create(
                    LowContextId,
                    lowPriority,
                    new[] { LowRouteId });
                Assert.That(highContext.Succeeded, Is.True, highContext.Error.ToString());
                Assert.That(lowContext.Succeeded, Is.True, lowContext.Error.ToString());
                var registry = InteractionRegistry.Create(
                    new[] { intent.Value },
                    new[] { highContext.Value, lowContext.Value },
                    new[] { highRoute.Value, lowRoute.Value });
                Assert.That(registry.Succeeded, Is.True, registry.Error.ToString());

                InteractionPolicySnapshot policy = null;
                if (disableHighRoute)
                {
                    var disabled = InteractionPolicySnapshot.Create(
                        null,
                        new[] { new RoutePolicyEntry(HighRouteId, false, 1d, false) });
                    Assert.That(disabled.Succeeded, Is.True, disabled.Error.ToString());
                    policy = disabled.Value;
                }

                Coordinator = new InteractionCoordinator(
                    registry.Value,
                    new[] { HighContextId, LowContextId },
                    policy);
            }

            public IntentId IntentId { get; }
            public ContextId HighContextId { get; }
            public ContextId LowContextId { get; }
            public RouteId HighRouteId { get; }
            public RouteId LowRouteId { get; }
            public SourceId SourceId { get; }
            public InteractionCoordinator Coordinator { get; }
            public InputAction Action { get; }

            public UnityInputSemanticInteractionAdapter CreateAdapter(
                Func<InputControl, bool> callbackControlAdmission = null) =>
                new UnityInputSemanticInteractionAdapter(
                    Coordinator,
                    new[]
                    {
                        CreateBinding(HighRouteId),
                        CreateBinding(LowRouteId),
                    },
                    callbackControlAdmission ?? (_ => true),
                    SourceId.Value,
                    InteractionModality.KeyboardMouse,
                    InteractionCapability.Digital);

            public InputRouteBinding CreateBinding(RouteId routeId) =>
                CreateBinding(routeId, _actionReference);

            public InputRouteBinding CreateBindingWithClonedAction(RouteId routeId)
            {
                _clonedActionAsset = InputActionAsset.FromJson(_actionAsset.ToJson());
                var clonedAction = _clonedActionAsset.FindAction(Action.id);
                Assert.That(clonedAction, Is.Not.Null);
                _clonedActionReference = InputActionReference.Create(clonedAction);
                return CreateBinding(routeId, _clonedActionReference);
            }

            public void EnableHighRoute()
            {
                var enabled = InteractionPolicySnapshot.Create(
                    null,
                    new[] { new RoutePolicyEntry(HighRouteId, true, 1d, false) });
                Assert.That(enabled.Succeeded, Is.True, enabled.Error.ToString());
                Coordinator.SetPolicy(enabled.Value);
            }

            public void Dispose()
            {
                _actionAsset.Disable();
                _clonedActionAsset?.Disable();
                if (_clonedActionReference != null)
                    UnityEngine.Object.DestroyImmediate(_clonedActionReference);
                if (_clonedActionAsset != null)
                    UnityEngine.Object.DestroyImmediate(_clonedActionAsset);
                UnityEngine.Object.DestroyImmediate(_actionReference);
                UnityEngine.Object.DestroyImmediate(_actionAsset);
            }

            private InputRouteBinding CreateBinding(
                RouteId routeId,
                InputActionReference actionReference) =>
                new InputRouteBinding(
                    routeId,
                    IntentId,
                    SourceId,
                    InteractionModality.KeyboardMouse,
                    InteractionCapability.Digital,
                    InteractionValueKind.Button,
                    actionReference);
        }

        private sealed class Scenario : IDisposable
        {
            private readonly InputActionAsset _actionAsset;
            private readonly InputActionReference _actionReference;

            public Scenario(
                InputActionType actionType = InputActionType.Button,
                string interactions = null,
                InteractionCapability requiredCapabilities = InteractionCapability.Digital,
                bool descriptorReferencesDifferentAction = false,
                string expectedControlLayout = "Button",
                string[] bindingPaths = null)
            {
                IntentId = CreateId("inventory.select", IntentId.TryCreate);
                ContextId = CreateId("inventory.context", ContextId.TryCreate);
                RouteId = CreateId("inventory.select.keyboard", RouteId.TryCreate);
                SourceId = CreateId("player.keyboard", SourceId.TryCreate);

                _actionAsset = ScriptableObject.CreateInstance<InputActionAsset>();
                var actionMap = _actionAsset.AddActionMap("Inventory");
                Action = actionMap.AddAction(
                    "Select",
                    actionType,
                    expectedControlLayout: expectedControlLayout,
                    interactions: interactions);
                foreach (var bindingPath in bindingPaths ?? new[]
                {
                    "<Keyboard>/enter",
                    "<Gamepad>/buttonSouth",
                })
                {
                    Action.AddBinding(bindingPath);
                }
                _actionReference = InputActionReference.Create(Action);
                var descriptorAction = descriptorReferencesDifferentAction
                    ? actionMap.AddAction("Other", InputActionType.Button, expectedControlLayout: "Button")
                    : Action;

                var intent = IntentDefinition.Create(
                    IntentId,
                    InteractionValueKind.Button,
                    requiredCapabilities,
                    0);
                Assert.That(intent.Succeeded, Is.True, intent.Error.ToString());
                var route = InteractionRoute.Create(
                    RouteId,
                    ContextId,
                    IntentId,
                    SourceId,
                    InteractionModality.KeyboardMouse,
                    InteractionCapability.Digital,
                    Encoding.UTF8.GetBytes(
                        "input-system-action:" + descriptorAction.id.ToString("D")),
                    0);
                Assert.That(route.Succeeded, Is.True, route.Error.ToString());
                var context = InteractionContextDefinition.Create(
                    ContextId,
                    0,
                    new[] { RouteId });
                Assert.That(context.Succeeded, Is.True, context.Error.ToString());
                var registry = InteractionRegistry.Create(
                    new[] { intent.Value },
                    new[] { context.Value },
                    new[] { route.Value });
                Assert.That(registry.Succeeded, Is.True, registry.Error.ToString());
                Coordinator = new InteractionCoordinator(registry.Value, new[] { ContextId });
            }

            public IntentId IntentId { get; }
            public ContextId ContextId { get; }
            public RouteId RouteId { get; }
            public SourceId SourceId { get; }
            public InteractionCoordinator Coordinator { get; }
            public InputAction Action { get; }

            public UnityInputSemanticInteractionAdapter CreateAdapter(
                Func<InputControl, bool> callbackControlAdmission = null) =>
                new UnityInputSemanticInteractionAdapter(
                    Coordinator,
                    new[] { CreateBinding() },
                    callbackControlAdmission ?? (_ => true),
                    SourceId.Value,
                    InteractionModality.KeyboardMouse,
                    InteractionCapability.Digital);

            public InputRouteBinding CreateBinding(IntentId? intentId = null) =>
                new InputRouteBinding(
                    RouteId,
                    intentId ?? IntentId,
                    SourceId,
                    InteractionModality.KeyboardMouse,
                    InteractionCapability.Digital,
                    InteractionValueKind.Button,
                    _actionReference);

            public void Dispose()
            {
                _actionAsset.Disable();
                UnityEngine.Object.DestroyImmediate(_actionReference);
                UnityEngine.Object.DestroyImmediate(_actionAsset);
            }
        }

        private T AddDevice<T>() where T : InputDevice
        {
            var device = InputSystem.AddDevice<T>();
            _ownedDevices.Add(device);
            return device;
        }

        private static void SetKeyboard(Keyboard keyboard, params Key[] pressedKeys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(pressedKeys));
            InputSystem.Update();
        }

        private static void SetGamepad(Gamepad gamepad, GamepadState state)
        {
            InputSystem.QueueStateEvent(gamepad, state);
            InputSystem.Update();
        }

        private static InteractionFrame CoreFrame(
            RouteId routeId,
            SourceId sourceId,
            InteractionPhase phase,
            long timestampTicks)
        {
            var frame = InteractionFrame.Create(new[]
            {
                new SourceSignal(
                    routeId,
                    sourceId,
                    InteractionModality.KeyboardMouse,
                    InteractionCapability.Digital,
                    InteractionValue.FromButton(true),
                    phase,
                    timestampTicks,
                    0,
                    0),
            });
            Assert.That(frame.Succeeded, Is.True, frame.Error.ToString());
            return frame.Value;
        }

        private static T CreateId<T>(
            string value,
            Func<string, InteractionResult<T>> factory)
        {
            var result = factory(value);
            Assert.That(result.Succeeded, Is.True, result.Error.ToString());
            return result.Value;
        }
    }
}
