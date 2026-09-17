using System.Text;

namespace Lingkyn.Localization.Core.Samples
{
    /// <summary>
    /// Domain-only walkthrough of the Localization Core: three message tables, a catalog
    /// with a default locale, plural formatting, fallback to the default locale, and the
    /// content validator. No scene, asset, or UnityEngine API is involved.
    /// </summary>
    public static class MessageCatalogSample
    {
        public static string Run()
        {
            var report = new StringBuilder();

            // 1. Author one table per locale. Every plural carries the categories its
            //    language needs: en one/other, ru one/few/many/other, ja other only.
            var en = new MessageTableBuilder(LocaleId.Parse("en"));
            en.Add("greeting", "Hello, {name}");
            en.Add("items", "{n, plural, one {# item} other {# items}}");

            var ru = new MessageTableBuilder(LocaleId.Parse("ru"));
            ru.Add("greeting", "Здравствуйте, {name}");
            ru.Add("items", "{n, plural, one {# предмет} few {# предмета} many {# предметов} other {# предмета}}");

            var ja = new MessageTableBuilder(LocaleId.Parse("ja"));
            ja.Add("greeting", "こんにちは、{name}");
            ja.Add("items", "{n, plural, other {#個のアイテム}}");

            // 2. Build the catalog; the default locale must own a table.
            var catalog = LocalizationCatalog.TryCreate(LocaleId.Parse("en"), new[] { en.Build(), ru.Build(), ja.Build() });
            if (!catalog.Succeeded)
            {
                report.AppendLine("catalog: " + catalog.Failure + " " + catalog.Message);
                return report.ToString();
            }
            report.AppendLine("catalog: default en, locales " + string.Join(", ", catalog.Value.Locales));

            // 3. ru-RU has no table of its own; RFC 4647 lookup truncates to ru, whose
            //    rules pick the 'many' branch for 5.
            var items = catalog.Value.Format(MessageId.Parse("items"), LocaleId.Parse("ru-RU"), new MessageArguments().WithNumber("n", 5));
            AppendFormatted(report, "items in ru-RU", items);

            // 4. de has no table anywhere in its chain, so the default locale answers and
            //    the result says so instead of returning an empty string.
            var greeting = catalog.Value.Format(MessageId.Parse("greeting"), LocaleId.Parse("de"), new MessageArguments().WithText("name", "Ada"));
            AppendFormatted(report, "greeting in de", greeting);

            // 5. Validate every locale against the source locale.
            var validation = LocalizationValidator.Validate(catalog.Value, LocaleId.Parse("en"));
            report.AppendLine("validation: " + (validation.IsValid ? "valid" : "invalid") + ", " + validation.Diagnostics.Count + " diagnostic(s)");
            foreach (var diagnostic in validation.Diagnostics)
            {
                report.AppendLine("  [" + diagnostic.Code + "] " + diagnostic.Locale.Tag + " " + diagnostic.MessageId + ": " + diagnostic.Detail);
            }

            return report.ToString();
        }

        private static void AppendFormatted(StringBuilder report, string label, LocalizationResult<FormattedMessage> result)
        {
            if (!result.Succeeded)
            {
                report.AppendLine(label + ": " + result.Failure + " " + result.Message);
                return;
            }
            report.AppendLine(label + ": \"" + result.Value.Text + "\" resolved " + result.Value.Resolved.Tag
                              + (result.Value.UsedFallback ? " (fallback)" : " (direct)"));
        }
    }
}
