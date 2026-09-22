using System;
using System.Collections.Generic;
using System.Linq;
using Lingkyn.LiveTuning.Core;
using UnityEngine;

namespace Lingkyn.LiveTuning.Unity
{
    // Explicit target resolution from Core binding records through hand-written, per-asset-type
    // binders. No binder resolves a member by reflection over fields or properties; the scaffold
    // (panel host and editors, in TuningPanelHost.cs and TuningEditors.cs) never references a
    // binder, a skin type, or a package type. Adding a new target adds a binding record and, for
    // a new asset type, a binder, never a control, panel, or editor class.

    /// <summary>A hand-written binder for one asset type's closed member set. No reflection: every
    /// member is resolved and applied through an explicit switch the binder author writes.</summary>
    public interface ISkinBinder
    {
        /// <summary>A stable name for diagnostics: the asset type this binder resolves members on.</summary>
        string AssetTypeName { get; }
        /// <summary>The closed set of member names this binder declares.</summary>
        IReadOnlyCollection<string> MemberNames { get; }
        /// <summary>True when this binder is the one that resolves members on <paramref name="asset"/>'s type.</summary>
        bool CanBind(UnityEngine.Object asset);
        /// <summary>The declared kind of one member, or false when the member is outside this
        /// binder's closed set.</summary>
        bool TryGetMemberKind(string memberName, out TunableKind kind);
        /// <summary>Writes one member's value onto the asset. Only ever called for a member and
        /// kind this binder already confirmed through <see cref="TryGetMemberKind"/>.</summary>
        void Apply(UnityEngine.Object asset, string memberName, TunableValue value);
    }

    /// <summary>The renderer adapter's own skin entry point, invoked after every binder write so a
    /// changed value reaches the view the same way any other skin change would (never a view field
    /// directly). One implementation per renderer adapter; the runtime holds it only as this seam.</summary>
    public interface ISkinApplyTarget
    {
        void ApplySkin(UnityEngine.Object skin);
    }

    /// <summary>Parses and formats the adapter's own opaque target-path convention:
    /// <c>asset_key#member_name</c>. The Core never interprets this text; only this adapter does.</summary>
    public static class SkinTargetPath
    {
        public static string Format(string assetKey, string memberName) => $"{assetKey}#{memberName}";

        public static bool TryParse(string targetPath, out string assetKey, out string memberName)
        {
            assetKey = string.Empty;
            memberName = string.Empty;
            if (string.IsNullOrEmpty(targetPath)) return false;
            var separator = targetPath.IndexOf('#');
            if (separator <= 0 || separator == targetPath.Length - 1) return false;
            assetKey = targetPath.Substring(0, separator);
            memberName = targetPath.Substring(separator + 1);
            return true;
        }
    }

    /// <summary>One resolution failure: a stable code, the offending field path, the source asset
    /// (when known), and a human message. Never silent (LESSON-004).</summary>
    public sealed class BindingDiagnostic
    {
        public BindingDiagnostic(string code, UnityEngine.Object source, string fieldPath, string message)
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

    public sealed class BindingReport
    {
        public BindingReport(IReadOnlyList<BindingDiagnostic> diagnostics)
        {
            Diagnostics = diagnostics;
        }

        public IReadOnlyList<BindingDiagnostic> Diagnostics { get; }
        public bool IsValid => Diagnostics.Count == 0;
    }

    public sealed class SkinBindingException : Exception
    {
        public SkinBindingException(BindingReport report)
            : base(Describe(report))
        {
            Report = report;
        }

        public BindingReport Report { get; }

        private static string Describe(BindingReport report)
        {
            var lines = new List<string>(report.Diagnostics.Count + 1) { "Live tuning binding is invalid:" };
            foreach (var diagnostic in report.Diagnostics)
            {
                lines.Add($"  [{diagnostic.Code}] {diagnostic.FieldPath}: {diagnostic.Message}");
            }
            return string.Join("\n", lines);
        }
    }

    /// <summary>One binding resolved all the way to a live target: the record, the bound asset,
    /// the member name on it, and the binder that resolved and will apply it.</summary>
    public sealed class ResolvedBinding
    {
        internal ResolvedBinding(BindingRecord record, UnityEngine.Object asset, string memberName, ISkinBinder binder)
        {
            Record = record;
            Asset = asset;
            MemberName = memberName;
            Binder = binder;
        }

        public BindingRecord Record { get; }
        public TunableId Id => Record.Id;
        public UnityEngine.Object Asset { get; }
        public string MemberName { get; }
        internal ISkinBinder Binder { get; }
    }

    public static class SkinBindingValidation
    {
        public static BindingReport Validate(
            IEnumerable<BindingRecord> records,
            TunableRegistry registry,
            IReadOnlyDictionary<string, UnityEngine.Object> assetsByKey,
            IReadOnlyList<ISkinBinder> binders)
        {
            if (records == null) throw new ArgumentNullException(nameof(records));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (assetsByKey == null) throw new ArgumentNullException(nameof(assetsByKey));
            if (binders == null) throw new ArgumentNullException(nameof(binders));

            var diagnostics = new List<BindingDiagnostic>();
            var seen = new HashSet<TunableId>();
            foreach (var record in records)
            {
                if (record == null)
                {
                    diagnostics.Add(new BindingDiagnostic(LiveTuningUnityFailure.BindingTargetMissing, null, string.Empty, "A binding record must not be null."));
                    continue;
                }
                if (!registry.TryGet(record.Id, out var registration))
                {
                    diagnostics.Add(new BindingDiagnostic(LiveTuningFailure.TunableUnknown, null, record.TargetPath, $"Binding names tunable '{record.Id}' which is not registered."));
                    continue;
                }
                if (registration.Kind != record.Kind)
                {
                    diagnostics.Add(new BindingDiagnostic(LiveTuningFailure.BindingKindMismatch, null, record.TargetPath, $"Binding for '{record.Id}' declares kind {record.Kind} but the tunable is registered as {registration.Kind}."));
                    continue;
                }
                if (!seen.Add(record.Id))
                {
                    diagnostics.Add(new BindingDiagnostic(LiveTuningFailure.BindingDuplicate, null, record.TargetPath, $"Tunable '{record.Id}' already has a binding record."));
                    continue;
                }
                if (!SkinTargetPath.TryParse(record.TargetPath, out var assetKey, out var memberName))
                {
                    diagnostics.Add(new BindingDiagnostic(LiveTuningUnityFailure.BindingTargetMissing, null, record.TargetPath, $"Target path '{record.TargetPath}' for '{record.Id}' does not name 'asset_key#member'."));
                    continue;
                }
                if (!assetsByKey.TryGetValue(assetKey, out var asset) || asset == null)
                {
                    diagnostics.Add(new BindingDiagnostic(LiveTuningUnityFailure.BindingTargetMissing, null, record.TargetPath, $"No bound asset reference named '{assetKey}' for '{record.Id}'."));
                    continue;
                }
                var binder = FindBinder(binders, asset);
                if (binder == null)
                {
                    diagnostics.Add(new BindingDiagnostic(LiveTuningUnityFailure.BindingTargetMissing, asset, record.TargetPath, $"No binder declares asset type '{asset.GetType().Name}' for '{record.Id}'."));
                    continue;
                }
                if (!binder.TryGetMemberKind(memberName, out var memberKind))
                {
                    diagnostics.Add(new BindingDiagnostic(LiveTuningUnityFailure.BindingMemberUnknown, asset, record.TargetPath, $"'{binder.AssetTypeName}' has no member '{memberName}' for '{record.Id}'."));
                    continue;
                }
                if (memberKind != record.Kind)
                {
                    diagnostics.Add(new BindingDiagnostic(LiveTuningFailure.BindingKindMismatch, asset, record.TargetPath, $"Member '{memberName}' on '{binder.AssetTypeName}' is {memberKind} but tunable '{record.Id}' is {record.Kind}."));
                }
            }
            return new BindingReport(diagnostics);
        }

        internal static ISkinBinder FindBinder(IReadOnlyList<ISkinBinder> binders, UnityEngine.Object asset)
        {
            foreach (var binder in binders)
            {
                if (binder.CanBind(asset)) return binder;
            }
            return null;
        }
    }

    /// <summary>The immutable, validated set of resolved bindings. Enumerates in canonical id
    /// order. Construction throws with the same report a failed <see cref="SkinBindingValidation.Validate"/>
    /// call would return.</summary>
    public sealed class SkinBindingSet
    {
        private readonly SortedDictionary<TunableId, ResolvedBinding> _bindings;

        private SkinBindingSet(SortedDictionary<TunableId, ResolvedBinding> bindings)
        {
            _bindings = bindings;
        }

        public int Count => _bindings.Count;
        public IEnumerable<ResolvedBinding> Bindings => _bindings.Values;
        public bool TryGet(TunableId id, out ResolvedBinding binding) => _bindings.TryGetValue(id, out binding);

        public static SkinBindingSet Create(
            IEnumerable<BindingRecord> records,
            TunableRegistry registry,
            IReadOnlyDictionary<string, UnityEngine.Object> assetsByKey,
            IReadOnlyList<ISkinBinder> binders)
        {
            var recordList = (records ?? throw new ArgumentNullException(nameof(records))).ToList();
            var report = SkinBindingValidation.Validate(recordList, registry, assetsByKey, binders);
            if (!report.IsValid) throw new SkinBindingException(report);

            var map = new SortedDictionary<TunableId, ResolvedBinding>();
            foreach (var record in recordList)
            {
                SkinTargetPath.TryParse(record.TargetPath, out var assetKey, out var memberName);
                assetsByKey.TryGetValue(assetKey, out var asset);
                var binder = SkinBindingValidation.FindBinder(binders, asset);
                map[record.Id] = new ResolvedBinding(record, asset, memberName, binder);
            }
            return new SkinBindingSet(map);
        }
    }
}
