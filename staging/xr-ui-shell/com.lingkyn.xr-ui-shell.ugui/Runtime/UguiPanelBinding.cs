using System;
using System.Collections.Generic;
using System.Linq;
using Lingkyn.XrUiShell.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Lingkyn.XrUiShell.Ugui
{
    // A thin binding of each Core-declared panel to exactly one world-space Canvas subtree.
    // Every reference (the Canvas, its event camera, its raycaster, a wrist/hand anchor
    // transform, the skin target, and the routed-event target) is explicit; nothing here is
    // resolved by name, by scene search, or by reflection. The active XR UI input module set is
    // passed in explicitly too, so validation never searches the scene for it (LESSON-004: every
    // missing or incompatible target reports a stable code instead of a silent success).

    /// <summary>The engine surface one bound panel exposes to the runtime: its Canvas, its event
    /// camera, its raycaster (typed as the Unity UI <see cref="BaseRaycaster"/> here so this
    /// scaffold carries no XR Interaction Toolkit package dependency; a real binder targets the
    /// validated tracked-device graphic raycaster type), the wrist/hand anchor transform a panel
    /// that admits one of those anchor kinds must carry, the injectable skin target this panel's
    /// subtree applies skin values through, and the target a resolved routed pointer event is
    /// forwarded to.</summary>
    public sealed class UguiPanelBindingEntry
    {
        public UguiPanelBindingEntry(
            Canvas canvas,
            Camera eventCamera,
            BaseRaycaster raycaster,
            IUguiPanelSkinTarget skinTarget,
            Transform anchorTarget = null,
            IUguiRoutedEventTarget routedEventTarget = null)
        {
            Canvas = canvas;
            EventCamera = eventCamera;
            Raycaster = raycaster;
            SkinTarget = skinTarget;
            AnchorTarget = anchorTarget;
            RoutedEventTarget = routedEventTarget;
        }

        public Canvas Canvas { get; }
        public Camera EventCamera { get; }
        public BaseRaycaster Raycaster { get; }
        public IUguiPanelSkinTarget SkinTarget { get; }
        public Transform AnchorTarget { get; }
        public IUguiRoutedEventTarget RoutedEventTarget { get; }
    }

    /// <summary>The injectable skin seam a bound Canvas subtree's root applies skin values
    /// through. One entry point per panel, propagated for every bound subtree.</summary>
    public interface IUguiPanelSkinTarget
    {
        void ApplySkin(UguiShellSkin skin);
    }

    /// <summary>The target a resolved routed pointer event (hover, select, or scroll) is
    /// forwarded to. Never invoked for a rejected, ambiguous, or empty routing result.</summary>
    public interface IUguiRoutedEventTarget
    {
        void OnRouted(RouteEventKind kind, InputSourceId source);
    }

    /// <summary>One resolution failure: a stable code, the offending field path, the source
    /// Canvas (when known), and a human message. Never silent (LESSON-004).</summary>
    public sealed class UguiBindingDiagnostic
    {
        public UguiBindingDiagnostic(string code, Canvas source, string fieldPath, string message)
        {
            Code = code;
            Source = source;
            FieldPath = fieldPath ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Code { get; }
        public Canvas Source { get; }
        public string FieldPath { get; }
        public string Message { get; }
    }

    public sealed class UguiBindingReport
    {
        public UguiBindingReport(IReadOnlyList<UguiBindingDiagnostic> diagnostics)
        {
            Diagnostics = diagnostics;
        }

        public IReadOnlyList<UguiBindingDiagnostic> Diagnostics { get; }
        public bool IsValid => Diagnostics.Count == 0;
    }

    public sealed class UguiBindingException : Exception
    {
        public UguiBindingException(UguiBindingReport report)
            : base(Describe(report))
        {
            Report = report;
        }

        public UguiBindingReport Report { get; }

        private static string Describe(UguiBindingReport report)
        {
            var lines = new List<string>(report.Diagnostics.Count + 1) { "XR UI Shell UGUI binding is invalid:" };
            foreach (var diagnostic in report.Diagnostics)
            {
                lines.Add($"  [{diagnostic.Code}] {diagnostic.FieldPath}: {diagnostic.Message}");
            }
            return string.Join("\n", lines);
        }
    }

    public static class UguiBindingValidation
    {
        /// <summary>Validates one binding entry per declared surface in <paramref name="layout"/>
        /// plus the scene-wide active input module reference list. A declared surface with no
        /// entry in <paramref name="entries"/> is reported as surface.missing; every other check
        /// mirrors the UGUI adapter gate clause list.</summary>
        public static UguiBindingReport Validate(
            ShellLayout layout,
            IReadOnlyDictionary<SurfaceId, UguiPanelBindingEntry> entries,
            IReadOnlyList<BaseInputModule> activeInputModules)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            if (activeInputModules == null) throw new ArgumentNullException(nameof(activeInputModules));

            var diagnostics = new List<UguiBindingDiagnostic>();
            var boundCanvases = new Dictionary<Canvas, SurfaceId>();

            foreach (var declaration in layout.Surfaces)
            {
                var fieldPath = declaration.Id.ToString();
                if (!entries.TryGetValue(declaration.Id, out var entry) || entry == null || entry.Canvas == null)
                {
                    diagnostics.Add(new UguiBindingDiagnostic(UguiFailure.SurfaceMissing, entry?.Canvas, fieldPath, $"Surface '{declaration.Id}' has no bound Canvas reference."));
                    continue;
                }
                if (entry.Canvas.renderMode != RenderMode.WorldSpace)
                {
                    diagnostics.Add(new UguiBindingDiagnostic(UguiFailure.SurfaceModeMismatch, entry.Canvas, fieldPath, $"Canvas bound to '{declaration.Id}' has render mode {entry.Canvas.renderMode}, not WorldSpace."));
                }
                if (boundCanvases.TryGetValue(entry.Canvas, out var firstOwner))
                {
                    diagnostics.Add(new UguiBindingDiagnostic(UguiFailure.SurfaceDuplicate, entry.Canvas, fieldPath, $"Canvas is already bound to '{firstOwner}'."));
                }
                else
                {
                    boundCanvases[entry.Canvas] = declaration.Id;
                }
                if (entry.EventCamera == null)
                {
                    diagnostics.Add(new UguiBindingDiagnostic(UguiFailure.CameraMissing, entry.Canvas, fieldPath + ".eventCamera", $"Canvas bound to '{declaration.Id}' has no event camera reference."));
                }
                if (entry.Raycaster == null)
                {
                    diagnostics.Add(new UguiBindingDiagnostic(UguiFailure.RaycasterMissing, entry.Canvas, fieldPath + ".raycaster", $"Canvas bound to '{declaration.Id}' has no raycaster reference."));
                }
                var needsAnchor = declaration.AdmittedAnchors.Contains(AnchorKind.Wrist) || declaration.AdmittedAnchors.Contains(AnchorKind.Hand);
                if (needsAnchor && entry.AnchorTarget == null)
                {
                    diagnostics.Add(new UguiBindingDiagnostic(UguiFailure.AnchorTargetMissing, entry.Canvas, fieldPath + ".anchorTarget", $"Surface '{declaration.Id}' admits a wrist or hand anchor but has no anchor transform reference."));
                }
            }

            if (activeInputModules.Count != 1)
            {
                diagnostics.Add(new UguiBindingDiagnostic(UguiFailure.InputModuleMissing, null, "activeInputModules", $"Exactly one active XR UI input module reference is required; {activeInputModules.Count} were passed."));
            }

            return new UguiBindingReport(diagnostics);
        }
    }

    /// <summary>The immutable, validated set of panel bindings. Enumerates in canonical surface
    /// id order. Construction throws with the same report a failed
    /// <see cref="UguiBindingValidation.Validate"/> call would return.</summary>
    public sealed class UguiPanelBindingSet
    {
        private readonly SortedDictionary<SurfaceId, UguiPanelBindingEntry> _entries;

        private UguiPanelBindingSet(SortedDictionary<SurfaceId, UguiPanelBindingEntry> entries)
        {
            _entries = entries;
        }

        public int Count => _entries.Count;
        public IEnumerable<SurfaceId> BoundSurfaces => _entries.Keys;
        public bool TryGet(SurfaceId id, out UguiPanelBindingEntry entry) => _entries.TryGetValue(id, out entry);

        public static UguiPanelBindingSet Create(
            ShellLayout layout,
            IReadOnlyDictionary<SurfaceId, UguiPanelBindingEntry> entries,
            IReadOnlyList<BaseInputModule> activeInputModules)
        {
            var report = UguiBindingValidation.Validate(layout, entries, activeInputModules);
            if (!report.IsValid) throw new UguiBindingException(report);
            return new UguiPanelBindingSet(new SortedDictionary<SurfaceId, UguiPanelBindingEntry>(entries));
        }
    }
}
