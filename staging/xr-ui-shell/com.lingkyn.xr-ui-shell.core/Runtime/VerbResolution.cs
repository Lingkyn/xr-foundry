using System;

namespace Lingkyn.XrUiShell.Core
{
    // Verb resolution and its pre-press affordance: both read exactly one value, the current
    // FocusSubject, plus the closed VerbRegistry a verb id is checked against. Neither reads
    // ShellState.FocusedSurface, a routing result, a candidate set, or any other source, and
    // neither branches over more than one input in a chosen order: there is one registry lookup
    // and one FocusSubject read, always in the same two steps, so two different verbs resolved
    // against the same FocusSubject value always agree on whether a target exists and which one
    // it is. VerbAffordanceQuery answers the same two checks before any press, so a verb is never
    // shown available for a target VerbResolver.Resolve would in fact refuse: an unregistered or
    // unwired verb, or a subject with no current target, is unavailable/rejected for exactly the
    // same reason on both paths.

    public static class VerbResolver
    {
        /// <summary>Resolves <paramref name="verbId"/> against <paramref name="subject"/>: an
        /// unregistered verb is rejected with <see cref="ShellFailure.VerbUnknown"/>, a registered
        /// but unwired verb with <see cref="ShellFailure.VerbUnwired"/>, and a wired verb with no
        /// current focus target with <see cref="ShellFailure.VerbNoTarget"/> (the named
        /// no-target result). Otherwise the current <see cref="FocusTarget"/> is the exactly-one
        /// resolved target.</summary>
        public static ShellResult<FocusTarget> Resolve(VerbRegistry registry, VerbId verbId, FocusSubject subject)
        {
            if (subject == null) throw new ArgumentNullException(nameof(subject));

            var lookup = VerbLookup.Resolve(registry, verbId);
            if (!lookup.Succeeded) return lookup.As<FocusTarget>();

            if (lookup.Value.WireState != VerbWireState.Wired)
            {
                return ShellResult<FocusTarget>.Fail(ShellFailure.VerbUnwired, $"Verb '{verbId}' is registered but not yet wired to a resolver.", verbId.ToString());
            }
            if (!subject.HasTarget)
            {
                return ShellResult<FocusTarget>.Fail(ShellFailure.VerbNoTarget, $"Verb '{verbId}' has no current focus target to resolve against.", verbId.ToString());
            }
            return ShellResult<FocusTarget>.Ok(subject.Current);
        }
    }

    /// <summary>Whether a verb's pre-press affordance is currently available.</summary>
    public enum VerbAffordanceAvailability
    {
        Available,
        Unavailable,
    }

    /// <summary>The pre-press affordance for one verb against the current FocusSubject: whether
    /// pressing would be accepted, the resolved target's name when one exists, and a stable
    /// reason code when it would be refused. <see cref="ReasonCode"/> is empty exactly when
    /// <see cref="Availability"/> is <see cref="VerbAffordanceAvailability.Available"/>.</summary>
    public sealed class VerbAffordance
    {
        internal VerbAffordance(VerbId verbId, VerbAffordanceAvailability availability, string targetName, string reasonCode)
        {
            VerbId = verbId;
            Availability = availability;
            TargetName = targetName ?? string.Empty;
            ReasonCode = reasonCode ?? string.Empty;
        }

        public VerbId VerbId { get; }
        public VerbAffordanceAvailability Availability { get; }
        public string TargetName { get; }
        public string ReasonCode { get; }
        public bool IsAvailable => Availability == VerbAffordanceAvailability.Available;
    }

    /// <summary>Queries a verb's pre-press affordance ahead of any press, through exactly the
    /// same two reads (the registry, then the subject) <see cref="VerbResolver.Resolve"/> uses,
    /// so a client can decide whether to show a verb as available without ever pressing it, and
    /// never shows it available for a target the resolver would refuse.</summary>
    public static class VerbAffordanceQuery
    {
        public static VerbAffordance Query(VerbRegistry registry, VerbId verbId, FocusSubject subject)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (subject == null) throw new ArgumentNullException(nameof(subject));

            if (!registry.TryGet(verbId, out var registration))
            {
                return new VerbAffordance(verbId, VerbAffordanceAvailability.Unavailable, string.Empty, ShellFailure.VerbUnknown);
            }
            if (registration.WireState != VerbWireState.Wired)
            {
                return new VerbAffordance(verbId, VerbAffordanceAvailability.Unavailable, string.Empty, ShellFailure.VerbUnwired);
            }
            if (!subject.HasTarget)
            {
                return new VerbAffordance(verbId, VerbAffordanceAvailability.Unavailable, string.Empty, ShellFailure.VerbNoTarget);
            }
            return new VerbAffordance(verbId, VerbAffordanceAvailability.Available, subject.Current.ToString(), string.Empty);
        }
    }
}
