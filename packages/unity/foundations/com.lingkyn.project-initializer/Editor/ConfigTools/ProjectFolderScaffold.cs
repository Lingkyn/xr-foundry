using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Lingkyn.Unity.ProjectInitializer.Editor.ConfigTools
{
    public static class ProjectFolderScaffold
    {
        public sealed class ScaffoldResult
        {
            public List<string> CreatedFolders { get; } = new();
            public List<string> ReusedFolders { get; } = new();
            public List<string> CreatedGitKeeps { get; } = new();
        }

        public static ScaffoldResult EnsureIndieDirectories()
        {
            var result = new ScaffoldResult();
            foreach (var folder in IndieDirectoryContract.RequiredFolders)
            {
                if (EnsureFolderChain(folder, out var createdChain))
                {
                    if (createdChain) result.CreatedFolders.Add(folder);
                    else result.ReusedFolders.Add(folder);
                }

                if (EnsureGitKeepIfEmpty(folder))
                {
                    result.CreatedGitKeeps.Add($"{folder}/.gitkeep");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return result;
        }

        static bool EnsureFolderChain(string assetPath, out bool createdAny)
        {
            createdAny = false;
            if (AssetDatabase.IsValidFolder(assetPath)) return true;

            var parts = assetPath.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                    createdAny = true;
                }

                current = next;
            }

            return AssetDatabase.IsValidFolder(assetPath);
        }

        static bool EnsureGitKeepIfEmpty(string assetFolder)
        {
            var absoluteFolder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetFolder));
            if (!Directory.Exists(absoluteFolder)) return false;

            foreach (var file in Directory.GetFiles(absoluteFolder))
            {
                if (!file.EndsWith(".meta")) return false;
            }

            foreach (var dir in Directory.GetDirectories(absoluteFolder))
            {
                return false;
            }

            var gitKeepPath = Path.Combine(absoluteFolder, ".gitkeep");
            if (File.Exists(gitKeepPath)) return false;

            File.WriteAllText(gitKeepPath, string.Empty);
            return true;
        }
    }
}
