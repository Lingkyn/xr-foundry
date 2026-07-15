using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Lingkyn.Inventory.UGUI.Editor
{
    public static class InventoryPrefabFactory
    {
        private const string Root = "Packages/com.lingkyn.inventory.ugui/Runtime/Prefabs";

        [MenuItem("Tools/XR Foundry/Inventory/Rebuild UGUI Prefabs")]
        public static void Rebuild()
        {
            Directory.CreateDirectory(Root);
            var item = SaveLeaf("ItemView", typeof(InventoryItemView));
            var slot = SaveWithChildren("InventorySlot", typeof(InventorySlotView), item);
            var grid = SaveWithChildren("InventoryGrid", typeof(InventoryGridView), slot);
            var details = SaveLeaf("ItemDetails", typeof(InventoryDetailsView));
            var actions = SaveLeaf("ActionMenu", typeof(InventoryActionMenuView), addCanvasGroup: true);
            var panel = SaveWithChildren("InventoryPanel", typeof(InventoryPanelView), grid, details, actions);
            SaveWithChildren("InventoryShell", typeof(InventoryShellView), panel);
            CreateVariant(slot, "InventorySlotCompact");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static GameObject SaveLeaf(string name, System.Type component, bool addCanvasGroup = false)
        {
            var root = new GameObject(name, typeof(RectTransform), component);
            if (addCanvasGroup) root.AddComponent<CanvasGroup>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{Root}/{name}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject SaveWithChildren(string name, System.Type component, params GameObject[] children)
        {
            var root = new GameObject(name, typeof(RectTransform), component);
            foreach (var child in children)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(child);
                instance.transform.SetParent(root.transform, false);
            }
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{Root}/{name}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void CreateVariant(GameObject source, string name)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            instance.name = name;
            PrefabUtility.SaveAsPrefabAsset(instance, $"{Root}/{name}.prefab");
            Object.DestroyImmediate(instance);
        }
    }
}
