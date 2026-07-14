using System.IO;
using UnityEditor;
using UnityEngine;
using Lingkyn.Unity.XrBaseline.Constants;

namespace Lingkyn.Unity.XrBaseline.Editor.SceneSetup
{
    public static class XrPrefabFactory
    {
        const string SampleOriginPath =
            "Assets/Samples/XR Interaction Toolkit/3.3.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";

        public static GameObject LoadOrResolveOriginSource()
        {
            var projectPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VrBaselineProjectPaths.XrOriginRigPrefab);
            if (projectPrefab != null) return UnwrapBuildingBlockPrefab(projectPrefab);

            if (File.Exists(SampleOriginPath))
            {
                return AssetDatabase.LoadAssetAtPath<GameObject>(SampleOriginPath);
            }

            var guids = AssetDatabase.FindAssets("XR Origin (XR Rig) t:Prefab");
            GameObject newest = null;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("XR Interaction Toolkit") || !path.Contains("Starter Assets"))
                {
                    continue;
                }

                var candidate = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (candidate == null) continue;
                newest = candidate;
            }

            return newest;
        }

        static GameObject UnwrapBuildingBlockPrefab(GameObject prefab)
        {
            if (prefab == null) return null;
            if (!prefab.name.Contains("[Building Block]")) return prefab;

            foreach (Transform child in prefab.transform)
            {
                if (child.name.Contains("XR Origin")) return child.gameObject;
            }

            return prefab;
        }

        public static void SaveAsProjectPrefab(GameObject instance)
        {
            if (instance == null) return;
            EnsureFolder(VrBaselineProjectPaths.XrPrefabsFolder);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(VrBaselineProjectPaths.XrOriginRigPrefab) != null) return;

            var root = PrefabUtility.GetOutermostPrefabInstanceRoot(instance) ?? instance;
            PrefabUtility.SaveAsPrefabAsset(root, VrBaselineProjectPaths.XrOriginRigPrefab);
            AssetDatabase.SaveAssets();
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            var name = Path.GetFileName(folder);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(name))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
