using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Lingkyn.Unity.XrBaseline.Interaction;

namespace Lingkyn.Unity.XrBaseline.Tests
{
    public sealed class GrabbableHoverVisualTests
    {
        const string WarningPrefix = "xr_baseline_hover_visual_unresolved:";

        [Test]
        public void MissingGrabInteractableWarnsOncePerComponent()
        {
            var host = new GameObject("GrabbableHoverVisual_Test");
            host.SetActive(false);
            var warnings = 0;

            void CountWarning(string condition, string stackTrace, LogType type)
            {
                if (type == LogType.Warning && condition.StartsWith(WarningPrefix)) warnings++;
            }

            Application.logMessageReceived += CountWarning;
            try
            {
                var visual = host.AddComponent<GrabbableHoverVisual>();
                LogAssert.Expect(LogType.Warning, new Regex("^" + Regex.Escape(WarningPrefix)));

                Assert.That(visual.TryResolveGrabInteractable(), Is.False, "No XRGrabInteractable is present, so resolution must fail.");
                Assert.That(warnings, Is.EqualTo(1), "The first unresolved resolution must warn exactly once.");

                Assert.That(visual.TryResolveGrabInteractable(), Is.False);
                Assert.That(visual.TryResolveGrabInteractable(), Is.False);
                Assert.That(warnings, Is.EqualTo(1), "Repeated resolutions on the same component must not warn again.");
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Application.logMessageReceived -= CountWarning;
                UnityEngine.Object.DestroyImmediate(host);
            }
        }
    }
}
