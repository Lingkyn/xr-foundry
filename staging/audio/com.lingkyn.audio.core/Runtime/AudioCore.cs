using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Lingkyn.Audio.Core
{
    // Engine-light audio core: stable event, bus, snapshot, anchor, and parameter
    // identity with one canonical form, an immutable declared mix graph, typed
    // parameter contracts with fail-closed rejection, spatial attachment intents that
    // reference an anchor id rather than an engine transform, deterministic immutable
    // audio state produced by intent sequences, and structured results with stable
    // failure codes. No UnityEngine dependency, no playback, no DSP.

    /// <summary>Stable machine codes carried by every failed <see cref="AudioResult{T}"/>.</summary>
    public static class AudioFailure
    {
        public const string None = "";
        public const string IdentityMalformed = "identity.malformed";
        public const string EventUnknown = "event.unknown";
        public const string EventActive = "event.active";
        public const string EventInactive = "event.inactive";
        public const string EventAttached = "event.attached";
        public const string EventUnattached = "event.unattached";
        public const string BusUnknown = "bus.unknown";
        public const string SnapshotUnknown = "snapshot.unknown";
        public const string SnapshotDurationInvalid = "snapshot.duration.invalid";
        public const string ParameterUnknown = "parameter.unknown";
        public const string ParameterKindMismatch = "parameter.kind.mismatch";
        public const string ParameterOutOfRange = "parameter.out_of_range";
        public const string ParameterContractInvalid = "parameter.contract.invalid";
        public const string AnchorUnknown = "anchor.unknown";
        public const string AnchorDuplicate = "anchor.duplicate";
        public const string GraphRootMissing = "graph.root.missing";
        public const string GraphRootDuplicate = "graph.root.duplicate";
        public const string GraphBusDuplicate = "graph.bus.duplicate";
        public const string GraphBusUnreachable = "graph.bus.unreachable";
        public const string GraphParameterDuplicate = "graph.parameter.duplicate";
        public const string GraphSnapshotDuplicate = "graph.snapshot.duplicate";
        public const string GraphEventDuplicate = "graph.event.duplicate";
    }

    /// <summary>Structured outcome: a value on success, a stable code and a human message on failure.</summary>
    public readonly struct AudioResult<T>
    {
        private AudioResult(bool succeeded, T value, string code, string message)
        {
            Succeeded = succeeded;
            Value = value;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public bool Succeeded { get; }
        public T Value { get; }
        /// <summary>Empty on success; otherwise one of the <see cref="AudioFailure"/> constants.</summary>
        public string Code { get; }
        public string Message { get; }

        public static AudioResult<T> Ok(T value) => new AudioResult<T>(true, value, AudioFailure.None, string.Empty);

        public static AudioResult<T> Fail(string code, string message)
        {
            if (string.IsNullOrEmpty(code)) throw new ArgumentException("A failure needs a stable code.", nameof(code));
            return new AudioResult<T>(false, default, code, message);
        }

        public AudioResult<TOther> As<TOther>()
        {
            if (Succeeded) throw new InvalidOperationException("Only a failed result can be re-typed.");
            return AudioResult<TOther>.Fail(Code, Message);
        }
    }

    /// <summary>
    /// One canonical form for every identity: ASCII letters folded to lower case, digits,
    /// '.', '_', '-', '/' as segment separators, 1 to 128 characters, no whitespace, no
    /// leading or trailing separator, no doubled separator.
    /// </summary>
    public static class AudioIdentity
    {
        public const int MaximumLength = 128;

        public static AudioResult<string> TryCanonicalize(string value, string kind)
        {
            if (value == null || value.Length == 0)
            {
                return AudioResult<string>.Fail(AudioFailure.IdentityMalformed, $"A {kind} id must not be empty.");
            }
            if (value.Length > MaximumLength)
            {
                return AudioResult<string>.Fail(AudioFailure.IdentityMalformed, $"A {kind} id must be at most {MaximumLength} characters.");
            }
            var canonical = new char[value.Length];
            var previousSeparator = false;
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (char.IsWhiteSpace(character))
                {
                    return AudioResult<string>.Fail(AudioFailure.IdentityMalformed, $"The {kind} id '{value}' contains whitespace at position {index}.");
                }
                if (character >= 'A' && character <= 'Z') character = (char)(character + ('a' - 'A'));
                var separator = character == '.' || character == '/';
                var ok = (character >= 'a' && character <= 'z')
                         || (character >= '0' && character <= '9')
                         || separator || character == '_' || character == '-';
                if (!ok)
                {
                    return AudioResult<string>.Fail(AudioFailure.IdentityMalformed, $"The {kind} id '{value}' contains an invalid character at position {index}.");
                }
                if ((separator || character == '-') && index == 0)
                {
                    return AudioResult<string>.Fail(AudioFailure.IdentityMalformed, $"The {kind} id '{value}' must not start with a separator.");
                }
                if (separator && index == value.Length - 1)
                {
                    return AudioResult<string>.Fail(AudioFailure.IdentityMalformed, $"The {kind} id '{value}' must not end with a separator.");
                }
                if (separator && previousSeparator)
                {
                    return AudioResult<string>.Fail(AudioFailure.IdentityMalformed, $"The {kind} id '{value}' repeats a separator at position {index}.");
                }
                previousSeparator = separator;
                canonical[index] = character;
            }
            return AudioResult<string>.Ok(new string(canonical));
        }
    }

    /// <summary>Stable identity of an audio event, separate from any clip, asset, or middleware event bound to it.</summary>
    public readonly struct AudioEventId : IEquatable<AudioEventId>, IComparable<AudioEventId>
    {
        private AudioEventId(string value) { Value = value; }

        public string Value { get; }

        public static AudioResult<AudioEventId> TryCreate(string value)
        {
            var canonical = AudioIdentity.TryCanonicalize(value, "event");
            return canonical.Succeeded ? AudioResult<AudioEventId>.Ok(new AudioEventId(canonical.Value)) : canonical.As<AudioEventId>();
        }

        public static AudioEventId Parse(string value)
        {
            var result = TryCreate(value);
            if (!result.Succeeded) throw new ArgumentException(result.Message, nameof(value));
            return result.Value;
        }

        public bool Equals(AudioEventId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is AudioEventId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public int CompareTo(AudioEventId other) => string.CompareOrdinal(Value, other.Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(AudioEventId left, AudioEventId right) => left.Equals(right);
        public static bool operator !=(AudioEventId left, AudioEventId right) => !left.Equals(right);
    }

    /// <summary>Stable identity of a bus in the declared mix graph, separate from any mixer group.</summary>
    public readonly struct BusId : IEquatable<BusId>, IComparable<BusId>
    {
        private BusId(string value) { Value = value; }

        public string Value { get; }

        public static AudioResult<BusId> TryCreate(string value)
        {
            var canonical = AudioIdentity.TryCanonicalize(value, "bus");
            return canonical.Succeeded ? AudioResult<BusId>.Ok(new BusId(canonical.Value)) : canonical.As<BusId>();
        }

        public static BusId Parse(string value)
        {
            var result = TryCreate(value);
            if (!result.Succeeded) throw new ArgumentException(result.Message, nameof(value));
            return result.Value;
        }

        public bool Equals(BusId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is BusId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public int CompareTo(BusId other) => string.CompareOrdinal(Value, other.Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(BusId left, BusId right) => left.Equals(right);
        public static bool operator !=(BusId left, BusId right) => !left.Equals(right);
    }

    /// <summary>Stable identity of a mix snapshot, separate from any mixer snapshot object.</summary>
    public readonly struct SnapshotId : IEquatable<SnapshotId>, IComparable<SnapshotId>
    {
        private SnapshotId(string value) { Value = value; }

        public string Value { get; }

        public static AudioResult<SnapshotId> TryCreate(string value)
        {
            var canonical = AudioIdentity.TryCanonicalize(value, "snapshot");
            return canonical.Succeeded ? AudioResult<SnapshotId>.Ok(new SnapshotId(canonical.Value)) : canonical.As<SnapshotId>();
        }

        public static SnapshotId Parse(string value)
        {
            var result = TryCreate(value);
            if (!result.Succeeded) throw new ArgumentException(result.Message, nameof(value));
            return result.Value;
        }

        public bool Equals(SnapshotId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SnapshotId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public int CompareTo(SnapshotId other) => string.CompareOrdinal(Value, other.Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(SnapshotId left, SnapshotId right) => left.Equals(right);
        public static bool operator !=(SnapshotId left, SnapshotId right) => !left.Equals(right);
    }

    /// <summary>Stable identity of a spatial anchor, separate from any transform, scene object, or tracked device.</summary>
    public readonly struct AnchorId : IEquatable<AnchorId>, IComparable<AnchorId>
    {
        private AnchorId(string value) { Value = value; }

        public string Value { get; }

        public static AudioResult<AnchorId> TryCreate(string value)
        {
            var canonical = AudioIdentity.TryCanonicalize(value, "anchor");
            return canonical.Succeeded ? AudioResult<AnchorId>.Ok(new AnchorId(canonical.Value)) : canonical.As<AnchorId>();
        }

        public static AnchorId Parse(string value)
        {
            var result = TryCreate(value);
            if (!result.Succeeded) throw new ArgumentException(result.Message, nameof(value));
            return result.Value;
        }

        public bool Equals(AnchorId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is AnchorId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public int CompareTo(AnchorId other) => string.CompareOrdinal(Value, other.Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(AnchorId left, AnchorId right) => left.Equals(right);
        public static bool operator !=(AnchorId left, AnchorId right) => !left.Equals(right);
    }

    /// <summary>Stable identity of a typed parameter declared on a bus.</summary>
    public readonly struct ParameterId : IEquatable<ParameterId>, IComparable<ParameterId>
    {
        private ParameterId(string value) { Value = value; }

        public string Value { get; }

        public static AudioResult<ParameterId> TryCreate(string value)
        {
            var canonical = AudioIdentity.TryCanonicalize(value, "parameter");
            return canonical.Succeeded ? AudioResult<ParameterId>.Ok(new ParameterId(canonical.Value)) : canonical.As<ParameterId>();
        }

        public static ParameterId Parse(string value)
        {
            var result = TryCreate(value);
            if (!result.Succeeded) throw new ArgumentException(result.Message, nameof(value));
            return result.Value;
        }

        public bool Equals(ParameterId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ParameterId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public int CompareTo(ParameterId other) => string.CompareOrdinal(Value, other.Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(ParameterId left, ParameterId right) => left.Equals(right);
        public static bool operator !=(ParameterId left, ParameterId right) => !left.Equals(right);
    }

    /// <summary>Closed set of parameter kinds; a product's tuning values never add a kind.</summary>
    public enum ParameterKind
    {
        Float,
        Bool,
        Enumerated,
    }

    /// <summary>A typed value carried by a set-parameter intent. Exactly one payload is meaningful per kind.</summary>
    public readonly struct ParameterValue : IEquatable<ParameterValue>
    {
        private ParameterValue(ParameterKind kind, float number, bool flag, string choice)
        {
            Kind = kind;
            Number = number;
            Flag = flag;
            Choice = choice ?? string.Empty;
        }

        public ParameterKind Kind { get; }
        public float Number { get; }
        public bool Flag { get; }
        public string Choice { get; }

        public static ParameterValue Float(float value) => new ParameterValue(ParameterKind.Float, value, false, string.Empty);
        public static ParameterValue Bool(bool value) => new ParameterValue(ParameterKind.Bool, 0f, value, string.Empty);
        public static ParameterValue Enumerated(string value) => new ParameterValue(ParameterKind.Enumerated, 0f, false, value);

        public bool Equals(ParameterValue other)
        {
            if (Kind != other.Kind) return false;
            switch (Kind)
            {
                case ParameterKind.Float: return Number.Equals(other.Number);
                case ParameterKind.Bool: return Flag == other.Flag;
                default: return string.Equals(Choice, other.Choice, StringComparison.Ordinal);
            }
        }

        public override bool Equals(object obj) => obj is ParameterValue other && Equals(other);

        public override int GetHashCode()
        {
            switch (Kind)
            {
                case ParameterKind.Float: return Number.GetHashCode();
                case ParameterKind.Bool: return Flag ? 1 : 0;
                default: return StringComparer.Ordinal.GetHashCode(Choice ?? string.Empty);
            }
        }

        public override string ToString()
        {
            switch (Kind)
            {
                case ParameterKind.Float: return Number.ToString("R", CultureInfo.InvariantCulture);
                case ParameterKind.Bool: return Flag ? "true" : "false";
                default: return Choice ?? string.Empty;
            }
        }

        public static bool operator ==(ParameterValue left, ParameterValue right) => left.Equals(right);
        public static bool operator !=(ParameterValue left, ParameterValue right) => !left.Equals(right);
    }

    /// <summary>
    /// The contract between game code and the mix for one parameter: a closed kind and,
    /// per kind, a declared inclusive float range or a declared value set. Validation
    /// fails closed with parameter.kind.mismatch or parameter.out_of_range.
    /// </summary>
    public sealed class ParameterContract
    {
        private readonly string[] _values;

        private ParameterContract(ParameterId id, ParameterKind kind, float minimum, float maximum, string[] values, ParameterValue defaultValue)
        {
            Id = id;
            Kind = kind;
            Minimum = minimum;
            Maximum = maximum;
            _values = values;
            DefaultValue = defaultValue;
        }

        public ParameterId Id { get; }
        public ParameterKind Kind { get; }
        /// <summary>Inclusive lower bound; meaningful for <see cref="ParameterKind.Float"/> only.</summary>
        public float Minimum { get; }
        /// <summary>Inclusive upper bound; meaningful for <see cref="ParameterKind.Float"/> only.</summary>
        public float Maximum { get; }
        /// <summary>Declared choices, in declaration order; meaningful for <see cref="ParameterKind.Enumerated"/> only.</summary>
        public IReadOnlyList<string> Values => _values;
        public ParameterValue DefaultValue { get; }

        public static AudioResult<ParameterContract> TryFloat(ParameterId id, float minimum, float maximum, float defaultValue)
        {
            if (float.IsNaN(minimum) || float.IsNaN(maximum) || float.IsInfinity(minimum) || float.IsInfinity(maximum))
            {
                return AudioResult<ParameterContract>.Fail(AudioFailure.ParameterContractInvalid, $"Parameter '{id}' must declare a finite range.");
            }
            if (minimum > maximum)
            {
                return AudioResult<ParameterContract>.Fail(AudioFailure.ParameterContractInvalid, $"Parameter '{id}' declares minimum {Text(minimum)} above maximum {Text(maximum)}.");
            }
            if (float.IsNaN(defaultValue) || defaultValue < minimum || defaultValue > maximum)
            {
                return AudioResult<ParameterContract>.Fail(AudioFailure.ParameterContractInvalid, $"Parameter '{id}' default {Text(defaultValue)} lies outside [{Text(minimum)}, {Text(maximum)}].");
            }
            return AudioResult<ParameterContract>.Ok(new ParameterContract(id, ParameterKind.Float, minimum, maximum, Array.Empty<string>(), ParameterValue.Float(defaultValue)));
        }

        public static ParameterContract Float(ParameterId id, float minimum, float maximum, float defaultValue) => Require(TryFloat(id, minimum, maximum, defaultValue));

        public static ParameterContract Bool(ParameterId id, bool defaultValue) =>
            new ParameterContract(id, ParameterKind.Bool, 0f, 0f, Array.Empty<string>(), ParameterValue.Bool(defaultValue));

        public static AudioResult<ParameterContract> TryEnumerated(ParameterId id, IEnumerable<string> values, string defaultValue)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            var list = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                if (string.IsNullOrEmpty(value))
                {
                    return AudioResult<ParameterContract>.Fail(AudioFailure.ParameterContractInvalid, $"Parameter '{id}' declares an empty value.");
                }
                if (!seen.Add(value))
                {
                    return AudioResult<ParameterContract>.Fail(AudioFailure.ParameterContractInvalid, $"Parameter '{id}' declares value '{value}' more than once.");
                }
                list.Add(value);
            }
            if (list.Count == 0)
            {
                return AudioResult<ParameterContract>.Fail(AudioFailure.ParameterContractInvalid, $"Parameter '{id}' must declare at least one value.");
            }
            if (defaultValue == null || !seen.Contains(defaultValue))
            {
                return AudioResult<ParameterContract>.Fail(AudioFailure.ParameterContractInvalid, $"Parameter '{id}' default '{defaultValue}' is not a declared value.");
            }
            return AudioResult<ParameterContract>.Ok(new ParameterContract(id, ParameterKind.Enumerated, 0f, 0f, list.ToArray(), ParameterValue.Enumerated(defaultValue)));
        }

        public static ParameterContract Enumerated(ParameterId id, IEnumerable<string> values, string defaultValue) => Require(TryEnumerated(id, values, defaultValue));

        /// <summary>Accepts the value unchanged or fails closed with a stable code.</summary>
        public AudioResult<ParameterValue> Validate(ParameterValue value)
        {
            if (value.Kind != Kind)
            {
                return AudioResult<ParameterValue>.Fail(AudioFailure.ParameterKindMismatch, $"Parameter '{Id}' is {Kind} but the value is {value.Kind}.");
            }
            switch (Kind)
            {
                case ParameterKind.Float:
                    if (float.IsNaN(value.Number) || value.Number < Minimum || value.Number > Maximum)
                    {
                        return AudioResult<ParameterValue>.Fail(AudioFailure.ParameterOutOfRange, $"Parameter '{Id}' value {Text(value.Number)} lies outside [{Text(Minimum)}, {Text(Maximum)}].");
                    }
                    return AudioResult<ParameterValue>.Ok(value);
                case ParameterKind.Bool:
                    return AudioResult<ParameterValue>.Ok(value);
                default:
                    if (Array.IndexOf(_values, value.Choice) < 0)
                    {
                        return AudioResult<ParameterValue>.Fail(AudioFailure.ParameterOutOfRange, $"Parameter '{Id}' value '{value.Choice}' is not one of [{string.Join(", ", _values)}].");
                    }
                    return AudioResult<ParameterValue>.Ok(value);
            }
        }

        private static ParameterContract Require(AudioResult<ParameterContract> result)
        {
            if (!result.Succeeded) throw new ArgumentException(result.Message);
            return result.Value;
        }

        private static string Text(float value) => value.ToString("R", CultureInfo.InvariantCulture);
    }

    /// <summary>A bus in the declared graph: the root has no parent; every other bus names a parent in the graph.</summary>
    public sealed class BusDefinition
    {
        private readonly ParameterContract[] _parameters;

        internal BusDefinition(BusId id, bool isRoot, BusId parent, ParameterContract[] parameters)
        {
            Id = id;
            IsRoot = isRoot;
            Parent = parent;
            _parameters = parameters;
        }

        public BusId Id { get; }
        public bool IsRoot { get; }
        /// <summary>Meaningful only when <see cref="IsRoot"/> is false.</summary>
        public BusId Parent { get; }
        public IReadOnlyList<ParameterContract> Parameters => _parameters;

        public bool TryGetParameter(ParameterId id, out ParameterContract contract)
        {
            foreach (var parameter in _parameters)
            {
                if (parameter.Id == id)
                {
                    contract = parameter;
                    return true;
                }
            }
            contract = null;
            return false;
        }
    }

    /// <summary>A named mix state that captures values for a set of buses in the graph.</summary>
    public sealed class SnapshotDefinition
    {
        private readonly BusId[] _buses;

        internal SnapshotDefinition(SnapshotId id, BusId[] buses)
        {
            Id = id;
            _buses = buses;
        }

        public SnapshotId Id { get; }
        public IReadOnlyList<BusId> Buses => _buses;
    }

    /// <summary>An event the graph knows about and the bus it routes to.</summary>
    public sealed class AudioEventDefinition
    {
        internal AudioEventDefinition(AudioEventId id, BusId bus)
        {
            Id = id;
            Bus = bus;
        }

        public AudioEventId Id { get; }
        public BusId Bus { get; }
    }

    /// <summary>
    /// The immutable declared mix graph: exactly one root bus, unique bus, snapshot, and
    /// event ids, every non-root bus reachable from the root through its parents, and
    /// every snapshot and event referencing only buses in the graph.
    /// </summary>
    public sealed class MixGraph
    {
        private readonly SortedDictionary<BusId, BusDefinition> _buses;
        private readonly SortedDictionary<SnapshotId, SnapshotDefinition> _snapshots;
        private readonly SortedDictionary<AudioEventId, AudioEventDefinition> _events;

        private MixGraph(
            BusDefinition root,
            SortedDictionary<BusId, BusDefinition> buses,
            SortedDictionary<SnapshotId, SnapshotDefinition> snapshots,
            SortedDictionary<AudioEventId, AudioEventDefinition> events)
        {
            Root = root;
            _buses = buses;
            _snapshots = snapshots;
            _events = events;
        }

        public BusDefinition Root { get; }
        public IEnumerable<BusDefinition> Buses => _buses.Values;
        public IEnumerable<SnapshotDefinition> Snapshots => _snapshots.Values;
        public IEnumerable<AudioEventDefinition> Events => _events.Values;
        public int BusCount => _buses.Count;
        public int SnapshotCount => _snapshots.Count;
        public int EventCount => _events.Count;

        public bool TryGetBus(BusId id, out BusDefinition bus) => _buses.TryGetValue(id, out bus);
        public bool TryGetSnapshot(SnapshotId id, out SnapshotDefinition snapshot) => _snapshots.TryGetValue(id, out snapshot);
        public bool TryGetEvent(AudioEventId id, out AudioEventDefinition definition) => _events.TryGetValue(id, out definition);

        /// <summary>Resolves a bus parameter with bus.unknown or parameter.unknown on failure.</summary>
        public AudioResult<ParameterContract> ResolveParameter(BusId bus, ParameterId parameter)
        {
            if (!_buses.TryGetValue(bus, out var definition))
            {
                return AudioResult<ParameterContract>.Fail(AudioFailure.BusUnknown, $"Bus '{bus}' is not in the mix graph.");
            }
            if (!definition.TryGetParameter(parameter, out var contract))
            {
                return AudioResult<ParameterContract>.Fail(AudioFailure.ParameterUnknown, $"Bus '{bus}' declares no parameter '{parameter}'.");
            }
            return AudioResult<ParameterContract>.Ok(contract);
        }

        internal static AudioResult<MixGraph> Build(
            List<BusDefinition> buses,
            List<SnapshotDefinition> snapshots,
            List<AudioEventDefinition> events)
        {
            var busMap = new SortedDictionary<BusId, BusDefinition>();
            BusDefinition root = null;
            foreach (var bus in buses)
            {
                if (busMap.ContainsKey(bus.Id))
                {
                    return AudioResult<MixGraph>.Fail(AudioFailure.GraphBusDuplicate, $"Bus '{bus.Id}' is declared more than once.");
                }
                var parameterIds = new HashSet<ParameterId>();
                foreach (var parameter in bus.Parameters)
                {
                    if (!parameterIds.Add(parameter.Id))
                    {
                        return AudioResult<MixGraph>.Fail(AudioFailure.GraphParameterDuplicate, $"Bus '{bus.Id}' declares parameter '{parameter.Id}' more than once.");
                    }
                }
                if (bus.IsRoot)
                {
                    if (root != null)
                    {
                        return AudioResult<MixGraph>.Fail(AudioFailure.GraphRootDuplicate, $"Bus '{bus.Id}' is a second root; '{root.Id}' is already the root.");
                    }
                    root = bus;
                }
                busMap[bus.Id] = bus;
            }
            if (root == null)
            {
                return AudioResult<MixGraph>.Fail(AudioFailure.GraphRootMissing, "The mix graph must declare exactly one root bus.");
            }
            foreach (var bus in busMap.Values)
            {
                if (bus.IsRoot) continue;
                if (!busMap.ContainsKey(bus.Parent))
                {
                    return AudioResult<MixGraph>.Fail(AudioFailure.BusUnknown, $"Bus '{bus.Id}' names parent '{bus.Parent}' which is not in the mix graph.");
                }
                var current = bus;
                var steps = 0;
                while (!current.IsRoot)
                {
                    current = busMap[current.Parent];
                    steps++;
                    if (steps > busMap.Count)
                    {
                        return AudioResult<MixGraph>.Fail(AudioFailure.GraphBusUnreachable, $"Bus '{bus.Id}' never reaches the root through its parents.");
                    }
                }
            }

            var snapshotMap = new SortedDictionary<SnapshotId, SnapshotDefinition>();
            foreach (var snapshot in snapshots)
            {
                if (snapshotMap.ContainsKey(snapshot.Id))
                {
                    return AudioResult<MixGraph>.Fail(AudioFailure.GraphSnapshotDuplicate, $"Snapshot '{snapshot.Id}' is declared more than once.");
                }
                foreach (var bus in snapshot.Buses)
                {
                    if (!busMap.ContainsKey(bus))
                    {
                        return AudioResult<MixGraph>.Fail(AudioFailure.BusUnknown, $"Snapshot '{snapshot.Id}' references bus '{bus}' which is not in the mix graph.");
                    }
                }
                snapshotMap[snapshot.Id] = snapshot;
            }

            var eventMap = new SortedDictionary<AudioEventId, AudioEventDefinition>();
            foreach (var definition in events)
            {
                if (eventMap.ContainsKey(definition.Id))
                {
                    return AudioResult<MixGraph>.Fail(AudioFailure.GraphEventDuplicate, $"Event '{definition.Id}' is declared more than once.");
                }
                if (!busMap.ContainsKey(definition.Bus))
                {
                    return AudioResult<MixGraph>.Fail(AudioFailure.BusUnknown, $"Event '{definition.Id}' routes to bus '{definition.Bus}' which is not in the mix graph.");
                }
                eventMap[definition.Id] = definition;
            }

            return AudioResult<MixGraph>.Ok(new MixGraph(root, busMap, snapshotMap, eventMap));
        }
    }

    /// <summary>Collects declarations; <see cref="Build"/> validates them and produces an immutable graph.</summary>
    public sealed class MixGraphBuilder
    {
        private readonly List<BusDefinition> _buses = new List<BusDefinition>();
        private readonly List<SnapshotDefinition> _snapshots = new List<SnapshotDefinition>();
        private readonly List<AudioEventDefinition> _events = new List<AudioEventDefinition>();

        public MixGraphBuilder RootBus(BusId id, params ParameterContract[] parameters)
        {
            _buses.Add(new BusDefinition(id, true, default, Copy(parameters)));
            return this;
        }

        public MixGraphBuilder Bus(BusId id, BusId parent, params ParameterContract[] parameters)
        {
            _buses.Add(new BusDefinition(id, false, parent, Copy(parameters)));
            return this;
        }

        public MixGraphBuilder Snapshot(SnapshotId id, params BusId[] buses)
        {
            _snapshots.Add(new SnapshotDefinition(id, buses == null ? Array.Empty<BusId>() : (BusId[])buses.Clone()));
            return this;
        }

        public MixGraphBuilder Event(AudioEventId id, BusId bus)
        {
            _events.Add(new AudioEventDefinition(id, bus));
            return this;
        }

        public AudioResult<MixGraph> Build() =>
            MixGraph.Build(new List<BusDefinition>(_buses), new List<SnapshotDefinition>(_snapshots), new List<AudioEventDefinition>(_events));

        private static ParameterContract[] Copy(ParameterContract[] parameters)
        {
            if (parameters == null) return Array.Empty<ParameterContract>();
            foreach (var parameter in parameters)
            {
                if (parameter == null) throw new ArgumentException("A bus parameter contract must not be null.", nameof(parameters));
            }
            return (ParameterContract[])parameters.Clone();
        }
    }

    /// <summary>One bus parameter value inside an <see cref="AudioState"/>.</summary>
    public readonly struct BusParameterValue : IEquatable<BusParameterValue>
    {
        public BusParameterValue(BusId bus, ParameterId parameter, ParameterValue value)
        {
            Bus = bus;
            Parameter = parameter;
            Value = value;
        }

        public BusId Bus { get; }
        public ParameterId Parameter { get; }
        public ParameterValue Value { get; }

        public bool Equals(BusParameterValue other) => Bus == other.Bus && Parameter == other.Parameter && Value == other.Value;
        public override bool Equals(object obj) => obj is BusParameterValue other && Equals(other);
        public override int GetHashCode() => (Bus.GetHashCode() * 397) ^ (Parameter.GetHashCode() * 31) ^ Value.GetHashCode();
    }

    /// <summary>An active event attached to exactly one anchor.</summary>
    public readonly struct EventAttachment : IEquatable<EventAttachment>
    {
        public EventAttachment(AudioEventId eventId, AnchorId anchor)
        {
            Event = eventId;
            Anchor = anchor;
        }

        public AudioEventId Event { get; }
        public AnchorId Anchor { get; }

        public bool Equals(EventAttachment other) => Event == other.Event && Anchor == other.Anchor;
        public override bool Equals(object obj) => obj is EventAttachment other && Equals(other);
        public override int GetHashCode() => (Event.GetHashCode() * 397) ^ Anchor.GetHashCode();
    }

    /// <summary>
    /// Closed set of intents. Each one is applied to an <see cref="AudioState"/> and either
    /// yields a new state or a structured failure that leaves the prior state untouched.
    /// </summary>
    public abstract class AudioIntent
    {
        private protected AudioIntent() { }

        public abstract string Describe();

        internal abstract AudioResult<AudioState> ApplyTo(AudioState state);

        public override string ToString() => Describe();
    }

    public sealed class PostEventIntent : AudioIntent
    {
        public PostEventIntent(AudioEventId eventId) { Event = eventId; }

        public AudioEventId Event { get; }

        public override string Describe() => $"post {Event}";

        internal override AudioResult<AudioState> ApplyTo(AudioState state) => state.PostEvent(Event);
    }

    public sealed class StopEventIntent : AudioIntent
    {
        public StopEventIntent(AudioEventId eventId) { Event = eventId; }

        public AudioEventId Event { get; }

        public override string Describe() => $"stop {Event}";

        internal override AudioResult<AudioState> ApplyTo(AudioState state) => state.StopEvent(Event);
    }

    public sealed class SetParameterIntent : AudioIntent
    {
        public SetParameterIntent(BusId bus, ParameterId parameter, ParameterValue value)
        {
            Bus = bus;
            Parameter = parameter;
            Value = value;
        }

        public BusId Bus { get; }
        public ParameterId Parameter { get; }
        public ParameterValue Value { get; }

        public override string Describe() => $"set {Bus}/{Parameter} = {Value}";

        internal override AudioResult<AudioState> ApplyTo(AudioState state) => state.SetParameter(Bus, Parameter, Value);
    }

    public sealed class RegisterAnchorIntent : AudioIntent
    {
        public RegisterAnchorIntent(AnchorId anchor) { Anchor = anchor; }

        public AnchorId Anchor { get; }

        public override string Describe() => $"register anchor {Anchor}";

        internal override AudioResult<AudioState> ApplyTo(AudioState state) => state.RegisterAnchor(Anchor);
    }

    public sealed class UnregisterAnchorIntent : AudioIntent
    {
        public UnregisterAnchorIntent(AnchorId anchor) { Anchor = anchor; }

        public AnchorId Anchor { get; }

        public override string Describe() => $"unregister anchor {Anchor}";

        internal override AudioResult<AudioState> ApplyTo(AudioState state) => state.UnregisterAnchor(Anchor);
    }

    public sealed class AttachEventIntent : AudioIntent
    {
        public AttachEventIntent(AudioEventId eventId, AnchorId anchor)
        {
            Event = eventId;
            Anchor = anchor;
        }

        public AudioEventId Event { get; }
        public AnchorId Anchor { get; }

        public override string Describe() => $"attach {Event} to {Anchor}";

        internal override AudioResult<AudioState> ApplyTo(AudioState state) => state.AttachEvent(Event, Anchor);
    }

    public sealed class MoveEventIntent : AudioIntent
    {
        public MoveEventIntent(AudioEventId eventId, AnchorId anchor)
        {
            Event = eventId;
            Anchor = anchor;
        }

        public AudioEventId Event { get; }
        public AnchorId Anchor { get; }

        public override string Describe() => $"move {Event} to {Anchor}";

        internal override AudioResult<AudioState> ApplyTo(AudioState state) => state.MoveEvent(Event, Anchor);
    }

    public sealed class DetachEventIntent : AudioIntent
    {
        public DetachEventIntent(AudioEventId eventId) { Event = eventId; }

        public AudioEventId Event { get; }

        public override string Describe() => $"detach {Event}";

        internal override AudioResult<AudioState> ApplyTo(AudioState state) => state.DetachEvent(Event);
    }

    public sealed class TransitionSnapshotIntent : AudioIntent
    {
        public TransitionSnapshotIntent(SnapshotId snapshot, float durationSeconds)
        {
            Snapshot = snapshot;
            DurationSeconds = durationSeconds;
        }

        public SnapshotId Snapshot { get; }
        /// <summary>Explicit, non-negative transition time; the Core records it, the adapter honors it.</summary>
        public float DurationSeconds { get; }

        public override string Describe() => $"transition to {Snapshot} over {DurationSeconds.ToString("R", CultureInfo.InvariantCulture)}s";

        internal override AudioResult<AudioState> ApplyTo(AudioState state) => state.TransitionSnapshot(Snapshot, DurationSeconds);
    }

    /// <summary>The outcome of one intent inside a sequence.</summary>
    public sealed class AudioIntentOutcome
    {
        public AudioIntentOutcome(int index, AudioIntent intent, bool accepted, string code, string message)
        {
            Index = index;
            Intent = intent;
            Accepted = accepted;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public int Index { get; }
        public AudioIntent Intent { get; }
        public bool Accepted { get; }
        /// <summary>Empty when accepted; otherwise a stable <see cref="AudioFailure"/> code.</summary>
        public string Code { get; }
        public string Message { get; }
    }

    /// <summary>The state after a sequence and one outcome per intent. A rejected intent keeps the state it found.</summary>
    public sealed class AudioSequenceResult
    {
        public AudioSequenceResult(AudioState state, IReadOnlyList<AudioIntentOutcome> outcomes)
        {
            State = state;
            Outcomes = outcomes;
        }

        public AudioState State { get; }
        public IReadOnlyList<AudioIntentOutcome> Outcomes { get; }

        public bool AllAccepted
        {
            get
            {
                foreach (var outcome in Outcomes)
                {
                    if (!outcome.Accepted) return false;
                }
                return true;
            }
        }

        public int AcceptedCount
        {
            get
            {
                var count = 0;
                foreach (var outcome in Outcomes)
                {
                    if (outcome.Accepted) count++;
                }
                return count;
            }
        }

        public int RejectedCount => Outcomes.Count - AcceptedCount;
    }

    /// <summary>
    /// Immutable audio state over one graph: active events, the current mix snapshot,
    /// bus parameter values, registered anchors, and event attachments. Every mutation
    /// returns a new state; the same intent sequence from the same initial state yields
    /// an equal state.
    /// </summary>
    public sealed class AudioState : IEquatable<AudioState>
    {
        private readonly SortedSet<AudioEventId> _activeEvents;
        private readonly SortedDictionary<string, BusParameterValue> _parameters;
        private readonly SortedDictionary<AudioEventId, AnchorId> _attachments;
        private readonly SortedSet<AnchorId> _anchors;

        private AudioState(
            MixGraph graph,
            SortedSet<AudioEventId> activeEvents,
            SnapshotId? currentSnapshot,
            SortedDictionary<string, BusParameterValue> parameters,
            SortedDictionary<AudioEventId, AnchorId> attachments,
            SortedSet<AnchorId> anchors)
        {
            Graph = graph;
            _activeEvents = activeEvents;
            CurrentSnapshot = currentSnapshot;
            _parameters = parameters;
            _attachments = attachments;
            _anchors = anchors;
        }

        public MixGraph Graph { get; }
        /// <summary>Null until the first accepted transition.</summary>
        public SnapshotId? CurrentSnapshot { get; }

        public IReadOnlyList<AudioEventId> ActiveEvents => new List<AudioEventId>(_activeEvents);
        public IReadOnlyList<BusParameterValue> ParameterValues => new List<BusParameterValue>(_parameters.Values);
        public IReadOnlyList<AnchorId> Anchors => new List<AnchorId>(_anchors);

        public IReadOnlyList<EventAttachment> Attachments
        {
            get
            {
                var list = new List<EventAttachment>(_attachments.Count);
                foreach (var pair in _attachments) list.Add(new EventAttachment(pair.Key, pair.Value));
                return list;
            }
        }

        /// <summary>The initial state: no active event, no snapshot, no anchor, every declared parameter at its default.</summary>
        public static AudioState Initial(MixGraph graph)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            var parameters = new SortedDictionary<string, BusParameterValue>(StringComparer.Ordinal);
            foreach (var bus in graph.Buses)
            {
                foreach (var contract in bus.Parameters)
                {
                    parameters[Key(bus.Id, contract.Id)] = new BusParameterValue(bus.Id, contract.Id, contract.DefaultValue);
                }
            }
            return new AudioState(graph, new SortedSet<AudioEventId>(), null, parameters, new SortedDictionary<AudioEventId, AnchorId>(), new SortedSet<AnchorId>());
        }

        public bool IsActive(AudioEventId eventId) => _activeEvents.Contains(eventId);
        public bool HasAnchor(AnchorId anchor) => _anchors.Contains(anchor);
        public bool TryGetAttachment(AudioEventId eventId, out AnchorId anchor) => _attachments.TryGetValue(eventId, out anchor);

        public bool TryGetParameter(BusId bus, ParameterId parameter, out ParameterValue value)
        {
            if (_parameters.TryGetValue(Key(bus, parameter), out var stored))
            {
                value = stored.Value;
                return true;
            }
            value = default;
            return false;
        }

        public AudioResult<AudioState> Apply(AudioIntent intent)
        {
            if (intent == null) throw new ArgumentNullException(nameof(intent));
            return intent.ApplyTo(this);
        }

        /// <summary>Applies every intent in order; a rejected intent is recorded and the state it found is kept for the next one.</summary>
        public AudioSequenceResult ApplyAll(IEnumerable<AudioIntent> intents)
        {
            if (intents == null) throw new ArgumentNullException(nameof(intents));
            var state = this;
            var outcomes = new List<AudioIntentOutcome>();
            var index = 0;
            foreach (var intent in intents)
            {
                var result = state.Apply(intent);
                if (result.Succeeded)
                {
                    state = result.Value;
                    outcomes.Add(new AudioIntentOutcome(index, intent, true, AudioFailure.None, string.Empty));
                }
                else
                {
                    outcomes.Add(new AudioIntentOutcome(index, intent, false, result.Code, result.Message));
                }
                index++;
            }
            return new AudioSequenceResult(state, outcomes);
        }

        internal AudioResult<AudioState> PostEvent(AudioEventId eventId)
        {
            if (!Graph.TryGetEvent(eventId, out _))
            {
                return AudioResult<AudioState>.Fail(AudioFailure.EventUnknown, $"Event '{eventId}' is not in the mix graph.");
            }
            if (_activeEvents.Contains(eventId))
            {
                return AudioResult<AudioState>.Fail(AudioFailure.EventActive, $"Event '{eventId}' is already active; stop it before posting it again.");
            }
            var events = new SortedSet<AudioEventId>(_activeEvents) { eventId };
            return AudioResult<AudioState>.Ok(new AudioState(Graph, events, CurrentSnapshot, _parameters, _attachments, _anchors));
        }

        internal AudioResult<AudioState> StopEvent(AudioEventId eventId)
        {
            if (!Graph.TryGetEvent(eventId, out _))
            {
                return AudioResult<AudioState>.Fail(AudioFailure.EventUnknown, $"Event '{eventId}' is not in the mix graph.");
            }
            if (!_activeEvents.Contains(eventId))
            {
                return AudioResult<AudioState>.Fail(AudioFailure.EventInactive, $"Event '{eventId}' is not active.");
            }
            var events = new SortedSet<AudioEventId>(_activeEvents);
            events.Remove(eventId);
            var attachments = _attachments;
            if (_attachments.ContainsKey(eventId))
            {
                attachments = new SortedDictionary<AudioEventId, AnchorId>(_attachments);
                attachments.Remove(eventId);
            }
            return AudioResult<AudioState>.Ok(new AudioState(Graph, events, CurrentSnapshot, _parameters, attachments, _anchors));
        }

        internal AudioResult<AudioState> SetParameter(BusId bus, ParameterId parameter, ParameterValue value)
        {
            var contract = Graph.ResolveParameter(bus, parameter);
            if (!contract.Succeeded) return contract.As<AudioState>();
            var accepted = contract.Value.Validate(value);
            if (!accepted.Succeeded) return accepted.As<AudioState>();
            var parameters = new SortedDictionary<string, BusParameterValue>(_parameters, StringComparer.Ordinal);
            parameters[Key(bus, parameter)] = new BusParameterValue(bus, parameter, accepted.Value);
            return AudioResult<AudioState>.Ok(new AudioState(Graph, _activeEvents, CurrentSnapshot, parameters, _attachments, _anchors));
        }

        internal AudioResult<AudioState> RegisterAnchor(AnchorId anchor)
        {
            if (_anchors.Contains(anchor))
            {
                return AudioResult<AudioState>.Fail(AudioFailure.AnchorDuplicate, $"Anchor '{anchor}' is already registered.");
            }
            var anchors = new SortedSet<AnchorId>(_anchors) { anchor };
            return AudioResult<AudioState>.Ok(new AudioState(Graph, _activeEvents, CurrentSnapshot, _parameters, _attachments, anchors));
        }

        /// <summary>Removes the anchor; every event attached to it becomes unattached but stays active.</summary>
        internal AudioResult<AudioState> UnregisterAnchor(AnchorId anchor)
        {
            if (!_anchors.Contains(anchor))
            {
                return AudioResult<AudioState>.Fail(AudioFailure.AnchorUnknown, $"Anchor '{anchor}' is not registered.");
            }
            var anchors = new SortedSet<AnchorId>(_anchors);
            anchors.Remove(anchor);
            var attachments = new SortedDictionary<AudioEventId, AnchorId>();
            foreach (var pair in _attachments)
            {
                if (pair.Value != anchor) attachments[pair.Key] = pair.Value;
            }
            return AudioResult<AudioState>.Ok(new AudioState(Graph, _activeEvents, CurrentSnapshot, _parameters, attachments, anchors));
        }

        internal AudioResult<AudioState> AttachEvent(AudioEventId eventId, AnchorId anchor)
        {
            var check = RequireActive(eventId);
            if (check != null) return check.Value;
            if (!_anchors.Contains(anchor))
            {
                return AudioResult<AudioState>.Fail(AudioFailure.AnchorUnknown, $"Anchor '{anchor}' is not registered.");
            }
            if (_attachments.TryGetValue(eventId, out var current))
            {
                return AudioResult<AudioState>.Fail(AudioFailure.EventAttached, $"Event '{eventId}' is already attached to '{current}'; move or detach it instead.");
            }
            var attachments = new SortedDictionary<AudioEventId, AnchorId>(_attachments) { [eventId] = anchor };
            return AudioResult<AudioState>.Ok(new AudioState(Graph, _activeEvents, CurrentSnapshot, _parameters, attachments, _anchors));
        }

        internal AudioResult<AudioState> MoveEvent(AudioEventId eventId, AnchorId anchor)
        {
            var check = RequireActive(eventId);
            if (check != null) return check.Value;
            if (!_anchors.Contains(anchor))
            {
                return AudioResult<AudioState>.Fail(AudioFailure.AnchorUnknown, $"Anchor '{anchor}' is not registered.");
            }
            if (!_attachments.ContainsKey(eventId))
            {
                return AudioResult<AudioState>.Fail(AudioFailure.EventUnattached, $"Event '{eventId}' is not attached; attach it first.");
            }
            var attachments = new SortedDictionary<AudioEventId, AnchorId>(_attachments) { [eventId] = anchor };
            return AudioResult<AudioState>.Ok(new AudioState(Graph, _activeEvents, CurrentSnapshot, _parameters, attachments, _anchors));
        }

        internal AudioResult<AudioState> DetachEvent(AudioEventId eventId)
        {
            var check = RequireActive(eventId);
            if (check != null) return check.Value;
            if (!_attachments.ContainsKey(eventId))
            {
                return AudioResult<AudioState>.Fail(AudioFailure.EventUnattached, $"Event '{eventId}' is not attached.");
            }
            var attachments = new SortedDictionary<AudioEventId, AnchorId>(_attachments);
            attachments.Remove(eventId);
            return AudioResult<AudioState>.Ok(new AudioState(Graph, _activeEvents, CurrentSnapshot, _parameters, attachments, _anchors));
        }

        internal AudioResult<AudioState> TransitionSnapshot(SnapshotId snapshot, float durationSeconds)
        {
            if (!Graph.TryGetSnapshot(snapshot, out _))
            {
                return AudioResult<AudioState>.Fail(AudioFailure.SnapshotUnknown, $"Snapshot '{snapshot}' is not in the mix graph.");
            }
            if (float.IsNaN(durationSeconds) || float.IsInfinity(durationSeconds) || durationSeconds < 0f)
            {
                return AudioResult<AudioState>.Fail(AudioFailure.SnapshotDurationInvalid, $"Transition to '{snapshot}' needs a finite, non-negative duration.");
            }
            return AudioResult<AudioState>.Ok(new AudioState(Graph, _activeEvents, snapshot, _parameters, _attachments, _anchors));
        }

        private AudioResult<AudioState>? RequireActive(AudioEventId eventId)
        {
            if (!Graph.TryGetEvent(eventId, out _))
            {
                return AudioResult<AudioState>.Fail(AudioFailure.EventUnknown, $"Event '{eventId}' is not in the mix graph.");
            }
            if (!_activeEvents.Contains(eventId))
            {
                return AudioResult<AudioState>.Fail(AudioFailure.EventInactive, $"Event '{eventId}' is not active.");
            }
            return null;
        }

        /// <summary>A canonical text of the whole state; equal states have equal fingerprints.</summary>
        public string Fingerprint()
        {
            var builder = new StringBuilder();
            builder.Append("events[");
            builder.Append(string.Join(",", _activeEvents));
            builder.Append("] snapshot[");
            builder.Append(CurrentSnapshot.HasValue ? CurrentSnapshot.Value.Value : "-");
            builder.Append("] parameters[");
            var first = true;
            foreach (var pair in _parameters)
            {
                if (!first) builder.Append(',');
                first = false;
                builder.Append(pair.Key).Append('=').Append(pair.Value.Value);
            }
            builder.Append("] attachments[");
            first = true;
            foreach (var pair in _attachments)
            {
                if (!first) builder.Append(',');
                first = false;
                builder.Append(pair.Key).Append("->").Append(pair.Value);
            }
            builder.Append("] anchors[");
            builder.Append(string.Join(",", _anchors));
            builder.Append(']');
            return builder.ToString();
        }

        public bool Equals(AudioState other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return ReferenceEquals(Graph, other.Graph)
                   && string.Equals(Fingerprint(), other.Fingerprint(), StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is AudioState other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Fingerprint());
        public override string ToString() => Fingerprint();

        private static string Key(BusId bus, ParameterId parameter) => bus.Value + "|" + parameter.Value;
    }
}
