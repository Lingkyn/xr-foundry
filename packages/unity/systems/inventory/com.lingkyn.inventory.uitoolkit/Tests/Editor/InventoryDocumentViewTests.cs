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

        [Test]
        public void InjectedSkinRestylesEnabledAndDisabledSlotsAndReachesLaterCreatedSlots()
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(DocumentPath);
            Assert.That(asset, Is.Not.Null);
            var root = asset.CloneTree();
            var gameObject = new GameObject("InventoryDocumentView", typeof(UIDocument), typeof(InventoryDocumentView));
            var skin = InventoryUiToolkitSkin.CreateDefault();
            try
            {
                skin.Surface = Color.yellow;
                skin.Section = Color.gray;
                skin.Accent = Color.cyan;
                skin.TextPrimary = Color.white;
                skin.TextMuted = Color.magenta;
                skin.SlotNormal = Color.red;
                skin.SlotHover = Color.black;
                skin.SlotSelected = Color.green;
                skin.SlotDisabled = new Color(0f, 0f, 1f, 0.25f);

                var view = gameObject.GetComponent<InventoryDocumentView>();
                view.Bind(root);
                // Slot 0 enabled + occupied, slot 1 enabled + selected, slot 2 disabled + empty.
                view.Render(CreateModel(InventoryUiState.Selected, true, true, false));

                view.ApplySkin(skin);

                Assert.That(view.Skin, Is.SameAs(skin));
                var shell = root.Q<VisualElement>(InventoryDocumentContract.Root);
                Assert.That(shell.style.backgroundColor.value, Is.EqualTo(Color.yellow));
                Assert.That(shell.style.color.value, Is.EqualTo(Color.white));
                Assert.That(root.Q<VisualElement>(InventoryDocumentContract.Grid).style.backgroundColor.value,
                    Is.EqualTo(Color.gray));
                Assert.That(root.Q<Label>(InventoryDocumentContract.Message).style.color.value,
                    Is.EqualTo(Color.magenta));
                Assert.That(root.Q<Button>(InventoryDocumentContract.PrimaryAction).style.backgroundColor.value,
                    Is.EqualTo(Color.cyan));

                var enabledSlot = view.SlotButtons[0];
                Assert.That(enabledSlot.style.backgroundColor.value, Is.EqualTo(Color.red));
                Assert.That(enabledSlot.style.color.value, Is.EqualTo(Color.white));
                Assert.That(enabledSlot.style.opacity.keyword, Is.EqualTo(StyleKeyword.Null));

                Assert.That(view.SlotButtons[1].style.backgroundColor.value, Is.EqualTo(Color.green));

                var disabledSlot = view.SlotButtons[2];
                Assert.That(disabledSlot.enabledSelf, Is.False);
                Assert.That(disabledSlot.style.backgroundColor.value, Is.EqualTo(new Color(0f, 0f, 1f, 1f)));
                Assert.That(disabledSlot.style.opacity.value, Is.EqualTo(0.25f));
                Assert.That(disabledSlot.style.color.value, Is.EqualTo(Color.magenta));

                // Selecting a different slot moves the selected token with it.
                Assert.That(view.TrySelect(0), Is.True);
                Assert.That(view.SlotButtons[0].style.backgroundColor.value, Is.EqualTo(Color.green));
                Assert.That(view.SlotButtons[1].style.backgroundColor.value, Is.EqualTo(Color.red));

                // Slots created by a later render receive the same skin without re-injection.
                view.Render(CreateModel(InventoryUiState.Partial, true, false, true));
                Assert.That(view.SlotButtons[0].style.backgroundColor.value, Is.EqualTo(Color.red));
                Assert.That(view.SlotButtons[1].style.backgroundColor.value, Is.EqualTo(new Color(0f, 0f, 1f, 1f)));
                Assert.That(view.SlotButtons[1].style.opacity.value, Is.EqualTo(0.25f));
                Assert.That(view.SlotButtons[2].style.backgroundColor.value, Is.EqualTo(Color.red));

                // The interaction gate dims every slot through the disabled token and restores it.
                view.SetInteractionEnabled(false);
                Assert.That(view.SlotButtons.All(button =>
                    button.style.backgroundColor.value == new Color(0f, 0f, 1f, 1f) &&
                    button.style.opacity.value == 0.25f), Is.True);
                view.SetInteractionEnabled(true);
                Assert.That(view.SlotButtons[0].style.backgroundColor.value, Is.EqualTo(Color.red));
                Assert.That(view.SlotButtons[0].style.opacity.keyword, Is.EqualTo(StyleKeyword.Null));

                // Removing the skin clears every inline value the seam wrote.
                view.ApplySkin(null);
                Assert.That(view.Skin, Is.Null);
                Assert.That(shell.style.backgroundColor.keyword, Is.EqualTo(StyleKeyword.Null));
                Assert.That(view.SlotButtons.All(button =>
                    button.style.backgroundColor.keyword == StyleKeyword.Null &&
                    button.style.color.keyword == StyleKeyword.Null &&
                    button.style.opacity.keyword == StyleKeyword.Null), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(skin);
            }
        }

        [Test]
        public void WithoutSkinStylesheetDrivesSlotsAndDefaultSkinCarriesCanonicalTokens()
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(DocumentPath);
            Assert.That(asset, Is.Not.Null);
            var root = asset.CloneTree();
            var gameObject = new GameObject("InventoryDocumentView", typeof(UIDocument), typeof(InventoryDocumentView));
            var skin = InventoryUiToolkitSkin.CreateDefault();
            try
            {
                var view = gameObject.GetComponent<InventoryDocumentView>();
                view.Bind(root);
                view.Render(CreateModel(InventoryUiState.Selected, true, true, false));
                view.SetInteractionEnabled(false);
                view.SetInteractionEnabled(true);

                // No skin: the seam writes nothing inline, so the USS selectors and the existing
                // state classes remain the only source of the look.
                Assert.That(view.Skin, Is.Null);
                var shell = root.Q<VisualElement>(InventoryDocumentContract.Root);
                Assert.That(shell.style.backgroundColor.keyword, Is.EqualTo(StyleKeyword.Null));
                Assert.That(shell.style.color.keyword, Is.EqualTo(StyleKeyword.Null));
                Assert.That(root.Q<Button>(InventoryDocumentContract.PrimaryAction).style.backgroundColor.keyword,
                    Is.EqualTo(StyleKeyword.Null));
                Assert.That(view.SlotButtons.All(button =>
                    button.style.backgroundColor.keyword == StyleKeyword.Null &&
                    button.style.color.keyword == StyleKeyword.Null &&
                    button.style.opacity.keyword == StyleKeyword.Null), Is.True);
                Assert.That(view.SlotButtons[1].ClassListContains("inventory-slot--selected"), Is.True);
                Assert.That(view.SlotButtons[2].ClassListContains("inventory-slot--disabled"), Is.True);
                Assert.That(view.SlotButtons[2].ClassListContains("inventory-slot--empty"), Is.True);
                Assert.That(view.SlotButtons[2].enabledSelf, Is.False);

                // The default skin carries the canonical design-language token values.
                Assert.That(skin.Surface, Is.EqualTo(new Color(0.031f, 0.094f, 0.122f, 0.96f)));
                Assert.That(skin.Section, Is.EqualTo(new Color(0.055f, 0.129f, 0.153f, 0.96f)));
                Assert.That(skin.Accent, Is.EqualTo(new Color(0.122f, 0.592f, 0.624f, 1f)));
                Assert.That(skin.TextPrimary, Is.EqualTo(new Color(0.886f, 0.988f, 1f, 1f)));
                Assert.That(skin.TextMuted, Is.EqualTo(new Color(0.439f, 0.616f, 0.639f, 1f)));
                Assert.That(skin.SlotNormal, Is.EqualTo(new Color(0.071f, 0.212f, 0.255f, 1f)));
                Assert.That(skin.SlotHover, Is.EqualTo(new Color(0.090f, 0.345f, 0.388f, 1f)));
                Assert.That(skin.SlotSelected, Is.EqualTo(new Color(0.094f, 0.494f, 0.525f, 1f)));
                Assert.That(skin.SlotDisabled, Is.EqualTo(new Color(0.071f, 0.212f, 0.255f, 0.46f)));
                Assert.That(skin.SlotDisabled, Is.EqualTo(InventoryUiToolkitSkin.CanonicalSlotDisabled));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(skin);
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
