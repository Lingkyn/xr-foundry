using Lingkyn.Inventory.UIToolkit;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Lingkyn.Inventory.XR.UIToolkit.Samples.Editor
{
    public static class InventoryUIToolkitWorldSpaceSampleSetup
    {
        private const string ProfilePath =
            "Packages/com.lingkyn.inventory.xr.uitoolkit/Runtime/Profiles/InventoryUIToolkitWorldSpaceDefault.asset";
        private const string DocumentPath =
            "Packages/com.lingkyn.inventory.uitoolkit/Runtime/UI/InventoryDocument.uxml";

        [MenuItem("GameObject/XR Foundry/Inventory/Create World-Space UI Toolkit Sample", false, 11)]
        public static void Create()
        {
            var profile = AssetDatabase.LoadAssetAtPath<InventoryUIToolkitWorldSpaceProfile>(ProfilePath);
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(DocumentPath);
            if (profile == null || tree == null)
            {
                throw new System.InvalidOperationException(
                    "Could not load the Inventory XR UI Toolkit profile or Inventory UI Toolkit document.");
            }

            EnsureXrUiToolkitManager();

            var root = new GameObject("Inventory UI Toolkit World Space");
            root.SetActive(false);
            Undo.RegisterCreatedObjectUndo(root, "Create Inventory UI Toolkit World Space");
            var document = root.AddComponent<UIDocument>();
            document.visualTreeAsset = tree;
            var view = root.AddComponent<InventoryDocumentView>();
            var collider = root.AddComponent<BoxCollider>();
            var surface = root.AddComponent<InventoryUIToolkitWorldSpaceSurface>();
            surface.Configure(profile, document, view, collider);
            surface.ApplyProfile();
            EnsurePanelInputConfigurationWhenNeeded(profile, root.layer);
            root.AddComponent<InventoryUIToolkitWorldSpaceSampleBootstrap>();
            root.SetActive(true);

            var sceneCamera = SceneView.lastActiveSceneView != null
                ? SceneView.lastActiveSceneView.camera
                : null;
            if (sceneCamera != null) surface.PlaceInFrontOf(sceneCamera);
            else root.transform.SetPositionAndRotation(new Vector3(0f, 1.35f, 1.25f), Quaternion.identity);

            surface.Revalidate();
            Selection.activeGameObject = root;
        }

        private static void EnsureXrUiToolkitManager()
        {
            if (Object.FindObjectsByType<XRUIToolkitManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length > 0)
            {
                return;
            }
            var manager = new GameObject("XR UI Toolkit Manager", typeof(XRUIToolkitManager));
            Undo.RegisterCreatedObjectUndo(manager, "Create XR UI Toolkit Manager");
        }

        private static void EnsurePanelInputConfigurationWhenNeeded(
            InventoryUIToolkitWorldSpaceProfile profile,
            int surfaceLayer)
        {
            if (Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0)
            {
                return;
            }
            var configurations = Object.FindObjectsByType<PanelInputConfiguration>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (configurations.Length > 1)
            {
                return;
            }

            PanelInputConfiguration configuration;
            if (configurations.Length == 1)
            {
                configuration = configurations[0];
                Undo.RecordObject(configuration, "Configure Inventory Panel Input");
            }
            else
            {
                var configurationObject = new GameObject(
                    "Inventory UI Toolkit Panel Input Configuration",
                    typeof(PanelInputConfiguration));
                Undo.RegisterCreatedObjectUndo(configurationObject, "Create Panel Input Configuration");
                configuration = configurationObject.GetComponent<PanelInputConfiguration>();
            }

            configuration.panelInputRedirection = PanelInputConfiguration.PanelInputRedirection.Never;
            configuration.processWorldSpaceInput = true;
            var interactionLayers = configuration.interactionLayers;
            interactionLayers.value |= 1 << surfaceLayer;
            configuration.interactionLayers = interactionLayers;
            configuration.maxInteractionDistance = profile.MaxInteractionDistanceMeters;
            EditorUtility.SetDirty(configuration);
        }
    }
}
