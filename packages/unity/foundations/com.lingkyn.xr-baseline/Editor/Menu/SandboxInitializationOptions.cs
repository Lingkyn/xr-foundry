using UnityEditor.SceneManagement;
using UnityEngine;
using Lingkyn.Unity.XrBaseline.Constants;
using Lingkyn.Unity.XrBaseline.Editor.SceneSetup;

namespace Lingkyn.Unity.XrBaseline.Editor.Menu
{
    /// <summary>
    /// Root-injectable inputs for <see cref="XrBaselineMenu.InitializeSandbox(SandboxInitializationOptions)"/>.
    /// The menu item runs with <see cref="Default"/>, which targets the fixed
    /// <see cref="VrBaselineProjectPaths"/> layout, opens or creates the Sandbox in
    /// <see cref="NewSceneMode.Single"/>, saves it, and resolves the rig source through
    /// <see cref="XrPrefabFactory.LoadOrResolveOriginSource()"/>. EditMode tests pass a disposable
    /// root under <c>Assets/</c>, an additive scene, and a rig source resolver of their own so the
    /// same code path runs without touching a real project.
    /// </summary>
    internal sealed class SandboxInitializationOptions
    {
        public static SandboxInitializationOptions Default => new SandboxInitializationOptions(VrBaselineProjectPaths.ProjectRoot);

        /// <param name="projectRoot">Asset folder every generated asset lives under (default <c>Assets/_Project</c>).</param>
        /// <param name="scenePath">Scene asset path to open or create; null means <c>&lt;projectRoot&gt;/Scenes/Sandbox.unity</c>.</param>
        /// <param name="sceneMode">How the Sandbox scene is opened or created.</param>
        /// <param name="saveScene">Whether the composed scene is saved to <paramref name="scenePath"/>.</param>
        /// <param name="rigSourceResolver">
        /// Returns the XR Origin prefab to instantiate, or null when none is available. Null selects
        /// the project-prefab-then-XRI-Starter-Assets lookup. A resolver that fails by name is
        /// expected to report through <see cref="XrBaselineDiagnostics.Unresolved"/> like every other
        /// by-name resolution in the Sandbox tools.
        /// </param>
        public SandboxInitializationOptions(
            string projectRoot,
            string scenePath = null,
            NewSceneMode sceneMode = NewSceneMode.Single,
            bool saveScene = true,
            System.Func<GameObject> rigSourceResolver = null)
        {
            Layout = VrBaselineProjectLayout.FromRoot(projectRoot);
            ScenePath = string.IsNullOrEmpty(scenePath) ? Layout.SandboxScene : scenePath.Replace('\\', '/');
            SceneMode = sceneMode;
            SaveScene = saveScene;
            RigSourceResolver = rigSourceResolver ?? (() => XrPrefabFactory.LoadOrResolveOriginSource(Layout));
        }

        public VrBaselineProjectLayout Layout { get; }
        public string ProjectRoot => Layout.ProjectRoot;
        public string ScenePath { get; }
        public NewSceneMode SceneMode { get; }
        public bool SaveScene { get; }
        public System.Func<GameObject> RigSourceResolver { get; }
    }
}
