using System;
using Lingkyn.Interaction.Core;

namespace Lingkyn.Interaction.Samples
{
    public static class SemanticRoutingExample
    {
        public static InteractionRoutingResult Run()
        {
            var confirmIntent = IntentDefinition.Create(
                MustIntentId("ui.confirm"),
                InteractionValueKind.Button,
                InteractionCapability.Digital,
                0).Value;

            var confirmRoute = InteractionRoute.Create(
                MustRouteId("route.ui.confirm"),
                MustContextId("context.ui"),
                MustIntentId("ui.confirm"),
                MustSourceId("source.keyboard.space"),
                InteractionModality.KeyboardMouse,
                InteractionCapability.Digital,
                new byte[] { 0x01 },
                0).Value;

            var context = InteractionContextDefinition.Create(
                MustContextId("context.ui"),
                0,
                new[] { confirmRoute.Id }).Value;

            var registry = InteractionRegistry.Create(
                new[] { confirmIntent },
                new[] { context },
                new[] { confirmRoute }).Value;

            var frame = InteractionFrame.Create(new[]
            {
                new SourceSignal(
                    confirmRoute.Id,
                    MustSourceId("source.keyboard.space"),
                    InteractionModality.KeyboardMouse,
                    InteractionCapability.Digital,
                    InteractionValue.FromButton(true),
                    InteractionPhase.Started,
                    1,
                    0),
                new SourceSignal(
                    confirmRoute.Id,
                    MustSourceId("source.keyboard.space"),
                    InteractionModality.KeyboardMouse,
                    InteractionCapability.Digital,
                    InteractionValue.FromButton(true),
                    InteractionPhase.Performed,
                    2,
                    1),
            }).Value;

            var coordinator = new InteractionCoordinator(
                registry,
                new[] { MustContextId("context.ui") },
                InteractionPolicySnapshot.Empty);

            return coordinator.RouteFrame(frame, _ => InteractionHandlerOutcome.Accepted);
        }

        private static IntentId MustIntentId(string value) => IntentId.TryCreate(value).Value;
        private static ContextId MustContextId(string value) => ContextId.TryCreate(value).Value;
        private static RouteId MustRouteId(string value) => RouteId.TryCreate(value).Value;
        private static SourceId MustSourceId(string value) => SourceId.TryCreate(value).Value;
    }
}
