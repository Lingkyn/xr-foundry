using System;
using System.Collections.Generic;
using Lingkyn.Haptics.Core;

namespace Lingkyn.Haptics.Unity
{
    // Explicit target resolution from a Core HapticTarget to one concrete route handle (an XR
    // Interaction Toolkit haptic impulse channel, or an OpenXR action-and-subaction-path pair)
    // through an explicit, injected mapping — never a scene search or a name-matching scan of
    // active input devices (LESSON-004). The closed non-device targets (left, right, both) must
    // resolve at construction time; a named_device target resolves lazily, by the device id the
    // active Core profile declares.

    /// <summary>The two haptic-output routes this adapter resolves a target to.</summary>
    public enum HapticRouteKind
    {
        XriImpulseChannel,
        OpenXrAction,
    }

    /// <summary>One concrete route handle: either an XR Interaction Toolkit haptic impulse channel
    /// name, or an OpenXR action name and subaction path. A plain, opaque description — this
    /// adapter never resolves it by scene search or a name-matching scan.</summary>
    public readonly struct HapticRouteHandle
    {
        private HapticRouteHandle(HapticRouteKind kind, string channelName, string actionName, string subactionPath)
        {
            Kind = kind;
            ChannelName = channelName ?? string.Empty;
            ActionName = actionName ?? string.Empty;
            SubactionPath = subactionPath ?? string.Empty;
        }

        public HapticRouteKind Kind { get; }
        /// <summary>Meaningful only when <see cref="Kind"/> is <see cref="HapticRouteKind.XriImpulseChannel"/>.</summary>
        public string ChannelName { get; }
        /// <summary>Meaningful only when <see cref="Kind"/> is <see cref="HapticRouteKind.OpenXrAction"/>.</summary>
        public string ActionName { get; }
        /// <summary>Meaningful only when <see cref="Kind"/> is <see cref="HapticRouteKind.OpenXrAction"/>.</summary>
        public string SubactionPath { get; }

        public static HapticRouteHandle XriChannel(string channelName) => new HapticRouteHandle(HapticRouteKind.XriImpulseChannel, channelName, string.Empty, string.Empty);

        public static HapticRouteHandle OpenXrAction(string actionName, string subactionPath) => new HapticRouteHandle(HapticRouteKind.OpenXrAction, string.Empty, actionName, subactionPath);

        public string RouteName => Kind == HapticRouteKind.XriImpulseChannel ? "xri_impulse_channel" : "openxr_action";

        public override string ToString() => Kind == HapticRouteKind.XriImpulseChannel ? $"xri:{ChannelName}" : $"openxr:{ActionName}@{SubactionPath}";
    }

    /// <summary>One route-resolution failure: a stable code, the target it concerns, the route
    /// name, and a human message. Never silent (LESSON-004).</summary>
    public sealed class HapticRouteDiagnostic
    {
        public HapticRouteDiagnostic(string code, HapticTarget target, string routeName, string message)
        {
            Code = code;
            Target = target;
            RouteName = routeName ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Code { get; }
        public HapticTarget Target { get; }
        public string RouteName { get; }
        public string Message { get; }
    }

    public sealed class HapticRouteReport
    {
        public HapticRouteReport(IReadOnlyList<HapticRouteDiagnostic> diagnostics)
        {
            Diagnostics = diagnostics;
        }

        public IReadOnlyList<HapticRouteDiagnostic> Diagnostics { get; }
        public bool IsValid => Diagnostics.Count == 0;
    }

    public sealed class HapticRouteException : Exception
    {
        public HapticRouteException(HapticRouteReport report) : base(Describe(report))
        {
            Report = report;
        }

        public HapticRouteReport Report { get; }

        private static string Describe(HapticRouteReport report)
        {
            var lines = new List<string>(report.Diagnostics.Count + 1) { "Haptic route mapping is invalid:" };
            foreach (var diagnostic in report.Diagnostics)
            {
                lines.Add($"  [{diagnostic.Code}] {diagnostic.Target} ({diagnostic.RouteName}): {diagnostic.Message}");
            }
            return string.Join("\n", lines);
        }
    }

    /// <summary>The explicit, injected mapping from a Core <see cref="HapticTarget"/> to one
    /// concrete <see cref="HapticRouteHandle"/>. <see cref="HapticTarget.Left"/>,
    /// <see cref="HapticTarget.Right"/>, and <see cref="HapticTarget.Both"/> must resolve at
    /// construction time; a <see cref="HapticTargetKind.NamedDevice"/> target may be declared here
    /// too, but a target not declared resolves lazily to a reported failure rather than throwing.</summary>
    public sealed class HapticRouteMap
    {
        private readonly Dictionary<HapticTarget, HapticRouteHandle> _routes;

        private HapticRouteMap(Dictionary<HapticTarget, HapticRouteHandle> routes)
        {
            _routes = routes;
        }

        /// <summary>Validates that <see cref="HapticTarget.Left"/>, <see cref="HapticTarget.Right"/>,
        /// and <see cref="HapticTarget.Both"/> each resolve to a route handle in
        /// <paramref name="routes"/>. Each missing entry is reported with
        /// <see cref="HapticFailure.TargetUnknown"/>, the target, and a human route-kind name.</summary>
        public static HapticRouteReport Validate(IReadOnlyDictionary<HapticTarget, HapticRouteHandle> routes)
        {
            if (routes == null) throw new ArgumentNullException(nameof(routes));
            var diagnostics = new List<HapticRouteDiagnostic>();
            foreach (var required in new[] { HapticTarget.Left, HapticTarget.Right, HapticTarget.Both })
            {
                if (!routes.ContainsKey(required))
                {
                    diagnostics.Add(new HapticRouteDiagnostic(HapticFailure.TargetUnknown, required, "route_map", $"No route handle is mapped for the closed-set target '{required}'."));
                }
            }
            return new HapticRouteReport(diagnostics);
        }

        /// <summary>Builds the route map. Throws <see cref="HapticRouteException"/> with the same
        /// report a failed <see cref="Validate"/> call would return.</summary>
        public static HapticRouteMap Create(IReadOnlyDictionary<HapticTarget, HapticRouteHandle> routes)
        {
            var report = Validate(routes);
            if (!report.IsValid) throw new HapticRouteException(report);
            return new HapticRouteMap(new Dictionary<HapticTarget, HapticRouteHandle>(routes));
        }

        /// <summary>Resolves one target to its route handle. False (never a throw) for a target
        /// this map does not declare, most commonly a <see cref="HapticTargetKind.NamedDevice"/>
        /// target the active Core profile declares but this consumer never wired a route for.</summary>
        public bool TryResolve(HapticTarget target, out HapticRouteHandle handle) => _routes.TryGetValue(target, out handle);
    }
}
