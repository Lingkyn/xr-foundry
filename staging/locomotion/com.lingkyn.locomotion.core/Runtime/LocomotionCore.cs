using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Lingkyn.Locomotion.Core
{
    // Engine-light locomotion core: a closed set of locomotion mode identities and an
    // open anchor identity sharing one canonical form, a closed typed comfort policy
    // with fail-closed rejection, teleport/turn/move intents applied to an immutable
    // locomotion state with an anchor registry, deterministic immutable state produced
    // by intent sequences, and structured results with stable failure codes. No
    // UnityEngine dependency, no input device, no world-space transform, no rendering.
    //
    // This file is authored and unexecuted: there is no C# compiler available while it
    // was written, and it has not compiled or run.

    /// <summary>Stable machine codes carried by every failed <see cref="LocomotionResult{T}"/>.</summary>
    public static class LocomotionFailure
    {
        public const string None = "";
        public const string IdentityMalformed = "identity.malformed";
        public const string OptionUnknown = "option.unknown";
        public const string OptionKindMismatch = "option.kind.mismatch";
        public const string OptionOutOfRange = "option.out_of_range";
        public const string AnchorUnknown = "anchor.unknown";
        public const string AnchorDuplicate = "anchor.duplicate";
        public const string ModeDisabled = "mode.disabled";
        public const string PostureMismatch = "posture.mismatch";
    }

    /// <summary>Structured outcome: a value on success, a stable code and a human message on failure.</summary>
    public readonly struct LocomotionResult<T>
    {
        private LocomotionResult(bool succeeded, T value, string code, string message)
        {
            Succeeded = succeeded;
            Value = value;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public bool Succeeded { get; }
        public T Value { get; }
        /// <summary>Empty on success; otherwise one of the <see cref="LocomotionFailure"/> constants.</summary>
        public string Code { get; }
        public string Message { get; }

        public static LocomotionResult<T> Ok(T value) => new LocomotionResult<T>(true, value, LocomotionFailure.None, string.Empty);

        public static LocomotionResult<T> Fail(string code, string message)
        {
            if (string.IsNullOrEmpty(code)) throw new ArgumentException("A failure needs a stable code.", nameof(code));
            return new LocomotionResult<T>(false, default, code, message);
        }

        public LocomotionResult<TOther> As<TOther>()
        {
            if (Succeeded) throw new InvalidOperationException("Only a failed result can be re-typed.");
            return LocomotionResult<TOther>.Fail(Code, Message);
        }
    }

    /// <summary>
    /// One canonical form shared by every locomotion identity: ASCII letters folded to
    /// lower case, digits, '.', '_', '-', '/' as segment separators, 1 to 128 characters,
    /// no whitespace, no leading or trailing separator, no doubled separator.
    /// </summary>
    public static class LocomotionIdentity
    {
        public const int MaximumLength = 128;

        public static LocomotionResult<string> TryCanonicalize(string value, string kind)
        {
            if (value == null || value.Length == 0)
            {
                return LocomotionResult<string>.Fail(LocomotionFailure.IdentityMalformed, $"A {kind} id must not be empty.");
            }
            if (value.Length > MaximumLength)
            {
                return LocomotionResult<string>.Fail(LocomotionFailure.IdentityMalformed, $"A {kind} id must be at most {MaximumLength} characters.");
            }
            var canonical = new char[value.Length];
            var previousSeparator = false;
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (char.IsWhiteSpace(character))
                {
                    return LocomotionResult<string>.Fail(LocomotionFailure.IdentityMalformed, $"The {kind} id '{value}' contains whitespace at position {index}.");
                }
                if (character >= 'A' && character <= 'Z') character = (char)(character + ('a' - 'A'));
                var separator = character == '.' || character == '/';
                var ok = (character >= 'a' && character <= 'z')
                         || (character >= '0' && character <= '9')
                         || separator || character == '_' || character == '-';
                if (!ok)
                {
                    return LocomotionResult<string>.Fail(LocomotionFailure.IdentityMalformed, $"The {kind} id '{value}' contains an invalid character at position {index}.");
                }
                if ((separator || character == '-') && index == 0)
                {
                    return LocomotionResult<string>.Fail(LocomotionFailure.IdentityMalformed, $"The {kind} id '{value}' must not start with a separator.");
                }
                if (separator && index == value.Length - 1)
                {
                    return LocomotionResult<string>.Fail(LocomotionFailure.IdentityMalformed, $"The {kind} id '{value}' must not end with a separator.");
                }
                if (separator && previousSeparator)
                {
                    return LocomotionResult<string>.Fail(LocomotionFailure.IdentityMalformed, $"The {kind} id '{value}' repeats a separator at position {index}.");
                }
                previousSeparator = separator;
                canonical[index] = character;
            }
            return LocomotionResult<string>.Ok(new string(canonical));
        }
    }

    /// <summary>
    /// Closed set of locomotion mode identities: teleport, snap_turn, smooth_turn, and
    /// continuous_move. Unlike <see cref="AnchorId"/>, well-formed text that is not one
    /// of these four canonical values is rejected as malformed, because the mode
    /// vocabulary itself is closed.
    /// </summary>
    public readonly struct ModeId : IEquatable<ModeId>, IComparable<ModeId>
    {
        private ModeId(string value) { Value = value; }

        public string Value { get; }

        public static readonly ModeId Teleport = new ModeId("teleport");
        public static readonly ModeId SnapTurn = new ModeId("snap_turn");
        public static readonly ModeId SmoothTurn = new ModeId("smooth_turn");
        public static readonly ModeId ContinuousMove = new ModeId("continuous_move");

        private static readonly ModeId[] Declared = { Teleport, SnapTurn, SmoothTurn, ContinuousMove };

        public static LocomotionResult<ModeId> TryCreate(string value)
        {
            var canonical = LocomotionIdentity.TryCanonicalize(value, "mode");
            if (!canonical.Succeeded) return canonical.As<ModeId>();
            foreach (var declared in Declared)
            {
                if (declared.Value == canonical.Value) return LocomotionResult<ModeId>.Ok(declared);
            }
            return LocomotionResult<ModeId>.Fail(
                LocomotionFailure.IdentityMalformed,
                $"'{canonical.Value}' is not a declared locomotion mode; expected one of teleport, snap_turn, smooth_turn, continuous_move.");
        }

        public static ModeId Parse(string value)
        {
            var result = TryCreate(value);
            if (!result.Succeeded) throw new ArgumentException(result.Message, nameof(value));
            return result.Value;
        }

        public bool Equals(ModeId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ModeId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public int CompareTo(ModeId other) => string.CompareOrdinal(Value, other.Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(ModeId left, ModeId right) => left.Equals(right);
        public static bool operator !=(ModeId left, ModeId right) => !left.Equals(right);
    }

    /// <summary>
    /// Stable identity of a spatial anchor, separate from any transform, scene object, or
    /// tracked device. Anchors share <see cref="ModeId"/>'s canonicalization rules but are
    /// an open vocabulary: any well-formed text is a valid anchor id, registered at
    /// runtime rather than declared up front.
    /// </summary>
    public readonly struct AnchorId : IEquatable<AnchorId>, IComparable<AnchorId>
    {
        private AnchorId(string value) { Value = value; }

        public string Value { get; }

        public static LocomotionResult<AnchorId> TryCreate(string value)
        {
            var canonical = LocomotionIdentity.TryCanonicalize(value, "anchor");
            return canonical.Succeeded ? LocomotionResult<AnchorId>.Ok(new AnchorId(canonical.Value)) : canonical.As<AnchorId>();
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

    /// <summary>Closed direction for a turn intent.</summary>
    public enum TurnDirection
    {
        Left,
        Right,
    }

    /// <summary>Closed selection between the two turn modes a comfort policy can activate.</summary>
    public enum TurnMode
    {
        Snap,
        Smooth,
    }

    /// <summary>Closed posture a comfort policy declares for the consumer.</summary>
    public enum Posture
    {
        Seated,
        Standing,
    }

    /// <summary>Closed set of value shapes a comfort option can carry.</summary>
    public enum ComfortOptionKind
    {
        Bool,
        FloatRange,
        DiscreteDegrees,
        Enumerated,
    }

    /// <summary>A typed value carried by a set-comfort-option intent. Exactly one payload is meaningful per kind.</summary>
    public readonly struct ComfortOptionValue : IEquatable<ComfortOptionValue>
    {
        private ComfortOptionValue(ComfortOptionKind kind, float number, bool flag, string choice)
        {
            Kind = kind;
            Number = number;
            Flag = flag;
            Choice = choice ?? string.Empty;
        }

        public ComfortOptionKind Kind { get; }
        public float Number { get; }
        public bool Flag { get; }
        public string Choice { get; }

        public static ComfortOptionValue Bool(bool value) => new ComfortOptionValue(ComfortOptionKind.Bool, 0f, value, string.Empty);
        public static ComfortOptionValue FloatRange(float value) => new ComfortOptionValue(ComfortOptionKind.FloatRange, value, false, string.Empty);
        public static ComfortOptionValue DiscreteDegrees(float value) => new ComfortOptionValue(ComfortOptionKind.DiscreteDegrees, value, false, string.Empty);
        public static ComfortOptionValue Enumerated(string value) => new ComfortOptionValue(ComfortOptionKind.Enumerated, 0f, false, value);

        public bool Equals(ComfortOptionValue other)
        {
            if (Kind != other.Kind) return false;
            switch (Kind)
            {
                case ComfortOptionKind.Bool: return Flag == other.Flag;
                case ComfortOptionKind.FloatRange:
                case ComfortOptionKind.DiscreteDegrees: return Number.Equals(other.Number);
                default: return string.Equals(Choice, other.Choice, StringComparison.Ordinal);
            }
        }

        public override bool Equals(object obj) => obj is ComfortOptionValue other && Equals(other);

        public override int GetHashCode()
        {
            switch (Kind)
            {
                case ComfortOptionKind.Bool: return Flag ? 1 : 0;
                case ComfortOptionKind.FloatRange:
                case ComfortOptionKind.DiscreteDegrees: return Number.GetHashCode();
                default: return StringComparer.Ordinal.GetHashCode(Choice ?? string.Empty);
            }
        }

        public override string ToString()
        {
            switch (Kind)
            {
                case ComfortOptionKind.Bool: return Flag ? "true" : "false";
                case ComfortOptionKind.FloatRange:
                case ComfortOptionKind.DiscreteDegrees: return Number.ToString("R", CultureInfo.InvariantCulture);
                default: return Choice ?? string.Empty;
            }
        }

        public static bool operator ==(ComfortOptionValue left, ComfortOptionValue right) => left.Equals(right);
        public static bool operator !=(ComfortOptionValue left, ComfortOptionValue right) => !left.Equals(right);
    }

    /// <summary>
    /// The declared contract for one comfort option: a closed kind and, per kind, a
    /// declared inclusive float range, a declared discrete degree set, or a declared
    /// value set. Validation fails closed with option.kind.mismatch or
    /// option.out_of_range.
    /// </summary>
    public sealed class ComfortOptionContract
    {
        private readonly float[] _discreteValues;
        private readonly string[] _values;

        private ComfortOptionContract(string name, ComfortOptionKind kind, float minimum, float maximum, float[] discreteValues, string[] values, ComfortOptionValue defaultValue)
        {
            Name = name;
            Kind = kind;
            Minimum = minimum;
            Maximum = maximum;
            _discreteValues = discreteValues ?? Array.Empty<float>();
            _values = values ?? Array.Empty<string>();
            DefaultValue = defaultValue;
        }

        public string Name { get; }
        public ComfortOptionKind Kind { get; }
        /// <summary>Inclusive lower bound; meaningful for <see cref="ComfortOptionKind.FloatRange"/> only.</summary>
        public float Minimum { get; }
        /// <summary>Inclusive upper bound; meaningful for <see cref="ComfortOptionKind.FloatRange"/> only.</summary>
        public float Maximum { get; }
        /// <summary>Declared degrees, in declaration order; meaningful for <see cref="ComfortOptionKind.DiscreteDegrees"/> only.</summary>
        public IReadOnlyList<float> DiscreteValues => _discreteValues;
        /// <summary>Declared choices, in declaration order; meaningful for <see cref="ComfortOptionKind.Enumerated"/> only.</summary>
        public IReadOnlyList<string> Values => _values;
        public ComfortOptionValue DefaultValue { get; }

        public static ComfortOptionContract Bool(string name, bool defaultValue) =>
            new ComfortOptionContract(name, ComfortOptionKind.Bool, 0f, 0f, Array.Empty<float>(), Array.Empty<string>(), ComfortOptionValue.Bool(defaultValue));

        public static ComfortOptionContract FloatRange(string name, float minimum, float maximum, float defaultValue)
        {
            if (float.IsNaN(minimum) || float.IsNaN(maximum) || float.IsInfinity(minimum) || float.IsInfinity(maximum))
            {
                throw new ArgumentException($"Comfort option '{name}' must declare a finite range.");
            }
            if (minimum > maximum)
            {
                throw new ArgumentException($"Comfort option '{name}' declares minimum {Text(minimum)} above maximum {Text(maximum)}.");
            }
            if (float.IsNaN(defaultValue) || defaultValue < minimum || defaultValue > maximum)
            {
                throw new ArgumentException($"Comfort option '{name}' default {Text(defaultValue)} lies outside [{Text(minimum)}, {Text(maximum)}].");
            }
            return new ComfortOptionContract(name, ComfortOptionKind.FloatRange, minimum, maximum, Array.Empty<float>(), Array.Empty<string>(), ComfortOptionValue.FloatRange(defaultValue));
        }

        public static ComfortOptionContract DiscreteDegrees(string name, IEnumerable<float> values, float defaultValue)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            var list = new List<float>();
            var seen = new HashSet<float>();
            foreach (var value in values)
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    throw new ArgumentException($"Comfort option '{name}' declares a non-finite degree value.");
                }
                if (!seen.Add(value))
                {
                    throw new ArgumentException($"Comfort option '{name}' declares degree value {Text(value)} more than once.");
                }
                list.Add(value);
            }
            if (list.Count == 0)
            {
                throw new ArgumentException($"Comfort option '{name}' must declare at least one degree value.");
            }
            if (!seen.Contains(defaultValue))
            {
                throw new ArgumentException($"Comfort option '{name}' default {Text(defaultValue)} is not a declared degree value.");
            }
            return new ComfortOptionContract(name, ComfortOptionKind.DiscreteDegrees, 0f, 0f, list.ToArray(), Array.Empty<string>(), ComfortOptionValue.DiscreteDegrees(defaultValue));
        }

        public static ComfortOptionContract Enumerated(string name, IEnumerable<string> values, string defaultValue)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            var list = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                if (string.IsNullOrEmpty(value))
                {
                    throw new ArgumentException($"Comfort option '{name}' declares an empty value.");
                }
                if (!seen.Add(value))
                {
                    throw new ArgumentException($"Comfort option '{name}' declares value '{value}' more than once.");
                }
                list.Add(value);
            }
            if (list.Count == 0)
            {
                throw new ArgumentException($"Comfort option '{name}' must declare at least one value.");
            }
            if (defaultValue == null || !seen.Contains(defaultValue))
            {
                throw new ArgumentException($"Comfort option '{name}' default '{defaultValue}' is not a declared value.");
            }
            return new ComfortOptionContract(name, ComfortOptionKind.Enumerated, 0f, 0f, Array.Empty<float>(), list.ToArray(), ComfortOptionValue.Enumerated(defaultValue));
        }

        /// <summary>Accepts the value unchanged or fails closed with a stable code.</summary>
        public LocomotionResult<ComfortOptionValue> Validate(ComfortOptionValue value)
        {
            if (value.Kind != Kind)
            {
                return LocomotionResult<ComfortOptionValue>.Fail(LocomotionFailure.OptionKindMismatch, $"Option '{Name}' is {Kind} but the value is {value.Kind}.");
            }
            switch (Kind)
            {
                case ComfortOptionKind.Bool:
                    return LocomotionResult<ComfortOptionValue>.Ok(value);
                case ComfortOptionKind.FloatRange:
                    if (float.IsNaN(value.Number) || value.Number < Minimum || value.Number > Maximum)
                    {
                        return LocomotionResult<ComfortOptionValue>.Fail(LocomotionFailure.OptionOutOfRange, $"Option '{Name}' value {Text(value.Number)} lies outside [{Text(Minimum)}, {Text(Maximum)}].");
                    }
                    return LocomotionResult<ComfortOptionValue>.Ok(value);
                case ComfortOptionKind.DiscreteDegrees:
                    if (Array.IndexOf(_discreteValues, value.Number) < 0)
                    {
                        return LocomotionResult<ComfortOptionValue>.Fail(LocomotionFailure.OptionOutOfRange, $"Option '{Name}' value {Text(value.Number)} is not one of the declared increments.");
                    }
                    return LocomotionResult<ComfortOptionValue>.Ok(value);
                default:
                    if (Array.IndexOf(_values, value.Choice) < 0)
                    {
                        return LocomotionResult<ComfortOptionValue>.Fail(LocomotionFailure.OptionOutOfRange, $"Option '{Name}' value '{value.Choice}' is not one of [{string.Join(", ", _values)}].");
                    }
                    return LocomotionResult<ComfortOptionValue>.Ok(value);
            }
        }

        private static string Text(float value) => value.ToString("R", CultureInfo.InvariantCulture);
    }

    /// <summary>The fixed, closed set of six comfort options every <see cref="ComfortPolicy"/> declares.</summary>
    public static class ComfortOptions
    {
        public const string VignetteEnabledName = "vignette.enabled";
        public const string VignetteIntensityName = "vignette.intensity";
        public const string TurnModeName = "turn.mode";
        public const string TurnIncrementDegreesName = "turn.increment_degrees";
        public const string MovementSpeedName = "movement.speed";
        public const string PostureName = "posture";

        public static readonly ComfortOptionContract VignetteEnabledOption = ComfortOptionContract.Bool(VignetteEnabledName, false);
        public static readonly ComfortOptionContract VignetteIntensityOption = ComfortOptionContract.FloatRange(VignetteIntensityName, 0f, 1f, 0f);
        public static readonly ComfortOptionContract TurnModeOption = ComfortOptionContract.Enumerated(TurnModeName, new[] { "snap", "smooth" }, "snap");
        public static readonly ComfortOptionContract TurnIncrementDegreesOption = ComfortOptionContract.DiscreteDegrees(TurnIncrementDegreesName, new[] { 15f, 30f, 45f, 60f, 90f }, 45f);
        public static readonly ComfortOptionContract MovementSpeedOption = ComfortOptionContract.FloatRange(MovementSpeedName, 0f, 5f, 1.5f);
        public static readonly ComfortOptionContract PostureOption = ComfortOptionContract.Enumerated(PostureName, new[] { "seated", "standing" }, "standing");

        private static readonly Dictionary<string, ComfortOptionContract> All = new Dictionary<string, ComfortOptionContract>(StringComparer.Ordinal)
        {
            [VignetteEnabledName] = VignetteEnabledOption,
            [VignetteIntensityName] = VignetteIntensityOption,
            [TurnModeName] = TurnModeOption,
            [TurnIncrementDegreesName] = TurnIncrementDegreesOption,
            [MovementSpeedName] = MovementSpeedOption,
            [PostureName] = PostureOption,
        };

        public static bool TryResolve(string name, out ComfortOptionContract contract) => All.TryGetValue(name, out contract);
    }

    /// <summary>
    /// Immutable comfort policy over the six declared <see cref="ComfortOptions"/>.
    /// Setting one option validates it against its declared contract and fails closed
    /// with option.unknown, option.kind.mismatch, or option.out_of_range without
    /// changing the policy.
    /// </summary>
    public sealed class ComfortPolicy : IEquatable<ComfortPolicy>
    {
        private ComfortPolicy(bool vignetteEnabled, float vignetteIntensity, TurnMode turnMode, float turnIncrementDegrees, float movementSpeed, Posture posture)
        {
            VignetteEnabled = vignetteEnabled;
            VignetteIntensity = vignetteIntensity;
            TurnMode = turnMode;
            TurnIncrementDegrees = turnIncrementDegrees;
            MovementSpeed = movementSpeed;
            Posture = posture;
        }

        public bool VignetteEnabled { get; }
        public float VignetteIntensity { get; }
        public TurnMode TurnMode { get; }
        public float TurnIncrementDegrees { get; }
        public float MovementSpeed { get; }
        public Posture Posture { get; }

        /// <summary>The default policy: vignette off, snap turn at 45 degrees, 1.5 m/s, standing.</summary>
        public static ComfortPolicy Default() => new ComfortPolicy(false, ComfortOptions.VignetteIntensityOption.DefaultValue.Number, global::Lingkyn.Locomotion.Core.TurnMode.Snap, ComfortOptions.TurnIncrementDegreesOption.DefaultValue.Number, ComfortOptions.MovementSpeedOption.DefaultValue.Number, global::Lingkyn.Locomotion.Core.Posture.Standing);

        public LocomotionResult<ComfortPolicy> TrySet(string optionName, ComfortOptionValue value)
        {
            var canonical = LocomotionIdentity.TryCanonicalize(optionName, "option");
            if (!canonical.Succeeded) return canonical.As<ComfortPolicy>();
            if (!ComfortOptions.TryResolve(canonical.Value, out var contract))
            {
                return LocomotionResult<ComfortPolicy>.Fail(LocomotionFailure.OptionUnknown, $"'{canonical.Value}' is not a declared comfort option.");
            }
            var validated = contract.Validate(value);
            if (!validated.Succeeded) return validated.As<ComfortPolicy>();
            return LocomotionResult<ComfortPolicy>.Ok(With(canonical.Value, validated.Value));
        }

        private ComfortPolicy With(string optionName, ComfortOptionValue value)
        {
            switch (optionName)
            {
                case ComfortOptions.VignetteEnabledName:
                    return new ComfortPolicy(value.Flag, VignetteIntensity, TurnMode, TurnIncrementDegrees, MovementSpeed, Posture);
                case ComfortOptions.VignetteIntensityName:
                    return new ComfortPolicy(VignetteEnabled, value.Number, TurnMode, TurnIncrementDegrees, MovementSpeed, Posture);
                case ComfortOptions.TurnModeName:
                    return new ComfortPolicy(VignetteEnabled, VignetteIntensity, value.Choice == "smooth" ? global::Lingkyn.Locomotion.Core.TurnMode.Smooth : global::Lingkyn.Locomotion.Core.TurnMode.Snap, TurnIncrementDegrees, MovementSpeed, Posture);
                case ComfortOptions.TurnIncrementDegreesName:
                    return new ComfortPolicy(VignetteEnabled, VignetteIntensity, TurnMode, value.Number, MovementSpeed, Posture);
                case ComfortOptions.MovementSpeedName:
                    return new ComfortPolicy(VignetteEnabled, VignetteIntensity, TurnMode, TurnIncrementDegrees, value.Number, Posture);
                default: // ComfortOptions.PostureName
                    return new ComfortPolicy(VignetteEnabled, VignetteIntensity, TurnMode, TurnIncrementDegrees, MovementSpeed, value.Choice == "seated" ? global::Lingkyn.Locomotion.Core.Posture.Seated : global::Lingkyn.Locomotion.Core.Posture.Standing);
            }
        }

        public bool Equals(ComfortPolicy other) => other != null && string.Equals(ToString(), other.ToString(), StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ComfortPolicy other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(ToString());

        public override string ToString() =>
            $"vignette.enabled={(VignetteEnabled ? "true" : "false")},vignette.intensity={Num(VignetteIntensity)},turn.mode={(TurnMode == global::Lingkyn.Locomotion.Core.TurnMode.Smooth ? "smooth" : "snap")},turn.increment_degrees={Num(TurnIncrementDegrees)},movement.speed={Num(MovementSpeed)},posture={(Posture == global::Lingkyn.Locomotion.Core.Posture.Seated ? "seated" : "standing")}";

        private static string Num(float value) => value.ToString("R", CultureInfo.InvariantCulture);
    }

    /// <summary>An immutable planar displacement, free of any engine vector type.</summary>
    public readonly struct PlanarOffset : IEquatable<PlanarOffset>
    {
        public PlanarOffset(float x, float z) { X = x; Z = z; }

        public float X { get; }
        public float Z { get; }

        public static readonly PlanarOffset Zero = new PlanarOffset(0f, 0f);

        public static PlanarOffset operator +(PlanarOffset left, PlanarOffset right) => new PlanarOffset(left.X + right.X, left.Z + right.Z);
        public static PlanarOffset operator *(PlanarOffset offset, float scalar) => new PlanarOffset(offset.X * scalar, offset.Z * scalar);

        public bool Equals(PlanarOffset other) => X.Equals(other.X) && Z.Equals(other.Z);
        public override bool Equals(object obj) => obj is PlanarOffset other && Equals(other);
        public override int GetHashCode() => (X.GetHashCode() * 397) ^ Z.GetHashCode();
        public override string ToString() => $"{X.ToString("R", CultureInfo.InvariantCulture)},{Z.ToString("R", CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    /// Closed set of intents. Each one is applied to a <see cref="LocomotionState"/> and
    /// either yields a new state or a structured failure that leaves the prior state
    /// untouched.
    /// </summary>
    public abstract class LocomotionIntent
    {
        private protected LocomotionIntent() { }

        public abstract string Describe();

        internal abstract LocomotionResult<LocomotionState> ApplyTo(LocomotionState state);

        public override string ToString() => Describe();
    }

    public sealed class RegisterAnchorIntent : LocomotionIntent
    {
        public RegisterAnchorIntent(AnchorId anchor) { Anchor = anchor; }

        public AnchorId Anchor { get; }

        public override string Describe() => $"register anchor {Anchor}";

        internal override LocomotionResult<LocomotionState> ApplyTo(LocomotionState state) => state.RegisterAnchor(Anchor);
    }

    public sealed class UnregisterAnchorIntent : LocomotionIntent
    {
        public UnregisterAnchorIntent(AnchorId anchor) { Anchor = anchor; }

        public AnchorId Anchor { get; }

        public override string Describe() => $"unregister anchor {Anchor}";

        internal override LocomotionResult<LocomotionState> ApplyTo(LocomotionState state) => state.UnregisterAnchor(Anchor);
    }

    /// <summary>A teleport request to a registered anchor id.</summary>
    public sealed class TeleportIntent : LocomotionIntent
    {
        public TeleportIntent(AnchorId anchor) { Anchor = anchor; }

        public AnchorId Anchor { get; }

        public override string Describe() => $"teleport to {Anchor}";

        internal override LocomotionResult<LocomotionState> ApplyTo(LocomotionState state) => state.Teleport(Anchor);
    }

    /// <summary>A turn by the policy's currently declared increment, in the given direction, through the named turn mode.</summary>
    public sealed class TurnIntent : LocomotionIntent
    {
        public TurnIntent(ModeId mode, TurnDirection direction)
        {
            if (mode != ModeId.SnapTurn && mode != ModeId.SmoothTurn)
            {
                throw new ArgumentException("A turn intent must name snap_turn or smooth_turn.", nameof(mode));
            }
            Mode = mode;
            Direction = direction;
        }

        public ModeId Mode { get; }
        public TurnDirection Direction { get; }

        public override string Describe() => $"turn {Direction} via {Mode}";

        internal override LocomotionResult<LocomotionState> ApplyTo(LocomotionState state) => state.Turn(Mode, Direction);
    }

    /// <summary>A move vector for one tick with an explicit, non-negative tick duration.</summary>
    public sealed class MoveIntent : LocomotionIntent
    {
        public MoveIntent(PlanarOffset vector, float tickDurationSeconds)
        {
            Vector = vector;
            TickDurationSeconds = tickDurationSeconds;
        }

        public PlanarOffset Vector { get; }
        public float TickDurationSeconds { get; }

        public override string Describe() => $"move {Vector} over {TickDurationSeconds.ToString("R", CultureInfo.InvariantCulture)}s";

        internal override LocomotionResult<LocomotionState> ApplyTo(LocomotionState state) => state.Move(Vector, TickDurationSeconds);
    }

    /// <summary>Sets one declared comfort option; a rejected value leaves the current policy and state untouched.</summary>
    public sealed class SetComfortOptionIntent : LocomotionIntent
    {
        public SetComfortOptionIntent(string optionName, ComfortOptionValue value)
        {
            OptionName = optionName ?? string.Empty;
            Value = value;
        }

        public string OptionName { get; }
        public ComfortOptionValue Value { get; }

        public override string Describe() => $"set {OptionName} = {Value}";

        internal override LocomotionResult<LocomotionState> ApplyTo(LocomotionState state) => state.SetComfortOption(OptionName, Value);
    }

    /// <summary>The outcome of one intent inside a sequence.</summary>
    public sealed class LocomotionIntentOutcome
    {
        public LocomotionIntentOutcome(int index, LocomotionIntent intent, bool accepted, string code, string message)
        {
            Index = index;
            Intent = intent;
            Accepted = accepted;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public int Index { get; }
        public LocomotionIntent Intent { get; }
        public bool Accepted { get; }
        /// <summary>Empty when accepted; otherwise a stable <see cref="LocomotionFailure"/> code.</summary>
        public string Code { get; }
        public string Message { get; }
    }

    /// <summary>The state after a sequence and one outcome per intent. A rejected intent keeps the state it found.</summary>
    public sealed class LocomotionSequenceResult
    {
        public LocomotionSequenceResult(LocomotionState state, IReadOnlyList<LocomotionIntentOutcome> outcomes)
        {
            State = state;
            Outcomes = outcomes;
        }

        public LocomotionState State { get; }
        public IReadOnlyList<LocomotionIntentOutcome> Outcomes { get; }

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
    /// Immutable locomotion state: the comfort policy, the registered anchor set, the
    /// current anchor, the accumulated heading, and the accumulated planar offset. Every
    /// mutation returns a new state; the same intent sequence from the same initial
    /// state and policy yields an equal state.
    /// </summary>
    public sealed class LocomotionState : IEquatable<LocomotionState>
    {
        private readonly SortedSet<AnchorId> _anchors;

        private LocomotionState(ComfortPolicy policy, AnchorId? currentAnchor, float headingDegrees, PlanarOffset offset, SortedSet<AnchorId> anchors)
        {
            Policy = policy;
            CurrentAnchor = currentAnchor;
            HeadingDegrees = headingDegrees;
            Offset = offset;
            _anchors = anchors;
        }

        public ComfortPolicy Policy { get; }
        /// <summary>Null until the first accepted teleport.</summary>
        public AnchorId? CurrentAnchor { get; }
        /// <summary>Accumulated heading in degrees, normalized to [0, 360).</summary>
        public float HeadingDegrees { get; }
        /// <summary>Accumulated planar offset from continuous movement.</summary>
        public PlanarOffset Offset { get; }

        public IReadOnlyList<AnchorId> RegisteredAnchors => new List<AnchorId>(_anchors);

        /// <summary>The turn mode the current policy selects: snap_turn or smooth_turn, never both.</summary>
        public ModeId ActiveTurnMode => Policy.TurnMode == TurnMode.Smooth ? ModeId.SmoothTurn : ModeId.SnapTurn;

        /// <summary>Teleport and continuous move are always active; the policy's turn mode selects exactly one turn mode.</summary>
        public IReadOnlyList<ModeId> ActiveModes => new List<ModeId> { ModeId.Teleport, ModeId.ContinuousMove, ActiveTurnMode };

        public bool IsModeActive(ModeId mode)
        {
            foreach (var active in ActiveModes)
            {
                if (active == mode) return true;
            }
            return false;
        }

        public bool IsAnchorRegistered(AnchorId anchor) => _anchors.Contains(anchor);

        /// <summary>The initial state: the given policy, no anchor, zero heading, zero offset, no registered anchor.</summary>
        public static LocomotionState Initial(ComfortPolicy policy)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            return new LocomotionState(policy, null, 0f, PlanarOffset.Zero, new SortedSet<AnchorId>());
        }

        public LocomotionResult<LocomotionState> Apply(LocomotionIntent intent)
        {
            if (intent == null) throw new ArgumentNullException(nameof(intent));
            return intent.ApplyTo(this);
        }

        /// <summary>Applies every intent in order; a rejected intent is recorded and the state it found is kept for the next one.</summary>
        public LocomotionSequenceResult ApplyAll(IEnumerable<LocomotionIntent> intents)
        {
            if (intents == null) throw new ArgumentNullException(nameof(intents));
            var state = this;
            var outcomes = new List<LocomotionIntentOutcome>();
            var index = 0;
            foreach (var intent in intents)
            {
                var result = state.Apply(intent);
                if (result.Succeeded)
                {
                    state = result.Value;
                    outcomes.Add(new LocomotionIntentOutcome(index, intent, true, LocomotionFailure.None, string.Empty));
                }
                else
                {
                    outcomes.Add(new LocomotionIntentOutcome(index, intent, false, result.Code, result.Message));
                }
                index++;
            }
            return new LocomotionSequenceResult(state, outcomes);
        }

        internal LocomotionResult<LocomotionState> RegisterAnchor(AnchorId anchor)
        {
            if (_anchors.Contains(anchor))
            {
                return LocomotionResult<LocomotionState>.Fail(LocomotionFailure.AnchorDuplicate, $"Anchor '{anchor}' is already registered.");
            }
            var anchors = new SortedSet<AnchorId>(_anchors) { anchor };
            return LocomotionResult<LocomotionState>.Ok(new LocomotionState(Policy, CurrentAnchor, HeadingDegrees, Offset, anchors));
        }

        /// <summary>Removes the anchor; if it was the current anchor, the current anchor becomes null.</summary>
        internal LocomotionResult<LocomotionState> UnregisterAnchor(AnchorId anchor)
        {
            if (!_anchors.Contains(anchor))
            {
                return LocomotionResult<LocomotionState>.Fail(LocomotionFailure.AnchorUnknown, $"Anchor '{anchor}' is not registered.");
            }
            var anchors = new SortedSet<AnchorId>(_anchors);
            anchors.Remove(anchor);
            var current = CurrentAnchor.HasValue && CurrentAnchor.Value == anchor ? (AnchorId?)null : CurrentAnchor;
            return LocomotionResult<LocomotionState>.Ok(new LocomotionState(Policy, current, HeadingDegrees, Offset, anchors));
        }

        internal LocomotionResult<LocomotionState> Teleport(AnchorId anchor)
        {
            if (!_anchors.Contains(anchor))
            {
                return LocomotionResult<LocomotionState>.Fail(LocomotionFailure.AnchorUnknown, $"Anchor '{anchor}' is not registered.");
            }
            return LocomotionResult<LocomotionState>.Ok(new LocomotionState(Policy, anchor, HeadingDegrees, Offset, _anchors));
        }

        internal LocomotionResult<LocomotionState> Turn(ModeId requestedMode, TurnDirection direction)
        {
            if (!IsModeActive(requestedMode))
            {
                return LocomotionResult<LocomotionState>.Fail(LocomotionFailure.ModeDisabled, $"'{requestedMode}' is not the active turn mode; the policy currently selects '{ActiveTurnMode}'.");
            }
            var delta = Policy.TurnIncrementDegrees * (direction == TurnDirection.Left ? -1f : 1f);
            var heading = Normalize(HeadingDegrees + delta);
            return LocomotionResult<LocomotionState>.Ok(new LocomotionState(Policy, CurrentAnchor, heading, Offset, _anchors));
        }

        internal LocomotionResult<LocomotionState> Move(PlanarOffset vector, float tickDurationSeconds)
        {
            if (Policy.Posture == Posture.Seated)
            {
                return LocomotionResult<LocomotionState>.Fail(LocomotionFailure.PostureMismatch, "Continuous movement is not admitted while the comfort policy posture is seated.");
            }
            var displacement = vector * (Policy.MovementSpeed * tickDurationSeconds);
            return LocomotionResult<LocomotionState>.Ok(new LocomotionState(Policy, CurrentAnchor, HeadingDegrees, Offset + displacement, _anchors));
        }

        internal LocomotionResult<LocomotionState> SetComfortOption(string optionName, ComfortOptionValue value)
        {
            var result = Policy.TrySet(optionName, value);
            if (!result.Succeeded) return result.As<LocomotionState>();
            return LocomotionResult<LocomotionState>.Ok(new LocomotionState(result.Value, CurrentAnchor, HeadingDegrees, Offset, _anchors));
        }

        private static float Normalize(float degrees)
        {
            var remainder = degrees % 360f;
            if (remainder < 0f) remainder += 360f;
            return remainder;
        }

        /// <summary>A canonical text of the whole state; equal states have equal fingerprints.</summary>
        public string Fingerprint()
        {
            var builder = new StringBuilder();
            builder.Append("policy[").Append(Policy).Append(']');
            builder.Append(" anchor[").Append(CurrentAnchor.HasValue ? CurrentAnchor.Value.Value : "-").Append(']');
            builder.Append(" heading[").Append(HeadingDegrees.ToString("R", CultureInfo.InvariantCulture)).Append(']');
            builder.Append(" offset[").Append(Offset).Append(']');
            builder.Append(" anchors[").Append(string.Join(",", _anchors)).Append(']');
            return builder.ToString();
        }

        public bool Equals(LocomotionState other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Fingerprint(), other.Fingerprint(), StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is LocomotionState other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Fingerprint());
        public override string ToString() => Fingerprint();
    }
}
