using Lingkyn.Inventory.Core;

namespace Lingkyn.Inventory.Samples
{
    public static class BasicInventoryExample
    {
        public static InventorySnapshot Run()
        {
            var potion = new ItemDefinition(new ItemDefinitionId("potion"), 10, ItemInstanceMode.Fungible);
            var catalog = new ItemDefinitionCatalog(new[] { potion });
            var bag = new ContainerDefinition(new ContainerId("bag"), 2);
            var inventory = new InventoryAggregate(new InventoryId("player"), catalog, new[] { bag });

            inventory.Execute(MutationRequest.Add(new ItemStack(potion.Id, 6), bag.Id));
            inventory.Execute(MutationRequest.Split(new SlotAddress(bag.Id, 0), new SlotAddress(bag.Id, 1), 2));
            return inventory.GetSnapshot();
        }
    }
}
