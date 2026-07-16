using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Lingkyn.Inventory.Core;
using UnityEngine;

namespace Lingkyn.Inventory.Unity
{
    [CreateAssetMenu(menuName = "Lingkyn/Inventory/Item Definition", fileName = "ItemDefinition")]
    public sealed class ItemDefinitionAsset : ScriptableObject
    {
        [SerializeField] private string stableId = string.Empty;
        [SerializeField, Min(1)] private int maximumStack = 1;
        [SerializeField] private ItemInstanceMode instanceMode = ItemInstanceMode.Fungible;
        [SerializeField] private string[] tags = Array.Empty<string>();

        public string StableId => stableId;
        public int MaximumStack => maximumStack;
        public ItemInstanceMode InstanceMode => instanceMode;
        public IReadOnlyList<string> Tags => tags ?? Array.Empty<string>();

        public ItemDefinition ToDomain()
        {
            InventoryAuthoringValidation.ThrowIfInvalid(this);
            return new ItemDefinition(
                new ItemDefinitionId(stableId),
                maximumStack,
                instanceMode,
                tags);
        }
    }

    [CreateAssetMenu(menuName = "Lingkyn/Inventory/Item Catalog", fileName = "ItemCatalog")]
    public sealed class ItemCatalogAsset : ScriptableObject
    {
        [SerializeField] private ItemDefinitionAsset[] items = Array.Empty<ItemDefinitionAsset>();

        public IReadOnlyList<ItemDefinitionAsset> Items => items ?? Array.Empty<ItemDefinitionAsset>();

        public ItemDefinitionCatalog ToDomain()
        {
            InventoryAuthoringValidation.ThrowIfInvalid(this);
            return new ItemDefinitionCatalog(Items
                .OrderBy(item => item.StableId, StringComparer.Ordinal)
                .Select(item => item.ToDomain()));
        }
    }

    [CreateAssetMenu(menuName = "Lingkyn/Inventory/Container Definition", fileName = "ContainerDefinition")]
    public sealed class ContainerDefinitionAsset : ScriptableObject
    {
        [SerializeField] private string stableId = string.Empty;
        [SerializeField, Min(1)] private int capacity = 1;

        public string StableId => stableId;
        public int Capacity => capacity;

        public ContainerDefinition ToDomain()
        {
            InventoryAuthoringValidation.ThrowIfInvalid(this);
            return new ContainerDefinition(new ContainerId(stableId), capacity);
        }
    }

    public sealed class InventoryAuthoringDefinition
    {
        internal InventoryAuthoringDefinition(InventoryId id, IEnumerable<ContainerDefinition> containers)
        {
            Id = id;
            Containers = new ReadOnlyCollection<ContainerDefinition>(containers.ToArray());
        }

        public InventoryId Id { get; }
        public IReadOnlyList<ContainerDefinition> Containers { get; }

        public InventoryAggregate CreateAggregate(
            IItemDefinitionCatalog catalog,
            IEnumerable<IInventoryPolicy> policies = null,
            ItemStateFragmentRegistry stateFragmentRegistry = null) =>
            new InventoryAggregate(Id, catalog, Containers, policies, stateFragmentRegistry);
    }

    [CreateAssetMenu(menuName = "Lingkyn/Inventory/Inventory Definition", fileName = "InventoryDefinition")]
    public sealed class InventoryDefinitionAsset : ScriptableObject
    {
        [SerializeField] private string stableId = string.Empty;
        [SerializeField] private ContainerDefinitionAsset[] containers = Array.Empty<ContainerDefinitionAsset>();

        public string StableId => stableId;
        public IReadOnlyList<ContainerDefinitionAsset> Containers => containers ?? Array.Empty<ContainerDefinitionAsset>();

        public InventoryAuthoringDefinition ToDomain()
        {
            InventoryAuthoringValidation.ThrowIfInvalid(this);
            return new InventoryAuthoringDefinition(
                new InventoryId(stableId),
                Containers
                    .OrderBy(container => container.StableId, StringComparer.Ordinal)
                    .Select(container => container.ToDomain()));
        }
    }
}
