using System;
using System.Collections.Generic;
using System.Linq;
using Lingkyn.XrUiShell.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lingkyn.XrUiShell.UiToolkit
{
    // A thin binding of each Core-declared panel to exactly one world-space UIDocument. Every
    // reference (the document, its collider, a wrist/hand anchor transform, the skin target, and
    // the routed-event target) is explicit; nothing here is resolved by name, by scene search, or
    // by reflection. The active XR UI Toolkit manager set is passed in explicitly, so validation
    // never searches the scene for it (LESSON-004: every missing or incompatible target reports a
    // stable code instead of a silent success).

    /// <summary>The closed set of collider update modes this composition admits for a world-space
    /// document's pointer collider. Only <see cref="Automatic"/> is currently admitted; any other
    /// value is reported the same way a missing collider is.</summary>
    public enum ColliderUpdateMode
    {
        Automatic,
        Manual,
    }

    /// <summary>The engine surface one bound panel exposes to the runtime: its UIDocument, the
    /// collider and update mode a world-space raycast against it needs, the wrist/hand anchor
    /// transform a panel that admits one of those anchor kinds must carry, the injectable skin
    /// target this panel's document root applies skin values through, and the target a resolved
    /// routed pointer event is forwarded to.</summary>
    public sealed class UiToolkitPanelBindingEntry
    {
        public UiToolkitPanelBindingEntry(
            UIDocument document,
            Collider collider,
            ColliderUpdateMode colliderUpdateMode,
            IUiToolkitPanelSkinTarget skinTarget,
            Transform anchorTarget = null,
            IUiToolkitRoutedEventTarget routedEventTarget = null)
        {
            Document = document;
            Collider = collider;
            ColliderUpdateMode = colliderUpdateMode;
            SkinTarget = skinTarget;
            AnchorTarget = anchorTarget;
            RoutedEventTarget = routedEventTarget;
        }

        public UIDocument Document { get; }
        public Collider Collider { get; }
        public ColliderUpdateMode ColliderUpdateMode { get; }
        public IUiToolkitPanelSkinTarget SkinTarget { get; }
        public Transform AnchorTarget { get; }
        public IUiToolkitRoutedEventTarget RoutedEventTarget { get; }
    }

    /// <summary>The injectable skin seam a bound document's root applies skin values through, as
    /// USS custom properties or classes on the root <see cref="VisualElement"/>. One entry point
    /// per panel, propagated for every bound document.</summary>
    public interface IUiToolkitPanelSkinTarget
    {
        void ApplySkin(UiToolkitShellSkin skin);
    }

    /// <summary>The target a resolved routed pointer event (hover, select, or scroll) is
    /// forwarded to. Never invoked for a rejected, ambiguous, or empty routing result.</summary>
    public interface IUiToolkitRoutedEventTarget
    {
        void OnRouted(RouteEventKind kind, InputSourceId source);
    }

    /// <summary>The scene's active XR UI Toolkit manager reference, passed in explicitly (never
    /// scene-searched). <see cref="RoutesThroughUiToolkitEvents"/> reports whether the manager
    /// dispatches pointer events through UI Toolkit's own event system rather than bypassing it.</summary>
    public interface IUiToolkitInputManager
    {
        bool RoutesThroughUiToolkitEvents { get; }
    }

    /// <summary>One resolution failure: a stable code, the offending field path, the source
    /// document (when known), and a human message. Never silent (LESSON-004).</summary>
    public sealed class UiToolkitBindingDiagnostic
    {
        public UiToolkitBindingDiagnostic(string code, UIDocument source, string fieldPath, string message)
        {
            Code = code;
            Source = source;
            FieldPath = fieldPath ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Code { get; }
        public UIDocument Source { get; }
        public string FieldPath { get; }
        public string Message { get; }
    }

    public sealed class UiToolkitBindingReport
    {
        public UiToolkitBindingReport(IReadOnlyList<UiToolkitBindingDiagnostic> diagnostics)
        {
            Diagnostics = diagnostics;
        }

        public IReadOnlyList<UiToolkitBindingDiagnostic> Diagnostics { get; }
        public bool IsValid => Diagnostics.Count == 0;
    }

    public sealed class UiToolkitBindingException : Exception
    {
        public UiToolkitBindingException(UiToolkitBindingReport report)
            : base(Describe(report))
        {
            Report = report;
        }

        public UiToolkitBindingReport Report { get; }

        private static string Describe(UiToolkitBindingReport report)
        {
            var lines = new List<string>(report.Diagnostics.Count + 1) { "XR UI Shell UI Toolkit binding is invalid:" };
            foreach (var diagnostic in report.Diagnostics)
            {
                lines.Add($"  [{diagnostic.Code}] {diagnostic.FieldPath}: {diagnostic.Message}");
            }
            return string.Join("\n", lines);
        }
    }

    public static class UiToolkitBindingValidation
    {
        /// <summary>The only collider update mode this composition currently admits.</summary>
        public static readonly IReadOnlyCollection<ColliderUpdateMode> AdmittedColliderUpdateModes = new[] { ColliderUpdateMode.Automatic };

        /// <summary>Validates one binding entry per declared surface in <paramref name="layout"/>
        /// plus the scene-wide active XR UI Toolkit manager reference list. A declared surface
        /// with no entry in <paramref name="entries"/> is reported as surface.missing; every
        /// other check mirrors the UI Toolkit adapter gate clause list.</summary>
        public static UiToolkitBindingReport Validate(
            ShellLayout layout,
            IReadOnlyDictionary<SurfaceId, UiToolkitPanelBindingEntry> entries,
            IReadOnlyList<IUiToolkitInputManager> activeManagers)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            if (activeManagers == null) throw new ArgumentNullException(nameof(activeManagers));

            var diagnostics = new List<UiToolkitBindingDiagnostic>();
            var boundDocuments = new Dictionary<UIDocument, SurfaceId>();

            foreach (var declaration in layout.Surfaces)
            {
                var fieldPath = declaration.Id.ToString();
                if (!entries.TryGetValue(declaration.Id, out var entry) || entry == null || entry.Document == null)
                {
                    diagnostics.Add(new UiToolkitBindingDiagnostic(UiToolkitFailure.SurfaceMissing, entry?.Document, fieldPath, $"Surface '{declaration.Id}' has no bound UIDocument reference."));
                    continue;
                }
                var settings = entry.Document.panelSettings;
                if (settings == null || settings.renderMode != PanelRenderMode.WorldSpace)
                {
                    diagnostics.Add(new UiToolkitBindingDiagnostic(UiToolkitFailure.SurfaceModeMismatch, entry.Document, fieldPath + ".panelSettings", $"Document bound to '{declaration.Id}' has no PanelSettings with render mode WorldSpace."));
                }
                if (boundDocuments.TryGetValue(entry.Document, out var firstOwner))
                {
                    diagnostics.Add(new UiToolkitBindingDiagnostic(UiToolkitFailure.SurfaceDuplicate, entry.Document, fieldPath, $"Document is already bound to '{firstOwner}'."));
                }
                else
                {
                    boundDocuments[entry.Document] = declaration.Id;
                }
                if (entry.Collider == null || !AdmittedColliderUpdateModes.Contains(entry.ColliderUpdateMode))
                {
                    diagnostics.Add(new UiToolkitBindingDiagnostic(UiToolkitFailure.ColliderMissing, entry.Document, fieldPath + ".collider", $"Document bound to '{declaration.Id}' has no collider reference in an admitted update mode."));
                }
                var needsAnchor = declaration.AdmittedAnchors.Contains(AnchorKind.Wrist) || declaration.AdmittedAnchors.Contains(AnchorKind.Hand);
                if (needsAnchor && entry.AnchorTarget == null)
                {
                    diagnostics.Add(new UiToolkitBindingDiagnostic(UiToolkitFailure.AnchorTargetMissing, entry.Document, fieldPath + ".anchorTarget", $"Surface '{declaration.Id}' admits a wrist or hand anchor but has no anchor transform reference."));
                }
            }

            if (activeManagers.Count != 1)
            {
                diagnostics.Add(new UiToolkitBindingDiagnostic(UiToolkitFailure.InputModuleMissing, null, "activeManagers", $"Exactly one active XR UI Toolkit manager reference is required; {activeManagers.Count} were passed."));
            }
            else if (!activeManagers[0].RoutesThroughUiToolkitEvents)
            {
                diagnostics.Add(new UiToolkitBindingDiagnostic(UiToolkitFailure.InputBypassed, null, "activeManagers[0]", "The active XR UI Toolkit manager reports that it bypasses UI Toolkit's own event system."));
            }

            return new UiToolkitBindingReport(diagnostics);
        }
    }

    /// <summary>The immutable, validated set of panel bindings. Enumerates in canonical surface
    /// id order. Construction throws with the same report a failed
    /// <see cref="UiToolkitBindingValidation.Validate"/> call would return.</summary>
    public sealed class UiToolkitPanelBindingSet
    {
        private readonly SortedDictionary<SurfaceId, UiToolkitPanelBindingEntry> _entries;

        private UiToolkitPanelBindingSet(SortedDictionary<SurfaceId, UiToolkitPanelBindingEntry> entries)
        {
            _entries = entries;
        }

        public int Count => _entries.Count;
        public IEnumerable<SurfaceId> BoundSurfaces => _entries.Keys;
        public bool TryGet(SurfaceId id, out UiToolkitPanelBindingEntry entry) => _entries.TryGetValue(id, out entry);

        public static UiToolkitPanelBindingSet Create(
            ShellLayout layout,
            IReadOnlyDictionary<SurfaceId, UiToolkitPanelBindingEntry> entries,
            IReadOnlyList<IUiToolkitInputManager> activeManagers)
        {
            var report = UiToolkitBindingValidation.Validate(layout, entries, activeManagers);
            if (!report.IsValid) throw new UiToolkitBindingException(report);
            return new UiToolkitPanelBindingSet(new SortedDictionary<SurfaceId, UiToolkitPanelBindingEntry>(entries));
        }
    }
}
