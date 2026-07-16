using Lingkyn.Inventory.Core;

namespace Lingkyn.Inventory.Presentation.Samples
{
    public static class BasicPresentationExample
    {
        public static InventoryViewModel CreateInitialModel()
        {
            var inventory = new InventoryAggregate(
                new InventoryId("sample-player"),
                new ItemDefinitionCatalog(new[]
                {
                    new ItemDefinition(
                        new ItemDefinitionId("sample-item"),
                        10,
                        ItemInstanceMode.Fungible),
                }),
                new[] { new ContainerDefinition(new ContainerId("sample-bag"), 3) });
            var view = new RecordingView();
            using (var presenter = new InventoryPresenter(inventory, view))
            {
                return presenter.Current;
            }
        }

        private sealed class RecordingView : IInventoryView
        {
            public void Render(InventoryViewModel model) => Last = model;
            public InventoryViewModel Last { get; private set; }
        }
    }
}
