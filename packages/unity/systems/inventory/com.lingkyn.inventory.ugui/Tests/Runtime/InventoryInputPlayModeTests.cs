using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Lingkyn.Inventory.Core;
using Lingkyn.Inventory.Presentation;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Lingkyn.Inventory.UGUI.Tests
{
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor)]
    public sealed class InventoryInputPlayModeTests
    {
        private const string ShellPath = "Packages/com.lingkyn.inventory.ugui/Runtime/Prefabs/InventoryShell.prefab";

        [UnityTest]
        public IEnumerator ShippedPrefabRaycastsAndActivatesDistinctLeftCenterRightSlots()
        {
            var setup = CreateCanvasAndShell();
            setup.Shell.Render(CreateModel(InventoryUiState.Partial, true, true, true));
            yield return null;
            ForceLayout(setup);

            var grid = setup.Shell.Panel.Grid;
            var activations = new List<InventorySlotIntent>();
            grid.ActivationRequested += activations.Add;

            for (var index = 0; index < 3; index++)
            {
                var slot = grid.SlotViews[index];
                var hit = Raycast(setup.EventSystem, slot);
                Assert.That(hit.gameObject, Is.EqualTo(slot.gameObject), $"Slot {index} was not reached through GraphicRaycaster.");
                var pointer = new PointerEventData(setup.EventSystem)
                {
                    button = PointerEventData.InputButton.Left,
                    pointerCurrentRaycast = hit,
                };
                ExecuteEvents.Execute(hit.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            }

            Assert.That(activations.Select(intent => intent.DisplayIndex), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(activations.Select(intent => intent.Address), Is.EqualTo(setup.Shell.LastModel.Slots.Select(slot => slot.Address)));
            Assert.That(typeof(InventorySlotView).Assembly.GetReferencedAssemblies()
                .Any(reference => reference.Name.StartsWith("Unity.XR", System.StringComparison.Ordinal)), Is.False);
            Destroy(setup);
        }

        [UnityTest]
        public IEnumerator KeyboardSubmitUsesSameShippedSlotIntentAndDisabledSuppressesBothPaths()
        {
            var setup = CreateCanvasAndShell();
            setup.Shell.Render(CreateModel(InventoryUiState.Disabled, true, false, true));
            yield return null;
            ForceLayout(setup);

            var grid = setup.Shell.Panel.Grid;
            var activations = new List<InventorySlotIntent>();
            grid.ActivationRequested += activations.Add;

            var enabled = grid.SlotViews[0];
            setup.EventSystem.SetSelectedGameObject(enabled.gameObject);
            ExecuteEvents.Execute(enabled.gameObject, new BaseEventData(setup.EventSystem), ExecuteEvents.submitHandler);

            var disabled = grid.SlotViews[1];
            var hit = Raycast(setup.EventSystem, disabled);
            Assert.That(hit.gameObject, Is.EqualTo(disabled.gameObject), "Disabled state must suppress activation without removing the stable ray target.");
            ExecuteEvents.Execute(hit.gameObject, new PointerEventData(setup.EventSystem)
            {
                button = PointerEventData.InputButton.Left,
                pointerCurrentRaycast = hit,
            }, ExecuteEvents.pointerClickHandler);
            ExecuteEvents.Execute(disabled.gameObject, new BaseEventData(setup.EventSystem), ExecuteEvents.submitHandler);

            Assert.That(activations.Select(intent => intent.DisplayIndex), Is.EqualTo(new[] { 0 }));
            Assert.That(activations[0].Address, Is.EqualTo(setup.Shell.LastModel.Slots[0].Address));
            Assert.That(disabled.Background.color, Is.Not.EqualTo(enabled.Background.color));
            Assert.That(setup.Shell.Panel.ActionMenu.Group.blocksRaycasts, Is.False);
            Destroy(setup);
        }

        [UnityTest]
        public IEnumerator PresenterRendersAggregateIntoShippedShellAndInputOnlyEmitsIntent()
        {
            var setup = CreateCanvasAndShell();
            var item = new ItemDefinitionId("stone");
            var container = new ContainerId("bag");
            var aggregate = new InventoryAggregate(
                new InventoryId("player"),
                new ItemDefinitionCatalog(new[] { new ItemDefinition(item, 5, ItemInstanceMode.Fungible) }),
                new[] { new ContainerDefinition(container, 3) });
            using var presenter = new InventoryPresenter(aggregate, setup.Shell);
            Assert.That(presenter.Execute(MutationRequest.Add(new ItemStack(item, 2), container)).Succeeded, Is.True);
            yield return null;
            ForceLayout(setup);

            var grid = setup.Shell.Panel.Grid;
            Assert.That(grid.ActiveSlotCount, Is.EqualTo(3));
            Assert.That(grid.SlotViews[0].ItemView.Label.text, Does.Contain("stone x2"));
            var revisionBeforeInput = aggregate.Revision;
            InventorySlotIntent? intent = null;
            grid.ActivationRequested += next => intent = next;
            var hit = Raycast(setup.EventSystem, grid.SlotViews[0]);
            Assert.That(hit.gameObject, Is.EqualTo(grid.SlotViews[0].gameObject));
            ExecuteEvents.Execute(hit.gameObject, new PointerEventData(setup.EventSystem)
            {
                button = PointerEventData.InputButton.Left,
                pointerCurrentRaycast = hit,
            }, ExecuteEvents.pointerClickHandler);

            Assert.That(intent.HasValue, Is.True);
            Assert.That(intent.Value.Address, Is.EqualTo(new SlotAddress(container, 0)));
            Assert.That(aggregate.Revision, Is.EqualTo(revisionBeforeInput), "A view intent must not mutate Inventory storage directly.");
            Destroy(setup);
        }

        private static Setup CreateCanvasAndShell()
        {
            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            GameObject shellObject;
#if UNITY_EDITOR
            var shellAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ShellPath);
            Assert.That(shellAsset, Is.Not.Null);
            shellObject = UnityEngine.Object.Instantiate(shellAsset, canvas.transform);
#else
            throw new System.NotSupportedException("The package prefab PlayMode suite runs in the Unity Editor consumer.");
#endif
            var shellRect = (RectTransform)shellObject.transform;
            shellRect.anchorMin = new Vector2(0.5f, 0.5f);
            shellRect.anchorMax = new Vector2(0.5f, 0.5f);
            shellRect.pivot = new Vector2(0.5f, 0.5f);
            shellRect.anchoredPosition = Vector2.zero;
            shellRect.sizeDelta = new Vector2(640f, 440f);

            return new Setup
            {
                Root = canvasObject,
                EventSystemObject = eventSystemObject,
                EventSystem = eventSystemObject.GetComponent<EventSystem>(),
                Canvas = canvas,
                Shell = shellObject.GetComponent<InventoryShellView>(),
            };
        }

        private static void ForceLayout(Setup setup)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)setup.Shell.Panel.Grid.ContentRoot);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)setup.Shell.transform);
            Canvas.ForceUpdateCanvases();
        }

        private static RaycastResult Raycast(EventSystem eventSystem, InventorySlotView slot)
        {
            var position = RectTransformUtility.WorldToScreenPoint(null, slot.RectTransform.TransformPoint(slot.RectTransform.rect.center));
            var pointer = new PointerEventData(eventSystem) { position = position };
            var results = new List<RaycastResult>();
            eventSystem.RaycastAll(pointer, results);
            return results.FirstOrDefault(result => result.gameObject == slot.gameObject);
        }

        private static InventoryViewModel CreateModel(InventoryUiState state, params bool[] enabled)
        {
            var slots = new[] { "left", "center", "right" }.Select((id, index) =>
                new InventorySlotViewModel(
                    new SlotAddress(new ContainerId($"{id}-container"), index + 3),
                    new ItemDefinitionId(id),
                    index + 1,
                    state == InventoryUiState.Selected && index == 1,
                    index < enabled.Length && enabled[index]));
            return new InventoryViewModel(1, state, slots, state.ToString());
        }

        private static void Destroy(Setup setup)
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(setup.Root);
                UnityEngine.Object.Destroy(setup.EventSystemObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(setup.Root);
                UnityEngine.Object.DestroyImmediate(setup.EventSystemObject);
            }
        }

        private sealed class Setup
        {
            public GameObject Root;
            public GameObject EventSystemObject;
            public EventSystem EventSystem;
            public Canvas Canvas;
            public InventoryShellView Shell;
        }
    }
}
