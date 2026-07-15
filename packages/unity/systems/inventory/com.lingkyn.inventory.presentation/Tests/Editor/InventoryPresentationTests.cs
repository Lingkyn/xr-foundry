using System.Collections.Generic;
using System.Linq;
using Lingkyn.Inventory.Core;
using NUnit.Framework;

namespace Lingkyn.Inventory.Presentation.Tests
{
    public sealed class InventoryPresentationTests
    {
        [Test]
        public void PresenterOwnsMutationAndViewsReceiveReadOnlyModels()
        {
            var item = new ItemDefinitionId("item");
            var container = new ContainerId("bag");
            var aggregate = CreateAggregate(item, container);
            var view = new RecordingView();
            using var presenter = new InventoryPresenter(aggregate, view);

            Assert.That(view.Last.State, Is.EqualTo(InventoryUiState.Empty));
            Assert.That(presenter.Execute(MutationRequest.Add(new ItemStack(item, 1), container)).Succeeded, Is.True);
            Assert.That(view.Last.State, Is.EqualTo(InventoryUiState.Partial));
            var rejected = presenter.Execute(MutationRequest.Remove(new SlotAddress(container, 1), 1));
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(view.Last.State, Is.EqualTo(InventoryUiState.Rejected));
            Assert.That(aggregate.Revision, Is.EqualTo(1));

            Assert.That(typeof(IInventoryView).GetMethods().Select(method => method.Name), Is.EqualTo(new[] { "Render" }));
            Assert.That(typeof(InventoryViewModel).GetProperties().All(property => !property.CanWrite), Is.True);
            Assert.That(typeof(InventorySlotViewModel).GetProperties().All(property => !property.CanWrite), Is.True);
            Assert.That(typeof(InventoryViewModel).GetProperties().Any(property => property.PropertyType == typeof(InventoryAggregate)), Is.False);
            Assert.That(typeof(InventoryPresenter).Assembly.GetReferencedAssemblies()
                .Any(reference => reference.Name.StartsWith("Unity", System.StringComparison.Ordinal)), Is.False);
        }

        [Test]
        public void DisabledPresenterStaysDisabledAcrossRefreshAndAggregateEvents()
        {
            var item = new ItemDefinitionId("item");
            var container = new ContainerId("bag");
            var aggregate = CreateAggregate(item, container);
            var view = new RecordingView();
            using var presenter = new InventoryPresenter(aggregate, view);

            presenter.SetDisabled(true);
            var revision = aggregate.Revision;
            Assert.That(
                () => presenter.Execute(MutationRequest.Add(new ItemStack(item, 1), container)),
                Throws.InvalidOperationException.With.Message.Contains("disabled"));
            Assert.That(aggregate.Revision, Is.EqualTo(revision));

            Assert.That(aggregate.Execute(MutationRequest.Add(new ItemStack(item, 1), container)).Succeeded, Is.True);
            presenter.Refresh();
            Assert.That(presenter.Current.State, Is.EqualTo(InventoryUiState.Disabled));
            Assert.That(view.Last.State, Is.EqualTo(InventoryUiState.Disabled));
            Assert.That(() => presenter.Select(new SlotAddress(container, 0)), Throws.InvalidOperationException);
            Assert.That(() => presenter.Replay(InventoryUiState.Selected), Throws.InvalidOperationException);

            presenter.SetDisabled(false);
            Assert.That(presenter.Current.State, Is.EqualTo(InventoryUiState.Partial));
        }

        [Test]
        public void ViewModelSnapshotsInputAndIntentPreservesStableAddress()
        {
            var address = new SlotAddress(new ContainerId("bag"), 3);
            var source = new List<InventorySlotViewModel>
            {
                new InventorySlotViewModel(address, new ItemDefinitionId("item"), 2, true, true),
            };
            var model = new InventoryViewModel(9, InventoryUiState.Selected, source, null);
            source.Clear();
            var intent = new InventorySlotIntent(address, 7);

            Assert.That(model.Slots, Has.Count.EqualTo(1));
            Assert.That(model.Message, Is.Empty);
            Assert.That(intent.Address, Is.EqualTo(address));
            Assert.That(intent.DisplayIndex, Is.EqualTo(7));
            Assert.That(() => new InventorySlotIntent(address, -1), Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }

        private static InventoryAggregate CreateAggregate(ItemDefinitionId item, ContainerId container) =>
            new InventoryAggregate(
                new InventoryId("player"),
                new ItemDefinitionCatalog(new[] { new ItemDefinition(item, 5, ItemInstanceMode.Fungible) }),
                new[] { new ContainerDefinition(container, 2) });

        private sealed class RecordingView : IInventoryView
        {
            public InventoryViewModel Last { get; private set; }
            public void Render(InventoryViewModel model) => Last = model;
        }
    }
}
