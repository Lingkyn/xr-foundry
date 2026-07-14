using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lingkyn.Unity.ProjectInitializer.Editor.SceneSetup
{
    public static class SceneRootBuilder
    {
        public static void EnsureBootScene(Scene scene)
        {
            var root = EnsureRoot(scene);
            var systems = EnsureChild(root.transform, "_Systems");
            EnsureChild(systems, "GameBootstrap");
            EnsureChild(systems, "ServiceHost");
            EnsureChild(root.transform, "_UI");
            EnsureChild(root.transform, "_Debug");
        }

        public static void EnsureMainMenuScene(Scene scene)
        {
            var root = EnsureRoot(scene);
            EnsureChild(root.transform, "_Systems");
            EnsureChild(root.transform, "_Cameras");
            EnsureBaselineLight(EnsureChild(root.transform, "_Lighting"));
            EnsureChild(root.transform, "_UI");
            EnsureChild(root.transform, "_Audio");
            EnsureChild(root.transform, "_Debug");
        }

        public static void EnsureLevelScene(Scene scene, bool sandbox)
        {
            var root = EnsureRoot(scene);
            EnsureChild(root.transform, "_Systems");
            EnsureChild(root.transform, "_Cameras");
            EnsureBaselineLight(EnsureChild(root.transform, "_Lighting"));
            var world = EnsureChild(root.transform, "_World");
            EnsureChild(world, "Environment");
            EnsureChild(world, "Props");
            EnsureChild(world, "Navigation");
            var actors = EnsureChild(root.transform, "_Actors");
            EnsureChild(actors, "Player");
            EnsureChild(actors, "NPCs");
            var gameplay = EnsureChild(root.transform, "_Gameplay");
            EnsureChild(gameplay, "Interactables");
            EnsureChild(gameplay, "Objectives");
            EnsureChild(root.transform, "_SpawnPoints");
            EnsureChild(root.transform, "_Volumes");
            EnsureChild(root.transform, "_UI");
            EnsureChild(root.transform, "_Audio");
            var debug = EnsureChild(root.transform, "_Debug");
            if (sandbox) EnsureChild(debug, "TestHarnesses");
        }

        public static void EnsureSceneRoot(Scene scene, bool withLighting)
        {
            var root = EnsureRoot(scene);
            EnsureChild(root.transform, "_Systems");
            if (withLighting) EnsureBaselineLight(EnsureChild(root.transform, "_Lighting"));
        }

        static GameObject EnsureRoot(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "Scene_Root") return root;
            }

            var created = new GameObject("Scene_Root");
            SceneManager.MoveGameObjectToScene(created, scene);
            return created;
        }

        static Transform EnsureChild(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing;
            var created = new GameObject(name);
            created.transform.SetParent(parent, false);
            return created.transform;
        }

        static void EnsureBaselineLight(Transform parent)
        {
            if (parent.GetComponentInChildren<Light>() != null) return;
            var created = new GameObject("Directional Light");
            created.transform.SetParent(parent, false);
            created.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            created.AddComponent<Light>().type = LightType.Directional;
        }
    }
}
