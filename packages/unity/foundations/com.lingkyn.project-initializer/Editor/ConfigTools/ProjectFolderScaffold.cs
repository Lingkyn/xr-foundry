using System;
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

        /// <summary>
        /// Scaffolds the contract folders under <see cref="IndieDirectoryContract.ProjectRoot"/>.
        /// </summary>
        public static ScaffoldResult EnsureIndieDirectories() =>
            EnsureDirectories(IndieDirectoryContract.ProjectRoot, IndieDirectoryContract.RequiredFolders);

        /// <summary>
        /// Ensures every folder in <paramref name="folders"/> exists under <paramref name="projectRoot"/>
        /// and drops a <c>.gitkeep</c> into each folder that is otherwise empty. Every folder is an
        /// asset path (<c>Assets/...</c>) that starts with the project root. Running it twice reuses
        /// every folder the first run created and adds nothing new.
        /// </summary>
        /// <exception cref="ArgumentException">A folder is not under <paramref name="projectRoot"/>.</exception>
        public static ScaffoldResult EnsureDirectories(string projectRoot, IEnumerable<string> folders)
        {
            if (string.IsNullOrEmpty(projectRoot)) throw new ArgumentException("Project root must not be empty.", nameof(projectRoot));
            if (folders == null) throw new ArgumentNullException(nameof(folders));

            var root = projectRoot.TrimEnd('/');
            var result = new ScaffoldResult();
            foreach (var folder in folders)
            {
                if (!IsUnderRoot(root, folder))
                {
                    throw new ArgumentException($"Folder '{folder}' is not under project root '{root}'.", nameof(folders));
                }

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

        static bool IsUnderRoot(string root, string folder)
        {
            if (string.IsNullOrEmpty(folder)) return false;
            return folder == root || folder.StartsWith(root + "/", StringComparison.Ordinal);
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
