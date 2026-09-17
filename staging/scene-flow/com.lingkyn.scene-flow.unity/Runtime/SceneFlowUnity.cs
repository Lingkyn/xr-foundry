using System;
using System.Collections.Generic;
using System.Linq;
using Lingkyn.SceneFlow.Core;
using UnityEngine;

namespace Lingkyn.SceneFlow.Unity
{
    // Thin Unity adapter for the Scene Flow Core: a ScriptableObject that binds each
    // Core SceneId to one scene registered in the pinned editor's build scene list;
    // fail-closed binding validation with stable codes, field paths, and the source
    // asset; an injectable ISceneLoaderSurface seam over additive scene loading, with
    // a real wrapper and a test fake so EditMode loads no scene; a plain runtime built
    // from explicit references (graph, bindings, transition options, loader seam, fade
    // surface) that maps Core phases to additive loads, activation, unloads, and a
    // fade surface, forwards engine completion and failure as Core intents, and
    // reports every by-name resolution failure as a diagnostic (LESSON-004). No
    // singleton, no static instance, no DontDestroyOnLoad discovery, no scene search,
    // no reflection discovery.

    /// <summary>The engine calls the loader seam mirrors, behind an interface so an EditMode test
    /// never has to load a real scene.</summary>
    public interface ISceneLoadHandle
    {
        /// <summary>True once the engine has finished attempting the load, success or failure.</summary>
        bool IsDone { get; }
        /// <summary>Meaningful only when <see cref="IsDone"/> is true.</summary>
        bool Succeeded { get; }
        /// <summary>A human reason for a failed load; empty on success.</summary>
        string FailureReason { get; }
    }

    /// <summary>
    /// The engine surface the adapter needs, behind a seam: whether a scene path is registered
    /// and enabled in the pinned editor's build scene list, issuing an additive load with
    /// activation held, unloading a scene by path, and setting the active scene by path. Every
    /// call mirrors an engine API that reports an unknown or incompatible target only through
    /// its return value.
    /// </summary>
    public interface ISceneLoaderSurface
    {
        /// <summary>Mirrors a build-settings lookup: false when the path is not registered at all;
        /// <paramref name="enabled"/> is meaningful only when this returns true.</summary>
        bool TryGetBuildSceneInfo(string scenePath, out bool enabled);

        /// <summary>Mirrors SceneManager.LoadSceneAsync(path, Additive) with activation held.</summary>
        ISceneLoadHandle LoadAdditive(string scenePath);

        /// <summary>Mirrors SceneManager.UnloadSceneAsync: false when the scene is not loaded.</summary>
        bool Unload(string scenePath);

        /// <summary>Mirrors SceneManager.SetActiveScene: false when the scene is not loaded or refused.</summary>
        bool TrySetActiveScene(string scenePath);
    }

    /// <summary>The real handle over a Unity <c>AsyncOperation</c>.</summary>
    public sealed class UnitySceneLoadHandle : ISceneLoadHandle
    {
        private readonly AsyncOperation _operation;

        public UnitySceneLoadHandle(AsyncOperation operation)
        {
            _operation = operation;
        }

        public bool IsDone => _operation == null || _operation.isDone;
        /// <summary>A null operation (an unrecognised path) never completes as loaded.</summary>
        public bool Succeeded => _operation != null && _operation.isDone;
        public string FailureReason => _operation == null ? "SceneManager.LoadSceneAsync returned null." : string.Empty;
    }

    /// <summary>The real surface over <c>SceneManager</c> and, in the Editor, the build scene list.</summary>
    public sealed class UnitySceneLoaderSurface : ISceneLoaderSurface
    {
        public bool TryGetBuildSceneInfo(string scenePath, out bool enabled)
        {
            enabled = false;
            if (string.IsNullOrEmpty(scenePath)) return false;
#if UNITY_EDITOR
            foreach (var scene in UnityEditor.EditorBuildSettings.scenes)
            {
                if (scene != null && scene.path == scenePath)
                {
                    enabled = scene.enabled;
                    return true;
                }
            }
            return false;
#else
            // Outside the Editor there is no build-settings list to query; a player build
            // trusts its own scene paths and reports unregistered only through the load call.
            return false;
#endif
        }

        public ISceneLoadHandle LoadAdditive(string scenePath)
        {
            var operation = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(scenePath, UnityEngine.SceneManagement.LoadSceneMode.Additive);
            if (operation != null) operation.allowSceneActivation = false;
            return new UnitySceneLoadHandle(operation);
        }

        public bool Unload(string scenePath)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);
            if (!scene.IsValid() || !scene.isLoaded) return false;
            UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene);
            return true;
        }

        public bool TrySetActiveScene(string scenePath)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);
            if (!scene.IsValid() || !scene.isLoaded) return false;
            return UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
        }
    }

    /// <summary>The injectable seam a fade renderer implements; the adapter owns no visual vocabulary.</summary>
    public interface IFadeSurface
    {
        void FadeOut(float durationSeconds);
        void FadeIn(float durationSeconds);
    }

    [Serializable]
    public sealed class SceneBindingEntry
    {
        [SerializeField] private string sceneId = string.Empty;
        [SerializeField] private string scenePath = string.Empty;

        public string SceneId => sceneId ?? string.Empty;
        /// <summary>The path under which the scene is registered in the build scene list.</summary>
        public string ScenePath => scenePath ?? string.Empty;
    }

    [CreateAssetMenu(menuName = "Lingkyn/Scene Flow/Scene Binding", fileName = "SceneFlowBinding")]
    public sealed class SceneFlowBindingAsset : ScriptableObject
    {
        [SerializeField] private List<SceneBindingEntry> scenes = new List<SceneBindingEntry>();
        [SerializeField] private UnityEngine.Object fadeSurfaceReference;

        public IReadOnlyList<SceneBindingEntry> Scenes => scenes;
        public UnityEngine.Object FadeSurfaceReference => fadeSurfaceReference;
        /// <summary>Null when no reference is set or the reference does not implement <see cref="IFadeSurface"/>.</summary>
        public IFadeSurface FadeSurface => fadeSurfaceReference as IFadeSurface;

        /// <summary>The real surface; a test supplies a fake instead.</summary>
        public ISceneLoaderSurface CreateSurface() => new UnitySceneLoaderSurface();

        /// <summary>Converts the asset without mutating it; throws with the validation report when invalid.</summary>
        public SceneFlowBinding ToBinding(SceneGraph graph, TransitionOptions options) => SceneFlowBinding.Create(this, graph, options, CreateSurface());

        /// <summary>Converts through an explicit surface, for tests and for hosts that own scene loading elsewhere.</summary>
        public SceneFlowBinding ToBinding(SceneGraph graph, TransitionOptions options, ISceneLoaderSurface surface) => SceneFlowBinding.Create(this, graph, options, surface);
    }

    public sealed class BindingDiagnostic
    {
        public BindingDiagnostic(string code, UnityEngine.Object source, string fieldPath, string message)
        {
            Code = code;
            Source = source;
            FieldPath = fieldPath ?? string.Empty;
            Message = message ?? string.Empty;
        }

        /// <summary>Stable code: scene.unregistered, binding.duplicate, binding.missing, fade.missing,
        /// binding.asset.missing, binding.entry.missing, or a Core identity.malformed/set.unknown code.</summary>
        public string Code { get; }
        public UnityEngine.Object Source { get; }
        public string FieldPath { get; }
        public string Message { get; }
    }

    public sealed class BindingReport
    {
        public BindingReport(IReadOnlyList<BindingDiagnostic> diagnostics)
        {
            Diagnostics = diagnostics;
        }

        public IReadOnlyList<BindingDiagnostic> Diagnostics { get; }
        public bool IsValid => Diagnostics.Count == 0;
    }

    public sealed class SceneBindingException : Exception
    {
        public SceneBindingException(BindingReport report)
            : base(Describe(report))
        {
            Report = report;
        }

        public BindingReport Report { get; }

        private static string Describe(BindingReport report)
        {
            var lines = new List<string>(report.Diagnostics.Count + 1) { "Scene flow binding is invalid:" };
            foreach (var diagnostic in report.Diagnostics)
            {
                lines.Add($"  [{diagnostic.Code}] {diagnostic.FieldPath}: {diagnostic.Message}");
            }
            return string.Join("\n", lines);
        }
    }

    public static class SceneBindingValidation
    {
        public const string SceneUnregistered = "scene.unregistered";
        public const string BindingDuplicate = "binding.duplicate";
        public const string BindingMissing = "binding.missing";
        public const string FadeMissing = "fade.missing";
        public const string AssetMissing = "binding.asset.missing";
        public const string EntryMissing = "binding.entry.missing";

        public static BindingReport Validate(SceneFlowBindingAsset asset, SceneGraph graph, TransitionOptions options, ISceneLoaderSurface surface)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (surface == null) throw new ArgumentNullException(nameof(surface));
            var diagnostics = new List<BindingDiagnostic>();
            if (asset == null)
            {
                diagnostics.Add(new BindingDiagnostic(AssetMissing, null, string.Empty, "The binding asset is null."));
                return new BindingReport(diagnostics);
            }

            var declaredScenes = new SortedSet<SceneId>();
            foreach (var set in graph.Sets)
            {
                foreach (var scene in set.Members) declaredScenes.Add(scene);
            }

            var bound = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var index = 0; index < asset.Scenes.Count; index++)
            {
                var entry = asset.Scenes[index];
                var path = $"scenes.Array.data[{index}]";
                if (entry == null)
                {
                    diagnostics.Add(new BindingDiagnostic(EntryMissing, asset, path, "A scene binding entry is empty."));
                    continue;
                }
                var sceneId = SceneId.TryCreate(entry.SceneId);
                if (!sceneId.Succeeded)
                {
                    diagnostics.Add(new BindingDiagnostic(sceneId.Code, asset, path + ".sceneId", sceneId.Message));
                    continue;
                }
                if (bound.TryGetValue(sceneId.Value.Value, out var first))
                {
                    diagnostics.Add(new BindingDiagnostic(BindingDuplicate, asset, path + ".sceneId", $"Scene '{sceneId.Value}' is already bound by scenes.Array.data[{first}]."));
                }
                else
                {
                    bound[sceneId.Value.Value] = index;
                }
                var scenePath = entry.ScenePath;
                if (scenePath.Length == 0)
                {
                    diagnostics.Add(new BindingDiagnostic(SceneUnregistered, asset, path + ".scenePath", "No scene path is set."));
                }
                else if (!surface.TryGetBuildSceneInfo(scenePath, out var enabled) || !enabled)
                {
                    diagnostics.Add(new BindingDiagnostic(SceneUnregistered, asset, path + ".scenePath", $"Scene path '{scenePath}' is missing from the build scene list or disabled in it."));
                }
            }

            foreach (var scene in declaredScenes)
            {
                if (!bound.ContainsKey(scene.Value))
                {
                    diagnostics.Add(new BindingDiagnostic(BindingMissing, asset, "scenes", $"Core scene '{scene}' has no binding entry."));
                }
            }

            var needsFadeSurface = !options.FadeOut.IsZero || !options.FadeIn.IsZero;
            if (needsFadeSurface && asset.FadeSurface == null)
            {
                diagnostics.Add(new BindingDiagnostic(FadeMissing, asset, "fadeSurfaceReference", "A non-zero fade-out or fade-in duration is declared but no bound reference implements IFadeSurface."));
            }

            return new BindingReport(diagnostics);
        }
    }

    /// <summary>An immutable, validated binding between one scene graph and one set of scene paths.</summary>
    public sealed class SceneFlowBinding
    {
        private readonly Dictionary<SceneId, string> _scenePaths;

        private SceneFlowBinding(SceneGraph graph, IFadeSurface fadeSurface, Dictionary<SceneId, string> scenePaths)
        {
            Graph = graph;
            FadeSurface = fadeSurface;
            _scenePaths = scenePaths;
        }

        public SceneGraph Graph { get; }
        public IFadeSurface FadeSurface { get; }
        public int SceneCount => _scenePaths.Count;

        public bool TryGetScenePath(SceneId scene, out string scenePath) => _scenePaths.TryGetValue(scene, out scenePath);

        public static SceneFlowBinding Create(SceneFlowBindingAsset asset, SceneGraph graph, TransitionOptions options, ISceneLoaderSurface surface)
        {
            var report = SceneBindingValidation.Validate(asset, graph, options, surface);
            if (!report.IsValid) throw new SceneBindingException(report);
            var scenePaths = new Dictionary<SceneId, string>();
            foreach (var entry in asset.Scenes)
            {
                scenePaths[SceneId.Parse(entry.SceneId)] = entry.ScenePath;
            }
            return new SceneFlowBinding(graph, asset.FadeSurface, scenePaths);
        }
    }

    /// <summary>An explicit report of a by-name or optional resolution that did not succeed (LESSON-004).</summary>
    public sealed class SceneFlowDiagnostic
    {
        public SceneFlowDiagnostic(string code, SceneFlowIntent intent, string message)
        {
            Code = code;
            Intent = intent;
            Message = message ?? string.Empty;
        }

        /// <summary>Stable code: scene.unbound, active.refused, scene.unload_failed.</summary>
        public string Code { get; }
        public SceneFlowIntent Intent { get; }
        public string Message { get; }
    }

    /// <summary>Stable codes for by-name resolution failures reported only at runtime, never at binding validation.</summary>
    public static class SceneFlowUnityFailure
    {
        /// <summary>An accepted load for a scene with no bound scene path.</summary>
        public const string SceneUnbound = "scene.unbound";
        /// <summary>The engine refused the active-scene change at the end of holding.</summary>
        public const string ActiveRefused = "active.refused";
        /// <summary>The engine refused to unload a scene of the set being left.</summary>
        public const string UnloadFailed = "scene.unload_failed";
    }

    /// <summary>
    /// Owns one <see cref="SceneFlowState"/> over one binding, built from explicit references
    /// (the binding's graph, the binding itself, the transition options, the loader seam, and
    /// the fade surface). The Core decides whether an intent is accepted and drives the phase
    /// machine; this runtime maps each phase entry to an engine operation through the loader
    /// seam: fading_out drives the fade surface, loading issues held additive loads for the
    /// destination's scenes, activation and the origin's unloads happen once holding completes,
    /// and fading_in drives the fade surface back. Engine completion and failure are forwarded
    /// to the Core only through <see cref="PollLoadHandles"/>, never inferred. Plain class: a
    /// consumer's composition root decides its lifetime, and two runtimes share nothing.
    /// </summary>
    public sealed class SceneFlowRuntime
    {
        private readonly List<SceneFlowIntentOutcome> _outcomes = new List<SceneFlowIntentOutcome>();
        private readonly List<SceneFlowDiagnostic> _diagnostics = new List<SceneFlowDiagnostic>();
        private readonly Dictionary<SceneId, ISceneLoadHandle> _pendingLoads = new Dictionary<SceneId, ISceneLoadHandle>();

        public SceneFlowRuntime(SceneFlowBinding binding, TransitionOptions options, ISceneLoaderSurface surface, IFadeSurface fadeSurface, SceneFlowState initialState)
        {
            Binding = binding ?? throw new ArgumentNullException(nameof(binding));
            Options = options ?? throw new ArgumentNullException(nameof(options));
            Surface = surface ?? throw new ArgumentNullException(nameof(surface));
            FadeSurface = fadeSurface;
            State = initialState ?? throw new ArgumentNullException(nameof(initialState));
            if (!ReferenceEquals(initialState.Graph, binding.Graph))
            {
                throw new ArgumentException("The initial state must be built over the binding's scene graph.", nameof(initialState));
            }
            if (!initialState.Options.Equals(options))
            {
                throw new ArgumentException("The initial state must be built over transition options equal to these.", nameof(options));
            }
        }

        public SceneFlowBinding Binding { get; }
        public TransitionOptions Options { get; }
        public ISceneLoaderSurface Surface { get; }
        public IFadeSurface FadeSurface { get; }
        public SceneFlowState State { get; private set; }

        /// <summary>Every intent this runtime saw, accepted or rejected, in order.</summary>
        public IReadOnlyList<SceneFlowIntentOutcome> Outcomes => _outcomes;
        public IReadOnlyList<SceneFlowDiagnostic> Diagnostics => _diagnostics;
        /// <summary>Scenes with a load issued and not yet reported complete or failed.</summary>
        public IReadOnlyCollection<SceneId> PendingLoadHandles => _pendingLoads.Keys;

        public SceneFlowIntentOutcome Apply(SceneFlowIntent intent)
        {
            if (intent == null) throw new ArgumentNullException(nameof(intent));
            var index = _outcomes.Count;
            var previous = State;
            var result = State.Apply(intent);
            if (!result.Succeeded)
            {
                var rejected = new SceneFlowIntentOutcome(index, intent, false, result.Code, result.Message);
                _outcomes.Add(rejected);
                return rejected;
            }
            State = result.Value;
            var accepted = new SceneFlowIntentOutcome(index, intent, true, result.Code, result.Message);
            _outcomes.Add(accepted);
            React(previous, State, intent);
            return accepted;
        }

        /// <summary>Forwards every finished load handle to the Core as load-completed or load-failed;
        /// engine completion is read from the handle, never inferred. A no-op outside loading.</summary>
        public void PollLoadHandles()
        {
            if (State.Phase != TransitionPhase.Loading || _pendingLoads.Count == 0) return;
            foreach (var scene in new List<SceneId>(_pendingLoads.Keys))
            {
                // A load-failed intent processed earlier in this same loop can recover to the
                // fallback set and prune this scene's stale handle out from under us.
                if (!_pendingLoads.TryGetValue(scene, out var handle)) continue;
                if (!handle.IsDone) continue;
                _pendingLoads.Remove(scene);
                if (handle.Succeeded) Apply(new SceneLoadCompletedIntent(scene));
                else Apply(new SceneLoadFailedIntent(scene, handle.FailureReason));
                if (State.Phase != TransitionPhase.Loading) return;
            }
        }

        private void React(SceneFlowState previous, SceneFlowState current, SceneFlowIntent intent)
        {
            if (previous.Phase != TransitionPhase.FadingOut && current.Phase == TransitionPhase.FadingOut)
            {
                FadeSurface?.FadeOut(Options.FadeOut.Seconds);
            }
            var enteredLoading = previous.Phase != TransitionPhase.Loading && current.Phase == TransitionPhase.Loading;
            // A load-failed intent can recover to the fallback set without leaving the loading
            // phase at all (LESSON-004: the recovery must still issue real loads, not be inferred).
            var recoveredToFallbackWithinLoading = current.Phase == TransitionPhase.Loading && current.PendingDestination != previous.PendingDestination;
            if (enteredLoading || recoveredToFallbackWithinLoading)
            {
                IssueLoads(current, intent);
            }
            if (previous.Phase == TransitionPhase.Holding && current.Phase == TransitionPhase.FadingIn)
            {
                ActivateAndUnloadOrigin(previous, current, intent);
                FadeSurface?.FadeIn(Options.FadeIn.Seconds);
            }
        }

        private void IssueLoads(SceneFlowState current, SceneFlowIntent intent)
        {
            var remaining = new HashSet<SceneId>(current.PendingScenesRemaining);
            foreach (var stale in _pendingLoads.Keys.Where(scene => !remaining.Contains(scene)).ToList())
            {
                _pendingLoads.Remove(stale);
            }
            foreach (var scene in current.PendingScenesRemaining)
            {
                if (_pendingLoads.ContainsKey(scene)) continue;
                if (!Binding.TryGetScenePath(scene, out var scenePath))
                {
                    _diagnostics.Add(new SceneFlowDiagnostic(SceneFlowUnityFailure.SceneUnbound, intent, $"Scene '{scene}' was accepted for loading but no scene path is bound to it."));
                    continue;
                }
                _pendingLoads[scene] = Surface.LoadAdditive(scenePath);
            }
        }

        private void ActivateAndUnloadOrigin(SceneFlowState previous, SceneFlowState current, SceneFlowIntent intent)
        {
            if (current.ActiveScene.HasValue && current.ActiveScene != previous.ActiveScene
                && Binding.TryGetScenePath(current.ActiveScene.Value, out var activePath))
            {
                if (!Surface.TrySetActiveScene(activePath))
                {
                    _diagnostics.Add(new SceneFlowDiagnostic(SceneFlowUnityFailure.ActiveRefused, intent, $"SetActiveScene('{activePath}') returned false; the active scene was not changed."));
                }
            }
            if (!previous.PendingOrigin.HasValue || !Binding.Graph.TryGetSet(previous.PendingOrigin.Value, out var originDefinition))
            {
                return;
            }
            var stillNeeded = new HashSet<SceneId>(current.LoadedScenes);
            foreach (var scene in originDefinition.Members)
            {
                if (stillNeeded.Contains(scene)) continue; // shared with another loaded set; keep it loaded
                _pendingLoads.Remove(scene);
                if (!Binding.TryGetScenePath(scene, out var scenePath))
                {
                    _diagnostics.Add(new SceneFlowDiagnostic(SceneFlowUnityFailure.SceneUnbound, intent, $"Scene '{scene}' of unloaded set '{previous.PendingOrigin}' has no bound scene path."));
                    continue;
                }
                if (!Surface.Unload(scenePath))
                {
                    _diagnostics.Add(new SceneFlowDiagnostic(SceneFlowUnityFailure.UnloadFailed, intent, $"Unload('{scenePath}') returned false for scene '{scene}'."));
                }
            }
        }
    }
}
