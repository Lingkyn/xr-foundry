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
        public static void EnsureAssets(VrBaselineConfig config = null) =>
            EnsureAssets(config, VrBaselineProjectLayout.Default);

        /// <summary>Same as <see cref="EnsureAssets(VrBaselineConfig)"/> under an injected project layout.</summary>
        internal static void EnsureAssets(VrBaselineConfig config, VrBaselineProjectLayout layout)
        {
            config ??= VrBaselineConfigAccess.EnsureExists(layout.ConfigAsset);

            try
            {
                EditorApplication.LockReloadAssemblies();
                VrBaselineAssetFactory.EnsureBaselineAssets(config, layout);
            }
            finally
            {
                EditorApplication.UnlockReloadAssemblies();
            }
        }
    }
}
