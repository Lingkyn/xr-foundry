using UnityEditor;
using Lingkyn.Unity.XrBaseline.Editor.ConfigTools;
using Lingkyn.Unity.XrBaseline.Config;

namespace Lingkyn.Unity.XrBaseline.Editor.SceneSetup
{
    /// <summary>
    /// Ensures greybox materials and prefabs from VrBaselineConfig. Scene wiring stays in the consuming project.
    /// </summary>
    public static class VrBaselineAssetsSetup
    {
        public static void EnsureAssets(VrBaselineConfig config = null)
        {
            config ??= VrBaselineConfigAccess.EnsureExists();

            try
            {
                EditorApplication.LockReloadAssemblies();
                VrBaselineAssetFactory.EnsureBaselineAssets(config);
            }
            finally
            {
                EditorApplication.UnlockReloadAssemblies();
            }
        }
    }
}
