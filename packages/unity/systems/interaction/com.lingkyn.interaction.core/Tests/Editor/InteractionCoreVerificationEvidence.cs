using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Lingkyn.Interaction.Core.Editor.Tests
{
    /// <summary>
    /// Captures routing outputs separately so acceptance tests can assert each
    /// verification-contract surface independently.
    /// </summary>
    internal sealed class RoutingEvidenceSnapshot
    {
        public RoutingEvidenceSnapshot(
            InteractionRoutingResult result,
            int handlerCallCount,
            IReadOnlyList<SemanticInteractionEvent> handlerEvents)
        {
            Result = result ?? throw new ArgumentNullException(nameof(result));
            HandlerCallCount = handlerCallCount;
            HandlerEvents = handlerEvents ?? Array.Empty<SemanticInteractionEvent>();
        }

        public InteractionRoutingResult Result { get; }
        public int HandlerCallCount { get; }
        public IReadOnlyList<SemanticInteractionEvent> HandlerEvents { get; }

        public IReadOnlyList<SemanticInteractionEvent> EmittedEvents => Result.Events;
        public IReadOnlyList<InteractionDiagnostic> Diagnostics => Result.Diagnostics;
        public IReadOnlyList<InteractionDispatchResult> Dispatches => Result.Dispatches;
        public ActiveContextSnapshot ActiveContexts => Result.ActiveContexts;

        public static RoutingEvidenceSnapshot Route(
            InteractionRegistry registry,
            IEnumerable<ContextId> activeContexts,
            InteractionPolicySnapshot policy,
            InteractionFrame frame,
            InteractionIntentHandler handler = null,
            InteractionRoutingSession session = null)
        {
            var handlerCalls = 0;
            var handlerEvents = new List<SemanticInteractionEvent>();
            InteractionIntentHandler wrapped = null;
            if (handler != null)
            {
                wrapped = semanticEvent =>
                {
                    handlerCalls++;
                    handlerEvents.Add(semanticEvent);
                    return handler(semanticEvent);
                };
            }

            var result = new InteractionCoordinator(registry, activeContexts, policy, session)
                .RouteFrame(frame, wrapped);
            return new RoutingEvidenceSnapshot(result, handlerCalls, handlerEvents);
        }

        public void AssertIndependentSurfaces(
            int expectedEventCount,
            int expectedDispatchCount,
            int expectedDiagnosticCount,
            int expectedHandlerCalls,
            IEnumerable<int> expectedDispatchIngressSequences = null,
            IEnumerable<string> expectedActiveContexts = null,
            IEnumerable<int> expectedEventIngressSequences = null)
        {
            Assert.That(EmittedEvents, Has.Count.EqualTo(expectedEventCount), "Emitted events");
            Assert.That(Dispatches, Has.Count.EqualTo(expectedDispatchCount), "Dispatch outcomes");
            Assert.That(Diagnostics, Has.Count.EqualTo(expectedDiagnosticCount), "Diagnostics");
            Assert.That(HandlerCallCount, Is.EqualTo(expectedHandlerCalls), "Handler calls");
            Assert.That(HandlerEvents, Has.Count.EqualTo(expectedHandlerCalls), "Handler event capture");

            var eventSequences = expectedEventIngressSequences ?? expectedDispatchIngressSequences;
            if (expectedEventCount > 0 && eventSequences != null)
            {
                Assert.That(
                    EmittedEvents.Select(e => e.IngressSequence),
                    Is.EqualTo(eventSequences),
                    "Emitted event ingress sequences");
            }
            else if (expectedEventCount == 0 && eventSequences != null)
            {
                Assert.That(EmittedEvents, Is.Empty, "Suppressed semantic events");
            }

            if (expectedDispatchIngressSequences != null)
            {
                Assert.That(
                    Dispatches.Select(d => d.IngressSequence),
                    Is.EqualTo(expectedDispatchIngressSequences),
                    "Dispatch ingress sequences");
            }

            if (expectedActiveContexts != null)
            {
                Assert.That(
                    ActiveContexts.ActiveContexts.Select(c => c.Value),
                    Is.EqualTo(expectedActiveContexts),
                    "Active context snapshot");
            }
        }

        public void AssertNoEmittedEventMatches(Func<SemanticInteractionEvent, bool> predicate, string because)
        {
            Assert.That(EmittedEvents.Any(predicate), Is.False, because);
        }

        public void AssertDiagnostic(
            InteractionDiagnosticKind kind,
            InteractionValidationCode code,
            int ingressSequence)
        {
            Assert.That(
                Diagnostics.Any(d =>
                    d.Kind == kind
                    && d.Code == code
                    && d.IngressSequence == ingressSequence),
                Is.True,
                $"Expected diagnostic kind={kind}, code={code}, ingress={ingressSequence}.");
        }
    }
}
