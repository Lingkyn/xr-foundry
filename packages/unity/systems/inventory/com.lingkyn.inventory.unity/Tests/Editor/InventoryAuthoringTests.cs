using System;
using System.Linq;
using Lingkyn.Inventory.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Lingkyn.Inventory.Unity.Tests
{
    public sealed class InventoryAuthoringTests
    {
        private const string Root = "Assets/__InventoryAuthoringTests";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(Root);
            AssetDatabase.CreateFolder("Assets", "__InventoryAuthoringTests");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(Root);
            AssetDatabase.Refresh();
        }

        [Test]
        public void ValidAssetsConvertDeterministicallyAndDoNotMutateDuringRuntimeUse()
        {
            var potion = CreateItem("Potion.asset", "potion", 5, ItemInstanceMode.Fungible, "consumable");
            var sword = CreateItem("Sword.asset", "sword", 1, ItemInstanceMode.Unique, "equipment");
            var catalog = CreateAsset<ItemCatalogAsset>("Catalog.asset");
            SetArray(catalog, "items", sword, potion);
            var bag = CreateContainer("Bag.asset", "bag", 4);
            var inventoryAsset = CreateAsset<InventoryDefinitionAsset>("Inventory.asset");
            SetString(inventoryAsset, "stableId", "player");
            SetArray(inventoryAsset, "containers", bag);
            var itemBefore = EditorJsonUtility.ToJson(potion);
            var inventoryBefore = EditorJsonUtility.ToJson(inventoryAsset);

            var domainCatalog = catalog.ToDomain();
            var definition = inventoryAsset.ToDomain();
            var aggregate = definition.CreateAggregate(domainCatalog);
            var mutation = aggregate.Execute(MutationRequest.Add(
                new ItemStack(new ItemDefinitionId("potion"), 3),
                new ContainerId("bag")));

            Assert.That(mutation.Succeeded, Is.True);
            Assert.That(domainCatalog.TryGet(new ItemDefinitionId("potion"), out var potionDomain), Is.True);
            Assert.That(potionDomain.MaximumStack, Is.EqualTo(5));
            Assert.That(definition.Containers.Select(item => item.Id.Value), Is.EqualTo(new[] { "bag" }));
            Assert.That(EditorJsonUtility.ToJson(potion), Is.EqualTo(itemBefore));
            Assert.That(EditorJsonUtility.ToJson(inventoryAsset), Is.EqualTo(inventoryBefore));
        }

        [Test]
        public void ValidationReportsAssetFieldAndCorrectiveCode()
        {
            var first = CreateItem("First.asset", "duplicate", 1, ItemInstanceMode.Unique);
            var second = CreateItem("Second.asset", "duplicate", 2, ItemInstanceMode.Unique);
            var catalog = CreateAsset<ItemCatalogAsset>("Catalog.asset");
            SetArray(catalog, "items", first, null, second);

            var report = InventoryAuthoringValidation.Validate(catalog);

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Diagnostics.Any(item =>
                item.Code == "catalog.item.missing"
                && item.Source == catalog
                && item.FieldPath == "items.Array.data[1]"), Is.True);
            Assert.That(report.Diagnostics.Any(item =>
                item.Code == "catalog.item.duplicateId"
                && AssetDatabase.GetAssetPath(item.Source).EndsWith("Second.asset", StringComparison.Ordinal)), Is.True);
            Assert.That(report.Diagnostics.Any(item =>
                item.Code == "item.unique.stack"
                && item.FieldPath == "maximumStack"), Is.True);
            Assert.Throws<InventoryAuthoringException>(() => catalog.ToDomain());
        }

        [Test]
        public void StableIdSurvivesAssetRenameAndMove()
        {
            var item = CreateItem("Original.asset", "persistent-sword", 1, ItemInstanceMode.Unique);
            var originalPath = AssetDatabase.GetAssetPath(item);
            var renamedPath = $"{Root}/Renamed.asset";
            Assert.That(AssetDatabase.RenameAsset(originalPath, "Renamed"), Is.Empty);
            AssetDatabase.CreateFolder(Root, "Moved");
            var movedPath = $"{Root}/Moved/Renamed.asset";
            Assert.That(AssetDatabase.MoveAsset(renamedPath, movedPath), Is.Empty);
            var moved = AssetDatabase.LoadAssetAtPath<ItemDefinitionAsset>(movedPath);

            Assert.That(moved.StableId, Is.EqualTo("persistent-sword"));
            Assert.That(moved.ToDomain().Id.Value, Is.EqualTo("persistent-sword"));
        }

        [Test]
        public void InvalidInventoryReferencesAndDuplicateContainersAreActionable()
        {
            var first = CreateContainer("FirstBag.asset", "bag", 0);
            var second = CreateContainer("SecondBag.asset", "bag", 2);
            var inventory = CreateAsset<InventoryDefinitionAsset>("Inventory.asset");
            SetString(inventory, "stableId", "player");
            SetArray(inventory, "containers", first, null, second);

            var report = InventoryAuthoringValidation.Validate(inventory);

            Assert.That(report.Diagnostics.Any(item => item.Code == "container.capacity.invalid"), Is.True);
            Assert.That(report.Diagnostics.Any(item => item.Code == "inventory.container.missing"), Is.True);
            Assert.That(report.Diagnostics.Any(item => item.Code == "inventory.container.duplicateId"), Is.True);
        }

        private static ItemDefinitionAsset CreateItem(
            string fileName,
            string id,
            int maximumStack,
            ItemInstanceMode mode,
            params string[] tags)
        {
            var asset = CreateAsset<ItemDefinitionAsset>(fileName);
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("stableId").stringValue = id;
            serialized.FindProperty("maximumStack").intValue = maximumStack;
            serialized.FindProperty("instanceMode").enumValueIndex = (int)mode;
            var property = serialized.FindProperty("tags");
            property.arraySize = tags.Length;
            for (var index = 0; index < tags.Length; index++)
            {
                property.GetArrayElementAtIndex(index).stringValue = tags[index];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        private static ContainerDefinitionAsset CreateContainer(string fileName, string id, int capacity)
        {
            var asset = CreateAsset<ContainerDefinitionAsset>(fileName);
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("stableId").stringValue = id;
            serialized.FindProperty("capacity").intValue = capacity;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        private static T CreateAsset<T>(string fileName) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, $"{Root}/{fileName}");
            AssetDatabase.SaveAssets();
            return asset;
        }

        private static void SetString(UnityEngine.Object asset, string fieldName, string value)
        {
            var serialized = new SerializedObject(asset);
            serialized.FindProperty(fieldName).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }

        private static void SetArray<T>(UnityEngine.Object asset, string fieldName, params T[] values)
            where T : UnityEngine.Object
        {
            var serialized = new SerializedObject(asset);
            var property = serialized.FindProperty(fieldName);
            property.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }
    }
}
