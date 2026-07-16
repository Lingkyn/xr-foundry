using Lingkyn.Inventory.UGUI;
using Lingkyn.Inventory.UGUI.Samples;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Lingkyn.Inventory.UGUI.Samples.Editor
{
    public static class InventoryStateGallerySampleSetup
    {
        private const string GalleryPath = "Packages/com.lingkyn.inventory.ugui/Runtime/Prefabs/InventoryStateGallery.prefab";

        [MenuItem("Tools/XR Foundry/Inventory/Create UGUI State Gallery Sample")]
        public static void Create()
        {
            var canvasObject = new GameObject("Inventory State Gallery Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            if (Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
                var inputSystemModule = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
                if (inputSystemModule == null)
                {
                    throw new System.InvalidOperationException("The Input System is active, but InputSystemUIInputModule is unavailable.");
                }
                eventSystemObject.AddComponent(inputSystemModule);
#elif ENABLE_LEGACY_INPUT_MANAGER
                eventSystemObject.AddComponent<StandaloneInputModule>();
#else
                throw new System.InvalidOperationException("No supported Unity UI input backend is active.");
#endif
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GalleryPath);
            if (prefab == null) throw new System.InvalidOperationException($"Shipped gallery prefab is missing at {GalleryPath}.");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
            var rect = (RectTransform)instance.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(640f, 440f);

            var bootstrap = instance.AddComponent<InventoryStateGalleryBootstrap>();
            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("gallery").objectReferenceValue = instance.GetComponent<InventoryStateGallery>();
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = instance;
            EditorGUIUtility.PingObject(instance);
        }
    }
}
