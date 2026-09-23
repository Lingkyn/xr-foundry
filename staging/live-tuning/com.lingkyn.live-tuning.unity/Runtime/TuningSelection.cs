using System.Collections.Generic;
using Lingkyn.LiveTuning.Core;

namespace Lingkyn.LiveTuning.Unity
{
    // "Pick-to-tune through the shell's routing, never the family's own raycast": TuningPanelHost
    // accepts a selection from the XR UI shell's typed routing (a SurfaceId or an explicit target
    // path), maps it through the Core BindingIndex, and shows exactly those slots as a scope
    // named "selection". These two small types are the vocabulary that clause uses; the
    // Select/ClearSelection methods themselves live on TuningPanelHost, next to AttachAll,
    // WhereScope, and ChangedOnly, so a slot's editor kind is resolved in exactly one place.

    /// <summary>One named group of a host's slots: "all" is every attached slot, "selection" is
    /// exactly what the active selection's target path indexes to. A scope changes which slots
    /// are shown and never which editor a slot holds.</summary>
    public sealed class TuningScope
    {
        public const string AllScopeName = "all";
        public const string SelectionScopeName = "selection";

        internal TuningScope(string name, IReadOnlyList<TuningSlot> slots)
        {
            Name = name;
            Slots = slots;
        }

        public string Name { get; }
        public IReadOnlyList<TuningSlot> Slots { get; }
    }

    /// <summary>Why a selection shows zero slots: its target path matched no binding record
    /// through the Core <see cref="BindingIndex"/>. Reported alongside the (empty) selection
    /// scope, never left as a silent empty panel.</summary>
    public sealed class SelectionDiagnostic
    {
        public SelectionDiagnostic(string targetPath, string code, string message)
        {
            TargetPath = targetPath ?? string.Empty;
            Code = code;
            Message = message ?? string.Empty;
        }

        public string TargetPath { get; }
        public string Code { get; }
        public string Message { get; }
    }
}
