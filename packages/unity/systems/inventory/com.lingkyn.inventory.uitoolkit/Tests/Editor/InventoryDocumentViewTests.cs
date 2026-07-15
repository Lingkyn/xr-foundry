using System.Linq;
using Lingkyn.Inventory.Core;
using Lingkyn.Inventory.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lingkyn.Inventory.UIToolkit.Tests
{
    public sealed class InventoryDocumentViewTests
    {
        private const string DocumentPath =
            "Packages/com.lingkyn.inventory.uitoolkit/Runtime/UI/InventoryDocument.uxml";

        [Test]
        public void ShippedDocumentBindsVisibleStatesAndStableSemanticIntents()
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(DocumentPath);
            Assert.That(asset, Is.Not.Null);
            var root = asset.CloneTree();
            var gameObject = new GameObject("InventoryDocumentView", typeof(UIDocument), typeof(InventoryDocumentView));
            try
            {
                var view = gameObject.GetComponent<InventoryDocumentView>();
                view.Bind(root);
                var model = CreateModel(InventoryUiState.Selected, true, true, false);
                InventorySlotIntent? selected = null;
                InventorySlotIntent? activated = null;
                view.SelectionRequested += value => selected = value;
                view.ActivationRequested += value => activated = value;

                view.Render(model);

                Assert.That(view.LastModel, Is.SameAs(model));
                Assert.That(view.SlotButtons.Count, Is.EqualTo(3));
                Assert.That(view.SlotButtons.All(button => button.parent != null), Is.True);
                Assert.That(root.Q<Label>(InventoryDocumentContract.State).text, Is.EqualTo("Selected"));
                Assert.That(root.Q<VisualElement>(InventoryDocumentContract.Root)
                    .ClassListContains("inventory-state--selected"), Is.True);
                Assert.That(view.SlotButtons[2].enabledSelf, Is.False);

                Assert.That(view.TrySelect(1), Is.True);
                Assert.That(view.TryActivate(1), Is.True);
                Assert.That(selected.Value.Address, Is.EqualTo(model.Slots[1].Address));
                Assert.That(selected.Value.DisplayIndex, Is.EqualTo(1));
                Assert.That(activated.Value.Address, Is.EqualTo(model.Slots[1].Address));
                Assert.That(view.TryActivate(2), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ReplacementDocumentFailsWhenNamedContractIsIncomplete()
        {
            var gameObject = new GameObject("InventoryDocumentView", typeof(UIDocument), typeof(InventoryDocumentView));
            try
            {
                var view = gameObject.GetComponent<InventoryDocumentView>();
                var incomplete = new VisualElement();
                incomplete.Add(new VisualElement { name = InventoryDocumentContract.Root });

                Assert.That(
                    () => view.Bind(incomplete),
                    Throws.InvalidOperationException.With.Message.Contains(InventoryDocumentContract.Grid));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        private static InventoryViewModel CreateModel(InventoryUiState state, params bool[] enabled)
        {
            var slots = Enumerable.Range(0, enabled.Length).Select(index => new InventorySlotViewModel(
                new SlotAddress(new ContainerId("pack"), index),
                index == 2 ? (ItemDefinitionId?)null : new ItemDefinitionId($"item-{index}"),
                index == 2 ? 0 : index + 1,
                state == InventoryUiState.Selected && index == 1,
                enabled[index]));
            return new InventoryViewModel(4, state, slots, "Replayable state");
        }
    }
}
