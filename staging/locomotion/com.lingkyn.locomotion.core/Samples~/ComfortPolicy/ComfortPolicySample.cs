using System.Text;

namespace Lingkyn.Locomotion.Core.Samples
{
    /// <summary>
    /// Domain-only walkthrough of the Locomotion Core: a comfort policy, an anchor
    /// registry, a teleport/turn/move intent sequence with one rejected intent, and a
    /// replay that proves the final state is deterministic. No scene, rig, XR device, or
    /// UnityEngine API is involved.
    /// </summary>
    public static class ComfortPolicySample
    {
        public static string Run()
        {
            var report = new StringBuilder();

            // 1. Start from the default comfort policy (snap turn at 45 degrees, 1.5 m/s,
            //    standing, vignette off) and register two anchor ids. Anchors are ids,
            //    never transforms; the consumer maps them to scene objects outside the Core.
            var spawn = AnchorId.Parse("spawn.point.a");
            var overlook = AnchorId.Parse("overlook.b");

            var intents = new LocomotionIntent[]
            {
                new RegisterAnchorIntent(spawn),
                new RegisterAnchorIntent(overlook),
                new TeleportIntent(spawn),
                new TurnIntent(ModeId.SnapTurn, TurnDirection.Right),
                new MoveIntent(new PlanarOffset(1f, 0f), 2f),
                new TurnIntent(ModeId.SmoothTurn, TurnDirection.Right), // mode.disabled: the policy currently selects snap turn
                new SetComfortOptionIntent(ComfortOptions.MovementSpeedName, ComfortOptionValue.FloatRange(3f)),
                new TeleportIntent(overlook),
            };

            // 2. Apply the sequence twice from the same initial state; the outcomes and
            //    the fingerprints agree, so the state is a function of the sequence.
            var first = LocomotionState.Initial(ComfortPolicy.Default()).ApplyAll(intents);
            var second = LocomotionState.Initial(ComfortPolicy.Default()).ApplyAll(intents);
            foreach (var outcome in first.Outcomes)
            {
                report.AppendLine("  " + outcome.Index + " " + outcome.Intent.Describe() + ": "
                                  + (outcome.Accepted ? "accepted" : outcome.Code + " " + outcome.Message));
            }
            report.AppendLine("accepted " + first.AcceptedCount + ", rejected " + first.RejectedCount);
            report.AppendLine("deterministic: " + (first.State.Equals(second.State) ? "yes" : "no"));

            // 3. Read the state back: active modes, the comfort policy, the current
            //    anchor, the accumulated heading, and the accumulated offset are all
            //    enumerable and immutable.
            report.AppendLine("active modes: " + string.Join(", ", first.State.ActiveModes));
            report.AppendLine("policy: " + first.State.Policy);
            report.AppendLine("anchor: " + (first.State.CurrentAnchor.HasValue ? first.State.CurrentAnchor.Value.ToString() : "-"));
            report.AppendLine("heading: " + first.State.HeadingDegrees);
            report.AppendLine("offset: " + first.State.Offset);

            return report.ToString();
        }
    }
}
