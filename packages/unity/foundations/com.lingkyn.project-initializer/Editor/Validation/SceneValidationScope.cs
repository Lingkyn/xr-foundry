using System;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Lingkyn.Unity.ProjectInitializer.Editor.Validation
{
    public static class SceneValidationScope
    {
        public static void WithScene(string scenePath, Action<Scene> validate)
        {
            if (string.IsNullOrEmpty(scenePath)) return;

            var wasAlreadyOpen = false;
            var scene = default(Scene);
            for (var i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var candidate = EditorSceneManager.GetSceneAt(i);
                if (candidate.path == scenePath && candidate.isLoaded)
                {
                    wasAlreadyOpen = true;
                    scene = candidate;
                    break;
                }
            }

            var openedHere = false;
            if (!wasAlreadyOpen)
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                openedHere = true;
            }

            try
            {
                validate(scene);
            }
            finally
            {
                if (openedHere && EditorSceneManager.sceneCount > 1)
                {
                    EditorSceneManager.CloseScene(scene, removeScene: true);
                }
            }
        }
    }
}
