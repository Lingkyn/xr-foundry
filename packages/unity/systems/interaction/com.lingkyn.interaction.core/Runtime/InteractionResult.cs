using System;
using System.Collections.Generic;

namespace Lingkyn.Interaction.Core
{
    public enum InteractionValidationCode
    {
        None = 0,
        InvalidIdentity,
        DuplicateIdentity,
        DefaultIdentity,
        InvalidDefinition,
        DuplicateDefinition,
        KindMismatch,
        CapabilityMismatch,
        NonFiniteValue,
        InvalidPose,
        InvalidPhaseTransition,
        DuplicatePhase,
        InactiveContext,
        ShadowedContext,
        AmbiguousContextCollision,
        UnknownRoute,
        UnknownIntent,
        UnknownContext,
        UnknownSource,
        DisabledRoute,
        InvalidIngressSequence,
        InvalidFrame,
        InvalidPolicy,
        InvalidBindingSuggestion,
        HandlerFailed,
    }

    public readonly struct InteractionError : IEquatable<InteractionError>
    {
        public InteractionError(InteractionValidationCode code, string message, string subject = null)
        {
            Code = code;
            Message = message ?? string.Empty;
            Subject = subject ?? string.Empty;
        }

        public InteractionValidationCode Code { get; }
        public string Message { get; }
        public string Subject { get; }

        public bool Equals(InteractionError other)
        {
            return Code == other.Code
                && string.Equals(Message, other.Message, StringComparison.Ordinal)
                && string.Equals(Subject, other.Subject, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is InteractionError other && Equals(other);

        public override int GetHashCode()
        {
            var message = Message ?? string.Empty;
            var subject = Subject ?? string.Empty;
            return ((int)Code * 397)
                ^ StringComparer.Ordinal.GetHashCode(message)
                ^ StringComparer.Ordinal.GetHashCode(subject);
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Subject)
                ? $"{Code}: {Message}"
                : $"{Code} [{Subject}]: {Message}";
        }
    }

    public readonly struct InteractionResult<T>
    {
        private InteractionResult(bool succeeded, T value, InteractionError error)
        {
            Succeeded = succeeded;
            Value = value;
            Error = error;
        }

        public bool Succeeded { get; }
        public T Value { get; }
        public InteractionError Error { get; }

        public static InteractionResult<T> Success(T value) => new InteractionResult<T>(true, value, default);

        public static InteractionResult<T> Fail(InteractionValidationCode code, string message, string subject = null)
        {
            return new InteractionResult<T>(false, default, new InteractionError(code, message, subject));
        }
    }

    public readonly struct InteractionResult
    {
        private InteractionResult(bool succeeded, InteractionError error)
        {
            Succeeded = succeeded;
            Error = error;
        }

        public bool Succeeded { get; }
        public InteractionError Error { get; }

        public static InteractionResult Success() => new InteractionResult(true, default);

        public static InteractionResult Fail(InteractionValidationCode code, string message, string subject = null)
        {
            return new InteractionResult(false, new InteractionError(code, message, subject));
        }
    }
}
