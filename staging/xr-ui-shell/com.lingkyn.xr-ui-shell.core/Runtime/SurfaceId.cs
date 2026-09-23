using System;

namespace Lingkyn.XrUiShell.Core
{
    // One declared shell surface, named generically as "panel" throughout the layout, intent,
    // and routing clauses of the verification contract: a panel, a wrist menu, or a hand menu.
    // SurfaceId carries the SurfaceKind alongside the canonical text so a PanelId "hud" and a
    // WristMenuId "hud" key two different surfaces, exactly as the identity clause's "no
    // cross-type equality" requires; it never accepts unvalidated text of its own.

    /// <summary>The declared-surface identity every placement intent and routing candidate names.
    /// Built only from an already-validated <see cref="PanelId"/>, <see cref="WristMenuId"/>, or
    /// <see cref="HandMenuId"/>.</summary>
    public readonly struct SurfaceId : IEquatable<SurfaceId>, IComparable<SurfaceId>
    {
        private SurfaceId(SurfaceKind kind, string value)
        {
            Kind = kind;
            Value = value;
        }

        public SurfaceKind Kind { get; }
        public string Value { get; }

        public static SurfaceId OfPanel(PanelId id) => new SurfaceId(SurfaceKind.Panel, id.Value);
        public static SurfaceId OfWristMenu(WristMenuId id) => new SurfaceId(SurfaceKind.WristMenu, id.Value);
        public static SurfaceId OfHandMenu(HandMenuId id) => new SurfaceId(SurfaceKind.HandMenu, id.Value);

        public bool Equals(SurfaceId other) => Kind == other.Kind && string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SurfaceId other && Equals(other);
        public override int GetHashCode() => ((int)Kind * 397) ^ (Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value));

        public int CompareTo(SurfaceId other)
        {
            var kindCompare = ((int)Kind).CompareTo((int)other.Kind);
            return kindCompare != 0 ? kindCompare : string.CompareOrdinal(Value, other.Value);
        }

        public override string ToString() => $"{Kind}:{Value}";
        public static bool operator ==(SurfaceId left, SurfaceId right) => left.Equals(right);
        public static bool operator !=(SurfaceId left, SurfaceId right) => !left.Equals(right);
    }
}
