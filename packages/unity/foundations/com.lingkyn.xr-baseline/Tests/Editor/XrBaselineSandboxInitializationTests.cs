using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Lingkyn.Unity.XrBaseline.Config;
using Lingkyn.Unity.XrBaseline.Constants;
using Lingkyn.Unity.XrBaseline.Editor;
using Lingkyn.Unity.XrBaseline.Editor.Menu;

namespace Lingkyn.Unity.XrBaseline.Tests
{
    /// <summary>
    /// Covers XB-06: Initialize Sandbox resets the diagnostics key set before any resolution runs
    /// and ends with exactly one <c>xr_baseline_initialized_with_unresolved</c> warning when any
    /// key was reported, or one <c>xr_baseline_initialized</c> line when none was. Each test drives
    /// <see cref="XrBaselineMenu.InitializeSandbox(SandboxInitializationOptions)"/> against a
    /// disposable <c>Assets/__XrBaselineTests_&lt;guid&gt;</c> root and an additive scene, and
    /// injects the rig source resolver so the "XRI Starter Assets sample is missing" outcome does
    /// not depend on what the host project has imported.
    /// </summary>
    public sealed class XrBaselineSandboxInitializationTests
    {
        const string RootParent = "Assets";
        const string RootPrefix = "__XrBaselineTests_";
        const string StaleKey = "test.stale-before-initialize";
        const string ResolverKey = "xri.starter-assets.rig-source";
        const string UnresolvedWarningPrefix = "xr_baseline_unresolved:";
        const string WithUnresolvedPrefix = "xr_baseline_initialized_with_unresolved:";
        const string CleanPrefix = "xr_baseline_initialized:";
        const string NoRigPrefix = "xr_baseline: no XRI Starter Assets rig source";

        string _root;
        Scene _previousActive;
        Scene _sandbox;
        int _withUnresolvedLines;
        int _cleanLines;

        [SetUp]
        public void SetUp()
        {
            XrBaselineDiagnostics.Reset();
            _previousActive = SceneManager.GetActiveScene();
            _sandbox = default;
            _withUnresolvedLines = 0;
            _cleanLines = 0;
            _root = RootParent + "/" + RootPrefix + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder(RootParent, _root.Substring(RootParent.Length + 1));
            Application.logMessageReceived += CountFinalLines;
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= CountFinalLines;
            XrBaselineDiagnostics.Reset();

            if (_previousActive.IsValid() && _previousActive.isLoaded)
            {
                SceneManager.SetActiveScene(_previousActive);
            }

            if (_sandbox.IsValid() && _sandbox.isLoaded && _sandbox != _previousActive)
            {
                EditorSceneManager.CloseScene(_sandbox, true);
            }

            DeleteRoot(_root);
        }

        [Test]
        public void InitializeSandboxResetsStaleKeysAndWarnsOnceWhenAKeyWasReported()
        {
            LogAssert.Expect(LogType.Warning, new Regex("^" + Regex.Escape(UnresolvedWarningPrefix + " " + StaleKey)));
            XrBaselineDiagnostics.Unresolved(StaleKey, "seeded before Initialize Sandbox; the reset must drop it.");
            Assume.That(XrBaselineDiagnostics.ReportedKeys, Does.Contain(StaleKey));

            var resolverRan = false;
            var staleKeyVisibleToResolver = true;
            var options = new SandboxInitializationOptions(
                _root,
                sceneMode: NewSceneMode.Additive,
                saveScene: true,
                rigSourceResolver: () =>
                {
                    resolverRan = true;
                    staleKeyVisibleToResolver = XrBaselineDiagnostics.ReportedKeys.Contains(StaleKey);
                    XrBaselineDiagnostics.Unresolved(ResolverKey, "the injected resolver stands in for a project without the XRI Starter Assets sample.");
                    return null;
                });

            LogAssert.Expect(LogType.Warning, new Regex("^" + Regex.Escape(UnresolvedWarningPrefix + " " + ResolverKey)));
            LogAssert.Expect(LogType.Warning, new Regex("^" + Regex.Escape(NoRigPrefix)));
            LogAssert.Expect(LogType.Warning, new Regex("^" + Regex.Escape(WithUnresolvedPrefix)));

            _sandbox = XrBaselineMenu.InitializeSandbox(options);

            Assert.That(resolverRan, Is.True, "The injected rig source resolver must run when the scene has no rig.");
            Assert.That(staleKeyVisibleToResolver, Is.False, "The key set must be reset before any resolution runs.");
            Assert.That(XrBaselineDiagnostics.ReportedKeys, Does.Not.Contain(StaleKey));
            Assert.That(XrBaselineDiagnostics.ReportedKeys, Does.Contain(ResolverKey));
            Assert.That(_withUnresolvedLines, Is.EqualTo(1), "Exactly one xr_baseline_initialized_with_unresolved line must end the run.");
            Assert.That(_cleanLines, Is.EqualTo(0), "A run with a reported key must not also log a clean success.");
            Assert.That(_sandbox.IsValid() && _sandbox.isLoaded, Is.True);
            Assert.That(_sandbox.path, Is.EqualTo(options.ScenePath));
            Assert.That(File.Exists(options.ScenePath), Is.True, "The injected scene path must receive the saved Sandbox.");
            Assert.That(options.ScenePath, Does.StartWith(_root + "/"));
            Assert.That(AssetDatabase.LoadAssetAtPath<VrBaselineConfig>(options.Layout.ConfigAsset), Is.Not.Null, "The config must be created under the injected root.");
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(options.Layout.FloorPlanePrefab), Is.Not.Null, "The greybox assets must be created under the injected root.");
        }

        [Test]
        public void InitializeSandboxLogsCleanSuccessWhenNoKeyWasReported()
        {
            var options = new SandboxInitializationOptions(
                _root,
                sceneMode: NewSceneMode.Additive,
                saveScene: false,
                rigSourceResolver: () => null);

            LogAssert.Expect(LogType.Warning, new Regex("^" + Regex.Escape(NoRigPrefix)));

            _sandbox = XrBaselineMenu.InitializeSandbox(options);

            // Every key the pipeline can report is a type-loaded check (XRI, Input System, a Lit
            // shader). In the pinned tuple the test assembly references XRI, so none is reported;
            // a tuple that does report one exercises the other branch, and this test is then
            // inconclusive rather than a false failure.
            Assume.That(
                XrBaselineDiagnostics.ReportedKeys,
                Is.Empty,
                "This tuple reported keys during a clean run: " + string.Join(", ", XrBaselineDiagnostics.ReportedKeys.ToArray()));

            Assert.That(_cleanLines, Is.EqualTo(1), "Exactly one xr_baseline_initialized line must end a run with no reported key.");
            Assert.That(_withUnresolvedLines, Is.EqualTo(0), "xr_baseline_initialized_with_unresolved must not be logged when nothing was reported.");
            Assert.That(File.Exists(options.ScenePath), Is.False, "saveScene: false must not write the scene asset.");
        }

        [Test]
        public void DefaultOptionsMatchTheFixedProjectPathConstants()
        {
            var options = SandboxInitializationOptions.Default;
            var layout = options.Layout;

            Assert.That(options.ProjectRoot, Is.EqualTo(VrBaselineProjectPaths.ProjectRoot));
            Assert.That(options.ScenePath, Is.EqualTo(VrBaselineProjectPaths.SandboxScene));
            Assert.That(options.SceneMode, Is.EqualTo(NewSceneMode.Single));
            Assert.That(options.SaveScene, Is.True);
            Assert.That(layout.SandboxScene, Is.EqualTo(VrBaselineProjectPaths.SandboxScene));
            Assert.That(layout.ConfigAsset, Is.EqualTo(VrBaselineConfig.DefaultAssetPath));
            Assert.That(layout.MaterialsFolder, Is.EqualTo(VrBaselineVisualPaths.MaterialsFolder));
            Assert.That(layout.PropsFolder, Is.EqualTo(VrBaselineVisualPaths.PropsFolder));
            Assert.That(layout.FloorMaterial, Is.EqualTo(VrBaselineVisualPaths.Floor));
            Assert.That(layout.EnvironmentMaterial, Is.EqualTo(VrBaselineVisualPaths.Environment));
            Assert.That(layout.InteractableMaterial, Is.EqualTo(VrBaselineVisualPaths.Interactable));
            Assert.That(layout.InteractableHighlightMaterial, Is.EqualTo(VrBaselineVisualPaths.InteractableHighlight));
            Assert.That(layout.DisabledMaterial, Is.EqualTo(VrBaselineVisualPaths.Disabled));
            Assert.That(layout.FloorPlanePrefab, Is.EqualTo(VrBaselineVisualPaths.FloorPlanePrefab));
            Assert.That(layout.GrabbableCubePrefab, Is.EqualTo(VrBaselineVisualPaths.GrabbableCubePrefab));
            Assert.That(layout.ScaleReferencePrefab, Is.EqualTo(VrBaselineVisualPaths.ScaleReferencePrefab));
            Assert.That(layout.XrPrefabsFolder, Is.EqualTo(VrBaselineProjectPaths.XrPrefabsFolder));
            Assert.That(layout.XrOriginRigPrefab, Is.EqualTo(VrBaselineProjectPaths.XrOriginRigPrefab));
        }

        void CountFinalLines(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Warning && condition.StartsWith(WithUnresolvedPrefix, StringComparison.Ordinal)) _withUnresolvedLines++;
            if (type == LogType.Log && condition.StartsWith(CleanPrefix, StringComparison.Ordinal)) _cleanLines++;
        }

        /// <summary>
        /// Removes the disposable root and everything under it, refusing any path that is not one
        /// of ours. Mirrors the project-initializer test helper.
        /// </summary>
        static void DeleteRoot(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith(RootParent + "/" + RootPrefix, StringComparison.Ordinal))
            {
                throw new ArgumentException($"Refusing to delete '{path}': not a disposable test root.", nameof(path));
            }

            if (AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.DeleteAsset(path);
            }

            var absolute = Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
            if (Directory.Exists(absolute)) Directory.Delete(absolute, true);
            if (File.Exists(absolute + ".meta")) File.Delete(absolute + ".meta");
            AssetDatabase.Refresh();
        }
    }
}
