using System.IO;
using UnityEditor;
using UnityEngine;
using Lingkyn.Unity.XrBaseline.Config;

namespace Lingkyn.Unity.XrBaseline.Editor.ConfigTools
{
    public static class VrBaselineConfigAccess
    {
        public static VrBaselineConfig LoadOrNull() =>
            AssetDatabase.LoadAssetAtPath<VrBaselineConfig>(VrBaselineConfig.DefaultAssetPath);

        public static VrBaselineConfig EnsureExists()
        {
            var existing = LoadOrNull();
            if (existing != null) return existing;

            EnsureConfigFolder(VrBaselineConfig.DefaultAssetPath);

            var asset = ScriptableObject.CreateInstance<VrBaselineConfig>();
            AssetDatabase.CreateAsset(asset, VrBaselineConfig.DefaultAssetPath);
            AssetDatabase.SaveAssets();
            return asset;
        }

        static void EnsureConfigFolder(string assetPath)
        {
            var folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;

            var parts = folder.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
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
