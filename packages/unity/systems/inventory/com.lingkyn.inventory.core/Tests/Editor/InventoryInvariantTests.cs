using System;
using System.Linq;
using NUnit.Framework;

namespace Lingkyn.Inventory.Core.Tests
{
    public sealed class InventoryInvariantTests
    {
        [Test]
        public void DeterministicStatefulSequencePreservesQuantityAndRejectedState()
        {
            var itemId = new ItemDefinitionId("resource");
            var containerId = new ContainerId("bag");
            var definition = new ItemDefinition(itemId, 10, ItemInstanceMode.Fungible);
            var inventory = new InventoryAggregate(
                new InventoryId("owner"),
                new ItemDefinitionCatalog(new[] { definition }),
                new[] { new ContainerDefinition(containerId, 5) });
            Assert.That(inventory.Execute(MutationRequest.Add(new ItemStack(itemId, 25), containerId)).Succeeded, Is.True);

            var random = new Random(73421);
            for (var iteration = 0; iteration < 500; iteration++)
            {
                var before = Describe(inventory.GetSnapshot());
                var source = new SlotAddress(containerId, random.Next(0, 5));
                var destination = new SlotAddress(containerId, random.Next(0, 5));
                var quantity = random.Next(1, 11);
                MutationRequest request;
                switch (random.Next(0, 4))
                {
                    case 0:
                        request = MutationRequest.Move(source, destination, quantity);
                        break;
                    case 1:
                        request = MutationRequest.Split(source, destination, quantity);
                        break;
                    case 2:
                        request = MutationRequest.Merge(source, destination, quantity);
                        break;
                    default:
                        request = MutationRequest.Swap(source, destination);
                        break;
                }

                var result = inventory.Execute(request);
                var snapshot = inventory.GetSnapshot();
                Assert.That(Total(snapshot), Is.EqualTo(25), $"quantity changed at iteration {iteration}");
                Assert.That(snapshot.Containers.SelectMany(container => container.Slots)
                    .Where(stack => stack != null)
                    .All(stack => stack.Quantity >= 1 && stack.Quantity <= 10), Is.True);
                if (!result.Succeeded)
                {
                    Assert.That(Describe(snapshot), Is.EqualTo(before), $"rejected request mutated state at iteration {iteration}");
                }
            }
        }

        private static int Total(InventorySnapshot snapshot) => snapshot.Containers
            .SelectMany(container => container.Slots)
            .Where(stack => stack != null)
            .Sum(stack => stack.Quantity);

        private static string Describe(InventorySnapshot snapshot) => string.Join("|", snapshot.Containers
            .SelectMany(container => container.Slots.Select((stack, index) =>
                stack == null ? $"{index}:empty" : $"{index}:{stack.DefinitionId}:{stack.Quantity}:{stack.InstanceId}")));
    }
}
