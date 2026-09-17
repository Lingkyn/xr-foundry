using System.IO;
using UnityEditor;
using UnityEngine;
using Lingkyn.Unity.XrBaseline.Config;

namespace Lingkyn.Unity.XrBaseline.Editor.ConfigTools
{
    public static class VrBaselineConfigAccess
    {
        public static VrBaselineConfig LoadOrNull() => LoadOrNull(VrBaselineConfig.DefaultAssetPath);

        public static VrBaselineConfig EnsureExists() => EnsureExists(VrBaselineConfig.DefaultAssetPath);

        /// <summary>Loads the config at <paramref name="assetPath"/>; tests pass a disposable-root path.</summary>
        internal static VrBaselineConfig LoadOrNull(string assetPath) =>
            AssetDatabase.LoadAssetAtPath<VrBaselineConfig>(assetPath);

        /// <summary>Loads or creates the config at <paramref name="assetPath"/>; tests pass a disposable-root path.</summary>
        internal static VrBaselineConfig EnsureExists(string assetPath)
        {
            var existing = LoadOrNull(assetPath);
            if (existing != null) return existing;

            EnsureConfigFolder(assetPath);

            var asset = ScriptableObject.CreateInstance<VrBaselineConfig>();
            AssetDatabase.CreateAsset(asset, assetPath);
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
