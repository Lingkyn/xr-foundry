using System.Text;

namespace Lingkyn.XrUiShell.Core.Samples
{
    /// <summary>
    /// Domain-only walkthrough of the XR UI Shell Core: declaring a panel, a wrist menu, and a
    /// hand menu; a placement intent sequence (open, focus, open a second panel, dock it to the
    /// first) on an immutable ShellState; typed pointer routing for a ray hover and a gaze
    /// select, the gaze select first rejected for lacking a registered commit source and then
    /// resolved once one is supplied; the canonical skin mapping; and a replay that proves the
    /// final state is deterministic. No asset, scene, or UnityEngine API is involved.
    /// </summary>
    public static class ShellWalkthroughSample
    {
        public static string Run()
        {
            var report = new StringBuilder();

            // 1. Declare the shell surfaces and register the input sources.
            var builder = new ShellLayoutBuilder();
            var primary = PanelId.Parse("hud.primary");
            var secondary = PanelId.Parse("hud.secondary");
            var quickMenu = WristMenuId.Parse("quick_menu");
            var palmMenu = HandMenuId.Parse("palm_menu");
            builder.DeclarePanel(primary, AnchorKind.World, AnchorKind.HeadLocked);
            builder.DeclarePanel(secondary, AnchorKind.World, AnchorKind.HeadLocked);
            builder.DeclareWristMenu(quickMenu);
            builder.DeclareHandMenu(palmMenu);
            var rightRay = InputSourceId.Parse("right_ray");
            var headGaze = InputSourceId.Parse("head_gaze");
            builder.RegisterInputSource(rightRay, InputSourceKind.Ray);
            builder.RegisterInputSource(headGaze, InputSourceKind.Gaze);
            var layout = builder.Build();
            report.AppendLine($"Layout: {layout.SurfaceCount} surfaces, {layout.SourceCount} input sources.");

            var primarySurface = SurfaceId.OfPanel(primary);
            var secondarySurface = SurfaceId.OfPanel(secondary);

            // 2. Apply a placement intent sequence to an immutable ShellState.
            var sequence = new[]
            {
                new OpenIntent(primarySurface),
                new FocusIntent(primarySurface),
                new OpenIntent(secondarySurface),
                new DockIntent(secondarySurface, primarySurface),
            };
            var sequenceResult = ShellState.Initial(layout).ApplyAll(sequence);
            report.AppendLine($"Sequence all accepted: {sequenceResult.AllAccepted}");
            report.AppendLine($"Fingerprint after sequence: {sequenceResult.State.Fingerprint()}");

            // 3. Typed pointer and gaze routing: a ray hover resolves against the focused panel,
            //    and a gaze select fails closed without a registered commit source (the head ray
            //    is never the primary pointer) before resolving once one is supplied.
            var hoverResult = ShellRouter.Resolve(sequenceResult.State, new HoverIntent(rightRay, new[] { primarySurface, secondarySurface }));
            report.AppendLine(hoverResult.Succeeded
                ? $"Ray hover resolved to: {hoverResult.Value}"
                : $"Ray hover rejected: {hoverResult.Code}");

            var gazeSelectWithoutCommit = ShellRouter.Resolve(sequenceResult.State, new SelectIntent(headGaze, new[] { primarySurface }));
            report.AppendLine($"Gaze select without a commit source: {gazeSelectWithoutCommit.Code}");

            var gazeSelectWithCommit = ShellRouter.Resolve(sequenceResult.State, new SelectIntent(headGaze, new[] { primarySurface }, rightRay));
            report.AppendLine(gazeSelectWithCommit.Succeeded
                ? $"Gaze select with a commit source resolved to: {gazeSelectWithCommit.Value}"
                : $"Gaze select with a commit source rejected: {gazeSelectWithCommit.Code}");

            // 4. The canonical skin mapping resolves every closed slot to a shared token name.
            var mapping = CanonicalSkinMapping.Build();
            report.AppendLine($"Canonical skin maps 'panel.background' to: {DesignTokenNames.ToName(mapping.Resolve(ShellSlot.PanelBackground))}");

            // 5. Deterministic replay: the same sequence from a fresh initial state yields an
            //    equal fingerprint, which is what "deterministic replay" means in the
            //    verification contract.
            var replay = ShellState.Initial(layout).ApplyAll(sequence);
            report.AppendLine($"Replay fingerprint equals original: {replay.State.Fingerprint() == sequenceResult.State.Fingerprint()}");

            return report.ToString();
        }
    }
}
