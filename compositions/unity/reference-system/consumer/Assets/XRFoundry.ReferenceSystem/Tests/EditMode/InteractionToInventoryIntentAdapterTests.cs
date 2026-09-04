using System;
using System.Collections.Generic;
using System.Linq;
using Lingkyn.Interaction.Core;
using Lingkyn.Inventory.Core;
using Lingkyn.Inventory.Presentation;
using NUnit.Framework;
using XRFoundry.ReferenceSystem.Bindings;

namespace XRFoundry.ReferenceSystem.Tests
{
    public sealed class InteractionToInventoryIntentAdapterTests
    {
        [Test]
        public void CoordinatorStartedThenPerformedSelectsFrozenRouteAddress()
        {
            using var scenario = new Scenario();
            var mutableMapping = new Dictionary<RouteId, SlotAddress>
            {
                [scenario.SelectionRouteId] = scenario.SecondSlot,
            };
            var adapter = scenario.CreateAdapter(mutableMapping);
            mutableMapping[scenario.SelectionRouteId] = scenario.FirstSlot;

            var started = scenario.Send(
                adapter.Handle,
                scenario.SelectionRouteId,
                scenario.SelectionSourceId,
                InteractionPhase.Started,
                10);
            Assert.That(started.Events.Single().Phase, Is.EqualTo(InteractionPhase.Started));
            Assert.That(scenario.Presenter.Current.Slots.All(slot => !slot.Selected), Is.True);

            var performed = scenario.Send(
                adapter.Handle,
                scenario.SelectionRouteId,
                scenario.SelectionSourceId,
                InteractionPhase.Performed,
                11);

            Assert.That(performed.Dispatches.Single().HandlerOutcome, Is.EqualTo(InteractionHandlerOutcome.Accepted));
            Assert.That(scenario.Presenter.Current.State, Is.EqualTo(InventoryUiState.Selected));
            Assert.That(Slot(scenario.Presenter, scenario.SecondSlot).Selected, Is.True);
            Assert.That(Slot(scenario.Presenter, scenario.FirstSlot).Selected, Is.False);
            Assert.That(scenario.Aggregate.Revision, Is.Zero);
        }

        [Test]
        public void DisabledSlotRejectsWithoutPollutionAndCanRecover()
        {
            using var scenario = new Scenario();
            var adapter = scenario.CreateAdapter(new Dictionary<RouteId, SlotAddress>
            {
                [scenario.SelectionRouteId] = scenario.FirstSlot,
            });
            scenario.Send(
                adapter.Handle,
                scenario.SelectionRouteId,
                scenario.SelectionSourceId,
                InteractionPhase.Started,
                20);
            scenario.Presenter.SetDisabled(true);
            var before = scenario.Presenter.Current;
            var revisionBefore = scenario.Aggregate.Revision;

            var rejected = scenario.Send(
                adapter.Handle,
                scenario.SelectionRouteId,
                scenario.SelectionSourceId,
                InteractionPhase.Performed,
                21);

            Assert.That(rejected.Dispatches.Single().HandlerOutcome, Is.EqualTo(InteractionHandlerOutcome.Rejected));
            Assert.That(scenario.Presenter.Current, Is.SameAs(before));
            Assert.That(scenario.Presenter.Current.State, Is.EqualTo(InventoryUiState.Disabled));
            Assert.That(scenario.Presenter.Current.Slots.All(slot => !slot.Selected), Is.True);
            Assert.That(scenario.Aggregate.Revision, Is.EqualTo(revisionBefore));

            scenario.Presenter.SetDisabled(false);
            scenario.Send(
                adapter.Handle,
                scenario.SelectionRouteId,
                scenario.SelectionSourceId,
                InteractionPhase.Started,
                30);
            var recovered = scenario.Send(
                adapter.Handle,
                scenario.SelectionRouteId,
                scenario.SelectionSourceId,
                InteractionPhase.Performed,
                31);

            Assert.That(recovered.Dispatches.Single().HandlerOutcome, Is.EqualTo(InteractionHandlerOutcome.Accepted));
            Assert.That(Slot(scenario.Presenter, scenario.FirstSlot).Selected, Is.True);
            Assert.That(scenario.Aggregate.Revision, Is.EqualTo(revisionBefore));
        }

        [Test]
        public void MissingRouteFailsWithoutChangingPresenterStateOrRevision()
        {
            using var scenario = new Scenario();
            var adapter = scenario.CreateAdapter(new Dictionary<RouteId, SlotAddress>());
            scenario.Send(
                adapter.Handle,
                scenario.SelectionRouteId,
                scenario.SelectionSourceId,
                InteractionPhase.Started,
                40);
            var before = scenario.Presenter.Current;
            var revisionBefore = scenario.Aggregate.Revision;

            var failed = scenario.Send(
                adapter.Handle,
                scenario.SelectionRouteId,
                scenario.SelectionSourceId,
                InteractionPhase.Performed,
                41);

            Assert.That(failed.Dispatches.Single().HandlerOutcome, Is.EqualTo(InteractionHandlerOutcome.Failed));
            Assert.That(scenario.Presenter.Current, Is.SameAs(before));
            Assert.That(scenario.Presenter.Current.Slots.All(slot => !slot.Selected), Is.True);
            Assert.That(scenario.Aggregate.Revision, Is.EqualTo(revisionBefore));
        }

        [Test]
        public void BoundAddressDriftFailsWithoutChangingPresenterStateOrRevision()
        {
            using var scenario = new Scenario();
            var driftedAddress = new SlotAddress(new ContainerId("removed-container"), 0);
            var adapter = scenario.CreateAdapter(new Dictionary<RouteId, SlotAddress>
            {
                [scenario.SelectionRouteId] = driftedAddress,
            });
            scenario.Send(
                adapter.Handle,
                scenario.SelectionRouteId,
                scenario.SelectionSourceId,
                InteractionPhase.Started,
                50);
            var before = scenario.Presenter.Current;
            var revisionBefore = scenario.Aggregate.Revision;

            var failed = scenario.Send(
                adapter.Handle,
                scenario.SelectionRouteId,
                scenario.SelectionSourceId,
                InteractionPhase.Performed,
                51);

            Assert.That(failed.Dispatches.Single().HandlerOutcome, Is.EqualTo(InteractionHandlerOutcome.Failed));
            Assert.That(scenario.Presenter.Current, Is.SameAs(before));
            Assert.That(scenario.Presenter.Current.Slots.All(slot => !slot.Selected), Is.True);
            Assert.That(scenario.Aggregate.Revision, Is.EqualTo(revisionBefore));
        }

        [Test]
        public void OtherIntentIsDeferredBeforeRouteBindingLookup()
        {
            using var scenario = new Scenario();
            var adapter = scenario.CreateAdapter(new Dictionary<RouteId, SlotAddress>
            {
                [scenario.SelectionRouteId] = scenario.FirstSlot,
            });
            scenario.Send(
                adapter.Handle,
                scenario.OtherRouteId,
                scenario.OtherSourceId,
                InteractionPhase.Started,
                60);
            var before = scenario.Presenter.Current;
            var revisionBefore = scenario.Aggregate.Revision;

            var deferred = scenario.Send(
                adapter.Handle,
                scenario.OtherRouteId,
                scenario.OtherSourceId,
                InteractionPhase.Performed,
                61);

            Assert.That(deferred.Dispatches.Single().HandlerOutcome, Is.EqualTo(InteractionHandlerOutcome.Deferred));
            Assert.That(scenario.Presenter.Current, Is.SameAs(before));
            Assert.That(scenario.Presenter.Current.Slots.All(slot => !slot.Selected), Is.True);
            Assert.That(scenario.Aggregate.Revision, Is.EqualTo(revisionBefore));
        }

        [Test]
        public void CanceledLifecycleDoesNotSelectAndHandleReportsDeferred()
        {
            using var scenario = new Scenario();
            var adapter = scenario.CreateAdapter(new Dictionary<RouteId, SlotAddress>
            {
                [scenario.SelectionRouteId] = scenario.FirstSlot,
            });
            scenario.Send(
                adapter.Handle,
                scenario.SelectionRouteId,
                scenario.SelectionSourceId,
                InteractionPhase.Started,
                70);
            var before = scenario.Presenter.Current;

            var canceled = scenario.Send(
                adapter.Handle,
                scenario.SelectionRouteId,
                scenario.SelectionSourceId,
                InteractionPhase.Canceled,
                71);

            Assert.That(canceled.Events.Single().Phase, Is.EqualTo(InteractionPhase.Canceled));
            Assert.That(canceled.Dispatches.Single().HandlerOutcome, Is.Null);
            Assert.That(adapter.Handle(scenario.SemanticEvent(InteractionPhase.Canceled)),
                Is.EqualTo(InteractionHandlerOutcome.Deferred));
            Assert.That(scenario.Presenter.Current, Is.SameAs(before));
            Assert.That(scenario.Presenter.Current.Slots.All(slot => !slot.Selected), Is.True);
            Assert.That(scenario.Aggregate.Revision, Is.Zero);
        }

        [Test]
        public void PerformedSemanticEventDoesNotReevaluatePhysicalButtonValue()
        {
            using var scenario = new Scenario();
            var adapter = scenario.CreateAdapter(new Dictionary<RouteId, SlotAddress>
            {
                [scenario.SelectionRouteId] = scenario.FirstSlot,
            });

            var outcome = adapter.Handle(scenario.SemanticEvent(
                InteractionPhase.Performed,
                InteractionValue.FromButton(false)));

            Assert.That(outcome, Is.EqualTo(InteractionHandlerOutcome.Accepted));
            Assert.That(Slot(scenario.Presenter, scenario.FirstSlot).Selected, Is.True);
            Assert.That(scenario.Aggregate.Revision, Is.Zero);
        }

        private static InventorySlotViewModel Slot(InventoryPresenter presenter, SlotAddress address) =>
            presenter.Current.Slots.Single(slot => slot.Address.Equals(address));

        private sealed class Scenario : IDisposable
        {
            private readonly ContextId _contextId;

            public Scenario()
            {
                SelectionIntentId = CreateIntentId("inventory.select-slot");
                OtherIntentId = CreateIntentId("ui.other");
                SelectionRouteId = CreateRouteId("route.inventory.slot");
                OtherRouteId = CreateRouteId("route.ui.other");
                SelectionSourceId = CreateSourceId("source.inventory.slot");
                OtherSourceId = CreateSourceId("source.ui.other");
                _contextId = CreateContextId("context.inventory");

                var selectionIntent = IntentDefinition.Create(
                    SelectionIntentId,
                    InteractionValueKind.Button,
                    InteractionCapability.Digital,
                    0);
                var otherIntent = IntentDefinition.Create(
                    OtherIntentId,
                    InteractionValueKind.Button,
                    InteractionCapability.Digital,
                    1);
                Assert.That(selectionIntent.Succeeded, Is.True, selectionIntent.Error.ToString());
                Assert.That(otherIntent.Succeeded, Is.True, otherIntent.Error.ToString());

                var selectionRoute = CreateRoute(SelectionRouteId, SelectionIntentId, SelectionSourceId, 0);
                var otherRoute = CreateRoute(OtherRouteId, OtherIntentId, OtherSourceId, 1);
                var context = InteractionContextDefinition.Create(
                    _contextId,
                    0,
                    new[] { SelectionRouteId, OtherRouteId });
                Assert.That(context.Succeeded, Is.True, context.Error.ToString());
                var registry = InteractionRegistry.Create(
                    new[] { selectionIntent.Value, otherIntent.Value },
                    new[] { context.Value },
                    new[] { selectionRoute, otherRoute });
                Assert.That(registry.Succeeded, Is.True, registry.Error.ToString());
                Coordinator = new InteractionCoordinator(registry.Value, new[] { _contextId });

                var containerId = new ContainerId("bag");
                FirstSlot = new SlotAddress(containerId, 0);
                SecondSlot = new SlotAddress(containerId, 1);
                Aggregate = new InventoryAggregate(
                    new InventoryId("player"),
                    new ItemDefinitionCatalog(Array.Empty<ItemDefinition>()),
                    new[] { new ContainerDefinition(containerId, 2) });
                Presenter = new InventoryPresenter(Aggregate, new RecordingView());
            }

            public IntentId SelectionIntentId { get; }
            public IntentId OtherIntentId { get; }
            public RouteId SelectionRouteId { get; }
            public RouteId OtherRouteId { get; }
            public SourceId SelectionSourceId { get; }
            public SourceId OtherSourceId { get; }
            public SlotAddress FirstSlot { get; }
            public SlotAddress SecondSlot { get; }
            public InventoryAggregate Aggregate { get; }
            public InventoryPresenter Presenter { get; }
            public InteractionCoordinator Coordinator { get; }

            public InteractionToInventoryIntentAdapter CreateAdapter(
                IReadOnlyDictionary<RouteId, SlotAddress> mapping) =>
                new InteractionToInventoryIntentAdapter(Presenter, SelectionIntentId, mapping);

            public InteractionRoutingResult Send(
                InteractionIntentHandler handler,
                RouteId routeId,
                SourceId sourceId,
                InteractionPhase phase,
                long ticks)
            {
                var frame = InteractionFrame.Create(new[]
                {
                    new SourceSignal(
                        routeId,
                        sourceId,
                        InteractionModality.Gamepad,
                        InteractionCapability.Digital,
                        InteractionValue.FromButton(true),
                        phase,
                        ticks,
                        0),
                });
                Assert.That(frame.Succeeded, Is.True, frame.Error.ToString());
                return Coordinator.RouteFrame(frame.Value, handler);
            }

            public SemanticInteractionEvent SemanticEvent(
                InteractionPhase phase,
                InteractionValue? value = null) =>
                new SemanticInteractionEvent(
                    SelectionIntentId,
                    _contextId,
                    SelectionRouteId,
                    SelectionSourceId,
                    InteractionModality.Gamepad,
                    value ?? InteractionValue.FromButton(true),
                    phase,
                    InteractionActivationMode.Momentary,
                    0,
                    100);

            public void Dispose() => Presenter.Dispose();

            private InteractionRoute CreateRoute(
                RouteId routeId,
                IntentId intentId,
                SourceId sourceId,
                int order)
            {
                var route = InteractionRoute.Create(
                    routeId,
                    _contextId,
                    intentId,
                    sourceId,
                    InteractionModality.Gamepad,
                    InteractionCapability.Digital,
                    null,
                    order);
                Assert.That(route.Succeeded, Is.True, route.Error.ToString());
                return route.Value;
            }

            private static IntentId CreateIntentId(string value)
            {
                var result = IntentId.TryCreate(value);
                Assert.That(result.Succeeded, Is.True, result.Error.ToString());
                return result.Value;
            }

            private static ContextId CreateContextId(string value)
            {
                var result = ContextId.TryCreate(value);
                Assert.That(result.Succeeded, Is.True, result.Error.ToString());
                return result.Value;
            }

            private static RouteId CreateRouteId(string value)
            {
                var result = RouteId.TryCreate(value);
                Assert.That(result.Succeeded, Is.True, result.Error.ToString());
                return result.Value;
            }

            private static SourceId CreateSourceId(string value)
            {
                var result = SourceId.TryCreate(value);
                Assert.That(result.Succeeded, Is.True, result.Error.ToString());
                return result.Value;
            }
        }

        private sealed class RecordingView : IInventoryView
        {
            public void Render(InventoryViewModel model)
            {
            }
        }
    }
}
