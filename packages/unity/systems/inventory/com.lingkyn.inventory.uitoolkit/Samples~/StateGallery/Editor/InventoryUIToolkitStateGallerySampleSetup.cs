using Lingkyn.Inventory.UIToolkit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lingkyn.Inventory.UIToolkit.Samples.Editor
{
    public static class InventoryUIToolkitStateGallerySampleSetup
    {
        private const string AssetFolder = "Assets/InventoryUIToolkitStateGallery";
        private const string PanelPath = AssetFolder + "/InventoryPanelSettings.asset";
        private const string DocumentPath =
            "Packages/com.lingkyn.inventory.uitoolkit/Runtime/UI/InventoryDocument.uxml";

        [MenuItem("GameObject/XR Foundry/Inventory/Create UI Toolkit State Gallery", false, 10)]
        public static void Create()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(DocumentPath);
            if (tree == null)
            {
                throw new System.InvalidOperationException($"Could not load Inventory UI Toolkit document at {DocumentPath}.");
            }

            EnsureFolder();
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                panel.name = "InventoryPanelSettings";
                AssetDatabase.CreateAsset(panel, PanelPath);
            }
            panel.renderMode = PanelRenderMode.ScreenSpaceOverlay;
            EditorUtility.SetDirty(panel);

            var root = new GameObject("Inventory UI Toolkit State Gallery");
            Undo.RegisterCreatedObjectUndo(root, "Create Inventory UI Toolkit State Gallery");
            var document = root.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.visualTreeAsset = tree;
            root.AddComponent<InventoryDocumentView>();
            root.AddComponent<InventoryUIToolkitStateGalleryBootstrap>();
            Selection.activeGameObject = root;
            AssetDatabase.SaveAssets();
        }

        private static void EnsureFolder()
        {
            if (AssetDatabase.IsValidFolder(AssetFolder)) return;
            AssetDatabase.CreateFolder("Assets", "InventoryUIToolkitStateGallery");
        }
    }
}
