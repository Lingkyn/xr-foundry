using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Lingkyn.SceneFlow.Core
{
    // Engine-light scene flow core: stable scene set and scene identity, an immutable
    // declared scene graph with one active scene per set and one fallback set, typed
    // non-negative transition durations, load/unload/switch intents applied to an
    // immutable flow state, a transition state machine advanced only by explicit
    // elapsed-time, load-completed, and load-failed intents, fail-closed error
    // recovery to the declared fallback set, deterministic replay with a fingerprint,
    // and structured results with stable failure codes. No UnityEngine dependency, no
    // I/O, no clock, no coroutine or task, no rendering. Time and load completion
    // enter only as intents.

    /// <summary>Stable machine codes carried by every <see cref="SceneFlowResult{T}"/>.</summary>
    public static class SceneFlowFailure
    {
        public const string None = "";
        public const string IdentityMalformed = "identity.malformed";
        public const string SetDuplicate = "set.duplicate";
        public const string SceneDuplicate = "scene.duplicate";
        public const string ActiveMissing = "active.missing";
        public const string SetUnknown = "set.unknown";
        public const string SceneNotLoaded = "scene.not_loaded";
        public const string TransitionInProgress = "transition.in_progress";
        public const string TransitionIllegal = "transition.illegal";
        public const string LoadFailed = "load.failed";
        public const string FallbackFailed = "fallback.failed";
        /// <summary>Not part of the family's headline code list; carried by a malformed or negative
        /// transition duration, the same way Audio carries snapshot.duration.invalid.</summary>
        public const string DurationInvalid = "duration.invalid";
    }

    /// <summary>Structured outcome: a value on success, a stable code and a human message on failure.
    /// A successful outcome ordinarily carries an empty code; the error-recovery clauses use
    /// <see cref="OkWithNote"/> for an accepted intent that also records an informational code
    /// such as load.failed or fallback.failed.</summary>
    public readonly struct SceneFlowResult<T>
    {
        private SceneFlowResult(bool succeeded, T value, string code, string message)
        {
            Succeeded = succeeded;
            Value = value;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public bool Succeeded { get; }
        public T Value { get; }
        /// <summary>Empty on a plain success. Non-empty on a rejection, and on an accepted intent
        /// that records an informational recovery event.</summary>
        public string Code { get; }
        public string Message { get; }

        public static SceneFlowResult<T> Ok(T value) => new SceneFlowResult<T>(true, value, SceneFlowFailure.None, string.Empty);

        public static SceneFlowResult<T> OkWithNote(T value, string code, string message)
        {
            if (string.IsNullOrEmpty(code)) throw new ArgumentException("A note needs a stable code.", nameof(code));
            return new SceneFlowResult<T>(true, value, code, message);
        }

        public static SceneFlowResult<T> Fail(string code, string message)
        {
            if (string.IsNullOrEmpty(code)) throw new ArgumentException("A failure needs a stable code.", nameof(code));
            return new SceneFlowResult<T>(false, default, code, message);
        }

        public SceneFlowResult<TOther> As<TOther>()
        {
            if (Succeeded) throw new InvalidOperationException("Only a failed result can be re-typed.");
            return SceneFlowResult<TOther>.Fail(Code, Message);
        }
    }

    /// <summary>
    /// One canonical form for every identity: trimmed of leading and trailing whitespace,
    /// non-empty after trimming, and free of any interior whitespace or control character.
    /// Two identities with the same canonical text compare equal.
    /// </summary>
    public static class SceneFlowIdentity
    {
        public static SceneFlowResult<string> TryCanonicalize(string value, string kind)
        {
            if (value == null)
            {
                return SceneFlowResult<string>.Fail(SceneFlowFailure.IdentityMalformed, $"A {kind} id must not be null.");
            }
            var trimmed = value.Trim();
            if (trimmed.Length == 0)
            {
                return SceneFlowResult<string>.Fail(SceneFlowFailure.IdentityMalformed, $"A {kind} id must not be empty.");
            }
            foreach (var character in trimmed)
            {
                if (char.IsWhiteSpace(character) || char.IsControl(character))
                {
                    return SceneFlowResult<string>.Fail(SceneFlowFailure.IdentityMalformed, $"The {kind} id '{value}' contains whitespace or a control character.");
                }
            }
            return SceneFlowResult<string>.Ok(trimmed);
        }
    }

    /// <summary>Stable identity of a declared scene set, separate from any scene asset or build index.</summary>
    public readonly struct SceneSetId : IEquatable<SceneSetId>, IComparable<SceneSetId>
    {
        private SceneSetId(string value) { Value = value; }

        public string Value { get; }

        public static SceneFlowResult<SceneSetId> TryCreate(string value)
        {
            var canonical = SceneFlowIdentity.TryCanonicalize(value, "scene set");
            return canonical.Succeeded ? SceneFlowResult<SceneSetId>.Ok(new SceneSetId(canonical.Value)) : canonical.As<SceneSetId>();
        }

        public static SceneSetId Parse(string value)
        {
            var result = TryCreate(value);
            if (!result.Succeeded) throw new ArgumentException(result.Message, nameof(value));
            return result.Value;
        }

        public bool Equals(SceneSetId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SceneSetId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public int CompareTo(SceneSetId other) => string.CompareOrdinal(Value, other.Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(SceneSetId left, SceneSetId right) => left.Equals(right);
        public static bool operator !=(SceneSetId left, SceneSetId right) => !left.Equals(right);
    }

    /// <summary>Stable identity of a declared scene, separate from any scene asset, path, or build index.</summary>
    public readonly struct SceneId : IEquatable<SceneId>, IComparable<SceneId>
    {
        private SceneId(string value) { Value = value; }

        public string Value { get; }

        public static SceneFlowResult<SceneId> TryCreate(string value)
        {
            var canonical = SceneFlowIdentity.TryCanonicalize(value, "scene");
            return canonical.Succeeded ? SceneFlowResult<SceneId>.Ok(new SceneId(canonical.Value)) : canonical.As<SceneId>();
        }

        public static SceneId Parse(string value)
        {
            var result = TryCreate(value);
            if (!result.Succeeded) throw new ArgumentException(result.Message, nameof(value));
            return result.Value;
        }

        public bool Equals(SceneId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SceneId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public int CompareTo(SceneId other) => string.CompareOrdinal(Value, other.Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(SceneId left, SceneId right) => left.Equals(right);
        public static bool operator !=(SceneId left, SceneId right) => !left.Equals(right);
    }

    /// <summary>
    /// A non-negative transition duration. Zero means the phase it governs completes on the
    /// tick that enters it; a negative or unparseable value is rejected before any intent is
    /// applied, with duration.invalid.
    /// </summary>
    public readonly struct SceneFlowDuration : IEquatable<SceneFlowDuration>, IComparable<SceneFlowDuration>
    {
        private SceneFlowDuration(float seconds) { Seconds = seconds; }

        public float Seconds { get; }
        public bool IsZero => Seconds == 0f;

        public static SceneFlowResult<SceneFlowDuration> TryCreate(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds))
            {
                return SceneFlowResult<SceneFlowDuration>.Fail(SceneFlowFailure.DurationInvalid, $"A transition duration must be finite; '{seconds}' is not.");
            }
            if (seconds < 0f)
            {
                return SceneFlowResult<SceneFlowDuration>.Fail(SceneFlowFailure.DurationInvalid, $"A transition duration must be non-negative; {Text(seconds)} is negative.");
            }
            return SceneFlowResult<SceneFlowDuration>.Ok(new SceneFlowDuration(seconds));
        }

        public static SceneFlowResult<SceneFlowDuration> TryParse(string text)
        {
            if (text == null || !float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
            {
                return SceneFlowResult<SceneFlowDuration>.Fail(SceneFlowFailure.DurationInvalid, $"'{text}' does not parse as a transition duration.");
            }
            return TryCreate(seconds);
        }

        public static SceneFlowDuration Create(float seconds)
        {
            var result = TryCreate(seconds);
            if (!result.Succeeded) throw new ArgumentException(result.Message, nameof(seconds));
            return result.Value;
        }

        public bool Equals(SceneFlowDuration other) => Seconds.Equals(other.Seconds);
        public override bool Equals(object obj) => obj is SceneFlowDuration other && Equals(other);
        public override int GetHashCode() => Seconds.GetHashCode();
        public int CompareTo(SceneFlowDuration other) => Seconds.CompareTo(other.Seconds);
        public override string ToString() => Text(Seconds);
        public static bool operator ==(SceneFlowDuration left, SceneFlowDuration right) => left.Equals(right);
        public static bool operator !=(SceneFlowDuration left, SceneFlowDuration right) => !left.Equals(right);

        private static string Text(float value) => value.ToString("R", CultureInfo.InvariantCulture);
    }

    /// <summary>The declared fade-out, hold, and fade-in durations for every transition. Two
    /// instances with the same three durations are equal values, so a runtime built with a
    /// freshly constructed options instance still matches a state built with an equal one.</summary>
    public sealed class TransitionOptions : IEquatable<TransitionOptions>
    {
        private TransitionOptions(SceneFlowDuration fadeOut, SceneFlowDuration hold, SceneFlowDuration fadeIn)
        {
            FadeOut = fadeOut;
            Hold = hold;
            FadeIn = fadeIn;
        }

        public SceneFlowDuration FadeOut { get; }
        public SceneFlowDuration Hold { get; }
        public SceneFlowDuration FadeIn { get; }

        public bool Equals(TransitionOptions other) =>
            other != null && FadeOut.Equals(other.FadeOut) && Hold.Equals(other.Hold) && FadeIn.Equals(other.FadeIn);

        public override bool Equals(object obj) => obj is TransitionOptions other && Equals(other);
        public override int GetHashCode() => (FadeOut.GetHashCode() * 397) ^ (Hold.GetHashCode() * 31) ^ FadeIn.GetHashCode();

        public static SceneFlowResult<TransitionOptions> TryCreate(float fadeOutSeconds, float holdSeconds, float fadeInSeconds)
        {
            var fadeOut = SceneFlowDuration.TryCreate(fadeOutSeconds);
            if (!fadeOut.Succeeded) return fadeOut.As<TransitionOptions>();
            var hold = SceneFlowDuration.TryCreate(holdSeconds);
            if (!hold.Succeeded) return hold.As<TransitionOptions>();
            var fadeIn = SceneFlowDuration.TryCreate(fadeInSeconds);
            if (!fadeIn.Succeeded) return fadeIn.As<TransitionOptions>();
            return SceneFlowResult<TransitionOptions>.Ok(new TransitionOptions(fadeOut.Value, hold.Value, fadeIn.Value));
        }

        public static TransitionOptions Create(float fadeOutSeconds, float holdSeconds, float fadeInSeconds)
        {
            var result = TryCreate(fadeOutSeconds, holdSeconds, fadeInSeconds);
            if (!result.Succeeded) throw new ArgumentException(result.Message);
            return result.Value;
        }
    }

    /// <summary>One declared set: its member scenes and the one member that is active while the set is current.</summary>
    public sealed class SceneSetDefinition
    {
        private readonly SceneId[] _members;

        internal SceneSetDefinition(SceneSetId id, SceneId active, SceneId[] members)
        {
            Id = id;
            Active = active;
            _members = members;
        }

        public SceneSetId Id { get; }
        public SceneId Active { get; }
        public IReadOnlyList<SceneId> Members => _members;

        public bool Contains(SceneId scene) => Array.IndexOf(_members, scene) >= 0;
    }

    /// <summary>
    /// The immutable declared scene graph: unique set ids, no scene declared twice within one
    /// set (a scene may belong to more than one set), an active scene that is a member of its
    /// own set, and one declared fallback set.
    /// </summary>
    public sealed class SceneGraph
    {
        private readonly SortedDictionary<SceneSetId, SceneSetDefinition> _sets;

        private SceneGraph(SortedDictionary<SceneSetId, SceneSetDefinition> sets, SceneSetId fallbackSet)
        {
            _sets = sets;
            FallbackSet = fallbackSet;
        }

        public SceneSetId FallbackSet { get; }
        public IEnumerable<SceneSetDefinition> Sets => _sets.Values;
        public int SetCount => _sets.Count;

        public bool TryGetSet(SceneSetId id, out SceneSetDefinition set) => _sets.TryGetValue(id, out set);

        internal static SceneFlowResult<SceneGraph> Build(List<SceneSetDefinition> sets, SceneSetId fallbackSet)
        {
            var map = new SortedDictionary<SceneSetId, SceneSetDefinition>();
            foreach (var set in sets)
            {
                if (map.ContainsKey(set.Id))
                {
                    return SceneFlowResult<SceneGraph>.Fail(SceneFlowFailure.SetDuplicate, $"Set '{set.Id}' is declared more than once.");
                }
                var seen = new HashSet<SceneId>();
                foreach (var scene in set.Members)
                {
                    if (!seen.Add(scene))
                    {
                        return SceneFlowResult<SceneGraph>.Fail(SceneFlowFailure.SceneDuplicate, $"Set '{set.Id}' declares scene '{scene}' more than once.");
                    }
                }
                if (!set.Contains(set.Active))
                {
                    return SceneFlowResult<SceneGraph>.Fail(SceneFlowFailure.ActiveMissing, $"Set '{set.Id}' declares active scene '{set.Active}' which is absent or not a member.");
                }
                map[set.Id] = set;
            }
            if (!map.ContainsKey(fallbackSet))
            {
                return SceneFlowResult<SceneGraph>.Fail(SceneFlowFailure.SetUnknown, $"Fallback set '{fallbackSet}' is not declared.");
            }
            return SceneFlowResult<SceneGraph>.Ok(new SceneGraph(map, fallbackSet));
        }
    }

    /// <summary>Collects declarations; <see cref="Build"/> validates them and produces an immutable graph.</summary>
    public sealed class SceneGraphBuilder
    {
        private readonly List<SceneSetDefinition> _sets = new List<SceneSetDefinition>();

        public SceneGraphBuilder Set(SceneSetId id, SceneId active, params SceneId[] members)
        {
            _sets.Add(new SceneSetDefinition(id, active, members == null ? Array.Empty<SceneId>() : (SceneId[])members.Clone()));
            return this;
        }

        public SceneFlowResult<SceneGraph> Build(SceneSetId fallbackSet) => SceneGraph.Build(new List<SceneSetDefinition>(_sets), fallbackSet);
    }

    /// <summary>The closed set of phases a transition passes through, advanced only by explicit intents.</summary>
    public enum TransitionPhase
    {
        Idle,
        FadingOut,
        Loading,
        Holding,
        FadingIn,
    }

    /// <summary>
    /// Closed set of intents. Each is applied to a <see cref="SceneFlowState"/> and either yields
    /// a new state (optionally carrying an informational recovery code) or a structured failure
    /// that leaves the prior state untouched.
    /// </summary>
    public abstract class SceneFlowIntent
    {
        private protected SceneFlowIntent() { }

        public abstract string Describe();

        internal abstract SceneFlowResult<SceneFlowState> ApplyTo(SceneFlowState state);

        public override string ToString() => Describe();
    }

    public sealed class LoadSetIntent : SceneFlowIntent
    {
        public LoadSetIntent(SceneSetId set) { Set = set; }

        public SceneSetId Set { get; }

        public override string Describe() => $"load {Set}";

        internal override SceneFlowResult<SceneFlowState> ApplyTo(SceneFlowState state) => state.ApplyLoad(Set);
    }

    public sealed class UnloadSetIntent : SceneFlowIntent
    {
        public UnloadSetIntent(SceneSetId set) { Set = set; }

        public SceneSetId Set { get; }

        public override string Describe() => $"unload {Set}";

        internal override SceneFlowResult<SceneFlowState> ApplyTo(SceneFlowState state) => state.ApplyUnload(Set);
    }

    public sealed class SwitchSetIntent : SceneFlowIntent
    {
        public SwitchSetIntent(SceneSetId origin, SceneSetId destination)
        {
            Origin = origin;
            Destination = destination;
        }

        public SceneSetId Origin { get; }
        public SceneSetId Destination { get; }

        public override string Describe() => $"switch {Origin} -> {Destination}";

        internal override SceneFlowResult<SceneFlowState> ApplyTo(SceneFlowState state) => state.ApplySwitch(Origin, Destination);
    }

    /// <summary>Advances the current phase's elapsed time; the only intent that carries a duration.</summary>
    public sealed class ElapsedTimeIntent : SceneFlowIntent
    {
        public ElapsedTimeIntent(SceneFlowDuration elapsed) { Elapsed = elapsed; }

        public SceneFlowDuration Elapsed { get; }

        public override string Describe() => $"elapsed {Elapsed}s";

        internal override SceneFlowResult<SceneFlowState> ApplyTo(SceneFlowState state) => state.ApplyElapsed(Elapsed);
    }

    /// <summary>Reports that one scene of the destination set finished loading; never inferred, only forwarded.</summary>
    public sealed class SceneLoadCompletedIntent : SceneFlowIntent
    {
        public SceneLoadCompletedIntent(SceneId scene) { Scene = scene; }

        public SceneId Scene { get; }

        public override string Describe() => $"load-completed {Scene}";

        internal override SceneFlowResult<SceneFlowState> ApplyTo(SceneFlowState state) => state.ApplyLoadCompleted(Scene);
    }

    /// <summary>Reports that one scene of the destination set failed to load; drives fallback recovery.</summary>
    public sealed class SceneLoadFailedIntent : SceneFlowIntent
    {
        public SceneLoadFailedIntent(SceneId scene, string reason)
        {
            Scene = scene;
            Reason = reason ?? string.Empty;
        }

        public SceneId Scene { get; }
        public string Reason { get; }

        public override string Describe() => $"load-failed {Scene} ({Reason})";

        internal override SceneFlowResult<SceneFlowState> ApplyTo(SceneFlowState state) => state.ApplyLoadFailed(Scene, Reason);
    }

    /// <summary>The outcome of one intent inside a sequence.</summary>
    public sealed class SceneFlowIntentOutcome
    {
        public SceneFlowIntentOutcome(int index, SceneFlowIntent intent, bool accepted, string code, string message)
        {
            Index = index;
            Intent = intent;
            Accepted = accepted;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public int Index { get; }
        public SceneFlowIntent Intent { get; }
        public bool Accepted { get; }
        /// <summary>Empty for a plain accepted intent or a stable code otherwise: a rejection code
        /// when <see cref="Accepted"/> is false, or an informational recovery code (load.failed,
        /// fallback.failed) when it is true.</summary>
        public string Code { get; }
        public string Message { get; }
    }

    /// <summary>The state after a sequence and one outcome per intent. A rejected intent keeps the state it found.</summary>
    public sealed class SceneFlowSequenceResult
    {
        public SceneFlowSequenceResult(SceneFlowState state, IReadOnlyList<SceneFlowIntentOutcome> outcomes)
        {
            State = state;
            Outcomes = outcomes;
        }

        public SceneFlowState State { get; }
        public IReadOnlyList<SceneFlowIntentOutcome> Outcomes { get; }

        public bool AllAccepted
        {
            get
            {
                foreach (var outcome in Outcomes)
                {
                    if (!outcome.Accepted) return false;
                }
                return true;
            }
        }

        public int AcceptedCount
        {
            get
            {
                var count = 0;
                foreach (var outcome in Outcomes)
                {
                    if (outcome.Accepted) count++;
                }
                return count;
            }
        }

        public int RejectedCount => Outcomes.Count - AcceptedCount;
    }

    /// <summary>
    /// Immutable scene flow state over one graph and one set of transition options: the current
    /// transition phase, the phase's elapsed time, the committed loaded sets and scenes, the
    /// active scene, and (mid-transition) the pending origin, destination, and scenes still
    /// being awaited. Every mutation returns a new state; the same intent sequence from the same
    /// initial state yields an equal state and an equal fingerprint.
    /// </summary>
    public sealed class SceneFlowState : IEquatable<SceneFlowState>
    {
        private readonly SortedSet<SceneSetId> _loadedSets;
        private readonly SortedSet<SceneId> _pendingScenesRemaining;

        private SceneFlowState(
            SceneGraph graph,
            TransitionOptions options,
            TransitionPhase phase,
            float phaseElapsedSeconds,
            SortedSet<SceneSetId> loadedSets,
            SceneId? activeScene,
            SceneSetId? pendingOrigin,
            SceneSetId? pendingDestination,
            SortedSet<SceneId> pendingScenesRemaining)
        {
            Graph = graph;
            Options = options;
            Phase = phase;
            PhaseElapsedSeconds = phaseElapsedSeconds;
            _loadedSets = loadedSets;
            ActiveScene = activeScene;
            PendingOrigin = pendingOrigin;
            PendingDestination = pendingDestination;
            _pendingScenesRemaining = pendingScenesRemaining;
        }

        public SceneGraph Graph { get; }
        public TransitionOptions Options { get; }
        public TransitionPhase Phase { get; }
        public float PhaseElapsedSeconds { get; }
        public SceneId? ActiveScene { get; }
        /// <summary>The set being unloaded by the in-flight transition, if any.</summary>
        public SceneSetId? PendingOrigin { get; }
        /// <summary>The set being loaded by the in-flight transition, if any.</summary>
        public SceneSetId? PendingDestination { get; }

        public IReadOnlyList<SceneSetId> LoadedSets => new List<SceneSetId>(_loadedSets);

        /// <summary>The union of member scenes of every currently loaded set.</summary>
        public IReadOnlyList<SceneId> LoadedScenes
        {
            get
            {
                var scenes = new SortedSet<SceneId>();
                foreach (var setId in _loadedSets)
                {
                    if (Graph.TryGetSet(setId, out var definition))
                    {
                        foreach (var scene in definition.Members) scenes.Add(scene);
                    }
                }
                return new List<SceneId>(scenes);
            }
        }

        /// <summary>The scenes of the pending destination not yet reported loaded; empty outside <see cref="TransitionPhase.Loading"/>.</summary>
        public IReadOnlyList<SceneId> PendingScenesRemaining => _pendingScenesRemaining == null ? Array.Empty<SceneId>() : new List<SceneId>(_pendingScenesRemaining);

        /// <summary>The initial state: idle, nothing loaded, no active scene.</summary>
        public static SceneFlowState Initial(SceneGraph graph, TransitionOptions options)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (options == null) throw new ArgumentNullException(nameof(options));
            return new SceneFlowState(graph, options, TransitionPhase.Idle, 0f, new SortedSet<SceneSetId>(), null, null, null, null);
        }

        public bool IsLoaded(SceneSetId set) => _loadedSets.Contains(set);

        public SceneFlowResult<SceneFlowState> Apply(SceneFlowIntent intent)
        {
            if (intent == null) throw new ArgumentNullException(nameof(intent));
            return intent.ApplyTo(this);
        }

        /// <summary>Applies every intent in order; a rejected intent is recorded and the state it found is kept for the next one.</summary>
        public SceneFlowSequenceResult ApplyAll(IEnumerable<SceneFlowIntent> intents)
        {
            if (intents == null) throw new ArgumentNullException(nameof(intents));
            var state = this;
            var outcomes = new List<SceneFlowIntentOutcome>();
            var index = 0;
            foreach (var intent in intents)
            {
                var result = state.Apply(intent);
                if (result.Succeeded)
                {
                    state = result.Value;
                    outcomes.Add(new SceneFlowIntentOutcome(index, intent, true, result.Code, result.Message));
                }
                else
                {
                    outcomes.Add(new SceneFlowIntentOutcome(index, intent, false, result.Code, result.Message));
                }
                index++;
            }
            return new SceneFlowSequenceResult(state, outcomes);
        }

        internal SceneFlowResult<SceneFlowState> ApplyLoad(SceneSetId set)
        {
            if (Phase != TransitionPhase.Idle)
            {
                return SceneFlowResult<SceneFlowState>.Fail(SceneFlowFailure.TransitionInProgress, $"Cannot load '{set}' while a transition is in phase {Phase}.");
            }
            if (!Graph.TryGetSet(set, out _))
            {
                return SceneFlowResult<SceneFlowState>.Fail(SceneFlowFailure.SetUnknown, $"Set '{set}' is not declared in the scene graph.");
            }
            return SceneFlowResult<SceneFlowState>.Ok(StartTransition(null, set));
        }

        internal SceneFlowResult<SceneFlowState> ApplyUnload(SceneSetId set)
        {
            if (Phase != TransitionPhase.Idle)
            {
                return SceneFlowResult<SceneFlowState>.Fail(SceneFlowFailure.TransitionInProgress, $"Cannot unload '{set}' while a transition is in phase {Phase}.");
            }
            if (!Graph.TryGetSet(set, out _))
            {
                return SceneFlowResult<SceneFlowState>.Fail(SceneFlowFailure.SetUnknown, $"Set '{set}' is not declared in the scene graph.");
            }
            if (!_loadedSets.Contains(set))
            {
                return SceneFlowResult<SceneFlowState>.Fail(SceneFlowFailure.SceneNotLoaded, $"Set '{set}' is not loaded.");
            }
            return SceneFlowResult<SceneFlowState>.Ok(StartTransition(set, null));
        }

        internal SceneFlowResult<SceneFlowState> ApplySwitch(SceneSetId origin, SceneSetId destination)
        {
            if (Phase != TransitionPhase.Idle)
            {
                return SceneFlowResult<SceneFlowState>.Fail(SceneFlowFailure.TransitionInProgress, $"Cannot switch '{origin}' -> '{destination}' while a transition is in phase {Phase}.");
            }
            if (!Graph.TryGetSet(destination, out _))
            {
                return SceneFlowResult<SceneFlowState>.Fail(SceneFlowFailure.SetUnknown, $"Destination set '{destination}' is not declared in the scene graph.");
            }
            if (!Graph.TryGetSet(origin, out _))
            {
                return SceneFlowResult<SceneFlowState>.Fail(SceneFlowFailure.SetUnknown, $"Origin set '{origin}' is not declared in the scene graph.");
            }
            if (!_loadedSets.Contains(origin))
            {
                return SceneFlowResult<SceneFlowState>.Fail(SceneFlowFailure.SceneNotLoaded, $"Origin set '{origin}' is not loaded.");
            }
            return SceneFlowResult<SceneFlowState>.Ok(StartTransition(origin, destination));
        }

        internal SceneFlowResult<SceneFlowState> ApplyElapsed(SceneFlowDuration elapsed)
        {
            switch (Phase)
            {
                case TransitionPhase.FadingOut:
                {
                    var total = PhaseElapsedSeconds + elapsed.Seconds;
                    return SceneFlowResult<SceneFlowState>.Ok(total >= Options.FadeOut.Seconds ? EnterLoading() : WithElapsed(total));
                }
                case TransitionPhase.Holding:
                {
                    var total = PhaseElapsedSeconds + elapsed.Seconds;
                    return SceneFlowResult<SceneFlowState>.Ok(total >= Options.Hold.Seconds ? CompleteHolding() : WithElapsed(total));
                }
                case TransitionPhase.FadingIn:
                {
                    var total = PhaseElapsedSeconds + elapsed.Seconds;
                    return SceneFlowResult<SceneFlowState>.Ok(total >= Options.FadeIn.Seconds ? EnterIdle() : WithElapsed(total));
                }
                default:
                    return SceneFlowResult<SceneFlowState>.Fail(SceneFlowFailure.TransitionIllegal, $"An elapsed-time intent is not valid during phase {Phase}.");
            }
        }

        internal SceneFlowResult<SceneFlowState> ApplyLoadCompleted(SceneId scene)
        {
            if (Phase != TransitionPhase.Loading)
            {
                return SceneFlowResult<SceneFlowState>.Fail(SceneFlowFailure.TransitionIllegal, $"A load-completed intent is not valid during phase {Phase}.");
            }
            if (_pendingScenesRemaining == null || !_pendingScenesRemaining.Contains(scene))
            {
                return SceneFlowResult<SceneFlowState>.Fail(SceneFlowFailure.TransitionIllegal, $"Scene '{scene}' is not being loaded by the current transition.");
            }
            var remaining = new SortedSet<SceneId>(_pendingScenesRemaining);
            remaining.Remove(scene);
            var updated = new SceneFlowState(Graph, Options, TransitionPhase.Loading, 0f, _loadedSets, ActiveScene, PendingOrigin, PendingDestination, remaining);
            return SceneFlowResult<SceneFlowState>.Ok(remaining.Count == 0 ? updated.EnterHolding() : updated);
        }

        internal SceneFlowResult<SceneFlowState> ApplyLoadFailed(SceneId scene, string reason)
        {
            if (Phase != TransitionPhase.Loading)
            {
                return SceneFlowResult<SceneFlowState>.Fail(SceneFlowFailure.TransitionIllegal, $"A load-failed intent is not valid during phase {Phase}.");
            }
            if (PendingDestination.HasValue && PendingDestination.Value == Graph.FallbackSet)
            {
                var reverted = new SceneFlowState(Graph, Options, TransitionPhase.Idle, 0f, _loadedSets, ActiveScene, null, null, null);
                return SceneFlowResult<SceneFlowState>.OkWithNote(reverted, SceneFlowFailure.FallbackFailed, $"Scene '{scene}' ({reason}) failed while loading the fallback set '{Graph.FallbackSet}'; returning to idle without looping.");
            }
            var fallbackState = new SceneFlowState(Graph, Options, TransitionPhase.Loading, 0f, _loadedSets, ActiveScene, PendingOrigin, PendingDestination, _pendingScenesRemaining);
            var recovered = fallbackState.EnterLoadingSet(Graph.FallbackSet);
            return SceneFlowResult<SceneFlowState>.OkWithNote(recovered, SceneFlowFailure.LoadFailed, $"Scene '{scene}' ({reason}) failed to load; recovering to fallback set '{Graph.FallbackSet}' from phase {Phase}.");
        }

        private SceneFlowState StartTransition(SceneSetId? origin, SceneSetId? destination)
        {
            var state = new SceneFlowState(Graph, Options, TransitionPhase.FadingOut, 0f, _loadedSets, ActiveScene, origin, destination, null);
            return Options.FadeOut.IsZero ? state.EnterLoading() : state;
        }

        private SceneFlowState EnterLoading() => EnterLoadingSet(PendingDestination);

        private SceneFlowState EnterLoadingSet(SceneSetId? destination)
        {
            var members = destination.HasValue && Graph.TryGetSet(destination.Value, out var definition)
                ? definition.Members
                : (IReadOnlyList<SceneId>)Array.Empty<SceneId>();
            var remaining = new SortedSet<SceneId>(members);
            var state = new SceneFlowState(Graph, Options, TransitionPhase.Loading, 0f, _loadedSets, ActiveScene, PendingOrigin, destination, remaining);
            return remaining.Count == 0 ? state.EnterHolding() : state;
        }

        private SceneFlowState EnterHolding()
        {
            var loadedSets = PendingDestination.HasValue ? WithAdded(_loadedSets, PendingDestination.Value) : _loadedSets;
            var state = new SceneFlowState(Graph, Options, TransitionPhase.Holding, 0f, loadedSets, ActiveScene, PendingOrigin, PendingDestination, null);
            return Options.Hold.IsZero ? state.CompleteHolding() : state;
        }

        private SceneFlowState CompleteHolding()
        {
            var activeScene = ActiveScene;
            if (PendingDestination.HasValue && Graph.TryGetSet(PendingDestination.Value, out var definition))
            {
                activeScene = definition.Active;
            }
            var loadedSets = PendingOrigin.HasValue ? WithRemoved(_loadedSets, PendingOrigin.Value) : _loadedSets;
            var state = new SceneFlowState(Graph, Options, TransitionPhase.FadingIn, 0f, loadedSets, activeScene, PendingOrigin, PendingDestination, null);
            return Options.FadeIn.IsZero ? state.EnterIdle() : state;
        }

        private SceneFlowState EnterIdle() =>
            new SceneFlowState(Graph, Options, TransitionPhase.Idle, 0f, _loadedSets, ActiveScene, null, null, null);

        private SceneFlowState WithElapsed(float elapsedSeconds) =>
            new SceneFlowState(Graph, Options, Phase, elapsedSeconds, _loadedSets, ActiveScene, PendingOrigin, PendingDestination, _pendingScenesRemaining);

        private static SortedSet<SceneSetId> WithAdded(SortedSet<SceneSetId> sets, SceneSetId set)
        {
            var copy = new SortedSet<SceneSetId>(sets) { set };
            return copy;
        }

        private static SortedSet<SceneSetId> WithRemoved(SortedSet<SceneSetId> sets, SceneSetId set)
        {
            var copy = new SortedSet<SceneSetId>(sets);
            copy.Remove(set);
            return copy;
        }

        /// <summary>A canonical text of the whole state; equal states have equal fingerprints.</summary>
        public string Fingerprint()
        {
            var builder = new StringBuilder();
            builder.Append("phase[").Append(Phase).Append("] elapsed[").Append(PhaseElapsedSeconds.ToString("R", CultureInfo.InvariantCulture));
            builder.Append("] loaded[").Append(string.Join(",", _loadedSets));
            builder.Append("] active[").Append(ActiveScene.HasValue ? ActiveScene.Value.Value : "-");
            builder.Append("] origin[").Append(PendingOrigin.HasValue ? PendingOrigin.Value.Value : "-");
            builder.Append("] destination[").Append(PendingDestination.HasValue ? PendingDestination.Value.Value : "-");
            builder.Append("] remaining[").Append(_pendingScenesRemaining == null ? "-" : string.Join(",", _pendingScenesRemaining));
            builder.Append(']');
            return builder.ToString();
        }

        public bool Equals(SceneFlowState other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return ReferenceEquals(Graph, other.Graph)
                   && Options.Equals(other.Options)
                   && string.Equals(Fingerprint(), other.Fingerprint(), StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is SceneFlowState other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Fingerprint());
        public override string ToString() => Fingerprint();
    }
}
