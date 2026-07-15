using System;
using System.Linq;
using Lingkyn.Inventory.Presentation;
using Lingkyn.Inventory.UGUI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Lingkyn.Inventory.XR.UGUI.Tests
{
    public sealed class InventoryXrPrefabAndValidationTests
    {
        private const string PrefabPath =
            "Packages/com.lingkyn.inventory.xr.ugui/Runtime/Prefabs/InventoryWorldSpaceSurface.prefab";
        private const string ShellPath =
            "Packages/com.lingkyn.inventory.ugui/Runtime/Prefabs/InventoryShell.prefab";

        [Test]
        public void ShippedPrefabIsWorldSpaceNestedProviderNeutralAndClosedByDefault()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var shellAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ShellPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(shellAsset, Is.Not.Null);
            Assert.That(MissingScripts(prefab), Is.Zero);

            var surface = prefab.GetComponent<InventoryWorldSpaceSurface>();
            Assert.That(surface, Is.Not.Null);
            Assert.That(surface.Canvas, Is.Not.Null);
            Assert.That(surface.Canvas.renderMode, Is.EqualTo(RenderMode.WorldSpace));
            Assert.That(surface.CanvasScaler, Is.Not.Null);
            Assert.That(surface.CanvasGroup, Is.Not.Null);
            Assert.That(surface.TrackedRaycaster, Is.Not.Null);
            Assert.That(surface.Shell, Is.Not.Null);
            Assert.That(surface.Profile, Is.Not.Null);
            Assert.That(surface.Profile.PhysicalSizeMeters.x, Is.EqualTo(0.64f).Within(0.0001f));
            Assert.That(surface.Profile.PhysicalSizeMeters.y, Is.EqualTo(0.44f).Within(0.0001f));
            Assert.That(surface.CanvasGroup.interactable, Is.False);
            Assert.That(surface.CanvasGroup.blocksRaycasts, Is.False);
            Assert.That(surface.TrackedRaycaster.enabled, Is.False);
            Assert.That(surface.Canvas.GetComponent<GraphicRaycaster>(), Is.Null);
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(surface.Shell.gameObject), Is.EqualTo(shellAsset));
            Assert.That(prefab.GetComponentsInChildren<EventSystem>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Camera>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Component>(true).Any(component =>
                component != null && component.GetType().FullName == "Unity.XR.CoreUtils.XROrigin"), Is.False);
            Assert.That(prefab.GetComponentsInChildren<Component>(true).Any(component =>
                component != null && component.GetType().FullName.IndexOf("PICO", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
        }

        [Test]
        public void ApplyingProfilePreservesWorldPoseAndDefaultPlacementDetachesFromCamera()
        {
            var instance = UnityEngine.Object.Instantiate(LoadPrefab());
            GameObject cameraObject = null;
            GameObject scaledParent = null;
            try
            {
                var surface = instance.GetComponent<InventoryWorldSpaceSurface>();
                var position = new Vector3(4f, 2f, -3f);
                var rotation = Quaternion.Euler(0f, 37f, 0f);
                surface.transform.position = position;
                surface.transform.rotation = rotation;
                surface.ApplyProfile();
                Assert.That(surface.transform.position, Is.EqualTo(position));
                Assert.That(surface.transform.rotation, Is.EqualTo(rotation));
                Assert.That(surface.RectTransform.sizeDelta, Is.EqualTo(surface.Profile.ReferenceResolution));
                Assert.That(surface.transform.localScale, Is.EqualTo(Vector3.one * surface.Profile.MetersPerPixel));

                cameraObject = new GameObject("Camera", typeof(Camera));
                scaledParent = new GameObject("Scaled XR Parent");
                scaledParent.transform.localScale = Vector3.one * 4f;
                surface.transform.SetParent(scaledParent.transform, false);
                surface.PlaceInFrontOf(cameraObject.GetComponent<Camera>());
                Assert.That(surface.transform.parent, Is.Null);
                Assert.That(surface.GetComponentInParent<Camera>(), Is.Null);
                Assert.That(surface.transform.lossyScale.x,
                    Is.EqualTo(surface.Profile.MetersPerPixel).Within(0.000001f));
                Assert.That(surface.transform.lossyScale.y,
                    Is.EqualTo(surface.Profile.MetersPerPixel).Within(0.000001f));
                Assert.That(surface.transform.lossyScale.z,
                    Is.EqualTo(surface.Profile.MetersPerPixel).Within(0.000001f));

                cameraObject.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
                surface.PlaceInFrontOf(cameraObject.GetComponent<Camera>());
                Assert.That(Mathf.Abs(Vector3.Dot(surface.transform.forward, Vector3.up)),
                    Is.LessThan(0.0001f));
                Assert.That(float.IsNaN(surface.transform.rotation.x), Is.False);
                Assert.That(float.IsNaN(surface.transform.rotation.y), Is.False);
                Assert.That(float.IsNaN(surface.transform.rotation.z), Is.False);
                Assert.That(float.IsNaN(surface.transform.rotation.w), Is.False);
            }
            finally
            {
                DestroyImmediate(scaledParent, cameraObject, instance);
            }
        }

        [Test]
        public void ValidatorRejectsMissingDuplicateDisabledAndDesktopInputModules()
        {
            var instance = UnityEngine.Object.Instantiate(LoadPrefab());
            GameObject cameraObject = null;
            GameObject desktopObject = null;
            GameObject xrObject = null;
            GameObject rayObject = null;
            try
            {
                var surface = instance.GetComponent<InventoryWorldSpaceSurface>();
                cameraObject = new GameObject("Camera", typeof(Camera));
                var camera = cameraObject.GetComponent<Camera>();
                surface.BindEventCamera(camera);

                Assert.That(InventoryXrSceneValidator.ValidateScene(
                        surface,
                        Array.Empty<EventSystem>(),
                        Array.Empty<MonoBehaviour>())
                    .Has(InventoryXrIssueCode.MissingEventSystem), Is.True);

                desktopObject = new GameObject("Desktop EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                var desktop = desktopObject.GetComponent<EventSystem>();
                Assert.That(InventoryXrSceneValidator.ValidateScene(
                        surface,
                        new[] { desktop },
                        Array.Empty<MonoBehaviour>())
                    .Has(InventoryXrIssueCode.IncompatibleInputModule), Is.True);

                xrObject = new GameObject("XR EventSystem", typeof(EventSystem), typeof(XRUIInputModule));
                var xrSystem = xrObject.GetComponent<EventSystem>();
                var xrModule = xrObject.GetComponent<XRUIInputModule>();
                Assert.That(InventoryXrSceneValidator.ValidateScene(
                        surface,
                        new[] { xrSystem },
                        Array.Empty<MonoBehaviour>())
                    .Has(InventoryXrIssueCode.MissingUiInteractor), Is.True);

                rayObject = new GameObject("Controller Ray");
                var ray = rayObject.AddComponent<XRRayInteractor>();
                ray.enableUIInteraction = true;
                ray.maxRaycastDistance = 5f;
                ray.raycastMask = ~0;
                Assert.That(InventoryXrSceneValidator.ValidateScene(
                    surface,
                    new[] { xrSystem },
                    new MonoBehaviour[] { ray }).IsValid, Is.True);

                ray.enableUIInteraction = false;
                Assert.That(InventoryXrSceneValidator.ValidateScene(
                        surface,
                        new[] { xrSystem },
                        new MonoBehaviour[] { ray })
                    .Has(InventoryXrIssueCode.UiInteractionDisabled), Is.True);
                ray.enableUIInteraction = true;

                xrSystem.enabled = false;
                Assert.That(InventoryXrSceneValidator.ValidateScene(surface, new[] { xrSystem }, new MonoBehaviour[] { ray })
                    .Has(InventoryXrIssueCode.DisabledEventSystem), Is.True);
                xrSystem.enabled = true;

                xrModule.enabled = false;
                Assert.That(InventoryXrSceneValidator.ValidateScene(surface, new[] { xrSystem }, new MonoBehaviour[] { ray })
                    .Has(InventoryXrIssueCode.DisabledInputModule), Is.True);
                xrModule.enabled = true;

                xrModule.enableXRInput = false;
                Assert.That(InventoryXrSceneValidator.ValidateScene(surface, new[] { xrSystem }, new MonoBehaviour[] { ray })
                    .Has(InventoryXrIssueCode.DisabledXrInput), Is.True);
                xrModule.enableXRInput = true;

                Assert.That(InventoryXrSceneValidator.ValidateScene(surface, new[] { xrSystem, desktop }, new MonoBehaviour[] { ray })
                    .Has(InventoryXrIssueCode.MultipleEventSystems), Is.True);

                surface.transform.SetParent(cameraObject.transform, true);
                Assert.That(InventoryXrSceneValidator.ValidateScene(surface, new[] { xrSystem }, new MonoBehaviour[] { ray })
                    .Has(InventoryXrIssueCode.HeadLockedSurface), Is.True);
            }
            finally
            {
                DestroyImmediate(rayObject, xrObject, desktopObject, cameraObject, instance);
            }
        }

        [Test]
        public void ValidatorRejectsConventionalGraphicRaycasterWithoutMutatingGlobalInput()
        {
            var instance = UnityEngine.Object.Instantiate(LoadPrefab());
            try
            {
                var surface = instance.GetComponent<InventoryWorldSpaceSurface>();
                var conventional = surface.gameObject.AddComponent<GraphicRaycaster>();
                var report = InventoryXrSceneValidator.ValidateSurface(surface);
                Assert.That(report.Has(InventoryXrIssueCode.ConventionalGraphicRaycasterPresent), Is.True);
                Assert.That(conventional, Is.Not.Null);
                Assert.That(surface.TrackedRaycaster, Is.Not.Null);
            }
            finally
            {
                DestroyImmediate(instance);
            }
        }

        private static GameObject LoadPrefab() => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

        private static int MissingScripts(GameObject root) => root.GetComponentsInChildren<Transform>(true)
            .Sum(transform => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject));

        private static void DestroyImmediate(params GameObject[] objects)
        {
            foreach (var item in objects)
            {
                if (item != null) UnityEngine.Object.DestroyImmediate(item);
            }
        }
    }
}
