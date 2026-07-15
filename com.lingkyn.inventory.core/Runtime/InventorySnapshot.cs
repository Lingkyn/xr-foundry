using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Lingkyn.Inventory.Core
{
    public sealed class ContainerSnapshot
    {
        private readonly ReadOnlyCollection<ItemStack> _slots;

        internal ContainerSnapshot(ContainerId id, ItemStack[] slots)
        {
            Id = id;
            _slots = new ReadOnlyCollection<ItemStack>(slots.Select(Clone).ToArray());
        }

        public ContainerId Id { get; }
        public int Capacity => _slots.Count;
        public IReadOnlyList<ItemStack> Slots => _slots;

        public ItemStack Get(SlotAddress address)
        {
            if (address.ContainerId != Id)
            {
                throw new ArgumentException("The address belongs to a different container.", nameof(address));
            }

            if (address.Index >= Capacity)
            {
                throw new ArgumentOutOfRangeException(nameof(address));
            }

            return Clone(_slots[address.Index]);
        }

        internal ItemStack[] CopySlots() => _slots.Select(Clone).ToArray();

        internal static ItemStack Clone(ItemStack stack)
        {
            return stack == null ? null : new ItemStack(stack.DefinitionId, stack.Quantity, stack.InstanceId);
        }
    }

    public sealed class InventorySnapshot
    {
        private readonly ReadOnlyDictionary<ContainerId, ContainerSnapshot> _containers;

        internal InventorySnapshot(InventoryId inventoryId, long revision, IDictionary<ContainerId, ItemStack[]> containers)
        {
            InventoryId = inventoryId;
            Revision = revision;
            _containers = new ReadOnlyDictionary<ContainerId, ContainerSnapshot>(
                containers.ToDictionary(pair => pair.Key, pair => new ContainerSnapshot(pair.Key, pair.Value)));
        }

        public InventoryId InventoryId { get; }
        public long Revision { get; }
        public IEnumerable<ContainerSnapshot> Containers => _containers.Values;

        public bool TryGetContainer(ContainerId id, out ContainerSnapshot container) => _containers.TryGetValue(id, out container);

        public ItemStack Get(SlotAddress address)
        {
            if (!_containers.TryGetValue(address.ContainerId, out var container))
            {
                throw new KeyNotFoundException($"Unknown container: {address.ContainerId}");
            }

            return container.Get(address);
        }

        internal Dictionary<ContainerId, ItemStack[]> CopyState()
        {
            return _containers.ToDictionary(pair => pair.Key, pair => pair.Value.CopySlots());
        }
    }
}
