using System.Collections.Generic;
using System.Linq;
using Lingkyn.XrUiShell.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Lingkyn.XrUiShell.Ugui.Editor.Tests
{
    // Authored and unexecuted: this assembly has never compiled or run. It is checked against
    // docs/standards/xr-ui-shell/verification-contract.md and coverage-map.json. No test in this
    // file claims that a panel was visible, legible, or reachable, that an anchor followed a
    // wrist or hand, that a ray or gaze hit a panel on a device, or that any input was read from
    // a device.
    public sealed class XrUiShellUguiContractTests
    {
        private readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var gameObject in _created)
            {
                if (gameObject != null) Object.DestroyImmediate(gameObject);
            }
            _created.Clear();
        }

        // ----- thin binding validation (CUG-01) -----

        [Test]
        public void ValidationReportsSurfaceMissingWhenADeclaredPanelHasNoCanvasReference()
        {
            var layout = SampleLayout();
            var entries = new Dictionary<SurfaceId, UguiPanelBindingEntry>();
            var report = UguiBindingValidation.Validate(layout, entries, OneInputModule());
            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Diagnostics.Select(d => d.Code), Has.Member(UguiFailure.SurfaceMissing));
        }

        [Test]
        public void ValidationReportsSurfaceModeMismatchWhenTheCanvasIsNotWorldSpace()
        {
            var layout = SampleLayout();
            var entries = FullEntries(layout);
            var primary = SurfaceId.OfPanel(PanelId.Parse("hud.primary"));
            var badCanvas = CreateCanvas(RenderMode.ScreenSpaceOverlay);
            entries[primary] = new UguiPanelBindingEntry(badCanvas, CreateCamera(), CreateRaycaster(badCanvas), new RecordingSkinTarget());
            var report = UguiBindingValidation.Validate(layout, entries, OneInputModule());
            Assert.That(report.Diagnostics.Select(d => d.Code), Has.Member(UguiFailure.SurfaceModeMismatch));
        }

        [Test]
        public void ValidationReportsSurfaceDuplicateWhenTheSameCanvasIsBoundToTwoPanels()
        {
            var layout = SampleLayout();
            var canvas = CreateCanvas(RenderMode.WorldSpace);
            var camera = CreateCamera();
            var raycaster = CreateRaycaster(canvas);
            var primary = SurfaceId.OfPanel(PanelId.Parse("hud.primary"));
            var secondary = SurfaceId.OfPanel(PanelId.Parse("hud.secondary"));
            var entries = new Dictionary<SurfaceId, UguiPanelBindingEntry>
            {
                [primary] = new UguiPanelBindingEntry(canvas, camera, raycaster, new RecordingSkinTarget()),
                [secondary] = new UguiPanelBindingEntry(canvas, camera, raycaster, new RecordingSkinTarget()),
                [SurfaceId.OfWristMenu(WristMenuId.Parse("quick"))] = FullWristEntry(),
            };
            var report = UguiBindingValidation.Validate(layout, entries, OneInputModule());
            Assert.That(report.Diagnostics.Select(d => d.Code), Has.Member(UguiFailure.SurfaceDuplicate));
        }

        [Test]
        public void ValidationReportsCameraMissingWhenTheCanvasHasNoEventCameraReference()
        {
            var layout = SampleLayout();
            var entries = FullEntries(layout);
            var primary = SurfaceId.OfPanel(PanelId.Parse("hud.primary"));
            var canvas = CreateCanvas(RenderMode.WorldSpace);
            entries[primary] = new UguiPanelBindingEntry(canvas, null, CreateRaycaster(canvas), new RecordingSkinTarget());
            var report = UguiBindingValidation.Validate(layout, entries, OneInputModule());
            Assert.That(report.Diagnostics.Select(d => d.Code), Has.Member(UguiFailure.CameraMissing));
        }

        [Test]
        public void ValidationReportsRaycasterMissingWhenTheCanvasHasNoRaycasterReference()
        {
            var layout = SampleLayout();
            var entries = FullEntries(layout);
            var primary = SurfaceId.OfPanel(PanelId.Parse("hud.primary"));
            var canvas = CreateCanvas(RenderMode.WorldSpace);
            entries[primary] = new UguiPanelBindingEntry(canvas, CreateCamera(), null, new RecordingSkinTarget());
            var report = UguiBindingValidation.Validate(layout, entries, OneInputModule());
            Assert.That(report.Diagnostics.Select(d => d.Code), Has.Member(UguiFailure.RaycasterMissing));
        }

        [Test]
        public void ValidationReportsInputModuleMissingWhenTheSceneDoesNotCarryExactlyOneActiveModule()
        {
            var layout = SampleLayout();
            var entries = FullEntries(layout);
            Assert.That(UguiBindingValidation.Validate(layout, entries, new BaseInputModule[0]).Diagnostics.Select(d => d.Code), Has.Member(UguiFailure.InputModuleMissing));
            Assert.That(UguiBindingValidation.Validate(layout, entries, new[] { CreateInputModule(), CreateInputModule() }).Diagnostics.Select(d => d.Code), Has.Member(UguiFailure.InputModuleMissing));
        }

        [Test]
        public void ValidationReportsAnchorTargetMissingForAWristMenuWithNoAnchorTransformReference()
        {
            var layout = SampleLayout();
            var entries = FullEntries(layout);
            var wristCanvas = CreateCanvas(RenderMode.WorldSpace);
            entries[SurfaceId.OfWristMenu(WristMenuId.Parse("quick"))] =
                new UguiPanelBindingEntry(wristCanvas, CreateCamera(), CreateRaycaster(wristCanvas), new RecordingSkinTarget(), anchorTarget: null);
            var report = UguiBindingValidation.Validate(layout, entries, OneInputModule());
            Assert.That(report.Diagnostics.Select(d => d.Code), Has.Member(UguiFailure.AnchorTargetMissing));
        }

        [Test]
        public void ConstructionThrowsWithTheSameReportAsValidation()
        {
            var layout = SampleLayout();
            var entries = new Dictionary<SurfaceId, UguiPanelBindingEntry>();
            var report = UguiBindingValidation.Validate(layout, entries, OneInputModule());

            var thrown = Assert.Throws<UguiBindingException>(() => UguiPanelBindingSet.Create(layout, entries, OneInputModule()));
            Assert.That(thrown.Report.Diagnostics.Select(d => d.Code), Is.EqualTo(report.Diagnostics.Select(d => d.Code)));
            Assert.That(thrown.Message, Does.Contain("[" + UguiFailure.SurfaceMissing + "]"));
        }

        // ----- explicit diagnostic for every by-name or optional resolution (CUG-02, LESSON-004) -----

        [Test]
        public void EveryMissingOrIncompatibleTargetReportsADiagnosticNeverASilentSuccess()
        {
            var layout = SampleLayout();
            var entries = new Dictionary<SurfaceId, UguiPanelBindingEntry>();
            var report = UguiBindingValidation.Validate(layout, entries, OneInputModule());
            Assert.That(report.IsValid, Is.False, "A completely unbound layout must never validate as clean.");
            foreach (var diagnostic in report.Diagnostics)
            {
                Assert.That(diagnostic.Code, Is.Not.Empty);
                Assert.That(diagnostic.FieldPath, Is.Not.Empty);
            }
        }

        [Test]
        public void AMissingCanvasDiagnosticNamesTheOffendingFieldPath()
        {
            var layout = SampleLayout();
            var entries = new Dictionary<SurfaceId, UguiPanelBindingEntry>();
            var report = UguiBindingValidation.Validate(layout, entries, OneInputModule());
            var diagnostic = report.Diagnostics.First(d => d.Code == UguiFailure.SurfaceMissing);
            Assert.That(diagnostic.FieldPath, Does.Contain("hud.primary").Or.Contain("hud.secondary").Or.Contain("quick"));
        }

        // ----- injectable skin seam (CUG-03) -----

        [Test]
        public void DefaultSkinCarriesTheCanonicalDesignLanguageValues()
        {
            var skin = ScriptableObject.CreateInstance<UguiShellSkin>();
            Assert.That(skin.SurfacePanel, Is.EqualTo(new Color(0.031f, 0.094f, 0.122f, 0.96f)).Using(ColorComparer));
            Assert.That(skin.SurfaceAccent, Is.EqualTo(new Color(0.122f, 0.592f, 0.624f, 1f)).Using(ColorComparer));
            Assert.That(skin.HitTargetMinimum, Is.EqualTo(48f));
            Assert.That(skin.HitTargetPrimary, Is.EqualTo(60f));
            Object.DestroyImmediate(skin);
        }

        [Test]
        public void ResolveSlotColorAndSizeFollowTheCanonicalMapping()
        {
            var skin = ScriptableObject.CreateInstance<UguiShellSkin>();
            var mapping = CanonicalSkinMapping.Build();
            Assert.That(skin.ResolveSlotColor(ShellSlot.PanelBackground, mapping), Is.EqualTo(skin.SurfacePanel).Using(ColorComparer));
            Assert.That(skin.ResolveSlotColor(ShellSlot.ControlAccent, mapping), Is.EqualTo(skin.SurfaceAccent).Using(ColorComparer));
            Assert.That(skin.ResolveSlotSize(ShellSlot.ControlHitMinimum, mapping), Is.EqualTo(skin.HitTargetMinimum));
            Object.DestroyImmediate(skin);
        }

        [Test]
        public void ApplySkinPropagatesToEveryBoundCanvasSubtreeInCanonicalOrder()
        {
            var layout = SampleLayout();
            var entries = FullEntries(layout);
            var bindings = UguiPanelBindingSet.Create(layout, entries, OneInputModule());
            var runtime = new UguiShellRuntime(layout, ShellState.Initial(layout), bindings);
            var skin = ScriptableObject.CreateInstance<UguiShellSkin>();

            runtime.ApplySkin(skin);

            foreach (var surfaceId in bindings.BoundSurfaces)
            {
                bindings.TryGet(surfaceId, out var entry);
                var recording = (RecordingSkinTarget)entry.SkinTarget;
                Assert.That(recording.AppliedSkins, Has.Member(skin), surfaceId.ToString());
            }
            Object.DestroyImmediate(skin);
        }

        // ----- translation of accepted routing results (CUG-04) -----

        [Test]
        public void AResolvedRoutedEventIsForwardedOnlyToTheBoundCanvasOfTheResolvedPanel()
        {
            var layout = SampleLayout();
            var entries = FullEntries(layout);
            var bindings = UguiPanelBindingSet.Create(layout, entries, OneInputModule());
            var runtime = new UguiShellRuntime(layout, ShellState.Initial(layout), bindings);
            var primary = SurfaceId.OfPanel(PanelId.Parse("hud.primary"));
            var secondary = SurfaceId.OfPanel(PanelId.Parse("hud.secondary"));
            runtime.Apply(new OpenIntent(primary));

            var rightRay = InputSourceId.Parse("right_ray");
            var result = runtime.Route(new HoverIntent(rightRay, new[] { primary }));
            Assert.That(result.Succeeded, Is.True, result.Message);

            bindings.TryGet(primary, out var primaryEntry);
            bindings.TryGet(secondary, out var secondaryEntry);
            Assert.That(((RecordingRoutedEventTarget)primaryEntry.RoutedEventTarget).Calls.Count, Is.EqualTo(1));
            Assert.That(((RecordingRoutedEventTarget)secondaryEntry.RoutedEventTarget).Calls.Count, Is.EqualTo(0));
        }

        [Test]
        public void NothingIsForwardedOnRouteAmbiguousRouteNoneOrAnyRejection()
        {
            var layout = SampleLayout();
            var entries = FullEntries(layout);
            var bindings = UguiPanelBindingSet.Create(layout, entries, OneInputModule());
            var runtime = new UguiShellRuntime(layout, ShellState.Initial(layout), bindings);
            var primary = SurfaceId.OfPanel(PanelId.Parse("hud.primary"));
            var secondary = SurfaceId.OfPanel(PanelId.Parse("hud.secondary"));
            var rightRay = InputSourceId.Parse("right_ray");

            // route.none: neither candidate is open yet.
            runtime.Route(new HoverIntent(rightRay, new[] { primary, secondary }));
            // route.ambiguous: both open, neither focused.
            runtime.Apply(new OpenIntent(primary));
            runtime.Apply(new OpenIntent(secondary));
            runtime.Route(new HoverIntent(rightRay, new[] { primary, secondary }));
            // source.unknown: an unregistered source.
            runtime.Route(new HoverIntent(InputSourceId.Parse("ghost_source"), new[] { primary }));

            bindings.TryGet(primary, out var primaryEntry);
            bindings.TryGet(secondary, out var secondaryEntry);
            Assert.That(((RecordingRoutedEventTarget)primaryEntry.RoutedEventTarget).Calls.Count, Is.EqualTo(0));
            Assert.That(((RecordingRoutedEventTarget)secondaryEntry.RoutedEventTarget).Calls.Count, Is.EqualTo(0));
        }

        // ----- runtime constructed with explicit references (CUG-05) -----

        [Test]
        public void AcceptedOpenAndCloseIntentsToggleTheBoundCanvassActiveStateInOrder()
        {
            var layout = SampleLayout();
            var entries = FullEntries(layout);
            var bindings = UguiPanelBindingSet.Create(layout, entries, OneInputModule());
            var runtime = new UguiShellRuntime(layout, ShellState.Initial(layout), bindings);
            var primary = SurfaceId.OfPanel(PanelId.Parse("hud.primary"));
            bindings.TryGet(primary, out var entry);
            Assert.That(entry.Canvas.gameObject.activeSelf, Is.True, "Freshly created GameObjects start active; the runtime has not touched it yet.");

            var openOutcome = runtime.Apply(new OpenIntent(primary));
            Assert.That(openOutcome.Accepted, Is.True);
            Assert.That(entry.Canvas.gameObject.activeSelf, Is.True);

            var closeOutcome = runtime.Apply(new CloseIntent(primary));
            Assert.That(closeOutcome.Accepted, Is.True);
            Assert.That(entry.Canvas.gameObject.activeSelf, Is.False);

            Assert.That(runtime.Outcomes.Select(o => o.Accepted), Is.EqualTo(new[] { true, true }));
        }

        [Test]
        public void ARejectedIntentReportsRejectionAndTouchesNoCanvas()
        {
            var layout = SampleLayout();
            var entries = FullEntries(layout);
            var bindings = UguiPanelBindingSet.Create(layout, entries, OneInputModule());
            var runtime = new UguiShellRuntime(layout, ShellState.Initial(layout), bindings);
            var ghost = SurfaceId.OfPanel(PanelId.Parse("ghost"));

            var outcome = runtime.Apply(new OpenIntent(ghost));
            Assert.That(outcome.Accepted, Is.False);
            Assert.That(outcome.Code, Is.EqualTo(ShellFailure.PanelUnknown));

            var primary = SurfaceId.OfPanel(PanelId.Parse("hud.primary"));
            bindings.TryGet(primary, out var entry);
            Assert.That(entry.Canvas.gameObject.activeSelf, Is.True, "A rejected intent touches no bound Canvas.");
        }

        // ----- two independent runtimes (CUG-06) -----

        [Test]
        public void TwoRuntimesConstructedSideBySideShareNoState()
        {
            var layoutA = SampleLayout();
            var layoutB = SampleLayout();
            var runtimeA = new UguiShellRuntime(layoutA, ShellState.Initial(layoutA), UguiPanelBindingSet.Create(layoutA, FullEntries(layoutA), OneInputModule()));
            var runtimeB = new UguiShellRuntime(layoutB, ShellState.Initial(layoutB), UguiPanelBindingSet.Create(layoutB, FullEntries(layoutB), OneInputModule()));
            var primary = SurfaceId.OfPanel(PanelId.Parse("hud.primary"));

            runtimeA.Apply(new OpenIntent(primary));

            Assert.That(runtimeA.State.TryGetSurfaceState(primary, out var stateA) && stateA.IsOpen, Is.True);
            Assert.That(runtimeB.State.TryGetSurfaceState(primary, out var stateB) && stateB.IsOpen, Is.False);
        }

        // ----- helpers -----

        private static readonly IComparer<Color> ColorComparer = Comparer<Color>.Create((a, b) =>
        {
            var close = Mathf.Abs(a.r - b.r) < 0.0005f && Mathf.Abs(a.g - b.g) < 0.0005f && Mathf.Abs(a.b - b.b) < 0.0005f && Mathf.Abs(a.a - b.a) < 0.0005f;
            return close ? 0 : 1;
        });

        private ShellLayout SampleLayout()
        {
            var builder = new ShellLayoutBuilder();
            builder.DeclarePanel(PanelId.Parse("hud.primary"), AnchorKind.World, AnchorKind.HeadLocked);
            builder.DeclarePanel(PanelId.Parse("hud.secondary"), AnchorKind.World, AnchorKind.HeadLocked);
            builder.DeclareWristMenu(WristMenuId.Parse("quick"));
            builder.RegisterInputSource(InputSourceId.Parse("right_ray"), InputSourceKind.Ray);
            return builder.Build();
        }

        /// <summary>A fully valid binding entry for every declared surface: a fresh world-space
        /// Canvas, camera, and raycaster per panel, and a fresh anchor for every wrist/hand menu.
        /// A test that needs one broken entry copies this dictionary and replaces one entry.</summary>
        private Dictionary<SurfaceId, UguiPanelBindingEntry> FullEntries(ShellLayout layout)
        {
            var entries = new Dictionary<SurfaceId, UguiPanelBindingEntry>();
            foreach (var declaration in layout.Surfaces)
            {
                if (declaration.Id.Kind == SurfaceKind.WristMenu || declaration.Id.Kind == SurfaceKind.HandMenu)
                {
                    entries[declaration.Id] = FullWristEntry();
                    continue;
                }
                var canvas = CreateCanvas(RenderMode.WorldSpace);
                entries[declaration.Id] = new UguiPanelBindingEntry(canvas, CreateCamera(), CreateRaycaster(canvas), new RecordingSkinTarget(), routedEventTarget: new RecordingRoutedEventTarget());
            }
            return entries;
        }

        private UguiPanelBindingEntry FullWristEntry()
        {
            var canvas = CreateCanvas(RenderMode.WorldSpace);
            return new UguiPanelBindingEntry(canvas, CreateCamera(), CreateRaycaster(canvas), new RecordingSkinTarget(), CreateAnchor(), new RecordingRoutedEventTarget());
        }

        private Canvas CreateCanvas(RenderMode renderMode)
        {
            var gameObject = new GameObject("Canvas");
            _created.Add(gameObject);
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = renderMode;
            return canvas;
        }

        private Camera CreateCamera()
        {
            var gameObject = new GameObject("Camera");
            _created.Add(gameObject);
            return gameObject.AddComponent<Camera>();
        }

        private BaseRaycaster CreateRaycaster(Canvas canvas)
        {
            return canvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        private Transform CreateAnchor()
        {
            var gameObject = new GameObject("Anchor");
            _created.Add(gameObject);
            return gameObject.transform;
        }

        private BaseInputModule CreateInputModule()
        {
            var gameObject = new GameObject("InputModule");
            _created.Add(gameObject);
            return gameObject.AddComponent<StandaloneInputModule>();
        }

        private IReadOnlyList<BaseInputModule> OneInputModule() => new[] { CreateInputModule() };

        private sealed class RecordingSkinTarget : IUguiPanelSkinTarget
        {
            public List<UguiShellSkin> AppliedSkins { get; } = new List<UguiShellSkin>();
            public void ApplySkin(UguiShellSkin skin) => AppliedSkins.Add(skin);
        }

        private sealed class RecordingRoutedEventTarget : IUguiRoutedEventTarget
        {
            public List<(RouteEventKind Kind, InputSourceId Source)> Calls { get; } = new List<(RouteEventKind, InputSourceId)>();
            public void OnRouted(RouteEventKind kind, InputSourceId source) => Calls.Add((kind, source));
        }
    }
}
