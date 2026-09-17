using System;
using System.Collections.Generic;
using Lingkyn.Localization.Core;
using UnityEngine;

namespace Lingkyn.Localization.Unity
{
    // Thin Unity adapter for the Localization Core: ScriptableObject authoring of
    // message tables and catalogs with fail-closed validation and stable codes, an
    // explicit SystemLanguage-to-locale map, and a plain runtime that owns the
    // current locale. No link to the Unity Localization package; the optional,
    // fail-closed bridge to its string tables lives in UnityLocalizationBridge.cs.

    [Serializable]
    public sealed class MessageEntry
    {
        [SerializeField] private string id = string.Empty;
        [SerializeField, TextArea(1, 6)] private string template = string.Empty;

        public string Id => id ?? string.Empty;
        public string Template => template ?? string.Empty;
    }

    [CreateAssetMenu(menuName = "Lingkyn/Localization/Message Table", fileName = "MessageTable")]
    public sealed class MessageTableAsset : ScriptableObject
    {
        [SerializeField] private string locale = "en";
        [SerializeField] private List<MessageEntry> entries = new List<MessageEntry>();

        public string LocaleTag => locale ?? string.Empty;
        public IReadOnlyList<MessageEntry> Entries => entries;

        /// <summary>Converts the asset without mutating it; throws with the validation report when invalid.</summary>
        public MessageTable ToDomain()
        {
            var report = LocalizationAuthoringValidation.Validate(this);
            if (!report.IsValid) throw new LocalizationAuthoringException(report);
            var builder = new MessageTableBuilder(LocaleId.Parse(LocaleTag));
            foreach (var entry in entries)
            {
                builder.Add(entry.Id, entry.Template);
            }
            return builder.Build();
        }
    }

    [CreateAssetMenu(menuName = "Lingkyn/Localization/Catalog", fileName = "LocalizationCatalog")]
    public sealed class LocalizationCatalogAsset : ScriptableObject
    {
        [SerializeField] private string defaultLocale = "en";
        [SerializeField] private List<MessageTableAsset> tables = new List<MessageTableAsset>();

        public string DefaultLocaleTag => defaultLocale ?? string.Empty;
        public IReadOnlyList<MessageTableAsset> Tables => tables;

        public LocalizationCatalog ToDomain(IPluralRules pluralRules = null)
        {
            var report = LocalizationAuthoringValidation.Validate(this);
            if (!report.IsValid) throw new LocalizationAuthoringException(report);
            var domainTables = new List<MessageTable>(tables.Count);
            foreach (var table in tables)
            {
                domainTables.Add(table.ToDomain());
            }
            var catalog = LocalizationCatalog.TryCreate(LocaleId.Parse(DefaultLocaleTag), domainTables, pluralRules);
            if (!catalog.Succeeded)
            {
                throw new LocalizationAuthoringException(new AuthoringReport(new[]
                {
                    new AuthoringDiagnostic("catalog.invalid", this, "tables", catalog.Message),
                }));
            }
            return catalog.Value;
        }
    }

    public sealed class AuthoringDiagnostic
    {
        public AuthoringDiagnostic(string code, UnityEngine.Object source, string fieldPath, string message)
        {
            Code = code;
            Source = source;
            FieldPath = fieldPath;
            Message = message;
        }

        /// <summary>Stable code: table.locale.invalid, table.entry.id.invalid, table.entry.duplicateId, table.entry.template.malformed, catalog.defaultLocale.invalid, catalog.defaultLocale.noTable, catalog.table.missing, catalog.table.duplicateLocale, catalog.invalid.</summary>
        public string Code { get; }
        public UnityEngine.Object Source { get; }
        public string FieldPath { get; }
        public string Message { get; }
    }

    public sealed class AuthoringReport
    {
        public AuthoringReport(IReadOnlyList<AuthoringDiagnostic> diagnostics)
        {
            Diagnostics = diagnostics;
        }

        public IReadOnlyList<AuthoringDiagnostic> Diagnostics { get; }
        public bool IsValid => Diagnostics.Count == 0;
    }

    public sealed class LocalizationAuthoringException : Exception
    {
        public LocalizationAuthoringException(AuthoringReport report)
            : base(Describe(report))
        {
            Report = report;
        }

        public AuthoringReport Report { get; }

        private static string Describe(AuthoringReport report)
        {
            var lines = new List<string>(report.Diagnostics.Count + 1) { "Localization authoring assets are invalid:" };
            foreach (var diagnostic in report.Diagnostics)
            {
                lines.Add($"  [{diagnostic.Code}] {diagnostic.FieldPath}: {diagnostic.Message}");
            }
            return string.Join("\n", lines);
        }
    }

    public static class LocalizationAuthoringValidation
    {
        public static AuthoringReport Validate(MessageTableAsset table)
        {
            var diagnostics = new List<AuthoringDiagnostic>();
            ValidateTable(table, diagnostics);
            return new AuthoringReport(diagnostics);
        }

        public static AuthoringReport Validate(LocalizationCatalogAsset catalog)
        {
            var diagnostics = new List<AuthoringDiagnostic>();
            if (catalog == null)
            {
                diagnostics.Add(new AuthoringDiagnostic("catalog.invalid", null, string.Empty, "The catalog asset is null."));
                return new AuthoringReport(diagnostics);
            }
            var defaultLocale = LocaleId.TryParse(catalog.DefaultLocaleTag);
            if (!defaultLocale.Succeeded)
            {
                diagnostics.Add(new AuthoringDiagnostic("catalog.defaultLocale.invalid", catalog, "defaultLocale", defaultLocale.Message));
            }
            var seen = new Dictionary<string, int>(StringComparer.Ordinal);
            var hasDefault = false;
            for (var index = 0; index < catalog.Tables.Count; index++)
            {
                var table = catalog.Tables[index];
                var path = $"tables.Array.data[{index}]";
                if (table == null)
                {
                    diagnostics.Add(new AuthoringDiagnostic("catalog.table.missing", catalog, path, "A table reference is empty."));
                    continue;
                }
                ValidateTable(table, diagnostics);
                var locale = LocaleId.TryParse(table.LocaleTag);
                if (!locale.Succeeded) continue;
                if (seen.TryGetValue(locale.Value.Tag, out var first))
                {
                    diagnostics.Add(new AuthoringDiagnostic("catalog.table.duplicateLocale", table, "locale", $"Locale '{locale.Value.Tag}' is already provided by tables.Array.data[{first}]."));
                }
                else
                {
                    seen[locale.Value.Tag] = index;
                }
                if (defaultLocale.Succeeded && locale.Value == defaultLocale.Value) hasDefault = true;
            }
            if (defaultLocale.Succeeded && !hasDefault)
            {
                diagnostics.Add(new AuthoringDiagnostic("catalog.defaultLocale.noTable", catalog, "defaultLocale", $"No table provides the default locale '{defaultLocale.Value.Tag}'."));
            }
            return new AuthoringReport(diagnostics);
        }

        private static void ValidateTable(MessageTableAsset table, List<AuthoringDiagnostic> diagnostics)
        {
            if (table == null)
            {
                diagnostics.Add(new AuthoringDiagnostic("catalog.table.missing", null, string.Empty, "The table asset is null."));
                return;
            }
            var locale = LocaleId.TryParse(table.LocaleTag);
            if (!locale.Succeeded)
            {
                diagnostics.Add(new AuthoringDiagnostic("table.locale.invalid", table, "locale", locale.Message));
            }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < table.Entries.Count; index++)
            {
                var entry = table.Entries[index];
                var path = $"entries.Array.data[{index}]";
                if (entry == null)
                {
                    diagnostics.Add(new AuthoringDiagnostic("table.entry.id.invalid", table, path, "An entry is empty."));
                    continue;
                }
                var id = MessageId.TryCreate(entry.Id);
                if (!id.Succeeded)
                {
                    diagnostics.Add(new AuthoringDiagnostic("table.entry.id.invalid", table, path + ".id", id.Message));
                }
                else if (!ids.Add(id.Value.Value))
                {
                    diagnostics.Add(new AuthoringDiagnostic("table.entry.duplicateId", table, path + ".id", $"Message id '{id.Value}' is defined more than once."));
                }
                var template = MessageTemplate.TryParse(entry.Template);
                if (!template.Succeeded)
                {
                    diagnostics.Add(new AuthoringDiagnostic("table.entry.template.malformed", table, path + ".template", template.Message));
                }
            }
        }
    }

    /// <summary>
    /// Explicit map from Unity's SystemLanguage enum to locale identity. Every entry is
    /// listed; Unknown and unlisted values fail closed so a product decides its own default.
    /// </summary>
    public static class SystemLocaleMap
    {
        private static readonly Dictionary<SystemLanguage, string> Tags = new Dictionary<SystemLanguage, string>
        {
            { SystemLanguage.Afrikaans, "af" },
            { SystemLanguage.Arabic, "ar" },
            { SystemLanguage.Basque, "eu" },
            { SystemLanguage.Belarusian, "be" },
            { SystemLanguage.Bulgarian, "bg" },
            { SystemLanguage.Catalan, "ca" },
            { SystemLanguage.Chinese, "zh" },
            { SystemLanguage.ChineseSimplified, "zh-Hans" },
            { SystemLanguage.ChineseTraditional, "zh-Hant" },
            { SystemLanguage.Czech, "cs" },
            { SystemLanguage.Danish, "da" },
            { SystemLanguage.Dutch, "nl" },
            { SystemLanguage.English, "en" },
            { SystemLanguage.Estonian, "et" },
            { SystemLanguage.Faroese, "fo" },
            { SystemLanguage.Finnish, "fi" },
            { SystemLanguage.French, "fr" },
            { SystemLanguage.German, "de" },
            { SystemLanguage.Greek, "el" },
            { SystemLanguage.Hebrew, "he" },
            { SystemLanguage.Hungarian, "hu" },
            { SystemLanguage.Icelandic, "is" },
            { SystemLanguage.Indonesian, "id" },
            { SystemLanguage.Italian, "it" },
            { SystemLanguage.Japanese, "ja" },
            { SystemLanguage.Korean, "ko" },
            { SystemLanguage.Latvian, "lv" },
            { SystemLanguage.Lithuanian, "lt" },
            { SystemLanguage.Norwegian, "nb" },
            { SystemLanguage.Polish, "pl" },
            { SystemLanguage.Portuguese, "pt" },
            { SystemLanguage.Romanian, "ro" },
            { SystemLanguage.Russian, "ru" },
            { SystemLanguage.SerboCroatian, "sr" },
            { SystemLanguage.Slovak, "sk" },
            { SystemLanguage.Slovenian, "sl" },
            { SystemLanguage.Spanish, "es" },
            { SystemLanguage.Swedish, "sv" },
            { SystemLanguage.Thai, "th" },
            { SystemLanguage.Turkish, "tr" },
            { SystemLanguage.Ukrainian, "uk" },
            { SystemLanguage.Vietnamese, "vi" },
        };

        public static bool TryMap(SystemLanguage language, out LocaleId locale)
        {
            if (Tags.TryGetValue(language, out var tag))
            {
                locale = LocaleId.Parse(tag);
                return true;
            }
            locale = LocaleId.Root;
            return false;
        }
    }

    /// <summary>
    /// Owns the current locale for one catalog. Plain class so a consumer's composition
    /// root decides its lifetime; no scene lookup, no static instance.
    /// </summary>
    public sealed class LocalizationRuntime
    {
        public LocalizationRuntime(LocalizationCatalog catalog, LocaleId initialLocale)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            CurrentLocale = initialLocale;
        }

        public LocalizationCatalog Catalog { get; }
        public LocaleId CurrentLocale { get; private set; }

        /// <summary>Raised after the locale changed; not raised when the same locale is set again.</summary>
        public event Action<LocaleId> LocaleChanged;

        /// <summary>True when the catalog holds a table for exactly this locale (no fallback needed for messages it defines).</summary>
        public bool HasDirectTable(LocaleId locale) => Catalog.TryGetTable(locale, out _);

        public bool SetLocale(LocaleId locale)
        {
            if (locale == CurrentLocale) return false;
            CurrentLocale = locale;
            LocaleChanged?.Invoke(locale);
            return true;
        }

        public LocalizationResult<FormattedMessage> Format(MessageId id, MessageArguments arguments) =>
            Catalog.Format(id, CurrentLocale, arguments);

        public LocalizationResult<FormattedMessage> Format(string id, MessageArguments arguments)
        {
            var messageId = MessageId.TryCreate(id);
            if (!messageId.Succeeded)
            {
                return LocalizationResult<FormattedMessage>.Fail(messageId.Failure, messageId.Message);
            }
            return Format(messageId.Value, arguments);
        }
    }
}
