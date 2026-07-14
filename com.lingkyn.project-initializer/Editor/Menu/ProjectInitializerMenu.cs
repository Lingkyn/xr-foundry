using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Lingkyn.Unity.ProjectInitializer.Editor.Build;
using Lingkyn.Unity.ProjectInitializer.Editor.ConfigTools;
using Lingkyn.Unity.ProjectInitializer.Editor.SceneSetup;
using Lingkyn.Unity.ProjectInitializer.Editor.Validation;

namespace Lingkyn.Unity.ProjectInitializer.Editor.Menu
{
    public static class ProjectInitializerMenu
    {
        [MenuItem("Tools/Lingkyn/Project Initializer/Initialize")]
        public static void InitializeProject()
        {
            Debug.Log("initializer_started");
            var scaffold = ProjectFolderScaffold.EnsureIndieDirectories();
            Debug.Log(
                $"folder_scaffold_completed created={scaffold.CreatedFolders.Count} " +
                $"reused={scaffold.ReusedFolders.Count} gitkeeps={scaffold.CreatedGitKeeps.Count}");
            CreateDefaultConfigs.CreateAll();
            BaselinePrefabFactory.CreateMissingSystemPrefabs();
            SceneSetupTool.SetupAllBaselineScenes();
            var report = IndieProjectValidator.ValidateIndieBaseline();
            Debug.Log(report.ToJson());
            Debug.Log("initializer_completed");
            Debug.Log("Next: install com.lingkyn.xr-baseline and run Tools/Lingkyn/XR Baseline/Initialize Sandbox when targeting XR.");
        }

        [MenuItem("Tools/Lingkyn/Project Initializer/Validate")]
        public static void ValidateProject()
        {
            var report = IndieProjectValidator.ValidateIndieBaseline();
            Debug.Log(report.ToJson());
        }

        [MenuItem("Tools/Lingkyn/Project Initializer/Create Default Settings")]
        public static void CreateDefaultConfigsMenu()
        {
            CreateDefaultConfigs.CreateAll();
            Debug.Log("config_created");
        }

        [MenuItem("Tools/Lingkyn/Project Initializer/Setup Current Scene")]
        public static void SetupCurrentScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(scene.path))
            {
                SceneSetupTool.SetupScene(scene.path);
                return;
            }

            var withLighting = scene.name is "MainMenu" or "Level_01" or "Sandbox";
            switch (scene.name)
            {
                case "Boot":
                    SceneRootBuilder.EnsureBootScene(scene);
                    break;
                case "MainMenu":
                    SceneRootBuilder.EnsureMainMenuScene(scene);
                    break;
                case "Level_01":
                    SceneRootBuilder.EnsureLevelScene(scene, sandbox: false);
                    break;
                case "Sandbox":
                    SceneRootBuilder.EnsureLevelScene(scene, sandbox: true);
                    break;
                default:
                    SceneRootBuilder.EnsureSceneRoot(scene, withLighting);
                    break;
            }

            EditorSceneManager.MarkSceneDirty(scene);
        }

        [MenuItem("Tools/Lingkyn/Project Initializer/Run Build Validation")]
        public static void RunBuildValidation()
        {
            var report = BuildValidator.ValidateForBuild();
            Debug.Log(report.ToJson());
        }

        public static void InitializeFromCommandLine()
        {
            InitializeProject();
            EditorApplication.Exit(0);
        }
    }
}
