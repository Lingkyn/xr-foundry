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

        [Test]
        public void MultipleSeededSequencesPreserveConservationUniqueIdentityAndBounds()
        {
            var resourceId = new ItemDefinitionId("resource");
            var relicId = new ItemDefinitionId("relic");
            var relicInstance = new ItemInstanceId("relic-1");
            var bagId = new ContainerId("bag");
            var stashId = new ContainerId("stash");

            foreach (var seed in new[] { 1, 2, 3, 4 })
            {
                var inventory = new InventoryAggregate(
                    new InventoryId("owner"),
                    new ItemDefinitionCatalog(new[]
                    {
                        new ItemDefinition(resourceId, 10, ItemInstanceMode.Fungible),
                        new ItemDefinition(relicId, 1, ItemInstanceMode.Unique),
                    }),
                    new[] { new ContainerDefinition(bagId, 4), new ContainerDefinition(stashId, 3) });
                Assert.That(inventory.Execute(MutationRequest.Add(new ItemStack(resourceId, 25), bagId)).Succeeded, Is.True);
                Assert.That(inventory.Execute(MutationRequest.Add(new ItemStack(relicId, 1, relicInstance), stashId)).Succeeded, Is.True);

                var random = new Random(seed);
                var containers = new[] { bagId, stashId };
                for (var iteration = 0; iteration < 300; iteration++)
                {
                    var before = Describe(inventory.GetSnapshot());
                    var source = new SlotAddress(containers[random.Next(0, 2)], random.Next(0, 4));
                    var destination = new SlotAddress(containers[random.Next(0, 2)], random.Next(0, 4));
                    var quantity = random.Next(1, 11);
                    MutationRequest request;
                    switch (random.Next(0, 5))
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
                        case 3:
                            request = MutationRequest.Transfer(source, destination, quantity);
                            break;
                        default:
                            request = MutationRequest.Swap(source, destination);
                            break;
                    }

                    var result = inventory.Execute(request);
                    var snapshot = inventory.GetSnapshot();
                    var stacks = snapshot.Containers.SelectMany(container => container.Slots).Where(stack => stack != null).ToList();

                    Assert.That(stacks.Where(stack => stack.DefinitionId == resourceId).Sum(stack => stack.Quantity),
                        Is.EqualTo(25), $"seed {seed}: resource quantity changed at iteration {iteration}");
                    Assert.That(stacks.Where(stack => stack.DefinitionId == resourceId).All(stack => stack.Quantity >= 1 && stack.Quantity <= 10),
                        Is.True, $"seed {seed}: resource stack bounds violated at iteration {iteration}");
                    var relics = stacks.Where(stack => stack.DefinitionId == relicId).ToList();
                    Assert.That(relics.Count, Is.EqualTo(1), $"seed {seed}: relic count changed at iteration {iteration}");
                    Assert.That(relics[0].Quantity, Is.EqualTo(1));
                    Assert.That(relics[0].InstanceId, Is.EqualTo(relicInstance), $"seed {seed}: relic identity changed at iteration {iteration}");
                    Assert.That(stacks.Where(stack => stack.InstanceId.HasValue).Select(stack => stack.InstanceId.Value).Distinct().Count(),
                        Is.EqualTo(stacks.Count(stack => stack.InstanceId.HasValue)), $"seed {seed}: duplicate instance id at iteration {iteration}");
                    foreach (var container in snapshot.Containers)
                    {
                        Assert.That(container.Slots.Count, Is.EqualTo(container.Id == bagId ? 4 : 3), $"seed {seed}: container shape changed at iteration {iteration}");
                    }

                    if (!result.Succeeded)
                    {
                        Assert.That(Describe(snapshot), Is.EqualTo(before), $"seed {seed}: rejected request mutated state at iteration {iteration}");
                    }
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
