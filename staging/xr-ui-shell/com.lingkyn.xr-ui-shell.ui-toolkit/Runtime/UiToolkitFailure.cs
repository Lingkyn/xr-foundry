namespace Lingkyn.XrUiShell.UiToolkit
{
    // Stable codes the UI Toolkit adapter itself reports, for a target the Core never sees: a
    // UIDocument reference, its PanelSettings render mode, its collider and the collider's
    // update mode, the scene's active XR UI Toolkit manager reference, or a wrist/hand anchor
    // transform. Every per-surface validation failure the Core already names (panel.unknown,
    // anchor.kind.unsupported, and the others) keeps the Core's own code; this adapter never
    // invents a code the verification contract does not name. Shared code names with the UGUI
    // sibling (surface.missing, surface.mode.mismatch, surface.duplicate, input.module.missing,
    // anchor.target.missing) are the verification contract's own words for the same shape of
    // failure against a different renderer's types; no behaviour or evidence is shared.

    public static class UiToolkitFailure
    {
        /// <summary>A declared panel has no bound UIDocument reference.</summary>
        public const string SurfaceMissing = "surface.missing";
        /// <summary>A bound document's PanelSettings render mode is not World Space.</summary>
        public const string SurfaceModeMismatch = "surface.mode.mismatch";
        /// <summary>The same UIDocument is bound to two declared panels.</summary>
        public const string SurfaceDuplicate = "surface.duplicate";
        /// <summary>A bound document has no collider reference, or its collider update mode is
        /// outside the closed set this composition admits.</summary>
        public const string ColliderMissing = "collider.missing";
        /// <summary>The scene does not carry exactly one active XR UI Toolkit manager reference.</summary>
        public const string InputModuleMissing = "input.module.missing";
        /// <summary>The active XR UI Toolkit manager reports that it bypasses UI Toolkit events.</summary>
        public const string InputBypassed = "input.bypassed";
        /// <summary>A panel that admits a wrist or hand anchor has no anchor transform reference.</summary>
        public const string AnchorTargetMissing = "anchor.target.missing";
    }
}
