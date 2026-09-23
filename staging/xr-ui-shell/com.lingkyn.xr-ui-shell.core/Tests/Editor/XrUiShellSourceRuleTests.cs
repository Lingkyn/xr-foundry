using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Lingkyn.XrUiShell.Core.Editor.Tests
{
    // Source rule (WI-023 step 2): the shell is the dock and every system's panel, the developer
    // scaffold included, is a peer client; the dock never references a client
    // (docs/standards/xr-ui-shell/README.md, docs/standards/live-tuning/README.md). This is the
    // machine-checked half of that rule: no asmdef under staging/xr-ui-shell references any
    // assembly outside Lingkyn.XrUiShell.* (plus the admitted engine and test assemblies), and no
    // shell Runtime .cs mentions LiveTuning, Inventory, Settings, or another family namespace by
    // name. Authored and unexecuted: this test has never compiled or run.
    public sealed class XrUiShellSourceRuleTests
    {
        private static readonly string[] AllowedReferencePrefixes =
        {
            "Lingkyn.XrUiShell.",
            "UnityEngine",
            "UnityEditor",
            "Unity.TextMeshPro",
            "TMPro",
            "NUnit",
        };

        private static readonly string[] ForbiddenFamilyTokens = { "LiveTuning", "Inventory", "Settings" };

        [Test]
        public void NoAsmdefUnderTheShellFamilyReferencesAnyAssemblyOutsideTheAllowedSet()
        {
            var familyRoot = ShellFamilyRoot();
            var asmdefPaths = Directory.GetFiles(familyRoot, "*.asmdef", SearchOption.AllDirectories);
            Assert.That(asmdefPaths, Is.Not.Empty, familyRoot);

            foreach (var asmdefPath in asmdefPaths)
            {
                var references = ExtractReferences(File.ReadAllText(asmdefPath));
                foreach (var reference in references)
                {
                    Assert.That(
                        AllowedReferencePrefixes.Any(prefix => reference.StartsWith(prefix, StringComparison.Ordinal)),
                        Is.True,
                        $"{Path.GetFileName(asmdefPath)} references '{reference}', which is outside Lingkyn.XrUiShell.* and the admitted engine/test assemblies.");
                }
            }
        }

        [Test]
        public void NoShellRuntimeSourceMentionsAnotherFamilyNamespace()
        {
            var familyRoot = ShellFamilyRoot();
            var runtimeDirectories = Directory.GetDirectories(familyRoot, "Runtime", SearchOption.AllDirectories);
            Assert.That(runtimeDirectories, Is.Not.Empty, familyRoot);

            foreach (var runtimeDirectory in runtimeDirectories)
            {
                var sourceFiles = Directory.GetFiles(runtimeDirectory, "*.cs", SearchOption.AllDirectories);
                foreach (var sourceFile in sourceFiles)
                {
                    var text = File.ReadAllText(sourceFile);
                    foreach (var token in ForbiddenFamilyTokens)
                    {
                        // A whole-identifier match: "Settings" must not appear as its own name
                        // (a family namespace or type), but this must not flag an unrelated
                        // engine identifier that merely contains the word, such as
                        // UnityEngine.UIElements.PanelSettings.
                        var pattern = $@"\b{Regex.Escape(token)}\b";
                        Assert.That(Regex.IsMatch(text, pattern), Is.False, $"{Path.GetFileName(sourceFile)} must not mention '{token}'.");
                    }
                }
            }
        }

        /// <summary>A dependency-free, minimal reader for the one JSON array this rule needs: every
        /// quoted string between the <c>"references"</c> key's <c>[</c> and its matching
        /// <c>]</c>. An asmdef is small, flat JSON, so this never needs a full parser.</summary>
        private static string[] ExtractReferences(string asmdefJson)
        {
            const string key = "\"references\"";
            var keyIndex = asmdefJson.IndexOf(key, StringComparison.Ordinal);
            if (keyIndex < 0) return Array.Empty<string>();
            var open = asmdefJson.IndexOf('[', keyIndex);
            var close = asmdefJson.IndexOf(']', open);
            if (open < 0 || close < 0) return Array.Empty<string>();
            var body = asmdefJson.Substring(open + 1, close - open - 1);
            return body.Split(',')
                .Select(entry => entry.Trim().Trim('"'))
                .Where(entry => entry.Length > 0)
                .ToArray();
        }

        private static string ShellFamilyRoot([CallerFilePath] string sourceFilePath = "") =>
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFilePath), "..", "..", ".."));
    }
}
