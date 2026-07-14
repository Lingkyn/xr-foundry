using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Lingkyn.Unity.XrBaseline.Editor.ConfigTools;
using Lingkyn.Unity.XrBaseline.Config;
using Lingkyn.Unity.XrBaseline.Constants;

namespace Lingkyn.Unity.XrBaseline.Editor.SceneSetup
{
    /// <summary>
    /// P2 opt-in: re-enable XRI Continuous Move on the active Sandbox rig.
    /// </summary>
    public static class XrContinuousMoveSetupTool
    {
        public static void EnableContinuousMove()
        {
            var config = VrBaselineConfigAccess.EnsureExists();
            config.enableContinuousMove = true;
            EditorUtility.SetDirty(config);

            var scene = EditorSceneManager.OpenScene(VrBaselineProjectPaths.SandboxScene, OpenSceneMode.Single);
            var rig = FindSandboxRig(scene);
            if (rig == null)
            {
                Debug.LogError("No XR rig in Sandbox. Run Tools/Lingkyn/XR Baseline/Initialize Sandbox first.");
                return;
            }

            XrLocomotionSetupTool.ApplyContinuousMovePreference(rig, true);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("xr_continuous_move_enabled");
        }

        static GameObject FindSandboxRig(Scene scene) => GenericXrRigFactory.FindRigInScene(scene);
    }
}
