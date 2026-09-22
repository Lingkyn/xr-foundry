using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Lingkyn.LiveTuning.Core
{
    // A minimal, dependency-free JSON reader and writer used only by the token bridge and the
    // token-override document. Not a general-purpose JSON library: it reads only what the
    // bridge needs (objects, arrays, strings, numbers, booleans, null) and writes only the
    // deterministic, sorted shapes this package produces. No UnityEngine or third-party JSON
    // package dependency.

    internal enum LiveTuningJsonKind
    {
        Object,
        Array,
        String,
        Number,
        Bool,
        Null,
    }

    /// <summary>One parsed JSON value. Object properties keep source order in
    /// <see cref="Properties"/> so a caller can look one up by name without assuming any order.</summary>
    internal sealed class LiveTuningJsonValue
    {
        public LiveTuningJsonKind Kind;
        public List<KeyValuePair<string, LiveTuningJsonValue>> Properties;
        public List<LiveTuningJsonValue> Items;
        public string StringValue;
        public double NumberValue;
        /// <summary>True when the source number literal had no '.', 'e', or 'E': the token bridge
        /// treats such a literal as an integer leaf and anything else as a float leaf.</summary>
        public bool IsIntegerLiteral;
        public bool BoolValue;

        public bool TryGetProperty(string name, out LiveTuningJsonValue value)
        {
            value = null;
            if (Kind != LiveTuningJsonKind.Object || Properties == null) return false;
            foreach (var property in Properties)
            {
                if (string.Equals(property.Key, name, StringComparison.Ordinal))
                {
                    value = property.Value;
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>Thrown only inside this file's parser; callers see it as a plain
    /// <see cref="FormatException"/> so the token bridge can turn it into token.unsupported.</summary>
    internal static class LiveTuningJson
    {
        public static LiveTuningJsonValue Parse(string text)
        {
            if (text == null) throw new FormatException("JSON text must not be null.");
            var position = 0;
            var value = ParseValue(text, ref position);
            SkipWhitespace(text, ref position);
            if (position != text.Length)
            {
                throw new FormatException($"Unexpected trailing content at position {position}.");
            }
            return value;
        }

        private static LiveTuningJsonValue ParseValue(string text, ref int position)
        {
            SkipWhitespace(text, ref position);
            if (position >= text.Length) throw new FormatException("Unexpected end of JSON text.");
            var current = text[position];
            switch (current)
            {
                case '{': return ParseObject(text, ref position);
                case '[': return ParseArray(text, ref position);
                case '"': return new LiveTuningJsonValue { Kind = LiveTuningJsonKind.String, StringValue = ParseString(text, ref position) };
                case 't':
                    Expect(text, ref position, "true");
                    return new LiveTuningJsonValue { Kind = LiveTuningJsonKind.Bool, BoolValue = true };
                case 'f':
                    Expect(text, ref position, "false");
                    return new LiveTuningJsonValue { Kind = LiveTuningJsonKind.Bool, BoolValue = false };
                case 'n':
                    Expect(text, ref position, "null");
                    return new LiveTuningJsonValue { Kind = LiveTuningJsonKind.Null };
                default:
                    return ParseNumber(text, ref position);
            }
        }

        private static LiveTuningJsonValue ParseObject(string text, ref int position)
        {
            position++; // consume '{'
            var properties = new List<KeyValuePair<string, LiveTuningJsonValue>>();
            SkipWhitespace(text, ref position);
            if (position < text.Length && text[position] == '}')
            {
                position++;
                return new LiveTuningJsonValue { Kind = LiveTuningJsonKind.Object, Properties = properties };
            }
            while (true)
            {
                SkipWhitespace(text, ref position);
                if (position >= text.Length || text[position] != '"') throw new FormatException($"Expected a property name at position {position}.");
                var key = ParseString(text, ref position);
                SkipWhitespace(text, ref position);
                if (position >= text.Length || text[position] != ':') throw new FormatException($"Expected ':' at position {position}.");
                position++;
                var value = ParseValue(text, ref position);
                properties.Add(new KeyValuePair<string, LiveTuningJsonValue>(key, value));
                SkipWhitespace(text, ref position);
                if (position >= text.Length) throw new FormatException("Unexpected end of JSON object.");
                if (text[position] == ',') { position++; continue; }
                if (text[position] == '}') { position++; break; }
                throw new FormatException($"Expected ',' or '}}' at position {position}.");
            }
            return new LiveTuningJsonValue { Kind = LiveTuningJsonKind.Object, Properties = properties };
        }

        private static LiveTuningJsonValue ParseArray(string text, ref int position)
        {
            position++; // consume '['
            var items = new List<LiveTuningJsonValue>();
            SkipWhitespace(text, ref position);
            if (position < text.Length && text[position] == ']')
            {
                position++;
                return new LiveTuningJsonValue { Kind = LiveTuningJsonKind.Array, Items = items };
            }
            while (true)
            {
                items.Add(ParseValue(text, ref position));
                SkipWhitespace(text, ref position);
                if (position >= text.Length) throw new FormatException("Unexpected end of JSON array.");
                if (text[position] == ',') { position++; continue; }
                if (text[position] == ']') { position++; break; }
                throw new FormatException($"Expected ',' or ']' at position {position}.");
            }
            return new LiveTuningJsonValue { Kind = LiveTuningJsonKind.Array, Items = items };
        }

        private static string ParseString(string text, ref int position)
        {
            position++; // consume opening '"'
            var builder = new StringBuilder();
            while (true)
            {
                if (position >= text.Length) throw new FormatException("Unterminated JSON string.");
                var current = text[position];
                if (current == '"') { position++; break; }
                if (current == '\\')
                {
                    position++;
                    if (position >= text.Length) throw new FormatException("Unterminated JSON escape.");
                    var escape = text[position];
                    switch (escape)
                    {
                        case '"': builder.Append('"'); break;
                        case '\\': builder.Append('\\'); break;
                        case '/': builder.Append('/'); break;
                        case 'b': builder.Append('\b'); break;
                        case 'f': builder.Append('\f'); break;
                        case 'n': builder.Append('\n'); break;
                        case 'r': builder.Append('\r'); break;
                        case 't': builder.Append('\t'); break;
                        case 'u':
                            if (position + 4 >= text.Length) throw new FormatException("Truncated unicode escape.");
                            var hex = text.Substring(position + 1, 4);
                            builder.Append((char)int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            position += 4;
                            break;
                        default:
                            throw new FormatException($"Unknown escape '\\{escape}'.");
                    }
                    position++;
                }
                else
                {
                    builder.Append(current);
                    position++;
                }
            }
            return builder.ToString();
        }

        private static LiveTuningJsonValue ParseNumber(string text, ref int position)
        {
            var start = position;
            var isIntegerLiteral = true;
            if (position < text.Length && text[position] == '-') position++;
            while (position < text.Length && char.IsDigit(text[position])) position++;
            if (position < text.Length && text[position] == '.')
            {
                isIntegerLiteral = false;
                position++;
                while (position < text.Length && char.IsDigit(text[position])) position++;
            }
            if (position < text.Length && (text[position] == 'e' || text[position] == 'E'))
            {
                isIntegerLiteral = false;
                position++;
                if (position < text.Length && (text[position] == '+' || text[position] == '-')) position++;
                while (position < text.Length && char.IsDigit(text[position])) position++;
            }
            if (position == start) throw new FormatException($"Expected a value at position {position}.");
            var literal = text.Substring(start, position - start);
            if (!double.TryParse(literal, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            {
                throw new FormatException($"'{literal}' is not a valid JSON number.");
            }
            return new LiveTuningJsonValue { Kind = LiveTuningJsonKind.Number, NumberValue = number, IsIntegerLiteral = isIntegerLiteral };
        }

        private static void Expect(string text, ref int position, string literal)
        {
            if (position + literal.Length > text.Length || text.Substring(position, literal.Length) != literal)
            {
                throw new FormatException($"Expected '{literal}' at position {position}.");
            }
            position += literal.Length;
        }

        private static void SkipWhitespace(string text, ref int position)
        {
            while (position < text.Length && char.IsWhiteSpace(text[position])) position++;
        }

        /// <summary>Writes a JSON string literal with the handful of escapes this package needs.</summary>
        public static string WriteString(string value)
        {
            var builder = new StringBuilder();
            builder.Append('"');
            foreach (var character in value ?? string.Empty)
            {
                switch (character)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (character < 0x20)
                        {
                            builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(character);
                        }
                        break;
                }
            }
            builder.Append('"');
            return builder.ToString();
        }
    }
}
