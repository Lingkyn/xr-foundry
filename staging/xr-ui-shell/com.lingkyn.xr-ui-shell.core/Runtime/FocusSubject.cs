using System;

namespace Lingkyn.XrUiShell.Core
{
    // FocusSubject: the shell's single current focus target for verb resolution
    // (VerbResolution.cs), distinct from ShellState.FocusedSurface, which only governs routing
    // preference among open panels for hover/select/scroll. FocusSubject is claimed only by an
    // explicit focus intent (the consumer calling Claim); nothing in this file reads a routing
    // result, a hover state, or any per-frame value and calls Claim on its own, so a per-frame
    // mirror or sync of hover/routing state never claims. Assignment always replaces the whole
    // value: Claim never merges with, or falls back to, whatever the previous target was, so no
    // target is special-cased. There is exactly one current target (a panel, a panel item within
    // a panel, or an external target path a consumer routes in) or none.

    /// <summary>The closed set of things a <see cref="FocusTarget"/> can name.</summary>
    public enum FocusTargetKind
    {
        Panel,
        PanelItem,
        External,
    }

    /// <summary>Exactly one focus target: a declared panel, a panel item within a declared panel
    /// (an opaque, consumer-defined item id inside that panel), or an external target path a
    /// consumer routes in (opaque text the Core never interprets, parses, or looks up). Immutable
    /// once built.</summary>
    public sealed class FocusTarget : IEquatable<FocusTarget>
    {
        private FocusTarget(FocusTargetKind kind, SurfaceId? panel, string itemId, string externalPath)
        {
            Kind = kind;
            Panel = panel;
            ItemId = itemId;
            ExternalPath = externalPath;
        }

        public FocusTargetKind Kind { get; }

        /// <summary>The declared panel this target names, for <see cref="FocusTargetKind.Panel"/>
        /// and <see cref="FocusTargetKind.PanelItem"/>; null for
        /// <see cref="FocusTargetKind.External"/>.</summary>
        public SurfaceId? Panel { get; }

        /// <summary>The opaque item id within <see cref="Panel"/>, for
        /// <see cref="FocusTargetKind.PanelItem"/> only; empty otherwise.</summary>
        public string ItemId { get; }

        /// <summary>The opaque external target path, for <see cref="FocusTargetKind.External"/>
        /// only; empty otherwise.</summary>
        public string ExternalPath { get; }

        public static FocusTarget OfPanel(SurfaceId panel) => new FocusTarget(FocusTargetKind.Panel, panel, string.Empty, string.Empty);

        public static FocusTarget OfPanelItem(SurfaceId panel, string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) throw new ArgumentException("A panel-item focus target needs a non-empty item id.", nameof(itemId));
            return new FocusTarget(FocusTargetKind.PanelItem, panel, itemId, string.Empty);
        }

        public static FocusTarget OfExternal(string targetPath)
        {
            if (string.IsNullOrEmpty(targetPath)) throw new ArgumentException("An external focus target needs a non-empty target path.", nameof(targetPath));
            return new FocusTarget(FocusTargetKind.External, null, string.Empty, targetPath);
        }

        public bool Equals(FocusTarget other) =>
            other != null
            && Kind == other.Kind
            && Nullable.Equals(Panel, other.Panel)
            && string.Equals(ItemId, other.ItemId, StringComparison.Ordinal)
            && string.Equals(ExternalPath, other.ExternalPath, StringComparison.Ordinal);

        public override bool Equals(object obj) => Equals(obj as FocusTarget);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Kind;
                hash = (hash * 397) ^ (Panel.HasValue ? Panel.Value.GetHashCode() : 0);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(ItemId ?? string.Empty);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(ExternalPath ?? string.Empty);
                return hash;
            }
        }

        public override string ToString()
        {
            switch (Kind)
            {
                case FocusTargetKind.Panel:
                    return Panel.HasValue ? Panel.Value.ToString() : string.Empty;
                case FocusTargetKind.PanelItem:
                    return Panel.HasValue ? $"{Panel.Value}#{ItemId}" : ItemId;
                case FocusTargetKind.External:
                    return ExternalPath;
                default:
                    return string.Empty;
            }
        }
    }

    /// <summary>Single-valued current focus subject: exactly one <see cref="FocusTarget"/> or
    /// none. The only two operations are <see cref="Claim"/> (an explicit new target) and
    /// <see cref="ClaimNone"/> (an explicit clear); both replace the whole value, and neither is
    /// ever invoked implicitly by reading <see cref="Current"/>, so repeatedly reading the
    /// current value from a per-frame loop never itself claims anything.</summary>
    public sealed class FocusSubject : IEquatable<FocusSubject>
    {
        private FocusSubject(FocusTarget current)
        {
            Current = current;
        }

        /// <summary>The one current target, or null when nothing is claimed.</summary>
        public FocusTarget Current { get; }

        public bool HasTarget => Current != null;

        /// <summary>The subject with no current target.</summary>
        public static FocusSubject None { get; } = new FocusSubject(null);

        /// <summary>Replaces the whole subject with an explicit claim of <paramref name="target"/>.
        /// This is the only way a FocusSubject's target changes to something non-null; there is
        /// no merge with, or precedence over, the value this replaces.</summary>
        public FocusSubject Claim(FocusTarget target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            return new FocusSubject(target);
        }

        /// <summary>Replaces the whole subject with an explicit claim of nothing. Clearing is
        /// itself an explicit claim, never an implicit side effect of routing, hover, or a
        /// per-frame sync.</summary>
        public FocusSubject ClaimNone() => None;

        public bool Equals(FocusSubject other) => other != null && Equals(Current, other.Current);
        public override bool Equals(object obj) => Equals(obj as FocusSubject);
        public override int GetHashCode() => Current?.GetHashCode() ?? 0;
    }
}
