using System;
using System.Collections.Generic;
using Lingkyn.Haptics.Core;
using UnityEngine;

namespace Lingkyn.Haptics.Unity
{
    // HapticProfileAsset as a ScriptableObject constructed with explicit references (design-time
    // serialized fields, never a resource-folder load, a scene search, or reflection), converted
    // deterministically to the Core's immutable HapticProfile without mutating the authored
    // asset. An invalid field reports the Core's own profile.declaration.invalid (or
    // identity.malformed for the profile id) with the field path and the source asset; conversion
    // throws with the same report.

    /// <summary>One named-device override: an opaque device id plus its scale and enable flag.
    /// A blank device id or a device id repeated across two entries is invalid.</summary>
    [Serializable]
    public sealed class HapticNamedDeviceEntry
    {
        [SerializeField] private string deviceId = string.Empty;
        [SerializeField] private float scale = 1f;
        [SerializeField] private bool enabled = true;

        public string DeviceId { get => deviceId; set => deviceId = value ?? string.Empty; }
        public float Scale { get => scale; set => scale = value; }
        public bool Enabled { get => enabled; set => enabled = value; }
    }

    /// <summary>A per-controller/per-hand haptic profile authored as design-time data: an explicit
    /// scale and enable flag for <see cref="HapticTarget.Left"/>, <see cref="HapticTarget.Right"/>,
    /// and <see cref="HapticTarget.Both"/>, plus an explicit list of named-device overrides.
    /// Converted to the Core's <see cref="HapticProfile"/> only through
    /// <see cref="HapticProfileAssetConverter"/>.</summary>
    [CreateAssetMenu(menuName = "Lingkyn/Haptics/Profile", fileName = "HapticProfile")]
    public sealed class HapticProfileAsset : ScriptableObject
    {
        [SerializeField] private string profileId = string.Empty;
        [SerializeField] private float leftScale = 1f;
        [SerializeField] private bool leftEnabled = true;
        [SerializeField] private float rightScale = 1f;
        [SerializeField] private bool rightEnabled = true;
        [SerializeField] private float bothScale = 1f;
        [SerializeField] private bool bothEnabled = true;
        [SerializeField] private List<HapticNamedDeviceEntry> namedDevices = new List<HapticNamedDeviceEntry>();

        // Property setters exist so a test can construct an asset with ScriptableObject.CreateInstance
        // and set its fields directly, the same way staging/live-tuning's LiveTuningDemoSkinAsset does;
        // an Editor Inspector still edits the serialized fields, never these setters.
        public string ProfileId { get => profileId; set => profileId = value ?? string.Empty; }
        public float LeftScale { get => leftScale; set => leftScale = value; }
        public bool LeftEnabled { get => leftEnabled; set => leftEnabled = value; }
        public float RightScale { get => rightScale; set => rightScale = value; }
        public bool RightEnabled { get => rightEnabled; set => rightEnabled = value; }
        public float BothScale { get => bothScale; set => bothScale = value; }
        public bool BothEnabled { get => bothEnabled; set => bothEnabled = value; }
        public List<HapticNamedDeviceEntry> NamedDevices { get => namedDevices; set => namedDevices = value ?? new List<HapticNamedDeviceEntry>(); }
    }

    /// <summary>One profile-asset conversion failure: a stable code (a reused Core code), the
    /// offending field path, the source asset, and a human message. Never silent (LESSON-004).</summary>
    public sealed class HapticProfileAssetDiagnostic
    {
        public HapticProfileAssetDiagnostic(string code, UnityEngine.Object source, string fieldPath, string message)
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

    public sealed class HapticProfileAssetReport
    {
        public HapticProfileAssetReport(IReadOnlyList<HapticProfileAssetDiagnostic> diagnostics)
        {
            Diagnostics = diagnostics;
        }

        public IReadOnlyList<HapticProfileAssetDiagnostic> Diagnostics { get; }
        public bool IsValid => Diagnostics.Count == 0;
    }

    public sealed class HapticProfileAssetException : Exception
    {
        public HapticProfileAssetException(HapticProfileAssetReport report) : base(Describe(report))
        {
            Report = report;
        }

        public HapticProfileAssetReport Report { get; }

        private static string Describe(HapticProfileAssetReport report)
        {
            var lines = new List<string>(report.Diagnostics.Count + 1) { "Haptic profile asset is invalid:" };
            foreach (var diagnostic in report.Diagnostics)
            {
                lines.Add($"  [{diagnostic.Code}] {diagnostic.FieldPath}: {diagnostic.Message}");
            }
            return string.Join("\n", lines);
        }
    }

    public static class HapticProfileAssetConverter
    {
        /// <summary>Validates <paramref name="asset"/> without converting it. Read-only: it never
        /// mutates the authored asset.</summary>
        public static HapticProfileAssetReport Validate(HapticProfileAsset asset) => Build(asset).Report;

        /// <summary>Converts <paramref name="asset"/> to the Core's immutable
        /// <see cref="HapticProfile"/>. Throws <see cref="HapticProfileAssetException"/> with the
        /// same report a failed <see cref="Validate"/> call would return.</summary>
        public static HapticProfile Convert(HapticProfileAsset asset)
        {
            var (report, profile) = Build(asset);
            if (!report.IsValid) throw new HapticProfileAssetException(report);
            return profile;
        }

        private static (HapticProfileAssetReport Report, HapticProfile Profile) Build(HapticProfileAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            var diagnostics = new List<HapticProfileAssetDiagnostic>();

            var idResult = HapticProfileId.TryCreate(asset.ProfileId);
            if (!idResult.Succeeded)
            {
                diagnostics.Add(new HapticProfileAssetDiagnostic(idResult.Code, asset, "profileId", idResult.Message));
                return (new HapticProfileAssetReport(diagnostics), null);
            }

            var builder = new HapticProfileBuilder(idResult.Value);
            AddField(builder, diagnostics, asset, HapticTarget.Left, asset.LeftScale, asset.LeftEnabled, "left");
            AddField(builder, diagnostics, asset, HapticTarget.Right, asset.RightScale, asset.RightEnabled, "right");
            AddField(builder, diagnostics, asset, HapticTarget.Both, asset.BothScale, asset.BothEnabled, "both");

            for (var index = 0; index < asset.NamedDevices.Count; index++)
            {
                var entry = asset.NamedDevices[index];
                if (string.IsNullOrEmpty(entry.DeviceId))
                {
                    diagnostics.Add(new HapticProfileAssetDiagnostic(HapticFailure.ProfileDeclarationInvalid, asset, $"namedDevices[{index}].deviceId", "A named-device entry must declare a non-empty device id."));
                    continue;
                }
                AddField(builder, diagnostics, asset, HapticTarget.NamedDevice(entry.DeviceId), entry.Scale, entry.Enabled, $"namedDevices[{index}]");
            }

            var report = new HapticProfileAssetReport(diagnostics);
            return (report, report.IsValid ? builder.Build() : null);
        }

        private static void AddField(HapticProfileBuilder builder, List<HapticProfileAssetDiagnostic> diagnostics, HapticProfileAsset asset, HapticTarget target, float scale, bool enabled, string fieldPath)
        {
            var added = builder.Add(target, scale, enabled);
            if (!added.Succeeded)
            {
                diagnostics.Add(new HapticProfileAssetDiagnostic(added.Code, asset, fieldPath, added.Message));
            }
        }
    }
}
