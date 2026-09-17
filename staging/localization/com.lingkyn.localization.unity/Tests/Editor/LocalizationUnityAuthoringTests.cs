using System.Collections.Generic;
using System.Linq;
using Lingkyn.Localization.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Lingkyn.Localization.Unity.Editor.Tests
{
    public sealed class LocalizationUnityAuthoringTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _created)
            {
                if (asset != null) Object.DestroyImmediate(asset);
            }
            _created.Clear();
        }

        [Test]
        public void TableAssetConvertsToDomainTableWithoutMutation()
        {
            var table = CreateTable("en", ("greeting", "Hello {name}"), ("items", "{n, plural, one {# item} other {# items}}"));
            var before = EditorJsonUtility.ToJson(table);

            var domain = table.ToDomain();

            Assert.That(domain.Locale.Tag, Is.EqualTo("en"));
            Assert.That(domain.Count, Is.EqualTo(2));
            Assert.That(domain.TryGet(MessageId.Parse("greeting"), out var template), Is.True);
            Assert.That(template.Source, Is.EqualTo("Hello {name}"));
            Assert.That(EditorJsonUtility.ToJson(table), Is.EqualTo(before));
        }

        [Test]
        public void TableAssetValidationReportsStableCodesAndConversionThrows()
        {
            var table = CreateTable("english", ("Bad Id", "x"), ("dup", "a"), ("dup", "b"), ("broken", "{n, plural, one {x}}"));

            var report = LocalizationAuthoringValidation.Validate(table);

            Assert.That(report.IsValid, Is.False);
            var codes = report.Diagnostics.Select(item => (item.Code, item.FieldPath)).ToList();
            Assert.That(codes, Does.Contain(("table.locale.invalid", "locale")));
            Assert.That(codes, Does.Contain(("table.entry.id.invalid", "entries.Array.data[0].id")));
            Assert.That(codes, Does.Contain(("table.entry.duplicateId", "entries.Array.data[2].id")));
            Assert.That(codes, Does.Contain(("table.entry.template.malformed", "entries.Array.data[3].template")));
            Assert.That(report.Diagnostics.All(item => item.Source == table), Is.True);
            var thrown = Assert.Throws<LocalizationAuthoringException>(() => table.ToDomain());
            Assert.That(thrown.Report.Diagnostics.Count, Is.EqualTo(report.Diagnostics.Count));
        }

        [Test]
        public void CatalogAssetValidationReportsMissingTableDuplicateLocaleAndDefaultWithoutTable()
        {
            var first = CreateTable("fr", ("a", "A"));
            var second = CreateTable("fr-FR", ("a", "A"));
            var duplicate = CreateTable("FR", ("a", "A"));
            var catalog = CreateCatalog("en", first, null, second, duplicate);

            var report = LocalizationAuthoringValidation.Validate(catalog);

            var codes = report.Diagnostics.Select(item => (item.Code, item.FieldPath)).ToList();
            Assert.That(codes, Does.Contain(("catalog.table.missing", "tables.Array.data[1]")));
            Assert.That(codes, Does.Contain(("catalog.table.duplicateLocale", "locale")));
            Assert.That(codes, Does.Contain(("catalog.defaultLocale.noTable", "defaultLocale")));
            Assert.That(report.Diagnostics.Single(item => item.Code == "catalog.table.duplicateLocale").Source, Is.EqualTo(duplicate));
            Assert.Throws<LocalizationAuthoringException>(() => catalog.ToDomain());

            var badDefault = CreateCatalog("not a locale", first);
            var badReport = LocalizationAuthoringValidation.Validate(badDefault);
            Assert.That(badReport.Diagnostics.Any(item => item.Code == "catalog.defaultLocale.invalid"), Is.True);
        }

        [Test]
        public void CatalogAssetConvertsAndFormatsWithFallback()
        {
            var en = CreateTable("en", ("items", "{n, plural, one {# item} other {# items}}"), ("greeting", "Hello"));
            var ru = CreateTable("ru", ("items", "{n, plural, one {#} few {#} many {#} other {#}}"));
            var catalog = CreateCatalog("en", en, ru).ToDomain();

            var greeting = catalog.Format(MessageId.Parse("greeting"), LocaleId.Parse("ru-RU"), MessageArguments.None);
            Assert.That(greeting.Succeeded, Is.True, greeting.Message);
            Assert.That(greeting.Value.Text, Is.EqualTo("Hello"));
            Assert.That(greeting.Value.Resolved.Tag, Is.EqualTo("en"));
            Assert.That(greeting.Value.UsedFallback, Is.True);

            var items = catalog.Format(MessageId.Parse("items"), LocaleId.Parse("ru-RU"), new MessageArguments().WithNumber("n", 3));
            Assert.That(items.Value.Text, Is.EqualTo("3"));
            Assert.That(items.Value.Resolved.Tag, Is.EqualTo("ru"));
        }

        [Test]
        public void SystemLanguageMapIsExplicitAndFailsClosed()
        {
            Assert.That(SystemLocaleMap.TryMap(SystemLanguage.English, out var english), Is.True);
            Assert.That(english.Tag, Is.EqualTo("en"));
            Assert.That(SystemLocaleMap.TryMap(SystemLanguage.ChineseTraditional, out var traditional), Is.True);
            Assert.That(traditional.Tag, Is.EqualTo("zh-Hant"));
            Assert.That(SystemLocaleMap.TryMap(SystemLanguage.ChineseSimplified, out var simplified), Is.True);
            Assert.That(simplified.Parent.Tag, Is.EqualTo("zh"));
            Assert.That(SystemLocaleMap.TryMap(SystemLanguage.Unknown, out var unknown), Is.False);
            Assert.That(unknown.IsRoot, Is.True);
        }

        [Test]
        public void RuntimeRaisesLocaleChangedOnceAndFormatsInNewLocale()
        {
            var en = CreateTable("en", ("greeting", "Hello"));
            var de = CreateTable("de", ("greeting", "Hallo"));
            var runtime = new LocalizationRuntime(CreateCatalog("en", en, de).ToDomain(), LocaleId.Parse("en"));
            var raised = new List<string>();
            runtime.LocaleChanged += locale => raised.Add(locale.Tag);

            Assert.That(runtime.Format("greeting", MessageArguments.None).Value.Text, Is.EqualTo("Hello"));
            Assert.That(runtime.SetLocale(LocaleId.Parse("de")), Is.True);
            Assert.That(runtime.SetLocale(LocaleId.Parse("DE")), Is.False, "Setting the same locale again must not raise.");
            Assert.That(raised, Is.EqualTo(new[] { "de" }));
            Assert.That(runtime.Format("greeting", MessageArguments.None).Value.Text, Is.EqualTo("Hallo"));
            Assert.That(runtime.Format("Bad Id", MessageArguments.None).Failure, Is.EqualTo(LocalizationFailure.InvalidMessageId));
        }

        [Test]
        public void RuntimeReportsFallbackWhenLocaleHasNoDirectTable()
        {
            var en = CreateTable("en", ("greeting", "Hello"));
            var runtime = new LocalizationRuntime(CreateCatalog("en", en).ToDomain(), LocaleId.Parse("ja"));

            Assert.That(runtime.HasDirectTable(LocaleId.Parse("ja")), Is.False);
            Assert.That(runtime.HasDirectTable(LocaleId.Parse("en")), Is.True);
            var formatted = runtime.Format("greeting", MessageArguments.None);
            Assert.That(formatted.Succeeded, Is.True, formatted.Message);
            Assert.That(formatted.Value.UsedFallback, Is.True);
            Assert.That(formatted.Value.Resolved.Tag, Is.EqualTo("en"));
        }

        private MessageTableAsset CreateTable(string locale, params (string Id, string Template)[] entries)
        {
            var asset = ScriptableObject.CreateInstance<MessageTableAsset>();
            _created.Add(asset);
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("locale").stringValue = locale;
            var list = serialized.FindProperty("entries");
            list.arraySize = entries.Length;
            for (var index = 0; index < entries.Length; index++)
            {
                var element = list.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("id").stringValue = entries[index].Id;
                element.FindPropertyRelative("template").stringValue = entries[index].Template;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private LocalizationCatalogAsset CreateCatalog(string defaultLocale, params MessageTableAsset[] tables)
        {
            var asset = ScriptableObject.CreateInstance<LocalizationCatalogAsset>();
            _created.Add(asset);
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("defaultLocale").stringValue = defaultLocale;
            var list = serialized.FindProperty("tables");
            list.arraySize = tables.Length;
            for (var index = 0; index < tables.Length; index++)
            {
                list.GetArrayElementAtIndex(index).objectReferenceValue = tables[index];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }
    }
}
