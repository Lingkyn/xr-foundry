using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Lingkyn.Inventory.Core;
using Lingkyn.Persistence.Core;

namespace XRFoundry.ReferenceSystem.Bindings
{
    [Serializable]
    public sealed class InventoryPersistenceDto
    {
        public int InventorySchemaVersion;
        public InventoryStatePersistenceDto InventoryState;
    }

    [Serializable]
    public sealed class InventoryStatePersistenceDto
    {
        public string InventoryId;
        public long Revision;
        public InventoryContainerPersistenceDto[] Containers = Array.Empty<InventoryContainerPersistenceDto>();
    }

    [Serializable]
    public sealed class InventoryContainerPersistenceDto
    {
        public string ContainerId;
        public InventorySlotPersistenceDto[] Slots = Array.Empty<InventorySlotPersistenceDto>();
    }

    [Serializable]
    public sealed class InventorySlotPersistenceDto
    {
        // Empty slots are represented explicitly so codecs do not have to preserve
        // null reference elements inside serialized arrays.
        public bool Occupied;
        public string DefinitionId;
        public int Quantity;
        public string InstanceId;
        public InventoryStateFragmentPersistenceDto[] StateFragments =
            Array.Empty<InventoryStateFragmentPersistenceDto>();
    }

    [Serializable]
    public sealed class InventoryStateFragmentPersistenceDto
    {
        public string TypeId;
        public int SchemaVersion;
        public string Payload;
    }

    public static class InventoryPersistenceDtoMapper
    {
        public static InventoryPersistenceDto FromSnapshot(InventorySnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            return new InventoryPersistenceDto
            {
                InventorySchemaVersion = InventoryPersistence.CurrentSchemaVersion,
                InventoryState = FromSnapshotState(snapshot),
            };
        }

        public static InventoryPersistenceDto FromEnvelope(PersistenceEnvelope envelope)
        {
            if (envelope == null)
            {
                throw new ArgumentNullException(nameof(envelope));
            }

            return new InventoryPersistenceDto
            {
                InventorySchemaVersion = envelope.SchemaVersion,
                InventoryState = FromState(envelope.State),
            };
        }

        public static SaveResult Validate(InventoryPersistenceDto dto)
        {
            if (dto == null)
            {
                return Invalid("Inventory persistence DTO is required.");
            }

            if (dto.InventorySchemaVersion < 1)
            {
                return Invalid("Inventory schema version must be positive.");
            }

            var state = dto.InventoryState;
            if (state == null)
            {
                return Invalid("Inventory state is required.");
            }

            if (string.IsNullOrWhiteSpace(state.InventoryId))
            {
                return Invalid("Inventory id is required.");
            }

            if (state.Revision < 0)
            {
                return Invalid("Inventory revision cannot be negative.");
            }

            if (state.Containers == null || state.Containers.Length == 0)
            {
                return Invalid("At least one Inventory container is required.");
            }

            var containerIds = new HashSet<string>(StringComparer.Ordinal);
            for (var containerIndex = 0; containerIndex < state.Containers.Length; containerIndex++)
            {
                var container = state.Containers[containerIndex];
                if (container == null)
                {
                    return Invalid($"Inventory container[{containerIndex}] is required.");
                }

                if (string.IsNullOrWhiteSpace(container.ContainerId))
                {
                    return Invalid($"Inventory container[{containerIndex}] id is required.");
                }

                if (!containerIds.Add(container.ContainerId))
                {
                    return Invalid($"Duplicate Inventory container id '{container.ContainerId}'.");
                }

                if (container.Slots == null || container.Slots.Length == 0)
                {
                    return Invalid($"Inventory container '{container.ContainerId}' requires at least one slot.");
                }

                for (var slotIndex = 0; slotIndex < container.Slots.Length; slotIndex++)
                {
                    var slotResult = ValidateSlot(container.ContainerId, slotIndex, container.Slots[slotIndex]);
                    if (!slotResult.Succeeded)
                    {
                        return slotResult;
                    }
                }
            }

            return SaveResult.Success();
        }

        public static SaveResult<PersistenceEnvelope> ToEnvelope(InventoryPersistenceDto dto)
        {
            var validated = Validate(dto);
            if (!validated.Succeeded)
            {
                return SaveResult<PersistenceEnvelope>.Fail(
                    validated.Error.Stage,
                    validated.Error.Code,
                    validated.Error.Message);
            }

            try
            {
                var state = dto.InventoryState;
                var containers = state.Containers.Select(container =>
                    new InventoryContainerState(
                        container.ContainerId,
                        container.Slots.Select(ToInventorySlot)));
                return SaveResult<PersistenceEnvelope>.Success(
                    new PersistenceEnvelope(
                        dto.InventorySchemaVersion,
                        new InventoryStateData(state.InventoryId, state.Revision, containers)));
            }
            catch (Exception exception)
            {
                return SaveResult<PersistenceEnvelope>.Fail(
                    SaveStage.Validate,
                    SaveErrorCode.ValidateRejected,
                    $"Inventory DTO could not be converted to a domain envelope: {exception.Message}");
            }
        }

        private static InventoryStatePersistenceDto FromSnapshotState(InventorySnapshot snapshot)
        {
            return new InventoryStatePersistenceDto
            {
                InventoryId = snapshot.InventoryId.Value,
                Revision = snapshot.Revision,
                Containers = snapshot.Containers
                    .OrderBy(container => container.Id.Value, StringComparer.Ordinal)
                    .Select(FromSnapshotContainer)
                    .ToArray(),
            };
        }

        private static InventoryStatePersistenceDto FromState(InventoryStateData state)
        {
            return new InventoryStatePersistenceDto
            {
                InventoryId = state.InventoryId,
                Revision = state.Revision,
                Containers = state.Containers.Select(container =>
                    new InventoryContainerPersistenceDto
                    {
                        ContainerId = container.ContainerId,
                        Slots = container.Slots.Select(FromInventorySlot).ToArray(),
                    }).ToArray(),
            };
        }

        private static InventoryContainerPersistenceDto FromSnapshotContainer(ContainerSnapshot container)
        {
            return new InventoryContainerPersistenceDto
            {
                ContainerId = container.Id.Value,
                Slots = container.Slots.Select(FromInventorySlot).ToArray(),
            };
        }

        private static InventorySlotPersistenceDto FromInventorySlot(InventorySlotState slot)
        {
            if (slot == null)
            {
                return EmptySlot();
            }

            return OccupiedSlot(
                slot.DefinitionId,
                slot.Quantity,
                slot.InstanceId,
                slot.StateFragments.Select(fragment => new InventoryStateFragmentPersistenceDto
                {
                    TypeId = fragment.TypeId,
                    SchemaVersion = fragment.SchemaVersion,
                    Payload = fragment.Payload,
                }).ToArray());
        }

        private static InventorySlotPersistenceDto FromInventorySlot(ItemStack slot)
        {
            if (slot == null)
            {
                return EmptySlot();
            }

            return OccupiedSlot(
                slot.DefinitionId.Value,
                slot.Quantity,
                slot.InstanceId?.Value,
                slot.StateFragments.Select(fragment => new InventoryStateFragmentPersistenceDto
                {
                    TypeId = fragment.TypeId.Value,
                    SchemaVersion = fragment.SchemaVersion,
                    Payload = fragment.Payload,
                }).ToArray());
        }

        private static InventorySlotPersistenceDto EmptySlot()
        {
            return new InventorySlotPersistenceDto
            {
                Occupied = false,
                DefinitionId = null,
                Quantity = 0,
                InstanceId = null,
                StateFragments = Array.Empty<InventoryStateFragmentPersistenceDto>(),
            };
        }

        private static InventorySlotPersistenceDto OccupiedSlot(
            string definitionId,
            int quantity,
            string instanceId,
            InventoryStateFragmentPersistenceDto[] stateFragments)
        {
            return new InventorySlotPersistenceDto
            {
                Occupied = true,
                DefinitionId = definitionId,
                Quantity = quantity,
                InstanceId = instanceId,
                StateFragments = stateFragments ?? Array.Empty<InventoryStateFragmentPersistenceDto>(),
            };
        }

        private static InventorySlotState ToInventorySlot(InventorySlotPersistenceDto slot)
        {
            if (!slot.Occupied)
            {
                return null;
            }

            return new InventorySlotState(
                slot.DefinitionId,
                slot.Quantity,
                slot.InstanceId,
                slot.StateFragments.Select(fragment => new InventoryStateFragmentData(
                    fragment.TypeId,
                    fragment.SchemaVersion,
                    fragment.Payload)));
        }

        private static SaveResult ValidateSlot(
            string containerId,
            int slotIndex,
            InventorySlotPersistenceDto slot)
        {
            if (slot == null)
            {
                return Invalid($"Inventory container '{containerId}' slot[{slotIndex}] is required.");
            }

            if (slot.StateFragments == null)
            {
                return Invalid($"Inventory container '{containerId}' slot[{slotIndex}] fragment array is required.");
            }

            if (!slot.Occupied)
            {
                if (!string.IsNullOrEmpty(slot.DefinitionId)
                    || slot.Quantity != 0
                    || !string.IsNullOrEmpty(slot.InstanceId)
                    || slot.StateFragments.Length != 0)
                {
                    return Invalid($"Inventory container '{containerId}' slot[{slotIndex}] has non-empty data while marked empty.");
                }

                return SaveResult.Success();
            }

            if (string.IsNullOrWhiteSpace(slot.DefinitionId))
            {
                return Invalid($"Inventory container '{containerId}' slot[{slotIndex}] definition id is required.");
            }

            if (slot.Quantity < 1)
            {
                return Invalid($"Inventory container '{containerId}' slot[{slotIndex}] quantity must be positive.");
            }

            if (slot.InstanceId != null && string.IsNullOrWhiteSpace(slot.InstanceId))
            {
                return Invalid($"Inventory container '{containerId}' slot[{slotIndex}] instance id cannot be whitespace.");
            }

            var fragmentTypes = new HashSet<string>(StringComparer.Ordinal);
            for (var fragmentIndex = 0; fragmentIndex < slot.StateFragments.Length; fragmentIndex++)
            {
                var fragment = slot.StateFragments[fragmentIndex];
                if (fragment == null)
                {
                    return Invalid($"Inventory container '{containerId}' slot[{slotIndex}] fragment[{fragmentIndex}] is required.");
                }

                if (string.IsNullOrWhiteSpace(fragment.TypeId))
                {
                    return Invalid($"Inventory container '{containerId}' slot[{slotIndex}] fragment[{fragmentIndex}] type id is required.");
                }

                if (!fragmentTypes.Add(fragment.TypeId))
                {
                    return Invalid($"Inventory container '{containerId}' slot[{slotIndex}] has duplicate fragment type '{fragment.TypeId}'.");
                }

                if (fragment.SchemaVersion < 1)
                {
                    return Invalid($"Inventory container '{containerId}' slot[{slotIndex}] fragment '{fragment.TypeId}' schema must be positive.");
                }

                if (fragment.Payload == null)
                {
                    return Invalid($"Inventory container '{containerId}' slot[{slotIndex}] fragment '{fragment.TypeId}' payload is required.");
                }
            }

            return SaveResult.Success();
        }

        private static SaveResult Invalid(string message)
        {
            return SaveResult.Fail(SaveStage.Validate, SaveErrorCode.ValidateRejected, message);
        }
    }

    public sealed class InventoryPersistenceLoadReceipt
    {
        internal InventoryPersistenceLoadReceipt(
            int inventorySchemaVersion,
            SaveCandidateKind selectedCandidateKind,
            SaveCandidateId selectedCandidateId,
            bool recoveryOccurred,
            SaveDiagnostic? primaryFailureDiagnostic,
            InventoryRestoreResult restoreResult)
        {
            InventorySchemaVersion = inventorySchemaVersion;
            SelectedCandidateKind = selectedCandidateKind;
            SelectedCandidateId = selectedCandidateId;
            RecoveryOccurred = recoveryOccurred;
            PrimaryFailureDiagnostic = primaryFailureDiagnostic;
            RestoreResult = restoreResult ?? throw new ArgumentNullException(nameof(restoreResult));
        }

        public int InventorySchemaVersion { get; }
        public SaveCandidateKind SelectedCandidateKind { get; }
        public SaveCandidateId SelectedCandidateId { get; }
        public bool RecoveryOccurred { get; }
        public SaveDiagnostic? PrimaryFailureDiagnostic { get; }
        public InventoryRestoreResult RestoreResult { get; }
    }

    public sealed class InventoryToPersistenceAdapter
    {
        // These values configure the persistence system's outer SaveEnvelope. The
        // Inventory schema is carried separately by InventoryPersistenceDto.
        public const string OuterPersistenceSchemaId = "xr-foundry.reference-system.inventory";
        public const int OuterPersistenceSchemaVersion = 1;

        private readonly InventoryAggregate _inventory;
        private readonly SaveCoordinator<InventoryPersistenceDto> _coordinator;
        private readonly IInventoryStateMigration[] _inventoryMigrations;

        public InventoryToPersistenceAdapter(
            InventoryAggregate inventory,
            SaveCoordinator<InventoryPersistenceDto> coordinator,
            IInventoryStateMigration inventoryMigration = null)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _inventoryMigrations = inventoryMigration == null
                ? Array.Empty<IInventoryStateMigration>()
                : new[] { inventoryMigration };
        }

        public SaveCommitResult Save(
            SaveSlotId slotId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var snapshot = _inventory.GetSnapshot();
                var dto = InventoryPersistenceDtoMapper.FromSnapshot(snapshot);
                return _coordinator.Save(slotId, dto, cancellationToken);
            }
            catch (Exception exception)
            {
                return SaveCommitResult.NotCommitted(
                    SaveStage.Snapshot,
                    SaveErrorCode.ProviderFailure,
                    $"Inventory snapshot could not be mapped: {exception.Message}",
                    priorCommittedRecordPreserved: true);
            }
        }

        public SaveResult<InventoryPersistenceLoadReceipt> Load(SaveSlotId slotId)
        {
            var loaded = _coordinator.LoadValidated(slotId, InventoryPersistenceDtoMapper.Validate);
            if (!loaded.Succeeded)
            {
                return SaveResult<InventoryPersistenceLoadReceipt>.Fail(
                    loaded.Error.Stage,
                    loaded.Error.Code,
                    loaded.Error.Message);
            }

            var inventoryEnvelope = InventoryPersistenceDtoMapper.ToEnvelope(loaded.Value.State);
            if (!inventoryEnvelope.Succeeded)
            {
                return SaveResult<InventoryPersistenceLoadReceipt>.Fail(
                    inventoryEnvelope.Error.Stage,
                    inventoryEnvelope.Error.Code,
                    inventoryEnvelope.Error.Message);
            }

            InventoryRestoreResult restore;
            try
            {
                restore = _inventory.Restore(inventoryEnvelope.Value, _inventoryMigrations);
            }
            catch (Exception exception)
            {
                return SaveResult<InventoryPersistenceLoadReceipt>.Fail(
                    SaveStage.Apply,
                    SaveErrorCode.ProviderFailure,
                    $"Inventory restore threw an exception: {exception.Message}");
            }

            if (!restore.Succeeded)
            {
                return SaveResult<InventoryPersistenceLoadReceipt>.Fail(
                    SaveStage.Apply,
                    ToSaveErrorCode(restore.Failure),
                    $"Inventory restore failed ({restore.Failure}): {restore.Message}");
            }

            return SaveResult<InventoryPersistenceLoadReceipt>.Success(
                new InventoryPersistenceLoadReceipt(
                    loaded.Value.State.InventorySchemaVersion,
                    loaded.Value.SelectedCandidateKind,
                    loaded.Value.SelectedCandidateId,
                    loaded.Value.RecoveryOccurred,
                    loaded.Value.PrimaryFailureDiagnostic,
                    restore));
        }

        private static SaveErrorCode ToSaveErrorCode(InventoryRestoreFailure failure)
        {
            switch (failure)
            {
                case InventoryRestoreFailure.UnsupportedSchema:
                    return SaveErrorCode.FutureSchema;
                case InventoryRestoreFailure.MissingMigration:
                    return SaveErrorCode.MissingMigration;
                case InventoryRestoreFailure.MigrationFailed:
                    return SaveErrorCode.ProviderFailure;
                default:
                    return SaveErrorCode.ValidateRejected;
            }
        }
    }
}
