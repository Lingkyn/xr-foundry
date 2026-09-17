using System.Text;

namespace Lingkyn.Audio.Core.Samples
{
    /// <summary>
    /// Domain-only walkthrough of the Audio Core: a three-bus mix graph with typed
    /// parameter contracts, two snapshots and two events, an anchor-referenced
    /// attachment, one rejected intent, and a replay that proves the final state is
    /// deterministic. No scene, asset, clip, mixer, or UnityEngine API is involved.
    /// </summary>
    public static class MixGraphSample
    {
        public static string Run()
        {
            var report = new StringBuilder();

            // 1. Declare the graph. The root bus is required; every other bus names its
            //    parent; each parameter has a closed kind and a declared range or set.
            var master = BusId.Parse("master");
            var music = BusId.Parse("music");
            var effects = BusId.Parse("effects");
            var volume = ParameterId.Parse("volume");
            var mode = ParameterId.Parse("mode");

            var graph = new MixGraphBuilder()
                .RootBus(master, ParameterContract.Float(volume, 0f, 1f, 1f))
                .Bus(music, master, ParameterContract.Float(volume, 0f, 1f, 0.8f))
                .Bus(effects, master, ParameterContract.Enumerated(mode, new[] { "indoor", "outdoor" }, "indoor"))
                .Snapshot(SnapshotId.Parse("calm"), music, effects)
                .Snapshot(SnapshotId.Parse("tense"), music)
                .Event(AudioEventId.Parse("ui.confirm"), effects)
                .Event(AudioEventId.Parse("ambience.loop"), music)
                .Build();
            if (!graph.Succeeded)
            {
                report.AppendLine("graph: " + graph.Code + " " + graph.Message);
                return report.ToString();
            }
            report.AppendLine("graph: root " + graph.Value.Root.Id + ", " + graph.Value.BusCount + " buses, "
                              + graph.Value.SnapshotCount + " snapshots, " + graph.Value.EventCount + " events");

            // 2. Author an intent sequence. Anchors are ids, never transforms; the
            //    consumer maps them to tracked devices or scene objects outside the Core.
            var leftHand = AnchorId.Parse("hand.left");
            var intents = new AudioIntent[]
            {
                new RegisterAnchorIntent(leftHand),
                new PostEventIntent(AudioEventId.Parse("ambience.loop")),
                new PostEventIntent(AudioEventId.Parse("ui.confirm")),
                new AttachEventIntent(AudioEventId.Parse("ui.confirm"), leftHand),
                new SetParameterIntent(music, volume, ParameterValue.Float(0.4f)),
                new SetParameterIntent(music, volume, ParameterValue.Float(4f)),          // out of range: rejected, state untouched
                new SetParameterIntent(effects, mode, ParameterValue.Enumerated("outdoor")),
                new TransitionSnapshotIntent(SnapshotId.Parse("tense"), 0.75f),
            };

            // 3. Apply the sequence twice from the same initial state; the outcomes and
            //    the fingerprints agree, so the state is a function of the sequence.
            var first = AudioState.Initial(graph.Value).ApplyAll(intents);
            var second = AudioState.Initial(graph.Value).ApplyAll(intents);
            foreach (var outcome in first.Outcomes)
            {
                report.AppendLine("  " + outcome.Index + " " + outcome.Intent.Describe() + ": "
                                  + (outcome.Accepted ? "accepted" : outcome.Code + " " + outcome.Message));
            }
            report.AppendLine("accepted " + first.AcceptedCount + ", rejected " + first.RejectedCount);
            report.AppendLine("deterministic: " + (first.State.Equals(second.State) ? "yes" : "no"));

            // 4. Read the state back: active events, current snapshot, bus parameter
            //    values, and attachments are all enumerable and immutable.
            report.AppendLine("active: " + string.Join(", ", first.State.ActiveEvents));
            report.AppendLine("snapshot: " + (first.State.CurrentSnapshot.HasValue ? first.State.CurrentSnapshot.Value.ToString() : "-"));
            foreach (var value in first.State.ParameterValues)
            {
                report.AppendLine("  " + value.Bus + "/" + value.Parameter + " = " + value.Value);
            }
            foreach (var attachment in first.State.Attachments)
            {
                report.AppendLine("  " + attachment.Event + " -> " + attachment.Anchor);
            }

            return report.ToString();
        }
    }
}
