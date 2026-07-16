using System;
using System.Linq;
using Lingkyn.Inventory.Presentation;
using Lingkyn.Inventory.UGUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Lingkyn.Inventory.XR.UGUI.Samples.Editor
{
    public static class InventoryWorldSpaceSampleSetup
    {
        private const string PrefabPath =
            "Packages/com.lingkyn.inventory.xr.ugui/Runtime/Prefabs/InventoryWorldSpaceSurface.prefab";

        [MenuItem("Tools/XR Foundry/Inventory/Create World-Space Inventory Sample")]
        public static void Create()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                throw new InvalidOperationException(
                    "World-Space Inventory sample setup requires an existing consumer Main Camera or XR camera.");
            }

            var eventSystems = UnityEngine.Object.FindObjectsByType<EventSystem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (eventSystems.Length > 1)
            {
                throw new InvalidOperationException(
                    $"World-Space Inventory sample refuses {eventSystems.Length} EventSystems; keep exactly one.");
            }

            GameObject createdEventSystem = null;
            GameObject instance = null;
            try
            {
                if (eventSystems.Length == 0)
                {
                    createdEventSystem = new GameObject(
                        "EventSystem",
                        typeof(EventSystem),
                        typeof(XRUIInputModule));
                    eventSystems = new[] { createdEventSystem.GetComponent<EventSystem>() };
                }
                else
                {
                    var modules = eventSystems[0].GetComponents<BaseInputModule>();
                    var xrModule = modules.Length == 1 ? modules[0] as XRUIInputModule : null;
                    if (!eventSystems[0].isActiveAndEnabled || modules.Length != 1 ||
                        xrModule == null || !xrModule.isActiveAndEnabled || !xrModule.enableXRInput)
                    {
                        var found = string.Join(", ", modules.Select(module => module.GetType().FullName));
                        throw new InvalidOperationException(
                            "Existing EventSystem or XRUIInputModule is missing, duplicated, disabled, or incompatible. " +
                            $"Sample setup does not repair consumer input globally. Found: {found}.");
                    }
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (prefab == null) throw new InvalidOperationException($"Inventory XR prefab is missing at {PrefabPath}.");
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                var surface = instance.GetComponent<InventoryWorldSpaceSurface>();
                if (surface == null) throw new InvalidOperationException("Inventory XR prefab has no InventoryWorldSpaceSurface.");
                surface.Prepare(camera, true);

                var gallery = instance.AddComponent<InventoryStateGallery>();
                var gallerySerialized = new SerializedObject(gallery);
                gallerySerialized.FindProperty("shell").objectReferenceValue = surface.Shell;
                gallerySerialized.ApplyModifiedPropertiesWithoutUndo();

                var bootstrap = instance.AddComponent<InventoryWorldSpaceSampleBootstrap>();
                var serialized = new SerializedObject(bootstrap);
                serialized.FindProperty("surface").objectReferenceValue = surface;
                serialized.FindProperty("gallery").objectReferenceValue = gallery;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                bootstrap.Replay(InventoryUiState.Partial);
                surface.Revalidate().ThrowIfInvalid();

                Selection.activeGameObject = instance;
                EditorGUIUtility.PingObject(instance);
            }
            catch
            {
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                if (createdEventSystem != null) UnityEngine.Object.DestroyImmediate(createdEventSystem);
                throw;
            }
        }
    }
}
