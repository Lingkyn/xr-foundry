using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Lingkyn.Haptics.Core
{
    // The closed HapticTarget set (left, right, both, named_device) and per-controller/per-hand
    // HapticProfiles built by explicit registration over that set. A named_device's device id
    // enters and leaves the Core as an opaque string; the Core never interprets it.

    /// <summary>The closed set of haptic targets a profile or a play/stop intent may name.</summary>
    public enum HapticTargetKind
    {
        Left,
        Right,
        Both,
        NamedDevice,
    }

    /// <summary>One target: one of the fixed <see cref="Left"/>/<see cref="Right"/>/<see cref="Both"/>
    /// members, or a <see cref="NamedDevice"/> carrying an opaque device-id string.</summary>
    public readonly struct HapticTarget : IEquatable<HapticTarget>, IComparable<HapticTarget>
    {
        private HapticTarget(HapticTargetKind kind, string deviceId)
        {
            Kind = kind;
            DeviceId = deviceId ?? string.Empty;
        }

        public HapticTargetKind Kind { get; }

        /// <summary>Meaningful only when <see cref="Kind"/> is <see cref="HapticTargetKind.NamedDevice"/>;
        /// an opaque string the Core never interprets.</summary>
        public string DeviceId { get; }

        public static readonly HapticTarget Left = new HapticTarget(HapticTargetKind.Left, string.Empty);
        public static readonly HapticTarget Right = new HapticTarget(HapticTargetKind.Right, string.Empty);
        public static readonly HapticTarget Both = new HapticTarget(HapticTargetKind.Both, string.Empty);

        public static HapticTarget NamedDevice(string deviceId) => new HapticTarget(HapticTargetKind.NamedDevice, deviceId ?? string.Empty);

        public bool Equals(HapticTarget other) => Kind == other.Kind && string.Equals(DeviceId, other.DeviceId, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is HapticTarget other && Equals(other);
        public override int GetHashCode() => ((int)Kind * 397) ^ StringComparer.Ordinal.GetHashCode(DeviceId);

        public int CompareTo(HapticTarget other)
        {
            var kindCompare = ((int)Kind).CompareTo((int)other.Kind);
            return kindCompare != 0 ? kindCompare : string.CompareOrdinal(DeviceId, other.DeviceId);
        }

        public override string ToString() => Kind == HapticTargetKind.NamedDevice ? $"named_device:{DeviceId}" : Kind.ToString().ToLowerInvariant();

        public static bool operator ==(HapticTarget left, HapticTarget right) => left.Equals(right);
        public static bool operator !=(HapticTarget left, HapticTarget right) => !left.Equals(right);
    }

    /// <summary>The immutable, explicitly-registered per-target scale and enable flag for one
    /// haptic profile. A target this profile does not list resolves to the documented default
    /// (scale <c>1.0</c>, enabled).</summary>
    public sealed class HapticProfile
    {
        /// <summary>The documented positive closed range a target's scale must lie within.</summary>
        public const float MinimumScale = 0.01f;
        public const float MaximumScale = 4f;

        /// <summary>The scale and enable flag every unlisted target resolves to.</summary>
        public static readonly (float Scale, bool Enabled) DefaultEntry = (1f, true);

        private readonly SortedDictionary<HapticTarget, (float Scale, bool Enabled)> _entries;

        private HapticProfile(HapticProfileId id, SortedDictionary<HapticTarget, (float Scale, bool Enabled)> entries)
        {
            Id = id;
            _entries = entries;
        }

        public HapticProfileId Id { get; }

        /// <summary>Every target this profile explicitly lists, in canonical target order.</summary>
        public IEnumerable<HapticTarget> Targets => _entries.Keys;

        public bool HasEntry(HapticTarget target) => _entries.ContainsKey(target);

        /// <summary>The scale and enable flag for a target: whatever was explicitly registered, or
        /// <see cref="DefaultEntry"/> for a target this profile does not list.</summary>
        public (float Scale, bool Enabled) Resolve(HapticTarget target) => _entries.TryGetValue(target, out var entry) ? entry : DefaultEntry;

        internal string Fingerprint()
        {
            var parts = _entries.Select(pair => $"{pair.Key}={pair.Value.Scale.ToString("R", CultureInfo.InvariantCulture)},{pair.Value.Enabled}");
            return $"{Id}:[{string.Join(",", parts)}]";
        }

        internal static HapticProfile FromEntries(HapticProfileId id, SortedDictionary<HapticTarget, (float Scale, bool Enabled)> entries) => new HapticProfile(id, entries);
    }

    /// <summary>Collects one profile's per-target entries one at a time; each call validates
    /// immediately and either extends the builder or leaves it unchanged and returns a stable
    /// failure code.</summary>
    public sealed class HapticProfileBuilder
    {
        private readonly HapticProfileId _id;
        private readonly SortedDictionary<HapticTarget, (float Scale, bool Enabled)> _entries = new SortedDictionary<HapticTarget, (float Scale, bool Enabled)>();

        public HapticProfileBuilder(HapticProfileId id)
        {
            _id = id;
        }

        public int Count => _entries.Count;

        /// <summary>Declares one target's scale and enable flag. Rejected with
        /// <see cref="HapticFailure.ProfileDeclarationInvalid"/> when the scale lies outside
        /// <see cref="HapticProfile.MinimumScale"/>..<see cref="HapticProfile.MaximumScale"/> or the
        /// target is already declared in this profile.</summary>
        public HapticResult<HapticProfileBuilder> Add(HapticTarget target, float scale, bool enabled)
        {
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale < HapticProfile.MinimumScale || scale > HapticProfile.MaximumScale)
            {
                return HapticResult<HapticProfileBuilder>.Fail(HapticFailure.ProfileDeclarationInvalid, $"Profile '{_id}': scale {scale} for target '{target}' lies outside [{HapticProfile.MinimumScale},{HapticProfile.MaximumScale}].");
            }
            if (_entries.ContainsKey(target))
            {
                return HapticResult<HapticProfileBuilder>.Fail(HapticFailure.ProfileDeclarationInvalid, $"Profile '{_id}': target '{target}' is already declared.");
            }
            _entries[target] = (scale, enabled);
            return HapticResult<HapticProfileBuilder>.Ok(this);
        }

        public HapticProfile Build() => HapticProfile.FromEntries(_id, new SortedDictionary<HapticTarget, (float Scale, bool Enabled)>(_entries));
    }

    /// <summary>The explicit, immutable set of named haptic profiles a <see cref="HapticState"/> is
    /// built over. Built by <see cref="Create"/>, which throws on a null profile or a duplicate
    /// profile id; the contract names no rejection code for this construction-time programmer
    /// error, so it is a plain exception rather than a <see cref="HapticResult{T}"/>.</summary>
    public sealed class HapticProfileSet
    {
        private readonly SortedDictionary<HapticProfileId, HapticProfile> _profiles;

        private HapticProfileSet(SortedDictionary<HapticProfileId, HapticProfile> profiles)
        {
            _profiles = profiles;
        }

        public int Count => _profiles.Count;
        public IEnumerable<HapticProfile> Profiles => _profiles.Values;
        public bool TryGet(HapticProfileId id, out HapticProfile profile) => _profiles.TryGetValue(id, out profile);

        public static HapticProfileSet Create(IEnumerable<HapticProfile> profiles)
        {
            if (profiles == null) throw new ArgumentNullException(nameof(profiles));
            var map = new SortedDictionary<HapticProfileId, HapticProfile>();
            foreach (var profile in profiles)
            {
                if (profile == null) throw new ArgumentException("A profile must not be null.", nameof(profiles));
                if (map.ContainsKey(profile.Id)) throw new ArgumentException($"Profile id '{profile.Id}' is declared more than once.", nameof(profiles));
                map[profile.Id] = profile;
            }
            return new HapticProfileSet(map);
        }

        internal string Fingerprint() => string.Join(";", _profiles.Values.Select(profile => profile.Fingerprint()));
    }
}
