using System;

namespace Lingkyn.Inventory.Core
{
    public sealed class PersistenceEnvelope
    {
        public PersistenceEnvelope(int schemaVersion, InventorySnapshot snapshot)
            : this(schemaVersion, InventoryStateData.FromSnapshot(snapshot))
        {
        }

        public PersistenceEnvelope(int schemaVersion, InventoryStateData state)
        {
            if (schemaVersion < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(schemaVersion), "Schema version must be positive.");
            }

            State = state ?? throw new ArgumentNullException(nameof(state));
            SchemaVersion = schemaVersion;
        }

        public int SchemaVersion { get; }
        public InventoryId InventoryId => new InventoryId(State.InventoryId);
        public long Revision => State.Revision;
        public InventoryStateData State { get; }
        public InventorySnapshot Snapshot => State.ToSnapshot();
    }
}
