using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Lingkyn.Unity.XrBaseline.Editor;

namespace Lingkyn.Unity.XrBaseline.Tests
{
    public sealed class XrBaselineDiagnosticsTests
    {
        [Test]
        public void UnresolvedMemberIsReportedOncePerKeyUntilReset()
        {
            XrBaselineDiagnostics.Reset();
            LogAssert.Expect(LogType.Warning, new Regex("^xr_baseline_unresolved: test\\.key: first reason"));

            Assert.That(XrBaselineDiagnostics.Unresolved("test.key", "first reason"), Is.True);
            Assert.That(XrBaselineDiagnostics.Unresolved("test.key", "second reason"), Is.False, "A repeated key must not log again.");
            Assert.That(XrBaselineDiagnostics.ReportedKeys, Does.Contain("test.key"));

            XrBaselineDiagnostics.Reset();
            Assert.That(XrBaselineDiagnostics.ReportedKeys, Is.Empty);
            LogAssert.Expect(LogType.Warning, new Regex("^xr_baseline_unresolved: test\\.key: after reset"));
            Assert.That(XrBaselineDiagnostics.Unresolved("test.key", "after reset"), Is.True);
            XrBaselineDiagnostics.Reset();
        }
    }
}
