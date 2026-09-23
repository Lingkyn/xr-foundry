namespace Lingkyn.XrUiShell.Core
{
    // The ornament: exactly one shell-owned fixed surface every shell composition carries. It is
    // never declared by a consumer through ShellLayoutBuilder (it never enters a built
    // ShellLayout's declared-surface set, its SurfaceCount, or ShellState.Surfaces), and it is
    // never subject to a fold or unfold intent: FoldIntent and UnfoldIntent (ShellIntents.cs)
    // reject the ornament's identity with a stable code before they even look for a declared
    // surface, from any layout. This is the whole "one shell-owned fixed surface, never folded"
    // clause: the ornament carries no anchor kind of its own to admit or reject, no open/close
    // state, and no routing candidacy, because those all belong to the general panel, wrist-menu,
    // and hand-menu vocabulary this type deliberately stays outside of.

    /// <summary>The single shell-owned ornament identity and its constant canonical id. There is
    /// exactly one ornament; nothing builds a second one from consumer text.</summary>
    public static class ShellOrnament
    {
        /// <summary>The ornament's fixed canonical id text, in the same lower-case dotted form
        /// every other shell identity uses, but never round-tripped through
        /// <see cref="ShellIdentityText"/>: it is a compile-time constant, not consumer input.</summary>
        public const string CanonicalId = "shell.ornament";

        /// <summary>The ornament's identity as a <see cref="SurfaceId"/>, for the equality checks
        /// <see cref="ShellState.ApplyFold"/> and <see cref="ShellState.ApplyUnfold"/> make before
        /// consulting any declared layout.</summary>
        public static SurfaceId Id => SurfaceId.TheOrnament;
    }
}
