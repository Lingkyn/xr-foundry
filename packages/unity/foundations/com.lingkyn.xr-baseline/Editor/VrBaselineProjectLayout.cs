using Lingkyn.Unity.XrBaseline.Config;
using Lingkyn.Unity.XrBaseline.Constants;

namespace Lingkyn.Unity.XrBaseline.Editor
{
    /// <summary>
    /// Every asset and scene path the Sandbox Editor tools generate, derived from one project
    /// root. <see cref="Default"/> reproduces the fixed <see cref="VrBaselineProjectPaths"/>,
    /// <see cref="VrBaselineVisualPaths"/>, and <see cref="VrBaselineConfig.DefaultAssetPath"/>
    /// constants exactly; <see cref="FromRoot"/> rebases the same relative layout under a
    /// disposable root so EditMode tests can run the tools without touching
    /// <c>Assets/_Project</c>. The public helper entry points keep using <see cref="Default"/>.
    /// </summary>
    internal sealed class VrBaselineProjectLayout
    {
        public static VrBaselineProjectLayout Default { get; } = FromRoot(VrBaselineProjectPaths.ProjectRoot);

        VrBaselineProjectLayout(string projectRoot)
        {
            ProjectRoot = projectRoot;
        }

        public static VrBaselineProjectLayout FromRoot(string projectRoot)
        {
            if (string.IsNullOrEmpty(projectRoot))
            {
                throw new System.ArgumentException("A project root asset path is required.", nameof(projectRoot));
            }

            return new VrBaselineProjectLayout(projectRoot.Replace('\\', '/').TrimEnd('/'));
        }

        public string ProjectRoot { get; }

        public string ScenesFolder => ProjectRoot + "/Scenes";
        public string SandboxScene => ScenesFolder + "/Sandbox.unity";
        public string ConfigAsset => ProjectRoot + "/Data/Config/VrBaselineConfig.asset";

        public string MaterialsFolder => ProjectRoot + "/Art/Materials";
        public string PropsFolder => ProjectRoot + "/Prefabs/Props";

        public string FloorMaterial => MaterialsFolder + "/M_Floor.mat";
        public string EnvironmentMaterial => MaterialsFolder + "/M_Environment.mat";
        public string InteractableMaterial => MaterialsFolder + "/M_Interactable.mat";
        public string InteractableHighlightMaterial => MaterialsFolder + "/M_Interactable_Highlight.mat";
        public string DisabledMaterial => MaterialsFolder + "/M_Disabled.mat";

        public string FloorPlanePrefab => PropsFolder + "/FloorPlane.prefab";
        public string GrabbableCubePrefab => PropsFolder + "/GrabbableCube.prefab";
        public string ScaleReferencePrefab => PropsFolder + "/ScaleReference1m.prefab";

        public string XrPrefabsFolder => ProjectRoot + "/Prefabs/XR";
        public string XrOriginRigPrefab => XrPrefabsFolder + "/XROriginRig.prefab";
    }
}
