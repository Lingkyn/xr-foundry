using System;

namespace Lingkyn.Inventory.Core
{
    public sealed class PersistenceEnvelope
    {
        public PersistenceEnvelope(int schemaVersion, InventorySnapshot snapshot)
        {
            if (schemaVersion < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(schemaVersion), "Schema version must be positive.");
            }

            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            SchemaVersion = schemaVersion;
        }

        public int SchemaVersion { get; }
        public InventoryId InventoryId => Snapshot.InventoryId;
        public long Revision => Snapshot.Revision;
        public InventorySnapshot Snapshot { get; }
    }
}
