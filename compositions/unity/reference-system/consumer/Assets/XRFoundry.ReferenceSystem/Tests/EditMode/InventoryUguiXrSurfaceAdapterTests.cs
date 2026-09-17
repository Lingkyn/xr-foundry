using System.Linq;
using Lingkyn.Inventory.Core;
using Lingkyn.Inventory.Presentation;
using Lingkyn.Inventory.UGUI;
using Lingkyn.Inventory.XR.UGUI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using XRFoundry.ReferenceSystem.Bindings;

namespace XRFoundry.ReferenceSystem.Tests
{
    public sealed class InventoryUguiXrSurfaceAdapterTests
    {
        private const string SurfacePrefabPath =
            "Packages/com.lingkyn.inventory.xr.ugui/Runtime/Prefabs/InventoryWorldSpaceSurface.prefab";

        private static readonly InventoryId InventoryId = new InventoryId("player");
        private static readonly ContainerId BagId = new ContainerId("bag");
        private static readonly ItemDefinitionId OrbId = new ItemDefinitionId("orb");

        [Test]
        public void PresenterRendersAggregateAndOnlyForwardsStableIntentThroughValidatedInteractionGate()
        {
            var surfaceObject = Object.Instantiate(LoadSurfacePrefab());
            GameObject[] interactionObjects = null;
            try
            {
                var surface = surfaceObject.GetComponent<InventoryWorldSpaceSurface>();
                var inventory = CreateInventory();

                Assert.That(surface.CanvasGroup.interactable, Is.False);
                Assert.That(surface.CanvasGroup.blocksRaycasts, Is.False);
                Assert.That(surface.InteractionEnabled, Is.False);
                using var adapter = new InventoryUguiXrSurfaceAdapter(inventory, surface);

                Assert.That(adapter.Surface, Is.SameAs(surface));
                Assert.That(adapter.Shell, Is.SameAs(surface.Shell));
                Assert.That(surface.CanvasGroup.interactable, Is.False,
                    "Adapter construction must not open the scene-validation interaction gate.");
                Assert.That(surface.InteractionEnabled, Is.False);
                Assert.That(adapter.Current.State, Is.EqualTo(InventoryUiState.Empty));
                Assert.That(surface.Shell.LastModel, Is.SameAs(adapter.Current));
                Assert.That(surface.Shell.Panel.Grid.ActiveSlotCount, Is.EqualTo(2));

                var added = inventory.Execute(MutationRequest.Add(new ItemStack(OrbId, 2), BagId));
                Assert.That(added.Succeeded, Is.True, added.Message);
                Assert.That(adapter.Current.State, Is.EqualTo(InventoryUiState.Partial));
                Assert.That(surface.Shell.Panel.Grid.SlotViews[0].ItemView.Label.text,
                    Does.Contain("orb x2"));

                InventorySlotIntent? activation = null;
                InventorySlotIntent? selection = null;
                adapter.ActivationRequested += intent => activation = intent;
                adapter.SelectionRequested += intent => selection = intent;
                var revisionBeforeUiIntent = inventory.Revision;

                interactionObjects = CreateInteractionScene(surface, out var eventSystem);

                // Exercise a synthetic raycast bypass: the slot remains selectable while the
                // complete surface gate is closed. The adapter must still fail closed.
                surface.CanvasGroup.interactable = true;
                Assert.That(surface.CanvasGroup.blocksRaycasts, Is.False);
                Assert.That(surface.InteractionEnabled, Is.False);

                var firstSlot = surface.Shell.Panel.Grid.SlotViews[0];
                ExecuteEvents.Execute(
                    firstSlot.gameObject,
                    new PointerEventData(eventSystem)
                    {
                        button = PointerEventData.InputButton.Left,
                    },
                    ExecuteEvents.pointerClickHandler);

                var secondSlot = surface.Shell.Panel.Grid.SlotViews[1];
                ExecuteEvents.Execute(
                    secondSlot.gameObject,
                    new BaseEventData(eventSystem),
                    ExecuteEvents.selectHandler);

                Assert.That(activation.HasValue, Is.False);
                Assert.That(selection.HasValue, Is.False);
                Assert.That(adapter.Current.State, Is.EqualTo(InventoryUiState.Partial));
                Assert.That(adapter.Current.Slots.All(slot => !slot.Selected), Is.True);

                var validation = surface.Revalidate();
                Assert.That(validation.IsValid, Is.True,
                    string.Join(" | ", validation.Issues.Select(issue => $"{issue.Code}: {issue.Message}")));
                Assert.That(surface.InteractionEnabled, Is.True);

                ExecuteEvents.Execute(
                    firstSlot.gameObject,
                    new PointerEventData(eventSystem)
                    {
                        button = PointerEventData.InputButton.Left,
                    },
                    ExecuteEvents.pointerClickHandler);

                Assert.That(activation.HasValue, Is.True);
                Assert.That(activation.Value.Address, Is.EqualTo(new SlotAddress(BagId, 0)));
                Assert.That(activation.Value.DisplayIndex, Is.Zero);
                Assert.That(adapter.Current.State, Is.EqualTo(InventoryUiState.Selected));
                Assert.That(adapter.Current.Slots[0].Selected, Is.True);

                ExecuteEvents.Execute(
                    secondSlot.gameObject,
                    new BaseEventData(eventSystem),
                    ExecuteEvents.selectHandler);
                Assert.That(selection.HasValue, Is.True);
                Assert.That(selection.Value.Address, Is.EqualTo(new SlotAddress(BagId, 1)));
                Assert.That(selection.Value.DisplayIndex, Is.EqualTo(1));
                Assert.That(adapter.Current.Slots[1].Selected, Is.True);
                Assert.That(inventory.Revision, Is.EqualTo(revisionBeforeUiIntent),
                    "The surface adapter must not turn a presentation intent into a domain mutation.");
            }
            finally
            {
                DestroyImmediate(interactionObjects);
                Object.DestroyImmediate(surfaceObject);
            }
        }

        [Test]
        public void MissingSerializedSurfaceBindingsFailClosedBeforePresenterSubscription()
        {
            var invalidSurfaceObject = new GameObject("Invalid Inventory Surface");
            try
            {
                var invalidSurface = invalidSurfaceObject.AddComponent<InventoryWorldSpaceSurface>();
                var inventory = CreateInventory();

                Assert.That(
                    () => new InventoryUguiXrSurfaceAdapter(inventory, invalidSurface),
                    Throws.InvalidOperationException
                        .With.Message.Contains(nameof(InventoryXrIssueCode.MissingProfile))
                        .And.Message.Contains(nameof(InventoryXrIssueCode.MissingInventoryShell)));
            }
            finally
            {
                Object.DestroyImmediate(invalidSurfaceObject);
            }
        }

        [Test]
        public void InvalidTemplateParentFailsBeforePresenterSubscriptionWithoutObserverLeak()
        {
            var surfaceObject = Object.Instantiate(LoadSurfacePrefab());
            try
            {
                var surface = surfaceObject.GetComponent<InventoryWorldSpaceSurface>();
                var grid = surface.Shell.Panel.Grid;
                var inventory = CreateInventory();
                System.Exception observerFault = null;
                inventory.ObserverFaulted += exception => observerFault = exception;

                grid.SlotTemplate.transform.SetParent(surface.transform, false);
                Assert.That(grid.SlotTemplate.transform.IsChildOf(grid.ContentRoot), Is.False);

                Assert.That(
                    () => new InventoryUguiXrSurfaceAdapter(inventory, surface),
                    Throws.InvalidOperationException.With.Message.Contains(
                        "slot template to be a child of its content root"));

                MutationResult added = null;
                Assert.DoesNotThrow(() =>
                    added = inventory.Execute(MutationRequest.Add(new ItemStack(OrbId, 1), BagId)));
                Assert.That(added, Is.Not.Null);
                Assert.That(added.Succeeded, Is.True, added.Message);
                Assert.That(observerFault, Is.Null,
                    "A failed adapter construction must not leave an unreachable presenter subscribed to the aggregate.");
            }
            finally
            {
                Object.DestroyImmediate(surfaceObject);
            }
        }

        [Test]
        public void ShellBindingOutsideSurfaceHierarchyFailsWithShellOwnershipError()
        {
            var surfaceObject = Object.Instantiate(LoadSurfacePrefab());
            var externalSurfaceObject = Object.Instantiate(LoadSurfacePrefab());
            InventoryUguiXrSurfaceAdapter adapter = null;
            try
            {
                var surface = surfaceObject.GetComponent<InventoryWorldSpaceSurface>();
                var externalSurface = externalSurfaceObject.GetComponent<InventoryWorldSpaceSurface>();
                var serializedSurface = new SerializedObject(surface);
                serializedSurface.FindProperty("shell").objectReferenceValue = externalSurface.Shell;
                serializedSurface.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(surface.Shell, Is.SameAs(externalSurface.Shell));
                Assert.That(surface.Shell.transform.IsChildOf(surface.transform), Is.False);
                Assert.That(
                    InventoryXrSceneValidator.ValidateSurface(surface).Has(
                        InventoryXrIssueCode.InventoryShellOutsideSurface),
                    Is.True);

                Assert.That(
                    () => adapter = new InventoryUguiXrSurfaceAdapter(CreateInventory(), surface),
                    Throws.InvalidOperationException.With.Message.Contains(
                        nameof(InventoryXrIssueCode.InventoryShellOutsideSurface)));
            }
            finally
            {
                adapter?.Dispose();
                Object.DestroyImmediate(externalSurfaceObject);
                Object.DestroyImmediate(surfaceObject);
            }
        }

        [Test]
        public void PanelBindingOutsideSurfaceHierarchyFailsWithPanelOwnershipError()
        {
            var surfaceObject = Object.Instantiate(LoadSurfacePrefab());
            var externalSurfaceObject = Object.Instantiate(LoadSurfacePrefab());
            InventoryUguiXrSurfaceAdapter adapter = null;
            try
            {
                var surface = surfaceObject.GetComponent<InventoryWorldSpaceSurface>();
                var externalSurface = externalSurfaceObject.GetComponent<InventoryWorldSpaceSurface>();
                var serializedShell = new SerializedObject(surface.Shell);
                serializedShell.FindProperty("panel").objectReferenceValue = externalSurface.Shell.Panel;
                serializedShell.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(InventoryXrSceneValidator.ValidateSurface(surface).IsValid, Is.True,
                    "The surface-only preflight intentionally proves why the consumer must validate nested ownership.");
                Assert.That(surface.Shell.Panel, Is.SameAs(externalSurface.Shell.Panel));
                Assert.That(surface.Shell.Panel.transform.IsChildOf(surface.transform), Is.False);

                Assert.That(
                    () => adapter = new InventoryUguiXrSurfaceAdapter(CreateInventory(), surface),
                    Throws.InvalidOperationException.With.Message.Contains(
                        "panel binding to remain inside the surface hierarchy"));
            }
            finally
            {
                adapter?.Dispose();
                Object.DestroyImmediate(externalSurfaceObject);
                Object.DestroyImmediate(surfaceObject);
            }
        }

        [Test]
        public void GridBindingOutsideSurfaceHierarchyFailsWithGridOwnershipError()
        {
            var surfaceObject = Object.Instantiate(LoadSurfacePrefab());
            var externalSurfaceObject = Object.Instantiate(LoadSurfacePrefab());
            InventoryUguiXrSurfaceAdapter adapter = null;
            try
            {
                var surface = surfaceObject.GetComponent<InventoryWorldSpaceSurface>();
                var externalSurface = externalSurfaceObject.GetComponent<InventoryWorldSpaceSurface>();
                var serializedPanel = new SerializedObject(surface.Shell.Panel);
                serializedPanel.FindProperty("grid").objectReferenceValue = externalSurface.Shell.Panel.Grid;
                serializedPanel.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(InventoryXrSceneValidator.ValidateSurface(surface).IsValid, Is.True);
                Assert.That(surface.Shell.Panel.Grid, Is.SameAs(externalSurface.Shell.Panel.Grid));
                Assert.That(surface.Shell.Panel.Grid.transform.IsChildOf(surface.transform), Is.False);

                Assert.That(
                    () => adapter = new InventoryUguiXrSurfaceAdapter(CreateInventory(), surface),
                    Throws.InvalidOperationException.With.Message.Contains(
                        "grid binding to remain inside the surface hierarchy"));
            }
            finally
            {
                adapter?.Dispose();
                Object.DestroyImmediate(externalSurfaceObject);
                Object.DestroyImmediate(surfaceObject);
            }
        }

        [Test]
        public void ContentRootBindingOutsideSurfaceHierarchyFailsWithContentRootOwnershipError()
        {
            var surfaceObject = Object.Instantiate(LoadSurfacePrefab());
            var externalSurfaceObject = Object.Instantiate(LoadSurfacePrefab());
            InventoryUguiXrSurfaceAdapter adapter = null;
            try
            {
                var surface = surfaceObject.GetComponent<InventoryWorldSpaceSurface>();
                var externalSurface = externalSurfaceObject.GetComponent<InventoryWorldSpaceSurface>();
                var serializedGrid = new SerializedObject(surface.Shell.Panel.Grid);
                serializedGrid.FindProperty("contentRoot").objectReferenceValue =
                    externalSurface.Shell.Panel.Grid.ContentRoot;
                serializedGrid.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(InventoryXrSceneValidator.ValidateSurface(surface).IsValid, Is.True);
                Assert.That(
                    surface.Shell.Panel.Grid.ContentRoot,
                    Is.SameAs(externalSurface.Shell.Panel.Grid.ContentRoot));
                Assert.That(surface.Shell.Panel.Grid.ContentRoot.IsChildOf(surface.transform), Is.False);

                Assert.That(
                    () => adapter = new InventoryUguiXrSurfaceAdapter(CreateInventory(), surface),
                    Throws.InvalidOperationException.With.Message.Contains(
                        "content root binding to remain inside the surface hierarchy"));
            }
            finally
            {
                adapter?.Dispose();
                Object.DestroyImmediate(externalSurfaceObject);
                Object.DestroyImmediate(surfaceObject);
            }
        }

        [Test]
        public void SlotTemplateBindingOutsideSurfaceHierarchyFailsWithSlotTemplateOwnershipError()
        {
            var surfaceObject = Object.Instantiate(LoadSurfacePrefab());
            var externalSurfaceObject = Object.Instantiate(LoadSurfacePrefab());
            InventoryUguiXrSurfaceAdapter adapter = null;
            try
            {
                var surface = surfaceObject.GetComponent<InventoryWorldSpaceSurface>();
                var externalSurface = externalSurfaceObject.GetComponent<InventoryWorldSpaceSurface>();
                var serializedGrid = new SerializedObject(surface.Shell.Panel.Grid);
                serializedGrid.FindProperty("slotTemplate").objectReferenceValue =
                    externalSurface.Shell.Panel.Grid.SlotTemplate;
                serializedGrid.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(InventoryXrSceneValidator.ValidateSurface(surface).IsValid, Is.True);
                Assert.That(
                    surface.Shell.Panel.Grid.SlotTemplate,
                    Is.SameAs(externalSurface.Shell.Panel.Grid.SlotTemplate));
                Assert.That(surface.Shell.Panel.Grid.SlotTemplate.transform.IsChildOf(surface.transform), Is.False);

                Assert.That(
                    () => adapter = new InventoryUguiXrSurfaceAdapter(CreateInventory(), surface),
                    Throws.InvalidOperationException.With.Message.Contains(
                        "slot template binding to remain inside the surface hierarchy"));
            }
            finally
            {
                adapter?.Dispose();
                Object.DestroyImmediate(externalSurfaceObject);
                Object.DestroyImmediate(surfaceObject);
            }
        }

        [Test]
        public void DisposeDetachesAggregateAndSurfaceSubscriptions()
        {
            var surfaceObject = Object.Instantiate(LoadSurfacePrefab());
            GameObject[] interactionObjects = null;
            try
            {
                var inventory = CreateInventory();
                var surface = surfaceObject.GetComponent<InventoryWorldSpaceSurface>();
                var adapter = new InventoryUguiXrSurfaceAdapter(inventory, surface);
                var activations = 0;
                var selections = 0;
                var gridActivations = 0;
                var gridSelections = 0;
                adapter.ActivationRequested += _ => activations++;
                adapter.SelectionRequested += _ => selections++;
                surface.Shell.Panel.Grid.ActivationRequested += _ => gridActivations++;
                surface.Shell.Panel.Grid.SelectionRequested += _ => gridSelections++;

                interactionObjects = CreateInteractionScene(surface, out var eventSystem);
                var validation = surface.Revalidate();
                Assert.That(validation.IsValid, Is.True,
                    string.Join(" | ", validation.Issues.Select(issue => $"{issue.Code}: {issue.Message}")));
                Assert.That(surface.InteractionEnabled, Is.True);

                var firstSlot = surface.Shell.Panel.Grid.SlotViews[0];
                var secondSlot = surface.Shell.Panel.Grid.SlotViews[1];
                ExecuteEvents.Execute(
                    firstSlot.gameObject,
                    new PointerEventData(eventSystem)
                    {
                        button = PointerEventData.InputButton.Left,
                    },
                    ExecuteEvents.pointerClickHandler);
                ExecuteEvents.Execute(
                    secondSlot.gameObject,
                    new BaseEventData(eventSystem),
                    ExecuteEvents.selectHandler);
                Assert.That(activations, Is.EqualTo(1),
                    "The pre-dispose activation subscription must be proven reachable.");
                Assert.That(selections, Is.EqualTo(1),
                    "The pre-dispose selection subscription must be proven reachable.");
                Assert.That(gridActivations, Is.EqualTo(1));
                Assert.That(gridSelections, Is.EqualTo(1));

                var renderedRevision = surface.Shell.LastModel.Revision;

                adapter.Dispose();
                Assert.That(adapter.IsDisposed, Is.True);
                Assert.DoesNotThrow(() => adapter.Dispose(), "Dispose must be idempotent.");
                Assert.That(() => adapter.Refresh(), Throws.TypeOf<System.ObjectDisposedException>());
                Assert.That(surface.InteractionEnabled, Is.True,
                    "Dispose must detach the adapter without closing the independently-owned surface gate.");

                var added = inventory.Execute(MutationRequest.Add(new ItemStack(OrbId, 1), BagId));
                Assert.That(added.Succeeded, Is.True, added.Message);
                Assert.That(surface.Shell.LastModel.Revision, Is.EqualTo(renderedRevision));

                ExecuteEvents.Execute(
                    firstSlot.gameObject,
                    new PointerEventData(eventSystem)
                    {
                        button = PointerEventData.InputButton.Left,
                    },
                    ExecuteEvents.pointerClickHandler);
                ExecuteEvents.Execute(
                    secondSlot.gameObject,
                    new BaseEventData(eventSystem),
                    ExecuteEvents.selectHandler);
                Assert.That(activations, Is.EqualTo(1));
                Assert.That(selections, Is.EqualTo(1));
                Assert.That(gridActivations, Is.EqualTo(2),
                    "The source event must remain reachable after adapter disposal.");
                Assert.That(gridSelections, Is.EqualTo(2),
                    "The source event must remain reachable after adapter disposal.");
                Assert.That(surface.InteractionEnabled, Is.True);
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

        private static GameObject LoadSurfacePrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SurfacePrefabPath);
            Assert.That(prefab, Is.Not.Null);
            return prefab;
        }

        private static InventoryAggregate CreateInventory() => new InventoryAggregate(
            InventoryId,
            new ItemDefinitionCatalog(new[]
            {
                new ItemDefinition(OrbId, 5, ItemInstanceMode.Fungible),
            }),
            new[] { new ContainerDefinition(BagId, 2) });
    }
}
