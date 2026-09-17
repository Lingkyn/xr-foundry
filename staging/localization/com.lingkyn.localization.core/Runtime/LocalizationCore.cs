using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Lingkyn.Localization.Core
{
    // Engine-light localization core: message identity, BCP 47 locale identity with
    // RFC 4647 lookup fallback, an ICU-MessageFormat subset (placeholders, plural,
    // select, apostrophe quoting), CLDR cardinal plural categories for a declared set
    // of languages, immutable message tables, a catalog resolver, and a content
    // validator with stable diagnostic codes. No UnityEngine dependency.

    public enum LocalizationFailure
    {
        None = 0,
        InvalidLocale,
        InvalidMessageId,
        TemplateMalformed,
        DuplicateMessage,
        MessageMissing,
        MissingArgument,
        ArgumentTypeMismatch,
        PluralRulesUnknown,
        UnsupportedLocale,
    }

    public readonly struct LocalizationResult<T>
    {
        private LocalizationResult(bool succeeded, T value, LocalizationFailure failure, string message)
        {
            Succeeded = succeeded;
            Value = value;
            Failure = failure;
            Message = message ?? string.Empty;
        }

        public bool Succeeded { get; }
        public T Value { get; }
        public LocalizationFailure Failure { get; }
        public string Message { get; }

        public static LocalizationResult<T> Ok(T value) =>
            new LocalizationResult<T>(true, value, LocalizationFailure.None, string.Empty);

        public static LocalizationResult<T> Fail(LocalizationFailure failure, string message) =>
            new LocalizationResult<T>(false, default, failure, message);
    }

    /// <summary>
    /// A BCP 47 language tag restricted to language[-script][-region]. Extra subtags
    /// are rejected so identity stays canonical and comparable. "root" is the neutral
    /// top of every fallback chain.
    /// </summary>
    public readonly struct LocaleId : IEquatable<LocaleId>
    {
        public const string RootTag = "root";

        private LocaleId(string language, string script, string region)
        {
            Language = language;
            Script = script ?? string.Empty;
            Region = region ?? string.Empty;
        }

        public string Language { get; }
        public string Script { get; }
        public string Region { get; }

        public bool IsRoot => Language == RootTag;

        public static LocaleId Root => new LocaleId(RootTag, string.Empty, string.Empty);

        public string Tag
        {
            get
            {
                if (IsRoot) return RootTag;
                var builder = new StringBuilder(Language);
                if (Script.Length > 0) builder.Append('-').Append(Script);
                if (Region.Length > 0) builder.Append('-').Append(Region);
                return builder.ToString();
            }
        }

        /// <summary>RFC 4647 lookup truncation: drop the last subtag; language alone falls to root.</summary>
        public LocaleId Parent
        {
            get
            {
                if (IsRoot) return Root;
                if (Region.Length > 0) return new LocaleId(Language, Script, string.Empty);
                if (Script.Length > 0) return new LocaleId(Language, string.Empty, string.Empty);
                return Root;
            }
        }

        public static LocalizationResult<LocaleId> TryParse(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return LocalizationResult<LocaleId>.Fail(LocalizationFailure.InvalidLocale, "A locale tag must not be empty.");
            }

            var trimmed = tag.Trim();
            if (string.Equals(trimmed, RootTag, StringComparison.OrdinalIgnoreCase))
            {
                return LocalizationResult<LocaleId>.Ok(Root);
            }

            var parts = trimmed.Split(new[] { '-', '_' }, StringSplitOptions.None);
            if (parts.Length < 1 || parts.Length > 3)
            {
                return LocalizationResult<LocaleId>.Fail(LocalizationFailure.InvalidLocale, $"Locale '{tag}' must be language[-script][-region].");
            }

            var language = parts[0];
            if (!IsAlpha(language, 2, 3))
            {
                return LocalizationResult<LocaleId>.Fail(LocalizationFailure.InvalidLocale, $"Locale '{tag}' has an invalid language subtag.");
            }
            language = language.ToLowerInvariant();

            var script = string.Empty;
            var region = string.Empty;
            for (var index = 1; index < parts.Length; index++)
            {
                var part = parts[index];
                if (script.Length == 0 && region.Length == 0 && IsAlpha(part, 4, 4))
                {
                    script = char.ToUpperInvariant(part[0]) + part.Substring(1).ToLowerInvariant();
                    continue;
                }
                if (region.Length == 0 && (IsAlpha(part, 2, 2) || IsDigits(part, 3)))
                {
                    region = part.ToUpperInvariant();
                    continue;
                }
                return LocalizationResult<LocaleId>.Fail(LocalizationFailure.InvalidLocale, $"Locale '{tag}' has an unsupported subtag '{part}'.");
            }

            return LocalizationResult<LocaleId>.Ok(new LocaleId(language, script, region));
        }

        public static LocaleId Parse(string tag)
        {
            var result = TryParse(tag);
            if (!result.Succeeded) throw new ArgumentException(result.Message, nameof(tag));
            return result.Value;
        }

        private static bool IsAlpha(string value, int minimum, int maximum)
        {
            if (value.Length < minimum || value.Length > maximum) return false;
            foreach (var character in value)
            {
                if (character < 'A' || (character > 'Z' && character < 'a') || character > 'z') return false;
            }
            return true;
        }

        private static bool IsDigits(string value, int length)
        {
            if (value.Length != length) return false;
            foreach (var character in value)
            {
                if (character < '0' || character > '9') return false;
            }
            return true;
        }

        public bool Equals(LocaleId other) =>
            string.Equals(Language, other.Language, StringComparison.Ordinal)
            && string.Equals(Script, other.Script, StringComparison.Ordinal)
            && string.Equals(Region, other.Region, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is LocaleId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Tag);
        public override string ToString() => Tag;
        public static bool operator ==(LocaleId left, LocaleId right) => left.Equals(right);
        public static bool operator !=(LocaleId left, LocaleId right) => !left.Equals(right);
    }

    /// <summary>Stable message identity: lowercase letters, digits, '.', '_', '-', at most 128 characters.</summary>
    public readonly struct MessageId : IEquatable<MessageId>, IComparable<MessageId>
    {
        public const int MaximumLength = 128;

        private MessageId(string value)
        {
            Value = value;
        }

        public string Value { get; }

        public static LocalizationResult<MessageId> TryCreate(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > MaximumLength)
            {
                return LocalizationResult<MessageId>.Fail(LocalizationFailure.InvalidMessageId, "A message id must be 1 to 128 characters.");
            }
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                var ok = (character >= 'a' && character <= 'z')
                         || (character >= '0' && character <= '9')
                         || character == '.' || character == '_' || character == '-';
                if (!ok || (index == 0 && (character == '.' || character == '-')))
                {
                    return LocalizationResult<MessageId>.Fail(LocalizationFailure.InvalidMessageId, $"Message id '{value}' contains an invalid character at position {index}.");
                }
            }
            return LocalizationResult<MessageId>.Ok(new MessageId(value));
        }

        public static MessageId Parse(string value)
        {
            var result = TryCreate(value);
            if (!result.Succeeded) throw new ArgumentException(result.Message, nameof(value));
            return result.Value;
        }

        public bool Equals(MessageId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is MessageId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public int CompareTo(MessageId other) => string.CompareOrdinal(Value, other.Value);
        public override string ToString() => Value ?? string.Empty;
    }

    public enum PluralCategory
    {
        Zero,
        One,
        Two,
        Few,
        Many,
        Other,
    }

    /// <summary>Cardinal plural rules keyed by language. Unknown languages are reported, never guessed.</summary>
    public interface IPluralRules
    {
        bool TryGetCategories(LocaleId locale, out IReadOnlyList<PluralCategory> categories);
        bool TrySelect(LocaleId locale, decimal value, out PluralCategory category);
    }

    /// <summary>
    /// CLDR cardinal rules for a declared set of languages, expressed with the CLDR
    /// operands: n (absolute value), i (integer digits), v (visible fraction digits).
    /// Languages outside the set are unsupported by design.
    /// </summary>
    public sealed class CldrPluralRules : IPluralRules
    {
        public static CldrPluralRules Default { get; } = new CldrPluralRules();

        private static readonly PluralCategory[] OtherOnly = { PluralCategory.Other };
        private static readonly PluralCategory[] OneOther = { PluralCategory.One, PluralCategory.Other };
        private static readonly PluralCategory[] OneTwoOther = { PluralCategory.One, PluralCategory.Two, PluralCategory.Other };
        private static readonly PluralCategory[] OneFewManyOther = { PluralCategory.One, PluralCategory.Few, PluralCategory.Many, PluralCategory.Other };
        private static readonly PluralCategory[] AllSix = { PluralCategory.Zero, PluralCategory.One, PluralCategory.Two, PluralCategory.Few, PluralCategory.Many, PluralCategory.Other };

        private static readonly HashSet<string> OtherOnlyLanguages = new HashSet<string>(StringComparer.Ordinal)
        {
            "ja", "zh", "ko", "vi", "th", "id", "ms",
        };

        private static readonly HashSet<string> IntegerOneLanguages = new HashSet<string>(StringComparer.Ordinal)
        {
            "en", "de", "nl", "sv", "da", "nb", "nn", "no", "fi", "et", "it", "es", "el", "hu", "tr", "ca", "bg",
        };

        private static readonly HashSet<string> ZeroOrOneLanguages = new HashSet<string>(StringComparer.Ordinal)
        {
            "fr", "pt",
        };

        private static readonly HashSet<string> EastSlavicLanguages = new HashSet<string>(StringComparer.Ordinal)
        {
            "ru", "uk", "be",
        };

        private static readonly HashSet<string> CzechSlovakLanguages = new HashSet<string>(StringComparer.Ordinal)
        {
            "cs", "sk",
        };

        public IEnumerable<string> SupportedLanguages
        {
            get
            {
                var all = new SortedSet<string>(StringComparer.Ordinal);
                all.UnionWith(OtherOnlyLanguages);
                all.UnionWith(IntegerOneLanguages);
                all.UnionWith(ZeroOrOneLanguages);
                all.UnionWith(EastSlavicLanguages);
                all.UnionWith(CzechSlovakLanguages);
                all.Add("pl");
                all.Add("ar");
                all.Add("he");
                return all;
            }
        }

        public bool TryGetCategories(LocaleId locale, out IReadOnlyList<PluralCategory> categories)
        {
            var language = locale.Language;
            if (OtherOnlyLanguages.Contains(language)) { categories = OtherOnly; return true; }
            if (IntegerOneLanguages.Contains(language) || ZeroOrOneLanguages.Contains(language)) { categories = OneOther; return true; }
            if (EastSlavicLanguages.Contains(language) || language == "pl" || CzechSlovakLanguages.Contains(language)) { categories = OneFewManyOther; return true; }
            if (language == "ar") { categories = AllSix; return true; }
            if (language == "he") { categories = OneTwoOther; return true; }
            categories = Array.Empty<PluralCategory>();
            return false;
        }

        public bool TrySelect(LocaleId locale, decimal value, out PluralCategory category)
        {
            var n = Math.Abs(value);
            var i = decimal.Truncate(n);
            var v = VisibleFractionDigits(n);
            var language = locale.Language;

            if (OtherOnlyLanguages.Contains(language))
            {
                category = PluralCategory.Other;
                return true;
            }
            if (IntegerOneLanguages.Contains(language))
            {
                category = i == 1 && v == 0 ? PluralCategory.One : PluralCategory.Other;
                return true;
            }
            if (ZeroOrOneLanguages.Contains(language))
            {
                category = (i == 0 || i == 1) ? PluralCategory.One : PluralCategory.Other;
                return true;
            }
            if (EastSlavicLanguages.Contains(language))
            {
                var mod10 = i % 10;
                var mod100 = i % 100;
                if (v != 0) category = PluralCategory.Other;
                else if (mod10 == 1 && mod100 != 11) category = PluralCategory.One;
                else if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) category = PluralCategory.Few;
                else category = PluralCategory.Many;
                return true;
            }
            if (language == "pl")
            {
                var mod10 = i % 10;
                var mod100 = i % 100;
                if (v != 0) category = PluralCategory.Other;
                else if (i == 1) category = PluralCategory.One;
                else if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) category = PluralCategory.Few;
                else category = PluralCategory.Many;
                return true;
            }
            if (CzechSlovakLanguages.Contains(language))
            {
                if (v != 0) category = PluralCategory.Many;
                else if (i == 1) category = PluralCategory.One;
                else if (i >= 2 && i <= 4) category = PluralCategory.Few;
                else category = PluralCategory.Other;
                return true;
            }
            if (language == "ar")
            {
                var mod100 = n % 100;
                if (n == 0) category = PluralCategory.Zero;
                else if (n == 1) category = PluralCategory.One;
                else if (n == 2) category = PluralCategory.Two;
                else if (v == 0 && mod100 >= 3 && mod100 <= 10) category = PluralCategory.Few;
                else if (v == 0 && mod100 >= 11 && mod100 <= 99) category = PluralCategory.Many;
                else category = PluralCategory.Other;
                return true;
            }
            if (language == "he")
            {
                if (i == 1 && v == 0) category = PluralCategory.One;
                else if (i == 2 && v == 0) category = PluralCategory.Two;
                else category = PluralCategory.Other;
                return true;
            }

            category = PluralCategory.Other;
            return false;
        }

        private static int VisibleFractionDigits(decimal value)
        {
            var bits = decimal.GetBits(value);
            return (bits[3] >> 16) & 0x7F;
        }
    }

    public enum ArgumentKind
    {
        Text,
        Number,
        Select,
    }

    public sealed class ArgumentSpec
    {
        public ArgumentSpec(string name, ArgumentKind kind)
        {
            Name = name;
            Kind = kind;
        }

        public string Name { get; }
        public ArgumentKind Kind { get; }
    }

    /// <summary>Named arguments for formatting. Numbers stay numeric so plural selection is exact.</summary>
    public sealed class MessageArguments
    {
        private readonly SortedDictionary<string, object> _values = new SortedDictionary<string, object>(StringComparer.Ordinal);

        public static MessageArguments None { get; } = new MessageArguments();

        public MessageArguments WithText(string name, string value)
        {
            _values[RequireName(name)] = value ?? string.Empty;
            return this;
        }

        public MessageArguments WithNumber(string name, decimal value)
        {
            _values[RequireName(name)] = value;
            return this;
        }

        public MessageArguments WithNumber(string name, long value) => WithNumber(name, (decimal)value);

        public bool TryGetText(string name, out string value)
        {
            if (_values.TryGetValue(name, out var stored) && stored is string text)
            {
                value = text;
                return true;
            }
            if (_values.TryGetValue(name, out stored) && stored is decimal number)
            {
                value = number.ToString(CultureInfo.InvariantCulture);
                return true;
            }
            value = null;
            return false;
        }

        public bool TryGetNumber(string name, out decimal value)
        {
            if (_values.TryGetValue(name, out var stored) && stored is decimal number)
            {
                value = number;
                return true;
            }
            value = 0;
            return false;
        }

        public bool Contains(string name) => _values.ContainsKey(name);

        private static string RequireName(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("An argument name must not be empty.", nameof(name));
            return name;
        }
    }

    /// <summary>
    /// Parsed ICU-MessageFormat subset: literal text, {name}, {name, plural, ...},
    /// {name, select, ...}, '#' inside plural branches, and apostrophe quoting of
    /// '{', '}', '#' and '' for a literal apostrophe. Every plural and select must
    /// carry an 'other' branch. Parsing is fail-closed with a stable code.
    /// </summary>
    public sealed class MessageTemplate
    {
        private readonly IReadOnlyList<Node> _nodes;
        private readonly IReadOnlyList<ArgumentSpec> _arguments;

        private MessageTemplate(string source, IReadOnlyList<Node> nodes, IReadOnlyList<ArgumentSpec> arguments)
        {
            Source = source;
            _nodes = nodes;
            _arguments = arguments;
        }

        public string Source { get; }
        public IReadOnlyList<ArgumentSpec> Arguments => _arguments;

        public static LocalizationResult<MessageTemplate> TryParse(string source)
        {
            if (source == null)
            {
                return LocalizationResult<MessageTemplate>.Fail(LocalizationFailure.TemplateMalformed, "A template must not be null.");
            }
            var parser = new Parser(source);
            var nodes = parser.ParseSequence(insidePlural: false, terminator: '\0', out var error);
            if (error != null)
            {
                return LocalizationResult<MessageTemplate>.Fail(LocalizationFailure.TemplateMalformed, error);
            }
            var arguments = new SortedDictionary<string, ArgumentKind>(StringComparer.Ordinal);
            var conflict = CollectArguments(nodes, arguments);
            if (conflict != null)
            {
                return LocalizationResult<MessageTemplate>.Fail(LocalizationFailure.TemplateMalformed, conflict);
            }
            var specs = new List<ArgumentSpec>(arguments.Count);
            foreach (var pair in arguments) specs.Add(new ArgumentSpec(pair.Key, pair.Value));
            return LocalizationResult<MessageTemplate>.Ok(new MessageTemplate(source, nodes, specs));
        }

        public static MessageTemplate Parse(string source)
        {
            var result = TryParse(source);
            if (!result.Succeeded) throw new ArgumentException(result.Message, nameof(source));
            return result.Value;
        }

        /// <summary>Plural categories used by every plural argument, keyed by argument name.</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<PluralCategory>> PluralCategoriesByArgument()
        {
            var map = new SortedDictionary<string, IReadOnlyList<PluralCategory>>(StringComparer.Ordinal);
            CollectPluralCategories(_nodes, map);
            return map;
        }

        public LocalizationResult<string> Format(MessageArguments arguments, LocaleId locale, IPluralRules pluralRules)
        {
            arguments = arguments ?? MessageArguments.None;
            pluralRules = pluralRules ?? CldrPluralRules.Default;
            var builder = new StringBuilder();
            var error = Render(_nodes, arguments, locale, pluralRules, null, builder);
            if (error.HasValue)
            {
                return LocalizationResult<string>.Fail(error.Value.Failure, error.Value.Message);
            }
            return LocalizationResult<string>.Ok(builder.ToString());
        }

        private static string CollectArguments(IReadOnlyList<Node> nodes, SortedDictionary<string, ArgumentKind> arguments)
        {
            foreach (var node in nodes)
            {
                switch (node)
                {
                    case PlaceholderNode placeholder:
                        if (!Register(arguments, placeholder.Name, ArgumentKind.Text, out var conflict)) return conflict;
                        break;
                    case PluralNode plural:
                        if (!Register(arguments, plural.Name, ArgumentKind.Number, out conflict)) return conflict;
                        foreach (var branch in plural.Branches.Values)
                        {
                            var nested = CollectArguments(branch, arguments);
                            if (nested != null) return nested;
                        }
                        break;
                    case SelectNode select:
                        if (!Register(arguments, select.Name, ArgumentKind.Select, out conflict)) return conflict;
                        foreach (var branch in select.Branches.Values)
                        {
                            var nested = CollectArguments(branch, arguments);
                            if (nested != null) return nested;
                        }
                        break;
                }
            }
            return null;
        }

        private static bool Register(SortedDictionary<string, ArgumentKind> arguments, string name, ArgumentKind kind, out string conflict)
        {
            conflict = null;
            if (arguments.TryGetValue(name, out var existing))
            {
                if (existing == kind) return true;
                if (existing == ArgumentKind.Text && kind == ArgumentKind.Number) { arguments[name] = ArgumentKind.Number; return true; }
                if (existing == ArgumentKind.Number && kind == ArgumentKind.Text) return true;
                conflict = $"Argument '{name}' is used both as {existing} and as {kind}.";
                return false;
            }
            arguments[name] = kind;
            return true;
        }

        private static void CollectPluralCategories(IReadOnlyList<Node> nodes, SortedDictionary<string, IReadOnlyList<PluralCategory>> map)
        {
            foreach (var node in nodes)
            {
                if (node is PluralNode plural)
                {
                    var categories = new List<PluralCategory>(plural.Branches.Keys);
                    map[plural.Name] = categories;
                    foreach (var branch in plural.Branches.Values) CollectPluralCategories(branch, map);
                }
                else if (node is SelectNode select)
                {
                    foreach (var branch in select.Branches.Values) CollectPluralCategories(branch, map);
                }
            }
        }

        private static (LocalizationFailure Failure, string Message)? Render(
            IReadOnlyList<Node> nodes,
            MessageArguments arguments,
            LocaleId locale,
            IPluralRules pluralRules,
            decimal? currentNumber,
            StringBuilder builder)
        {
            foreach (var node in nodes)
            {
                switch (node)
                {
                    case TextNode text:
                        builder.Append(text.Text);
                        break;
                    case NumberSignNode _:
                        builder.Append(currentNumber.HasValue ? currentNumber.Value.ToString(CultureInfo.InvariantCulture) : "#");
                        break;
                    case PlaceholderNode placeholder:
                        if (!arguments.TryGetText(placeholder.Name, out var value))
                        {
                            return (LocalizationFailure.MissingArgument, $"Argument '{placeholder.Name}' was not supplied.");
                        }
                        builder.Append(value);
                        break;
                    case PluralNode plural:
                    {
                        if (!arguments.Contains(plural.Name))
                        {
                            return (LocalizationFailure.MissingArgument, $"Argument '{plural.Name}' was not supplied.");
                        }
                        if (!arguments.TryGetNumber(plural.Name, out var number))
                        {
                            return (LocalizationFailure.ArgumentTypeMismatch, $"Argument '{plural.Name}' must be a number for plural selection.");
                        }
                        IReadOnlyList<Node> branch;
                        if (plural.ExactBranches.TryGetValue(number, out branch))
                        {
                            // exact match, e.g. =0
                        }
                        else
                        {
                            if (!pluralRules.TrySelect(locale, number, out var category))
                            {
                                if (plural.Branches.Count == 1 && plural.Branches.ContainsKey(PluralCategory.Other))
                                {
                                    category = PluralCategory.Other;
                                }
                                else
                                {
                                    return (LocalizationFailure.PluralRulesUnknown, $"No plural rules are registered for locale '{locale.Tag}'.");
                                }
                            }
                            if (!plural.Branches.TryGetValue(category, out branch))
                            {
                                branch = plural.Branches[PluralCategory.Other];
                            }
                        }
                        var nested = Render(branch, arguments, locale, pluralRules, number, builder);
                        if (nested.HasValue) return nested;
                        break;
                    }
                    case SelectNode select:
                    {
                        if (!arguments.TryGetText(select.Name, out var key))
                        {
                            return (LocalizationFailure.MissingArgument, $"Argument '{select.Name}' was not supplied.");
                        }
                        if (!select.Branches.TryGetValue(key, out var branch))
                        {
                            branch = select.Branches["other"];
                        }
                        var nested = Render(branch, arguments, locale, pluralRules, currentNumber, builder);
                        if (nested.HasValue) return nested;
                        break;
                    }
                }
            }
            return null;
        }

        private abstract class Node { }

        private sealed class TextNode : Node
        {
            public TextNode(string text) { Text = text; }
            public string Text { get; }
        }

        private sealed class NumberSignNode : Node { }

        private sealed class PlaceholderNode : Node
        {
            public PlaceholderNode(string name) { Name = name; }
            public string Name { get; }
        }

        private sealed class PluralNode : Node
        {
            public PluralNode(string name, SortedDictionary<PluralCategory, IReadOnlyList<Node>> branches, SortedDictionary<decimal, IReadOnlyList<Node>> exact)
            {
                Name = name;
                Branches = branches;
                ExactBranches = exact;
            }

            public string Name { get; }
            public SortedDictionary<PluralCategory, IReadOnlyList<Node>> Branches { get; }
            public SortedDictionary<decimal, IReadOnlyList<Node>> ExactBranches { get; }
        }

        private sealed class SelectNode : Node
        {
            public SelectNode(string name, SortedDictionary<string, IReadOnlyList<Node>> branches)
            {
                Name = name;
                Branches = branches;
            }

            public string Name { get; }
            public SortedDictionary<string, IReadOnlyList<Node>> Branches { get; }
        }

        private sealed class Parser
        {
            private readonly string _source;
            private int _position;

            public Parser(string source)
            {
                _source = source;
            }

            public IReadOnlyList<Node> ParseSequence(bool insidePlural, char terminator, out string error)
            {
                error = null;
                var nodes = new List<Node>();
                var text = new StringBuilder();
                while (_position < _source.Length)
                {
                    var character = _source[_position];
                    if (character == terminator)
                    {
                        break;
                    }
                    if (character == '\'')
                    {
                        if (_position + 1 < _source.Length && _source[_position + 1] == '\'')
                        {
                            text.Append('\'');
                            _position += 2;
                            continue;
                        }
                        if (_position + 1 < _source.Length && IsQuotable(_source[_position + 1]))
                        {
                            var close = _source.IndexOf('\'', _position + 1);
                            if (close < 0)
                            {
                                error = $"Unterminated quote at position {_position}.";
                                return nodes;
                            }
                            text.Append(_source, _position + 1, close - _position - 1);
                            _position = close + 1;
                            continue;
                        }
                        text.Append('\'');
                        _position++;
                        continue;
                    }
                    if (character == '#' && insidePlural)
                    {
                        FlushText(nodes, text);
                        nodes.Add(new NumberSignNode());
                        _position++;
                        continue;
                    }
                    if (character == '}')
                    {
                        error = $"Unexpected '}}' at position {_position}.";
                        return nodes;
                    }
                    if (character == '{')
                    {
                        FlushText(nodes, text);
                        var node = ParseArgument(insidePlural, out error);
                        if (error != null) return nodes;
                        nodes.Add(node);
                        continue;
                    }
                    text.Append(character);
                    _position++;
                }
                FlushText(nodes, text);
                return nodes;
            }

            private static bool IsQuotable(char character) => character == '{' || character == '}' || character == '#';

            private static void FlushText(List<Node> nodes, StringBuilder text)
            {
                if (text.Length == 0) return;
                nodes.Add(new TextNode(text.ToString()));
                text.Clear();
            }

            private Node ParseArgument(bool insidePlural, out string error)
            {
                error = null;
                var start = _position;
                _position++; // '{'
                var name = ReadIdentifier();
                if (name.Length == 0)
                {
                    error = $"Argument at position {start} has no name.";
                    return null;
                }
                SkipWhitespace();
                if (_position >= _source.Length)
                {
                    error = $"Unterminated argument '{name}' at position {start}.";
                    return null;
                }
                if (_source[_position] == '}')
                {
                    _position++;
                    return new PlaceholderNode(name);
                }
                if (_source[_position] != ',')
                {
                    error = $"Expected ',' or '}}' after argument '{name}' at position {_position}.";
                    return null;
                }
                _position++;
                SkipWhitespace();
                var type = ReadIdentifier();
                SkipWhitespace();
                if (_position >= _source.Length || _source[_position] != ',')
                {
                    error = $"Argument '{name}' must be '{{{name}, plural, ...}}' or '{{{name}, select, ...}}'.";
                    return null;
                }
                _position++;
                if (type == "plural")
                {
                    return ParsePlural(name, start, out error);
                }
                if (type == "select")
                {
                    return ParseSelect(name, start, out error);
                }
                error = $"Argument '{name}' uses unsupported type '{type}'.";
                return null;
            }

            private Node ParsePlural(string name, int start, out string error)
            {
                error = null;
                var branches = new SortedDictionary<PluralCategory, IReadOnlyList<Node>>();
                var exact = new SortedDictionary<decimal, IReadOnlyList<Node>>();
                while (true)
                {
                    SkipWhitespace();
                    if (_position >= _source.Length)
                    {
                        error = $"Unterminated plural argument '{name}' at position {start}.";
                        return null;
                    }
                    if (_source[_position] == '}')
                    {
                        _position++;
                        break;
                    }
                    string selector;
                    decimal? exactValue = null;
                    if (_source[_position] == '=')
                    {
                        _position++;
                        var number = ReadNumber();
                        if (!decimal.TryParse(number, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
                        {
                            error = $"Plural argument '{name}' has an invalid exact selector '={number}'.";
                            return null;
                        }
                        exactValue = parsed;
                        selector = "=" + number;
                    }
                    else
                    {
                        selector = ReadIdentifier();
                        if (selector.Length == 0)
                        {
                            error = $"Plural argument '{name}' has a malformed branch at position {_position}.";
                            return null;
                        }
                    }
                    var body = ParseBranchBody(insidePlural: true, out error);
                    if (error != null) return null;
                    if (exactValue.HasValue)
                    {
                        if (exact.ContainsKey(exactValue.Value))
                        {
                            error = $"Plural argument '{name}' repeats branch '{selector}'.";
                            return null;
                        }
                        exact[exactValue.Value] = body;
                        continue;
                    }
                    if (!TryParseCategory(selector, out var category))
                    {
                        error = $"Plural argument '{name}' has unknown category '{selector}'.";
                        return null;
                    }
                    if (branches.ContainsKey(category))
                    {
                        error = $"Plural argument '{name}' repeats branch '{selector}'.";
                        return null;
                    }
                    branches[category] = body;
                }
                if (!branches.ContainsKey(PluralCategory.Other))
                {
                    error = $"Plural argument '{name}' must define an 'other' branch.";
                    return null;
                }
                return new PluralNode(name, branches, exact);
            }

            private Node ParseSelect(string name, int start, out string error)
            {
                error = null;
                var branches = new SortedDictionary<string, IReadOnlyList<Node>>(StringComparer.Ordinal);
                while (true)
                {
                    SkipWhitespace();
                    if (_position >= _source.Length)
                    {
                        error = $"Unterminated select argument '{name}' at position {start}.";
                        return null;
                    }
                    if (_source[_position] == '}')
                    {
                        _position++;
                        break;
                    }
                    var selector = ReadIdentifier();
                    if (selector.Length == 0)
                    {
                        error = $"Select argument '{name}' has a malformed branch at position {_position}.";
                        return null;
                    }
                    var body = ParseBranchBody(insidePlural: false, out error);
                    if (error != null) return null;
                    if (branches.ContainsKey(selector))
                    {
                        error = $"Select argument '{name}' repeats branch '{selector}'.";
                        return null;
                    }
                    branches[selector] = body;
                }
                if (!branches.ContainsKey("other"))
                {
                    error = $"Select argument '{name}' must define an 'other' branch.";
                    return null;
                }
                return new SelectNode(name, branches);
            }

            private IReadOnlyList<Node> ParseBranchBody(bool insidePlural, out string error)
            {
                error = null;
                SkipWhitespace();
                if (_position >= _source.Length || _source[_position] != '{')
                {
                    error = $"Expected '{{' to open a branch at position {_position}.";
                    return null;
                }
                _position++;
                var body = ParseSequence(insidePlural, '}', out error);
                if (error != null) return null;
                if (_position >= _source.Length || _source[_position] != '}')
                {
                    error = "Unterminated branch body.";
                    return null;
                }
                _position++;
                return body;
            }

            private static bool TryParseCategory(string selector, out PluralCategory category)
            {
                switch (selector)
                {
                    case "zero": category = PluralCategory.Zero; return true;
                    case "one": category = PluralCategory.One; return true;
                    case "two": category = PluralCategory.Two; return true;
                    case "few": category = PluralCategory.Few; return true;
                    case "many": category = PluralCategory.Many; return true;
                    case "other": category = PluralCategory.Other; return true;
                    default: category = PluralCategory.Other; return false;
                }
            }

            private string ReadIdentifier()
            {
                SkipWhitespace();
                var start = _position;
                while (_position < _source.Length)
                {
                    var character = _source[_position];
                    var ok = (character >= 'a' && character <= 'z') || (character >= 'A' && character <= 'Z')
                             || (character >= '0' && character <= '9') || character == '_';
                    if (!ok) break;
                    _position++;
                }
                return _source.Substring(start, _position - start);
            }

            private string ReadNumber()
            {
                var start = _position;
                while (_position < _source.Length)
                {
                    var character = _source[_position];
                    if ((character >= '0' && character <= '9') || character == '.' || character == '-') _position++;
                    else break;
                }
                return _source.Substring(start, _position - start);
            }

            private void SkipWhitespace()
            {
                while (_position < _source.Length && char.IsWhiteSpace(_source[_position])) _position++;
            }
        }
    }

    /// <summary>Immutable messages for one locale.</summary>
    public sealed class MessageTable
    {
        private readonly SortedDictionary<MessageId, MessageTemplate> _messages;

        internal MessageTable(LocaleId locale, SortedDictionary<MessageId, MessageTemplate> messages)
        {
            Locale = locale;
            _messages = messages;
        }

        public LocaleId Locale { get; }
        public int Count => _messages.Count;
        public IEnumerable<MessageId> Ids => _messages.Keys;

        public bool TryGet(MessageId id, out MessageTemplate template) => _messages.TryGetValue(id, out template);
    }

    public sealed class MessageTableBuilder
    {
        private readonly SortedDictionary<MessageId, MessageTemplate> _messages = new SortedDictionary<MessageId, MessageTemplate>();

        public MessageTableBuilder(LocaleId locale)
        {
            Locale = locale;
        }

        public LocaleId Locale { get; }

        public LocalizationResult<MessageTableBuilder> Add(string id, string template)
        {
            var messageId = MessageId.TryCreate(id);
            if (!messageId.Succeeded)
            {
                return LocalizationResult<MessageTableBuilder>.Fail(messageId.Failure, messageId.Message);
            }
            return Add(messageId.Value, template);
        }

        public LocalizationResult<MessageTableBuilder> Add(MessageId id, string template)
        {
            if (_messages.ContainsKey(id))
            {
                return LocalizationResult<MessageTableBuilder>.Fail(LocalizationFailure.DuplicateMessage, $"Message '{id}' is already defined for locale '{Locale.Tag}'.");
            }
            var parsed = MessageTemplate.TryParse(template);
            if (!parsed.Succeeded)
            {
                return LocalizationResult<MessageTableBuilder>.Fail(parsed.Failure, $"Message '{id}' for locale '{Locale.Tag}': {parsed.Message}");
            }
            _messages[id] = parsed.Value;
            return LocalizationResult<MessageTableBuilder>.Ok(this);
        }

        public MessageTable Build() => new MessageTable(Locale, new SortedDictionary<MessageId, MessageTemplate>(_messages));
    }

    public sealed class ResolvedMessage
    {
        public ResolvedMessage(MessageId id, LocaleId requested, LocaleId resolved, MessageTemplate template, IReadOnlyList<LocaleId> chain)
        {
            Id = id;
            Requested = requested;
            Resolved = resolved;
            Template = template;
            Chain = chain;
        }

        public MessageId Id { get; }
        public LocaleId Requested { get; }
        public LocaleId Resolved { get; }
        public MessageTemplate Template { get; }
        public IReadOnlyList<LocaleId> Chain { get; }
        public bool UsedFallback => Requested != Resolved;
    }

    public sealed class FormattedMessage
    {
        public FormattedMessage(string text, LocaleId resolved, bool usedFallback)
        {
            Text = text;
            Resolved = resolved;
            UsedFallback = usedFallback;
        }

        public string Text { get; }
        public LocaleId Resolved { get; }
        public bool UsedFallback { get; }
    }

    /// <summary>
    /// A set of message tables with a declared default locale. Resolution follows the
    /// RFC 4647 lookup chain of the requested locale, then the default locale's chain,
    /// then root; the first table that holds the message wins and the chain is reported.
    /// </summary>
    public sealed class LocalizationCatalog
    {
        private readonly SortedDictionary<string, MessageTable> _tables;

        private LocalizationCatalog(LocaleId defaultLocale, SortedDictionary<string, MessageTable> tables, IPluralRules pluralRules)
        {
            DefaultLocale = defaultLocale;
            _tables = tables;
            PluralRules = pluralRules;
        }

        public LocaleId DefaultLocale { get; }
        public IPluralRules PluralRules { get; }
        public IEnumerable<LocaleId> Locales
        {
            get
            {
                foreach (var table in _tables.Values) yield return table.Locale;
            }
        }

        public static LocalizationResult<LocalizationCatalog> TryCreate(LocaleId defaultLocale, IEnumerable<MessageTable> tables, IPluralRules pluralRules = null)
        {
            if (tables == null) throw new ArgumentNullException(nameof(tables));
            var map = new SortedDictionary<string, MessageTable>(StringComparer.Ordinal);
            foreach (var table in tables)
            {
                if (table == null) continue;
                if (map.ContainsKey(table.Locale.Tag))
                {
                    return LocalizationResult<LocalizationCatalog>.Fail(LocalizationFailure.DuplicateMessage, $"Locale '{table.Locale.Tag}' has more than one table.");
                }
                map[table.Locale.Tag] = table;
            }
            if (!map.ContainsKey(defaultLocale.Tag))
            {
                return LocalizationResult<LocalizationCatalog>.Fail(LocalizationFailure.UnsupportedLocale, $"Default locale '{defaultLocale.Tag}' has no table.");
            }
            return LocalizationResult<LocalizationCatalog>.Ok(new LocalizationCatalog(defaultLocale, map, pluralRules ?? CldrPluralRules.Default));
        }

        public bool TryGetTable(LocaleId locale, out MessageTable table) => _tables.TryGetValue(locale.Tag, out table);

        /// <summary>The ordered, de-duplicated locales consulted for a request.</summary>
        public IReadOnlyList<LocaleId> FallbackChain(LocaleId requested)
        {
            var chain = new List<LocaleId>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            AppendChain(requested, chain, seen);
            AppendChain(DefaultLocale, chain, seen);
            if (seen.Add(LocaleId.RootTag)) chain.Add(LocaleId.Root);
            return chain;
        }

        private static void AppendChain(LocaleId start, List<LocaleId> chain, HashSet<string> seen)
        {
            var current = start;
            while (!current.IsRoot)
            {
                if (seen.Add(current.Tag)) chain.Add(current);
                current = current.Parent;
            }
        }

        public LocalizationResult<ResolvedMessage> Resolve(MessageId id, LocaleId requested)
        {
            var chain = FallbackChain(requested);
            foreach (var locale in chain)
            {
                if (_tables.TryGetValue(locale.Tag, out var table) && table.TryGet(id, out var template))
                {
                    return LocalizationResult<ResolvedMessage>.Ok(new ResolvedMessage(id, requested, locale, template, chain));
                }
            }
            return LocalizationResult<ResolvedMessage>.Fail(
                LocalizationFailure.MessageMissing,
                $"Message '{id}' is not defined in any locale of the chain [{string.Join(", ", ChainTags(chain))}].");
        }

        public LocalizationResult<FormattedMessage> Format(MessageId id, LocaleId requested, MessageArguments arguments)
        {
            var resolved = Resolve(id, requested);
            if (!resolved.Succeeded)
            {
                return LocalizationResult<FormattedMessage>.Fail(resolved.Failure, resolved.Message);
            }
            var text = resolved.Value.Template.Format(arguments, resolved.Value.Resolved, PluralRules);
            if (!text.Succeeded)
            {
                return LocalizationResult<FormattedMessage>.Fail(text.Failure, $"Message '{id}' ({resolved.Value.Resolved.Tag}): {text.Message}");
            }
            return LocalizationResult<FormattedMessage>.Ok(new FormattedMessage(text.Value, resolved.Value.Resolved, resolved.Value.UsedFallback));
        }

        private static IEnumerable<string> ChainTags(IReadOnlyList<LocaleId> chain)
        {
            foreach (var locale in chain) yield return locale.Tag;
        }
    }

    public enum DiagnosticSeverity
    {
        Warning,
        Error,
    }

    public sealed class LocalizationDiagnostic
    {
        public LocalizationDiagnostic(DiagnosticSeverity severity, string code, LocaleId locale, MessageId? messageId, string detail)
        {
            Severity = severity;
            Code = code;
            Locale = locale;
            MessageId = messageId;
            Detail = detail;
        }

        public DiagnosticSeverity Severity { get; }
        /// <summary>Stable machine code: message.missing, message.extra, placeholder.mismatch, plural.category.missing, plural.category.unused, plural.rules.unknown.</summary>
        public string Code { get; }
        public LocaleId Locale { get; }
        public MessageId? MessageId { get; }
        public string Detail { get; }
    }

    public sealed class LocalizationValidationReport
    {
        public LocalizationValidationReport(IReadOnlyList<LocalizationDiagnostic> diagnostics)
        {
            Diagnostics = diagnostics;
        }

        public IReadOnlyList<LocalizationDiagnostic> Diagnostics { get; }

        public bool IsValid
        {
            get
            {
                foreach (var diagnostic in Diagnostics)
                {
                    if (diagnostic.Severity == DiagnosticSeverity.Error) return false;
                }
                return true;
            }
        }
    }

    /// <summary>
    /// Content validation against a source locale: every locale must define every source
    /// message with the same argument names and kinds, plural branches must cover the
    /// categories the locale's rules produce, and unused categories are reported.
    /// </summary>
    public static class LocalizationValidator
    {
        public const string MessageMissing = "message.missing";
        public const string MessageExtra = "message.extra";
        public const string PlaceholderMismatch = "placeholder.mismatch";
        public const string PluralCategoryMissing = "plural.category.missing";
        public const string PluralCategoryUnused = "plural.category.unused";
        public const string PluralRulesUnknown = "plural.rules.unknown";

        public static LocalizationValidationReport Validate(LocalizationCatalog catalog, LocaleId sourceLocale)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var diagnostics = new List<LocalizationDiagnostic>();
            if (!catalog.TryGetTable(sourceLocale, out var source))
            {
                diagnostics.Add(new LocalizationDiagnostic(DiagnosticSeverity.Error, MessageMissing, sourceLocale, null, $"Source locale '{sourceLocale.Tag}' has no table."));
                return new LocalizationValidationReport(diagnostics);
            }

            foreach (var locale in catalog.Locales)
            {
                catalog.TryGetTable(locale, out var table);
                var isSource = locale == sourceLocale;
                var rulesKnown = catalog.PluralRules.TryGetCategories(locale, out var localeCategories);

                foreach (var id in source.Ids)
                {
                    if (!table.TryGet(id, out var template))
                    {
                        if (!isSource)
                        {
                            diagnostics.Add(new LocalizationDiagnostic(DiagnosticSeverity.Error, MessageMissing, locale, id, $"Locale '{locale.Tag}' does not define '{id}'."));
                        }
                        continue;
                    }
                    source.TryGet(id, out var sourceTemplate);
                    if (!isSource && !SameArguments(sourceTemplate, template, out var difference))
                    {
                        diagnostics.Add(new LocalizationDiagnostic(DiagnosticSeverity.Error, PlaceholderMismatch, locale, id, difference));
                    }
                    ValidatePlurals(template, locale, id, rulesKnown, localeCategories, diagnostics);
                }

                if (!isSource)
                {
                    foreach (var id in table.Ids)
                    {
                        if (!source.TryGet(id, out _))
                        {
                            diagnostics.Add(new LocalizationDiagnostic(DiagnosticSeverity.Warning, MessageExtra, locale, id, $"Locale '{locale.Tag}' defines '{id}' which the source locale lacks."));
                        }
                    }
                }
            }

            return new LocalizationValidationReport(diagnostics);
        }

        private static void ValidatePlurals(
            MessageTemplate template,
            LocaleId locale,
            MessageId id,
            bool rulesKnown,
            IReadOnlyList<PluralCategory> localeCategories,
            List<LocalizationDiagnostic> diagnostics)
        {
            var plurals = template.PluralCategoriesByArgument();
            if (plurals.Count == 0) return;
            if (!rulesKnown)
            {
                diagnostics.Add(new LocalizationDiagnostic(DiagnosticSeverity.Error, PluralRulesUnknown, locale, id, $"No plural rules are registered for '{locale.Tag}', so its plural branches cannot be checked."));
                return;
            }
            foreach (var pair in plurals)
            {
                var present = new HashSet<PluralCategory>(pair.Value);
                foreach (var required in localeCategories)
                {
                    if (!present.Contains(required))
                    {
                        diagnostics.Add(new LocalizationDiagnostic(DiagnosticSeverity.Error, PluralCategoryMissing, locale, id, $"Argument '{pair.Key}' lacks the '{required.ToString().ToLowerInvariant()}' branch that '{locale.Tag}' requires."));
                    }
                }
                var allowed = new HashSet<PluralCategory>(localeCategories);
                foreach (var category in pair.Value)
                {
                    if (!allowed.Contains(category))
                    {
                        diagnostics.Add(new LocalizationDiagnostic(DiagnosticSeverity.Warning, PluralCategoryUnused, locale, id, $"Argument '{pair.Key}' defines '{category.ToString().ToLowerInvariant()}' which '{locale.Tag}' never selects."));
                    }
                }
            }
        }

        private static bool SameArguments(MessageTemplate source, MessageTemplate target, out string difference)
        {
            difference = null;
            var sourceMap = new SortedDictionary<string, ArgumentKind>(StringComparer.Ordinal);
            foreach (var argument in source.Arguments) sourceMap[argument.Name] = argument.Kind;
            var targetMap = new SortedDictionary<string, ArgumentKind>(StringComparer.Ordinal);
            foreach (var argument in target.Arguments) targetMap[argument.Name] = argument.Kind;
            foreach (var pair in sourceMap)
            {
                if (!targetMap.TryGetValue(pair.Key, out var kind))
                {
                    difference = $"Argument '{pair.Key}' from the source template is missing.";
                    return false;
                }
                if (kind != pair.Value)
                {
                    difference = $"Argument '{pair.Key}' is {pair.Value} in the source template but {kind} here.";
                    return false;
                }
            }
            foreach (var pair in targetMap)
            {
                if (!sourceMap.ContainsKey(pair.Key))
                {
                    difference = $"Argument '{pair.Key}' does not exist in the source template.";
                    return false;
                }
            }
            return true;
        }
    }
}
