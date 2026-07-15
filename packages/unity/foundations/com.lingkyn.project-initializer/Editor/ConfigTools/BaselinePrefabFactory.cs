using UnityEditor;
using UnityEngine;

namespace Lingkyn.Unity.ProjectInitializer.Editor.ConfigTools
{
    public static class BaselinePrefabFactory
    {
        public static void CreateMissingSystemPrefabs()
        {
            CreatePrefab("Assets/_Project/Prefabs/Systems/ServiceHost.prefab", "ServiceHost");
            CreatePrefab("Assets/_Project/Prefabs/Systems/RuntimeDiagnostics.prefab", "RuntimeDiagnostics");
            CreatePrefab("Assets/_Project/Prefabs/UI/UIRoot.prefab", "UIRoot");
            CreatePrefab("Assets/_Project/Prefabs/UI/LoadingScreen.prefab", "LoadingScreen");
            CreatePrefab("Assets/_Project/Prefabs/UI/DebugOverlay.prefab", "DebugOverlay");
            AssetDatabase.SaveAssets();
        }

        static void CreatePrefab(string path, string objectName, System.Action<GameObject> configure = null)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

            EnsureFolder(path);
            var go = new GameObject(objectName);
            configure?.Invoke(go);
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        static void EnsureFolder(string assetPath)
        {
            var parts = assetPath.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length - 1; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}
