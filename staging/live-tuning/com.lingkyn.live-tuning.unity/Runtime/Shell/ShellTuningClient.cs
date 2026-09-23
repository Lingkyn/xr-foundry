using System;
using System.Collections.Generic;
using Lingkyn.LiveTuning.Core;
using Lingkyn.XrUiShell.Core;

namespace Lingkyn.LiveTuning.Unity
{
    // Live tuning as a peer client of the XR UI shell, never a shell dependency (LESSON-010): the
    // shell's own assemblies reference nothing in Lingkyn.LiveTuning.*, and this file is the only
    // place live tuning references the shell, entirely through the shell's own renderer-neutral
    // Core (already referenced by this package's asmdef). This file:
    //   - registers the "tune" verb into the shell's VerbRegistry (VerbRegistry.cs) exactly the
    //     entry point any other peer family would use, never a shell-side special case;
    //   - adapts the shell's generic IShellPanelContent<TSlot> seam (ShellPanelContent.cs) to
    //     this package's own ITuningPanelSurface, so a real TuningPanelHost can attach its slots
    //     to a bound shell panel without the shell ever referencing TuningSlot; and
    //   - maps the shell's FocusSubject (FocusSubject.cs) to a TuningPanelHost scope through
    //     TuningPanelHost.Select, which itself maps it through the Core BindingIndex.
    // It holds no reference to a Canvas, UIDocument, camera, ray, or any other renderer or input
    // type: only the shell's own Core identities and this package's own TuningSlot.

    /// <summary>The live tuning family's verb: a constant id and display word it registers into
    /// the shell's registry like any peer. The shell itself never names "tune" and never calls
    /// this; a consumer's own composition code does, alongside every other family's verb
    /// registration.</summary>
    public static class TuningVerb
    {
        public const string Id = "tune";
        public const string DisplayWord = "Tune";

        /// <summary>Registers the "tune" verb on <paramref name="builder"/>. Rejects a duplicate
        /// registration with the Core's own <see cref="ShellFailure.VerbDuplicate"/>, exactly as
        /// registering any other verb would.</summary>
        public static ShellResult<VerbRegistryBuilder> Register(VerbRegistryBuilder builder, VerbWireState wireState)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            return builder.Register(VerbId.Parse(Id), DisplayWord, wireState);
        }
    }

    /// <summary>One shell panel acting as a live tuning panel surface: adapts the shell's generic
    /// <see cref="IShellPanelContent{TSlot}"/> seam to this package's own
    /// <see cref="ITuningPanelSurface"/>, so a real <see cref="TuningPanelHost"/> can attach its
    /// slots to a bound shell panel. It only tracks which slots were added, exactly like
    /// <see cref="UguiFallbackPanelSurface"/>; a later real view renders each slot's editor inside
    /// the bound panel the same way, still through this one seam. No shell value beyond the
    /// panel's own <see cref="SurfaceId"/> is read or written here.</summary>
    public sealed class ShellTuningClient : IShellPanelContent<TuningSlot>, ITuningPanelSurface
    {
        private readonly List<TuningSlot> _slots = new List<TuningSlot>();

        public ShellTuningClient(SurfaceId panel)
        {
            Panel = panel;
        }

        /// <summary>The shell panel identity this client's slots belong to. The shell resolves
        /// that panel's anchor, open, close, focus, dock, and follow state and which pointer or
        /// gaze source targets it; this client never reads any of that itself.</summary>
        public SurfaceId Panel { get; }

        public IReadOnlyList<TuningSlot> Slots => _slots;

        public void AddSlot(TuningSlot slot) => _slots.Add(slot ?? throw new ArgumentNullException(nameof(slot)));
        public void RemoveAllSlots() => _slots.Clear();

        /// <summary>Maps the shell's current <see cref="FocusSubject"/> to a scope on
        /// <paramref name="host"/> through <see cref="TuningPanelHost.Select(string)"/> or
        /// <see cref="TuningPanelHost.Select(SurfaceId)"/>, and so through the Core
        /// <see cref="BindingIndex"/>: a panel target selects by that panel's own id, a panel-item
        /// target selects by "panel#item" (the same "asset_key#member" shaped opaque path
        /// convention <see cref="BindingIndex"/> already documents), an external target selects
        /// by its own opaque path, and no current target restores the host's previous scope. This
        /// is the whole "receives the focus target through the shell's FocusSubject" seam: it
        /// performs no raycast, reads no pose, and holds no camera, ray, or input-device
        /// reference.</summary>
        public static TuningScope ApplyFocus(TuningPanelHost host, FocusSubject subject)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (subject == null) throw new ArgumentNullException(nameof(subject));

            if (!subject.HasTarget) return host.ClearSelection();

            switch (subject.Current.Kind)
            {
                case FocusTargetKind.Panel:
                    return host.Select(subject.Current.Panel.Value);
                case FocusTargetKind.PanelItem:
                case FocusTargetKind.External:
                    return host.Select(subject.Current.ToString());
                default:
                    return host.ClearSelection();
            }
        }
    }
}
