using System;

namespace Lingkyn.Interaction.Core
{
    public readonly struct InteractionVector2 : IEquatable<InteractionVector2>
    {
        public InteractionVector2(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }
        public double Y { get; }

        public bool IsFinite() => IsFiniteNumber(X) && IsFiniteNumber(Y);

        public bool Equals(InteractionVector2 other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object obj) => obj is InteractionVector2 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);

        internal static bool IsFiniteNumber(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }

    public readonly struct InteractionVector3 : IEquatable<InteractionVector3>
    {
        public InteractionVector3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double X { get; }
        public double Y { get; }
        public double Z { get; }

        public bool IsFinite() =>
            InteractionVector2.IsFiniteNumber(X)
            && InteractionVector2.IsFiniteNumber(Y)
            && InteractionVector2.IsFiniteNumber(Z);

        public bool Equals(InteractionVector3 other) =>
            X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);

        public override bool Equals(object obj) => obj is InteractionVector3 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y, Z);
    }

    public readonly struct InteractionQuaternion : IEquatable<InteractionQuaternion>
    {
        public InteractionQuaternion(double x, double y, double z, double w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public double X { get; }
        public double Y { get; }
        public double Z { get; }
        public double W { get; }

        public bool IsFinite() =>
            InteractionVector2.IsFiniteNumber(X)
            && InteractionVector2.IsFiniteNumber(Y)
            && InteractionVector2.IsFiniteNumber(Z)
            && InteractionVector2.IsFiniteNumber(W);

        public bool Equals(InteractionQuaternion other) =>
            X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z) && W.Equals(other.W);

        public override bool Equals(object obj) => obj is InteractionQuaternion other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y, Z, W);
    }

    public readonly struct InteractionPose : IEquatable<InteractionPose>
    {
        public InteractionPose(
            InteractionVector3 position,
            InteractionQuaternion rotation,
            bool positionValid,
            bool rotationValid)
        {
            Position = position;
            Rotation = rotation;
            PositionValid = positionValid;
            RotationValid = rotationValid;
        }

        public InteractionVector3 Position { get; }
        public InteractionQuaternion Rotation { get; }
        public bool PositionValid { get; }
        public bool RotationValid { get; }

        public bool IsFinite()
        {
            if (PositionValid && !Position.IsFinite())
            {
                return false;
            }

            if (RotationValid && !Rotation.IsFinite())
            {
                return false;
            }

            return PositionValid || RotationValid;
        }

        public bool Equals(InteractionPose other) =>
            Position.Equals(other.Position)
            && Rotation.Equals(other.Rotation)
            && PositionValid == other.PositionValid
            && RotationValid == other.RotationValid;

        public override bool Equals(object obj) => obj is InteractionPose other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Position, Rotation, PositionValid, RotationValid);
    }

    public readonly struct InteractionValue : IEquatable<InteractionValue>
    {
        private InteractionValue(
            InteractionValueKind kind,
            bool button,
            double scalar,
            InteractionVector2 vector2,
            InteractionVector3 vector3,
            InteractionPose pose,
            string text)
        {
            Kind = kind;
            Button = button;
            Scalar = scalar;
            Vector2 = vector2;
            Vector3 = vector3;
            Pose = pose;
            Text = text ?? string.Empty;
        }

        public InteractionValueKind Kind { get; }
        public bool Button { get; }
        public double Scalar { get; }
        public InteractionVector2 Vector2 { get; }
        public InteractionVector3 Vector3 { get; }
        public InteractionPose Pose { get; }
        public string Text { get; }

        public static InteractionValue FromButton(bool pressed) =>
            new InteractionValue(InteractionValueKind.Button, pressed, 0, default, default, default, string.Empty);

        public static InteractionValue FromScalar(double value) =>
            new InteractionValue(InteractionValueKind.Scalar, false, value, default, default, default, string.Empty);

        public static InteractionValue FromVector2(InteractionVector2 value) =>
            new InteractionValue(InteractionValueKind.Vector2, false, 0, value, default, default, string.Empty);

        public static InteractionValue FromVector3(InteractionVector3 value) =>
            new InteractionValue(InteractionValueKind.Vector3, false, 0, default, value, default, string.Empty);

        public static InteractionValue FromPose(InteractionPose value) =>
            new InteractionValue(InteractionValueKind.Pose, false, 0, default, default, value, string.Empty);

        public static InteractionValue FromText(string value) =>
            new InteractionValue(InteractionValueKind.Text, false, 0, default, default, default, value ?? string.Empty);

        public static InteractionResult<InteractionValue> Validate(InteractionValueKind expectedKind, InteractionValue value)
        {
            if (!Enum.IsDefined(typeof(InteractionValueKind), expectedKind)
                || !Enum.IsDefined(typeof(InteractionValueKind), value.Kind))
            {
                return InteractionResult<InteractionValue>.Fail(
                    InteractionValidationCode.KindMismatch,
                    "Value kind must be defined.");
            }
            if (value.Kind != expectedKind)
            {
                return InteractionResult<InteractionValue>.Fail(
                    InteractionValidationCode.KindMismatch,
                    $"Expected value kind '{expectedKind}' but received '{value.Kind}'.");
            }

            switch (value.Kind)
            {
                case InteractionValueKind.Scalar:
                    if (!InteractionVector2.IsFiniteNumber(value.Scalar))
                    {
                        return InteractionResult<InteractionValue>.Fail(
                            InteractionValidationCode.NonFiniteValue,
                            "Scalar value must be finite.");
                    }

                    break;
                case InteractionValueKind.Vector2:
                    if (!value.Vector2.IsFinite())
                    {
                        return InteractionResult<InteractionValue>.Fail(
                            InteractionValidationCode.NonFiniteValue,
                            "Vector2 value must be finite.");
                    }

                    break;
                case InteractionValueKind.Vector3:
                    if (!value.Vector3.IsFinite())
                    {
                        return InteractionResult<InteractionValue>.Fail(
                            InteractionValidationCode.NonFiniteValue,
                            "Vector3 value must be finite.");
                    }

                    break;
                case InteractionValueKind.Pose:
                    if (!value.Pose.IsFinite())
                    {
                        return InteractionResult<InteractionValue>.Fail(
                            InteractionValidationCode.InvalidPose,
                            "Pose value must contain valid finite components.");
                    }

                    break;
                case InteractionValueKind.Text:
                    if (value.Text == null)
                    {
                        return InteractionResult<InteractionValue>.Fail(
                            InteractionValidationCode.InvalidDefinition,
                            "Text value must not be null.");
                    }

                    break;
            }

            return InteractionResult<InteractionValue>.Success(value);
        }

        public static InteractionCapability RequiredCapabilitiesForKind(InteractionValueKind kind)
        {
            switch (kind)
            {
                case InteractionValueKind.Button:
                    return InteractionCapability.Digital;
                case InteractionValueKind.Scalar:
                    return InteractionCapability.Scalar;
                case InteractionValueKind.Vector2:
                    return InteractionCapability.Vector2;
                case InteractionValueKind.Vector3:
                    return InteractionCapability.Vector3;
                case InteractionValueKind.Pose:
                    return InteractionCapability.Pose;
                case InteractionValueKind.Text:
                    return InteractionCapability.Text;
                default:
                    return InteractionCapability.None;
            }
        }

        public bool Equals(InteractionValue other) =>
            Kind == other.Kind
            && Button == other.Button
            && Scalar.Equals(other.Scalar)
            && Vector2.Equals(other.Vector2)
            && Vector3.Equals(other.Vector3)
            && Pose.Equals(other.Pose)
            && string.Equals(Text, other.Text, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is InteractionValue other && Equals(other);

        public override int GetHashCode()
        {
            var text = Text ?? string.Empty;
            return HashCode.Combine(
                Kind,
                Button,
                Scalar,
                Vector2,
                Vector3,
                Pose,
                StringComparer.Ordinal.GetHashCode(text));
        }
    }
}
