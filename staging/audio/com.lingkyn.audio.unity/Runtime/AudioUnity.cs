using System;
using System.Collections.Generic;
using System.Globalization;
using Lingkyn.Audio.Core;
using UnityEngine;
using UnityEngine.Audio;

namespace Lingkyn.Audio.Unity
{
    // Thin Unity adapter for the Audio Core: a ScriptableObject that binds bus,
    // parameter, and snapshot identity to an AudioMixer, its exposed parameter names,
    // and its snapshots; fail-closed binding validation with stable codes; a mixer
    // surface seam that wraps the engine's by-name calls; and a plain runtime built
    // from explicit references that pushes accepted intents to the mixer and reports
    // every by-name failure (LESSON-004). No singleton, no static instance, no scene
    // search, no reflection, no playback, no clip.

    [Serializable]
    public sealed class ParameterBindingEntry
    {
        [SerializeField] private string busId = string.Empty;
        [SerializeField] private string parameterId = string.Empty;
        [SerializeField] private string exposedName = string.Empty;

        public string BusId => busId ?? string.Empty;
        public string ParameterId => parameterId ?? string.Empty;
        /// <summary>The name under which the mixer exposes the parameter to script.</summary>
        public string ExposedName => exposedName ?? string.Empty;
    }

    [Serializable]
    public sealed class SnapshotBindingEntry
    {
        [SerializeField] private string snapshotId = string.Empty;
        [SerializeField] private AudioMixerSnapshot snapshot;
        [SerializeField] private string snapshotName = string.Empty;

        public string SnapshotId => snapshotId ?? string.Empty;
        /// <summary>Direct reference to the mixer snapshot; preferred when authoring in the Editor.</summary>
        public AudioMixerSnapshot Snapshot => snapshot;
        /// <summary>Fallback name used when no reference is set, for example in a test with a fake surface.</summary>
        public string SnapshotName => snapshotName ?? string.Empty;

        /// <summary>The mixer snapshot name the runtime resolves: the reference's name when set, else the fallback.</summary>
        public string EffectiveName => snapshot != null ? snapshot.name : SnapshotName;
    }

    /// <summary>
    /// The engine calls the adapter needs, behind a seam. The real implementation wraps an
    /// <see cref="AudioMixer"/>; a test can substitute a fake because the engine offers
    /// no way to create a mixer or a snapshot from script.
    /// </summary>
    public interface IMixerSurface
    {
        /// <summary>False when the mixer reference is missing or has been destroyed.</summary>
        bool IsPresent { get; }

        /// <summary>Mirrors AudioMixer.GetFloat: false when the name is not an exposed parameter.</summary>
        bool TryGetFloat(string exposedName, out float value);

        /// <summary>Mirrors AudioMixer.SetFloat: false when the name is not an exposed parameter.</summary>
        bool TrySetFloat(string exposedName, float value);

        /// <summary>Mirrors AudioMixer.FindSnapshot(name) != null.</summary>
        bool HasSnapshot(string snapshotName);

        /// <summary>Mirrors AudioMixer.FindSnapshot(name)?.TransitionTo(duration): false when the lookup fails.</summary>
        bool TryTransitionToSnapshot(string snapshotName, float durationSeconds);
    }

    /// <summary>The real surface over an <see cref="AudioMixer"/>. Every call is by name and reports failure through its return value.</summary>
    public sealed class AudioMixerSurface : IMixerSurface
    {
        private readonly AudioMixer _mixer;

        public AudioMixerSurface(AudioMixer mixer)
        {
            _mixer = mixer;
        }

        public bool IsPresent => _mixer != null;

        public bool TryGetFloat(string exposedName, out float value)
        {
            value = 0f;
            if (!IsPresent || string.IsNullOrEmpty(exposedName)) return false;
            return _mixer.GetFloat(exposedName, out value);
        }

        public bool TrySetFloat(string exposedName, float value)
        {
            if (!IsPresent || string.IsNullOrEmpty(exposedName)) return false;
            return _mixer.SetFloat(exposedName, value);
        }

        public bool HasSnapshot(string snapshotName)
        {
            if (!IsPresent || string.IsNullOrEmpty(snapshotName)) return false;
            return _mixer.FindSnapshot(snapshotName) != null;
        }

        public bool TryTransitionToSnapshot(string snapshotName, float durationSeconds)
        {
            if (!IsPresent || string.IsNullOrEmpty(snapshotName)) return false;
            var snapshot = _mixer.FindSnapshot(snapshotName);
            if (snapshot == null) return false;
            snapshot.TransitionTo(durationSeconds);
            return true;
        }
    }

    [CreateAssetMenu(menuName = "Lingkyn/Audio/Mixer Binding", fileName = "AudioMixerBinding")]
    public sealed class AudioMixerBindingAsset : ScriptableObject
    {
        [SerializeField] private AudioMixer mixer;
        [SerializeField] private List<ParameterBindingEntry> parameters = new List<ParameterBindingEntry>();
        [SerializeField] private List<SnapshotBindingEntry> snapshots = new List<SnapshotBindingEntry>();

        public AudioMixer Mixer => mixer;
        public IReadOnlyList<ParameterBindingEntry> Parameters => parameters;
        public IReadOnlyList<SnapshotBindingEntry> Snapshots => snapshots;

        /// <summary>The real surface over the referenced mixer; present only when the reference is set.</summary>
        public IMixerSurface CreateSurface() => new AudioMixerSurface(mixer);

        /// <summary>Converts the asset without mutating it; throws with the validation report when invalid.</summary>
        public AudioMixerBinding ToBinding(MixGraph graph) => AudioMixerBinding.Create(this, graph, CreateSurface());

        /// <summary>Converts through an explicit surface, for tests and for hosts that own the mixer elsewhere.</summary>
        public AudioMixerBinding ToBinding(MixGraph graph, IMixerSurface surface) => AudioMixerBinding.Create(this, graph, surface);
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

        /// <summary>Stable code: mixer.missing, mixer.parameter.unexposed, mixer.parameter.kind.mismatch, mixer.snapshot.missing, binding.duplicate, binding.asset.missing, binding.entry.missing, identity.malformed, bus.unknown, parameter.unknown, snapshot.unknown.</summary>
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

    public sealed class AudioBindingException : Exception
    {
        public AudioBindingException(BindingReport report)
            : base(Describe(report))
        {
            Report = report;
        }

        public BindingReport Report { get; }

        private static string Describe(BindingReport report)
        {
            var lines = new List<string>(report.Diagnostics.Count + 1) { "Audio mixer binding is invalid:" };
            foreach (var diagnostic in report.Diagnostics)
            {
                lines.Add($"  [{diagnostic.Code}] {diagnostic.FieldPath}: {diagnostic.Message}");
            }
            return string.Join("\n", lines);
        }
    }

    public static class AudioBindingValidation
    {
        public const string MixerMissing = "mixer.missing";
        public const string ParameterUnexposed = "mixer.parameter.unexposed";
        public const string ParameterKindMismatch = "mixer.parameter.kind.mismatch";
        public const string SnapshotMissing = "mixer.snapshot.missing";
        public const string BindingDuplicate = "binding.duplicate";
        public const string AssetMissing = "binding.asset.missing";
        public const string EntryMissing = "binding.entry.missing";

        /// <summary>True for the kinds an exposed float parameter can carry: Float directly, Bool as 1 or 0.</summary>
        public static bool IsRepresentable(ParameterKind kind) => kind == ParameterKind.Float || kind == ParameterKind.Bool;

        public static BindingReport Validate(AudioMixerBindingAsset asset, MixGraph graph, IMixerSurface surface)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (surface == null) throw new ArgumentNullException(nameof(surface));
            var diagnostics = new List<BindingDiagnostic>();
            if (asset == null)
            {
                diagnostics.Add(new BindingDiagnostic(AssetMissing, null, string.Empty, "The binding asset is null."));
                return new BindingReport(diagnostics);
            }
            var mixerPresent = surface.IsPresent;
            if (!mixerPresent)
            {
                diagnostics.Add(new BindingDiagnostic(MixerMissing, asset, "mixer", "No AudioMixer is referenced, so no exposed parameter or snapshot can be resolved."));
            }

            var parameterKeys = new Dictionary<string, int>(StringComparer.Ordinal);
            var exposedNames = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var index = 0; index < asset.Parameters.Count; index++)
            {
                var entry = asset.Parameters[index];
                var path = $"parameters.Array.data[{index}]";
                if (entry == null)
                {
                    diagnostics.Add(new BindingDiagnostic(EntryMissing, asset, path, "A parameter binding entry is empty."));
                    continue;
                }
                var bus = BusId.TryCreate(entry.BusId);
                if (!bus.Succeeded)
                {
                    diagnostics.Add(new BindingDiagnostic(bus.Code, asset, path + ".busId", bus.Message));
                }
                var parameter = ParameterId.TryCreate(entry.ParameterId);
                if (!parameter.Succeeded)
                {
                    diagnostics.Add(new BindingDiagnostic(parameter.Code, asset, path + ".parameterId", parameter.Message));
                }
                if (bus.Succeeded && parameter.Succeeded)
                {
                    var contract = graph.ResolveParameter(bus.Value, parameter.Value);
                    if (!contract.Succeeded)
                    {
                        var field = contract.Code == AudioFailure.BusUnknown ? ".busId" : ".parameterId";
                        diagnostics.Add(new BindingDiagnostic(contract.Code, asset, path + field, contract.Message));
                    }
                    else if (!IsRepresentable(contract.Value.Kind))
                    {
                        diagnostics.Add(new BindingDiagnostic(ParameterKindMismatch, asset, path + ".parameterId", $"Parameter '{bus.Value}/{parameter.Value}' is {contract.Value.Kind}; an exposed mixer parameter carries only Float or Bool."));
                    }
                    var key = bus.Value.Value + "|" + parameter.Value.Value;
                    if (parameterKeys.TryGetValue(key, out var first))
                    {
                        diagnostics.Add(new BindingDiagnostic(BindingDuplicate, asset, path + ".parameterId", $"Parameter '{bus.Value}/{parameter.Value}' is already bound by parameters.Array.data[{first}]."));
                    }
                    else
                    {
                        parameterKeys[key] = index;
                    }
                }
                var exposedName = entry.ExposedName;
                if (exposedName.Length == 0)
                {
                    diagnostics.Add(new BindingDiagnostic(ParameterUnexposed, asset, path + ".exposedName", "No exposed parameter name is set."));
                }
                else
                {
                    if (mixerPresent && !surface.TryGetFloat(exposedName, out _))
                    {
                        diagnostics.Add(new BindingDiagnostic(ParameterUnexposed, asset, path + ".exposedName", $"The mixer exposes no parameter named '{exposedName}' (AudioMixer.GetFloat returned false)."));
                    }
                    if (exposedNames.TryGetValue(exposedName, out var first))
                    {
                        diagnostics.Add(new BindingDiagnostic(BindingDuplicate, asset, path + ".exposedName", $"Exposed parameter '{exposedName}' is already bound by parameters.Array.data[{first}]."));
                    }
                    else
                    {
                        exposedNames[exposedName] = index;
                    }
                }
            }

            var snapshotKeys = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var index = 0; index < asset.Snapshots.Count; index++)
            {
                var entry = asset.Snapshots[index];
                var path = $"snapshots.Array.data[{index}]";
                if (entry == null)
                {
                    diagnostics.Add(new BindingDiagnostic(EntryMissing, asset, path, "A snapshot binding entry is empty."));
                    continue;
                }
                var snapshot = SnapshotId.TryCreate(entry.SnapshotId);
                if (!snapshot.Succeeded)
                {
                    diagnostics.Add(new BindingDiagnostic(snapshot.Code, asset, path + ".snapshotId", snapshot.Message));
                }
                else
                {
                    if (!graph.TryGetSnapshot(snapshot.Value, out _))
                    {
                        diagnostics.Add(new BindingDiagnostic(AudioFailure.SnapshotUnknown, asset, path + ".snapshotId", $"Snapshot '{snapshot.Value}' is not in the mix graph."));
                    }
                    if (snapshotKeys.TryGetValue(snapshot.Value.Value, out var first))
                    {
                        diagnostics.Add(new BindingDiagnostic(BindingDuplicate, asset, path + ".snapshotId", $"Snapshot '{snapshot.Value}' is already bound by snapshots.Array.data[{first}]."));
                    }
                    else
                    {
                        snapshotKeys[snapshot.Value.Value] = index;
                    }
                }
                var name = entry.EffectiveName;
                if (name.Length == 0)
                {
                    diagnostics.Add(new BindingDiagnostic(SnapshotMissing, asset, path + ".snapshot", "No AudioMixerSnapshot reference or fallback name is set."));
                }
                else if (mixerPresent && !surface.HasSnapshot(name))
                {
                    diagnostics.Add(new BindingDiagnostic(SnapshotMissing, asset, path + ".snapshot", $"The mixer has no snapshot named '{name}' (AudioMixer.FindSnapshot returned null)."));
                }
            }

            return new BindingReport(diagnostics);
        }
    }

    /// <summary>One validated bus parameter to exposed-name mapping.</summary>
    public sealed class ParameterBinding
    {
        internal ParameterBinding(BusId bus, ParameterId parameter, ParameterKind kind, string exposedName)
        {
            Bus = bus;
            Parameter = parameter;
            Kind = kind;
            ExposedName = exposedName;
        }

        public BusId Bus { get; }
        public ParameterId Parameter { get; }
        public ParameterKind Kind { get; }
        public string ExposedName { get; }
    }

    /// <summary>One validated snapshot id to mixer snapshot name mapping.</summary>
    public sealed class SnapshotBinding
    {
        internal SnapshotBinding(SnapshotId snapshot, string mixerSnapshotName)
        {
            Snapshot = snapshot;
            MixerSnapshotName = mixerSnapshotName;
        }

        public SnapshotId Snapshot { get; }
        public string MixerSnapshotName { get; }
    }

    /// <summary>An immutable, validated binding between one graph and one mixer surface.</summary>
    public sealed class AudioMixerBinding
    {
        private readonly Dictionary<string, ParameterBinding> _parameters;
        private readonly Dictionary<SnapshotId, SnapshotBinding> _snapshots;

        private AudioMixerBinding(MixGraph graph, IMixerSurface surface, Dictionary<string, ParameterBinding> parameters, Dictionary<SnapshotId, SnapshotBinding> snapshots)
        {
            Graph = graph;
            Surface = surface;
            _parameters = parameters;
            _snapshots = snapshots;
        }

        public MixGraph Graph { get; }
        public IMixerSurface Surface { get; }
        public IEnumerable<ParameterBinding> Parameters => _parameters.Values;
        public IEnumerable<SnapshotBinding> Snapshots => _snapshots.Values;
        public int ParameterCount => _parameters.Count;
        public int SnapshotCount => _snapshots.Count;

        public static AudioMixerBinding Create(AudioMixerBindingAsset asset, MixGraph graph, IMixerSurface surface)
        {
            var report = AudioBindingValidation.Validate(asset, graph, surface);
            if (!report.IsValid) throw new AudioBindingException(report);
            var parameters = new Dictionary<string, ParameterBinding>(StringComparer.Ordinal);
            foreach (var entry in asset.Parameters)
            {
                var bus = BusId.Parse(entry.BusId);
                var parameter = ParameterId.Parse(entry.ParameterId);
                var contract = graph.ResolveParameter(bus, parameter).Value;
                parameters[Key(bus, parameter)] = new ParameterBinding(bus, parameter, contract.Kind, entry.ExposedName);
            }
            var snapshots = new Dictionary<SnapshotId, SnapshotBinding>();
            foreach (var entry in asset.Snapshots)
            {
                var snapshot = SnapshotId.Parse(entry.SnapshotId);
                snapshots[snapshot] = new SnapshotBinding(snapshot, entry.EffectiveName);
            }
            return new AudioMixerBinding(graph, surface, parameters, snapshots);
        }

        public bool TryGetParameter(BusId bus, ParameterId parameter, out ParameterBinding binding) =>
            _parameters.TryGetValue(Key(bus, parameter), out binding);

        public bool TryGetSnapshot(SnapshotId snapshot, out SnapshotBinding binding) =>
            _snapshots.TryGetValue(snapshot, out binding);

        /// <summary>Float passes through; Bool becomes 1 or 0. Enumerated values never reach a binding.</summary>
        public static float ToMixerValue(ParameterValue value)
        {
            switch (value.Kind)
            {
                case ParameterKind.Float: return value.Number;
                case ParameterKind.Bool: return value.Flag ? 1f : 0f;
                default: throw new InvalidOperationException($"An {value.Kind} value cannot be written to an exposed mixer parameter.");
            }
        }

        private static string Key(BusId bus, ParameterId parameter) => bus.Value + "|" + parameter.Value;
    }

    /// <summary>An explicit report of a by-name mixer call that did not succeed (LESSON-004).</summary>
    public sealed class MixerDiagnostic
    {
        public MixerDiagnostic(string code, AudioIntent intent, string message)
        {
            Code = code;
            Intent = intent;
            Message = message ?? string.Empty;
        }

        /// <summary>Stable code: mixer.missing, mixer.parameter.unexposed, mixer.parameter.unbound, mixer.snapshot.missing.</summary>
        public string Code { get; }
        public AudioIntent Intent { get; }
        public string Message { get; }
    }

    public sealed class MixerParameterWrite
    {
        public MixerParameterWrite(BusId bus, ParameterId parameter, string exposedName, float value)
        {
            Bus = bus;
            Parameter = parameter;
            ExposedName = exposedName;
            Value = value;
        }

        public BusId Bus { get; }
        public ParameterId Parameter { get; }
        public string ExposedName { get; }
        public float Value { get; }
    }

    public sealed class MixerSnapshotTransition
    {
        public MixerSnapshotTransition(SnapshotId snapshot, string mixerSnapshotName, float durationSeconds)
        {
            Snapshot = snapshot;
            MixerSnapshotName = mixerSnapshotName;
            DurationSeconds = durationSeconds;
        }

        public SnapshotId Snapshot { get; }
        public string MixerSnapshotName { get; }
        public float DurationSeconds { get; }
    }

    /// <summary>
    /// Owns one <see cref="AudioState"/> over one binding. The Core decides whether an
    /// intent is accepted; this runtime then pushes accepted parameter values to the
    /// mixer in acceptance order and transitions snapshots with the intent's explicit
    /// duration. Every by-name call that fails becomes a <see cref="MixerDiagnostic"/>;
    /// the runtime never reports a mixer write it did not confirm. Plain class: a
    /// consumer's composition root decides its lifetime, and two runtimes share nothing.
    /// </summary>
    public sealed class AudioMixerRuntime
    {
        public const string ParameterUnbound = "mixer.parameter.unbound";

        private readonly List<AudioIntentOutcome> _outcomes = new List<AudioIntentOutcome>();
        private readonly List<MixerDiagnostic> _diagnostics = new List<MixerDiagnostic>();
        private readonly List<MixerParameterWrite> _writes = new List<MixerParameterWrite>();
        private readonly List<MixerSnapshotTransition> _transitions = new List<MixerSnapshotTransition>();

        public AudioMixerRuntime(AudioMixerBinding binding, AudioState initialState)
        {
            Binding = binding ?? throw new ArgumentNullException(nameof(binding));
            State = initialState ?? throw new ArgumentNullException(nameof(initialState));
            if (!ReferenceEquals(initialState.Graph, binding.Graph))
            {
                throw new ArgumentException("The initial state must be built over the binding's mix graph.", nameof(initialState));
            }
        }

        public AudioMixerBinding Binding { get; }
        public AudioState State { get; private set; }

        /// <summary>Every intent this runtime saw, accepted or rejected, in order.</summary>
        public IReadOnlyList<AudioIntentOutcome> Outcomes => _outcomes;
        public IReadOnlyList<MixerDiagnostic> Diagnostics => _diagnostics;
        /// <summary>Confirmed mixer writes, in acceptance order.</summary>
        public IReadOnlyList<MixerParameterWrite> Writes => _writes;
        /// <summary>Confirmed snapshot transitions, in acceptance order.</summary>
        public IReadOnlyList<MixerSnapshotTransition> Transitions => _transitions;

        public int AcceptedCount
        {
            get
            {
                var count = 0;
                foreach (var outcome in _outcomes)
                {
                    if (outcome.Accepted) count++;
                }
                return count;
            }
        }

        public int RejectedCount => _outcomes.Count - AcceptedCount;

        public AudioIntentOutcome Apply(AudioIntent intent)
        {
            if (intent == null) throw new ArgumentNullException(nameof(intent));
            var index = _outcomes.Count;
            var result = State.Apply(intent);
            AudioIntentOutcome outcome;
            if (!result.Succeeded)
            {
                outcome = new AudioIntentOutcome(index, intent, false, result.Code, result.Message);
                _outcomes.Add(outcome);
                return outcome;
            }
            State = result.Value;
            outcome = new AudioIntentOutcome(index, intent, true, AudioFailure.None, string.Empty);
            _outcomes.Add(outcome);
            if (intent is SetParameterIntent set) Push(set);
            else if (intent is TransitionSnapshotIntent transition) Transition(transition);
            return outcome;
        }

        /// <summary>Applies each intent in order and returns the outcomes of this call only.</summary>
        public AudioSequenceResult ApplyAll(IEnumerable<AudioIntent> intents)
        {
            if (intents == null) throw new ArgumentNullException(nameof(intents));
            var outcomes = new List<AudioIntentOutcome>();
            foreach (var intent in intents)
            {
                outcomes.Add(Apply(intent));
            }
            return new AudioSequenceResult(State, outcomes);
        }

        private void Push(SetParameterIntent set)
        {
            if (!Binding.TryGetParameter(set.Bus, set.Parameter, out var binding))
            {
                _diagnostics.Add(new MixerDiagnostic(ParameterUnbound, set, $"Parameter '{set.Bus}/{set.Parameter}' was accepted but no exposed mixer parameter is bound to it."));
                return;
            }
            if (!Binding.Surface.IsPresent)
            {
                _diagnostics.Add(new MixerDiagnostic(AudioBindingValidation.MixerMissing, set, $"The mixer is missing, so '{binding.ExposedName}' was not written."));
                return;
            }
            var value = AudioMixerBinding.ToMixerValue(set.Value);
            if (!Binding.Surface.TrySetFloat(binding.ExposedName, value))
            {
                _diagnostics.Add(new MixerDiagnostic(AudioBindingValidation.ParameterUnexposed, set, $"AudioMixer.SetFloat('{binding.ExposedName}', {value.ToString("R", CultureInfo.InvariantCulture)}) returned false; the parameter is not exposed under that name."));
                return;
            }
            _writes.Add(new MixerParameterWrite(set.Bus, set.Parameter, binding.ExposedName, value));
        }

        private void Transition(TransitionSnapshotIntent transition)
        {
            if (!Binding.TryGetSnapshot(transition.Snapshot, out var binding))
            {
                _diagnostics.Add(new MixerDiagnostic(AudioBindingValidation.SnapshotMissing, transition, $"Snapshot '{transition.Snapshot}' was accepted but no mixer snapshot is bound to it."));
                return;
            }
            if (!Binding.Surface.IsPresent)
            {
                _diagnostics.Add(new MixerDiagnostic(AudioBindingValidation.MixerMissing, transition, $"The mixer is missing, so snapshot '{binding.MixerSnapshotName}' was not transitioned."));
                return;
            }
            if (!Binding.Surface.TryTransitionToSnapshot(binding.MixerSnapshotName, transition.DurationSeconds))
            {
                _diagnostics.Add(new MixerDiagnostic(AudioBindingValidation.SnapshotMissing, transition, $"AudioMixer.FindSnapshot('{binding.MixerSnapshotName}') returned null; the snapshot was not transitioned."));
                return;
            }
            _transitions.Add(new MixerSnapshotTransition(transition.Snapshot, binding.MixerSnapshotName, transition.DurationSeconds));
        }
    }
}
