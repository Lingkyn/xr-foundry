using System;
using System.Collections.Generic;
using System.Linq;
using Lingkyn.LiveTuning.Core;

namespace Lingkyn.LiveTuning.Unity
{
    // "Two view presets over one host, never two panels": designer and engineer are both read
    // over the one TuningPanelHost's own slots. Neither preset raises an intent, adds a slot, or
    // holds a second copy of a slot's editor; each is a fresh, disposable display-descriptor
    // object built from the host's current state, since no rendering exists yet to hold a
    // "current preset" of its own. Switching a preset changes only what TuningView.Describe
    // returns for the next call; it never touches the host, the runtime, or the registry.

    /// <summary>The closed set of view presets. Designer is the everyday tuning view; engineer
    /// adds the plumbing detail (range, step, target path, export destination, diagnostics) a
    /// person debugging a binding needs. Neither can mutate a registered range, step, or value
    /// set: no API on this type, or on <see cref="TuningSlotView"/>, exposes a setter for one.</summary>
    public enum TuningViewPreset
    {
        Designer,
        Engineer,
    }

    /// <summary>One slot's display descriptor for one preset. Designer carries the label and
    /// editor kind (plus a per-slot reset, always available); engineer carries the same two
    /// fields and adds the registered range/step summary, the binding's target path, and the
    /// destination the runtime's export sink reports. Every field here is read-only.</summary>
    public sealed class TuningSlotView
    {
        private TuningSlotView(TuningViewPreset preset, TunableId id, string label, EditorKind editorKind, bool canReset, string rangeSummary, string targetPath, string exportDestination)
        {
            Preset = preset;
            Id = id;
            Label = label ?? string.Empty;
            EditorKind = editorKind;
            CanReset = canReset;
            RangeSummary = rangeSummary ?? string.Empty;
            TargetPath = targetPath ?? string.Empty;
            ExportDestination = exportDestination ?? string.Empty;
        }

        public TuningViewPreset Preset { get; }
        public TunableId Id { get; }
        public string Label { get; }
        public EditorKind EditorKind { get; }
        /// <summary>Shown in both presets: every slot always carries its own reset control.</summary>
        public bool CanReset { get; }
        /// <summary>Engineer-only: the registered range and step, textually. Empty in designer.</summary>
        public string RangeSummary { get; }
        /// <summary>Engineer-only: the binding's own opaque target path. Empty in designer.</summary>
        public string TargetPath { get; }
        /// <summary>Engineer-only: the destination the runtime's export sink reports. Empty in designer.</summary>
        public string ExportDestination { get; }

        internal static TuningSlotView ForDesigner(TunableId id, string label, EditorKind editorKind) =>
            new TuningSlotView(TuningViewPreset.Designer, id, label, editorKind, true, string.Empty, string.Empty, string.Empty);

        internal static TuningSlotView ForEngineer(TunableId id, string label, EditorKind editorKind, string rangeSummary, string targetPath, string exportDestination) =>
            new TuningSlotView(TuningViewPreset.Engineer, id, label, editorKind, true, rangeSummary, targetPath, exportDestination);
    }

    /// <summary>One host's display descriptor for one preset: every slot's descriptor, in the
    /// host's own slot order, plus (engineer only) every diagnostic the host recorded, each still
    /// carrying its own stable code.</summary>
    public sealed class TuningHostView
    {
        internal TuningHostView(TuningViewPreset preset, IReadOnlyList<TuningSlotView> slots, IReadOnlyList<TuningSlotDiagnostic> diagnostics)
        {
            Preset = preset;
            Slots = slots;
            Diagnostics = diagnostics;
        }

        public TuningViewPreset Preset { get; }
        public IReadOnlyList<TuningSlotView> Slots { get; }
        /// <summary>Engineer-only: every <see cref="TuningSlotDiagnostic"/> this host recorded at
        /// attach time, each with its stable code. Empty in designer.</summary>
        public IReadOnlyList<TuningSlotDiagnostic> Diagnostics { get; }
    }

    /// <summary>Builds a read-only display descriptor for a host under one preset. A pure function
    /// of the host's current slots and diagnostics: it raises no intent, adds no slot, and writes
    /// nothing through the export sink (it only asks the sink to describe its own destination).</summary>
    public static class TuningView
    {
        public static TuningHostView Describe(TuningPanelHost host, TuningViewPreset preset)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            var slots = host.Slots.Select(slot => DescribeSlot(slot, preset, host.Runtime.ExportSink)).ToList();
            var diagnostics = preset == TuningViewPreset.Engineer
                ? host.Diagnostics
                : (IReadOnlyList<TuningSlotDiagnostic>)Array.Empty<TuningSlotDiagnostic>();
            return new TuningHostView(preset, slots, diagnostics);
        }

        private static TuningSlotView DescribeSlot(TuningSlot slot, TuningViewPreset preset, ITuningExportSink exportSink)
        {
            if (preset == TuningViewPreset.Designer)
            {
                return TuningSlotView.ForDesigner(slot.Id, slot.Label, slot.Editor.Kind);
            }
            return TuningSlotView.ForEngineer(
                slot.Id,
                slot.Label,
                slot.Editor.Kind,
                DescribeRange(slot.Registration.Declaration),
                slot.Binding.Record.TargetPath,
                exportSink.DescribeDestination());
        }

        /// <summary>A textual summary of a declaration's own registered range and step. Reads only
        /// the public range/step properties each closed-set declaration type already exposes;
        /// never a reflection scan, and never a value this or any other code can write back.</summary>
        private static string DescribeRange(TunableKindDeclaration declaration)
        {
            switch (declaration)
            {
                case FloatDeclaration f: return $"[{Text(f.Min)},{Text(f.Max)}] step {Text(f.Step)}";
                case IntegerDeclaration i: return $"[{i.Min},{i.Max}]";
                case Vector2Declaration v2: return $"x[{Text(v2.MinX)},{Text(v2.MaxX)}] y[{Text(v2.MinY)},{Text(v2.MaxY)}]";
                case Vector3Declaration v3: return $"x[{Text(v3.MinX)},{Text(v3.MaxX)}] y[{Text(v3.MinY)},{Text(v3.MaxY)}] z[{Text(v3.MinZ)},{Text(v3.MaxZ)}]";
                case EnumeratedDeclaration e: return "{" + string.Join(",", e.Values) + "}";
                case ColourDeclaration _: return "[0,1] per channel";
                case BoolDeclaration _: return string.Empty;
                default: return string.Empty;
            }
        }

        private static string Text(float value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
