using System;
using System.Collections.Generic;

namespace Lingkyn.XrUiShell.Core
{
    // The verb registry: a closed, explicitly registered set of verb ids and display words that
    // any client family registers itself into through this same entry point, exactly like a
    // panel or an input source (never a global event bus, service locator, or reflection scan
    // over loaded assemblies). VerbResolution.cs and the pre-press affordance in this same file
    // read a built VerbRegistry and a FocusSubject and nothing else. A verb id absent from the
    // registry is rejected with a stable code and never silently absorbed into another verb or
    // dropped; registered ids partition into exactly two states (wired or unwired); and a verb's
    // id and display word are fixed at registration and never vary afterwards, because what
    // varies from one press to the next is only the resolved target, never the verb's own word.

    /// <summary>Whether a registered verb id currently has a resolver backing it (its pre-press
    /// affordance can be available and a press can be routed) or is only reserved, declared but
    /// not yet backed by any client. Every registered id is exactly one of these two; there is no
    /// third state.</summary>
    public enum VerbWireState
    {
        Wired,
        Unwired,
    }

    /// <summary>Stable identity of one registered verb (for example "tune" or "grab"), sharing
    /// the surface and input-source identities' canonical form and <see
    /// cref="ShellFailure.IdentityMalformed"/> rejection code but never equal to a
    /// <see cref="PanelId"/>, <see cref="WristMenuId"/>, <see cref="HandMenuId"/>, or
    /// <see cref="InputSourceId"/> built from the same text.</summary>
    public readonly struct VerbId : IEquatable<VerbId>, IComparable<VerbId>
    {
        private VerbId(string value) { Value = value; }

        public string Value { get; }

        public static ShellResult<VerbId> TryCreate(string value)
        {
            var canonical = ShellIdentityText.TryCanonicalize(value, "verb");
            return canonical.Succeeded ? ShellResult<VerbId>.Ok(new VerbId(canonical.Value)) : canonical.As<VerbId>();
        }

        public static VerbId Parse(string value)
        {
            var result = TryCreate(value);
            if (!result.Succeeded) throw new ArgumentException(result.Message, nameof(value));
            return result.Value;
        }

        public bool Equals(VerbId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is VerbId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public int CompareTo(VerbId other) => string.CompareOrdinal(Value, other.Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(VerbId left, VerbId right) => left.Equals(right);
        public static bool operator !=(VerbId left, VerbId right) => !left.Equals(right);
    }

    /// <summary>One registered verb: its constant id, its constant display word, and its wire
    /// state. Immutable; nothing changes a registration's word or id after it is built.</summary>
    public sealed class VerbRegistration
    {
        internal VerbRegistration(VerbId id, string displayWord, VerbWireState wireState)
        {
            Id = id;
            DisplayWord = displayWord;
            WireState = wireState;
        }

        public VerbId Id { get; }
        public string DisplayWord { get; }
        public VerbWireState WireState { get; }
    }

    /// <summary>The immutable, closed set of registered verbs, enumerable in canonical id order.
    /// Built only by <see cref="VerbRegistryBuilder"/>; nothing mutates it afterwards.</summary>
    public sealed class VerbRegistry
    {
        private readonly SortedDictionary<VerbId, VerbRegistration> _verbs;

        private VerbRegistry(SortedDictionary<VerbId, VerbRegistration> verbs)
        {
            _verbs = verbs;
        }

        public int Count => _verbs.Count;

        /// <summary>Every registered verb, in canonical id order.</summary>
        public IEnumerable<VerbRegistration> Verbs => _verbs.Values;

        public bool TryGet(VerbId id, out VerbRegistration registration) => _verbs.TryGetValue(id, out registration);

        internal static VerbRegistry FromEntries(SortedDictionary<VerbId, VerbRegistration> verbs) => new VerbRegistry(verbs);
    }

    /// <summary>Collects verb registrations one at a time; each call validates immediately and
    /// either extends the builder or leaves it unchanged and returns a stable failure code.</summary>
    public sealed class VerbRegistryBuilder
    {
        private readonly SortedDictionary<VerbId, VerbRegistration> _verbs = new SortedDictionary<VerbId, VerbRegistration>();

        public int Count => _verbs.Count;
        public bool ContainsVerb(VerbId id) => _verbs.ContainsKey(id);

        /// <summary>Registers one verb with its constant display word and wire state. The word is
        /// a programmer-supplied constant, never empty; a caller that fails to supply one has a
        /// defect, so this throws rather than returning a result, exactly like the null guards on
        /// every other builder in this Core. Rejects a verb id already registered with
        /// <see cref="ShellFailure.VerbDuplicate"/>, leaving the builder unchanged.</summary>
        public ShellResult<VerbRegistryBuilder> Register(VerbId id, string displayWord, VerbWireState wireState)
        {
            if (string.IsNullOrWhiteSpace(displayWord))
            {
                throw new ArgumentException("A verb needs a non-empty, constant display word.", nameof(displayWord));
            }
            if (_verbs.ContainsKey(id))
            {
                return ShellResult<VerbRegistryBuilder>.Fail(ShellFailure.VerbDuplicate, $"Verb '{id}' is already registered.", id.ToString());
            }
            _verbs[id] = new VerbRegistration(id, displayWord, wireState);
            return ShellResult<VerbRegistryBuilder>.Ok(this);
        }

        public VerbRegistry Build() => VerbRegistry.FromEntries(new SortedDictionary<VerbId, VerbRegistration>(_verbs));
    }

    /// <summary>Looks a verb id up against a built registry. An id outside the closed registered
    /// set is rejected with <see cref="ShellFailure.VerbUnknown"/> and never silently absorbed
    /// into another verb or dropped.</summary>
    public static class VerbLookup
    {
        public static ShellResult<VerbRegistration> Resolve(VerbRegistry registry, VerbId id)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (!registry.TryGet(id, out var registration))
            {
                return ShellResult<VerbRegistration>.Fail(ShellFailure.VerbUnknown, $"Verb '{id}' is not registered.", id.ToString());
            }
            return ShellResult<VerbRegistration>.Ok(registration);
        }
    }
}
