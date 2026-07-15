using System;
using System.Linq;
using System.Reflection;
using Lingkyn.Inventory.Core;
using Lingkyn.Inventory.UGUI.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Lingkyn.Inventory.UGUI.Tests
{
    public sealed class InventoryPresenterAndPrefabTests
    {
        private const string Root = "Packages/com.lingkyn.inventory.ugui/Runtime/Prefabs";

        [Test]
        public void PresenterOwnsMutationAndViewsReceiveReadOnlyModels()
        {
            var item = new ItemDefinitionId("item");
            var container = new ContainerId("bag");
            var aggregate = new InventoryAggregate(
                new InventoryId("player"),
                new ItemDefinitionCatalog(new[] { new ItemDefinition(item, 5, ItemInstanceMode.Fungible) }),
                new[] { new ContainerDefinition(container, 2) });
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
        }

        [Test]
        public void AllRequiredStatesAreReplayable()
        {
            var aggregate = new InventoryAggregate(
                new InventoryId("player"),
                new ItemDefinitionCatalog(Array.Empty<ItemDefinition>()),
                new[] { new ContainerDefinition(new ContainerId("bag"), 1) });
            var view = new RecordingView();
            using var presenter = new InventoryPresenter(aggregate, view);

            foreach (InventoryUiState state in Enum.GetValues(typeof(InventoryUiState)))
            {
                presenter.Replay(state, state.ToString());
                Assert.That(view.Last.State, Is.EqualTo(state));
                Assert.That(view.Last.Message, Is.EqualTo(state.ToString()));
            }
        }

        [Test]
        public void NestedPrefabRolesAndVariantRetainIndependentSourceLinks()
        {
            InventoryPrefabFactory.Rebuild();
            var shell = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/InventoryShell.prefab");
            var panel = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/InventoryPanel.prefab");
            var grid = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/InventoryGrid.prefab");
            var slot = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/InventorySlot.prefab");
            var item = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/ItemView.prefab");
            var variant = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/InventorySlotCompact.prefab");

            Assert.That(shell, Is.Not.Null);
            Assert.That(SourceOf(shell.transform.GetChild(0).gameObject), Is.EqualTo(panel));
            var panelSources = Enumerable.Range(0, panel.transform.childCount)
                .Select(index => SourceOf(panel.transform.GetChild(index).gameObject)).ToArray();
            Assert.That(panelSources, Does.Contain(grid));
            Assert.That(SourceOf(grid.transform.GetChild(0).gameObject), Is.EqualTo(slot));
            Assert.That(SourceOf(slot.transform.GetChild(0).gameObject), Is.EqualTo(item));
            Assert.That(PrefabUtility.GetPrefabAssetType(variant), Is.EqualTo(PrefabAssetType.Variant));
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(variant), Is.EqualTo(slot));
        }

        [Test]
        public void SlotVariantCanReplaceNestedSlotWithoutChangingGridAsset()
        {
            InventoryPrefabFactory.Rebuild();
            var grid = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/InventoryGrid.prefab");
            var variant = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/InventorySlotCompact.prefab");
            var originalSource = SourceOf(grid.transform.GetChild(0).gameObject);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(grid);
            var original = instance.transform.GetChild(0).gameObject;
            UnityEngine.Object.DestroyImmediate(original);
            var replacement = (GameObject)PrefabUtility.InstantiatePrefab(variant, instance.transform);

            Assert.That(SourceOf(replacement), Is.EqualTo(variant));
            Assert.That(SourceOf(grid.transform.GetChild(0).gameObject), Is.EqualTo(originalSource));
            UnityEngine.Object.DestroyImmediate(instance);
        }

        private static GameObject SourceOf(GameObject instance) =>
            PrefabUtility.GetCorrespondingObjectFromSource(instance);

        private sealed class RecordingView : IInventoryView
        {
            public InventoryViewModel Last { get; private set; }
            public void Render(InventoryViewModel model) => Last = model;
        }
    }
}
