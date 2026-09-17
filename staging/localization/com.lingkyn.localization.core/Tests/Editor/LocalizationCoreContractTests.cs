using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Lingkyn.Localization.Core;

namespace Lingkyn.Localization.Core.Editor.Tests
{
    public sealed class LocalizationCoreContractTests
    {
        private static readonly LocaleId En = LocaleId.Parse("en");
        private static readonly LocaleId EnUs = LocaleId.Parse("en-US");
        private static readonly LocaleId Ru = LocaleId.Parse("ru");
        private static readonly LocaleId Ar = LocaleId.Parse("ar");
        private static readonly LocaleId Fr = LocaleId.Parse("fr");
        private static readonly LocaleId Ja = LocaleId.Parse("ja");

        [Test]
        public void LocaleIdParsesAndCanonicalizesSubtags()
        {
            var traditional = LocaleId.TryParse("ZH-hant-tw");
            Assert.That(traditional.Succeeded, Is.True, traditional.Message);
            Assert.That(traditional.Value.Tag, Is.EqualTo("zh-Hant-TW"));
            Assert.That(traditional.Value.Parent.Tag, Is.EqualTo("zh-Hant"));
            Assert.That(traditional.Value.Parent.Parent.Tag, Is.EqualTo("zh"));
            Assert.That(traditional.Value.Parent.Parent.Parent.IsRoot, Is.True);

            Assert.That(LocaleId.TryParse("en_US").Value.Tag, Is.EqualTo("en-US"));
            Assert.That(LocaleId.TryParse("es-419").Value.Region, Is.EqualTo("419"));
            Assert.That(LocaleId.TryParse(" ROOT ").Value.IsRoot, Is.True);
            Assert.That(LocaleId.Parse("en-US"), Is.EqualTo(LocaleId.Parse("EN-us")));
        }

        [Test]
        public void LocaleIdRejectsUnsupportedShapes()
        {
            foreach (var tag in new[] { "", " ", "e", "1234", "en-US-x-private", "en-Latn-US-extra", "en-Latn-Latn", "en--US" })
            {
                var result = LocaleId.TryParse(tag);
                Assert.That(result.Succeeded, Is.False, tag);
                Assert.That(result.Failure, Is.EqualTo(LocalizationFailure.InvalidLocale), tag);
            }
        }

        [Test]
        public void MessageIdAcceptsStableShapeAndRejectsOthers()
        {
            Assert.That(MessageId.TryCreate("menu.inventory.title").Succeeded, Is.True);
            Assert.That(MessageId.TryCreate("item_count-2").Succeeded, Is.True);
            foreach (var value in new[] { "", "Menu.Title", "has space", ".leading", "-leading", new string('a', 129) })
            {
                var result = MessageId.TryCreate(value);
                Assert.That(result.Succeeded, Is.False, value);
                Assert.That(result.Failure, Is.EqualTo(LocalizationFailure.InvalidMessageId), value);
            }
        }

        [Test]
        public void TemplateParsesPlaceholdersPluralSelectAndReportsArgumentKinds()
        {
            var parsed = MessageTemplate.TryParse("{name} has {count, plural, one {# item} other {# items}} ({gender, select, female {her} other {their}} bag)");
            Assert.That(parsed.Succeeded, Is.True, parsed.Message);
            var kinds = parsed.Value.Arguments.ToDictionary(argument => argument.Name, argument => argument.Kind);
            Assert.That(kinds["name"], Is.EqualTo(ArgumentKind.Text));
            Assert.That(kinds["count"], Is.EqualTo(ArgumentKind.Number));
            Assert.That(kinds["gender"], Is.EqualTo(ArgumentKind.Select));
            var plurals = parsed.Value.PluralCategoriesByArgument();
            Assert.That(plurals["count"], Is.EquivalentTo(new[] { PluralCategory.One, PluralCategory.Other }));
        }

        [Test]
        public void TemplateRejectsMalformedInputWithStableFailure()
        {
            foreach (var source in new[]
            {
                "{count, plural, one {# item}}",
                "{count, plural, one {# item} other {# items}",
                "{count, plural, lots {x} other {y}}",
                "{count, plural, one {x} one {y} other {z}}",
                "{value, date, short}",
                "{name",
                "stray } brace",
                "{gender, select, female {her}}",
                "{n} and {n, select, a {x} other {y}}",
                "'{unterminated",
            })
            {
                var result = MessageTemplate.TryParse(source);
                Assert.That(result.Succeeded, Is.False, source);
                Assert.That(result.Failure, Is.EqualTo(LocalizationFailure.TemplateMalformed), source);
                Assert.That(result.Message, Is.Not.Empty, source);
            }
        }

        [Test]
        public void FormatSubstitutesTextAndNumbers()
        {
            var template = MessageTemplate.Parse("Hello {name}, level {level}.");
            var text = template.Format(new MessageArguments().WithText("name", "Ada").WithNumber("level", 12), En, CldrPluralRules.Default);
            Assert.That(text.Succeeded, Is.True, text.Message);
            Assert.That(text.Value, Is.EqualTo("Hello Ada, level 12."));
        }

        [Test]
        public void FormatPluralEnglishUsesExactBranchThenCategories()
        {
            var template = MessageTemplate.Parse("{count, plural, =0 {No items} one {# item} other {# items}}");
            Assert.That(Format(template, En, 0), Is.EqualTo("No items"));
            Assert.That(Format(template, En, 1), Is.EqualTo("1 item"));
            Assert.That(Format(template, En, 2), Is.EqualTo("2 items"));
            Assert.That(Format(template, En, 1.5m), Is.EqualTo("1.5 items"));
        }

        [Test]
        public void FormatPluralRussianSelectsOneFewManyOther()
        {
            var template = MessageTemplate.Parse("{count, plural, one {# предмет} few {# предмета} many {# предметов} other {# предмета}}");
            Assert.That(Format(template, Ru, 1), Is.EqualTo("1 предмет"));
            Assert.That(Format(template, Ru, 2), Is.EqualTo("2 предмета"));
            Assert.That(Format(template, Ru, 5), Is.EqualTo("5 предметов"));
            Assert.That(Format(template, Ru, 11), Is.EqualTo("11 предметов"));
            Assert.That(Format(template, Ru, 21), Is.EqualTo("21 предмет"));
            Assert.That(Format(template, Ru, 22), Is.EqualTo("22 предмета"));
            Assert.That(Format(template, Ru, 1.5m), Is.EqualTo("1.5 предмета"));
        }

        [Test]
        public void FormatPluralArabicCoversSixCategories()
        {
            var template = MessageTemplate.Parse("{n, plural, zero {zero} one {one} two {two} few {few} many {many} other {other}}");
            Assert.That(Format(template, Ar, 0), Is.EqualTo("zero"));
            Assert.That(Format(template, Ar, 1), Is.EqualTo("one"));
            Assert.That(Format(template, Ar, 2), Is.EqualTo("two"));
            Assert.That(Format(template, Ar, 3), Is.EqualTo("few"));
            Assert.That(Format(template, Ar, 10), Is.EqualTo("few"));
            Assert.That(Format(template, Ar, 11), Is.EqualTo("many"));
            Assert.That(Format(template, Ar, 99), Is.EqualTo("many"));
            Assert.That(Format(template, Ar, 100), Is.EqualTo("other"));
            Assert.That(Format(template, Ar, 1.5m), Is.EqualTo("other"));
        }

        [Test]
        public void FormatPluralFrenchTreatsZeroAsOne()
        {
            var template = MessageTemplate.Parse("{n, plural, one {# objet} other {# objets}}");
            Assert.That(Format(template, Fr, 0), Is.EqualTo("0 objet"));
            Assert.That(Format(template, Fr, 1), Is.EqualTo("1 objet"));
            Assert.That(Format(template, Fr, 2), Is.EqualTo("2 objets"));
            Assert.That(Format(template, Ja, 1, "{n, plural, other {#個}}"), Is.EqualTo("1個"));
        }

        [Test]
        public void FormatSelectFallsBackToOther()
        {
            var template = MessageTemplate.Parse("{gender, select, female {She} male {He} other {They}} waved.");
            Assert.That(template.Format(new MessageArguments().WithText("gender", "female"), En, null).Value, Is.EqualTo("She waved."));
            Assert.That(template.Format(new MessageArguments().WithText("gender", "unknown"), En, null).Value, Is.EqualTo("They waved."));
        }

        [Test]
        public void FormatQuotingProducesLiteralBracesHashesAndApostrophes()
        {
            var template = MessageTemplate.Parse("'{'{name}'}' it''s '#'1 don't");
            var text = template.Format(new MessageArguments().WithText("name", "Ada"), En, null);
            Assert.That(text.Succeeded, Is.True, text.Message);
            Assert.That(text.Value, Is.EqualTo("{Ada} it's #1 don't"));
        }

        [Test]
        public void FormatMissingArgumentFailsWithCode()
        {
            var template = MessageTemplate.Parse("Hello {name}");
            var text = template.Format(MessageArguments.None, En, null);
            Assert.That(text.Succeeded, Is.False);
            Assert.That(text.Failure, Is.EqualTo(LocalizationFailure.MissingArgument));
            Assert.That(text.Message, Does.Contain("name"));
        }

        [Test]
        public void FormatPluralWithTextArgumentIsTypeMismatch()
        {
            var template = MessageTemplate.Parse("{count, plural, one {#} other {#}}");
            var text = template.Format(new MessageArguments().WithText("count", "many"), En, null);
            Assert.That(text.Succeeded, Is.False);
            Assert.That(text.Failure, Is.EqualTo(LocalizationFailure.ArgumentTypeMismatch));
        }

        [Test]
        public void FormatWithUnknownPluralRulesFailsUnlessOnlyOtherIsUsed()
        {
            var unknown = LocaleId.Parse("xx");
            var categorized = MessageTemplate.Parse("{n, plural, one {a} other {b}}");
            var failed = categorized.Format(new MessageArguments().WithNumber("n", 1), unknown, null);
            Assert.That(failed.Succeeded, Is.False);
            Assert.That(failed.Failure, Is.EqualTo(LocalizationFailure.PluralRulesUnknown));

            var otherOnly = MessageTemplate.Parse("{n, plural, other {# things}}");
            var text = otherOnly.Format(new MessageArguments().WithNumber("n", 3), unknown, null);
            Assert.That(text.Succeeded, Is.True, text.Message);
            Assert.That(text.Value, Is.EqualTo("3 things"));
        }

        [Test]
        public void CldrRulesDeclareSupportedLanguagesAndCategories()
        {
            var rules = CldrPluralRules.Default;
            Assert.That(rules.TryGetCategories(Ar, out var arabic), Is.True);
            Assert.That(arabic.Count, Is.EqualTo(6));
            Assert.That(rules.TryGetCategories(Ja, out var japanese), Is.True);
            Assert.That(japanese, Is.EqualTo(new[] { PluralCategory.Other }));
            Assert.That(rules.TryGetCategories(LocaleId.Parse("xx"), out _), Is.False);
            Assert.That(rules.TrySelect(LocaleId.Parse("xx"), 1, out _), Is.False);
            Assert.That(rules.SupportedLanguages, Does.Contain("ru").And.Contain("en").And.Contain("ja"));
            Assert.That(rules.TrySelect(LocaleId.Parse("pl"), 5, out var polish), Is.True);
            Assert.That(polish, Is.EqualTo(PluralCategory.Many));
            Assert.That(rules.TrySelect(LocaleId.Parse("cs"), 1.5m, out var czech), Is.True);
            Assert.That(czech, Is.EqualTo(PluralCategory.Many));
        }

        [Test]
        public void TableBuilderRejectsDuplicateAndMalformedMessages()
        {
            var builder = new MessageTableBuilder(En);
            Assert.That(builder.Add("greeting", "Hello {name}").Succeeded, Is.True);
            var duplicate = builder.Add("greeting", "Hi");
            Assert.That(duplicate.Failure, Is.EqualTo(LocalizationFailure.DuplicateMessage));
            var malformed = builder.Add("broken", "{n, plural, one {x}}");
            Assert.That(malformed.Failure, Is.EqualTo(LocalizationFailure.TemplateMalformed));
            var badId = builder.Add("Bad Id", "x");
            Assert.That(badId.Failure, Is.EqualTo(LocalizationFailure.InvalidMessageId));
            Assert.That(builder.Build().Count, Is.EqualTo(1));
        }

        [Test]
        public void TablesAreImmutableAfterBuild()
        {
            var builder = new MessageTableBuilder(En);
            builder.Add("a", "A");
            var table = builder.Build();
            builder.Add("b", "B");
            Assert.That(table.Count, Is.EqualTo(1));
            Assert.That(table.TryGet(MessageId.Parse("b"), out _), Is.False);
        }

        [Test]
        public void CatalogRequiresDefaultLocaleTableAndUniqueLocales()
        {
            var en = Table(En, ("a", "A"));
            var missingDefault = LocalizationCatalog.TryCreate(Fr, new[] { en });
            Assert.That(missingDefault.Succeeded, Is.False);
            Assert.That(missingDefault.Failure, Is.EqualTo(LocalizationFailure.UnsupportedLocale));

            var duplicate = LocalizationCatalog.TryCreate(En, new[] { en, Table(En, ("b", "B")) });
            Assert.That(duplicate.Failure, Is.EqualTo(LocalizationFailure.DuplicateMessage));
        }

        [Test]
        public void FallbackChainFollowsLookupThenDefaultThenRoot()
        {
            var catalog = LocalizationCatalog.TryCreate(EnUs, new[] { Table(EnUs, ("a", "A")) }).Value;
            Assert.That(Tags(catalog.FallbackChain(LocaleId.Parse("zh-Hant-TW"))), Is.EqualTo(new[] { "zh-Hant-TW", "zh-Hant", "zh", "en-US", "en", "root" }));
            Assert.That(Tags(catalog.FallbackChain(LocaleId.Parse("en-GB"))), Is.EqualTo(new[] { "en-GB", "en", "en-US", "root" }));
            Assert.That(Tags(catalog.FallbackChain(EnUs)), Is.EqualTo(new[] { "en-US", "en", "root" }));
        }

        [Test]
        public void ResolveReportsFallbackLocaleAndChain()
        {
            var catalog = LocalizationCatalog.TryCreate(En, new[]
            {
                Table(En, ("greeting", "Hello"), ("farewell", "Bye")),
                Table(LocaleId.Parse("de"), ("greeting", "Hallo")),
                Table(LocaleId.Root, ("brand", "XR Foundry")),
            }).Value;

            var direct = catalog.Resolve(MessageId.Parse("greeting"), LocaleId.Parse("de-AT"));
            Assert.That(direct.Succeeded, Is.True, direct.Message);
            Assert.That(direct.Value.Resolved.Tag, Is.EqualTo("de"));
            Assert.That(direct.Value.UsedFallback, Is.True);

            var viaDefault = catalog.Resolve(MessageId.Parse("farewell"), LocaleId.Parse("de-AT"));
            Assert.That(viaDefault.Value.Resolved, Is.EqualTo(En));
            Assert.That(Tags(viaDefault.Value.Chain), Is.EqualTo(new[] { "de-AT", "de", "en", "root" }));

            var viaRoot = catalog.Resolve(MessageId.Parse("brand"), LocaleId.Parse("ja"));
            Assert.That(viaRoot.Value.Resolved.IsRoot, Is.True);

            var exact = catalog.Resolve(MessageId.Parse("greeting"), En);
            Assert.That(exact.Value.UsedFallback, Is.False);
        }

        [Test]
        public void ResolveMissingEverywhereFailsWithChain()
        {
            var catalog = LocalizationCatalog.TryCreate(En, new[] { Table(En, ("a", "A")) }).Value;
            var missing = catalog.Resolve(MessageId.Parse("nope"), LocaleId.Parse("fr-CA"));
            Assert.That(missing.Succeeded, Is.False);
            Assert.That(missing.Failure, Is.EqualTo(LocalizationFailure.MessageMissing));
            Assert.That(missing.Message, Does.Contain("fr-CA").And.Contain("fr").And.Contain("en").And.Contain("root"));
        }

        [Test]
        public void CatalogFormatUsesPluralRulesOfTheResolvedLocale()
        {
            var catalog = LocalizationCatalog.TryCreate(En, new[]
            {
                Table(En, ("items", "{count, plural, one {# item} other {# items}}")),
                Table(Ru, ("items", "{count, plural, one {# предмет} few {# предмета} many {# предметов} other {# предмета}}")),
            }).Value;

            var russian = catalog.Format(MessageId.Parse("items"), LocaleId.Parse("ru-RU"), new MessageArguments().WithNumber("count", 3));
            Assert.That(russian.Succeeded, Is.True, russian.Message);
            Assert.That(russian.Value.Text, Is.EqualTo("3 предмета"));
            Assert.That(russian.Value.Resolved, Is.EqualTo(Ru));
            Assert.That(russian.Value.UsedFallback, Is.True);

            var english = catalog.Format(MessageId.Parse("items"), LocaleId.Parse("pt-BR"), new MessageArguments().WithNumber("count", 3));
            Assert.That(english.Value.Text, Is.EqualTo("3 items"));
            Assert.That(english.Value.Resolved, Is.EqualTo(En));

            var failure = catalog.Format(MessageId.Parse("items"), En, MessageArguments.None);
            Assert.That(failure.Failure, Is.EqualTo(LocalizationFailure.MissingArgument));
        }

        [Test]
        public void ValidatorReportsMissingExtraAndPlaceholderMismatch()
        {
            var catalog = LocalizationCatalog.TryCreate(En, new[]
            {
                Table(En, ("greeting", "Hello {name}"), ("farewell", "Bye {name}"), ("count", "{n, plural, one {#} other {#}}")),
                Table(Fr, ("greeting", "Bonjour {nom}"), ("count", "{n, plural, one {#} other {#}}"), ("extra", "Supplément")),
            }).Value;

            var report = LocalizationValidator.Validate(catalog, En);
            Assert.That(report.IsValid, Is.False);
            var codes = report.Diagnostics.Select(item => (item.Code, item.Locale.Tag, item.MessageId?.Value)).ToList();
            Assert.That(codes, Does.Contain((LocalizationValidator.MessageMissing, "fr", "farewell")));
            Assert.That(codes, Does.Contain((LocalizationValidator.PlaceholderMismatch, "fr", "greeting")));
            Assert.That(codes, Does.Contain((LocalizationValidator.MessageExtra, "fr", "extra")));
            Assert.That(report.Diagnostics.Single(item => item.Code == LocalizationValidator.MessageExtra).Severity, Is.EqualTo(DiagnosticSeverity.Warning));
        }

        [Test]
        public void ValidatorReportsPluralCategoryGapsPerLocale()
        {
            var catalog = LocalizationCatalog.TryCreate(En, new[]
            {
                Table(En, ("count", "{n, plural, one {# item} few {# items} other {# items}}")),
                Table(Ru, ("count", "{n, plural, one {# предмет} other {# предметов}}")),
            }).Value;

            var report = LocalizationValidator.Validate(catalog, En);
            var missing = report.Diagnostics.Where(item => item.Code == LocalizationValidator.PluralCategoryMissing && item.Locale == Ru).Select(item => item.Detail).ToList();
            Assert.That(missing.Count, Is.EqualTo(2), string.Join("\n", missing));
            Assert.That(missing.Any(detail => detail.Contains("'few'")), Is.True);
            Assert.That(missing.Any(detail => detail.Contains("'many'")), Is.True);
            var unused = report.Diagnostics.Single(item => item.Code == LocalizationValidator.PluralCategoryUnused);
            Assert.That(unused.Locale, Is.EqualTo(En));
            Assert.That(unused.Severity, Is.EqualTo(DiagnosticSeverity.Warning));
            Assert.That(report.IsValid, Is.False);
        }

        [Test]
        public void ValidatorReportsUnknownPluralRulesAsError()
        {
            var unknown = LocaleId.Parse("xx");
            var catalog = LocalizationCatalog.TryCreate(En, new[]
            {
                Table(En, ("count", "{n, plural, one {#} other {#}}")),
                Table(unknown, ("count", "{n, plural, one {#} other {#}}")),
            }).Value;
            var report = LocalizationValidator.Validate(catalog, En);
            var diagnostic = report.Diagnostics.Single(item => item.Code == LocalizationValidator.PluralRulesUnknown);
            Assert.That(diagnostic.Locale, Is.EqualTo(unknown));
            Assert.That(diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Error));
        }

        [Test]
        public void ValidatorPassesAConsistentCatalog()
        {
            var catalog = LocalizationCatalog.TryCreate(En, new[]
            {
                Table(En, ("greeting", "Hello {name}"), ("count", "{n, plural, one {# item} other {# items}}")),
                Table(Ru, ("greeting", "Привет, {name}"), ("count", "{n, plural, one {#} few {#} many {#} other {#}}")),
                Table(Ja, ("greeting", "{name}さん、こんにちは"), ("count", "{n, plural, other {#個}}")),
            }).Value;
            var report = LocalizationValidator.Validate(catalog, En);
            Assert.That(report.Diagnostics, Is.Empty, string.Join("\n", report.Diagnostics.Select(item => item.Code + " " + item.Detail)));
            Assert.That(report.IsValid, Is.True);
        }

        [Test]
        public void ValidatorFailsWhenSourceLocaleHasNoTable()
        {
            var catalog = LocalizationCatalog.TryCreate(En, new[] { Table(En, ("a", "A")) }).Value;
            var report = LocalizationValidator.Validate(catalog, Fr);
            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Diagnostics.Single().Code, Is.EqualTo(LocalizationValidator.MessageMissing));
        }

        private static string Format(MessageTemplate template, LocaleId locale, decimal count, string overrideSource = null)
        {
            var target = overrideSource == null ? template : MessageTemplate.Parse(overrideSource);
            var name = target.Arguments.Single(argument => argument.Kind == ArgumentKind.Number).Name;
            var result = target.Format(new MessageArguments().WithNumber(name, count), locale, CldrPluralRules.Default);
            Assert.That(result.Succeeded, Is.True, result.Message);
            return result.Value;
        }

        private static MessageTable Table(LocaleId locale, params (string Id, string Template)[] messages)
        {
            var builder = new MessageTableBuilder(locale);
            foreach (var message in messages)
            {
                var added = builder.Add(message.Id, message.Template);
                Assert.That(added.Succeeded, Is.True, added.Message);
            }
            return builder.Build();
        }

        private static string[] Tags(IReadOnlyList<LocaleId> chain) => chain.Select(locale => locale.Tag).ToArray();
    }
}
