namespace Lingkyn.XrUiShell.Ugui
{
    // Stable codes the UGUI adapter itself reports, for a target the Core never sees: a Canvas
    // reference, its render mode, its event camera, its raycaster, the scene's XR UI input
    // module, or a wrist/hand anchor transform. Every per-surface validation failure the Core
    // already names (panel.unknown, anchor.kind.unsupported, and the others) keeps the Core's
    // own code; this adapter never invents a code the verification contract does not name.

    public static class UguiFailure
    {
        /// <summary>A declared panel has no bound Canvas reference.</summary>
        public const string SurfaceMissing = "surface.missing";
        /// <summary>A bound Canvas's render mode is not World Space.</summary>
        public const string SurfaceModeMismatch = "surface.mode.mismatch";
        /// <summary>The same Canvas is bound to two declared panels.</summary>
        public const string SurfaceDuplicate = "surface.duplicate";
        /// <summary>A bound Canvas has no event camera reference.</summary>
        public const string CameraMissing = "camera.missing";
        /// <summary>A bound Canvas has no raycaster reference.</summary>
        public const string RaycasterMissing = "raycaster.missing";
        /// <summary>The scene does not carry exactly one active XR UI input module reference.</summary>
        public const string InputModuleMissing = "input.module.missing";
        /// <summary>A panel that admits a wrist or hand anchor has no anchor transform reference.</summary>
        public const string AnchorTargetMissing = "anchor.target.missing";
    }
}
