namespace Lingkyn.QualityTiers.Unity
{
    // The optional Adaptive Performance feedback route (verification-contract.md, Unity adapter
    // gate bullets 6 and 9): a separate seam behind the adapter's public surface, never a required
    // dependency and never a runtime platform check inside the family. A missing provider is
    // reported explicitly with adaptive_performance.unresolved rather than staying silent
    // (LESSON-004); the route being present or absent is entirely the consumer's own explicit
    // wiring at construction time.

    /// <summary>Explicit, injected access to an optional Adaptive Performance feedback provider. A
    /// real implementation wraps the Adaptive Performance package's indexer API; a fixed fake
    /// reports whatever a test wires it to.</summary>
    public interface IAdaptivePerformanceFeedbackProvider
    {
        bool TryResolveProvider(out string providerName);
    }

    public sealed class FixedAdaptivePerformanceFeedbackProvider : IAdaptivePerformanceFeedbackProvider
    {
        private readonly bool _resolves;
        private readonly string _providerName;

        public FixedAdaptivePerformanceFeedbackProvider(bool resolves, string providerName = "fake_adaptive_performance_provider")
        {
            _resolves = resolves;
            _providerName = providerName ?? string.Empty;
        }

        public bool TryResolveProvider(out string providerName)
        {
            providerName = _resolves ? _providerName : string.Empty;
            return _resolves;
        }
    }

    /// <summary>Resolves an optional Adaptive Performance feedback route without throwing: the
    /// route is optional configuration, so a missing provider is reported through
    /// <see cref="QualityDisplayDiagnostic"/> rather than failing construction, but it is never
    /// silent (LESSON-004). A consumer that wires this route reads <see cref="Diagnostic"/> to
    /// decide whether to use the resolved provider name at all.</summary>
    public static class QualityAdaptivePerformanceFeedback
    {
        public static (string ProviderName, QualityDisplayDiagnostic Diagnostic) Resolve(IAdaptivePerformanceFeedbackProvider provider)
        {
            if (provider == null)
            {
                return (string.Empty, new QualityDisplayDiagnostic(QualityUnityFailure.AdaptivePerformanceUnresolved, "adaptive_performance", "No Adaptive Performance feedback provider was configured."));
            }
            if (!provider.TryResolveProvider(out var providerName))
            {
                return (string.Empty, new QualityDisplayDiagnostic(QualityUnityFailure.AdaptivePerformanceUnresolved, "adaptive_performance", "No live Adaptive Performance feedback provider resolved through the injected accessor."));
            }
            return (providerName, null);
        }
    }
}
