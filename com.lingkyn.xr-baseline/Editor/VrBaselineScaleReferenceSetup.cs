using UnityEditor;
using UnityEngine;
using Lingkyn.Unity.XrBaseline.Constants;

namespace Lingkyn.Unity.XrBaseline.Editor.SceneSetup
{
    /// <summary>
    /// Creates a 1 m pole and ground cross at the origin for scale / tracking sanity checks.
    /// </summary>
    public static class VrBaselineScaleReferenceSetup
    {
        public static void EnsureScaleReferencePrefab()
        {
            var path = VrBaselineVisualPaths.ScaleReferencePrefab;
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return;

            var root = new GameObject("ScaleReference1m");

            CreatePart(root.transform, "Pole1m", new Vector3(0.04f, 1f, 0.04f), new Vector3(0f, 0.5f, 0f),
                VrBaselineVisualPaths.InteractableHighlight);
            CreatePart(root.transform, "CrossX", new Vector3(1f, 0.02f, 0.04f), new Vector3(0f, 0.01f, 0f),
                VrBaselineVisualPaths.Environment);
            CreatePart(root.transform, "CrossZ", new Vector3(0.04f, 0.02f, 1f), new Vector3(0f, 0.01f, 0f),
                VrBaselineVisualPaths.Environment);

            SavePrefab(root, path);
        }

        public static void EnsureScaleReference(Transform environmentParent)
        {
            if (environmentParent == null) return;

            EnsureScaleReferencePrefab();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VrBaselineVisualPaths.ScaleReferencePrefab);
            if (prefab == null) return;

            var existing = environmentParent.Find(VrBaselineVisualPaths.SandboxScaleReferenceName);
            if (existing != null)
            {
                existing.localPosition = Vector3.zero;
                existing.localRotation = Quaternion.identity;
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, environmentParent);
            instance.name = VrBaselineVisualPaths.SandboxScaleReferenceName;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
        }

        static void CreatePart(Transform parent, string name, Vector3 scale, Vector3 localPosition, string materialPath)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localScale = scale;
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.identity;

            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            var renderer = part.GetComponent<MeshRenderer>();
            if (renderer != null && material != null) renderer.sharedMaterial = material;

            var collider = part.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
        }

        static void SavePrefab(GameObject root, string path)
        {
            var folder = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
            {
                var parts = folder.Split('/');
                var current = parts[0];
                for (var i = 1; i < parts.Length; i++)
                {
                    var next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                    current = next;
                }
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }
    }
}
