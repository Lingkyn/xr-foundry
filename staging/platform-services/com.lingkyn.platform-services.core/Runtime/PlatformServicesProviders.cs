using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lingkyn.PlatformServices.Core
{
    // The closed platform-service capability set, the ProviderDescriptor built by explicit
    // registration, and the immutable ProviderRegistry. No reflection, attribute scan, or
    // platform-detection probe builds any of these; every entry is explicit, caller-supplied data.

    /// <summary>The closed set of platform-service capabilities. Adding a member here is a breaking
    /// change; until then, a capability outside these five is not representable.</summary>
    public enum PlatformServiceCapability
    {
        Entitlement,
        Achievement,
        Leaderboard,
        CloudSave,
        Identity,
    }

    public static class PlatformServiceCapabilities
    {
        private static readonly IReadOnlyDictionary<string, PlatformServiceCapability> ByName =
            new Dictionary<string, PlatformServiceCapability>(StringComparer.Ordinal)
            {
                ["entitlement"] = PlatformServiceCapability.Entitlement,
                ["achievement"] = PlatformServiceCapability.Achievement,
                ["leaderboard"] = PlatformServiceCapability.Leaderboard,
                ["cloud_save"] = PlatformServiceCapability.CloudSave,
                ["identity"] = PlatformServiceCapability.Identity,
            };

        /// <summary>The canonical string name for a capability, the same text
        /// <see cref="TryParse"/> accepts back.</summary>
        public static string Name(PlatformServiceCapability capability)
        {
            foreach (var pair in ByName)
            {
                if (pair.Value == capability) return pair.Key;
            }
            throw new ArgumentOutOfRangeException(nameof(capability), capability, "Not a member of the closed capability set.");
        }

        /// <summary>Parses a capability name from external, string-shaped data (for example a
        /// Unity-authored provider configuration asset). A name outside the closed set of five is
        /// rejected with <see cref="PlatformServicesFailure.CapabilityUnknown"/>.</summary>
        public static PlatformServicesResult<PlatformServiceCapability> TryParse(string name)
        {
            if (name != null && ByName.TryGetValue(name, out var capability))
            {
                return PlatformServicesResult<PlatformServiceCapability>.Ok(capability);
            }
            return PlatformServicesResult<PlatformServiceCapability>.Fail(
                PlatformServicesFailure.CapabilityUnknown,
                $"'{name ?? "<null>"}' is not one of the closed platform-service capabilities (entitlement, achievement, leaderboard, cloud_save, identity).");
        }
    }

    /// <summary>One provider's declared capability subset and, only for <see cref="PlatformServiceCapability.CloudSave"/>,
    /// its positive payload-size guard rail in bytes. Built only through <see cref="TryCreate"/>,
    /// which fails closed with <see cref="PlatformServicesFailure.ProviderDeclarationInvalid"/> for an
    /// empty capability subset, a cloud_save guard rail that is zero, negative, or missing while
    /// cloud_save is declared, or a guard rail present while cloud_save is not declared.</summary>
    public sealed class ProviderDescriptor
    {
        private readonly SortedSet<PlatformServiceCapability> _capabilities;

        private ProviderDescriptor(ProviderId id, SortedSet<PlatformServiceCapability> capabilities, int? cloudSaveGuardRailBytes)
        {
            Id = id;
            _capabilities = capabilities;
            CloudSaveGuardRailBytes = cloudSaveGuardRailBytes;
        }

        public ProviderId Id { get; }

        /// <summary>The closed subset of capabilities this provider supports, in canonical
        /// (declaration) order.</summary>
        public IReadOnlyCollection<PlatformServiceCapability> Capabilities => _capabilities;

        /// <summary>Meaningful only when <see cref="Capabilities"/> declares
        /// <see cref="PlatformServiceCapability.CloudSave"/>; null otherwise.</summary>
        public int? CloudSaveGuardRailBytes { get; }

        public bool Supports(PlatformServiceCapability capability) => _capabilities.Contains(capability);

        public static PlatformServicesResult<ProviderDescriptor> TryCreate(
            ProviderId id,
            IEnumerable<PlatformServiceCapability> capabilities,
            int? cloudSaveGuardRailBytes)
        {
            if (capabilities == null) throw new ArgumentNullException(nameof(capabilities));
            var set = new SortedSet<PlatformServiceCapability>(capabilities);
            if (set.Count == 0)
            {
                return PlatformServicesResult<ProviderDescriptor>.Fail(
                    PlatformServicesFailure.ProviderDeclarationInvalid,
                    $"Provider '{id}': a declared capability subset must not be empty.");
            }
            var declaresCloudSave = set.Contains(PlatformServiceCapability.CloudSave);
            if (declaresCloudSave && (!cloudSaveGuardRailBytes.HasValue || cloudSaveGuardRailBytes.Value <= 0))
            {
                return PlatformServicesResult<ProviderDescriptor>.Fail(
                    PlatformServicesFailure.ProviderDeclarationInvalid,
                    $"Provider '{id}': cloud_save requires a positive payload-size guard rail in bytes; got {cloudSaveGuardRailBytes?.ToString() ?? "none"}.");
            }
            if (!declaresCloudSave && cloudSaveGuardRailBytes.HasValue)
            {
                return PlatformServicesResult<ProviderDescriptor>.Fail(
                    PlatformServicesFailure.ProviderDeclarationInvalid,
                    $"Provider '{id}': a payload-size guard rail was supplied but cloud_save is not declared.");
            }
            return PlatformServicesResult<ProviderDescriptor>.Ok(new ProviderDescriptor(id, set, cloudSaveGuardRailBytes));
        }

        internal string Fingerprint() =>
            $"{Id}:[{string.Join(",", _capabilities.Select(PlatformServiceCapabilities.Name))}]:{(CloudSaveGuardRailBytes?.ToString() ?? "-")}";
    }

    /// <summary>The immutable, explicitly-registered set of provider descriptors a
    /// <see cref="PlatformServicesState"/> is composed over.</summary>
    public sealed class ProviderRegistry
    {
        private readonly SortedDictionary<ProviderId, ProviderDescriptor> _providers;
        private readonly string _fingerprint;

        private ProviderRegistry(SortedDictionary<ProviderId, ProviderDescriptor> providers)
        {
            _providers = providers;
            var builder = new StringBuilder();
            foreach (var descriptor in _providers.Values)
            {
                builder.Append(descriptor.Fingerprint()).Append(';');
            }
            _fingerprint = builder.ToString();
        }

        public int Count => _providers.Count;

        /// <summary>Every registered provider, in canonical id order.</summary>
        public IEnumerable<ProviderDescriptor> Providers => _providers.Values;

        public bool TryGet(ProviderId id, out ProviderDescriptor descriptor) => _providers.TryGetValue(id, out descriptor);

        /// <summary>A canonical text covering every registration in canonical id order; equal
        /// registries (built from equal registrations, in any registration order) have equal
        /// fingerprints.</summary>
        public string Fingerprint() => _fingerprint;

        internal static ProviderRegistry FromEntries(SortedDictionary<ProviderId, ProviderDescriptor> entries) => new ProviderRegistry(entries);
    }

    /// <summary>Collects provider registrations one at a time; each call validates immediately and
    /// either extends the builder or leaves it unchanged and returns a stable failure code.</summary>
    public sealed class ProviderRegistryBuilder
    {
        private readonly SortedDictionary<ProviderId, ProviderDescriptor> _entries = new SortedDictionary<ProviderId, ProviderDescriptor>();

        public int Count => _entries.Count;

        public bool Contains(ProviderId id) => _entries.ContainsKey(id);

        /// <summary>Registers one provider. A rejected call leaves this builder exactly as it was:
        /// <see cref="PlatformServicesFailure.ProviderDeclarationInvalid"/> for an invalid declaration
        /// (checked first, by <see cref="ProviderDescriptor.TryCreate"/>), then
        /// <see cref="PlatformServicesFailure.ProviderDuplicate"/> for an id already registered.</summary>
        public PlatformServicesResult<ProviderRegistryBuilder> Register(
            ProviderId id,
            IEnumerable<PlatformServiceCapability> capabilities,
            int? cloudSaveGuardRailBytes = null)
        {
            var descriptor = ProviderDescriptor.TryCreate(id, capabilities, cloudSaveGuardRailBytes);
            if (!descriptor.Succeeded)
            {
                return descriptor.As<ProviderRegistryBuilder>();
            }
            if (_entries.ContainsKey(id))
            {
                return PlatformServicesResult<ProviderRegistryBuilder>.Fail(PlatformServicesFailure.ProviderDuplicate, $"Provider '{id}' is already registered.");
            }
            _entries[id] = descriptor.Value;
            return PlatformServicesResult<ProviderRegistryBuilder>.Ok(this);
        }

        /// <summary>Produces the immutable registry. Safe to call more than once; each call snapshots
        /// the current entries so a later successful <see cref="Register"/> never mutates a registry
        /// already handed out.</summary>
        public ProviderRegistry Build() => ProviderRegistry.FromEntries(new SortedDictionary<ProviderId, ProviderDescriptor>(_entries));
    }
}
