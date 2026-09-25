using Lingkyn.PlatformServices.Core;

namespace Lingkyn.PlatformServices.Unity
{
    // Cloud save composes on Persistence documents without this adapter depending on the Persistence
    // package: this is a small IPersistedDocumentSource seam, not a reference to Persistence's own
    // stage/integrity/flush pipeline (docs/standards/persistence/verification-contract.md's document
    // contract is referenced, never depended on; see docs/standards/platform-services/README.md's
    // "why it composes" section).

    /// <summary>A small seam a consumer wires to read the same bytes a local save's document holds,
    /// so a <see cref="CloudWriteIntent"/> mirrors a local save. A real implementation reads a
    /// Persistence document's already-flushed bytes; a fixed fake reports whatever a test wires it
    /// to.</summary>
    public interface IPersistedDocumentSource
    {
        /// <summary>True with the document's current bytes when one exists; false when no document
        /// has been written yet.</summary>
        bool TryReadDocument(out byte[] payload);
    }

    public sealed class FixedPersistedDocumentSource : IPersistedDocumentSource
    {
        private readonly byte[] _payload;

        public FixedPersistedDocumentSource(byte[] payload)
        {
            _payload = payload;
        }

        public bool TryReadDocument(out byte[] payload)
        {
            payload = _payload;
            return _payload != null;
        }
    }

    /// <summary>Builds a <see cref="CloudWriteIntent"/> from the same bytes an
    /// <see cref="IPersistedDocumentSource"/> holds, without this adapter depending on the Persistence
    /// package or its stage/integrity/flush pipeline.</summary>
    public static class PersistenceMirroredCloudWrite
    {
        public static bool TryBuildIntent(IPersistedDocumentSource source, string cloudKey, string idempotencyKey, out CloudWriteIntent intent)
        {
            intent = null;
            if (source == null) return false;
            if (!source.TryReadDocument(out var payload)) return false;
            intent = new CloudWriteIntent(cloudKey, payload, idempotencyKey);
            return true;
        }
    }
}
