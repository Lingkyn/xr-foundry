using System;
using System.Collections.Generic;
using Lingkyn.Haptics.Core;

namespace Lingkyn.Haptics.Unity
{
    // A plain runtime constructed with explicit references (an initial Core state, an injectable
    // output sink, and an explicit envelope-support report) that turns every accepted Core play/
    // stop/stop_all outcome into exactly one call on the sink, degrading an envelope play to a
    // transient when the runtime lacks the extension. A rejected intent reaches no sink. No scene
    // singleton, static instance, scene search, or reflection discovery resolves any of this
    // runtime's references; no per-frame update loop drives it — only the explicit Apply calls it
    // receives (no polling).

    public sealed class HapticUnityRuntime
    {
        private readonly List<HapticIntentOutcome> _outcomes = new List<HapticIntentOutcome>();
        private readonly List<HapticEnvelopeDiagnostic> _envelopeDiagnostics = new List<HapticEnvelopeDiagnostic>();

        public HapticUnityRuntime(HapticState initialState, IHapticOutputSink sink, IHapticEnvelopeSupport envelopeSupport)
        {
            State = initialState ?? throw new ArgumentNullException(nameof(initialState));
            Sink = sink ?? throw new ArgumentNullException(nameof(sink));
            EnvelopeSupport = envelopeSupport ?? throw new ArgumentNullException(nameof(envelopeSupport));
        }

        public HapticState State { get; private set; }
        public IHapticOutputSink Sink { get; }
        public IHapticEnvelopeSupport EnvelopeSupport { get; }

        /// <summary>Every intent this runtime saw, accepted or rejected, in the order it saw them.</summary>
        public IReadOnlyList<HapticIntentOutcome> Outcomes => _outcomes;

        /// <summary>Every envelope-to-transient degrade this runtime has reported, in order.</summary>
        public IReadOnlyList<HapticEnvelopeDiagnostic> EnvelopeDiagnostics => _envelopeDiagnostics;

        /// <summary>Applies one intent through the Core's one entry point
        /// (<see cref="HapticState.Apply"/>) and no other path: a rejected intent (including a
        /// stale <see cref="HapticFailure.StateStale"/>) reaches no sink. Two runtimes constructed
        /// side by side over two states and two sinks share nothing: applying an intent to one
        /// changes no target, active playback, or sink call of the other.</summary>
        public HapticIntentOutcome Apply(HapticIntent intent)
        {
            if (intent == null) throw new ArgumentNullException(nameof(intent));
            var index = _outcomes.Count;
            var result = State.Apply(intent);
            if (!result.Succeeded)
            {
                var rejected = new HapticIntentOutcome(index, intent, false, result.Code, result.Message, State.Revision);
                _outcomes.Add(rejected);
                return rejected;
            }
            var current = result.Value;
            State = current;
            DispatchToSink(intent, current);
            var accepted = new HapticIntentOutcome(index, intent, true, result.Code, result.Message, current.Revision);
            _outcomes.Add(accepted);
            return accepted;
        }

        /// <summary>Turns one accepted intent into exactly one sink call. A <see cref="SetProfileIntent"/>
        /// reaches no sink: the Core does not retroactively rescale an already-active playback when
        /// the active profile changes, so there is nothing new to send.</summary>
        private void DispatchToSink(HapticIntent intent, HapticState current)
        {
            switch (intent)
            {
                case PlayIntent play:
                {
                    if (!current.TryGetActive(play.Target, out var playback)) return;
                    if (!current.Registry.TryGet(playback.EventId, out var definition)) return;
                    var resolution = HapticEnvelopeDegrade.Resolve(EnvelopeSupport, definition.Kind, playback.Amplitude, playback.DurationMs, playback.FrequencyHz);
                    if (resolution.Diagnostic.HasValue) _envelopeDiagnostics.Add(resolution.Diagnostic.Value);
                    Sink.Play(play.Target, resolution.Kind, resolution.Amplitude, resolution.DurationMs, resolution.FrequencyHz);
                    return;
                }
                case StopIntent stop:
                    Sink.Stop(stop.Target);
                    return;
                case StopAllIntent _:
                    Sink.StopAll();
                    return;
                default:
                    return;
            }
        }
    }
}
