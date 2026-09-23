using System.Collections.Generic;
using Lingkyn.LiveTuning.Core;
using Lingkyn.XrUiShell.Core;
using NUnit.Framework;
using UnityEngine;

namespace Lingkyn.LiveTuning.Unity.Editor.Tests
{
    // Authored and unexecuted: this assembly has never compiled or run. Proves that live tuning
    // is a peer client of the XR UI shell (LESSON-010, docs/standards/lessons/lessons-register.json):
    // it registers its own "tune" verb into the shell's VerbRegistry, adapts the shell's generic
    // IShellPanelContent<TuningSlot> seam to its own ITuningPanelSurface so a real TuningPanelHost
    // attaches slots to a bound shell panel, and maps the shell's FocusSubject to a
    // TuningPanelHost scope, all without the shell referencing any live tuning type (proved
    // separately, in the shell Core test assembly, by a source-rule test that no asmdef under
    // staging/xr-ui-shell references Lingkyn.LiveTuning.* and no shell Runtime .cs mentions
    // "LiveTuning"). Moved here from the shell's UGUI adapter test assembly, where this same
    // scenario used to depend on a shell-owned adapter that referenced Live Tuning types: that
    // was the defect this item fixes. Outside the coverage map's numbered Unity adapter-gate
    // clauses; listed under that gate's additional_tests_outside_clauses.
    public sealed class ShellTuningClientTests
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

        [Test]
        public void TuningVerbRegistersIntoAShellVerbRegistryLikeAnyPeer()
        {
            var builder = new VerbRegistryBuilder();
            var result = TuningVerb.Register(builder, VerbWireState.Wired);
            Assert.That(result.Succeeded, Is.True, result.Message);

            var registry = builder.Build();
            Assert.That(registry.TryGet(VerbId.Parse(TuningVerb.Id), out var registration), Is.True);
            Assert.That(registration.DisplayWord, Is.EqualTo(TuningVerb.DisplayWord));
            Assert.That(registration.WireState, Is.EqualTo(VerbWireState.Wired));

            var duplicate = TuningVerb.Register(builder, VerbWireState.Wired);
            Assert.That(duplicate.Succeeded, Is.False);
            Assert.That(duplicate.Code, Is.EqualTo(ShellFailure.VerbDuplicate));
        }

        [Test]
        public void ShellTuningClientAttachesOneSlotFromARealTuningPanelHost()
        {
            var host = BuildHost(out var client, out _);
            host.AttachAll();

            Assert.That(client.Slots.Count, Is.EqualTo(1));

            host.Runtime.Apply(new ResetAllIntent());
            client.RemoveAllSlots();
            Assert.That(client.Slots.Count, Is.EqualTo(0));
        }

        [Test]
        public void ApplyFocusSelectsAPanelTargetThroughTheBindingIndex()
        {
            // The bound asset key embeds the panel's own SurfaceId text as its leading segment
            // ("Panel:hud.primary/primary"), the same "<selector>/<asset_key>#<member>" composite
            // convention this package's own pick-to-tune tests already use
            // (LiveTuningUnityContractTests.SelectionHost): BindingIndex compares target paths as
            // opaque, segment-wise text, never a SurfaceId or panel concept of its own, so a
            // shell panel becomes selectable purely by naming its identity text inside a binding's
            // own target path.
            var host = BuildHost(out _, out var panelId);
            host.AttachAll();

            var scope = ShellTuningClient.ApplyFocus(host, FocusSubject.None.Claim(FocusTarget.OfPanel(panelId)));

            Assert.That(scope.Name, Is.EqualTo(TuningScope.SelectionScopeName));
            Assert.That(scope.Slots.Count, Is.EqualTo(1));
            Assert.That(host.ActiveSelectionDiagnostic, Is.Null);
        }

        [Test]
        public void ApplyFocusWithNoCurrentTargetRestoresThePreviousScope()
        {
            var host = BuildHost(out _, out var panelId);
            host.AttachAll();

            ShellTuningClient.ApplyFocus(host, FocusSubject.None.Claim(FocusTarget.OfPanel(panelId)));
            var restored = ShellTuningClient.ApplyFocus(host, FocusSubject.None);

            Assert.That(restored.Name, Is.EqualTo(TuningScope.AllScopeName));
        }

        [Test]
        public void ApplyFocusOnAnExternalTargetNoBindingTargetsReportsSelectionUnboundRatherThanASilentEmptyPanel()
        {
            var host = BuildHost(out _, out _);
            host.AttachAll();

            var scope = ShellTuningClient.ApplyFocus(host, FocusSubject.None.Claim(FocusTarget.OfExternal("nothing/targets/this")));

            Assert.That(scope.Name, Is.EqualTo(TuningScope.SelectionScopeName));
            Assert.That(scope.Slots, Is.Empty);
            Assert.That(host.ActiveSelectionDiagnostic, Is.Not.Null);
            Assert.That(host.ActiveSelectionDiagnostic.Code, Is.EqualTo(LiveTuningUnityFailure.SelectionUnbound));
        }

        private TuningPanelHost BuildHost(out ShellTuningClient client, out SurfaceId panelId)
        {
            var registry = new TunableRegistryBuilder()
                .Register(TunableId.Parse("skin.corner_radius"), new FloatDeclaration(0f, 32f, 0.5f), TunableValue.OfFloat(8f))
                .Value.Build();

            var skinAsset = ScriptableObject.CreateInstance<LiveTuningDemoSkinAsset>();
            _created.Add(skinAsset);

            panelId = SurfaceId.OfPanel(PanelId.Parse("hud.primary"));
            var assetKey = $"{panelId}/primary";
            var bindings = SkinBindingSet.Create(
                new[] { new BindingRecord(TunableId.Parse("skin.corner_radius"), TunableKind.Float, SkinTargetPath.Format(assetKey, LiveTuningDemoSkinBinder.CornerRadiusMember)) },
                registry,
                new Dictionary<string, Object> { [assetKey] = skinAsset },
                new ISkinBinder[] { new LiveTuningDemoSkinBinder() });

            var runtime = new TuningRuntime(registry, TuningState.Initial(registry), bindings, new RecordingSkinApplyTarget(), new RecordingExportSink());
            client = new ShellTuningClient(panelId);
            return new TuningPanelHost(runtime, client);
        }

        private sealed class RecordingSkinApplyTarget : ISkinApplyTarget
        {
            public void ApplySkin(Object skin) { }
        }

        private sealed class RecordingExportSink : ITuningExportSink
        {
            public TuningExportResult Write(TokenOverrideDocument document, string label) =>
                TuningExportResult.Ok(string.Empty, string.Empty, 0, new List<TunableId>());

            public string DescribeDestination() => string.Empty;
        }
    }
}
