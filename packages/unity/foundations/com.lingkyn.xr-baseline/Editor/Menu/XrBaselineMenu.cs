using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Lingkyn.Unity.XrBaseline.Config;
using Lingkyn.Unity.XrBaseline.Constants;
using Lingkyn.Unity.XrBaseline.Editor.ConfigTools;
using Lingkyn.Unity.XrBaseline.Editor.SceneSetup;

namespace Lingkyn.Unity.XrBaseline.Editor.Menu
{
    public static class XrBaselineMenu
    {
        [MenuItem("Tools/Lingkyn/XR Baseline/Initialize Sandbox")]
        public static void InitializeSandbox()
        {
            EnsureAssetFolder("Assets/_Project/Scenes");
            var scene = File.Exists(VrBaselineProjectPaths.SandboxScene)
                ? EditorSceneManager.OpenScene(VrBaselineProjectPaths.SandboxScene, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sceneRoot = EnsureSceneRoot(scene);
            var player = EnsurePath(sceneRoot.transform, "_Actors/Player");
            EnsurePath(sceneRoot.transform, "_Systems");
            EnsurePath(sceneRoot.transform, "_Lighting");
            EnsurePath(sceneRoot.transform, "_World/Environment");
            EnsurePath(sceneRoot.transform, "_Gameplay/Interactables");

            var config = VrBaselineConfigAccess.EnsureExists();
            VrBaselineAssetsSetup.EnsureAssets(config);
            VrBaselineScenePlacer.PlaceGreybox(scene, sceneRoot.transform, config);
            var rig = GenericXrRigFactory.EnsureRig(scene, player, config);
            if (rig == null)
            {
                Debug.LogWarning("xr_baseline: no XRI Starter Assets rig source was found; import the current XRI Starter Assets sample and run Initialize Sandbox again.");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, VrBaselineProjectPaths.SandboxScene);
            Debug.Log("xr_baseline_initialized: Sandbox saved. Device behavior remains unverified until a headset test is recorded.");
        }

        [MenuItem("Tools/Lingkyn/XR Baseline/Apply Config")]
        public static void ApplyConfig() => InitializeSandbox();

        [MenuItem("Tools/Lingkyn/XR Baseline/Enable Continuous Move")]
        public static void EnableContinuousMove() => XrContinuousMoveSetupTool.EnableContinuousMove();

        [MenuItem("Tools/Lingkyn/XR Baseline/Apply Smoke Build Settings")]
        public static void ApplySmokeBuildSettings() => VrSmokeBuildSettings.ApplySandboxOnly();

        public static void InitializeFromCommandLine()
        {
            InitializeSandbox();
            EditorApplication.Exit(0);
        }

        static GameObject EnsureSceneRoot(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "Scene_Root") return root;
            }

            var created = new GameObject("Scene_Root");
            SceneManager.MoveGameObjectToScene(created, scene);
            return created;
        }

        static Transform EnsurePath(Transform root, string path)
        {
            var current = root;
            foreach (var part in path.Split('/'))
            {
                var child = current.Find(part);
                if (child == null)
                {
                    var created = new GameObject(part);
                    created.transform.SetParent(current, false);
                    child = created.transform;
                }
                current = child;
            }
            return current;
        }

        static void EnsureAssetFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
