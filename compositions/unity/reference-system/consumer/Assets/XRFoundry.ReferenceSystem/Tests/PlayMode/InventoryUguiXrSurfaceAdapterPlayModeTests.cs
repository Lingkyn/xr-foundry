using System.Collections;
using System.Linq;
using Lingkyn.Inventory.Core;
using Lingkyn.Inventory.Presentation;
using Lingkyn.Inventory.UGUI;
using Lingkyn.Inventory.XR.UGUI;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using XRFoundry.ReferenceSystem.Bindings;

namespace XRFoundry.ReferenceSystem.Tests
{
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor)]
    public sealed class InventoryUguiXrSurfaceAdapterPlayModeTests
    {
        private const string SurfacePrefabPath =
            "Packages/com.lingkyn.inventory.xr.ugui/Runtime/Prefabs/InventoryWorldSpaceSurface.prefab";

        [UnityTest]
        public IEnumerator ConcreteXrSurfaceOnlyReturnsTypedIntentThroughValidatedInteractionGate()
        {
            var surfaceObject = InstantiateSurface();
            GameObject[] interactionObjects = null;
            try
            {
                var itemId = new ItemDefinitionId("crystal");
                var bagId = new ContainerId("bag");
                var inventory = new InventoryAggregate(
                    new InventoryId("player"),
                    new ItemDefinitionCatalog(new[]
                    {
                        new ItemDefinition(itemId, 5, ItemInstanceMode.Fungible),
                    }),
                    new[] { new ContainerDefinition(bagId, 3) });
                var surface = surfaceObject.GetComponent<InventoryWorldSpaceSurface>();

                Assert.That(surface.CanvasGroup.interactable, Is.False);
                Assert.That(surface.CanvasGroup.blocksRaycasts, Is.False);
                Assert.That(surface.InteractionEnabled, Is.False);
                using var adapter = new InventoryUguiXrSurfaceAdapter(inventory, surface);

                Assert.That(surface.CanvasGroup.interactable, Is.False,
                    "Adapter construction must not open the scene-validation interaction gate.");
                Assert.That(surface.InteractionEnabled, Is.False);

                var added = inventory.Execute(MutationRequest.Add(new ItemStack(itemId, 3), bagId));
                Assert.That(added.Succeeded, Is.True, added.Message);
                yield return null;
                ForceLayout(surface);

                Assert.That(surface.Canvas.renderMode, Is.EqualTo(RenderMode.WorldSpace));
                Assert.That(surface.Shell.LastModel, Is.SameAs(adapter.Current));
                Assert.That(surface.Shell.Panel.Grid.ActiveSlotCount, Is.EqualTo(3));
                Assert.That(surface.Shell.Panel.Grid.SlotViews[0].ItemView.Label.text,
                    Does.Contain("crystal x3"));

                InventorySlotIntent? activation = null;
                InventorySlotIntent? selection = null;
                adapter.ActivationRequested += intent => activation = intent;
                adapter.SelectionRequested += intent => selection = intent;
                var revisionBeforeUiIntent = inventory.Revision;

                interactionObjects = CreateInteractionScene(surface, out var eventSystem);

                // Direct submit/navigation dispatch deliberately bypasses raycasting. Keeping
                // the complete surface gate closed must still prevent presenter selection and
                // consumer forwarding.
                surface.CanvasGroup.interactable = true;
                Assert.That(surface.CanvasGroup.blocksRaycasts, Is.False);
                Assert.That(surface.InteractionEnabled, Is.False);

                var slot = surface.Shell.Panel.Grid.SlotViews[0];
                ExecuteEvents.Execute(
                    slot.gameObject,
                    new BaseEventData(eventSystem),
                    ExecuteEvents.submitHandler);
                var secondSlot = surface.Shell.Panel.Grid.SlotViews[1];
                ExecuteEvents.Execute(
                    secondSlot.gameObject,
                    new BaseEventData(eventSystem),
                    ExecuteEvents.selectHandler);
                yield return null;

                Assert.That(activation.HasValue, Is.False);
                Assert.That(selection.HasValue, Is.False);
                Assert.That(adapter.Current.State, Is.EqualTo(InventoryUiState.Partial));
                Assert.That(adapter.Current.Slots.All(item => !item.Selected), Is.True);

                var validation = surface.Revalidate();
                Assert.That(validation.IsValid, Is.True,
                    string.Join(" | ", validation.Issues.Select(issue => $"{issue.Code}: {issue.Message}")));
                Assert.That(surface.InteractionEnabled, Is.True);

                ExecuteEvents.Execute(
                    slot.gameObject,
                    new BaseEventData(eventSystem),
                    ExecuteEvents.submitHandler);
                ExecuteEvents.Execute(
                    secondSlot.gameObject,
                    new BaseEventData(eventSystem),
                    ExecuteEvents.selectHandler);
                yield return null;

                Assert.That(activation.HasValue, Is.True);
                Assert.That(activation.Value.Address, Is.EqualTo(new SlotAddress(bagId, 0)));
                Assert.That(selection.HasValue, Is.True);
                Assert.That(selection.Value.Address, Is.EqualTo(new SlotAddress(bagId, 1)));
                Assert.That(adapter.Current.State, Is.EqualTo(InventoryUiState.Selected));
                Assert.That(adapter.Current.Slots[1].Selected, Is.True);
                Assert.That(inventory.Revision, Is.EqualTo(revisionBeforeUiIntent));
            }
            finally
            {
                DestroyImmediate(interactionObjects);
                Object.DestroyImmediate(surfaceObject);
            }
        }

        private static GameObject[] CreateInteractionScene(
            InventoryWorldSpaceSurface surface,
            out EventSystem eventSystem)
        {
            var cameraObject = new GameObject("XR Camera", typeof(Camera));
            cameraObject.transform.position = surface.transform.position - surface.transform.forward;
            surface.BindEventCamera(cameraObject.GetComponent<Camera>());

            var eventSystemObject = new GameObject("XR EventSystem", typeof(EventSystem));
            eventSystem = eventSystemObject.GetComponent<EventSystem>();
            var xrInputModule = eventSystemObject.AddComponent(ResolveType(
                "UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule"));
            SetProperty(xrInputModule, "enableXRInput", true);

            var managerObject = new GameObject(
                "XR Interaction Manager",
                ResolveType("UnityEngine.XR.Interaction.Toolkit.XRInteractionManager"));
            var rayObject = new GameObject("Synthetic XR UI Ray");
            rayObject.SetActive(false);
            var ray = rayObject.AddComponent(ResolveType(
                "UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor"));
            SetProperty(ray, "enableUIInteraction", true);
            SetProperty(ray, "maxRaycastDistance", 5f);
            SetProperty(ray, "raycastMask", (LayerMask)~0);
            rayObject.transform.position = surface.transform.position;
            rayObject.SetActive(true);
            InvokeMethod(xrInputModule, "RegisterInteractor", ray);

            Assert.That(surface.InteractionEnabled, Is.False,
                "Creating scene prerequisites must not bypass the surface revalidation gate.");
            return new[] { rayObject, managerObject, eventSystemObject, cameraObject };
        }

        private static System.Type ResolveType(string fullName)
        {
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }

            throw new AssertionException($"Required XRI type is unavailable: {fullName}");
        }

        private static void SetProperty(Component component, string propertyName, object value)
        {
            var property = component.GetType().GetProperty(propertyName);
            Assert.That(property, Is.Not.Null,
                $"{component.GetType().FullName} must expose {propertyName}.");
            property.SetValue(component, value);
        }

        private static void InvokeMethod(
            Component component,
            string methodName,
            Component argument)
        {
            var method = component.GetType().GetMethods().SingleOrDefault(candidate =>
                candidate.Name == methodName &&
                candidate.GetParameters().Length == 1 &&
                candidate.GetParameters()[0].ParameterType.IsInstanceOfType(argument));
            Assert.That(method, Is.Not.Null,
                $"{component.GetType().FullName} must expose {methodName}.");
            method.Invoke(component, new object[] { argument });
        }

        private static void DestroyImmediate(GameObject[] objects)
        {
            if (objects == null) return;
            foreach (var item in objects)
            {
                if (item != null) Object.DestroyImmediate(item);
            }
        }

        private static GameObject InstantiateSurface()
        {
#if UNITY_EDITOR
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SurfacePrefabPath);
            Assert.That(prefab, Is.Not.Null);
            return Object.Instantiate(prefab);
#else
            throw new System.NotSupportedException(
                "The reference-system surface test runs in an Editor consumer.");
#endif
        }

        private static void ForceLayout(InventoryWorldSpaceSurface surface)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(
                (RectTransform)surface.Shell.Panel.Grid.ContentRoot);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)surface.Shell.transform);
            Canvas.ForceUpdateCanvases();
        }
    }
}
