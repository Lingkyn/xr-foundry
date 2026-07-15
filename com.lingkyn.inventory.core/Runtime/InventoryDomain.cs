using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Lingkyn.Inventory.Core
{
    public enum ItemInstanceMode
    {
        Fungible = 0,
        Unique = 1,
    }

    public sealed class ItemDefinition
    {
        private readonly ReadOnlyCollection<string> _tags;

        public ItemDefinition(
            ItemDefinitionId id,
            int maximumStack,
            ItemInstanceMode instanceMode,
            IEnumerable<string> tags = null)
        {
            IdentifierGuard.Require(id.Value, nameof(id));
            if (maximumStack < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumStack), "Maximum stack must be positive.");
            }

            if (instanceMode == ItemInstanceMode.Unique && maximumStack != 1)
            {
                throw new ArgumentException("Unique item definitions must have a maximum stack of one.", nameof(maximumStack));
            }

            Id = id;
            MaximumStack = maximumStack;
            InstanceMode = instanceMode;
            _tags = new ReadOnlyCollection<string>((tags ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray());
        }

        public ItemDefinitionId Id { get; }
        public int MaximumStack { get; }
        public ItemInstanceMode InstanceMode { get; }
        public IReadOnlyList<string> Tags => _tags;
    }

    public interface IItemDefinitionCatalog
    {
        bool TryGet(ItemDefinitionId id, out ItemDefinition definition);
    }

    public sealed class ItemDefinitionCatalog : IItemDefinitionCatalog
    {
        private readonly Dictionary<ItemDefinitionId, ItemDefinition> _definitions;

        public ItemDefinitionCatalog(IEnumerable<ItemDefinition> definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            _definitions = new Dictionary<ItemDefinitionId, ItemDefinition>();
            foreach (var definition in definitions)
            {
                if (definition == null)
                {
                    throw new ArgumentException("A definition catalog cannot contain null entries.", nameof(definitions));
                }

                if (_definitions.ContainsKey(definition.Id))
                {
                    throw new ArgumentException($"Duplicate item definition id: {definition.Id}", nameof(definitions));
                }

                _definitions.Add(definition.Id, definition);
            }
        }

        public bool TryGet(ItemDefinitionId id, out ItemDefinition definition) => _definitions.TryGetValue(id, out definition);
    }

    public sealed class ItemInstance
    {
        public ItemInstance(ItemInstanceId id, ItemDefinitionId definitionId)
        {
            IdentifierGuard.Require(id.Value, nameof(id));
            IdentifierGuard.Require(definitionId.Value, nameof(definitionId));
            Id = id;
            DefinitionId = definitionId;
        }

        public ItemInstanceId Id { get; }
        public ItemDefinitionId DefinitionId { get; }
    }

    public sealed class ItemStack : IEquatable<ItemStack>
    {
        public ItemStack(ItemDefinitionId definitionId, int quantity, ItemInstanceId? instanceId = null)
        {
            IdentifierGuard.Require(definitionId.Value, nameof(definitionId));
            if (quantity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(quantity), "A stack quantity must be positive.");
            }

            if (instanceId.HasValue && quantity != 1)
            {
                throw new ArgumentException("A stack with a unique instance id must have quantity one.", nameof(quantity));
            }

            DefinitionId = definitionId;
            Quantity = quantity;
            InstanceId = instanceId;
        }

        public ItemDefinitionId DefinitionId { get; }
        public int Quantity { get; }
        public ItemInstanceId? InstanceId { get; }
        public bool IsUnique => InstanceId.HasValue;

        public ItemStack WithQuantity(int quantity) => new ItemStack(DefinitionId, quantity, InstanceId);

        public bool Equals(ItemStack other)
        {
            return other != null
                && DefinitionId.Equals(other.DefinitionId)
                && Quantity == other.Quantity
                && Nullable.Equals(InstanceId, other.InstanceId);
        }

        public override bool Equals(object obj) => Equals(obj as ItemStack);
        public override int GetHashCode()
        {
            var hash = (DefinitionId.GetHashCode() * 397) ^ Quantity;
            return (hash * 397) ^ (InstanceId.HasValue ? InstanceId.Value.GetHashCode() : 0);
        }
    }

    public sealed class ContainerDefinition
    {
        public ContainerDefinition(ContainerId id, int capacity)
        {
            IdentifierGuard.Require(id.Value, nameof(id));
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Container capacity must be positive.");
            }

            Id = id;
            Capacity = capacity;
        }

        public ContainerId Id { get; }
        public int Capacity { get; }
    }
}
