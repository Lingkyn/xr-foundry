using System;
using Lingkyn.QualityTiers.Core;

namespace Lingkyn.QualityTiers.Core.Samples.QualityTiersWalkthrough
{
    // Domain-only walkthrough: no asset, scene, or UnityEngine API. It builds a tier registry and a
    // device capability set by explicit registration, applies a full select_tier/set_override/
    // reset/apply_preset intent sequence, and replays the same sequence to show the resulting
    // fingerprint is deterministic. Run this from a plain C# host (a console app or an EditMode
    // test), never from a Unity scene: the Core has no engine dependency to run inside one.
    public static class QualityTiersWalkthroughSample
    {
        public static void Run()
        {
            var registry = BuildRegistry();
            var capabilities = BuildCapabilities();
            var handheld = DeviceProfileId.Parse("device.handheld");

            var state = QualityState.Initial(registry, capabilities);

            var sequence = new QualityIntent[]
            {
                new SelectTierIntent(handheld, TierId.Parse("tier.balanced")),
                new SetOverrideIntent(handheld, "render_scale", 1.0f),
                new ResetIntent(handheld),
                new ApplyPresetIntent(handheld, TierId.Parse("tier.balanced")),
            };

            var result = state.ApplyAll(sequence);
            Console.WriteLine($"All accepted: {result.AllAccepted}");
            foreach (var outcome in result.Outcomes)
            {
                Console.WriteLine($"  [{outcome.Index}] {outcome.Intent.Describe()} -> accepted={outcome.Accepted} code='{outcome.Code}' actor={outcome.Actor} revision={outcome.RevisionAfter}");
            }
            Console.WriteLine($"Final fingerprint: {result.State.Fingerprint()}");

            // Replaying the identical sequence over a freshly built registry and capability set
            // produces an equal fingerprint (QC-09): the state is deterministic, not incidental.
            var replay = QualityState.Initial(BuildRegistry(), BuildCapabilities()).ApplyAll(sequence);
            Console.WriteLine($"Replay fingerprint matches: {replay.State.Fingerprint() == result.State.Fingerprint()}");

            // A tier the device does not support fails closed before any state change.
            var unsupported = result.State.Apply(new SelectTierIntent(handheld, TierId.Parse("tier.ultra_shadows")));
            Console.WriteLine($"Unsupported tier rejected with: '{unsupported.Code}' ({unsupported.Message})");
        }

        private static QualityTierRegistry BuildRegistry()
        {
            var builder = new QualityTierRegistryBuilder();
            builder.Register(
                TierId.Parse("tier.balanced"),
                new RefreshRateRangeHz(72f, 90f),
                new RenderScaleRange(0.8f, 1.0f),
                FoveationLevel.Medium,
                MsaaSampleCount.Two,
                new ShadowBudgetRange(5f, 20f),
                new PostProcessingBudgetRange(5f, 15f),
                "balanced");
            builder.Register(
                TierId.Parse("tier.ultra_shadows"),
                new RefreshRateRangeHz(90f, 120f),
                new RenderScaleRange(1.0f, 1.4f),
                FoveationLevel.Off,
                MsaaSampleCount.Eight,
                new ShadowBudgetRange(30f, 60f),
                new PostProcessingBudgetRange(20f, 40f),
                "ultra shadows");
            return builder.Build();
        }

        private static DeviceCapabilitySet BuildCapabilities()
        {
            var builder = new DeviceCapabilitySetBuilder();
            var handheldCapability = DeviceCapabilityDescriptor.TryCreate(
                DeviceProfileId.Parse("device.handheld"),
                new[] { 72f, 90f },
                new RenderScaleRange(0.7f, 1.1f),
                new[] { FoveationLevel.Medium, FoveationLevel.High },
                new[] { MsaaSampleCount.Two, MsaaSampleCount.Four });
            if (!handheldCapability.Succeeded)
            {
                throw new InvalidOperationException(handheldCapability.Message);
            }
            builder.Register(handheldCapability.Value);
            return builder.Build();
        }
    }
}
