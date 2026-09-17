using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Lingkyn.Unity.XrBaseline.Config;
using Lingkyn.Unity.XrBaseline.Editor.ConfigTools;
using Lingkyn.Unity.XrBaseline.Editor.SceneSetup;
using Lingkyn.Unity.XrBaseline.Editor;

namespace Lingkyn.Unity.XrBaseline.Editor.Menu
{
    public static class XrBaselineMenu
    {
        [MenuItem("Tools/Lingkyn/XR Baseline/Initialize Sandbox")]
        public static void InitializeSandbox() => InitializeSandbox(SandboxInitializationOptions.Default);

        /// <summary>
        /// The Initialize Sandbox pipeline with its project root, scene path, scene mode, save
        /// step, and rig source lookup injected. The menu item passes
        /// <see cref="SandboxInitializationOptions.Default"/>; EditMode tests pass a disposable root
        /// and an additive scene. Returns the composed scene so a caller can inspect or close it.
        /// </summary>
        internal static Scene InitializeSandbox(SandboxInitializationOptions options)
        {
            var layout = options.Layout;
            XrBaselineDiagnostics.Reset();
            EnsureAssetFolder(layout.ScenesFolder);
            var scene = File.Exists(options.ScenePath)
                ? EditorSceneManager.OpenScene(options.ScenePath, ToOpenSceneMode(options.SceneMode))
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, options.SceneMode);
            if (SceneManager.GetActiveScene() != scene)
            {
                // Single mode already made the scene active; an additive test scene must be active so
                // the lighting setup writes RenderSettings into the disposable scene, not the caller's.
                SceneManager.SetActiveScene(scene);
            }

            var sceneRoot = EnsureSceneRoot(scene);
            var player = EnsurePath(sceneRoot.transform, "_Actors/Player");
            EnsurePath(sceneRoot.transform, "_Systems");
            EnsurePath(sceneRoot.transform, "_Lighting");
            EnsurePath(sceneRoot.transform, "_World/Environment");
            EnsurePath(sceneRoot.transform, "_Gameplay/Interactables");

            var config = VrBaselineConfigAccess.EnsureExists(layout.ConfigAsset);
            VrBaselineAssetsSetup.EnsureAssets(config, layout);
            VrBaselineScenePlacer.PlaceGreybox(scene, sceneRoot.transform, config, layout);
            var rig = GenericXrRigFactory.EnsureRig(scene, player, config, options.RigSourceResolver);
            if (rig == null)
            {
                Debug.LogWarning("xr_baseline: no XRI Starter Assets rig source was found; import the current XRI Starter Assets sample and run Initialize Sandbox again.");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (options.SaveScene)
            {
                EditorSceneManager.SaveScene(scene, options.ScenePath);
            }

            if (XrBaselineDiagnostics.ReportedKeys.Count > 0)
            {
                Debug.LogWarning($"xr_baseline_initialized_with_unresolved: Sandbox saved, but {XrBaselineDiagnostics.ReportedKeys.Count} item(s) could not be configured; see the xr_baseline_unresolved warnings above.");
            }
            else
            {
                Debug.Log("xr_baseline_initialized: Sandbox saved. Device behavior remains unverified until a headset test is recorded.");
            }

            return scene;
        }

        static OpenSceneMode ToOpenSceneMode(NewSceneMode mode) =>
            mode == NewSceneMode.Additive ? OpenSceneMode.Additive : OpenSceneMode.Single;

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
