using System;
using System.Linq;
using Lingkyn.Inventory.Core;
using Lingkyn.Inventory.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Lingkyn.Inventory.UGUI.Tests
{
    public sealed class InventoryPresenterAndPrefabTests
    {
        private const string Root = "Packages/com.lingkyn.inventory.ugui/Runtime/Prefabs";

        [Test]
        public void StateGalleryReplaysSemanticallyDistinctVisibleStates()
        {
            var instance = UnityEngine.Object.Instantiate(Load("InventoryStateGallery"));
            var gallery = instance.GetComponent<InventoryStateGallery>();

            foreach (InventoryUiState state in Enum.GetValues(typeof(InventoryUiState)))
            {
                gallery.ReplayState(state);
                var model = gallery.LastModel;
                var occupied = model.Slots.Count(slot => slot.DefinitionId.HasValue);
                var expectedOccupied = state == InventoryUiState.Empty || state == InventoryUiState.Loading || state == InventoryUiState.Error
                    ? 0
                    : state == InventoryUiState.Full
                        ? 3
                        : 2;

                Assert.That(model.State, Is.EqualTo(state));
                Assert.That(occupied, Is.EqualTo(expectedOccupied), state.ToString());
                Assert.That(model.Slots.Count(slot => slot.Selected), Is.EqualTo(state == InventoryUiState.Selected ? 1 : 0));
                Assert.That(model.Slots.All(slot => slot.Enabled),
                    Is.EqualTo(state != InventoryUiState.Disabled && state != InventoryUiState.Loading && state != InventoryUiState.Error));
                Assert.That(gallery.Shell.Panel.Details.Label.text, Does.Contain(state.ToString()));
                Assert.That(gallery.Shell.Panel.ActionMenu.Group.interactable,
                    Is.EqualTo(state != InventoryUiState.Disabled && state != InventoryUiState.Loading && state != InventoryUiState.Error));
            }

            UnityEngine.Object.DestroyImmediate(instance);
        }

        [Test]
        public void InjectedSkinDrivesSlotStateColorsAcrossPooledSlots()
        {
            var instance = UnityEngine.Object.Instantiate(Load("InventoryShell"));
            var shell = instance.GetComponent<InventoryShellView>();
            var skin = ScriptableObject.CreateInstance<InventorySkin>();
            skin.slotNormalColor = new Color(0.11f, 0.22f, 0.33f, 1f);
            skin.slotDisabledColor = new Color(0.44f, 0.44f, 0.44f, 0.5f);
            shell.ApplySkin(skin);

            shell.Render(CreateModel(InventoryUiState.Disabled, "disabled", true, false, true));
            ForceLayout(shell);
            var grid = shell.Panel.Grid;

            Assert.That(grid.SlotViews[0].Background.color, Is.EqualTo(skin.slotNormalColor),
                "An enabled slot must adopt the injected skin's normal color.");
            Assert.That(grid.SlotViews[1].Background.color, Is.EqualTo(skin.slotDisabledColor),
                "A disabled slot must adopt the injected skin's disabled color.");
            Assert.That(grid.SlotViews[2].Background.color, Is.EqualTo(skin.slotNormalColor));

            UnityEngine.Object.DestroyImmediate(instance);
            UnityEngine.Object.DestroyImmediate(skin);
        }

        [Test]
        public void DisabledPresenterRemainsDisabledAcrossRefreshAndExternalAggregateChanges()
        {
            var item = new ItemDefinitionId("item");
            var container = new ContainerId("bag");
            var aggregate = new InventoryAggregate(
                new InventoryId("player"),
                new ItemDefinitionCatalog(new[] { new ItemDefinition(item, 5, ItemInstanceMode.Fungible) }),
                new[] { new ContainerDefinition(container, 2) });
            var shellObject = UnityEngine.Object.Instantiate(Load("InventoryShell"));
            var shell = shellObject.GetComponent<InventoryShellView>();
            using var presenter = new InventoryPresenter(aggregate, shell);

            presenter.SetDisabled(true);
            var external = aggregate.Execute(MutationRequest.Add(new ItemStack(item, 1), container));
            Assert.That(external.Succeeded, Is.True);
            Assert.That(presenter.Current.State, Is.EqualTo(InventoryUiState.Disabled));
            Assert.That(shell.Panel.ActionMenu.Group.interactable, Is.False);
            Assert.That(shell.Panel.ActionMenu.Group.blocksRaycasts, Is.False);
            Assert.That(shell.Panel.ActionMenu.PrimaryAction.interactable, Is.False);

            presenter.Refresh();
            Assert.That(presenter.Current.State, Is.EqualTo(InventoryUiState.Disabled));
            Assert.That(() => presenter.Select(new SlotAddress(container, 0)),
                Throws.InvalidOperationException.With.Message.Contains("disabled"));
            Assert.That(() => presenter.Replay(InventoryUiState.Selected),
                Throws.InvalidOperationException.With.Message.Contains("disabled"));
            Assert.That(presenter.Current.State, Is.EqualTo(InventoryUiState.Disabled));

            UnityEngine.Object.DestroyImmediate(shellObject);
        }

        [Test]
        public void ShippedPrefabsHaveNoMissingScriptsAndAllFunctionalReferencesAreWired()
        {
            var names = new[]
            {
                "ItemView", "InventorySlot", "InventorySlotCompact", "InventoryGrid",
                "ItemDetails", "ActionMenu", "InventoryPanel", "InventoryShell", "InventoryStateGallery",
            };

            foreach (var name in names)
            {
                var prefab = Load(name);
                Assert.That(prefab, Is.Not.Null, name);
                var missing = prefab.GetComponentsInChildren<Transform>(true)
                    .Sum(item => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject));
                Assert.That(missing, Is.Zero, $"{name} contains missing scripts.");
            }

            var item = Load("ItemView").GetComponent<InventoryItemView>();
            Assert.That(item.Label, Is.Not.Null);
            Assert.That(item.Label.font, Is.Not.Null);
            Assert.That(item.Label.raycastTarget, Is.False);

            var slot = Load("InventorySlot").GetComponent<InventorySlotView>();
            Assert.That(slot.ItemView, Is.Not.Null);
            Assert.That(slot.Background, Is.Not.Null);
            Assert.That(slot.Background.raycastTarget, Is.True);
            Assert.That(slot.SelectionControl, Is.Not.Null);
            Assert.That(slot.SelectionControl.targetGraphic, Is.SameAs(slot.Background));

            var grid = Load("InventoryGrid").GetComponent<InventoryGridView>();
            var scroll = grid.GetComponent<ScrollRect>();
            Assert.That(grid.ContentRoot, Is.Not.Null);
            Assert.That(grid.SlotTemplate, Is.Not.Null);
            Assert.That(grid.ContentRoot.GetComponent<GridLayoutGroup>(), Is.Not.Null);
            Assert.That(grid.ContentRoot.GetComponent<ContentSizeFitter>(), Is.Not.Null);
            Assert.That(scroll, Is.Not.Null);
            Assert.That(scroll.content, Is.SameAs(grid.ContentRoot));
            Assert.That(scroll.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
            Assert.That(scroll.vertical, Is.True);
            Assert.That(scroll.horizontal, Is.False);

            var details = Load("ItemDetails").GetComponent<InventoryDetailsView>();
            Assert.That(details.Label, Is.Not.Null);
            Assert.That(details.Label.raycastTarget, Is.False);

            var actions = Load("ActionMenu").GetComponent<InventoryActionMenuView>();
            Assert.That(actions.Group, Is.Not.Null);
            Assert.That(actions.PrimaryAction, Is.Not.Null);
            Assert.That(actions.PrimaryAction.targetGraphic, Is.Not.Null);
            Assert.That(((Graphic)actions.PrimaryAction.targetGraphic).raycastTarget, Is.True);
            Assert.That(actions.Label, Is.Not.Null);
            Assert.That(actions.Label.raycastTarget, Is.False);

            var panel = Load("InventoryPanel").GetComponent<InventoryPanelView>();
            Assert.That(panel.Grid, Is.Not.Null);
            Assert.That(panel.Details, Is.Not.Null);
            Assert.That(panel.ActionMenu, Is.Not.Null);
            Assert.That(panel.GetComponent<Image>().raycastTarget, Is.False);

            var shell = Load("InventoryShell").GetComponent<InventoryShellView>();
            Assert.That(shell.Panel, Is.Not.Null);
            Assert.That(shell.GetComponent<Canvas>(), Is.Null, "UGUI shell must remain render-mode neutral.");

            var gallery = Load("InventoryStateGallery").GetComponent<InventoryStateGallery>();
            Assert.That(gallery.Shell, Is.Not.Null);
        }

        [Test]
        public void NestedPrefabRolesAndVariantRetainIndependentSourceLinksAndBindings()
        {
            var shellAsset = Load("InventoryShell");
            var panelAsset = Load("InventoryPanel");
            var gridAsset = Load("InventoryGrid");
            var slotAsset = Load("InventorySlot");
            var itemAsset = Load("ItemView");
            var variant = Load("InventorySlotCompact");

            Assert.That(SourceOf(shellAsset.transform.GetChild(0).gameObject), Is.EqualTo(panelAsset));
            Assert.That(SourceOf(shellAsset.GetComponent<InventoryShellView>().Panel.gameObject), Is.EqualTo(panelAsset));

            var panelSources = Enumerable.Range(0, panelAsset.transform.childCount)
                .Select(index => SourceOf(panelAsset.transform.GetChild(index).gameObject)).ToArray();
            Assert.That(panelSources, Does.Contain(gridAsset));
            Assert.That(SourceOf(panelAsset.GetComponent<InventoryPanelView>().Grid.gameObject), Is.EqualTo(gridAsset));
            Assert.That(SourceOf(gridAsset.GetComponent<InventoryGridView>().SlotTemplate.gameObject), Is.EqualTo(slotAsset));
            Assert.That(SourceOf(slotAsset.transform.GetChild(0).gameObject), Is.EqualTo(itemAsset));
            Assert.That(SourceOf(slotAsset.GetComponent<InventorySlotView>().ItemView.gameObject), Is.EqualTo(itemAsset));
            Assert.That(PrefabUtility.GetPrefabAssetType(variant), Is.EqualTo(PrefabAssetType.Variant));
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(variant), Is.EqualTo(slotAsset));
            Assert.That(variant.GetComponent<InventorySlotView>().ItemView, Is.Not.Null);
        }

        [Test]
        public void ShippedShellPreservesStableAddressesAndDoesNotDuplicateIntentSubscriptions()
        {
            var instance = UnityEngine.Object.Instantiate(Load("InventoryShell"));
            var shell = instance.GetComponent<InventoryShellView>();
            var model = CreateModel(InventoryUiState.Selected, "ready", true, true, true);

            shell.Render(model);
            shell.Render(model);
            ForceLayout(shell);

            var grid = shell.Panel.Grid;
            Assert.That(grid.ActiveSlotCount, Is.EqualTo(3));
            Assert.That(grid.SlotViews.Take(3).Select(slot => slot.SlotIndex), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(grid.SlotViews.Take(3).Select(slot => slot.Address), Is.EqualTo(model.Slots.Select(slot => (SlotAddress?)slot.Address)));
            Assert.That(grid.SlotViews.Take(3).All(slot => slot.RectTransform.rect.width > 0f && slot.RectTransform.rect.height > 0f), Is.True);
            Assert.That(grid.SlotViews.Take(3).Select(slot => slot.RectTransform.anchoredPosition).Distinct().Count(), Is.EqualTo(3));
            Assert.That(grid.SlotViews[0].ItemView.Label.text, Does.Contain("item-a"));
            Assert.That(grid.SlotViews[1].ItemView.Label.text, Does.Contain("item-b"));

            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            InventorySlotIntent? received = null;
            var activations = 0;
            grid.ActivationRequested += intent =>
            {
                activations++;
                received = intent;
            };
            ExecuteEvents.Execute(grid.SlotViews[1].gameObject, LeftPointer(), ExecuteEvents.pointerClickHandler);
            Assert.That(activations, Is.EqualTo(1), "Repeated Render must not duplicate intent subscriptions.");
            Assert.That(received.Value.Address, Is.EqualTo(model.Slots[1].Address));
            Assert.That(received.Value.DisplayIndex, Is.EqualTo(1));

            UnityEngine.Object.DestroyImmediate(eventSystemObject);
            UnityEngine.Object.DestroyImmediate(instance);
        }

        [Test]
        public void ScrollGridHandlesZeroSmallBoundaryAndLargeCapacitiesWithoutGhostState()
        {
            var instance = UnityEngine.Object.Instantiate(Load("InventoryShell"));
            var shell = instance.GetComponent<InventoryShellView>();
            var grid = shell.Panel.Grid;

            foreach (var count in new[] { 0, 1, 6, 7, 64 })
            {
                shell.Render(CreateCountModel(count));
                ForceLayout(shell);
                Assert.That(grid.ActiveSlotCount, Is.EqualTo(count));
                Assert.That(grid.SlotViews.Count(slot => slot.gameObject.activeSelf), Is.EqualTo(count));
                if (count > 6)
                {
                    Assert.That(LayoutUtility.GetPreferredHeight((RectTransform)grid.ContentRoot),
                        Is.GreaterThan(((RectTransform)grid.transform).rect.height));
                }
            }

            shell.Render(CreateCountModel(3));
            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            var reused = grid.SlotViews[2];
            ExecuteEvents.Execute(reused.gameObject, LeftPointer(), ExecuteEvents.pointerEnterHandler);
            ExecuteEvents.Execute(reused.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.selectHandler);
            shell.Render(CreateCountModel(1));
            Assert.That(reused.Address, Is.Null, "A pooled hidden slot must not expose the address from its prior binding.");
            Assert.That(reused.SlotIndex, Is.EqualTo(-1));
            Assert.That(reused.Interactable, Is.False);
            Assert.That(reused.ItemView.Label.text, Is.EqualTo("Empty"));
            shell.Render(CreateCountModel(3));
            Assert.That(reused.Address, Is.EqualTo(CreateCountModel(3).Slots[2].Address));
            Assert.That(reused.Background.color, Is.EqualTo(grid.SlotViews[0].Background.color),
                "A hidden and reused slot must not retain hover/navigation state.");

            UnityEngine.Object.DestroyImmediate(eventSystemObject);
            UnityEngine.Object.DestroyImmediate(instance);
        }

        [Test]
        public void DisabledAndNonPrimaryPointerInputsCannotActivateShippedSlot()
        {
            var instance = UnityEngine.Object.Instantiate(Load("InventoryShell"));
            var shell = instance.GetComponent<InventoryShellView>();
            shell.Render(CreateModel(InventoryUiState.Disabled, "disabled", true, false, true));
            var grid = shell.Panel.Grid;
            var disabled = grid.SlotViews[1];
            var enabled = grid.SlotViews[0];

            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            var activations = 0;
            grid.ActivationRequested += _ => activations++;
            ExecuteEvents.Execute(disabled.gameObject, LeftPointer(), ExecuteEvents.pointerClickHandler);
            ExecuteEvents.Execute(disabled.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            var right = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Right };
            ExecuteEvents.Execute(enabled.gameObject, right, ExecuteEvents.pointerClickHandler);

            Assert.That(disabled.Interactable, Is.False);
            Assert.That(activations, Is.Zero);
            Assert.That(disabled.Background.color, Is.Not.EqualTo(enabled.Background.color));
            Assert.That(shell.Panel.ActionMenu.Group.blocksRaycasts, Is.False);

            UnityEngine.Object.DestroyImmediate(eventSystemObject);
            UnityEngine.Object.DestroyImmediate(instance);
        }

        [Test]
        public void SlotVariantReplacementMustRebindAndThenRendersWithoutChangingGridAsset()
        {
            var gridAsset = Load("InventoryGrid");
            var slotAsset = Load("InventorySlot");
            var variant = Load("InventorySlotCompact");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(gridAsset);
            var grid = instance.GetComponent<InventoryGridView>();
            var content = grid.ContentRoot;
            UnityEngine.Object.DestroyImmediate(grid.SlotTemplate.gameObject);

            Assert.That(() => grid.Render(CreateCountModel(1)), Throws.InvalidOperationException);

            var replacementObject = (GameObject)PrefabUtility.InstantiatePrefab(variant, content);
            var replacement = replacementObject.GetComponent<InventorySlotView>();
            grid.ConfigureTemplate(replacement, content);
            grid.Render(CreateCountModel(2));
            ForceLayoutImmediate((RectTransform)content);

            Assert.That(SourceOf(replacementObject), Is.EqualTo(variant));
            Assert.That(grid.ActiveSlotCount, Is.EqualTo(2));
            Assert.That(grid.SlotViews[0].ItemView, Is.Not.Null);
            Assert.That(content.GetComponent<GridLayoutGroup>().cellSize, Is.EqualTo(new Vector2(144f, 60f)));
            Assert.That(grid.SlotViews[0].RectTransform.rect.size, Is.EqualTo(new Vector2(144f, 60f)),
                "The compact variant's preferred size must remain observable after GridLayoutGroup layout.");
            Assert.That(SourceOf(gridAsset.GetComponent<InventoryGridView>().SlotTemplate.gameObject), Is.EqualTo(slotAsset));
            UnityEngine.Object.DestroyImmediate(instance);
        }

        private static void ForceLayoutImmediate(RectTransform content)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            Canvas.ForceUpdateCanvases();
        }

        private static GameObject Load(string name) =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/{name}.prefab");

        private static GameObject SourceOf(GameObject instance) =>
            PrefabUtility.GetCorrespondingObjectFromSource(instance);

        private static void ForceLayout(InventoryShellView shell)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)shell.Panel.Grid.ContentRoot);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)shell.transform);
            Canvas.ForceUpdateCanvases();
        }

        private static PointerEventData LeftPointer() => new PointerEventData(EventSystem.current)
        {
            button = PointerEventData.InputButton.Left,
        };

        private static InventoryViewModel CreateModel(InventoryUiState state, string message, params bool[] enabled)
        {
            var addresses = new[]
            {
                new SlotAddress(new ContainerId("pack-left"), 4),
                new SlotAddress(new ContainerId("pack-center"), 1),
                new SlotAddress(new ContainerId("pack-right"), 8),
            };
            var items = new[] { "item-a", "item-b", "item-c" };
            var slots = items.Select((item, index) => new InventorySlotViewModel(
                addresses[index],
                new ItemDefinitionId(item),
                index + 1,
                state == InventoryUiState.Selected && index == 1,
                index < enabled.Length && enabled[index]));
            return new InventoryViewModel(7, state, slots, message);
        }

        private static InventoryViewModel CreateCountModel(int count)
        {
            var slots = Enumerable.Range(0, count).Select(index => new InventorySlotViewModel(
                new SlotAddress(new ContainerId($"container-{index % 2}"), index),
                new ItemDefinitionId($"item-{index}"),
                index + 1,
                false,
                true));
            return new InventoryViewModel(1, count == 0 ? InventoryUiState.Empty : InventoryUiState.Partial, slots);
        }

    }
}
