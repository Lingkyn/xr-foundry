using System.Linq;
using System.Text;

namespace Lingkyn.LiveTuning.Core.Samples
{
    /// <summary>
    /// Domain-only walkthrough of the Live Tuning Core: a registry built two ways (explicit
    /// registration, and the token bridge over a small inline token document), a full
    /// set/snapshot/reset/apply_snapshot intent sequence, an export to a token-override
    /// document, and a replay that proves the final state is deterministic. No asset, scene,
    /// or UnityEngine API is involved.
    /// </summary>
    public static class TuningWalkthroughSample
    {
        public static string Run()
        {
            var report = new StringBuilder();

            // 1. Register a few tunables directly: one of every kind, each with an explicit
            //    range or value set. A duplicate id or an out-of-range default is rejected
            //    before it ever reaches the registry.
            var builder = new TunableRegistryBuilder();
            var opacity = TunableId.Parse("panel.opacity");
            var cornerRadius = TunableId.Parse("shape.corner_radius");
            var weight = TunableId.Parse("text.weight");
            var panelColour = TunableId.Parse("surface.panel");
            builder.Register(opacity, new FloatDeclaration(0f, 1f, 0.01f), TunableValue.OfFloat(0.9f), "surface", "opacity");
            builder.Register(cornerRadius, new IntegerDeclaration(0, 32), TunableValue.OfInteger(8), "shape", "corner_radius");
            builder.Register(weight, new EnumeratedDeclaration(new[] { "medium", "bold" }), TunableValue.OfEnumerated("medium"), "text", "weight");
            builder.Register(panelColour, ColourDeclaration.Instance, TunableValue.OfColour(0.031f, 0.094f, 0.122f, 0.96f), "surface", "panel");
            var registry = builder.Build();
            report.AppendLine("registry: " + registry.Count + " tunables, fingerprint length " + registry.Fingerprint().Length);

            // 2. Build a second registry from a small inline token document through the token
            //    bridge, with an explicit annotation-key set and range policy supplied by the
            //    caller (never held inside the Core).
            const string tokenJson = "{\"hit_target_dp\":{\"minimum\":48,\"small_visual_rule\":\"extend with hit-slop\"}}";
            var rangePolicy = new TokenRangePolicy().WithIntegerRange("hit_target_dp.minimum", 0, 128);
            var bridged = TokenBridge.BuildRegistry(tokenJson, LiveTuningTokenAnnotations.Default, rangePolicy);
            report.AppendLine("token bridge: " + (bridged.Succeeded ? bridged.Value.Count + " tunable(s) derived" : bridged.Code + " " + bridged.Message));

            // 3. Drive a full intent sequence: set two values, take a snapshot, change one of
            //    them again, reset it, then apply the snapshot back.
            var state = TuningState.Initial(registry);
            state = Apply(report, state, new SetIntent(opacity, TunableValue.OfFloat(0.4f)));
            state = Apply(report, state, new SetIntent(cornerRadius, TunableValue.OfInteger(4)));
            var saved = SnapshotId.Parse("before_experiment");
            state = Apply(report, state, new SnapshotIntent(saved));
            state = Apply(report, state, new SetIntent(panelColour, TunableValue.OfColour(0.2f, 0.2f, 0.2f, 1f)));
            state = Apply(report, state, new ResetIntent(panelColour));
            state = Apply(report, state, new ApplySnapshotIntent(saved));
            report.AppendLine("overrides after apply_snapshot: " + state.Overrides.Count);

            // 4. Export the overrides to a token-override document and read it back as text.
            var document = state.Export();
            report.AppendLine("export: schema=" + TokenOverrideDocument.SchemaId + " overrides=" + document.Overrides.Count);
            report.AppendLine(document.ToJson());

            // 5. Replay the same short sequence from a fresh initial state; the final state and
            //    its fingerprint are equal, which is what "deterministic replay" means.
            var sequence = new TuningIntent[]
            {
                new SetIntent(opacity, TunableValue.OfFloat(0.4f)),
                new SetIntent(cornerRadius, TunableValue.OfInteger(4)),
            };
            var replayA = TuningState.Initial(registry).ApplyAll(sequence);
            var replayB = TuningState.Initial(registry).ApplyAll(sequence);
            report.AppendLine("deterministic replay: " + (replayA.State.Equals(replayB.State) ? "yes" : "no")
                              + ", fingerprint equal: " + (replayA.State.Fingerprint() == replayB.State.Fingerprint() ? "yes" : "no"));
            report.AppendLine("replay outcomes accepted: " + replayA.Outcomes.Count(item => item.Accepted) + "/" + replayA.Outcomes.Count);

            return report.ToString();
        }

        private static TuningState Apply(StringBuilder report, TuningState state, TuningIntent intent)
        {
            var result = state.Apply(intent);
            report.AppendLine("  " + intent.Describe() + ": " + (result.Succeeded ? "accepted" : result.Code + " " + result.Message));
            return result.Succeeded ? result.Value : state;
        }
    }
}
