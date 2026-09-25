using System;
using System.Collections.Generic;
using Lingkyn.PlatformServices.Core;
using UnityEngine;

namespace Lingkyn.PlatformServices.Unity
{
    // PlatformServicesProviderConfigAsset as a ScriptableObject constructed with explicit references
    // (design-time serialized fields, never a resource-folder load, a scene search, or reflection),
    // converted deterministically to the Core's immutable ProviderDescriptor without mutating the
    // authored asset. An invalid field reports the Core's own provider.declaration.invalid (or
    // identity.malformed for the provider id) with the field path and the source asset; conversion
    // throws with the same report.

    /// <summary>One provider's capability subset and cloud_save guard rail, authored as design-time
    /// data. Converted to the Core's <see cref="ProviderDescriptor"/> only through
    /// <see cref="PlatformServicesProviderConfigConverter"/>.</summary>
    [CreateAssetMenu(menuName = "Lingkyn/PlatformServices/ProviderConfig", fileName = "PlatformServicesProviderConfig")]
    public sealed class PlatformServicesProviderConfigAsset : ScriptableObject
    {
        [SerializeField] private string providerId = string.Empty;
        [SerializeField] private bool entitlement;
        [SerializeField] private bool achievement;
        [SerializeField] private bool leaderboard;
        [SerializeField] private bool cloudSave;
        [SerializeField] private bool identity;
        [SerializeField] private int cloudSaveGuardRailBytes;

        // Property setters exist so a test can construct an asset with ScriptableObject.CreateInstance
        // and set its fields directly, the same way staging/haptics's HapticProfileAsset does; an
        // Editor Inspector still edits the serialized fields, never these setters.
        public string ProviderId { get => providerId; set => providerId = value ?? string.Empty; }
        public bool Entitlement { get => entitlement; set => entitlement = value; }
        public bool Achievement { get => achievement; set => achievement = value; }
        public bool Leaderboard { get => leaderboard; set => leaderboard = value; }
        public bool CloudSave { get => cloudSave; set => cloudSave = value; }
        public bool Identity { get => identity; set => identity = value; }
        public int CloudSaveGuardRailBytes { get => cloudSaveGuardRailBytes; set => cloudSaveGuardRailBytes = value; }
    }

    /// <summary>One provider-config conversion failure: a stable code (a reused Core code), the
    /// offending field path, the source asset, and a human message. Never silent (LESSON-004).</summary>
    public sealed class PlatformServicesProviderConfigDiagnostic
    {
        public PlatformServicesProviderConfigDiagnostic(string code, UnityEngine.Object source, string fieldPath, string message)
        {
            Code = code;
            Source = source;
            FieldPath = fieldPath ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Code { get; }
        public UnityEngine.Object Source { get; }
        public string FieldPath { get; }
        public string Message { get; }
    }

    public sealed class PlatformServicesProviderConfigReport
    {
        public PlatformServicesProviderConfigReport(IReadOnlyList<PlatformServicesProviderConfigDiagnostic> diagnostics)
        {
            Diagnostics = diagnostics;
        }

        public IReadOnlyList<PlatformServicesProviderConfigDiagnostic> Diagnostics { get; }
        public bool IsValid => Diagnostics.Count == 0;
    }

    public sealed class PlatformServicesProviderConfigException : Exception
    {
        public PlatformServicesProviderConfigException(PlatformServicesProviderConfigReport report) : base(Describe(report))
        {
            Report = report;
        }

        public PlatformServicesProviderConfigReport Report { get; }

        private static string Describe(PlatformServicesProviderConfigReport report)
        {
            var lines = new List<string>(report.Diagnostics.Count + 1) { "Platform-services provider config asset is invalid:" };
            foreach (var diagnostic in report.Diagnostics)
            {
                lines.Add($"  [{diagnostic.Code}] {diagnostic.FieldPath}: {diagnostic.Message}");
            }
            return string.Join("\n", lines);
        }
    }

    public static class PlatformServicesProviderConfigConverter
    {
        /// <summary>Validates <paramref name="asset"/> without converting it. Read-only: it never
        /// mutates the authored asset.</summary>
        public static PlatformServicesProviderConfigReport Validate(PlatformServicesProviderConfigAsset asset) => Build(asset).Report;

        /// <summary>Converts <paramref name="asset"/> to the Core's immutable
        /// <see cref="ProviderDescriptor"/>. Throws <see cref="PlatformServicesProviderConfigException"/>
        /// with the same report a failed <see cref="Validate"/> call would return.</summary>
        public static ProviderDescriptor Convert(PlatformServicesProviderConfigAsset asset)
        {
            var (report, descriptor) = Build(asset);
            if (!report.IsValid) throw new PlatformServicesProviderConfigException(report);
            return descriptor;
        }

        private static (PlatformServicesProviderConfigReport Report, ProviderDescriptor Descriptor) Build(PlatformServicesProviderConfigAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            var diagnostics = new List<PlatformServicesProviderConfigDiagnostic>();

            var idResult = ProviderId.TryCreate(asset.ProviderId);
            if (!idResult.Succeeded)
            {
                diagnostics.Add(new PlatformServicesProviderConfigDiagnostic(idResult.Code, asset, "providerId", idResult.Message));
                return (new PlatformServicesProviderConfigReport(diagnostics), null);
            }

            var capabilities = new List<PlatformServiceCapability>();
            if (asset.Entitlement) capabilities.Add(PlatformServiceCapability.Entitlement);
            if (asset.Achievement) capabilities.Add(PlatformServiceCapability.Achievement);
            if (asset.Leaderboard) capabilities.Add(PlatformServiceCapability.Leaderboard);
            if (asset.CloudSave) capabilities.Add(PlatformServiceCapability.CloudSave);
            if (asset.Identity) capabilities.Add(PlatformServiceCapability.Identity);
            int? guardRail = asset.CloudSave ? asset.CloudSaveGuardRailBytes : (int?)null;

            var descriptorResult = ProviderDescriptor.TryCreate(idResult.Value, capabilities, guardRail);
            if (!descriptorResult.Succeeded)
            {
                diagnostics.Add(new PlatformServicesProviderConfigDiagnostic(descriptorResult.Code, asset, "capabilities", descriptorResult.Message));
                return (new PlatformServicesProviderConfigReport(diagnostics), null);
            }

            return (new PlatformServicesProviderConfigReport(diagnostics), descriptorResult.Value);
        }
    }
}
