using System.IO;
using UnityEditor;
using UnityEngine;
using Lingkyn.Unity.ProjectInitializer.Editor.Settings;

namespace Lingkyn.Unity.ProjectInitializer.Editor.ConfigTools
{
    public static class CreateDefaultConfigs
    {
        public static void CreateAll()
        {
            ProjectFolderScaffold.EnsureIndieDirectories();
            CreateInputActionsIfMissing(IndieDirectoryContract.SettingsRoot + "/InputActions.inputactions");
            CreateActivationMarker();
            CreateAnchorDocumentIfMissing("ARCHITECTURE.md", "# Architecture\n\nDescribe runtime ownership, assembly boundaries, and dependency direction here.\n");
            CreateAnchorDocumentIfMissing("DEVELOPMENT_CONVENTIONS.md", "# Development Conventions\n\nRecord project-specific naming, scene, prefab, test, and validation conventions here.\n");
            CreateAnchorDocumentIfMissing("README.md", "# Unity Project\n\nDescribe the project purpose, setup, validation, and contribution workflow here.\n");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ProjectInputSystemSetup.ApplyWhenEnabled(true);
        }

        static void CreateActivationMarker()
        {
            if (File.Exists(IndieDirectoryContract.ActivationMarker)) return;
            File.WriteAllText(
                IndieDirectoryContract.ActivationMarker,
                "Initialized by com.lingkyn.project-initializer. Remove this marker to disable build validation.\n");
        }

        static void CreateAnchorDocumentIfMissing(string fileName, string body)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var path = Path.Combine(projectRoot, fileName);
            if (!File.Exists(path)) File.WriteAllText(path, body);
        }

        static void CreateInputActionsIfMissing(string path)
        {
            if (File.Exists(path)) return;
            File.WriteAllText(path, "{\n    \"name\": \"InputActions\",\n    \"maps\": [],\n    \"controlSchemes\": []\n}\n");
            AssetDatabase.ImportAsset(path);
        }
    }
}
