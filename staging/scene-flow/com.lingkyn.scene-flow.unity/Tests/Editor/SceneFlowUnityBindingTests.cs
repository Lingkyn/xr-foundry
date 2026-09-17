using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Lingkyn.SceneFlow.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Lingkyn.SceneFlow.Unity.Editor.Tests
{
    // A real AsyncOperation and the build scene list cannot be driven deterministically
    // inside an EditMode test, so every test drives the adapter through
    // ISceneLoaderSurface with a fake that mirrors the engine's by-name contract:
    // an unregistered path, a refused activation, and a refused unload all report
    // failure through a return value, exactly as SceneManager does. No test in this
    // file claims that a scene appeared, that a fade rendered, how long a load took,
    // or anything about frame timing during a transition.
    public sealed class SceneFlowUnityBindingTests
    {
        private static readonly SceneSetId Menu = SceneSetId.Parse("menu");
        private static readonly SceneSetId Level1 = SceneSetId.Parse("level1");
        private static readonly SceneSetId Level2 = SceneSetId.Parse("level2");
        private static readonly SceneSetId Fallback = SceneSetId.Parse("fallback");
        private static readonly SceneId MenuMain = SceneId.Parse("menu.main");
        private static readonly SceneId Level1Play = SceneId.Parse("level1.play");
        private static readonly SceneId Level1Audio = SceneId.Parse("level1.audio");
        private static readonly SceneId Level2Play = SceneId.Parse("level2.play");
        private static readonly SceneId FallbackError = SceneId.Parse("fallback.error");

        private const string MenuPath = "Assets/Scenes/Menu.unity";
        private const string Level1Path = "Assets/Scenes/Level1.unity";
        private const string Level1AudioPath = "Assets/Scenes/Level1Audio.unity";
        private const string Level2Path = "Assets/Scenes/Level2.unity";
        private const string FallbackPath = "Assets/Scenes/Fallback.unity";

        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _created)
            {
                if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
            }
            _created.Clear();
        }

        // ----- binding validation -----

        [Test]
        public void BindingAssetConvertsToBindingWithoutMutation()
        {
            var asset = CreateAsset(AllEntries(), null);
            var before = EditorJsonUtility.ToJson(asset);
            var surface = FullyRegistered();

            var binding = asset.ToBinding(Graph(), ZeroFadeOptions(), surface);

            Assert.That(binding.SceneCount, Is.EqualTo(5));
            Assert.That(binding.TryGetScenePath(MenuMain, out var path), Is.True);
            Assert.That(path, Is.EqualTo(MenuPath));
            Assert.That(binding.TryGetScenePath(SceneId.Parse("ghost"), out _), Is.False);
            Assert.That(EditorJsonUtility.ToJson(asset), Is.EqualTo(before));
        }

        [Test]
        public void BindingValidationReportsUnregisteredOrDisabledScenePathWithFieldPathAndSource()
        {
            var asset = CreateAsset(new[] { B(MenuMain, "Assets/Scenes/Missing.unity"), B(Level1Play, "Assets/Scenes/Disabled.unity"), B(Level1Audio, "") }, null);
            var surface = Fake();
            surface.RegisteredDisabled.Add("Assets/Scenes/Disabled.unity");

            var report = SceneBindingValidation.Validate(asset, GraphLevel1(), ZeroFadeOptions(), surface);

            var codes = report.Diagnostics.Where(item => item.Code == SceneBindingValidation.SceneUnregistered).Select(item => item.FieldPath).ToList();
            Assert.That(codes, Is.EquivalentTo(new[] { "scenes.Array.data[0].scenePath", "scenes.Array.data[1].scenePath", "scenes.Array.data[2].scenePath" }));
            Assert.That(report.Diagnostics.All(item => item.Source == asset), Is.True);
        }

        [Test]
        public void BindingValidationReportsDuplicateBindingWithFieldPath()
        {
            var asset = CreateAsset(new[] { B(MenuMain, MenuPath), B(MenuMain, "Assets/Scenes/Menu2.unity") }, null);
            var surface = Fake();
            surface.RegisteredEnabled.Add(MenuPath);
            surface.RegisteredEnabled.Add("Assets/Scenes/Menu2.unity");

            var report = SceneBindingValidation.Validate(asset, GraphMenu(), ZeroFadeOptions(), surface);

            var duplicate = report.Diagnostics.Single(item => item.Code == SceneBindingValidation.BindingDuplicate);
            Assert.That(duplicate.FieldPath, Is.EqualTo("scenes.Array.data[1].sceneId"));
            Assert.That(duplicate.Message, Does.Contain("scenes.Array.data[0]"));
        }

        [Test]
        public void BindingValidationReportsEveryCoreSceneWithNoBindingEntry()
        {
            var asset = CreateAsset(new[] { B(MenuMain, MenuPath) }, null);
            var surface = Fake();
            surface.RegisteredEnabled.Add(MenuPath);

            var report = SceneBindingValidation.Validate(asset, Graph(), ZeroFadeOptions(), surface);

            var missing = report.Diagnostics.Where(item => item.Code == SceneBindingValidation.BindingMissing).ToList();
            Assert.That(missing.Count, Is.EqualTo(4), "level1.play, level1.audio, level2.play, and fallback.error all lack a binding entry.");
            Assert.That(missing.All(item => item.Source == asset), Is.True);
        }

        [Test]
        public void BindingValidationReportsMissingFadeSurfaceOnlyWhenAFadeDurationIsNonZero()
        {
            var asset = CreateAsset(AllEntries(), null);
            var surface = FullyRegistered();

            var noFadeNeeded = SceneBindingValidation.Validate(asset, Graph(), ZeroFadeOptions(), surface);
            Assert.That(noFadeNeeded.Diagnostics.Any(item => item.Code == SceneBindingValidation.FadeMissing), Is.False);

            var fadeNeeded = SceneBindingValidation.Validate(asset, Graph(), NonZeroOptions(), surface);
            var fadeMissing = fadeNeeded.Diagnostics.Single(item => item.Code == SceneBindingValidation.FadeMissing);
            Assert.That(fadeMissing.FieldPath, Is.EqualTo("fadeSurfaceReference"));
            Assert.That(fadeMissing.Source, Is.EqualTo(asset));

            var fade = CreateFadeAsset();
            var withFade = CreateAsset(AllEntries(), fade);
            var satisfied = SceneBindingValidation.Validate(withFade, Graph(), NonZeroOptions(), surface);
            Assert.That(satisfied.IsValid, Is.True, string.Join(", ", satisfied.Diagnostics.Select(item => item.Code)));
        }

        [Test]
        public void BindingValidationReportsMalformedSceneIdentityFromTheCore()
        {
            var asset = CreateAsset(new[] { B(default, MenuPath) }, null);
            var surface = Fake();

            var report = SceneBindingValidation.Validate(asset, GraphMenu(), ZeroFadeOptions(), surface);

            Assert.That(report.Diagnostics.Any(item => item.Code == SceneFlowFailure.IdentityMalformed), Is.True);
        }

        [Test]
        public void BindingConstructionThrowsWithTheSameReportAndNullAssetReportsAssetMissing()
        {
            var asset = CreateAsset(new[] { B(MenuMain, "") }, null);
            var surface = Fake();
            var report = SceneBindingValidation.Validate(asset, GraphMenu(), ZeroFadeOptions(), surface);
            Assert.That(report.IsValid, Is.False);

            var thrown = Assert.Throws<SceneBindingException>(() => asset.ToBinding(GraphMenu(), ZeroFadeOptions(), surface));

            Assert.That(thrown.Report.Diagnostics.Select(item => item.Code), Is.EqualTo(report.Diagnostics.Select(item => item.Code)));
            Assert.That(thrown.Message, Does.Contain("[scene.unregistered]"));
            Assert.That(SceneBindingValidation.Validate(null, GraphMenu(), ZeroFadeOptions(), surface).Diagnostics.Single().Code, Is.EqualTo(SceneBindingValidation.AssetMissing));
        }

        // ----- runtime: mapping Core phases to engine operations through the loader seam -----

        [Test]
        public void FadingOutDrivesTheBoundFadeSurface()
        {
            var surface = FullyRegistered();
            var fade = CreateFadeAsset();
            var runtime = Runtime(surface, fade, NonZeroOptions(), Level1);

            runtime.Apply(new LoadSetIntent(Level1));

            Assert.That(runtime.State.Phase, Is.EqualTo(TransitionPhase.FadingOut));
            Assert.That(fade.Log, Is.EqualTo(new[] { "out 1" }));
        }

        [Test]
        public void LoadingIssuesHeldAdditiveLoadsForEveryDestinationScene()
        {
            var surface = FullyRegistered();
            var runtime = Runtime(surface, CreateFadeAsset(), NonZeroOptions(), Level1);
            runtime.Apply(new LoadSetIntent(Level1));

            runtime.Apply(new ElapsedTimeIntent(SceneFlowDuration.Create(1f)));

            Assert.That(runtime.State.Phase, Is.EqualTo(TransitionPhase.Loading));
            Assert.That(surface.Log, Is.EquivalentTo(new[] { "load " + Level1Path, "load " + Level1AudioPath }));
            Assert.That(runtime.PendingLoadHandles, Is.EquivalentTo(new[] { Level1Play, Level1Audio }));
        }

        [Test]
        public void PollLoadHandlesForwardsEngineCompletionAsLoadCompletedIntentsNeverInferred()
        {
            var surface = FullyRegistered();
            var runtime = Runtime(surface, CreateFadeAsset(), NonZeroOptions(), Level1);
            runtime.Apply(new LoadSetIntent(Level1));
            runtime.Apply(new ElapsedTimeIntent(SceneFlowDuration.Create(1f)));

            runtime.PollLoadHandles();
            Assert.That(runtime.State.Phase, Is.EqualTo(TransitionPhase.Loading), "Neither fake handle has reported done yet.");

            surface.Handles[Level1Path].IsDone = true;
            runtime.PollLoadHandles();
            Assert.That(runtime.State.Phase, Is.EqualTo(TransitionPhase.Loading), "level1.audio has not reported yet.");

            surface.Handles[Level1AudioPath].IsDone = true;
            runtime.PollLoadHandles();

            Assert.That(runtime.State.Phase, Is.EqualTo(TransitionPhase.Holding));
            Assert.That(runtime.State.LoadedSets, Is.EquivalentTo(new[] { Level1 }));
            Assert.That(runtime.PendingLoadHandles, Is.Empty);
        }

        [Test]
        public void PollLoadHandlesForwardsEngineFailureAsLoadFailedAndRecoversToFallback()
        {
            var surface = FullyRegistered();
            var runtime = Runtime(surface, CreateFadeAsset(), NonZeroOptions(), Level1);
            runtime.Apply(new LoadSetIntent(Level1));
            runtime.Apply(new ElapsedTimeIntent(SceneFlowDuration.Create(1f)));

            surface.Handles[Level1Path].IsDone = true;
            surface.Handles[Level1Path].Succeeded = false;
            surface.Handles[Level1Path].FailureReason = "asset bundle missing";
            surface.Handles[Level1AudioPath].IsDone = true;
            runtime.PollLoadHandles();

            Assert.That(runtime.State.Phase, Is.EqualTo(TransitionPhase.Loading));
            Assert.That(runtime.State.PendingDestination, Is.EqualTo(Fallback));
            Assert.That(runtime.Outcomes.Any(item => item.Accepted && item.Code == SceneFlowFailure.LoadFailed), Is.True);
            Assert.That(surface.Log, Does.Contain("load " + FallbackPath));
        }

        [Test]
        public void HoldingKeepsActivationHeldUntilTheHoldDurationElapses()
        {
            var surface = FullyRegistered();
            var runtime = Runtime(surface, CreateFadeAsset(), NonZeroOptions(), Menu);
            DriveToHolding(runtime, surface, new LoadSetIntent(Menu), MenuPath);

            Assert.That(runtime.State.Phase, Is.EqualTo(TransitionPhase.Holding));
            Assert.That(surface.Log.Any(item => item.StartsWith("active")), Is.False, "Activation is held until holding completes.");

            runtime.Apply(new ElapsedTimeIntent(SceneFlowDuration.Create(0.5f)));
            Assert.That(runtime.State.Phase, Is.EqualTo(TransitionPhase.Holding), "The declared hold duration is one second.");
            Assert.That(surface.Log.Any(item => item.StartsWith("active")), Is.False);
        }

        [Test]
        public void ActivationHappensAtTheEndOfHoldingAndUnloadsFollowActivationThenFadingInDrivesTheSurfaceBack()
        {
            var surface = FullyRegistered();
            var fade = CreateFadeAsset();
            var runtime = Runtime(surface, fade, NonZeroOptions(), Menu, Level1);
            SettleFully(runtime, surface, new LoadSetIntent(Menu), MenuPath);
            fade.Log.Clear();
            surface.Log.Clear();
            DriveToHolding(runtime, surface, new LoadSetIntent(Level1), Level1Path, Level1AudioPath);
            surface.Log.Clear();

            runtime.Apply(new ElapsedTimeIntent(SceneFlowDuration.Create(1f)));

            Assert.That(runtime.State.Phase, Is.EqualTo(TransitionPhase.FadingIn));
            Assert.That(runtime.State.ActiveScene, Is.EqualTo(Level1Play));
            Assert.That(surface.Log, Is.EquivalentTo(new[] { "active " + Level1Path }), "This is a plain load, not a switch, so nothing is unloaded here.");
            Assert.That(fade.Log, Is.EqualTo(new[] { "in 1" }));
            Assert.That(runtime.Diagnostics, Is.Empty);
        }

        [Test]
        public void UnloadsOfTheOriginSetAreIssuedAfterActivationDuringASwitch()
        {
            var surface = FullyRegistered();
            var runtime = Runtime(surface, CreateFadeAsset(), NonZeroOptions(), Level1, Level2);
            SettleFully(runtime, surface, new LoadSetIntent(Level1), Level1Path, Level1AudioPath);
            surface.Log.Clear();

            DriveToHolding(runtime, surface, new SwitchSetIntent(Level1, Level2), Level2Path);
            runtime.Apply(new ElapsedTimeIntent(SceneFlowDuration.Create(1f)));

            var activeIndex = surface.Log.IndexOf("active " + Level2Path);
            var unloadIndex = surface.Log.IndexOf("unload " + Level1Path);
            Assert.That(activeIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(unloadIndex, Is.GreaterThan(activeIndex), "Level1 is unloaded only after Level2 is activated.");
            Assert.That(surface.Log, Does.Not.Contain("unload " + Level1AudioPath), "level1.audio is still a member of the now-loaded level2 set.");
        }

        [Test]
        public void SharedSceneAcrossSetsIsNotUnloadedWhileAnotherLoadedSetStillNeedsIt()
        {
            var surface = FullyRegistered();
            var runtime = Runtime(surface, CreateFadeAsset(), NonZeroOptions(), Level1, Level2);
            SettleFully(runtime, surface, new LoadSetIntent(Level1), Level1Path, Level1AudioPath);
            surface.Log.Clear();

            DriveToHolding(runtime, surface, new SwitchSetIntent(Level1, Level2), Level2Path);
            runtime.Apply(new ElapsedTimeIntent(SceneFlowDuration.Create(1f)));

            Assert.That(surface.Log, Does.Not.Contain("unload " + Level1AudioPath), "level1.audio is still a member of the now-loaded level2 set.");
            Assert.That(surface.Log, Does.Contain("unload " + Level1Path));
        }

        [Test]
        public void RefusedActiveSceneChangeIsReportedAsActiveRefused()
        {
            var surface = FullyRegistered();
            surface.RejectActive.Add(MenuPath);
            var runtime = Runtime(surface, CreateFadeAsset(), NonZeroOptions(), Menu);
            DriveToHolding(runtime, surface, new LoadSetIntent(Menu), MenuPath);

            runtime.Apply(new ElapsedTimeIntent(SceneFlowDuration.Create(1f)));

            var diagnostic = runtime.Diagnostics.Single();
            Assert.That(diagnostic.Code, Is.EqualTo(SceneFlowUnityFailure.ActiveRefused));
            Assert.That(diagnostic.Message, Does.Contain("SetActiveScene").And.Contain(MenuPath));
        }

        [Test]
        public void RefusedUnloadIsReportedAsSceneUnloadFailedInsteadOfSilentSuccess()
        {
            var surface = FullyRegistered();
            surface.RejectUnload.Add(Level1Path);
            var runtime = Runtime(surface, CreateFadeAsset(), NonZeroOptions(), Level1, Level2);
            SettleFully(runtime, surface, new LoadSetIntent(Level1), Level1Path, Level1AudioPath);

            DriveToHolding(runtime, surface, new SwitchSetIntent(Level1, Level2), Level2Path);
            runtime.Apply(new ElapsedTimeIntent(SceneFlowDuration.Create(1f)));

            var diagnostic = runtime.Diagnostics.Single(item => item.Code == SceneFlowUnityFailure.UnloadFailed);
            Assert.That(diagnostic.Message, Does.Contain("Unload").And.Contain(Level1Path));
        }

        // ----- runtime construction and ordering -----

        [Test]
        public void RuntimeForwardsAcceptedIntentsInOrderAndReportsRejectedState()
        {
            var runtime = Runtime(FullyRegistered(), CreateFadeAsset(), NonZeroOptions(), Menu);

            var rejected = runtime.Apply(new UnloadSetIntent(Menu));
            Assert.That(rejected.Accepted, Is.False);
            Assert.That(rejected.Code, Is.EqualTo(SceneFlowFailure.SceneNotLoaded));

            var accepted = runtime.Apply(new LoadSetIntent(Menu));
            Assert.That(accepted.Accepted, Is.True);
            Assert.That(runtime.Outcomes.Select(item => item.Index), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(runtime.Outcomes[0].Accepted, Is.False);
            Assert.That(runtime.Outcomes[1].Accepted, Is.True);
        }

        [Test]
        public void RuntimeConstructionRequiresExplicitReferencesAndAMatchingGraphAndOptions()
        {
            var surface = FullyRegistered();
            var fade = CreateFadeAsset();
            var binding = CreateAsset(AllEntries(), fade).ToBinding(Graph(), NonZeroOptions(), surface);
            // A freshly constructed options instance with the same three durations still
            // matches: TransitionOptions compares by value, not by reference.
            var matching = SceneFlowState.Initial(binding.Graph, NonZeroOptions());
            var foreignGraph = SceneFlowState.Initial(Graph(), NonZeroOptions());
            var foreignOptions = SceneFlowState.Initial(binding.Graph, ZeroFadeOptions());

            Assert.Throws<ArgumentNullException>(() => new SceneFlowRuntime(null, NonZeroOptions(), surface, fade, matching));
            Assert.Throws<ArgumentNullException>(() => new SceneFlowRuntime(binding, null, surface, fade, matching));
            Assert.Throws<ArgumentNullException>(() => new SceneFlowRuntime(binding, NonZeroOptions(), null, fade, matching));
            Assert.Throws<ArgumentNullException>(() => new SceneFlowRuntime(binding, NonZeroOptions(), surface, fade, null));
            Assert.Throws<ArgumentException>(() => new SceneFlowRuntime(binding, NonZeroOptions(), surface, fade, foreignGraph), "A state built over a different graph instance is rejected.");
            Assert.Throws<ArgumentException>(() => new SceneFlowRuntime(binding, NonZeroOptions(), surface, fade, foreignOptions), "A state built over unequal transition options is rejected.");
            Assert.DoesNotThrow(() => new SceneFlowRuntime(binding, NonZeroOptions(), surface, fade, matching));
        }

        // ----- no scene, no shared state -----

        [Test]
        public void TwoRuntimesAreIndependentWithoutSharedStateAndNoStaticInstance()
        {
            var firstSurface = FullyRegistered();
            var secondSurface = FullyRegistered();
            var firstFade = CreateFadeAsset();
            var secondFade = CreateFadeAsset();
            var first = Runtime(firstSurface, firstFade, NonZeroOptions(), Menu);
            var second = Runtime(secondSurface, secondFade, NonZeroOptions(), Menu);

            first.Apply(new LoadSetIntent(Menu));
            first.Apply(new ElapsedTimeIntent(SceneFlowDuration.Create(1f)));

            Assert.That(first.Outcomes.Count, Is.EqualTo(2));
            Assert.That(second.Outcomes, Is.Empty);
            Assert.That(second.State.Phase, Is.EqualTo(TransitionPhase.Idle));
            Assert.That(firstFade.Log, Is.EqualTo(new[] { "out 1" }));
            Assert.That(secondFade.Log, Is.Empty);
            Assert.That(firstSurface.Log, Is.Not.Empty, "The first runtime's load reaches only its own loader surface.");
            Assert.That(secondSurface.Log, Is.Empty);
            Assert.That(ReferenceEquals(first.Binding, second.Binding), Is.False);
            Assert.That(ReferenceEquals(first.State, second.State), Is.False);

            const BindingFlags statics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var type in new[] { typeof(SceneFlowRuntime), typeof(SceneFlowBinding), typeof(UnitySceneLoaderSurface), typeof(SceneFlowBindingAsset) })
            {
                Assert.That(type.GetFields(statics).Where(field => !field.IsLiteral), Is.Empty, type.Name + " must hold no static instance.");
                Assert.That(type.GetProperties(statics), Is.Empty, type.Name + " must expose no static instance.");
            }
        }

        [Test]
        public void RealSurfaceReportsUnregisteredWithoutTheEditorBuildSettingsList()
        {
            var surface = new UnitySceneLoaderSurface();
            // Outside a recognised build-settings path the real surface reports unregistered,
            // and this test never calls SceneManager.LoadScene, so EditMode loads no scene.
            Assert.That(surface.TryGetBuildSceneInfo("Assets/Scenes/DoesNotExist.unity", out var enabled), Is.False);
            Assert.That(enabled, Is.False);
        }

        // ----- helpers -----

        private static SceneGraph Graph()
        {
            var result = new SceneGraphBuilder()
                .Set(Menu, MenuMain, MenuMain)
                .Set(Level1, Level1Play, Level1Play, Level1Audio)
                .Set(Level2, Level2Play, Level2Play, Level1Audio)
                .Set(Fallback, FallbackError, FallbackError)
                .Build(Fallback);
            Assert.That(result.Succeeded, Is.True, result.Message);
            return result.Value;
        }

        private static SceneGraph GraphMenu()
        {
            var result = new SceneGraphBuilder().Set(Menu, MenuMain, MenuMain).Build(Menu);
            Assert.That(result.Succeeded, Is.True, result.Message);
            return result.Value;
        }

        private static SceneGraph GraphLevel1()
        {
            var result = new SceneGraphBuilder()
                .Set(Level1, Level1Play, Level1Play, Level1Audio)
                .Set(Fallback, FallbackError, FallbackError)
                .Build(Fallback);
            Assert.That(result.Succeeded, Is.True, result.Message);
            return result.Value;
        }

        private static TransitionOptions NonZeroOptions() => TransitionOptions.Create(1f, 1f, 1f);
        private static TransitionOptions ZeroFadeOptions() => TransitionOptions.Create(0f, 0f, 0f);

        private static (SceneId Scene, string Path)[] AllEntries() => new[]
        {
            (MenuMain, MenuPath),
            (Level1Play, Level1Path),
            (Level1Audio, Level1AudioPath),
            (Level2Play, Level2Path),
            (FallbackError, FallbackPath),
        };

        private static (SceneId Scene, string Path) B(SceneId scene, string path) => (scene, path);

        private SceneFlowRuntime Runtime(FakeLoaderSurface surface, FakeFadeSurface fade, TransitionOptions options, params SceneSetId[] registeredSets)
        {
            var graph = Graph();
            var binding = CreateAsset(AllEntries(), fade).ToBinding(graph, options, surface);
            return new SceneFlowRuntime(binding, options, surface, fade, SceneFlowState.Initial(graph, options));
        }

        /// <summary>Applies the given start intent, advances past fading_out, and completes every
        /// pending load, leaving the runtime in holding.</summary>
        private static void DriveToHolding(SceneFlowRuntime runtime, FakeLoaderSurface surface, SceneFlowIntent startIntent, params string[] scenePaths)
        {
            runtime.Apply(startIntent);
            runtime.Apply(new ElapsedTimeIntent(SceneFlowDuration.Create(1f)));
            foreach (var path in scenePaths)
            {
                if (surface.Handles.TryGetValue(path, out var handle)) handle.IsDone = true;
            }
            if (scenePaths.Length > 0) runtime.PollLoadHandles();
        }

        /// <summary>Drives a start intent all the way through to idle.</summary>
        private static void SettleFully(SceneFlowRuntime runtime, FakeLoaderSurface surface, SceneFlowIntent startIntent, params string[] scenePaths)
        {
            DriveToHolding(runtime, surface, startIntent, scenePaths);
            runtime.Apply(new ElapsedTimeIntent(SceneFlowDuration.Create(1f)));
            runtime.Apply(new ElapsedTimeIntent(SceneFlowDuration.Create(1f)));
        }

        private FakeLoaderSurface Fake() => new FakeLoaderSurface();

        private FakeLoaderSurface FullyRegistered()
        {
            var surface = new FakeLoaderSurface();
            foreach (var entry in AllEntries()) surface.RegisteredEnabled.Add(entry.Path);
            return surface;
        }

        private SceneFlowBindingAsset CreateAsset((SceneId Scene, string Path)[] entries, FakeFadeSurface fadeSurface)
        {
            var asset = ScriptableObject.CreateInstance<SceneFlowBindingAsset>();
            _created.Add(asset);
            var serialized = new SerializedObject(asset);
            var list = serialized.FindProperty("scenes");
            list.arraySize = entries.Length;
            for (var index = 0; index < entries.Length; index++)
            {
                var element = list.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("sceneId").stringValue = entries[index].Scene.Value ?? string.Empty;
                element.FindPropertyRelative("scenePath").stringValue = entries[index].Path;
            }
            serialized.FindProperty("fadeSurfaceReference").objectReferenceValue = fadeSurface;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private FakeFadeSurface CreateFadeAsset()
        {
            var fade = ScriptableObject.CreateInstance<FakeFadeSurface>();
            _created.Add(fade);
            return fade;
        }

        /// <summary>A ScriptableObject fake so it can be assigned to the asset's UnityEngine.Object field.</summary>
        private sealed class FakeFadeSurface : ScriptableObject, IFadeSurface
        {
            public readonly List<string> Log = new List<string>();
            public void FadeOut(float durationSeconds) => Log.Add("out " + durationSeconds.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            public void FadeIn(float durationSeconds) => Log.Add("in " + durationSeconds.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        }

        private sealed class FakeLoadHandle : ISceneLoadHandle
        {
            public bool IsDone { get; set; }
            public bool Succeeded { get; set; } = true;
            public string FailureReason { get; set; } = string.Empty;
        }

        /// <summary>Mirrors the engine's by-name contract: an unregistered path, a refused
        /// activation, and a refused unload all report failure through a return value.</summary>
        private sealed class FakeLoaderSurface : ISceneLoaderSurface
        {
            public HashSet<string> RegisteredEnabled { get; } = new HashSet<string>(StringComparer.Ordinal);
            public HashSet<string> RegisteredDisabled { get; } = new HashSet<string>(StringComparer.Ordinal);
            public HashSet<string> RejectActive { get; } = new HashSet<string>(StringComparer.Ordinal);
            public HashSet<string> RejectUnload { get; } = new HashSet<string>(StringComparer.Ordinal);
            public Dictionary<string, FakeLoadHandle> Handles { get; } = new Dictionary<string, FakeLoadHandle>(StringComparer.Ordinal);
            public List<string> Log { get; } = new List<string>();

            public bool TryGetBuildSceneInfo(string scenePath, out bool enabled)
            {
                if (RegisteredEnabled.Contains(scenePath)) { enabled = true; return true; }
                if (RegisteredDisabled.Contains(scenePath)) { enabled = false; return true; }
                enabled = false;
                return false;
            }

            public ISceneLoadHandle LoadAdditive(string scenePath)
            {
                Log.Add("load " + scenePath);
                var handle = new FakeLoadHandle();
                Handles[scenePath] = handle;
                return handle;
            }

            public bool Unload(string scenePath)
            {
                Log.Add("unload " + scenePath);
                return !RejectUnload.Contains(scenePath);
            }

            public bool TrySetActiveScene(string scenePath)
            {
                Log.Add("active " + scenePath);
                return !RejectActive.Contains(scenePath);
            }
        }
    }
}
