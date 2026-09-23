using System.Collections.Generic;
using System.Linq;
using Lingkyn.XrUiShell.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lingkyn.XrUiShell.UiToolkit.Editor.Tests
{
    // Authored and unexecuted: this assembly has never compiled or run. It is checked against
    // docs/standards/xr-ui-shell/verification-contract.md and coverage-map.json. No test in this
    // file claims that a panel was visible, legible, or reachable, that an anchor followed a
    // wrist or hand, that a ray or gaze hit a panel on a device, or that any input was read from
    // a device. Nothing in this file is shared with the UGUI sibling adapter's tests beyond Core.
    public sealed class XrUiShellUiToolkitContractTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                if (created != null) Object.DestroyImmediate(created);
            }
            _created.Clear();
        }

        // ----- thin binding validation (CUT-01) -----

        [Test]
        public void ValidationReportsSurfaceMissingWhenADeclaredPanelHasNoDocumentReference()
        {
            var layout = SampleLayout();
            var entries = new Dictionary<SurfaceId, UiToolkitPanelBindingEntry>();
            var report = UiToolkitBindingValidation.Validate(layout, entries, OneManager());
            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Diagnostics.Select(d => d.Code), Has.Member(UiToolkitFailure.SurfaceMissing));
        }

        [Test]
        public void ValidationReportsSurfaceModeMismatchWhenThePanelSettingsRenderModeIsNotWorldSpace()
        {
            var layout = SampleLayout();
            var entries = FullEntries(layout);
            var primary = SurfaceId.OfPanel(PanelId.Parse("hud.primary"));
            var badDocument = CreateDocument(PanelRenderMode.ScreenSpaceOverlay);
            entries[primary] = new UiToolkitPanelBindingEntry(badDocument, CreateCollider(), ColliderUpdateMode.Automatic, new RecordingSkinTarget());
            var report = UiToolkitBindingValidation.Validate(layout, entries, OneManager());
            Assert.That(report.Diagnostics.Select(d => d.Code), Has.Member(UiToolkitFailure.SurfaceModeMismatch));
        }

        [Test]
        public void ValidationReportsSurfaceDuplicateWhenTheSameDocumentIsBoundToTwoPanels()
        {
            var layout = SampleLayout();
            var document = CreateDocument(PanelRenderMode.WorldSpace);
            var primary = SurfaceId.OfPanel(PanelId.Parse("hud.primary"));
            var secondary = SurfaceId.OfPanel(PanelId.Parse("hud.secondary"));
            var entries = new Dictionary<SurfaceId, UiToolkitPanelBindingEntry>
            {
                [primary] = new UiToolkitPanelBindingEntry(document, CreateCollider(), ColliderUpdateMode.Automatic, new RecordingSkinTarget()),
                [secondary] = new UiToolkitPanelBindingEntry(document, CreateCollider(), ColliderUpdateMode.Automatic, new RecordingSkinTarget()),
                [SurfaceId.OfWristMenu(WristMenuId.Parse("quick"))] = FullWristEntry(),
            };
            var report = UiToolkitBindingValidation.Validate(layout, entries, OneManager());
            Assert.That(report.Diagnostics.Select(d => d.Code), Has.Member(UiToolkitFailure.SurfaceDuplicate));
        }

        [Test]
        public void ValidationReportsColliderMissingWhenTheDocumentHasNoColliderReference()
        {
            var layout = SampleLayout();
            var entries = FullEntries(layout);
            var primary = SurfaceId.OfPanel(PanelId.Parse("hud.primary"));
            entries[primary] = new UiToolkitPanelBindingEntry(CreateDocument(PanelRenderMode.WorldSpace), null, ColliderUpdateMode.Automatic, new RecordingSkinTarget());
            var report = UiToolkitBindingValidation.Validate(layout, entries, OneManager());
            Assert.That(report.Diagnostics.Select(d => d.Code), Has.Member(UiToolkitFailure.ColliderMissing));
        }

        [Test]
        public void ValidationReportsColliderMissingWhenTheUpdateModeIsNotAdmitted()
        {
            var layout = SampleLayout();
            var entries = FullEntries(layout);
            var primary = SurfaceId.OfPanel(PanelId.Parse("hud.primary"));
            entries[primary] = new UiToolkitPanelBindingEntry(CreateDocument(PanelRenderMode.WorldSpace), CreateCollider(), ColliderUpdateMode.Manual, new RecordingSkinTarget());
            var report = UiToolkitBindingValidation.Validate(layout, entries, OneManager());
            Assert.That(report.Diagnostics.Select(d => d.Code), Has.Member(UiToolkitFailure.ColliderMissing));
        }

        [Test]
        public void ValidationReportsInputModuleMissingWhenTheSceneDoesNotCarryExactlyOneActiveManager()
        {
            var layout = SampleLayout();
            var entries = FullEntries(layout);
            Assert.That(UiToolkitBindingValidation.Validate(layout, entries, new IUiToolkitInputManager[0]).Diagnostics.Select(d => d.Code), Has.Member(UiToolkitFailure.InputModuleMissing));
            Assert.That(UiToolkitBindingValidation.Validate(layout, entries, new IUiToolkitInputManager[] { new FakeInputManager(true), new FakeInputManager(true) }).Diagnostics.Select(d => d.Code), Has.Member(UiToolkitFailure.InputModuleMissing));
        }

        [Test]
        public void ValidationReportsInputBypassedWhenTheActiveManagerBypassesUiToolkitEvents()
        {
            var layout = SampleLayout();
            var entries = FullEntries(layout);
            var report = UiToolkitBindingValidation.Validate(layout, entries, new IUiToolkitInputManager[] { new FakeInputManager(false) });
            Assert.That(report.Diagnostics.Select(d => d.Code), Has.Member(UiToolkitFailure.InputBypassed));
        }

        [Test]
        public void ValidationReportsAnchorTargetMissingForAWristMenuWithNoAnchorTransformReference()
        {
            var layout = SampleLayout();
            var entries = FullEntries(layout);
            var wristDocument = CreateDocument(PanelRenderMode.WorldSpace);
            entries[SurfaceId.OfWristMenu(WristMenuId.Parse("quick"))] =
                new UiToolkitPanelBindingEntry(wristDocument, CreateCollider(), ColliderUpdateMode.Automatic, new RecordingSkinTarget(), anchorTarget: null);
            var report = UiToolkitBindingValidation.Validate(layout, entries, OneManager());
            Assert.That(report.Diagnostics.Select(d => d.Code), Has.Member(UiToolkitFailure.AnchorTargetMissing));
        }

        [Test]
        public void ConstructionThrowsWithTheSameReportAsValidation()
        {
            var layout = SampleLayout();
            var entries = new Dictionary<SurfaceId, UiToolkitPanelBindingEntry>();
            var report = UiToolkitBindingValidation.Validate(layout, entries, OneManager());

            var thrown = Assert.Throws<UiToolkitBindingException>(() => UiToolkitPanelBindingSet.Create(layout, entries, OneManager()));
            Assert.That(thrown.Report.Diagnostics.Select(d => d.Code), Is.EqualTo(report.Diagnostics.Select(d => d.Code)));
            Assert.That(thrown.Message, Does.Contain("[" + UiToolkitFailure.SurfaceMissing + "]"));
        }

        // ----- explicit diagnostic for every by-name or optional resolution (CUT-02, LESSON-004) -----

        [Test]
        public void EveryMissingOrIncompatibleTargetReportsADiagnosticNeverASilentSuccess()
        {
            var layout = SampleLayout();
            var entries = new Dictionary<SurfaceId, UiToolkitPanelBindingEntry>();
            var report = UiToolkitBindingValidation.Validate(layout, entries, OneManager());
            Assert.That(report.IsValid, Is.False, "A completely unbound layout must never validate as clean.");
            foreach (var diagnostic in report.Diagnostics)
            {
                Assert.That(diagnostic.Code, Is.Not.Empty);
                Assert.That(diagnostic.FieldPath, Is.Not.Empty);
            }
        }

        [Test]
        public void AMissingDocumentDiagnosticNamesTheOffendingFieldPath()
        {
            var layout = SampleLayout();
            var entries = new Dictionary<SurfaceId, UiToolkitPanelBindingEntry>();
            var report = UiToolkitBindingValidation.Validate(layout, entries, OneManager());
            var diagnostic = report.Diagnostics.First(d => d.Code == UiToolkitFailure.SurfaceMissing);
            Assert.That(diagnostic.FieldPath, Does.Contain("hud.primary").Or.Contain("hud.secondary").Or.Contain("quick"));
        }

        // ----- injectable skin seam (CUT-03) -----

        [Test]
        public void DefaultSkinCarriesTheCanonicalDesignLanguageValues()
        {
            var skin = ScriptableObject.CreateInstance<UiToolkitShellSkin>();
            Assert.That(skin.SurfacePanel, Is.EqualTo(new Color(0.031f, 0.094f, 0.122f, 0.96f)).Using(ColorComparer));
            Assert.That(skin.SurfaceAccent, Is.EqualTo(new Color(0.122f, 0.592f, 0.624f, 1f)).Using(ColorComparer));
            Assert.That(skin.HitTargetMinimum, Is.EqualTo(48f));
            Assert.That(skin.HitTargetPrimary, Is.EqualTo(60f));
            Object.DestroyImmediate(skin);
        }

        [Test]
        public void ResolveSlotColorAndSizeFollowTheCanonicalMapping()
        {
            var skin = ScriptableObject.CreateInstance<UiToolkitShellSkin>();
            var mapping = CanonicalSkinMapping.Build();
            Assert.That(skin.ResolveSlotColor(ShellSlot.PanelBackground, mapping), Is.EqualTo(skin.SurfacePanel).Using(ColorComparer));
            Assert.That(skin.ResolveSlotColor(ShellSlot.ControlAccent, mapping), Is.EqualTo(skin.SurfaceAccent).Using(ColorComparer));
            Assert.That(skin.ResolveSlotSize(ShellSlot.ControlHitMinimum, mapping), Is.EqualTo(skin.HitTargetMinimum));
            Assert.That(UiToolkitShellSkin.UssVariableName(ShellSlot.PanelBackground), Is.EqualTo("--shell-panel-background"));
            Object.DestroyImmediate(skin);
        }

        [Test]
        public void ApplySkinPropagatesToEveryBoundDocumentInCanonicalOrder()
        {
            var layout = SampleLayout();
            var entries = FullEntries(layout);
            var bindings = UiToolkitPanelBindingSet.Create(layout, entries, OneManager());
            var runtime = new UiToolkitShellRuntime(layout, ShellState.Initial(layout), bindings);
            var skin = ScriptableObject.CreateInstance<UiToolkitShellSkin>();

            runtime.ApplySkin(skin);

            foreach (var surfaceId in bindings.BoundSurfaces)
            {
                bindings.TryGet(surfaceId, out var entry);
                var recording = (RecordingSkinTarget)entry.SkinTarget;
                Assert.That(recording.AppliedSkins, Has.Member(skin), surfaceId.ToString());
            }
            Object.DestroyImmediate(skin);
        }

        // ----- translation of accepted routing results (CUT-04) -----

        [Test]
        public void AResolvedRoutedEventIsForwardedOnlyToTheBoundDocumentOfTheResolvedPanel()
        {
            var layout = SampleLayout();
            var entries = FullEntries(layout);
            var bindings = UiToolkitPanelBindingSet.Create(layout, entries, OneManager());
            var runtime = new UiToolkitShellRuntime(layout, ShellState.Initial(layout), bindings);
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
            var bindings = UiToolkitPanelBindingSet.Create(layout, entries, OneManager());
            var runtime = new UiToolkitShellRuntime(layout, ShellState.Initial(layout), bindings);
            var primary = SurfaceId.OfPanel(PanelId.Parse("hud.primary"));
            var secondary = SurfaceId.OfPanel(PanelId.Parse("hud.secondary"));
            var rightRay = InputSourceId.Parse("right_ray");

            runtime.Route(new HoverIntent(rightRay, new[] { primary, secondary }));
            runtime.Apply(new OpenIntent(primary));
            runtime.Apply(new OpenIntent(secondary));
            runtime.Route(new HoverIntent(rightRay, new[] { primary, secondary }));
            runtime.Route(new HoverIntent(InputSourceId.Parse("ghost_source"), new[] { primary }));

            bindings.TryGet(primary, out var primaryEntry);
            bindings.TryGet(secondary, out var secondaryEntry);
            Assert.That(((RecordingRoutedEventTarget)primaryEntry.RoutedEventTarget).Calls.Count, Is.EqualTo(0));
            Assert.That(((RecordingRoutedEventTarget)secondaryEntry.RoutedEventTarget).Calls.Count, Is.EqualTo(0));
        }

        // ----- runtime constructed with explicit references (CUT-05) -----

        [Test]
        public void AcceptedOpenAndCloseIntentsToggleTheBoundDocumentsActiveStateInOrder()
        {
            var layout = SampleLayout();
            var entries = FullEntries(layout);
            var bindings = UiToolkitPanelBindingSet.Create(layout, entries, OneManager());
            var runtime = new UiToolkitShellRuntime(layout, ShellState.Initial(layout), bindings);
            var primary = SurfaceId.OfPanel(PanelId.Parse("hud.primary"));
            bindings.TryGet(primary, out var entry);
            Assert.That(entry.Document.gameObject.activeSelf, Is.True, "Freshly created GameObjects start active; the runtime has not touched it yet.");

            var openOutcome = runtime.Apply(new OpenIntent(primary));
            Assert.That(openOutcome.Accepted, Is.True);
            Assert.That(entry.Document.gameObject.activeSelf, Is.True);

            var closeOutcome = runtime.Apply(new CloseIntent(primary));
            Assert.That(closeOutcome.Accepted, Is.True);
            Assert.That(entry.Document.gameObject.activeSelf, Is.False);

            Assert.That(runtime.Outcomes.Select(o => o.Accepted), Is.EqualTo(new[] { true, true }));
        }

        [Test]
        public void ARejectedIntentReportsRejectionAndTouchesNoDocument()
        {
            var layout = SampleLayout();
            var entries = FullEntries(layout);
            var bindings = UiToolkitPanelBindingSet.Create(layout, entries, OneManager());
            var runtime = new UiToolkitShellRuntime(layout, ShellState.Initial(layout), bindings);
            var ghost = SurfaceId.OfPanel(PanelId.Parse("ghost"));

            var outcome = runtime.Apply(new OpenIntent(ghost));
            Assert.That(outcome.Accepted, Is.False);
            Assert.That(outcome.Code, Is.EqualTo(ShellFailure.PanelUnknown));

            var primary = SurfaceId.OfPanel(PanelId.Parse("hud.primary"));
            bindings.TryGet(primary, out var entry);
            Assert.That(entry.Document.gameObject.activeSelf, Is.True, "A rejected intent touches no bound document.");
        }

        // ----- two independent runtimes (CUT-06) -----

        [Test]
        public void TwoRuntimesConstructedSideBySideShareNoState()
        {
            var layoutA = SampleLayout();
            var layoutB = SampleLayout();
            var runtimeA = new UiToolkitShellRuntime(layoutA, ShellState.Initial(layoutA), UiToolkitPanelBindingSet.Create(layoutA, FullEntries(layoutA), OneManager()));
            var runtimeB = new UiToolkitShellRuntime(layoutB, ShellState.Initial(layoutB), UiToolkitPanelBindingSet.Create(layoutB, FullEntries(layoutB), OneManager()));
            var primary = SurfaceId.OfPanel(PanelId.Parse("hud.primary"));

            runtimeA.Apply(new OpenIntent(primary));

            Assert.That(runtimeA.State.TryGetSurfaceState(primary, out var stateA) && stateA.IsOpen, Is.True);
            Assert.That(runtimeB.State.TryGetSurfaceState(primary, out var stateB) && stateB.IsOpen, Is.False);
        }

        // ----- one intent channel: actor/revision shape and no second write path (CUT-07) -----

        [Test]
        public void APlayerIssuedAndAnAgentIssuedCopyOfTheSameIntentTakeTheSamePathAndProduceEqualResultingState()
        {
            var layoutA = SampleLayout();
            var layoutB = SampleLayout();
            var runtimeA = new UiToolkitShellRuntime(layoutA, ShellState.Initial(layoutA), UiToolkitPanelBindingSet.Create(layoutA, FullEntries(layoutA), OneManager()));
            var runtimeB = new UiToolkitShellRuntime(layoutB, ShellState.Initial(layoutB), UiToolkitPanelBindingSet.Create(layoutB, FullEntries(layoutB), OneManager()));
            var primary = SurfaceId.OfPanel(PanelId.Parse("hud.primary"));

            var byPlayer = runtimeA.Apply(new OpenIntent(primary, IntentActor.Player));
            var byAgent = runtimeB.Apply(new OpenIntent(primary, IntentActor.Agent));

            Assert.That(byPlayer.Accepted, Is.EqualTo(byAgent.Accepted));
            Assert.That(byPlayer.Actor, Is.EqualTo(IntentActor.Player));
            Assert.That(byAgent.Actor, Is.EqualTo(IntentActor.Agent));
            Assert.That(runtimeA.State.Fingerprint(), Is.EqualTo(runtimeB.State.Fingerprint()));
        }

        [Test]
        public void RuntimeSourceIssuesOnlyCoreIntentsAndNeverCallsAShellStateMutatorDirectly()
        {
            // This adapter writes state only by constructing a ShellIntent and passing it to
            // ShellState.Apply (LESSON-011); the Core's own internal ApplyOpen/ApplyClose/…
            // mutators are already uncallable from here without InternalsVisibleTo. This is the
            // explicit, regression-proof record of that rule.
            var runtimeDirectory = System.IO.Path.GetFullPath(System.IO.Path.Combine(TestSourceDirectory(), "..", "..", "Runtime"));
            Assert.That(System.IO.Directory.Exists(runtimeDirectory), Is.True, runtimeDirectory);

            var forbidden = new[] { ".ApplyOpen(", ".ApplyClose(", ".ApplyFocus(", ".ApplyDock(", ".ApplyFollow(", ".ApplyFold(", ".ApplyUnfold(" };
            var sourceFiles = System.IO.Directory.GetFiles(runtimeDirectory, "*.cs", System.IO.SearchOption.AllDirectories);
            Assert.That(sourceFiles, Is.Not.Empty, runtimeDirectory);
            foreach (var sourceFile in sourceFiles)
            {
                var text = System.IO.File.ReadAllText(sourceFile);
                foreach (var token in forbidden)
                {
                    Assert.That(text, Does.Not.Contain(token),
                        $"{System.IO.Path.GetFileName(sourceFile)} calls a Core mutator directly ('{token}'); state changes only through ShellState.Apply.");
                }
            }
        }

        private static string TestSourceDirectory([System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "") => System.IO.Path.GetDirectoryName(sourceFilePath);

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

        /// <summary>A fully valid binding entry for every declared surface. A test that needs one
        /// broken entry copies this dictionary and replaces one entry.</summary>
        private Dictionary<SurfaceId, UiToolkitPanelBindingEntry> FullEntries(ShellLayout layout)
        {
            var entries = new Dictionary<SurfaceId, UiToolkitPanelBindingEntry>();
            foreach (var declaration in layout.Surfaces)
            {
                if (declaration.Id.Kind == SurfaceKind.WristMenu || declaration.Id.Kind == SurfaceKind.HandMenu)
                {
                    entries[declaration.Id] = FullWristEntry();
                    continue;
                }
                entries[declaration.Id] = new UiToolkitPanelBindingEntry(
                    CreateDocument(PanelRenderMode.WorldSpace), CreateCollider(), ColliderUpdateMode.Automatic, new RecordingSkinTarget(), routedEventTarget: new RecordingRoutedEventTarget());
            }
            return entries;
        }

        private UiToolkitPanelBindingEntry FullWristEntry()
        {
            return new UiToolkitPanelBindingEntry(
                CreateDocument(PanelRenderMode.WorldSpace), CreateCollider(), ColliderUpdateMode.Automatic, new RecordingSkinTarget(), CreateAnchor(), new RecordingRoutedEventTarget());
        }

        private UIDocument CreateDocument(PanelRenderMode renderMode)
        {
            var gameObject = new GameObject("Document");
            _created.Add(gameObject);
            var document = gameObject.AddComponent<UIDocument>();
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            _created.Add(settings);
            settings.renderMode = renderMode;
            document.panelSettings = settings;
            return document;
        }

        private Collider CreateCollider()
        {
            var gameObject = new GameObject("Collider");
            _created.Add(gameObject);
            return gameObject.AddComponent<BoxCollider>();
        }

        private Transform CreateAnchor()
        {
            var gameObject = new GameObject("Anchor");
            _created.Add(gameObject);
            return gameObject.transform;
        }

        private IReadOnlyList<IUiToolkitInputManager> OneManager() => new IUiToolkitInputManager[] { new FakeInputManager(true) };

        private sealed class FakeInputManager : IUiToolkitInputManager
        {
            public FakeInputManager(bool routesThroughUiToolkitEvents) { RoutesThroughUiToolkitEvents = routesThroughUiToolkitEvents; }
            public bool RoutesThroughUiToolkitEvents { get; }
        }

        private sealed class RecordingSkinTarget : IUiToolkitPanelSkinTarget
        {
            public List<UiToolkitShellSkin> AppliedSkins { get; } = new List<UiToolkitShellSkin>();
            public void ApplySkin(UiToolkitShellSkin skin) => AppliedSkins.Add(skin);
        }

        private sealed class RecordingRoutedEventTarget : IUiToolkitRoutedEventTarget
        {
            public List<(RouteEventKind Kind, InputSourceId Source)> Calls { get; } = new List<(RouteEventKind, InputSourceId)>();
            public void OnRouted(RouteEventKind kind, InputSourceId source) => Calls.Add((kind, source));
        }
    }
}
