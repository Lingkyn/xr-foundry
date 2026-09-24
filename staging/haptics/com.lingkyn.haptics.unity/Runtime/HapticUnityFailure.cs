namespace Lingkyn.Haptics.Unity
{
    // Stable codes for by-name or optional resolution failures the Unity adapter reports on its
    // own (a target the Core never sees: a device, a channel, an action reference). Every
    // per-target validation failure the Core already names (target.unknown,
    // profile.declaration.invalid, and the others) keeps the Core's own code (LESSON-004).

    public static class HapticUnityFailure
    {
        /// <summary>An XR Interaction Toolkit haptic impulse channel does not resolve for a target:
        /// the channel name the route map declares has no live channel on the resolved device.</summary>
        public const string ChannelMissing = "channel.missing";

        /// <summary>An OpenXR haptic action's device/subaction path does not resolve, or an
        /// <c>IDualMotorRumble</c>-capable device is not found on the Input System fallback route.</summary>
        public const string DeviceMissing = "device.missing";

        /// <summary>An <see cref="UnityEngine.InputSystem.InputActionReference"/> does not resolve
        /// to a live, enabled <c>InputAction</c>.</summary>
        public const string ActionMissing = "action.missing";

        /// <summary>Diagnostic-only (never a rejection): an <c>envelope</c> event played as a
        /// <c>transient</c> impulse because the active runtime does not report the
        /// amplitude-envelope (or PCM) OpenXR extension. Names the runtime and the substitution;
        /// never claims the requested envelope kind played when it did not.</summary>
        public const string EnvelopeUnsupportedDegraded = "envelope.unsupported.degraded";
    }
}
