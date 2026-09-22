using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Lingkyn.LiveTuning.Core
{
    // The closed set of tunable kinds, the closed set of editor kinds resolved one-per-kind
    // and never by location, the typed value that carries one kind's payload, and the closed
    // set of per-kind declarations that validate a range, a step, or a value set.

    /// <summary>The closed set of tunable kinds. Adding a member here is a breaking change that
    /// also requires a new <see cref="EditorKind"/> and a new <see cref="ResolveEditorKind"/> case;
    /// until then, an adapter that resolves this kind fails closed with tunable.kind.unsupported.</summary>
    public enum TunableKind
    {
        Float,
        Integer,
        Bool,
        Enumerated,
        Colour,
        Vector2,
        Vector3,
    }

    /// <summary>The closed set of editor kinds, exactly one member per <see cref="TunableKind"/>.
    /// Resolved by kind only, never by the package, skin, or screen that owns the target.</summary>
    public enum EditorKind
    {
        FloatEditor,
        IntegerEditor,
        BoolEditor,
        EnumeratedEditor,
        ColourEditor,
        Vector2Editor,
        Vector3Editor,
    }

    /// <summary>Resolves the one editor kind for a tunable kind. A total, pure function over the
    /// closed set: any kind value outside the seven declared members (a defect in an adapter, or a
    /// future kind added without an editor) fails closed with tunable.kind.unsupported and never
    /// resolves to an empty or default editor.</summary>
    public static class EditorKindResolver
    {
        public static LiveTuningResult<EditorKind> ResolveEditorKind(TunableKind kind)
        {
            switch (kind)
            {
                case TunableKind.Float: return LiveTuningResult<EditorKind>.Ok(EditorKind.FloatEditor);
                case TunableKind.Integer: return LiveTuningResult<EditorKind>.Ok(EditorKind.IntegerEditor);
                case TunableKind.Bool: return LiveTuningResult<EditorKind>.Ok(EditorKind.BoolEditor);
                case TunableKind.Enumerated: return LiveTuningResult<EditorKind>.Ok(EditorKind.EnumeratedEditor);
                case TunableKind.Colour: return LiveTuningResult<EditorKind>.Ok(EditorKind.ColourEditor);
                case TunableKind.Vector2: return LiveTuningResult<EditorKind>.Ok(EditorKind.Vector2Editor);
                case TunableKind.Vector3: return LiveTuningResult<EditorKind>.Ok(EditorKind.Vector3Editor);
                default:
                    return LiveTuningResult<EditorKind>.Fail(LiveTuningFailure.TunableKindUnsupported, $"No editor kind is registered for tunable kind '{kind}' ({(int)kind}); it is outside the closed set.");
            }
        }
    }

    /// <summary>A typed value for exactly one <see cref="TunableKind"/>. Only the fields matching
    /// <see cref="Kind"/> are meaningful; equality and canonical text are both kind-scoped.</summary>
    public readonly struct TunableValue : IEquatable<TunableValue>
    {
        private readonly float _x;
        private readonly float _y;
        private readonly float _z;
        private readonly float _w;
        private readonly int _i;
        private readonly bool _b;
        private readonly string _s;

        private TunableValue(TunableKind kind, float x, float y, float z, float w, int i, bool b, string s)
        {
            Kind = kind;
            _x = x; _y = y; _z = z; _w = w; _i = i; _b = b; _s = s;
        }

        public TunableKind Kind { get; }

        public static TunableValue OfFloat(float value) => new TunableValue(TunableKind.Float, value, 0, 0, 0, 0, false, null);
        public static TunableValue OfInteger(int value) => new TunableValue(TunableKind.Integer, 0, 0, 0, 0, value, false, null);
        public static TunableValue OfBool(bool value) => new TunableValue(TunableKind.Bool, 0, 0, 0, 0, 0, value, null);
        public static TunableValue OfEnumerated(string value) => new TunableValue(TunableKind.Enumerated, 0, 0, 0, 0, 0, false, value ?? string.Empty);
        public static TunableValue OfColour(float r, float g, float b, float a) => new TunableValue(TunableKind.Colour, r, g, b, a, 0, false, null);
        public static TunableValue OfVector2(float x, float y) => new TunableValue(TunableKind.Vector2, x, y, 0, 0, 0, false, null);
        public static TunableValue OfVector3(float x, float y, float z) => new TunableValue(TunableKind.Vector3, x, y, z, 0, 0, false, null);

        public float AsFloat => _x;
        public int AsInteger => _i;
        public bool AsBool => _b;
        public string AsEnumerated => _s ?? string.Empty;
        public (float R, float G, float B, float A) AsColour => (_x, _y, _z, _w);
        public (float X, float Y) AsVector2 => (_x, _y);
        public (float X, float Y, float Z) AsVector3 => (_x, _y, _z);

        public bool Equals(TunableValue other)
        {
            if (Kind != other.Kind) return false;
            switch (Kind)
            {
                case TunableKind.Float: return _x.Equals(other._x);
                case TunableKind.Integer: return _i == other._i;
                case TunableKind.Bool: return _b == other._b;
                case TunableKind.Enumerated: return string.Equals(AsEnumerated, other.AsEnumerated, StringComparison.Ordinal);
                case TunableKind.Colour: return _x.Equals(other._x) && _y.Equals(other._y) && _z.Equals(other._z) && _w.Equals(other._w);
                case TunableKind.Vector2: return _x.Equals(other._x) && _y.Equals(other._y);
                case TunableKind.Vector3: return _x.Equals(other._x) && _y.Equals(other._y) && _z.Equals(other._z);
                default: return false;
            }
        }

        public override bool Equals(object obj) => obj is TunableValue other && Equals(other);

        public override int GetHashCode()
        {
            switch (Kind)
            {
                case TunableKind.Float: return _x.GetHashCode();
                case TunableKind.Integer: return _i.GetHashCode();
                case TunableKind.Bool: return _b.GetHashCode();
                case TunableKind.Enumerated: return AsEnumerated.GetHashCode();
                case TunableKind.Colour: return (_x.GetHashCode() * 397) ^ (_y.GetHashCode() * 31) ^ (_z.GetHashCode() * 17) ^ _w.GetHashCode();
                case TunableKind.Vector2: return (_x.GetHashCode() * 397) ^ _y.GetHashCode();
                case TunableKind.Vector3: return (_x.GetHashCode() * 397) ^ (_y.GetHashCode() * 31) ^ _z.GetHashCode();
                default: return 0;
            }
        }

        /// <summary>The canonical text this kind's value round-trips through: an override document,
        /// a fingerprint, and <see cref="TryParseCanonical"/>.</summary>
        public string ToCanonicalString()
        {
            switch (Kind)
            {
                case TunableKind.Float: return Number(_x);
                case TunableKind.Integer: return _i.ToString(CultureInfo.InvariantCulture);
                case TunableKind.Bool: return _b ? "true" : "false";
                case TunableKind.Enumerated: return AsEnumerated;
                case TunableKind.Colour: return $"{Number(_x)},{Number(_y)},{Number(_z)},{Number(_w)}";
                case TunableKind.Vector2: return $"{Number(_x)},{Number(_y)}";
                case TunableKind.Vector3: return $"{Number(_x)},{Number(_y)},{Number(_z)}";
                default: return string.Empty;
            }
        }

        public static bool TryParseCanonical(TunableKind kind, string text, out TunableValue value)
        {
            value = default;
            if (text == null) return false;
            switch (kind)
            {
                case TunableKind.Float:
                    if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var f)) return false;
                    value = OfFloat(f);
                    return true;
                case TunableKind.Integer:
                    if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) return false;
                    value = OfInteger(i);
                    return true;
                case TunableKind.Bool:
                    if (text == "true") { value = OfBool(true); return true; }
                    if (text == "false") { value = OfBool(false); return true; }
                    return false;
                case TunableKind.Enumerated:
                    value = OfEnumerated(text);
                    return true;
                case TunableKind.Colour:
                {
                    var parts = text.Split(',');
                    if (parts.Length != 4) return false;
                    if (!TryParseFloats(parts, out var channels)) return false;
                    value = OfColour(channels[0], channels[1], channels[2], channels[3]);
                    return true;
                }
                case TunableKind.Vector2:
                {
                    var parts = text.Split(',');
                    if (parts.Length != 2) return false;
                    if (!TryParseFloats(parts, out var axes)) return false;
                    value = OfVector2(axes[0], axes[1]);
                    return true;
                }
                case TunableKind.Vector3:
                {
                    var parts = text.Split(',');
                    if (parts.Length != 3) return false;
                    if (!TryParseFloats(parts, out var axes)) return false;
                    value = OfVector3(axes[0], axes[1], axes[2]);
                    return true;
                }
                default:
                    return false;
            }
        }

        private static bool TryParseFloats(string[] parts, out float[] values)
        {
            values = new float[parts.Length];
            for (var index = 0; index < parts.Length; index++)
            {
                if (!float.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)) return false;
                values[index] = parsed;
            }
            return true;
        }

        private static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);
    }

    /// <summary>One member of the closed set of per-kind declarations: a range, a step, or a value
    /// set. A declaration is invalid (kind.declaration.invalid) with an inverted range, a
    /// non-positive step, a non-finite bound, or an empty value set.</summary>
    public abstract class TunableKindDeclaration
    {
        public abstract TunableKind Kind { get; }

        internal abstract bool IsValid(out string message);

        internal abstract bool Contains(TunableValue value);

        internal abstract string Summarize();
    }

    public sealed class FloatDeclaration : TunableKindDeclaration
    {
        public FloatDeclaration(float min, float max, float step) { Min = min; Max = max; Step = step; }

        public float Min { get; }
        public float Max { get; }
        public float Step { get; }

        public override TunableKind Kind => TunableKind.Float;

        internal override bool IsValid(out string message)
        {
            if (!IsFinite(Min) || !IsFinite(Max) || !IsFinite(Step))
            {
                message = $"A float declaration's bounds and step must be finite; got min={Min}, max={Max}, step={Step}.";
                return false;
            }
            if (Min > Max)
            {
                message = $"A float declaration's range is inverted: min ({Min}) is greater than max ({Max}).";
                return false;
            }
            if (Step <= 0f)
            {
                message = $"A float declaration's step must be positive; got {Step}.";
                return false;
            }
            message = string.Empty;
            return true;
        }

        internal override bool Contains(TunableValue value) => value.Kind == TunableKind.Float && value.AsFloat >= Min && value.AsFloat <= Max;

        internal override string Summarize() => $"float[{Min.ToString("R", CultureInfo.InvariantCulture)},{Max.ToString("R", CultureInfo.InvariantCulture)},{Step.ToString("R", CultureInfo.InvariantCulture)}]";

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public sealed class IntegerDeclaration : TunableKindDeclaration
    {
        public IntegerDeclaration(int min, int max) { Min = min; Max = max; }

        public int Min { get; }
        public int Max { get; }

        public override TunableKind Kind => TunableKind.Integer;

        internal override bool IsValid(out string message)
        {
            if (Min > Max)
            {
                message = $"An integer declaration's range is inverted: min ({Min}) is greater than max ({Max}).";
                return false;
            }
            message = string.Empty;
            return true;
        }

        internal override bool Contains(TunableValue value) => value.Kind == TunableKind.Integer && value.AsInteger >= Min && value.AsInteger <= Max;

        internal override string Summarize() => $"integer[{Min},{Max}]";
    }

    public sealed class BoolDeclaration : TunableKindDeclaration
    {
        public static readonly BoolDeclaration Instance = new BoolDeclaration();

        public override TunableKind Kind => TunableKind.Bool;

        internal override bool IsValid(out string message) { message = string.Empty; return true; }

        internal override bool Contains(TunableValue value) => value.Kind == TunableKind.Bool;

        internal override string Summarize() => "bool[]";
    }

    public sealed class EnumeratedDeclaration : TunableKindDeclaration
    {
        private readonly string[] _values;

        public EnumeratedDeclaration(IEnumerable<string> values)
        {
            _values = (values ?? Array.Empty<string>()).ToArray();
        }

        public IReadOnlyList<string> Values => _values;

        public override TunableKind Kind => TunableKind.Enumerated;

        internal override bool IsValid(out string message)
        {
            if (_values.Length == 0)
            {
                message = "An enumerated declaration's value set must not be empty.";
                return false;
            }
            message = string.Empty;
            return true;
        }

        internal override bool Contains(TunableValue value) => value.Kind == TunableKind.Enumerated && _values.Contains(value.AsEnumerated, StringComparer.Ordinal);

        internal override string Summarize() => "enumerated[" + string.Join(",", _values) + "]";
    }

    public sealed class ColourDeclaration : TunableKindDeclaration
    {
        /// <summary>Every colour tunable shares this one declaration: four unit-float channels.
        /// There is no per-tunable range to configure, so this can never be kind.declaration.invalid.</summary>
        public static readonly ColourDeclaration Instance = new ColourDeclaration();

        public override TunableKind Kind => TunableKind.Colour;

        internal override bool IsValid(out string message) { message = string.Empty; return true; }

        internal override bool Contains(TunableValue value)
        {
            if (value.Kind != TunableKind.Colour) return false;
            var (r, g, b, a) = value.AsColour;
            return InUnitRange(r) && InUnitRange(g) && InUnitRange(b) && InUnitRange(a);
        }

        internal override string Summarize() => "colour[0,1]";

        private static bool InUnitRange(float channel) => !float.IsNaN(channel) && channel >= 0f && channel <= 1f;
    }

    public sealed class Vector2Declaration : TunableKindDeclaration
    {
        public Vector2Declaration(float minX, float maxX, float minY, float maxY)
        {
            MinX = minX; MaxX = maxX; MinY = minY; MaxY = maxY;
        }

        public float MinX { get; }
        public float MaxX { get; }
        public float MinY { get; }
        public float MaxY { get; }

        public override TunableKind Kind => TunableKind.Vector2;

        internal override bool IsValid(out string message)
        {
            if (!AllFinite(MinX, MaxX, MinY, MaxY))
            {
                message = "A vector2 declaration's bounds must be finite.";
                return false;
            }
            if (MinX > MaxX || MinY > MaxY)
            {
                message = "A vector2 declaration's range is inverted on at least one axis.";
                return false;
            }
            message = string.Empty;
            return true;
        }

        internal override bool Contains(TunableValue value)
        {
            if (value.Kind != TunableKind.Vector2) return false;
            var (x, y) = value.AsVector2;
            return x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;
        }

        internal override string Summarize() =>
            $"vector2[{MinX.ToString("R", CultureInfo.InvariantCulture)},{MaxX.ToString("R", CultureInfo.InvariantCulture)},{MinY.ToString("R", CultureInfo.InvariantCulture)},{MaxY.ToString("R", CultureInfo.InvariantCulture)}]";

        private static bool AllFinite(params float[] values) => values.All(value => !float.IsNaN(value) && !float.IsInfinity(value));
    }

    public sealed class Vector3Declaration : TunableKindDeclaration
    {
        public Vector3Declaration(float minX, float maxX, float minY, float maxY, float minZ, float maxZ)
        {
            MinX = minX; MaxX = maxX; MinY = minY; MaxY = maxY; MinZ = minZ; MaxZ = maxZ;
        }

        public float MinX { get; }
        public float MaxX { get; }
        public float MinY { get; }
        public float MaxY { get; }
        public float MinZ { get; }
        public float MaxZ { get; }

        public override TunableKind Kind => TunableKind.Vector3;

        internal override bool IsValid(out string message)
        {
            if (!AllFinite(MinX, MaxX, MinY, MaxY, MinZ, MaxZ))
            {
                message = "A vector3 declaration's bounds must be finite.";
                return false;
            }
            if (MinX > MaxX || MinY > MaxY || MinZ > MaxZ)
            {
                message = "A vector3 declaration's range is inverted on at least one axis.";
                return false;
            }
            message = string.Empty;
            return true;
        }

        internal override bool Contains(TunableValue value)
        {
            if (value.Kind != TunableKind.Vector3) return false;
            var (x, y, z) = value.AsVector3;
            return x >= MinX && x <= MaxX && y >= MinY && y <= MaxY && z >= MinZ && z <= MaxZ;
        }

        internal override string Summarize() =>
            $"vector3[{MinX.ToString("R", CultureInfo.InvariantCulture)},{MaxX.ToString("R", CultureInfo.InvariantCulture)},{MinY.ToString("R", CultureInfo.InvariantCulture)},{MaxY.ToString("R", CultureInfo.InvariantCulture)},{MinZ.ToString("R", CultureInfo.InvariantCulture)},{MaxZ.ToString("R", CultureInfo.InvariantCulture)}]";

        private static bool AllFinite(params float[] values) => values.All(value => !float.IsNaN(value) && !float.IsInfinity(value));
    }
}
