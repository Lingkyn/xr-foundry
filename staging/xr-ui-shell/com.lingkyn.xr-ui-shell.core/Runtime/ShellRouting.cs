using System;
using System.Collections.Generic;
using System.Linq;

namespace Lingkyn.XrUiShell.Core
{
    // Typed pointer and gaze routing over an already-built ShellState: hover, select, and scroll
    // each carry a registered InputSourceId and the candidate open-panel set the adapter
    // observed for that source. Routing is a pure read of the state; it never mutates it, so a
    // rejected routing intent changes nothing.

    /// <summary>Closed set of routing event kinds.</summary>
    public enum RouteEventKind
    {
        Hover,
        Select,
        Scroll,
    }

    /// <summary>One typed routing intent: the input source raising it and the candidate surface
    /// set the adapter observed for that source (for example every panel a raycaster's hits
    /// named). A source whose registered kind is gaze needs an explicit, separately registered
    /// commit source for <see cref="SelectIntent"/> and <see cref="ScrollIntent"/>, because the
    /// head ray is never the primary pointer.</summary>
    public abstract class RoutingIntent
    {
        private protected RoutingIntent(InputSourceId source, IReadOnlyList<SurfaceId> candidates)
        {
            Source = source;
            Candidates = candidates ?? Array.Empty<SurfaceId>();
        }

        public abstract RouteEventKind Kind { get; }
        public InputSourceId Source { get; }
        public IReadOnlyList<SurfaceId> Candidates { get; }

        /// <summary>The registered commit source for a gaze select or scroll. Always null for
        /// <see cref="HoverIntent"/>, and optional (but required when <see cref="Source"/> is a
        /// gaze source) for <see cref="SelectIntent"/> and <see cref="ScrollIntent"/>.</summary>
        public virtual InputSourceId? CommitSource => null;

        public override string ToString() => Candidates.Count == 0
            ? $"{Kind} from {Source} (no candidates)"
            : $"{Kind} from {Source} candidates=[{string.Join(",", Candidates)}]";
    }

    public sealed class HoverIntent : RoutingIntent
    {
        public HoverIntent(InputSourceId source, IReadOnlyList<SurfaceId> candidates) : base(source, candidates) { }
        public override RouteEventKind Kind => RouteEventKind.Hover;
    }

    public sealed class SelectIntent : RoutingIntent
    {
        public SelectIntent(InputSourceId source, IReadOnlyList<SurfaceId> candidates, InputSourceId? commitSource = null) : base(source, candidates)
        {
            _commitSource = commitSource;
        }

        private readonly InputSourceId? _commitSource;
        public override RouteEventKind Kind => RouteEventKind.Select;
        public override InputSourceId? CommitSource => _commitSource;
    }

    public sealed class ScrollIntent : RoutingIntent
    {
        public ScrollIntent(InputSourceId source, IReadOnlyList<SurfaceId> candidates, InputSourceId? commitSource = null) : base(source, candidates)
        {
            _commitSource = commitSource;
        }

        private readonly InputSourceId? _commitSource;
        public override RouteEventKind Kind => RouteEventKind.Scroll;
        public override InputSourceId? CommitSource => _commitSource;
    }

    /// <summary>Resolves a routing intent to exactly one open panel, or a named no-target result.
    /// Stateless and pure: it reads a <see cref="ShellState"/> and never mutates it.</summary>
    public static class ShellRouter
    {
        public static ShellResult<SurfaceId> Resolve(ShellState state, RoutingIntent intent)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (intent == null) throw new ArgumentNullException(nameof(intent));

            if (!state.Layout.TryGetSource(intent.Source, out var sourceRegistration))
            {
                return ShellResult<SurfaceId>.Fail(ShellFailure.SourceUnknown, $"Input source '{intent.Source}' is not registered.", intent.Source.ToString());
            }

            if ((intent.Kind == RouteEventKind.Select || intent.Kind == RouteEventKind.Scroll) && sourceRegistration.Kind == InputSourceKind.Gaze)
            {
                var commit = intent.CommitSource;
                if (!commit.HasValue || !state.Layout.TryGetSource(commit.Value, out _))
                {
                    return ShellResult<SurfaceId>.Fail(ShellFailure.SourceKindUnsupported, $"A {intent.Kind} from gaze source '{intent.Source}' needs a registered commit source; the head ray is not the primary pointer.", intent.Source.ToString());
                }
            }

            var openCandidates = new SortedSet<SurfaceId>();
            foreach (var candidate in intent.Candidates)
            {
                if (state.TryGetSurfaceState(candidate, out var candidateState) && candidateState.IsOpen)
                {
                    openCandidates.Add(candidate);
                }
            }

            if (state.FocusedSurface.HasValue && openCandidates.Contains(state.FocusedSurface.Value))
            {
                return ShellResult<SurfaceId>.Ok(state.FocusedSurface.Value);
            }
            if (openCandidates.Count == 0)
            {
                return ShellResult<SurfaceId>.Fail(ShellFailure.RouteNone, $"No open, declared candidate panel is available for {intent}.", intent.Source.ToString());
            }
            if (openCandidates.Count > 1)
            {
                return ShellResult<SurfaceId>.Fail(ShellFailure.RouteAmbiguous, $"More than one open candidate panel matches {intent}: [{string.Join(",", openCandidates)}].", intent.Source.ToString());
            }
            return ShellResult<SurfaceId>.Ok(openCandidates.Single());
        }
    }
}
