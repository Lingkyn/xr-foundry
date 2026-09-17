using System.Collections.Generic;
using Lingkyn.Localization.Core;
using NUnit.Framework;

namespace Lingkyn.Localization.Unity.Editor.Tests
{
    // These tests compile and pass whether or not com.unity.localization is installed.
    // Each one branches on UnityLocalizationBridge.IsPackagePresent and asserts the
    // behaviour that configuration must show, so neither configuration is silently skipped.
    public sealed class UnityLocalizationBridgeTests
    {
        [Test]
        public void BridgeFailsClosedWithoutPackageAndNeverInvokesReader()
        {
            var readerInvoked = false;
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Reader(string collection)
            {
                readerInvoked = true;
                return Data(("en", ("greeting", "Hello")));
            }

            var result = UnityLocalizationBridge.TryImport("Ui", Reader, out var tables);

            if (!UnityLocalizationBridge.IsPackagePresent)
            {
                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Code, Is.EqualTo("bridge.package.missing"));
                Assert.That(readerInvoked, Is.False, "The reader must not run when the package is absent.");
                Assert.That(tables, Is.Empty);
            }
            else
            {
                Assert.That(readerInvoked, Is.True, "The reader must run when the package is present.");
                Assert.That(result.Succeeded, Is.True, result.Message);
            }
        }

        [Test]
        public void BridgeReportsMissingTableWhenReaderReturnsNothing()
        {
            var empty = new Dictionary<string, IReadOnlyDictionary<string, string>>();

            var emptyResult = UnityLocalizationBridge.TryImport("Ui", _ => empty, out var emptyTables);
            var nullResult = UnityLocalizationBridge.TryImport("Ui", _ => null, out var nullTables);

            if (UnityLocalizationBridge.IsPackagePresent)
            {
                Assert.That(emptyResult.Succeeded, Is.False);
                Assert.That(emptyResult.Code, Is.EqualTo("bridge.table.missing"));
                Assert.That(nullResult.Code, Is.EqualTo("bridge.table.missing"));
            }
            else
            {
                Assert.That(emptyResult.Code, Is.EqualTo("bridge.package.missing"));
                Assert.That(nullResult.Code, Is.EqualTo("bridge.package.missing"));
            }
            Assert.That(emptyTables, Is.Empty);
            Assert.That(nullTables, Is.Empty);
        }

        [Test]
        public void BridgeBuildsOneTablePerLocaleFromReaderData()
        {
            var result = UnityLocalizationBridge.TryImport("Ui", _ => Data(("en", ("greeting", "Hello"))), out var tables);

            if (UnityLocalizationBridge.IsPackagePresent)
            {
                Assert.That(result.Succeeded, Is.True, result.Message);
                Assert.That(result.Code, Is.Empty);
                Assert.That(tables.Count, Is.EqualTo(1));
                Assert.That(tables[0].Locale.Tag, Is.EqualTo("en"));
                Assert.That(tables[0].Count, Is.EqualTo(1));
                Assert.That(tables[0].TryGet(MessageId.Parse("greeting"), out var template), Is.True);
                Assert.That(template.Source, Is.EqualTo("Hello"));
            }
            else
            {
                Assert.That(result.Code, Is.EqualTo("bridge.package.missing"));
                Assert.That(tables, Is.Empty);
            }
        }

        [Test]
        public void BridgeRejectsInvalidLocaleOrEntryWithStableCode()
        {
            var badLocale = UnityLocalizationBridge.TryImport("Ui", _ => Data(("english", ("greeting", "Hello"))), out _);
            var badEntry = UnityLocalizationBridge.TryImport("Ui", _ => Data(("en", ("greeting", "{n, plural, one {x}}"))), out _);

            if (UnityLocalizationBridge.IsPackagePresent)
            {
                Assert.That(badLocale.Code, Is.EqualTo("bridge.locale.invalid"));
                Assert.That(badEntry.Code, Is.EqualTo("bridge.entry.invalid"));
            }
            else
            {
                Assert.That(badLocale.Code, Is.EqualTo("bridge.package.missing"));
                Assert.That(badEntry.Code, Is.EqualTo("bridge.package.missing"));
            }
        }

        private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Data(
            params (string Locale, (string Id, string Template) Entry)[] rows)
        {
            var result = new Dictionary<string, IReadOnlyDictionary<string, string>>();
            foreach (var row in rows)
            {
                if (!result.TryGetValue(row.Locale, out var messages))
                {
                    messages = new Dictionary<string, string>();
                    result[row.Locale] = messages;
                }
                ((Dictionary<string, string>)messages)[row.Entry.Id] = row.Entry.Template;
            }
            return result;
        }
    }
}
