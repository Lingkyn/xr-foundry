using System.Collections.Generic;
using Lingkyn.LiveTuning.Core;
using Lingkyn.LiveTuning.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Lingkyn.XrUiShell.Ugui.LiveTuning;

namespace Lingkyn.XrUiShell.Ugui.Editor.Tests
{
    // Authored and unexecuted: this assembly has never compiled or run. Proves that a bound XR
    // UI Shell panel satisfies the Live Tuning family's ITuningPanelSurface seam as one client of
    // the shell, without either family referencing the other's concrete skin or panel type
    // (outside the coverage-map's numbered UGUI adapter-gate clauses; listed under that gate's
    // additional_tests_outside_clauses).
    public sealed class XrUiShellUguiLiveTuningIntegrationTests
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
        public void AShellPanelAdapterAttachesOneSlotFromARealLiveTuningPanelHost()
        {
            var registry = new TunableRegistryBuilder()
                .Register(TunableId.Parse("skin.corner_radius"), new IntegerDeclaration(0, 32), TunableValue.OfInteger(8))
                .Value.Build();

            var skinAsset = ScriptableObject.CreateInstance<LiveTuningDemoSkinAsset>();
            _created.Add(skinAsset);
            var bindings = SkinBindingSet.Create(
                new[] { new BindingRecord(TunableId.Parse("skin.corner_radius"), TunableKind.Integer, SkinTargetPath.Format("primary", LiveTuningDemoSkinBinder.CornerRadiusMember)) },
                registry,
                new Dictionary<string, Object> { ["primary"] = skinAsset },
                new ISkinBinder[] { new LiveTuningDemoSkinBinder() });

            var runtime = new TuningRuntime(registry, TuningState.Initial(registry), bindings, new RecordingSkinApplyTarget(), new RecordingExportSink());

            var canvasObject = new GameObject("ShellPanelCanvas");
            _created.Add(canvasObject);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var panelEntry = new UguiPanelBindingEntry(canvas, null, canvasObject.AddComponent<GraphicRaycaster>(), null);
            var adapter = new ShellTuningPanelSurfaceAdapter(panelEntry);

            var host = new TuningPanelHost(runtime, adapter);
            host.AttachAll();

            Assert.That(adapter.Slots.Count, Is.EqualTo(1));
            Assert.That(adapter.Panel, Is.SameAs(panelEntry));

            host.Runtime.Apply(new ResetAllIntent());
            adapter.RemoveAllSlots();
            Assert.That(adapter.Slots.Count, Is.EqualTo(0));
        }

        private sealed class RecordingSkinApplyTarget : ISkinApplyTarget
        {
            public void ApplySkin(Object skin) { }
        }

        private sealed class RecordingExportSink : ITuningExportSink
        {
            public TuningExportResult Write(TokenOverrideDocument document, string label) =>
                TuningExportResult.Ok(string.Empty, string.Empty, 0, new List<TunableId>());
        }
    }
}
