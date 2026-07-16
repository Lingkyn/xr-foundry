using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Lingkyn.Inventory.Core
{
    public sealed class InventoryStateFragmentData
    {
        public InventoryStateFragmentData(string typeId, int schemaVersion, string payload)
        {
            TypeId = IdentifierGuard.Require(typeId, nameof(typeId));
            if (schemaVersion < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            }

            SchemaVersion = schemaVersion;
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        }

        public string TypeId { get; }
        public int SchemaVersion { get; }
        public string Payload { get; }
    }

    public sealed class InventorySlotState
    {
        private readonly ReadOnlyCollection<InventoryStateFragmentData> _stateFragments;

        public InventorySlotState(
            string definitionId,
            int quantity,
            string instanceId = null,
            IEnumerable<InventoryStateFragmentData> stateFragments = null)
        {
            DefinitionId = IdentifierGuard.Require(definitionId, nameof(definitionId));
            if (quantity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(quantity), "A persisted quantity must be positive.");
            }

            Quantity = quantity;
            InstanceId = string.IsNullOrWhiteSpace(instanceId) ? null : instanceId;
            var fragments = (stateFragments ?? Array.Empty<InventoryStateFragmentData>())
                .Select(fragment => fragment == null
                    ? null
                    : new InventoryStateFragmentData(fragment.TypeId, fragment.SchemaVersion, fragment.Payload))
                .ToArray();
            if (fragments.Any(fragment => fragment == null))
            {
                throw new ArgumentException("Persisted instance-state fragments cannot contain null values.", nameof(stateFragments));
            }

            var duplicate = fragments.GroupBy(fragment => fragment.TypeId, StringComparer.Ordinal)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicate != null)
            {
                throw new ArgumentException(
                    $"Duplicate persisted instance-state fragment type: {duplicate.Key}",
                    nameof(stateFragments));
            }

            _stateFragments = new ReadOnlyCollection<InventoryStateFragmentData>(fragments
                .OrderBy(fragment => fragment.TypeId, StringComparer.Ordinal)
                .ToArray());
        }

        public string DefinitionId { get; }
        public int Quantity { get; }
        public string InstanceId { get; }
        public IReadOnlyList<InventoryStateFragmentData> StateFragments => _stateFragments;
    }

    public sealed class InventoryContainerState
    {
        private readonly ReadOnlyCollection<InventorySlotState> _slots;

        public InventoryContainerState(string containerId, IEnumerable<InventorySlotState> slots)
        {
            ContainerId = IdentifierGuard.Require(containerId, nameof(containerId));
            if (slots == null)
            {
                throw new ArgumentNullException(nameof(slots));
            }

            _slots = new ReadOnlyCollection<InventorySlotState>(slots.Select(Clone).ToArray());
            if (_slots.Count < 1)
            {
                throw new ArgumentException("A persisted container must contain at least one slot.", nameof(slots));
            }
        }

        public string ContainerId { get; }
        public IReadOnlyList<InventorySlotState> Slots => _slots;
        public int Capacity => _slots.Count;

        private static InventorySlotState Clone(InventorySlotState slot) => slot == null
            ? null
            : new InventorySlotState(slot.DefinitionId, slot.Quantity, slot.InstanceId, slot.StateFragments);
    }

    public sealed class InventoryStateData
    {
        private readonly ReadOnlyCollection<InventoryContainerState> _containers;

        public InventoryStateData(
            string inventoryId,
            long revision,
            IEnumerable<InventoryContainerState> containers)
        {
            InventoryId = IdentifierGuard.Require(inventoryId, nameof(inventoryId));
            if (revision < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(revision), "A persisted revision cannot be negative.");
            }

            if (containers == null)
            {
                throw new ArgumentNullException(nameof(containers));
            }

            _containers = new ReadOnlyCollection<InventoryContainerState>(containers
                .Select(container => container == null
                    ? null
                    : new InventoryContainerState(container.ContainerId, container.Slots))
                .ToArray());
            if (_containers.Count < 1 || _containers.Any(container => container == null))
            {
                throw new ArgumentException("Persisted state must contain non-null containers.", nameof(containers));
            }

            var duplicate = _containers.GroupBy(container => container.ContainerId, StringComparer.Ordinal)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicate != null)
            {
                throw new ArgumentException($"Duplicate persisted container id: {duplicate.Key}", nameof(containers));
            }

            Revision = revision;
        }

        public string InventoryId { get; }
        public long Revision { get; }
        public IReadOnlyList<InventoryContainerState> Containers => _containers;

        internal static InventoryStateData FromSnapshot(InventorySnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            return new InventoryStateData(
                snapshot.InventoryId.Value,
                snapshot.Revision,
                snapshot.Containers
                    .OrderBy(container => container.Id.Value, StringComparer.Ordinal)
                    .Select(container => new InventoryContainerState(
                        container.Id.Value,
                        container.Slots.Select(stack => stack == null
                            ? null
                            : new InventorySlotState(
                                stack.DefinitionId.Value,
                                stack.Quantity,
                                stack.InstanceId?.Value,
                                stack.StateFragments.Select(fragment => new InventoryStateFragmentData(
                                    fragment.TypeId.Value,
                                    fragment.SchemaVersion,
                                    fragment.Payload)))))));
        }

        internal InventorySnapshot ToSnapshot()
        {
            return new InventorySnapshot(
                new InventoryId(InventoryId),
                Revision,
                _containers.ToDictionary(
                    container => new ContainerId(container.ContainerId),
                    container => container.Slots.Select(slot => slot == null
                        ? null
                        : new ItemStack(
                            new ItemDefinitionId(slot.DefinitionId),
                            slot.Quantity,
                            string.IsNullOrWhiteSpace(slot.InstanceId)
                                ? (ItemInstanceId?)null
                                : new ItemInstanceId(slot.InstanceId),
                            slot.StateFragments.Select(fragment => new ItemStateFragment(
                                new ItemStateFragmentTypeId(fragment.TypeId),
                                fragment.SchemaVersion,
                                fragment.Payload))))
                        .ToArray()));
        }
    }

    public interface IInventoryStateMigration
    {
        int FromVersion { get; }
        int ToVersion { get; }
        InventoryStateData Migrate(InventoryStateData source);
    }

    public enum InventoryRestoreFailure
    {
        None = 0,
        InvalidEnvelope = 1,
        UnsupportedSchema = 2,
        MissingMigration = 3,
        MigrationFailed = 4,
        InventoryMismatch = 5,
        ContainerMismatch = 6,
        CapacityMismatch = 7,
        MissingDefinition = 8,
        InvalidStack = 9,
        DuplicateInstance = 10,
        InvalidInstanceState = 11,
    }

    public sealed class InventoryRestoreResult
    {
        internal InventoryRestoreResult(
            bool succeeded,
            InventoryRestoreFailure failure,
            string message,
            int schemaVersionBefore,
            int schemaVersionAfter,
            long revisionBefore,
            long revisionAfter)
        {
            Succeeded = succeeded;
            Failure = failure;
            Message = message ?? string.Empty;
            SchemaVersionBefore = schemaVersionBefore;
            SchemaVersionAfter = schemaVersionAfter;
            RevisionBefore = revisionBefore;
            RevisionAfter = revisionAfter;
        }

        public bool Succeeded { get; }
        public InventoryRestoreFailure Failure { get; }
        public string Message { get; }
        public int SchemaVersionBefore { get; }
        public int SchemaVersionAfter { get; }
        public long RevisionBefore { get; }
        public long RevisionAfter { get; }
    }

    public static class InventoryPersistence
    {
        public const int CurrentSchemaVersion = 2;

        internal static bool TryMigrate(
            PersistenceEnvelope envelope,
            IEnumerable<IInventoryStateMigration> migrations,
            out InventoryStateData state,
            out InventoryRestoreFailure failure,
            out string message)
        {
            state = null;
            failure = InventoryRestoreFailure.None;
            message = string.Empty;
            if (envelope == null)
            {
                failure = InventoryRestoreFailure.InvalidEnvelope;
                message = "The persistence envelope is required.";
                return false;
            }

            if (envelope.SchemaVersion > CurrentSchemaVersion)
            {
                failure = InventoryRestoreFailure.UnsupportedSchema;
                message = $"Schema {envelope.SchemaVersion} is newer than supported schema {CurrentSchemaVersion}.";
                return false;
            }

            state = envelope.State;
            var version = envelope.SchemaVersion;
            var available = (migrations ?? Array.Empty<IInventoryStateMigration>()).ToArray();
            if (available.Any(migration => migration == null))
            {
                failure = InventoryRestoreFailure.InvalidEnvelope;
                message = "Migration collections cannot contain null entries.";
                return false;
            }

            while (version < CurrentSchemaVersion)
            {
                var matches = available.Where(migration => migration.FromVersion == version).ToArray();
                if (matches.Length != 1 || matches[0].ToVersion <= version)
                {
                    failure = InventoryRestoreFailure.MissingMigration;
                    message = $"Exactly one forward migration is required from schema {version}.";
                    return false;
                }

                try
                {
                    state = matches[0].Migrate(state);
                }
                catch (Exception exception)
                {
                    failure = InventoryRestoreFailure.MigrationFailed;
                    message = $"Migration from schema {version} failed: {exception.Message}";
                    return false;
                }

                if (state == null)
                {
                    failure = InventoryRestoreFailure.MigrationFailed;
                    message = $"Migration from schema {version} returned no state.";
                    return false;
                }

                version = matches[0].ToVersion;
                if (version > CurrentSchemaVersion)
                {
                    failure = InventoryRestoreFailure.UnsupportedSchema;
                    message = $"Migration advanced to unsupported schema {version}.";
                    return false;
                }
            }

            return true;
        }
    }
}
