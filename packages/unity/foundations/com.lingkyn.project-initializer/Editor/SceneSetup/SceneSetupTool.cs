using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Lingkyn.Unity.ProjectInitializer.Editor.ConfigTools;

namespace Lingkyn.Unity.ProjectInitializer.Editor.SceneSetup
{
    public static class SceneSetupTool
    {
        public static void SetupScene(string sceneAssetPath)
        {
            var scene = File.Exists(sceneAssetPath)
                ? EditorSceneManager.OpenScene(sceneAssetPath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var name = Path.GetFileNameWithoutExtension(sceneAssetPath);
            switch (name)
            {
                case "Boot": SceneRootBuilder.EnsureBootScene(scene); break;
                case "MainMenu": SceneRootBuilder.EnsureMainMenuScene(scene); break;
                case "Level_01": SceneRootBuilder.EnsureLevelScene(scene, false); break;
                case "Sandbox": SceneRootBuilder.EnsureLevelScene(scene, true); break;
                default: SceneRootBuilder.EnsureSceneRoot(scene, true); break;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, sceneAssetPath);
        }

        public static void SetupAllBaselineScenes()
        {
            foreach (var path in IndieDirectoryContract.BaselineScenes) SetupScene(path);
        }
    }
}
