using System;
using System.Collections.Generic;
using System.Linq;
using Lingkyn.LiveTuning.Core;

namespace Lingkyn.LiveTuning.Unity
{
    // A plain runtime constructed with explicit references (registry, initial state, validated
    // bindings, an export sink, a skin apply target) that applies every accepted set, reset,
    // reset_all, and apply_snapshot to the bound skin instance through each binding's binder and
    // then re-invokes the renderer adapter's own skin entry point, never a view field directly. A
    // rejected intent reaches no binder. No scene singleton, static instance, scene search, or
    // reflection discovery; two runtimes constructed side by side share nothing.

    public sealed class TuningRuntime
    {
        private readonly List<TuningIntentOutcome> _outcomes = new List<TuningIntentOutcome>();

        public TuningRuntime(TunableRegistry registry, TuningState initialState, SkinBindingSet bindings, ISkinApplyTarget skinApplyTarget, ITuningExportSink exportSink)
        {
            Registry = registry ?? throw new ArgumentNullException(nameof(registry));
            Bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
            SkinApplyTarget = skinApplyTarget ?? throw new ArgumentNullException(nameof(skinApplyTarget));
            ExportSink = exportSink ?? throw new ArgumentNullException(nameof(exportSink));
            State = initialState ?? throw new ArgumentNullException(nameof(initialState));
            if (!ReferenceEquals(initialState.Registry, registry))
            {
                throw new ArgumentException("The initial state must be built over this runtime's registry.", nameof(initialState));
            }
        }

        public TunableRegistry Registry { get; }
        public SkinBindingSet Bindings { get; }
        public ISkinApplyTarget SkinApplyTarget { get; }
        public ITuningExportSink ExportSink { get; }
        public TuningState State { get; private set; }

        /// <summary>Every intent this runtime saw, accepted or rejected, in the order it saw them.</summary>
        public IReadOnlyList<TuningIntentOutcome> Outcomes => _outcomes;

        public TuningIntentOutcome Apply(TuningIntent intent)
        {
            if (intent == null) throw new ArgumentNullException(nameof(intent));
            var index = _outcomes.Count;
            var result = State.Apply(intent);
            if (!result.Succeeded)
            {
                var rejected = new TuningIntentOutcome(index, intent, false, result.Code, result.Message);
                _outcomes.Add(rejected);
                return rejected;
            }
            var previous = State;
            State = result.Value;
            ApplyChangedBindings(previous, State);
            var accepted = new TuningIntentOutcome(index, intent, true, result.Code, result.Message);
            _outcomes.Add(accepted);
            return accepted;
        }

        /// <summary>Writes every tunable whose effective value changed to its bound member through
        /// its binder, then re-invokes the skin apply target once per distinct changed asset.</summary>
        private void ApplyChangedBindings(TuningState previous, TuningState current)
        {
            var changedAssets = new List<UnityEngine.Object>();
            foreach (var binding in Bindings.Bindings)
            {
                previous.TryGetEffective(binding.Id, out var before);
                current.TryGetEffective(binding.Id, out var after);
                if (before.Equals(after)) continue;
                binding.Binder.Apply(binding.Asset, binding.MemberName, after);
                if (!changedAssets.Contains(binding.Asset)) changedAssets.Add(binding.Asset);
            }
            foreach (var asset in changedAssets)
            {
                SkinApplyTarget.ApplySkin(asset);
            }
        }

        public TuningExportResult Export(string label = "") => ExportSink.Write(State.Export(), label);
    }
}
