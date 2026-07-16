using UnityEditor;
using UnityEngine;
using Lingkyn.Unity.ProjectInitializer.Editor.ConfigTools;

namespace Lingkyn.Unity.ProjectInitializer.Editor.SceneSetup
{
    public static class PlayModeTestBuildSettings
    {
        public static void ApplyAllScenes()
        {
            var paths = IndieDirectoryContract.BaselineScenes;
            var scenes = new EditorBuildSettingsScene[paths.Length];
            for (var i = 0; i < paths.Length; i++) scenes[i] = new EditorBuildSettingsScene(paths[i], true);
            EditorBuildSettings.scenes = scenes;
            Debug.Log("project_initializer: baseline scenes added to Build Settings.");
        }
    }
}
