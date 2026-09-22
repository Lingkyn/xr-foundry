using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Lingkyn.LiveTuning.Core
{
    // The two-way bridge between the design-language token document and a TunableRegistry /
    // TuningState, from any file, asset, or codec that stores either. The token document enters
    // and leaves this file as text; no token value is held here between calls.

    /// <summary>The closed annotation-key set the token bridge skips while walking a token
    /// document, exactly as the verification contract names it.</summary>
    public static class LiveTuningTokenAnnotations
    {
        public static readonly IReadOnlyCollection<string> Default = new[]
        {
            "role", "intent", "note", "technique", "rounded_via", "small_visual_rule", "slot", "bundled",
        };
    }

    /// <summary>An explicit range or value-set policy the token bridge consults for every numeric
    /// or string leaf, keyed by the leaf's derived tunable id text. The bridge holds no policy of
    /// its own; a caller (a test, or the consumer that owns the token document) builds one.</summary>
    public sealed class TokenRangePolicy
    {
        private readonly Dictionary<string, FloatDeclaration> _floatRanges = new Dictionary<string, FloatDeclaration>(StringComparer.Ordinal);
        private readonly Dictionary<string, IntegerDeclaration> _integerRanges = new Dictionary<string, IntegerDeclaration>(StringComparer.Ordinal);
        private readonly Dictionary<string, string[]> _enumeratedValues = new Dictionary<string, string[]>(StringComparer.Ordinal);

        public TokenRangePolicy WithFloatRange(string tunableId, float min, float max, float step)
        {
            _floatRanges[tunableId] = new FloatDeclaration(min, max, step);
            return this;
        }

        public TokenRangePolicy WithIntegerRange(string tunableId, int min, int max)
        {
            _integerRanges[tunableId] = new IntegerDeclaration(min, max);
            return this;
        }

        public TokenRangePolicy WithEnumeratedValues(string tunableId, params string[] values)
        {
            _enumeratedValues[tunableId] = values ?? Array.Empty<string>();
            return this;
        }

        internal bool TryGetFloatRange(string tunableId, out FloatDeclaration declaration) => _floatRanges.TryGetValue(tunableId, out declaration);
        internal bool TryGetIntegerRange(string tunableId, out IntegerDeclaration declaration) => _integerRanges.TryGetValue(tunableId, out declaration);
        internal bool TryGetEnumeratedValues(string tunableId, out string[] values) => _enumeratedValues.TryGetValue(tunableId, out values);
    }

    /// <summary>Turns a design-language token document into a <see cref="TunableRegistry"/> and,
    /// separately, a <see cref="TuningState"/>'s overrides into a token-override document.</summary>
    public static class TokenBridge
    {
        /// <summary>
        /// Walks the token document (its top-level object; a consumer passes the already-extracted
        /// "design_tokens" object, not the whole standard file with its schema and maturity keys)
        /// and registers exactly one tunable per token leaf: a four-element unit-float array
        /// becomes a colour, an integer-literal leaf an integer, a non-integer numeric leaf a
        /// float, a boolean leaf a bool, and a string leaf with a value set declared in
        /// <paramref name="rangePolicy"/> an enumerated. A key in <paramref name="annotationKeys"/>
        /// is skipped entirely. A leaf that is neither a tunable kind nor an annotation is
        /// rejected with token.unsupported; a numeric leaf absent from the policy with
        /// token.range.missing; two leaves that derive the same id with tunable.duplicate.
        /// </summary>
        public static LiveTuningResult<TunableRegistry> BuildRegistry(string tokenJson, IReadOnlyCollection<string> annotationKeys, TokenRangePolicy rangePolicy)
        {
            LiveTuningJsonValue root;
            try
            {
                root = LiveTuningJson.Parse(tokenJson ?? string.Empty);
            }
            catch (FormatException error)
            {
                return LiveTuningResult<TunableRegistry>.Fail(LiveTuningFailure.TokenUnsupported, $"The token document is not valid JSON: {error.Message}");
            }
            if (root.Kind != LiveTuningJsonKind.Object)
            {
                return LiveTuningResult<TunableRegistry>.Fail(LiveTuningFailure.TokenUnsupported, "The token document's root must be a JSON object.");
            }
            var annotations = new HashSet<string>(annotationKeys ?? LiveTuningTokenAnnotations.Default, StringComparer.Ordinal);
            var policy = rangePolicy ?? new TokenRangePolicy();
            var builder = new TunableRegistryBuilder();
            var failure = Walk(root, string.Empty, annotations, policy, builder);
            return failure.HasValue ? failure.Value.As<TunableRegistry>() : LiveTuningResult<TunableRegistry>.Ok(builder.Build());
        }

        private static LiveTuningResult<int>? Walk(LiveTuningJsonValue node, string path, HashSet<string> annotations, TokenRangePolicy policy, TunableRegistryBuilder builder)
        {
            switch (node.Kind)
            {
                case LiveTuningJsonKind.Object:
                {
                    if (node.TryGetProperty("rgba", out var rgba) && IsUnitColourArray(rgba))
                    {
                        return RegisterLeaf(path, ColourDeclaration.Instance, ColourFromArray(rgba), builder);
                    }
                    foreach (var property in node.Properties)
                    {
                        if (annotations.Contains(property.Key)) continue;
                        var childPath = path.Length == 0 ? property.Key : path + "." + property.Key;
                        var failure = Walk(property.Value, childPath, annotations, policy, builder);
                        if (failure.HasValue) return failure;
                    }
                    return null;
                }
                case LiveTuningJsonKind.Array:
                    if (IsUnitColourArray(node))
                    {
                        return RegisterLeaf(path, ColourDeclaration.Instance, ColourFromArray(node), builder);
                    }
                    return Unsupported(path, "a JSON array that is not a four-element unit-float colour");
                case LiveTuningJsonKind.Bool:
                    return RegisterLeaf(path, BoolDeclaration.Instance, TunableValue.OfBool(node.BoolValue), builder);
                case LiveTuningJsonKind.String:
                    if (policy.TryGetEnumeratedValues(path, out var values))
                    {
                        return RegisterLeaf(path, new EnumeratedDeclaration(values), TunableValue.OfEnumerated(node.StringValue), builder);
                    }
                    return Unsupported(path, "a string leaf with no declared value set");
                case LiveTuningJsonKind.Number:
                    if (node.IsIntegerLiteral)
                    {
                        if (!policy.TryGetIntegerRange(path, out var integerDeclaration))
                        {
                            return RangeMissing(path);
                        }
                        return RegisterLeaf(path, integerDeclaration, TunableValue.OfInteger((int)node.NumberValue), builder);
                    }
                    if (!policy.TryGetFloatRange(path, out var floatDeclaration))
                    {
                        return RangeMissing(path);
                    }
                    return RegisterLeaf(path, floatDeclaration, TunableValue.OfFloat((float)node.NumberValue), builder);
                case LiveTuningJsonKind.Null:
                    return Unsupported(path, "a JSON null");
                default:
                    return Unsupported(path, "an unrecognised JSON node");
            }
        }

        private static LiveTuningResult<int>? RegisterLeaf(string path, TunableKindDeclaration declaration, TunableValue value, TunableRegistryBuilder builder)
        {
            var idResult = TunableId.TryCreate(path);
            if (!idResult.Succeeded) return idResult.As<int>();
            var group = path.Contains(".") ? path.Substring(0, path.IndexOf('.')) : path;
            var label = path.Contains(".") ? path.Substring(path.LastIndexOf('.') + 1) : path;
            var registered = builder.Register(idResult.Value, declaration, value, group, label);
            return registered.Succeeded ? (LiveTuningResult<int>?)null : registered.As<int>();
        }

        private static LiveTuningResult<int> Unsupported(string path, string reason) =>
            LiveTuningResult<int>.Fail(LiveTuningFailure.TokenUnsupported, $"Token leaf '{path}' is unsupported ({reason}).");

        private static LiveTuningResult<int> RangeMissing(string path) =>
            LiveTuningResult<int>.Fail(LiveTuningFailure.TokenRangeMissing, $"Token leaf '{path}' is numeric but the range policy declares no range for it.");

        private static bool IsUnitColourArray(LiveTuningJsonValue node)
        {
            if (node.Kind != LiveTuningJsonKind.Array || node.Items.Count != 4) return false;
            foreach (var item in node.Items)
            {
                if (item.Kind != LiveTuningJsonKind.Number) return false;
                if (item.NumberValue < 0.0 || item.NumberValue > 1.0) return false;
            }
            return true;
        }

        private static TunableValue ColourFromArray(LiveTuningJsonValue array) =>
            TunableValue.OfColour((float)array.Items[0].NumberValue, (float)array.Items[1].NumberValue, (float)array.Items[2].NumberValue, (float)array.Items[3].NumberValue);
    }

    /// <summary>The per-tunable outcome of applying one entry of a <see cref="TokenOverrideDocument"/>.</summary>
    public sealed class TokenOverrideOutcome
    {
        public TokenOverrideOutcome(TunableId id, bool accepted, string code)
        {
            Id = id;
            Accepted = accepted;
            Code = code ?? string.Empty;
        }

        public TunableId Id { get; }
        public bool Accepted { get; }
        public string Code { get; }
    }

    /// <summary>The result of applying a whole document's overrides while skipping the ones that
    /// do not resolve, used by the Unity import clause.</summary>
    public sealed class TokenOverrideApplyReport
    {
        public TokenOverrideApplyReport(TuningState state, IReadOnlyList<TokenOverrideOutcome> outcomes)
        {
            State = state;
            Outcomes = outcomes;
        }

        public TuningState State { get; }
        public IReadOnlyList<TokenOverrideOutcome> Outcomes { get; }
    }

    /// <summary>The state's overrides bridged back to a token-override document: a schema id, the
    /// registry fingerprint, and the overrides in canonical id order with each value in its
    /// kind's canonical form. The same state always yields byte-equal <see cref="ToJson"/> output.</summary>
    public sealed class TokenOverrideDocument
    {
        public const string SchemaId = "xr-foundry.token_overrides.v1";

        private readonly List<(TunableId Id, string Value)> _overrides;

        private TokenOverrideDocument(string registryFingerprint, List<(TunableId Id, string Value)> overrides)
        {
            RegistryFingerprint = registryFingerprint;
            _overrides = overrides;
        }

        public string RegistryFingerprint { get; }
        public IReadOnlyList<(TunableId Id, string Value)> Overrides => _overrides;

        public static TokenOverrideDocument FromState(TuningState state)
        {
            var overrides = new List<(TunableId, string)>();
            foreach (var pair in state.Overrides)
            {
                overrides.Add((pair.Key, pair.Value.ToCanonicalString()));
            }
            return new TokenOverrideDocument(state.Registry.Fingerprint(), overrides);
        }

        public string ToJson()
        {
            var builder = new StringBuilder();
            builder.Append('{');
            builder.Append("\"schema\":").Append(LiveTuningJson.WriteString(SchemaId)).Append(',');
            builder.Append("\"registry_fingerprint\":").Append(LiveTuningJson.WriteString(RegistryFingerprint)).Append(',');
            builder.Append("\"overrides\":[");
            for (var index = 0; index < _overrides.Count; index++)
            {
                if (index > 0) builder.Append(',');
                var (id, value) = _overrides[index];
                builder.Append("{\"id\":").Append(LiveTuningJson.WriteString(id.Value)).Append(",\"value\":").Append(LiveTuningJson.WriteString(value)).Append('}');
            }
            builder.Append("]}");
            return builder.ToString();
        }

        public static LiveTuningResult<TokenOverrideDocument> Parse(string json)
        {
            LiveTuningJsonValue root;
            try
            {
                root = LiveTuningJson.Parse(json ?? string.Empty);
            }
            catch (FormatException error)
            {
                return LiveTuningResult<TokenOverrideDocument>.Fail(LiveTuningFailure.TokenUnsupported, $"The override document is not valid JSON: {error.Message}");
            }
            if (root.Kind != LiveTuningJsonKind.Object
                || !root.TryGetProperty("schema", out var schema) || schema.Kind != LiveTuningJsonKind.String || schema.StringValue != SchemaId
                || !root.TryGetProperty("registry_fingerprint", out var fingerprint) || fingerprint.Kind != LiveTuningJsonKind.String
                || !root.TryGetProperty("overrides", out var overridesNode) || overridesNode.Kind != LiveTuningJsonKind.Array)
            {
                return LiveTuningResult<TokenOverrideDocument>.Fail(LiveTuningFailure.TokenUnsupported, $"The override document does not match schema '{SchemaId}'.");
            }
            var overrides = new List<(TunableId, string)>();
            foreach (var entry in overridesNode.Items)
            {
                if (!entry.TryGetProperty("id", out var idNode) || idNode.Kind != LiveTuningJsonKind.String
                    || !entry.TryGetProperty("value", out var valueNode) || valueNode.Kind != LiveTuningJsonKind.String)
                {
                    return LiveTuningResult<TokenOverrideDocument>.Fail(LiveTuningFailure.TokenUnsupported, "An override entry must carry a string 'id' and a string 'value'.");
                }
                var idResult = TunableId.TryCreate(idNode.StringValue);
                if (!idResult.Succeeded) return idResult.As<TokenOverrideDocument>();
                overrides.Add((idResult.Value, valueNode.StringValue));
            }
            return LiveTuningResult<TokenOverrideDocument>.Ok(new TokenOverrideDocument(fingerprint.StringValue, overrides));
        }

        /// <summary>Applies every override to <paramref name="state"/> over its registry. Fails on
        /// the first entry that does not resolve; used by the Core round-trip guarantee, where
        /// every entry is expected to resolve because the document was built from the same
        /// registry.</summary>
        public LiveTuningResult<TuningState> ApplyTo(TuningState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var current = state;
            foreach (var (id, text) in _overrides)
            {
                var stepResult = ApplyOne(current, id, text);
                if (!stepResult.Succeeded) return stepResult;
                current = stepResult.Value;
            }
            return LiveTuningResult<TuningState>.Ok(current);
        }

        /// <summary>Applies every override to <paramref name="state"/>, skipping (rather than
        /// failing on) any entry that names an unknown id, a mismatched kind, or an out-of-range
        /// value; each skip is reported with the Core's code. Used for importing a device or
        /// Editor override file, which may be stale against the current registry.</summary>
        public TokenOverrideApplyReport ApplySkippingFailures(TuningState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var current = state;
            var outcomes = new List<TokenOverrideOutcome>();
            foreach (var (id, text) in _overrides)
            {
                var stepResult = ApplyOne(current, id, text);
                if (stepResult.Succeeded)
                {
                    current = stepResult.Value;
                    outcomes.Add(new TokenOverrideOutcome(id, true, string.Empty));
                }
                else
                {
                    outcomes.Add(new TokenOverrideOutcome(id, false, stepResult.Code));
                }
            }
            return new TokenOverrideApplyReport(current, outcomes);
        }

        private static LiveTuningResult<TuningState> ApplyOne(TuningState state, TunableId id, string text)
        {
            if (!state.Registry.TryGet(id, out var registration))
            {
                return LiveTuningResult<TuningState>.Fail(LiveTuningFailure.TunableUnknown, $"Override names tunable '{id}' which is not registered.");
            }
            if (!TunableValue.TryParseCanonical(registration.Kind, text, out var value))
            {
                return LiveTuningResult<TuningState>.Fail(LiveTuningFailure.TunableKindMismatch, $"Override value '{text}' for '{id}' does not parse as {registration.Kind}.");
            }
            return state.Apply(new SetIntent(id, value));
        }
    }
}
