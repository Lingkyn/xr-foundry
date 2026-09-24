using System;
using System.Collections.Generic;
using Lingkyn.Haptics.Core;
using UnityEngine.InputSystem;

namespace Lingkyn.Haptics.Unity
{
    // The three real haptic-output routes, each behind IHapticOutputSink: an XR Interaction
    // Toolkit impulse-channel sink, an OpenXR haptic-action sink, and an Input System rumble
    // fallback sink. Each resolves its device by an explicit, injected probe and reports an
    // explicit failure with a stable code rather than staying silent or reporting success when
    // the target is missing or unsupported (LESSON-004). Which sink (or ordered fallback set of
    // sinks) a runtime uses is the consumer's own explicit wiring at construction time
    // (OrderedFallbackHapticSink below), never a platform check inside this adapter.

    /// <summary>Explicit, injected confirmation that an XRI haptic impulse channel name actually
    /// exists on the resolved device. A real implementation queries the live XRI channel; a fixed
    /// fake reports whatever a test wires it to.</summary>
    public interface IXriChannelProbe
    {
        bool ChannelExists(string channelName);
    }

    public sealed class FixedXriChannelProbe : IXriChannelProbe
    {
        private readonly HashSet<string> _existingChannels;

        public FixedXriChannelProbe(IEnumerable<string> existingChannels)
        {
            _existingChannels = new HashSet<string>(existingChannels ?? Array.Empty<string>(), StringComparer.Ordinal);
        }

        public bool ChannelExists(string channelName) => _existingChannels.Contains(channelName ?? string.Empty);
    }

    /// <summary>A thin sink over an XRI haptic impulse channel. Resolves a Core target through an
    /// explicit <see cref="HapticRouteMap"/>, then confirms the resolved channel actually exists
    /// through an explicit <see cref="IXriChannelProbe"/>; either miss is reported, never silent.</summary>
    public sealed class XriImpulseChannelSink : IHapticOutputSink
    {
        private readonly HapticRouteMap _routeMap;
        private readonly IXriChannelProbe _probe;

        public XriImpulseChannelSink(HapticRouteMap routeMap, IXriChannelProbe probe)
        {
            _routeMap = routeMap ?? throw new ArgumentNullException(nameof(routeMap));
            _probe = probe ?? throw new ArgumentNullException(nameof(probe));
        }

        public HapticSinkResult Play(HapticTarget target, HapticKind kind, float amplitude, float durationMs, float? frequencyHz) =>
            TryResolveChannel(target, out _) ? HapticSinkResult.Ok() : _lastFailure;

        public HapticSinkResult Stop(HapticTarget target) =>
            TryResolveChannel(target, out _) ? HapticSinkResult.Ok() : _lastFailure;

        public HapticSinkResult StopAll() => HapticSinkResult.Ok();

        private HapticSinkResult _lastFailure;

        private bool TryResolveChannel(HapticTarget target, out string channelName)
        {
            channelName = string.Empty;
            if (!_routeMap.TryResolve(target, out var handle) || handle.Kind != HapticRouteKind.XriImpulseChannel)
            {
                _lastFailure = HapticSinkResult.Fail(HapticFailure.TargetUnknown, $"No XRI impulse channel route is mapped for target '{target}'.");
                return false;
            }
            if (!_probe.ChannelExists(handle.ChannelName))
            {
                _lastFailure = HapticSinkResult.Fail(HapticUnityFailure.ChannelMissing, $"XRI impulse channel '{handle.ChannelName}' for target '{target}' does not exist on the resolved device.");
                return false;
            }
            channelName = handle.ChannelName;
            return true;
        }
    }

    /// <summary>Explicit, injected confirmation that an OpenXR action-and-subaction-path pair
    /// resolves to a live device. A real implementation queries the live OpenXR runtime; a fixed
    /// fake reports whatever a test wires it to.</summary>
    public interface IOpenXrDeviceProbe
    {
        bool DeviceExists(string actionName, string subactionPath);
    }

    public sealed class FixedOpenXrDeviceProbe : IOpenXrDeviceProbe
    {
        private readonly HashSet<string> _existing;

        public FixedOpenXrDeviceProbe(IEnumerable<(string ActionName, string SubactionPath)> existing)
        {
            _existing = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (actionName, subactionPath) in existing ?? Array.Empty<(string, string)>())
            {
                _existing.Add(Key(actionName, subactionPath));
            }
        }

        public bool DeviceExists(string actionName, string subactionPath) => _existing.Contains(Key(actionName, subactionPath));

        private static string Key(string actionName, string subactionPath) => $"{actionName}@{subactionPath}";
    }

    /// <summary>A thin sink over an OpenXR haptic action. Resolves a Core target through an
    /// explicit <see cref="HapticRouteMap"/>, then confirms the resolved action's device actually
    /// exists through an explicit <see cref="IOpenXrDeviceProbe"/>; either miss is reported, never
    /// silent.</summary>
    public sealed class OpenXrHapticActionSink : IHapticOutputSink
    {
        private readonly HapticRouteMap _routeMap;
        private readonly IOpenXrDeviceProbe _probe;
        private HapticSinkResult _lastFailure;

        public OpenXrHapticActionSink(HapticRouteMap routeMap, IOpenXrDeviceProbe probe)
        {
            _routeMap = routeMap ?? throw new ArgumentNullException(nameof(routeMap));
            _probe = probe ?? throw new ArgumentNullException(nameof(probe));
        }

        public HapticSinkResult Play(HapticTarget target, HapticKind kind, float amplitude, float durationMs, float? frequencyHz) =>
            TryResolveAction(target) ? HapticSinkResult.Ok() : _lastFailure;

        public HapticSinkResult Stop(HapticTarget target) => TryResolveAction(target) ? HapticSinkResult.Ok() : _lastFailure;

        public HapticSinkResult StopAll() => HapticSinkResult.Ok();

        private bool TryResolveAction(HapticTarget target)
        {
            if (!_routeMap.TryResolve(target, out var handle) || handle.Kind != HapticRouteKind.OpenXrAction)
            {
                _lastFailure = HapticSinkResult.Fail(HapticFailure.TargetUnknown, $"No OpenXR action route is mapped for target '{target}'.");
                return false;
            }
            if (!_probe.DeviceExists(handle.ActionName, handle.SubactionPath))
            {
                _lastFailure = HapticSinkResult.Fail(HapticUnityFailure.DeviceMissing, $"No device resolves for OpenXR action '{handle.ActionName}' at subaction path '{handle.SubactionPath}' for target '{target}'.");
                return false;
            }
            return true;
        }
    }

    /// <summary>Explicit, injected confirmation that an <see cref="InputActionReference"/> resolves
    /// to a live, dual-motor-rumble-capable device. A real implementation queries the bound
    /// control's device; a fixed fake reports whatever a test wires it to.</summary>
    public interface IInputSystemRumbleProbe
    {
        bool TryResolveRumbleDevice(InputActionReference actionReference, out string resolvedDeviceName);
    }

    public sealed class FixedInputSystemRumbleProbe : IInputSystemRumbleProbe
    {
        private readonly bool _resolves;
        private readonly string _deviceName;

        public FixedInputSystemRumbleProbe(bool resolves, string deviceName = "fake_rumble_device")
        {
            _resolves = resolves;
            _deviceName = deviceName ?? string.Empty;
        }

        public bool TryResolveRumbleDevice(InputActionReference actionReference, out string resolvedDeviceName)
        {
            resolvedDeviceName = _resolves ? _deviceName : string.Empty;
            return _resolves;
        }
    }

    /// <summary>The Input System rumble fallback route: an explicit, per-target
    /// <see cref="InputActionReference"/> map, resolved to a dual-motor-rumble-capable device
    /// through an explicit <see cref="IInputSystemRumbleProbe"/>. A reference that does not name a
    /// live action is <see cref="HapticUnityFailure.ActionMissing"/>; a resolved action with no
    /// rumble-capable device is <see cref="HapticUnityFailure.DeviceMissing"/>. Neither is silent.</summary>
    public sealed class InputSystemRumbleFallbackSink : IHapticOutputSink
    {
        private readonly IReadOnlyDictionary<HapticTarget, InputActionReference> _routes;
        private readonly IInputSystemRumbleProbe _probe;
        private HapticSinkResult _lastFailure;

        public InputSystemRumbleFallbackSink(IReadOnlyDictionary<HapticTarget, InputActionReference> routes, IInputSystemRumbleProbe probe)
        {
            _routes = routes ?? throw new ArgumentNullException(nameof(routes));
            _probe = probe ?? throw new ArgumentNullException(nameof(probe));
        }

        public HapticSinkResult Play(HapticTarget target, HapticKind kind, float amplitude, float durationMs, float? frequencyHz) =>
            TryResolveDevice(target) ? HapticSinkResult.Ok() : _lastFailure;

        public HapticSinkResult Stop(HapticTarget target) => TryResolveDevice(target) ? HapticSinkResult.Ok() : _lastFailure;

        public HapticSinkResult StopAll() => HapticSinkResult.Ok();

        private bool TryResolveDevice(HapticTarget target)
        {
            if (!_routes.TryGetValue(target, out var reference) || reference == null || reference.action == null)
            {
                _lastFailure = HapticSinkResult.Fail(HapticUnityFailure.ActionMissing, $"No live InputActionReference is mapped for target '{target}'.");
                return false;
            }
            if (!_probe.TryResolveRumbleDevice(reference, out _))
            {
                _lastFailure = HapticSinkResult.Fail(HapticUnityFailure.DeviceMissing, $"No dual-motor-rumble-capable device is bound to '{reference.action.name}' for target '{target}'.");
                return false;
            }
            return true;
        }
    }

    /// <summary>Explicit route selection as configuration, never platform detection inside the
    /// family: an ordered, explicitly-constructed list of sinks (for example XRI, then OpenXR,
    /// then the Input System rumble fallback) tried in that fixed order until one succeeds. Which
    /// sinks, and in which order, is entirely the consumer's own wiring; this type contains no
    /// runtime platform check and no build-target define.</summary>
    public sealed class OrderedFallbackHapticSink : IHapticOutputSink
    {
        private readonly IReadOnlyList<IHapticOutputSink> _sinksInOrder;

        public OrderedFallbackHapticSink(IReadOnlyList<IHapticOutputSink> sinksInOrder)
        {
            if (sinksInOrder == null || sinksInOrder.Count == 0)
            {
                throw new ArgumentException("At least one sink must be supplied, in explicit fallback order.", nameof(sinksInOrder));
            }
            _sinksInOrder = sinksInOrder;
        }

        public HapticSinkResult Play(HapticTarget target, HapticKind kind, float amplitude, float durationMs, float? frequencyHz) =>
            TryEach(sink => sink.Play(target, kind, amplitude, durationMs, frequencyHz));

        public HapticSinkResult Stop(HapticTarget target) => TryEach(sink => sink.Stop(target));

        public HapticSinkResult StopAll() => TryEach(sink => sink.StopAll());

        private HapticSinkResult TryEach(Func<IHapticOutputSink, HapticSinkResult> call)
        {
            var last = HapticSinkResult.Fail(HapticUnityFailure.DeviceMissing, "No sink in the ordered fallback list succeeded.");
            foreach (var sink in _sinksInOrder)
            {
                last = call(sink);
                if (last.Succeeded) return last;
            }
            return last;
        }
    }
}
