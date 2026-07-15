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
        private readonly ReadOnlyCollection<ItemStateFragment> _stateFragments;

        public ItemInstance(
            ItemInstanceId id,
            ItemDefinitionId definitionId,
            IEnumerable<ItemStateFragment> stateFragments = null)
        {
            IdentifierGuard.Require(id.Value, nameof(id));
            IdentifierGuard.Require(definitionId.Value, nameof(definitionId));
            Id = id;
            DefinitionId = definitionId;
            _stateFragments = ItemStack.NormalizeFragments(stateFragments);
        }

        public ItemInstanceId Id { get; }
        public ItemDefinitionId DefinitionId { get; }
        public IReadOnlyList<ItemStateFragment> StateFragments => _stateFragments;
    }

    public sealed class ItemStack : IEquatable<ItemStack>
    {
        private readonly ReadOnlyCollection<ItemStateFragment> _stateFragments;

        public ItemStack(
            ItemDefinitionId definitionId,
            int quantity,
            ItemInstanceId? instanceId = null,
            IEnumerable<ItemStateFragment> stateFragments = null)
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

            var fragments = NormalizeFragments(stateFragments);
            if (fragments.Count > 0 && !instanceId.HasValue)
            {
                throw new ArgumentException("Instance-state fragments require a unique instance id.", nameof(stateFragments));
            }

            DefinitionId = definitionId;
            Quantity = quantity;
            InstanceId = instanceId;
            _stateFragments = fragments;
        }

        public ItemDefinitionId DefinitionId { get; }
        public int Quantity { get; }
        public ItemInstanceId? InstanceId { get; }
        public bool IsUnique => InstanceId.HasValue;
        public IReadOnlyList<ItemStateFragment> StateFragments => _stateFragments;

        public ItemStack WithQuantity(int quantity) => new ItemStack(DefinitionId, quantity, InstanceId, _stateFragments);

        public bool TryGetState(ItemStateFragmentTypeId typeId, out ItemStateFragment fragment)
        {
            fragment = _stateFragments.FirstOrDefault(candidate => candidate.TypeId == typeId);
            return fragment != null;
        }

        public ItemStack WithState(ItemStateFragment fragment)
        {
            if (!IsUnique)
            {
                throw new InvalidOperationException("Only a unique item instance can carry mutable instance state.");
            }

            if (fragment == null)
            {
                throw new ArgumentNullException(nameof(fragment));
            }

            return new ItemStack(
                DefinitionId,
                Quantity,
                InstanceId,
                _stateFragments.Where(candidate => candidate.TypeId != fragment.TypeId).Concat(new[] { fragment }));
        }

        public ItemStack WithoutState(ItemStateFragmentTypeId typeId)
        {
            if (!IsUnique)
            {
                throw new InvalidOperationException("Only a unique item instance can carry mutable instance state.");
            }

            return new ItemStack(
                DefinitionId,
                Quantity,
                InstanceId,
                _stateFragments.Where(candidate => candidate.TypeId != typeId));
        }

        public bool Equals(ItemStack other)
        {
            return other != null
                && DefinitionId.Equals(other.DefinitionId)
                && Quantity == other.Quantity
                && Nullable.Equals(InstanceId, other.InstanceId)
                && _stateFragments.SequenceEqual(other._stateFragments);
        }

        public override bool Equals(object obj) => Equals(obj as ItemStack);
        public override int GetHashCode()
        {
            var hash = (DefinitionId.GetHashCode() * 397) ^ Quantity;
            hash = (hash * 397) ^ (InstanceId.HasValue ? InstanceId.Value.GetHashCode() : 0);
            foreach (var fragment in _stateFragments)
            {
                hash = (hash * 397) ^ fragment.GetHashCode();
            }

            return hash;
        }

        internal static ReadOnlyCollection<ItemStateFragment> NormalizeFragments(
            IEnumerable<ItemStateFragment> stateFragments)
        {
            var fragments = (stateFragments ?? Array.Empty<ItemStateFragment>()).ToArray();
            if (fragments.Any(fragment => fragment == null))
            {
                throw new ArgumentException("Instance-state fragments cannot contain null values.", nameof(stateFragments));
            }

            var duplicate = fragments.GroupBy(fragment => fragment.TypeId)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicate != null)
            {
                throw new ArgumentException(
                    $"Duplicate instance-state fragment type: {duplicate.Key}",
                    nameof(stateFragments));
            }

            return new ReadOnlyCollection<ItemStateFragment>(fragments
                .OrderBy(fragment => fragment.TypeId.Value, StringComparer.Ordinal)
                .ToArray());
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
