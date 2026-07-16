using System.Collections;
using System.Linq;
using Lingkyn.Inventory.UIToolkit;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Lingkyn.Inventory.XR.UIToolkit.Tests
{
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor)]
    public sealed class InventoryUIToolkitPlacementPlayModeTests
    {
        [UnityTest]
        public IEnumerator RealEnabledXrRayInteractorOpensGateAndSurfaceDoesNotFollowPlacementCamera()
        {
#if UNITY_EDITOR
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.lingkyn.inventory.uitoolkit/Runtime/UI/InventoryDocument.uxml");
            var profile = AssetDatabase.LoadAssetAtPath<InventoryUIToolkitWorldSpaceProfile>(
                "Packages/com.lingkyn.inventory.xr.uitoolkit/Runtime/Profiles/InventoryUIToolkitWorldSpaceDefault.asset");
            var surfaceObject = new GameObject("Inventory UI Toolkit World Space");
            surfaceObject.SetActive(false);
            var document = surfaceObject.AddComponent<UIDocument>();
            document.visualTreeAsset = tree;
            var view = surfaceObject.AddComponent<InventoryDocumentView>();
            var collider = surfaceObject.AddComponent<BoxCollider>();
            var surface = surfaceObject.AddComponent<InventoryUIToolkitWorldSpaceSurface>();
            surface.Configure(profile, document, view, collider);
            surface.ApplyProfile();

            var managerObject = new GameObject("XR UI Toolkit Manager", typeof(XRUIToolkitManager));
            var interactionManagerObject = new GameObject("XR Interaction Manager", typeof(XRInteractionManager));
            var rayObject = new GameObject("XR Ray Interactor");
            rayObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var ray = rayObject.AddComponent<XRRayInteractor>();
            ray.enableUIInteraction = false;
            var cameraObject = new GameObject("Placement Camera", typeof(Camera));
            GameObject panelInputObject = null;
            try
            {
                cameraObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                surfaceObject.SetActive(true);
                surface.PlaceInFrontOf(cameraObject.GetComponent<Camera>());
                Assert.That(surface.transform.parent, Is.Null);
                Assert.That(Vector3.Dot(
                        surface.FacingNormal,
                        (cameraObject.transform.position - surface.transform.position).normalized),
                    Is.GreaterThan(0.999f), "The UIDocument front must face the placement camera.");
                Assert.That(surface.Revalidate().Has(InventoryUIToolkitIssueCode.UiInteractionDisabled), Is.True);
                Assert.That(surface.InteractionEnabled, Is.False);

                ray.enableUIInteraction = true;
                yield return null;
                if (Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length > 0)
                {
                    panelInputObject = new GameObject(
                        "Panel Input Configuration",
                        typeof(PanelInputConfiguration));
                    var panelInput = panelInputObject.GetComponent<PanelInputConfiguration>();
                    panelInput.panelInputRedirection = PanelInputConfiguration.PanelInputRedirection.Never;
                    panelInput.processWorldSpaceInput = true;
                    panelInput.interactionLayers = 1 << surface.gameObject.layer;
                    panelInput.maxInteractionDistance = surface.Profile.MaxInteractionDistanceMeters;
                }
                var validReport = surface.Revalidate();
                Assert.That(
                    validReport.IsValid,
                    Is.True,
                    "The gate requires a real active XRRayInteractor with UI interaction enabled. " +
                    string.Join(" | ", validReport.Issues.Select(issue => $"{issue.Code}: {issue.Message}")));
                Assert.That(surface.InteractionEnabled, Is.True);

                var position = surface.transform.position;
                var rotation = surface.transform.rotation;
                cameraObject.transform.SetPositionAndRotation(new Vector3(4f, 2f, -3f), Quaternion.Euler(0f, 120f, 0f));
                yield return null;
                Assert.That(surface.transform.position, Is.EqualTo(position));
                Assert.That(surface.transform.rotation, Is.EqualTo(rotation));
                Assert.That(surface.GetComponentsInChildren<Camera>(true), Is.Empty);
                Assert.That(surface.GetComponentsInChildren<XRUIToolkitManager>(true), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(panelInputObject);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(rayObject);
                Object.DestroyImmediate(interactionManagerObject);
                Object.DestroyImmediate(managerObject);
                Object.DestroyImmediate(surfaceObject);
            }
#else
            yield break;
#endif
        }
    }
}
