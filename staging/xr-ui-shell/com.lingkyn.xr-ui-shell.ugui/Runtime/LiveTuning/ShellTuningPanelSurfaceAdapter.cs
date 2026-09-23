using System;
using System.Collections.Generic;

namespace Lingkyn.XrUiShell.Ugui.LiveTuning
{
    // Adapts one bound XR UI Shell panel to the Live Tuning family's ITuningPanelSurface seam
    // (staging/live-tuning/com.lingkyn.live-tuning.unity/Runtime/TuningPanelHost.cs), so the Live
    // Tuning panel host can attach its per-tunable editor slots to a real shell panel without
    // either family referencing the other's concrete skin or panel type: this adapter names only
    // the shell's own UguiPanelBindingEntry and the Live Tuning host's own TuningSlot type.
    //
    // This package references Lingkyn.LiveTuning.Core and Lingkyn.LiveTuning.Unity directly
    // (see the asmdef): the reference is not circular (Live Tuning's own asmdefs reference only
    // its own Core, never this shell) and is small enough that the heavier fallback the work item
    // allows for — a small ITuningPanelSurface-compatible type kept in this same folder behind a
    // compile define, built without referencing Lingkyn.LiveTuning.Unity at all — was not needed.
    // This folder is kept separate so that fallback stays a one-file, one-define change if a
    // future dependency shape ever makes the direct reference circular or heavy.

    /// <summary>One shell panel acting as a Live Tuning panel surface. It only tracks which slots
    /// were added, exactly like the Live Tuning package's own <c>UguiFallbackPanelSurface</c>; a
    /// later real view renders each slot's editor inside <see cref="Panel"/>'s Canvas subtree the
    /// same way, still through this one seam. No Live Tuning value (a tunable, a binding, or a
    /// skin) is read or written here — only the closed <c>ITuningPanelSurface</c> contract.</summary>
    public sealed class ShellTuningPanelSurfaceAdapter : Lingkyn.LiveTuning.Unity.ITuningPanelSurface
    {
        private readonly List<Lingkyn.LiveTuning.Unity.TuningSlot> _slots = new List<Lingkyn.LiveTuning.Unity.TuningSlot>();

        public ShellTuningPanelSurfaceAdapter(UguiPanelBindingEntry panel)
        {
            Panel = panel ?? throw new ArgumentNullException(nameof(panel));
        }

        /// <summary>The bound shell panel this surface's slots belong to.</summary>
        public UguiPanelBindingEntry Panel { get; }

        public IReadOnlyList<Lingkyn.LiveTuning.Unity.TuningSlot> Slots => _slots;

        public void AddSlot(Lingkyn.LiveTuning.Unity.TuningSlot slot) => _slots.Add(slot ?? throw new ArgumentNullException(nameof(slot)));

        public void RemoveAllSlots() => _slots.Clear();
    }
}
