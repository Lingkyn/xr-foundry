using System;
using System.Collections.Generic;

namespace Lingkyn.Interaction.Core
{
    [Flags]
    public enum InteractionCapability
    {
        None = 0,
        Digital = 1 << 0,
        Scalar = 1 << 1,
        Vector2 = 1 << 2,
        Vector3 = 1 << 3,
        Pose = 1 << 4,
        Pointing = 1 << 5,
        HapticOutput = 1 << 6,
        Text = 1 << 7,
    }

    public enum InteractionModality
    {
        Unknown = 0,
        KeyboardMouse = 1,
        Gamepad = 2,
        Touch = 3,
        TrackedController = 4,
        ArticulatedHand = 5,
        Gaze = 6,
        Voice = 7,
        Assistive = 8,
        Simulated = 9,
    }

    public enum InteractionPhase
    {
        Started = 0,
        Performed = 1,
        Canceled = 2,
    }

    public enum InteractionValueKind
    {
        Button = 0,
        Scalar = 1,
        Vector2 = 2,
        Vector3 = 3,
        Pose = 4,
        Text = 5,
    }

    public enum InteractionActivationMode
    {
        Momentary = 0,
        Toggle = 1,
        Hold = 2,
    }

    public enum InteractionDispatchStatus
    {
        Routed = 0,
        Canceled = 1,
        Shadowed = 2,
        Rejected = 3,
        Ambiguous = 4,
        HandlerOutcome = 5,
    }

    public enum InteractionHandlerOutcome
    {
        Accepted = 0,
        Rejected = 1,
        Deferred = 2,
        Failed = 3,
    }

    public enum InteractionDiagnosticKind
    {
        ValidationFailure = 0,
        InactiveContext = 1,
        ShadowedRoute = 2,
        AmbiguousRoute = 3,
        DisabledRoute = 4,
        HandlerResult = 5,
        PolicyApplied = 6,
    }
}
