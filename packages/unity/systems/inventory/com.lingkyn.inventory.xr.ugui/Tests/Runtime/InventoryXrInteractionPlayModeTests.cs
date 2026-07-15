using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Lingkyn.Inventory.Presentation;
using Lingkyn.Inventory.UGUI;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Lingkyn.Inventory.XR.UGUI.Tests
{
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor)]
    public sealed class InventoryXrInteractionPlayModeTests
    {
        private const string PrefabPath =
            "Packages/com.lingkyn.inventory.xr.ugui/Runtime/Prefabs/InventoryWorldSpaceSurface.prefab";

        [UnityTest]
        public IEnumerator SurfaceFailsClosedRecoversAndDoesNotFollowCamera()
        {
            var surface = CreateSurface(out var surfaceObject);
            var cameraObject = new GameObject("Main Camera", typeof(Camera));
            GameObject eventSystemObject = null;
            GameObject managerObject = null;
            GameObject rayObject = null;
            GameObject secondObject = null;
            try
            {
                cameraObject.tag = "MainCamera";
                var camera = cameraObject.GetComponent<Camera>();
                camera.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                surface.Prepare(camera, true);
                var worldPosition = surface.transform.position;
                var worldRotation = surface.transform.rotation;

                Assert.That(surface.Revalidate().Has(InventoryXrIssueCode.MissingEventSystem), Is.True);
                Assert.That(surface.InteractionEnabled, Is.False);

                eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(XRUIInputModule));
                var xrModule = eventSystemObject.GetComponent<XRUIInputModule>();
                Assert.That(surface.Revalidate().Has(InventoryXrIssueCode.MissingUiInteractor), Is.True);

                managerObject = new GameObject("XR Interaction Manager", typeof(XRInteractionManager));
                rayObject = CreateControllerRay(camera.transform.position, camera.transform.forward);
                xrModule.RegisterInteractor(rayObject.GetComponent<XRRayInteractor>());
                yield return null;
                Assert.That(surface.Revalidate().IsValid, Is.True);
                Assert.That(surface.InteractionEnabled, Is.True);

                xrModule.enableXRInput = false;
                Assert.That(surface.Revalidate().Has(InventoryXrIssueCode.DisabledXrInput), Is.True);
                Assert.That(surface.InteractionEnabled, Is.False);
                xrModule.enableXRInput = true;
                Assert.That(surface.Revalidate().IsValid, Is.True);
                Assert.That(surface.InteractionEnabled, Is.True);

                var desktopModule = eventSystemObject.AddComponent<StandaloneInputModule>();
                Assert.That(surface.Revalidate().Has(InventoryXrIssueCode.MultipleInputModules), Is.True);
                Assert.That(surface.InteractionEnabled, Is.False);
                UnityEngine.Object.DestroyImmediate(desktopModule);
                Assert.That(surface.Revalidate().IsValid, Is.True);
                Assert.That(surface.InteractionEnabled, Is.True);

                var secondSurface = CreateSurface(out secondObject);
                secondSurface.Prepare(camera, false);
                Assert.That(secondSurface.Revalidate().IsValid, Is.True);

                camera.transform.SetPositionAndRotation(new Vector3(2f, 1f, -3f), Quaternion.Euler(10f, 80f, 0f));
                yield return null;
                Assert.That(surface.transform.position, Is.EqualTo(worldPosition));
                Assert.That(surface.transform.rotation, Is.EqualTo(worldRotation));
            }
            finally
            {
                if (rayObject != null && eventSystemObject != null)
                    eventSystemObject.GetComponent<XRUIInputModule>()
                        .UnregisterInteractor(rayObject.GetComponent<XRRayInteractor>());
                DestroyImmediate(secondObject, rayObject, managerObject, eventSystemObject, cameraObject, surfaceObject);
            }
        }

        [UnityTest]
        public IEnumerator RealXriRayInteractorRoutesToDistinctShippedSlots()
        {
            var surface = CreateSurface(out var surfaceObject);
            surface.transform.SetPositionAndRotation(new Vector3(0f, 0f, 1.25f), Quaternion.identity);
            var cameraObject = new GameObject("Main Camera", typeof(Camera));
            GameObject eventSystemObject = null;
            GameObject managerObject = null;
            GameObject rayObject = null;
            try
            {
                cameraObject.tag = "MainCamera";
                var camera = cameraObject.GetComponent<Camera>();
                camera.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                surface.BindEventCamera(camera);

                eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(XRUIInputModule));
                var module = eventSystemObject.GetComponent<XRUIInputModule>();
                managerObject = new GameObject("XR Interaction Manager", typeof(XRInteractionManager));
                rayObject = CreateControllerRay(camera.transform.position, camera.transform.forward);
                var interactor = rayObject.GetComponent<XRRayInteractor>();
                module.RegisterInteractor(interactor);
                yield return null;
                Assert.That(surface.Revalidate().IsValid, Is.True);

                RenderGallery(surface);
                yield return null;
                ForceLayout(surface);
                var grid = surface.Shell.Panel.Grid;
                var activations = new List<InventorySlotIntent>();
                grid.ActivationRequested += activations.Add;

                for (var index = 0; index < 3; index++)
                {
                    var slot = grid.SlotViews[index];
                    var target = slot.RectTransform.TransformPoint(slot.RectTransform.rect.center);
                    rayObject.transform.SetPositionAndRotation(
                        camera.transform.position,
                        Quaternion.LookRotation(target - camera.transform.position, Vector3.up));
                    interactor.uiPressInput.QueueManualState(false, 0f);
                    yield return null;
                    yield return null;
                    Assert.That(module.GetTrackedDeviceModel(interactor, out var hoveredModel), Is.True);
                    Assert.That(hoveredModel.currentRaycast.isValid, Is.True);
                    Assert.That(hoveredModel.currentRaycast.gameObject.GetComponentInParent<InventorySlotView>(),
                        Is.SameAs(slot));
                    interactor.uiPressInput.QueueManualState(true, 1f);
                    yield return null;
                    interactor.uiPressInput.QueueManualState(false, 0f);
                    yield return null;
                }

                var displayIndexes = activations.Select(item => item.DisplayIndex).ToArray();
                var addresses = activations.Select(item => item.Address).ToArray();
                Assert.That(displayIndexes, Is.EqualTo(new[] { 0, 1, 2 }));
                Assert.That(addresses,
                    Is.EqualTo(surface.Shell.LastModel.Slots.Select(slot => slot.Address).ToArray()));

                module.UnregisterInteractor(interactor);
                yield return null;
            }
            finally
            {
                DestroyImmediate(rayObject, managerObject, eventSystemObject, cameraObject, surfaceObject);
            }
        }

        [UnityTest]
        public IEnumerator RealXriPokeInteractorRoutesThroughShippedSlotOnRelease()
        {
            var surface = CreateSurface(out var surfaceObject);
            surface.transform.SetPositionAndRotation(new Vector3(0f, 0f, 1.25f), Quaternion.identity);
            var cameraObject = new GameObject("Main Camera", typeof(Camera));
            GameObject eventSystemObject = null;
            GameObject managerObject = null;
            GameObject pokeObject = null;
            try
            {
                cameraObject.tag = "MainCamera";
                var camera = cameraObject.GetComponent<Camera>();
                camera.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                surface.BindEventCamera(camera);

                eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(XRUIInputModule));
                var module = eventSystemObject.GetComponent<XRUIInputModule>();
                managerObject = new GameObject("XR Interaction Manager", typeof(XRInteractionManager));

                pokeObject = new GameObject("Poke Interactor");
                pokeObject.SetActive(false);
                var pokeInteractor = pokeObject.AddComponent<XRPokeInteractor>();
                const float pokeDepth = 0.05f;
                pokeInteractor.pokeDepth = pokeDepth;
                pokeInteractor.enableUIInteraction = true;
                pokeInteractor.clickUIOnDown = false;
                pokeObject.SetActive(true);
                module.RegisterInteractor(pokeInteractor);
                yield return null;
                Assert.That(surface.Revalidate().IsValid, Is.True);

                RenderGallery(surface);
                yield return null;
                ForceLayout(surface);
                var slot = surface.Shell.Panel.Grid.SlotViews[1];
                var target = slot.RectTransform.TransformPoint(slot.RectTransform.rect.center);
                var activations = new List<InventorySlotIntent>();
                surface.Shell.Panel.Grid.ActivationRequested += activations.Add;

                var approach = slot.RectTransform.forward;
                pokeObject.transform.SetPositionAndRotation(
                    target - approach * (pokeDepth + 0.01f),
                    Quaternion.LookRotation(approach, slot.RectTransform.up));
                yield return null;

                Assert.That(module.GetTrackedDeviceModel(pokeInteractor, out var initialModel), Is.True);
                Assert.That(initialModel.select, Is.False);
                Assert.That(activations, Is.Empty);

                pokeObject.transform.position = target - approach * (pokeDepth - 0.01f);
                yield return null;
                Assert.That(TrackedDeviceGraphicRaycaster.IsPokeInteractingWithUI(pokeInteractor), Is.True);

                const int velocityFrames = 5;
                var interval = pokeDepth / velocityFrames;
                for (var index = velocityFrames; index > 0; index--)
                {
                    pokeObject.transform.position = target - approach * (interval * index);
                    yield return null;
                }

                pokeObject.transform.position = target - approach * (pokeDepth * 0.02f);
                yield return null;
                yield return null;
                Assert.That(module.GetTrackedDeviceModel(pokeInteractor, out var selectedModel), Is.True);
                Assert.That(selectedModel.currentRaycast.isValid, Is.True);
                Assert.That(selectedModel.currentRaycast.gameObject.GetComponentInParent<InventorySlotView>(),
                    Is.SameAs(slot));
                Assert.That(selectedModel.select, Is.True);
                Assert.That(TrackedDeviceGraphicRaycaster.IsPokeSelectingWithUI(pokeInteractor), Is.True);

                pokeObject.transform.position = target - approach * (pokeDepth * 0.03f);
                yield return null;
                yield return null;
                Assert.That(activations.Count, Is.EqualTo(1));
                Assert.That(activations[0].DisplayIndex, Is.EqualTo(1));
                Assert.That(activations[0].Address, Is.EqualTo(surface.Shell.LastModel.Slots[1].Address));

                pokeObject.transform.position = target - approach * (pokeDepth + 0.1f);
                yield return null;
                Assert.That(TrackedDeviceGraphicRaycaster.IsPokeInteractingWithUI(pokeInteractor), Is.False);
                module.UnregisterInteractor(pokeInteractor);
                yield return null;
            }
            finally
            {
                DestroyImmediate(pokeObject, managerObject, eventSystemObject, cameraObject, surfaceObject);
            }
        }

        private static InventoryWorldSpaceSurface CreateSurface(out GameObject instance)
        {
#if UNITY_EDITOR
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            instance = UnityEngine.Object.Instantiate(prefab);
            var surface = instance.GetComponent<InventoryWorldSpaceSurface>();
            Assert.That(surface, Is.Not.Null);
            return surface;
#else
            throw new System.NotSupportedException("Inventory XR package prefab tests run in an Editor consumer.");
#endif
        }

        private static InventoryViewModel RenderGallery(InventoryWorldSpaceSurface surface)
        {
#if UNITY_EDITOR
            var gallery = surface.gameObject.AddComponent<InventoryStateGallery>();
            var serialized = new SerializedObject(gallery);
            serialized.FindProperty("shell").objectReferenceValue = surface.Shell;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            gallery.ReplayState(InventoryUiState.Partial);
            return gallery.LastModel;
#else
            throw new System.NotSupportedException("Inventory XR package gallery tests run in an Editor consumer.");
#endif
        }

        private static void ForceLayout(InventoryWorldSpaceSurface surface)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)surface.Shell.Panel.Grid.ContentRoot);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)surface.Shell.transform);
            Canvas.ForceUpdateCanvases();
        }

        private static GameObject CreateControllerRay(Vector3 origin, Vector3 direction)
        {
            var rayObject = new GameObject("Controller Ray");
            rayObject.SetActive(false);
            var ray = rayObject.AddComponent<XRRayInteractor>();
            ray.enableUIInteraction = true;
            ray.maxRaycastDistance = 5f;
            ray.raycastMask = ~0;
            ray.uiPressInput.inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue;
            rayObject.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(direction, Vector3.up));
            rayObject.SetActive(true);
            return rayObject;
        }

        private static void DestroyImmediate(params GameObject[] objects)
        {
            foreach (var item in objects)
            {
                if (item != null) UnityEngine.Object.DestroyImmediate(item);
            }
        }

    }
}
