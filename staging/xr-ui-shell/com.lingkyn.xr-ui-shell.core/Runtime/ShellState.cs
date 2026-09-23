using System;
using System.Collections.Generic;
using System.Text;

namespace Lingkyn.XrUiShell.Core
{
    // Immutable shell state over one ShellLayout: the current anchor kind, open flag, docked
    // target, and follow flag of every declared surface, plus the one exclusively focused
    // surface (if any). Every accepted intent returns a new state; the prior state stays intact
    // and readable. The same intent sequence over the same layout and initial state always
    // produces an equal final state and an equal fingerprint.

    /// <summary>One declared surface's current placement: its current anchor kind (its home
    /// anchor until a dock or follow intent changes it), whether it is open, the surface it is
    /// docked to (if any), and whether it is following a target anchor kind.</summary>
    public sealed class SurfaceRuntimeState
    {
        internal SurfaceRuntimeState(SurfaceId id, AnchorKind currentAnchorKind, bool isOpen, SurfaceId? dockedTarget, bool isFollowing)
        {
            Id = id;
            CurrentAnchorKind = currentAnchorKind;
            IsOpen = isOpen;
            DockedTarget = dockedTarget;
            IsFollowing = isFollowing;
        }

        public SurfaceId Id { get; }
        public AnchorKind CurrentAnchorKind { get; }
        public bool IsOpen { get; }
        public SurfaceId? DockedTarget { get; }
        public bool IsFollowing { get; }

        internal string Fingerprint() =>
            $"{Id}:anchor={CurrentAnchorKind}:open={IsOpen}:dock={(DockedTarget.HasValue ? DockedTarget.Value.ToString() : "-")}:follow={IsFollowing}";
    }

    public sealed class ShellState : IEquatable<ShellState>
    {
        private readonly SortedDictionary<SurfaceId, SurfaceRuntimeState> _surfaces;

        private ShellState(ShellLayout layout, SortedDictionary<SurfaceId, SurfaceRuntimeState> surfaces, SurfaceId? focusedSurface)
        {
            Layout = layout;
            _surfaces = surfaces;
            FocusedSurface = focusedSurface;
        }

        public ShellLayout Layout { get; }
        public SurfaceId? FocusedSurface { get; }

        /// <summary>Every declared surface's current runtime placement, in canonical (kind, id)
        /// order.</summary>
        public IEnumerable<SurfaceRuntimeState> Surfaces => _surfaces.Values;

        /// <summary>The registered input sources, passed through from the layout.</summary>
        public IEnumerable<InputSourceRegistration> RegisteredSources => Layout.Sources;

        public bool TryGetSurfaceState(SurfaceId id, out SurfaceRuntimeState state) => _surfaces.TryGetValue(id, out state);

        /// <summary>The initial state: every declared surface at its home anchor, closed,
        /// undocked, not following, and nothing focused.</summary>
        public static ShellState Initial(ShellLayout layout)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            var surfaces = new SortedDictionary<SurfaceId, SurfaceRuntimeState>();
            foreach (var declaration in layout.Surfaces)
            {
                surfaces[declaration.Id] = new SurfaceRuntimeState(declaration.Id, declaration.Home, false, null, false);
            }
            return new ShellState(layout, surfaces, null);
        }

        public ShellResult<ShellState> Apply(ShellIntent intent)
        {
            if (intent == null) throw new ArgumentNullException(nameof(intent));
            return intent.ApplyTo(this);
        }

        /// <summary>Applies every intent in order. A rejected intent is recorded and the state it
        /// found is kept for the next intent in the sequence.</summary>
        public ShellSequenceResult ApplyAll(IEnumerable<ShellIntent> intents)
        {
            if (intents == null) throw new ArgumentNullException(nameof(intents));
            var state = this;
            var outcomes = new List<ShellIntentOutcome>();
            var index = 0;
            foreach (var intent in intents)
            {
                var result = state.Apply(intent);
                if (result.Succeeded)
                {
                    state = result.Value;
                    outcomes.Add(new ShellIntentOutcome(index, intent, true, result.Code, result.Message));
                }
                else
                {
                    outcomes.Add(new ShellIntentOutcome(index, intent, false, result.Code, result.Message));
                }
                index++;
            }
            return new ShellSequenceResult(state, outcomes);
        }

        internal ShellResult<ShellState> ApplyOpen(SurfaceId surface)
        {
            if (!TryGetSurfaceState(surface, out var current))
            {
                return ShellResult<ShellState>.Fail(ShellFailure.PanelUnknown, $"Surface '{surface}' is not declared.", surface.ToString());
            }
            return ShellResult<ShellState>.Ok(WithSurface(new SurfaceRuntimeState(surface, current.CurrentAnchorKind, true, current.DockedTarget, current.IsFollowing), FocusedSurface));
        }

        internal ShellResult<ShellState> ApplyClose(SurfaceId surface)
        {
            if (!TryGetSurfaceState(surface, out var current))
            {
                return ShellResult<ShellState>.Fail(ShellFailure.PanelUnknown, $"Surface '{surface}' is not declared.", surface.ToString());
            }
            var focus = FocusedSurface.HasValue && FocusedSurface.Value.Equals(surface) ? (SurfaceId?)null : FocusedSurface;
            return ShellResult<ShellState>.Ok(WithSurface(new SurfaceRuntimeState(surface, current.CurrentAnchorKind, false, current.DockedTarget, current.IsFollowing), focus));
        }

        internal ShellResult<ShellState> ApplyFocus(SurfaceId surface)
        {
            if (!TryGetSurfaceState(surface, out var current))
            {
                return ShellResult<ShellState>.Fail(ShellFailure.PanelUnknown, $"Surface '{surface}' is not declared.", surface.ToString());
            }
            if (FocusedSurface.HasValue && !FocusedSurface.Value.Equals(surface)
                && TryGetSurfaceState(FocusedSurface.Value, out var focusedState) && focusedState.IsOpen)
            {
                return ShellResult<ShellState>.Fail(ShellFailure.FocusConflict, $"Surface '{FocusedSurface.Value}' already holds exclusive focus while open.", surface.ToString());
            }
            return ShellResult<ShellState>.Ok(new ShellState(Layout, _surfaces, surface));
        }

        internal ShellResult<ShellState> ApplyDock(SurfaceId surface, SurfaceId target)
        {
            if (!Layout.TryGetSurface(surface, out var surfaceDeclaration) || !TryGetSurfaceState(surface, out var current))
            {
                return ShellResult<ShellState>.Fail(ShellFailure.PanelUnknown, $"Surface '{surface}' is not declared.", surface.ToString());
            }
            if (!Layout.TryGetSurface(target, out var targetDeclaration))
            {
                return ShellResult<ShellState>.Fail(ShellFailure.PanelUnknown, $"Dock target '{target}' is not declared.", target.ToString());
            }
            if (!surfaceDeclaration.Admits(targetDeclaration.Home))
            {
                return ShellResult<ShellState>.Fail(ShellFailure.AnchorKindUnsupported, $"Surface '{surface}' does not admit anchor kind '{targetDeclaration.Home}' (the dock target's home anchor).", surface.ToString());
            }
            return ShellResult<ShellState>.Ok(WithSurface(new SurfaceRuntimeState(surface, targetDeclaration.Home, current.IsOpen, target, current.IsFollowing), FocusedSurface));
        }

        internal ShellResult<ShellState> ApplyFollow(SurfaceId surface, AnchorKind targetAnchorKind)
        {
            if (!Layout.TryGetSurface(surface, out var surfaceDeclaration) || !TryGetSurfaceState(surface, out var current))
            {
                return ShellResult<ShellState>.Fail(ShellFailure.PanelUnknown, $"Surface '{surface}' is not declared.", surface.ToString());
            }
            if (!surfaceDeclaration.Admits(targetAnchorKind))
            {
                return ShellResult<ShellState>.Fail(ShellFailure.AnchorKindUnsupported, $"Surface '{surface}' does not admit anchor kind '{targetAnchorKind}'.", surface.ToString());
            }
            return ShellResult<ShellState>.Ok(WithSurface(new SurfaceRuntimeState(surface, targetAnchorKind, current.IsOpen, current.DockedTarget, true), FocusedSurface));
        }

        private ShellState WithSurface(SurfaceRuntimeState updated, SurfaceId? focusedSurface)
        {
            var copy = new SortedDictionary<SurfaceId, SurfaceRuntimeState>(_surfaces) { [updated.Id] = updated };
            return new ShellState(Layout, copy, focusedSurface);
        }

        /// <summary>A canonical text of the whole state: the layout fingerprint, the focused
        /// surface, and every surface's runtime placement in canonical order. Equal states,
        /// however reached, have equal fingerprints.</summary>
        public string Fingerprint()
        {
            var builder = new StringBuilder();
            builder.Append("layout[").Append(Layout.Fingerprint()).Append(']');
            builder.Append(" focused[").Append(FocusedSurface.HasValue ? FocusedSurface.Value.ToString() : "-").Append(']');
            builder.Append(" surfaces[");
            foreach (var pair in _surfaces)
            {
                builder.Append(pair.Value.Fingerprint()).Append(';');
            }
            builder.Append(']');
            return builder.ToString();
        }

        public bool Equals(ShellState other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return ReferenceEquals(Layout, other.Layout) && string.Equals(Fingerprint(), other.Fingerprint(), StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is ShellState other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Fingerprint());
        public override string ToString() => Fingerprint();
    }
}
