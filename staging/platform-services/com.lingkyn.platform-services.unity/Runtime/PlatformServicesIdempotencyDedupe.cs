using System;
using System.Collections.Generic;

namespace Lingkyn.PlatformServices.Unity
{
    /// <summary>Explicit client-side idempotency-key mapping, synthesized because the fakes and the
    /// vendor shells in this package expose no native request-deduplication mechanism of their own: a
    /// retried call for a key already sent is never sent to the provider twice. A vendor shell that
    /// does expose its own dedupe mechanism maps the Core's opaque idempotency key to that mechanism
    /// instead, as its own adapter logic — never a Core assumption about any vendor's behavior.</summary>
    public sealed class ClientSideDedupeStore
    {
        private readonly Dictionary<string, PlatformServicesProviderCallResult> _sent = new Dictionary<string, PlatformServicesProviderCallResult>(StringComparer.Ordinal);

        public int Count => _sent.Count;

        public bool TryGetPrior(string idempotencyKey, out PlatformServicesProviderCallResult result) => _sent.TryGetValue(idempotencyKey, out result);

        public void Record(string idempotencyKey, PlatformServicesProviderCallResult result) => _sent[idempotencyKey] = result;
    }
}
