using System.Linq;
using System.Text;

namespace Lingkyn.SceneFlow.Core.Samples
{
    /// <summary>
    /// Domain-only walkthrough of the Scene Flow Core: a declared scene graph with a menu
    /// set, two content sets, and a fallback set; a full load, a switch, one failed load
    /// that recovers to the fallback set, and a replay that proves the final state is
    /// deterministic. No scene, asset, build index, or UnityEngine API is involved; time and
    /// load completion are supplied as explicit intents, never a clock.
    /// </summary>
    public static class SceneGraphSample
    {
        public static string Run()
        {
            var report = new StringBuilder();

            // 1. Declare the graph. Every set names its member scenes and the one member that
            //    is active while the set is current; the graph also names one fallback set.
            var menu = SceneSetId.Parse("menu");
            var level1 = SceneSetId.Parse("level1");
            var level2 = SceneSetId.Parse("level2");
            var fallback = SceneSetId.Parse("fallback");
            var menuMain = SceneId.Parse("menu.main");
            var level1Play = SceneId.Parse("level1.play");
            var level1Audio = SceneId.Parse("level1.audio");
            var level2Play = SceneId.Parse("level2.play");
            var fallbackError = SceneId.Parse("fallback.error");

            var graph = new SceneGraphBuilder()
                .Set(menu, menuMain, menuMain)
                .Set(level1, level1Play, level1Play, level1Audio)
                .Set(level2, level2Play, level2Play, level1Audio) // level1Audio persists across the switch
                .Set(fallback, fallbackError, fallbackError)
                .Build(fallback);
            if (!graph.Succeeded)
            {
                report.AppendLine("graph: " + graph.Code + " " + graph.Message);
                return report.ToString();
            }
            report.AppendLine("graph: " + graph.Value.SetCount + " sets, fallback " + graph.Value.FallbackSet);

            // 2. Declare non-zero transition durations so every phase is a distinct, explicit
            //    step; a zero duration would complete a phase on the very tick it is entered.
            var options = TransitionOptions.Create(0.5f, 0.25f, 0.5f);
            var state = SceneFlowState.Initial(graph.Value, options);

            // 3. Load the menu: fading_out -> loading -> holding -> fading_in -> idle, driven
            //    only by explicit elapsed-time and load-completed intents.
            state = Apply(report, state, new LoadSetIntent(menu));
            state = Apply(report, state, new ElapsedTimeIntent(SceneFlowDuration.Create(0.5f)));
            state = Apply(report, state, new SceneLoadCompletedIntent(menuMain));
            state = Apply(report, state, new ElapsedTimeIntent(SceneFlowDuration.Create(0.25f)));
            state = Apply(report, state, new ElapsedTimeIntent(SceneFlowDuration.Create(0.5f)));
            report.AppendLine("after menu load: loaded=[" + string.Join(",", state.LoadedSets) + "] active=" + state.ActiveScene);

            // 4. Load level1 additively (the menu stays loaded), then switch to level2. One of
            //    level1's scenes fails to load first, which is recovered to the fallback set.
            state = Apply(report, state, new LoadSetIntent(level1));
            state = Apply(report, state, new ElapsedTimeIntent(SceneFlowDuration.Create(0.5f)));
            var failed = state.Apply(new SceneLoadFailedIntent(level1Play, "missing asset bundle"));
            report.AppendLine("  " + new SceneLoadFailedIntent(level1Play, "missing asset bundle").Describe()
                              + ": accepted, note=" + failed.Code + " (recovering to " + graph.Value.FallbackSet + ")");
            state = failed.Value;
            state = Apply(report, state, new SceneLoadCompletedIntent(fallbackError));
            state = Apply(report, state, new ElapsedTimeIntent(SceneFlowDuration.Create(0.25f)));
            state = Apply(report, state, new ElapsedTimeIntent(SceneFlowDuration.Create(0.5f)));
            report.AppendLine("after recovery: loaded=[" + string.Join(",", state.LoadedSets) + "] active=" + state.ActiveScene);

            // 5. Read the state back: loaded sets, loaded scenes, and the active scene are all
            //    enumerable and immutable.
            report.AppendLine("loaded scenes: " + string.Join(", ", state.LoadedScenes));

            // 6. Replay the exact same intent sequence from a fresh initial state; the final
            //    state and its fingerprint are equal, which is what "deterministic replay" means.
            var replaySequence = new SceneFlowIntent[]
            {
                new LoadSetIntent(menu),
                new ElapsedTimeIntent(SceneFlowDuration.Create(0.5f)),
                new SceneLoadCompletedIntent(menuMain),
                new ElapsedTimeIntent(SceneFlowDuration.Create(0.25f)),
                new ElapsedTimeIntent(SceneFlowDuration.Create(0.5f)),
            };
            var replayA = SceneFlowState.Initial(graph.Value, options).ApplyAll(replaySequence);
            var replayB = SceneFlowState.Initial(graph.Value, options).ApplyAll(replaySequence);
            report.AppendLine("deterministic replay: " + (replayA.State.Equals(replayB.State) ? "yes" : "no")
                              + ", fingerprint equal: " + (replayA.State.Fingerprint() == replayB.State.Fingerprint() ? "yes" : "no"));
            report.AppendLine("replay outcomes accepted: " + replayA.Outcomes.Count(item => item.Accepted) + "/" + replayA.Outcomes.Count);

            return report.ToString();
        }

        private static SceneFlowState Apply(StringBuilder report, SceneFlowState state, SceneFlowIntent intent)
        {
            var result = state.Apply(intent);
            report.AppendLine("  " + intent.Describe() + ": " + (result.Succeeded ? "accepted, phase=" + result.Value.Phase : result.Code + " " + result.Message));
            return result.Succeeded ? result.Value : state;
        }
    }
}
