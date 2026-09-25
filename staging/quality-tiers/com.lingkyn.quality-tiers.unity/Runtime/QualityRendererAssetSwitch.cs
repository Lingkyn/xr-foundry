using System;
using System.Collections.Generic;
using Lingkyn.QualityTiers.Core;
using UnityEngine.Rendering.Universal;

namespace Lingkyn.QualityTiers.Unity
{
    // MSAA, shadow, and post-processing budgets applied through an explicit URP renderer-asset
    // swap (verification-contract.md, Unity adapter gate bullet 4): QualityRendererAssetMap binds
    // each registered tier to an explicit UniversalRenderPipelineAsset reference constructed at
    // authoring time, never resolved by name, path, or Resources.Load. A tier with no bound asset
    // reports renderer.asset.unbound naming the tier; construction throws with the same report
    // (LESSON-004).

    /// <summary>Applies a bound <see cref="UniversalRenderPipelineAsset"/> and a Unity quality
    /// level for one accepted tier selection. A real implementation sets
    /// <c>QualitySettings.renderPipelineAsset</c> and <c>QualitySettings.SetQualityLevel</c>; a
    /// fixed fake records exactly what it was asked to apply.</summary>
    public interface IRendererAssetSwitch
    {
        void Apply(TierId tier, UniversalRenderPipelineAsset asset, int qualityLevel);
    }

    public sealed class QualityRendererAssetDiagnostic
    {
        public QualityRendererAssetDiagnostic(string code, TierId tier, string message)
        {
            Code = code;
            Tier = tier;
            Message = message ?? string.Empty;
        }

        public string Code { get; }
        public TierId Tier { get; }
        public string Message { get; }
    }

    public sealed class QualityRendererAssetReport
    {
        public QualityRendererAssetReport(IReadOnlyList<QualityRendererAssetDiagnostic> diagnostics)
        {
            Diagnostics = diagnostics;
        }

        public IReadOnlyList<QualityRendererAssetDiagnostic> Diagnostics { get; }
        public bool IsValid => Diagnostics.Count == 0;
    }

    public sealed class QualityRendererAssetException : Exception
    {
        public QualityRendererAssetException(QualityRendererAssetReport report) : base(Describe(report))
        {
            Report = report;
        }

        public QualityRendererAssetReport Report { get; }

        private static string Describe(QualityRendererAssetReport report)
        {
            var lines = new List<string>(report.Diagnostics.Count + 1) { "Quality renderer-asset map is invalid:" };
            foreach (var diagnostic in report.Diagnostics)
            {
                lines.Add($"  [{diagnostic.Code}] {diagnostic.Tier}: {diagnostic.Message}");
            }
            return string.Join("\n", lines);
        }
    }

    /// <summary>The explicit, injected map from a registered <see cref="TierId"/> to one
    /// authoring-time <see cref="UniversalRenderPipelineAsset"/> reference and a Unity quality
    /// level index. Every tier in <see cref="QualityTierRegistry"/> must have a bound entry.</summary>
    public sealed class QualityRendererAssetMap
    {
        private readonly Dictionary<TierId, (UniversalRenderPipelineAsset Asset, int QualityLevel)> _bindings;

        private QualityRendererAssetMap(Dictionary<TierId, (UniversalRenderPipelineAsset Asset, int QualityLevel)> bindings)
        {
            _bindings = bindings;
        }

        public static QualityRendererAssetReport Validate(QualityTierRegistry registry, IReadOnlyDictionary<TierId, (UniversalRenderPipelineAsset Asset, int QualityLevel)> bindings)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (bindings == null) throw new ArgumentNullException(nameof(bindings));
            var diagnostics = new List<QualityRendererAssetDiagnostic>();
            foreach (var tier in registry.Tiers)
            {
                if (!bindings.TryGetValue(tier.Id, out var binding) || binding.Asset == null)
                {
                    diagnostics.Add(new QualityRendererAssetDiagnostic(QualityUnityFailure.RendererAssetUnbound, tier.Id, $"Tier '{tier.Id}' has no bound UniversalRenderPipelineAsset."));
                }
            }
            return new QualityRendererAssetReport(diagnostics);
        }

        public static QualityRendererAssetMap Create(QualityTierRegistry registry, IReadOnlyDictionary<TierId, (UniversalRenderPipelineAsset Asset, int QualityLevel)> bindings)
        {
            var report = Validate(registry, bindings);
            if (!report.IsValid) throw new QualityRendererAssetException(report);
            return new QualityRendererAssetMap(new Dictionary<TierId, (UniversalRenderPipelineAsset, int)>(bindings));
        }

        public bool TryResolve(TierId tier, out UniversalRenderPipelineAsset asset, out int qualityLevel)
        {
            if (_bindings.TryGetValue(tier, out var binding))
            {
                asset = binding.Asset;
                qualityLevel = binding.QualityLevel;
                return true;
            }
            asset = null;
            qualityLevel = -1;
            return false;
        }
    }

    public readonly struct RecordedRendererAssetCall
    {
        public RecordedRendererAssetCall(TierId tier, UniversalRenderPipelineAsset asset, int qualityLevel)
        {
            Tier = tier;
            Asset = asset;
            QualityLevel = qualityLevel;
        }

        public TierId Tier { get; }
        public UniversalRenderPipelineAsset Asset { get; }
        public int QualityLevel { get; }
    }

    /// <summary>An in-memory test double: records exactly what it was asked to apply so a test can
    /// assert the resolved tier, asset, and quality level without a device or a renderer
    /// dependency.</summary>
    public sealed class RecordingRendererAssetSwitch : IRendererAssetSwitch
    {
        private readonly List<RecordedRendererAssetCall> _calls = new List<RecordedRendererAssetCall>();

        public IReadOnlyList<RecordedRendererAssetCall> Calls => _calls;

        public void Apply(TierId tier, UniversalRenderPipelineAsset asset, int qualityLevel)
        {
            _calls.Add(new RecordedRendererAssetCall(tier, asset, qualityLevel));
        }
    }
}
