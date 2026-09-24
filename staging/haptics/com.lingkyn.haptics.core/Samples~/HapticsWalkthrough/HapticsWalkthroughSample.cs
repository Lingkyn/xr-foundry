using System.Linq;
using System.Text;

namespace Lingkyn.Haptics.Core.Samples
{
    /// <summary>
    /// Domain-only walkthrough of the Haptics Core: an event registry built by explicit
    /// registration, a profile set, a full play/stop/stop_all/set_profile intent sequence, a
    /// binding table resolving one source intent to a haptic play intent and reporting
    /// binding.unbound for an unbound one, and a replay that proves the final state is
    /// deterministic. No asset, scene, or UnityEngine API is involved.
    /// </summary>
    public static class HapticsWalkthroughSample
    {
        public static string Run()
        {
            var report = new StringBuilder();

            // 1. Register a few events: one of every kind, each with an explicit guard rail and
            //    default. A duplicate id or an invalid declaration is rejected before it ever
            //    reaches the registry.
            var registryBuilder = new HapticEventRegistryBuilder();
            var grabContact = HapticEventId.Parse("grab.contact");
            var engineHum = HapticEventId.Parse("engine.hum");
            var impactPulse = HapticEventId.Parse("impact.pulse");
            registryBuilder.Register(grabContact, HapticKind.Transient, new AmplitudeRange(0f, 1f), 0.6f, new DurationRangeMs(10f, 200f), 40f, null, null, "grab contact");
            registryBuilder.Register(engineHum, HapticKind.Continuous, new AmplitudeRange(0f, 1f), 0.3f, new DurationRangeMs(50f, 5000f), 500f, new FrequencyRangeHz(20f, 300f), 90f, "engine hum");
            registryBuilder.Register(impactPulse, HapticKind.Envelope, new AmplitudeRange(0f, 1f), 0.8f, new DurationRangeMs(10f, 500f), 120f, new FrequencyRangeHz(20f, 300f), 150f, "impact pulse");
            var registry = registryBuilder.Build();
            report.AppendLine("registry: " + registry.Count + " events, fingerprint length " + registry.Fingerprint().Length);

            // 2. Build two profiles: a default profile that scales one named device down, and a
            //    boosted profile that scales the left target up.
            var defaultProfileId = HapticProfileId.Parse("default");
            var boostedProfileId = HapticProfileId.Parse("boosted");
            var defaultProfile = new HapticProfileBuilder(defaultProfileId).Add(HapticTarget.NamedDevice("glove_left"), 0.5f, true).Value.Build();
            var boostedProfile = new HapticProfileBuilder(boostedProfileId).Add(HapticTarget.Left, 2f, true).Value.Build();
            var profiles = HapticProfileSet.Create(new[] { defaultProfile, boostedProfile });
            report.AppendLine("profiles: " + profiles.Count);

            // 3. Drive a full intent sequence: play on two targets, switch profiles, stop one
            //    target, then stop everything.
            var state = HapticState.Initial(registry, profiles, defaultProfileId);
            state = Apply(report, state, new PlayIntent(grabContact, HapticTarget.Left));
            state = Apply(report, state, new PlayIntent(engineHum, HapticTarget.Right));
            state = Apply(report, state, new SetProfileIntent(boostedProfileId));
            state = Apply(report, state, new StopIntent(HapticTarget.Left));
            state = Apply(report, state, new StopAllIntent());
            report.AppendLine("active targets after stop_all: " + state.ActiveTargets.Count);

            // 4. Resolve a source intent through a binding table, and show the named result for
            //    an unbound one.
            var bindings = new[] { new HapticBinding(HapticSourceKind.InteractionIntent, "grab.begin", grabContact, HapticTarget.Left) };
            var table = HapticBindingTable.Validate(bindings, registry).Value;
            var bound = table.Dispatch(HapticSourceKind.InteractionIntent, "grab.begin");
            var unbound = table.Dispatch(HapticSourceKind.AudioEvent, "impact.hit");
            report.AppendLine("bound dispatch: " + (bound.Bound ? "play " + bound.Intent.EventId : bound.Code));
            report.AppendLine("unbound dispatch: " + unbound.Code + " " + unbound.Message);

            // 5. Replay the same short sequence from a fresh initial state; the final state and its
            //    fingerprint are equal, which is what "deterministic replay" means.
            var sequence = new HapticIntent[]
            {
                new PlayIntent(grabContact, HapticTarget.Left),
                new PlayIntent(engineHum, HapticTarget.Right),
            };
            var replayA = HapticState.Initial(registry, profiles, defaultProfileId).ApplyAll(sequence);
            var replayB = HapticState.Initial(registry, profiles, defaultProfileId).ApplyAll(sequence);
            report.AppendLine("deterministic replay: " + (replayA.State.Fingerprint() == replayB.State.Fingerprint() ? "yes" : "no"));
            report.AppendLine("replay outcomes accepted: " + replayA.Outcomes.Count(item => item.Accepted) + "/" + replayA.Outcomes.Count);

            return report.ToString();
        }

        private static HapticState Apply(StringBuilder report, HapticState state, HapticIntent intent)
        {
            var result = state.Apply(intent);
            report.AppendLine("  " + intent.Describe() + ": " + (result.Succeeded ? "accepted" : result.Code + " " + result.Message));
            return result.Succeeded ? result.Value : state;
        }
    }
}
