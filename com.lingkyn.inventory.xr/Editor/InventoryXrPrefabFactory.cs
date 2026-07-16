using System;
using System.IO;
using Lingkyn.Inventory.UGUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Lingkyn.Inventory.XR.Editor
{
    public static class InventoryXrPrefabFactory
    {
        private const string RuntimeRoot = "Packages/com.lingkyn.inventory.xr/Runtime";
        private const string ProfilePath = RuntimeRoot + "/Profiles/InventoryWorldSpaceDefault.asset";
        private const string PrefabPath = RuntimeRoot + "/Prefabs/InventoryWorldSpaceSurface.prefab";
        private const string ShellPath = "Packages/com.lingkyn.inventory.ugui/Runtime/Prefabs/InventoryShell.prefab";

        // Maintainer-only batch entry. This intentionally has no MenuItem because
        // Git and registry package installs are immutable consumer surfaces.
        public static void Rebuild()
        {
            Directory.CreateDirectory(RuntimeRoot + "/Profiles");
            Directory.CreateDirectory(RuntimeRoot + "/Prefabs");

            var profile = AssetDatabase.LoadAssetAtPath<InventoryWorldSpaceProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<InventoryWorldSpaceProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            profile.Validate();
            var shellAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ShellPath);
            if (shellAsset == null) throw new InvalidOperationException($"Inventory UGUI shell is missing at {ShellPath}.");

            var root = new GameObject(
                "InventoryWorldSpaceSurface",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(CanvasGroup),
                typeof(TrackedDeviceGraphicRaycaster));
            try
            {
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                var scaler = root.GetComponent<CanvasScaler>();
                var group = root.GetComponent<CanvasGroup>();
                group.interactable = false;
                group.blocksRaycasts = false;
                var raycaster = root.GetComponent<TrackedDeviceGraphicRaycaster>();
                raycaster.enabled = false;

                var shellInstance = (GameObject)PrefabUtility.InstantiatePrefab(shellAsset);
                shellInstance.transform.SetParent(root.transform, false);
                Stretch((RectTransform)shellInstance.transform);
                var shell = shellInstance.GetComponent<InventoryShellView>();
                if (shell == null) throw new InvalidOperationException("InventoryShell prefab has no InventoryShellView.");

                var surface = root.AddComponent<InventoryWorldSpaceSurface>();
                SetObjectReference(surface, "canvas", canvas);
                SetObjectReference(surface, "canvasScaler", scaler);
                SetObjectReference(surface, "canvasGroup", group);
                SetObjectReference(surface, "trackedRaycaster", raycaster);
                SetObjectReference(surface, "shell", shell);
                SetObjectReference(surface, "profile", profile);

                root.transform.position = new Vector3(0f, 1.4f, 1.25f);
                root.transform.rotation = Quaternion.identity;
                surface.ApplyProfile();

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (prefab == null) throw new InvalidOperationException($"Could not save Inventory XR prefab at {PrefabPath}.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (value == null) throw new ArgumentNullException(nameof(value));
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null) throw new InvalidOperationException($"Missing serialized property {target.GetType().Name}.{propertyName}.");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
