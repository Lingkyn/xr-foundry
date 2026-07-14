using UnityEditor;
using UnityEngine;
using Lingkyn.Unity.XrBaseline.Constants;

namespace Lingkyn.Unity.XrBaseline.Editor.SceneSetup
{
    /// <summary>
    /// One-click Build Settings for headset greybox smoke tests.
    /// </summary>
    public static class VrSmokeBuildSettings
    {
        public static void ApplySandboxOnly()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(VrBaselineProjectPaths.SandboxScene, true),
            };
            Debug.Log("xr_smoke_build_settings_applied: Sandbox only (index 0). " +
                      "Restore the consumer project's normal Build Settings after the smoke test.");
        }
    }
}
