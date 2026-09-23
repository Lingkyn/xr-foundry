using System;
using System.Collections.Generic;
using System.Linq;
using Lingkyn.LiveTuning.Core;
using Lingkyn.XrUiShell.Core;

namespace Lingkyn.LiveTuning.Unity
{
    // A tuning panel host with one slot per bound tunable, resolving each slot's editor by kind
    // at attach time through ResolveEditorKind. The host contains no per-family, per-package,
    // per-skin, or per-screen code path: attaching bindings from two different skin types runs
    // the same attach path for both. Every editor change raises exactly one Core set intent
    // through the host and writes to no target directly; a rejected intent restores the editor
    // to the effective value. Scope filtering is metadata only: it changes which slots a view
    // lists and never which editor a slot holds. The host's own look comes from the surface it is
    // given, never from the tunables it edits.
    //
    // Pick-to-tune (see TuningSelection.cs): the host accepts a selection from the XR UI shell's
    // typed routing, a SurfaceId or an explicit opaque target path, maps it through the Core
    // BindingIndex to the bindings targeting that thing, and shows exactly those slots as a scope
    // named "selection". The host never raycasts, reads a pose, or holds a camera, ray, or input
    // device reference; a source rule in the test assembly proves the Runtime assembly references
    // none of those types.

    /// <summary>One hosted slot: the resolved binding, its registration (for the group and label
    /// the host displays), and the attached editor control.</summary>
    public sealed class TuningSlot
    {
        internal TuningSlot(ResolvedBinding binding, TunableRegistration registration, ITuningEditorControl editor)
        {
            Binding = binding;
            Registration = registration;
            Editor = editor;
        }

        public ResolvedBinding Binding { get; }
        public TunableRegistration Registration { get; }
        public ITuningEditorControl Editor { get; }
        public TunableId Id => Registration.Id;
        public string Group => Registration.Group;
        public string Label => Registration.Label;
    }

    /// <summary>Why a binding attached no slot: a kind with no implemented editor, carrying the
    /// offending tunable's id.</summary>
    public sealed class TuningSlotDiagnostic
    {
        public TuningSlotDiagnostic(TunableId id, string code, string message)
        {
            Id = id;
            Code = code;
            Message = message ?? string.Empty;
        }

        public TunableId Id { get; }
        public string Code { get; }
        public string Message { get; }
    }

    /// <summary>The XR UI shell seam the panel host is a client of. The shell owns identity,
    /// anchor, open, close, focus, dock, follow state, pointer routing, and its own skin; the
    /// host owns only the slots and editors it adds through this surface. A minimal UGUI
    /// world-space fallback (<see cref="UguiFallbackPanelSurface"/>) satisfies the same contract
    /// until the shell's own panel surface exists, and is swapped in without a host, editor, or
    /// binding change.</summary>
    public interface ITuningPanelSurface
    {
        void AddSlot(TuningSlot slot);
        void RemoveAllSlots();
    }

    /// <summary>A minimal UGUI world-space fallback surface. It only tracks which slots were
    /// added, with no scene, prefab, or singleton of its own; a real fallback renders each slot's
    /// editor in world space the same way, still through this one seam.</summary>
    public sealed class UguiFallbackPanelSurface : ITuningPanelSurface
    {
        private readonly List<TuningSlot> _slots = new List<TuningSlot>();

        public IReadOnlyList<TuningSlot> Slots => _slots;

        public void AddSlot(TuningSlot slot) => _slots.Add(slot ?? throw new ArgumentNullException(nameof(slot)));
        public void RemoveAllSlots() => _slots.Clear();
    }

    public sealed class TuningPanelHost
    {
        private readonly List<TuningSlot> _slots = new List<TuningSlot>();
        private readonly List<TuningSlotDiagnostic> _diagnostics = new List<TuningSlotDiagnostic>();
        private readonly Dictionary<TunableId, TuningSlot> _slotsById = new Dictionary<TunableId, TuningSlot>();
        private readonly BindingIndex _bindingIndex;

        private TuningScope _activeScope;
        private TuningScope _previousScope;
        private SelectionDiagnostic _selectionDiagnostic;

        public TuningPanelHost(TuningRuntime runtime, ITuningPanelSurface surface)
        {
            Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            Surface = surface ?? throw new ArgumentNullException(nameof(surface));

            // The Core BindingIndex is built once, from exactly the records the runtime's own
            // SkinBindingSet already resolved and validated. Re-validating them here can only
            // fail from a defect in that earlier validation, since SkinBindingSet.Create never
            // hands out a set holding a record the Core itself would reject.
            var validated = BindingSet.Validate(Runtime.Bindings.Bindings.Select(binding => binding.Record), Runtime.Registry);
            if (!validated.Succeeded)
            {
                throw new InvalidOperationException($"Live tuning binding index construction failed unexpectedly: [{validated.Code}] {validated.Message}");
            }
            _bindingIndex = BindingIndex.Build(validated.Value);
        }

        public TuningRuntime Runtime { get; }
        public ITuningPanelSurface Surface { get; }
        public IReadOnlyList<TuningSlot> Slots => _slots;
        public IReadOnlyList<TuningSlotDiagnostic> Diagnostics => _diagnostics;

        /// <summary>The scope currently shown: "all" until a selection is active, "selection"
        /// while one is. Switching a view preset (see TuningView.cs) never changes this.</summary>
        public TuningScope ActiveScope => _activeScope ?? AllScope();

        /// <summary>Set only while the active scope is "selection" and its target path matched no
        /// binding record; null otherwise. Never silence: a selection nothing targets still shows
        /// (as zero slots) and reports this diagnostic with the path.</summary>
        public SelectionDiagnostic ActiveSelectionDiagnostic => _selectionDiagnostic;

        /// <summary>Creates exactly one slot per validated binding, in canonical id order, and
        /// resolves each slot's editor by kind at attach time. This method is the host's whole
        /// attach path: it runs identically no matter which package, skin, or screen a binding's
        /// scope metadata names.</summary>
        public void AttachAll()
        {
            Surface.RemoveAllSlots();
            _slots.Clear();
            _slotsById.Clear();
            _diagnostics.Clear();
            _activeScope = null;
            _previousScope = null;
            _selectionDiagnostic = null;
            foreach (var binding in Runtime.Bindings.Bindings)
            {
                if (!Runtime.Registry.TryGet(binding.Id, out var registration)) continue;
                var editorResult = TuningEditorFactory.Create(registration.Kind);
                if (!editorResult.Succeeded)
                {
                    _diagnostics.Add(new TuningSlotDiagnostic(binding.Id, editorResult.Code, $"Tunable '{binding.Id}': {editorResult.Message}"));
                    continue;
                }
                Runtime.State.TryGetEffective(binding.Id, out var effective);
                var slot = new TuningSlot(binding, registration, editorResult.Value);
                editorResult.Value.Attach(registration, effective, value => OnEditorChanged(slot, value));
                _slots.Add(slot);
                _slotsById[slot.Id] = slot;
                Surface.AddSlot(slot);
            }
        }

        private void OnEditorChanged(TuningSlot slot, TunableValue value)
        {
            var outcome = Runtime.Apply(new SetIntent(slot.Id, value));
            if (!outcome.Accepted)
            {
                Runtime.State.TryGetEffective(slot.Id, out var effective);
                slot.Editor.SetValue(effective);
            }
        }

        public void Reset(TunableId id) => Runtime.Apply(new ResetIntent(id));
        public void ResetAll() => Runtime.Apply(new ResetAllIntent());

        /// <summary>Selects the thing a shell surface names: maps <paramref name="surfaceId"/>'s
        /// own text through the Core <see cref="BindingIndex"/> exactly as <see cref="Select(string)"/>
        /// does. The host never resolves a SurfaceId any other way (no raycast, no scene search):
        /// this is the whole "pick-to-tune through the shell's routing" seam.</summary>
        public TuningScope Select(SurfaceId surfaceId) => Select(surfaceId.ToString());

        /// <summary>Selects an explicit opaque target path. Shows exactly the slots the Core
        /// <see cref="BindingIndex"/> indexes to that path as a scope named "selection"; a path no
        /// binding targets still switches to that (empty) scope and sets
        /// <see cref="ActiveSelectionDiagnostic"/> to <c>selection.unbound</c> rather than leaving
        /// the panel silently empty. Selecting changes which slots are shown and never which
        /// editor a shown slot holds. Remembers exactly the scope that was active before this
        /// call, for <see cref="ClearSelection"/> to restore.</summary>
        public TuningScope Select(string targetPath)
        {
            if (targetPath == null) throw new ArgumentNullException(nameof(targetPath));

            var matches = _bindingIndex.Lookup(targetPath);
            _selectionDiagnostic = matches.Count == 0
                ? new SelectionDiagnostic(targetPath, LiveTuningUnityFailure.SelectionUnbound, $"No binding targets '{targetPath}'.")
                : null;

            var slots = new List<TuningSlot>(matches.Count);
            foreach (var record in matches)
            {
                if (_slotsById.TryGetValue(record.Id, out var slot)) slots.Add(slot);
            }

            _previousScope = _activeScope ?? AllScope();
            _activeScope = new TuningScope(TuningScope.SelectionScopeName, slots);
            return _activeScope;
        }

        /// <summary>Restores whichever scope was active immediately before the current selection,
        /// clearing the selection diagnostic along with it. Restores the host's baseline "all"
        /// scope when there was no earlier scope to return to.</summary>
        public TuningScope ClearSelection()
        {
            _activeScope = _previousScope ?? AllScope();
            _previousScope = null;
            _selectionDiagnostic = null;
            return _activeScope;
        }

        private TuningScope AllScope() => new TuningScope(TuningScope.AllScopeName, new List<TuningSlot>(_slots));

        /// <summary>Exactly the slots whose tunable currently holds an override.</summary>
        public IEnumerable<TuningSlot> ChangedOnly() => _slots.Where(slot => Runtime.State.HasOverride(slot.Id));

        /// <summary>Exactly the slots whose binding record carries <paramref name="value"/> for
        /// <paramref name="key"/> in its scope metadata. Metadata-only: it never changes which
        /// editor a returned slot holds.</summary>
        public IEnumerable<TuningSlot> WhereScope(string key, string value) =>
            _slots.Where(slot => slot.Binding.Record.TryGetScope(key, out var found) && string.Equals(found, value, StringComparison.Ordinal));
    }
}
