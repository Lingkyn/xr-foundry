using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Lingkyn.Inventory.XR.Tests
{
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor)]
    public sealed class InventoryXrInteractionPlayModeTests
    {
        private const string PrefabPath =
            "Packages/com.lingkyn.inventory.xr/Runtime/Prefabs/InventoryWorldSpaceSurface.prefab";

        [UnityTest]
        public IEnumerator SurfaceFailsClosedRecoversAndDoesNotFollowCamera()
        {
            var surface = CreateSurface(out var surfaceObject);
            var cameraObject = new GameObject("Main Camera", typeof(Camera));
            GameObject eventSystemObject = null;
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
                DestroyImmediate(secondObject, eventSystemObject, cameraObject, surfaceObject);
            }
        }

        [UnityTest]
        public IEnumerator RegisteredXriInteractorRayRoutesToDistinctShippedSlots()
        {
            var surface = CreateSurface(out var surfaceObject);
            surface.transform.SetPositionAndRotation(new Vector3(0f, 0f, 1.25f), Quaternion.identity);
            var cameraObject = new GameObject("Main Camera", typeof(Camera));
            GameObject eventSystemObject = null;
            try
            {
                cameraObject.tag = "MainCamera";
                var camera = cameraObject.GetComponent<Camera>();
                camera.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                surface.BindEventCamera(camera);

                eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(XRUIInputModule));
                var module = eventSystemObject.GetComponent<XRUIInputModule>();
                Assert.That(surface.Revalidate().IsValid, Is.True);

                RenderGallery(surface);
                yield return null;
                ForceLayout(surface);
                var grid = surface.Shell.Panel.Grid;
                var activations = new List<InventorySlotIntent>();
                grid.ActivationRequested += activations.Add;
                var interactor = new FakeUiInteractor();
                module.RegisterInteractor(interactor);
                yield return null;

                for (var index = 0; index < 3; index++)
                {
                    var slot = grid.SlotViews[index];
                    var target = slot.RectTransform.TransformPoint(slot.RectTransform.rect.center);
                    var direction = (target - camera.transform.position).normalized;
                    var endpoint = target + direction * 0.25f;
                    interactor.SetRay(camera.transform.position, endpoint, false);
                    yield return null;
                    Assert.That(module.GetTrackedDeviceModel(interactor, out var hoveredModel), Is.True);
                    Assert.That(hoveredModel.currentRaycast.isValid, Is.True);
                    Assert.That(hoveredModel.currentRaycast.gameObject.GetComponentInParent<InventorySlotView>(),
                        Is.SameAs(slot));
                    interactor.SetRay(camera.transform.position, endpoint, true);
                    yield return null;
                    interactor.SetRay(camera.transform.position, endpoint, false);
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
                DestroyImmediate(eventSystemObject, cameraObject, surfaceObject);
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
                Assert.That(surface.Revalidate().IsValid, Is.True);

                RenderGallery(surface);
                yield return null;
                ForceLayout(surface);
                var slot = surface.Shell.Panel.Grid.SlotViews[1];
                var target = slot.RectTransform.TransformPoint(slot.RectTransform.rect.center);
                var activations = new List<InventorySlotIntent>();
                surface.Shell.Panel.Grid.ActivationRequested += activations.Add;

                const float pokeDepth = 0.05f;
                var approach = slot.RectTransform.forward;
                pokeObject = new GameObject("Poke Interactor");
                pokeObject.SetActive(false);
                var pokeInteractor = pokeObject.AddComponent<XRPokeInteractor>();
                pokeInteractor.pokeDepth = pokeDepth;
                pokeInteractor.enableUIInteraction = true;
                pokeInteractor.clickUIOnDown = false;
                pokeObject.transform.SetPositionAndRotation(
                    target - approach * (pokeDepth + 0.01f),
                    Quaternion.LookRotation(approach, slot.RectTransform.up));
                pokeObject.SetActive(true);
                module.RegisterInteractor(pokeInteractor);
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

        private static void DestroyImmediate(params GameObject[] objects)
        {
            foreach (var item in objects)
            {
                if (item != null) UnityEngine.Object.DestroyImmediate(item);
            }
        }

        private sealed class FakeUiInteractor : IUIInteractor
        {
            private readonly List<Vector3> _points = new List<Vector3>();
            private TrackedDeviceModel _lastModel = TrackedDeviceModel.invalid;
            private bool _select;

            public void SetRay(Vector3 origin, Vector3 target, bool select)
            {
                _points.Clear();
                _points.Add(origin);
                _points.Add(target);
                _select = select;
            }

            public void UpdateUIModel(ref TrackedDeviceModel model)
            {
                model.position = _points.Count == 0 ? Vector3.zero : _points[0];
                model.orientation = _points.Count < 2
                    ? Quaternion.identity
                    : Quaternion.LookRotation(_points[1] - _points[0]);
                model.raycastPoints.Clear();
                model.raycastPoints.AddRange(_points);
                model.raycastLayerMask = ~0;
                model.interactionType = UIInteractionType.Ray;
                model.select = _select;
                _lastModel = model;
            }

            public bool TryGetUIModel(out TrackedDeviceModel model)
            {
                model = _lastModel;
                return _lastModel.pointerId >= 0;
            }
        }
    }
}
