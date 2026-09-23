namespace Lingkyn.LiveTuning.Unity
{
    // Stable codes for by-name or optional resolution failures the Unity adapter reports on its
    // own (a target the Core never sees: an asset reference, a member name, a file path). Every
    // per-tunable validation failure that the Core already names (tunable.unknown,
    // binding.kind.mismatch, binding.duplicate, and the others) keeps the Core's own code.

    public static class LiveTuningUnityFailure
    {
        /// <summary>A binding's target path does not resolve to a bound asset reference: the path
        /// does not parse as "asset_key#member", the asset key names no bound reference, or no
        /// binder declares the asset's type. Also carried by a device override file path that
        /// names no file: both are a by-name target that is missing (LESSON-004).</summary>
        public const string BindingTargetMissing = "binding.target.missing";
        /// <summary>A binding's member name is not in the binder's closed set for the asset's type.</summary>
        public const string BindingMemberUnknown = "binding.member.unknown";
        /// <summary>An export sink could not write its document: an unwritable device path, or an
        /// Editor asset that is not saved on disk.</summary>
        public const string ExportWriteFailed = "export.write.failed";
        /// <summary>A selection (a shell <c>SurfaceId</c> or an explicit target path) names a
        /// thing no binding record targets, through <see cref="Lingkyn.LiveTuning.Core.BindingIndex"/>.
        /// The selection scope still shows (zero slots); this diagnostic is never silence.</summary>
        public const string SelectionUnbound = "selection.unbound";
    }
}
