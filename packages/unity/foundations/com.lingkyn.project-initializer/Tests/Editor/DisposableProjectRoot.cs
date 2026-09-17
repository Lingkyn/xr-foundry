using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Lingkyn.Unity.ProjectInitializer.Tests
{
    /// <summary>
    /// A throwaway project root under <c>Assets/__FoundationsTests_&lt;guid&gt;</c> so scaffold and
    /// validator tests never touch the real <c>Assets/_Project</c>. Every test that creates one
    /// deletes it in a <c>finally</c> block.
    /// </summary>
    internal static class DisposableProjectRoot
    {
        const string Parent = "Assets";
        const string Prefix = "__FoundationsTests_";

        /// <summary>A fresh root path that does not exist yet.</summary>
        public static string NewPath() => Parent + "/" + Prefix + Guid.NewGuid().ToString("N");

        /// <summary>Creates a fresh, empty root folder and returns its asset path.</summary>
        public static string Create()
        {
            var path = NewPath();
            AssetDatabase.CreateFolder(Parent, path.Substring(Parent.Length + 1));
            return path;
        }

        /// <summary>
        /// Removes the root and everything under it. <see cref="AssetDatabase.DeleteAsset"/> is the
        /// primary path; a leftover on disk (for example after a failed import) is removed directly.
        /// </summary>
        public static void Delete(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith(Parent + "/" + Prefix, StringComparison.Ordinal))
            {
                throw new ArgumentException($"Refusing to delete '{path}': not a disposable test root.", nameof(path));
            }

            if (AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.DeleteAsset(path);
            }

            var absolute = Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
            if (Directory.Exists(absolute)) Directory.Delete(absolute, true);
            if (File.Exists(absolute + ".meta")) File.Delete(absolute + ".meta");
            AssetDatabase.Refresh();
        }
    }
}
