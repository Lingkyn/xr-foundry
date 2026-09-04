using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Lingkyn.Inventory.Core;
using Lingkyn.Persistence.Core;
using NUnit.Framework;
using UnityEngine;
using XRFoundry.ReferenceSystem.Bindings;

namespace XRFoundry.ReferenceSystem.Tests
{
    public sealed class InventoryToPersistenceAdapterTests
    {
        private static readonly InventoryId InventoryId = new InventoryId("player");
        private static readonly ContainerId BagId = new ContainerId("bag");
        private static readonly ItemDefinitionId PotionId = new ItemDefinitionId("potion");
        private static readonly ItemDefinitionId SwordId = new ItemDefinitionId("sword");

        [Test]
        public void SaveAndLoadRoundTripPreservesEmptyUniqueAndStateFragmentSlots()
        {
            var fragmentCodec = new DurabilityFragmentCodec();
            var fragmentRegistry = new ItemStateFragmentRegistry();
            fragmentRegistry.Register(fragmentCodec);
            var inventory = CreateInventory(fragmentRegistry);
            Assert.That(
                inventory.Execute(MutationRequest.Add(new ItemStack(PotionId, 3), BagId)).Succeeded,
                Is.True);
            var durability = fragmentRegistry.Create(fragmentCodec, new DurabilityState(73));
            Assert.That(
                inventory.Execute(MutationRequest.Add(
                    new ItemStack(
                        SwordId,
                        1,
                        new ItemInstanceId("sword-001"),
                        new[] { durability }),
                    BagId)).Succeeded,
                Is.True);

            var codec = new JsonUtilityInventoryPersistenceCodec();
            var store = new InMemoryInventoryPersistenceStore();
            var coordinator = CreateCoordinator(codec, store);
            var adapter = new InventoryToPersistenceAdapter(inventory, coordinator);
            var slot = MustSlot("inventory_roundtrip");
            var savedRevision = inventory.Revision;

            var save = adapter.Save(slot);
            Assert.That(save.Committed, Is.True);
            Assert.That(inventory.Execute(MutationRequest.Remove(new SlotAddress(BagId, 0), 3)).Succeeded, Is.True);
            Assert.That(inventory.Execute(MutationRequest.Remove(new SlotAddress(BagId, 1), 1)).Succeeded, Is.True);

            var load = adapter.Load(slot);

            Assert.That(load.Succeeded, Is.True, load.Error.Message);
            Assert.That(load.Value.SelectedCandidateKind, Is.EqualTo(SaveCandidateKind.Primary));
            Assert.That(load.Value.SelectedCandidateId.Value, Is.EqualTo("primary"));
            Assert.That(load.Value.RecoveryOccurred, Is.False);
            Assert.That(load.Value.PrimaryFailureDiagnostic, Is.Null);
            Assert.That(load.Value.RestoreResult.Succeeded, Is.True);
            Assert.That(load.Value.InventorySchemaVersion, Is.EqualTo(InventoryPersistence.CurrentSchemaVersion));
            Assert.That(codec.LastDecodedOuterSchemaVersion, Is.EqualTo(InventoryToPersistenceAdapter.OuterPersistenceSchemaVersion));
            Assert.That(codec.LastDecodedOuterSchemaVersion, Is.Not.EqualTo(load.Value.InventorySchemaVersion));
            Assert.That(codec.LastDecodedDto.InventoryState.Containers[0].Slots[0].InstanceId, Is.EqualTo(string.Empty));
            Assert.That(inventory.Revision, Is.EqualTo(savedRevision));

            var snapshot = inventory.GetSnapshot();
            var potion = snapshot.Get(new SlotAddress(BagId, 0));
            Assert.That(potion.Quantity, Is.EqualTo(3));
            Assert.That(potion.InstanceId, Is.Null);
            var sword = snapshot.Get(new SlotAddress(BagId, 1));
            Assert.That(sword.InstanceId.Value.Value, Is.EqualTo("sword-001"));
            Assert.That(sword.TryGetState(fragmentCodec.TypeId, out var restoredFragment), Is.True);
            Assert.That(fragmentRegistry.Read(fragmentCodec, restoredFragment).Value, Is.EqualTo(73));
            Assert.That(snapshot.Get(new SlotAddress(BagId, 2)), Is.Null);
            Assert.That(snapshot.Get(new SlotAddress(BagId, 3)), Is.Null);
        }

        [Test]
        public void LoadNotFoundLeavesInventoryUnchanged()
        {
            var inventory = CreateInventory();
            SeedPotion(inventory, 2);
            var before = Describe(inventory);
            var adapter = new InventoryToPersistenceAdapter(
                inventory,
                CreateCoordinator(
                    new JsonUtilityInventoryPersistenceCodec(),
                    new InMemoryInventoryPersistenceStore()));

            var result = adapter.Load(MustSlot("inventory_missing"));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error.Stage, Is.EqualTo(SaveStage.Read));
            Assert.That(result.Error.Code, Is.EqualTo(SaveErrorCode.NotFound));
            Assert.That(Describe(inventory), Is.EqualTo(before));
        }

        [Test]
        public void DecodeFailureLeavesInventoryUnchanged()
        {
            var inventory = CreateInventory();
            SeedPotion(inventory, 2);
            var codec = new JsonUtilityInventoryPersistenceCodec();
            var store = new InMemoryInventoryPersistenceStore();
            var adapter = new InventoryToPersistenceAdapter(inventory, CreateCoordinator(codec, store));
            var slot = MustSlot("inventory_decode_failure");
            Assert.That(adapter.Save(slot).Committed, Is.True);
            Assert.That(inventory.Execute(MutationRequest.Add(new ItemStack(PotionId, 1), BagId)).Succeeded, Is.True);
            var before = Describe(inventory);
            codec.ReturnDecodeFailure = true;

            var result = adapter.Load(slot);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error.Stage, Is.EqualTo(SaveStage.Decode));
            Assert.That(result.Error.Code, Is.EqualTo(SaveErrorCode.UnsupportedFormat));
            Assert.That(Describe(inventory), Is.EqualTo(before));
        }

        [Test]
        public void StructurallyInvalidDtoIsRejectedBeforeRestoreAndLeavesInventoryUnchanged()
        {
            var inventory = CreateInventory();
            SeedPotion(inventory, 2);
            var codec = new JsonUtilityInventoryPersistenceCodec();
            var store = new InMemoryInventoryPersistenceStore();
            var coordinator = CreateCoordinator(codec, store);
            var adapter = new InventoryToPersistenceAdapter(inventory, coordinator);
            var slot = MustSlot("inventory_invalid_dto");
            Assert.That(adapter.Save(slot).Committed, Is.True);
            var invalid = InventoryPersistenceDtoMapper.FromSnapshot(inventory.GetSnapshot());
            invalid.InventoryState.Containers = null;
            codec.DecodeOverride = invalid;
            var before = Describe(inventory);

            var result = adapter.Load(slot);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error.Stage, Is.EqualTo(SaveStage.Validate));
            Assert.That(result.Error.Code, Is.EqualTo(SaveErrorCode.ValidateRejected));
            Assert.That(Describe(inventory), Is.EqualTo(before));
        }

        [Test]
        public void NonEmptyWhitespaceOptionalInstanceIdIsRejectedWithoutMutation()
        {
            var inventory = CreateInventory();
            SeedPotion(inventory, 2);
            var codec = new JsonUtilityInventoryPersistenceCodec();
            var store = new InMemoryInventoryPersistenceStore();
            var coordinator = CreateCoordinator(codec, store);
            var adapter = new InventoryToPersistenceAdapter(inventory, coordinator);
            var slot = MustSlot("inventory_whitespace_instance");
            Assert.That(adapter.Save(slot).Committed, Is.True);
            var whitespaceInstance = InventoryPersistenceDtoMapper.FromSnapshot(inventory.GetSnapshot());
            whitespaceInstance.InventoryState.Containers[0].Slots[0].InstanceId = " ";
            codec.DecodeOverride = whitespaceInstance;
            var before = Describe(inventory);

            var result = adapter.Load(slot);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error.Stage, Is.EqualTo(SaveStage.Validate));
            Assert.That(result.Error.Code, Is.EqualTo(SaveErrorCode.ValidateRejected));
            StringAssert.Contains("instance id cannot be whitespace", result.Error.Message);
            Assert.That(Describe(inventory), Is.EqualTo(before));
        }

        [Test]
        public void DomainRestoreRejectionIsAtomicAndReportedAtApplyStage()
        {
            var inventory = CreateInventory();
            SeedPotion(inventory, 2);
            var codec = new JsonUtilityInventoryPersistenceCodec();
            var store = new InMemoryInventoryPersistenceStore();
            var coordinator = CreateCoordinator(codec, store);
            var adapter = new InventoryToPersistenceAdapter(inventory, coordinator);
            var slot = MustSlot("inventory_restore_failure");
            Assert.That(adapter.Save(slot).Committed, Is.True);
            var domainInvalid = InventoryPersistenceDtoMapper.FromSnapshot(inventory.GetSnapshot());
            domainInvalid.InventoryState.Containers[0].Slots[0].DefinitionId = "missing-definition";
            codec.DecodeOverride = domainInvalid;
            var before = Describe(inventory);

            var result = adapter.Load(slot);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error.Stage, Is.EqualTo(SaveStage.Apply));
            Assert.That(result.Error.Code, Is.EqualTo(SaveErrorCode.ValidateRejected));
            StringAssert.Contains("MissingDefinition", result.Error.Message);
            Assert.That(Describe(inventory), Is.EqualTo(before));
        }

        [Test]
        public void SavePassesCommitFailureThroughWithoutChangingItsMeaning()
        {
            var inventory = CreateInventory();
            SeedPotion(inventory, 1);
            var store = new InMemoryInventoryPersistenceStore
            {
                ForcedCommitResult = SaveCommitResult.NotCommitted(
                    SaveStage.Commit,
                    SaveErrorCode.OutOfSpace,
                    "capacity exhausted",
                    priorCommittedRecordPreserved: true),
            };
            var adapter = new InventoryToPersistenceAdapter(
                inventory,
                CreateCoordinator(new JsonUtilityInventoryPersistenceCodec(), store));

            var result = adapter.Save(MustSlot("inventory_commit_failure"));

            Assert.That(result.Committed, Is.False);
            Assert.That(result.PriorCommittedRecordPreserved, Is.True);
            Assert.That(result.Error.Stage, Is.EqualTo(SaveStage.Commit));
            Assert.That(result.Error.Code, Is.EqualTo(SaveErrorCode.OutOfSpace));
            Assert.That(result.Error.Message, Is.EqualTo("capacity exhausted"));
        }

        [Test]
        public void CorruptPrimaryFallsBackToBackupAndPreservesRecoveryReceipt()
        {
            var inventory = CreateInventory();
            SeedPotion(inventory, 2);
            var codec = new JsonUtilityInventoryPersistenceCodec();
            var store = new InMemoryInventoryPersistenceStore();
            var coordinator = CreateCoordinator(codec, store, SaveRecoveryPolicy.PrimaryThenBackup);
            var adapter = new InventoryToPersistenceAdapter(inventory, coordinator);
            var slot = MustSlot("inventory_recovery");
            Assert.That(adapter.Save(slot).Committed, Is.True);
            var backupBytes = store.CopyLastCommittedEnvelopeBytes();

            Assert.That(inventory.Execute(MutationRequest.Add(new ItemStack(PotionId, 1), BagId)).Succeeded, Is.True);
            var beforeLoad = Describe(inventory);
            var corruptPrimary = (byte[])backupBytes.Clone();
            corruptPrimary[corruptPrimary.Length - 1] ^= 0x40;
            store.ReplaceCandidates(
                Candidate(SaveCandidateKind.Primary, "primary", corruptPrimary),
                Candidate(SaveCandidateKind.Backup, "backup", backupBytes));

            var result = adapter.Load(slot);

            Assert.That(result.Succeeded, Is.True, result.Error.Message);
            Assert.That(result.Value.SelectedCandidateKind, Is.EqualTo(SaveCandidateKind.Backup));
            Assert.That(result.Value.SelectedCandidateId.Value, Is.EqualTo("backup"));
            Assert.That(result.Value.RecoveryOccurred, Is.True);
            Assert.That(result.Value.PrimaryFailureDiagnostic, Is.Not.Null);
            Assert.That(result.Value.PrimaryFailureDiagnostic.Value.Stage, Is.EqualTo(SaveStage.Verify));
            Assert.That(result.Value.PrimaryFailureDiagnostic.Value.Code, Is.EqualTo(SaveErrorCode.CorruptPayload));
            Assert.That(result.Value.RestoreResult.Succeeded, Is.True);
            Assert.That(Total(inventory), Is.EqualTo(2));
            Assert.That(Describe(inventory), Is.Not.EqualTo(beforeLoad));
        }

        [Test]
        public void OptionalInventoryMigrationRunsInsideAtomicRestore()
        {
            var inventory = CreateInventory();
            var codec = new JsonUtilityInventoryPersistenceCodec();
            var store = new InMemoryInventoryPersistenceStore();
            var coordinator = CreateCoordinator(codec, store);
            var adapter = new InventoryToPersistenceAdapter(
                inventory,
                coordinator,
                new LegacyInventoryMigration());
            var legacyDto = new InventoryPersistenceDto
            {
                InventorySchemaVersion = 1,
                InventoryState = new InventoryStatePersistenceDto
                {
                    InventoryId = "player",
                    Revision = 7,
                    Containers = new[]
                    {
                        new InventoryContainerPersistenceDto
                        {
                            ContainerId = "legacy-bag",
                            Slots = new[]
                            {
                                OccupiedSlot("legacy-potion", 3),
                                EmptySlot(),
                                EmptySlot(),
                                EmptySlot(),
                            },
                        },
                    },
                },
            };
            var slot = MustSlot("inventory_migration");
            Assert.That(coordinator.Save(slot, legacyDto).Committed, Is.True);

            var result = adapter.Load(slot);

            Assert.That(result.Succeeded, Is.True, result.Error.Message);
            Assert.That(result.Value.InventorySchemaVersion, Is.EqualTo(1));
            Assert.That(result.Value.RestoreResult.SchemaVersionBefore, Is.EqualTo(1));
            Assert.That(result.Value.RestoreResult.SchemaVersionAfter, Is.EqualTo(InventoryPersistence.CurrentSchemaVersion));
            Assert.That(inventory.Revision, Is.EqualTo(7));
            Assert.That(inventory.GetSnapshot().Get(new SlotAddress(BagId, 0)).DefinitionId, Is.EqualTo(PotionId));
            Assert.That(inventory.GetSnapshot().Get(new SlotAddress(BagId, 0)).Quantity, Is.EqualTo(3));
        }

        private static SaveCoordinator<InventoryPersistenceDto> CreateCoordinator(
            JsonUtilityInventoryPersistenceCodec codec,
            InMemoryInventoryPersistenceStore store,
            SaveRecoveryPolicy recoveryPolicy = SaveRecoveryPolicy.PrimaryOnly)
        {
            return new SaveCoordinator<InventoryPersistenceDto>(
                InventoryToPersistenceAdapter.OuterPersistenceSchemaId,
                InventoryToPersistenceAdapter.OuterPersistenceSchemaVersion,
                "test-commit",
                codec,
                new Sha256IntegrityProvider(),
                new MigrationPipeline<InventoryPersistenceDto>(Array.Empty<ISaveMigration<InventoryPersistenceDto>>()),
                store,
                SaveCommitCapabilities.BestEffortWrite,
                recoveryPolicy: recoveryPolicy);
        }

        private static InventoryAggregate CreateInventory(ItemStateFragmentRegistry fragmentRegistry = null)
        {
            return new InventoryAggregate(
                InventoryId,
                new ItemDefinitionCatalog(new[]
                {
                    new ItemDefinition(PotionId, 5, ItemInstanceMode.Fungible),
                    new ItemDefinition(SwordId, 1, ItemInstanceMode.Unique),
                }),
                new[] { new ContainerDefinition(BagId, 4) },
                stateFragmentRegistry: fragmentRegistry);
        }

        private static void SeedPotion(InventoryAggregate inventory, int quantity)
        {
            Assert.That(
                inventory.Execute(MutationRequest.Add(new ItemStack(PotionId, quantity), BagId)).Succeeded,
                Is.True);
        }

        private static int Total(InventoryAggregate inventory)
        {
            return inventory.GetSnapshot().Containers
                .SelectMany(container => container.Slots)
                .Where(stack => stack != null)
                .Sum(stack => stack.Quantity);
        }

        private static string Describe(InventoryAggregate inventory)
        {
            var snapshot = inventory.GetSnapshot();
            return $"revision={snapshot.Revision}|" + string.Join("|", snapshot.Containers
                .OrderBy(container => container.Id.Value, StringComparer.Ordinal)
                .SelectMany(container => container.Slots.Select((stack, index) =>
                    stack == null
                        ? $"{container.Id.Value}:{index}:empty"
                        : $"{container.Id.Value}:{index}:{stack.DefinitionId.Value}:{stack.Quantity}:{stack.InstanceId?.Value}:"
                          + string.Join(",", stack.StateFragments.Select(fragment =>
                              $"{fragment.TypeId.Value}@{fragment.SchemaVersion}={fragment.Payload}")))));
        }

        private static SaveSlotId MustSlot(string value)
        {
            var result = SaveSlotId.TryCreate(value);
            Assert.That(result.Succeeded, Is.True, result.Error.Message);
            return result.Value;
        }

        private static SaveReadCandidate Candidate(SaveCandidateKind kind, string id, byte[] bytes)
        {
            var candidateId = SaveCandidateId.TryCreate(id);
            Assert.That(candidateId.Succeeded, Is.True, candidateId.Error.Message);
            return new SaveReadCandidate(kind, candidateId.Value, bytes);
        }

        private static InventorySlotPersistenceDto OccupiedSlot(string definitionId, int quantity)
        {
            return new InventorySlotPersistenceDto
            {
                Occupied = true,
                DefinitionId = definitionId,
                Quantity = quantity,
                StateFragments = Array.Empty<InventoryStateFragmentPersistenceDto>(),
            };
        }

        private static InventorySlotPersistenceDto EmptySlot()
        {
            return new InventorySlotPersistenceDto
            {
                Occupied = false,
                Quantity = 0,
                StateFragments = Array.Empty<InventoryStateFragmentPersistenceDto>(),
            };
        }

        private sealed class LegacyInventoryMigration : IInventoryStateMigration
        {
            public int FromVersion => 1;
            public int ToVersion => 2;

            public InventoryStateData Migrate(InventoryStateData source)
            {
                return new InventoryStateData(
                    source.InventoryId,
                    source.Revision,
                    source.Containers.Select(container => new InventoryContainerState(
                        container.ContainerId == "legacy-bag" ? "bag" : container.ContainerId,
                        container.Slots.Select(slot => slot == null
                            ? null
                            : new InventorySlotState(
                                slot.DefinitionId == "legacy-potion" ? "potion" : slot.DefinitionId,
                                slot.Quantity,
                                slot.InstanceId,
                                slot.StateFragments)))));
            }
        }

        private sealed class DurabilityState
        {
            public DurabilityState(int value)
            {
                Value = value;
            }

            public int Value { get; }
        }

        private sealed class DurabilityFragmentCodec : ItemStateFragmentCodec<DurabilityState>
        {
            public DurabilityFragmentCodec()
                : base(new ItemStateFragmentTypeId("durability"), 1)
            {
            }

            public override string Encode(DurabilityState value)
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                return value.Value.ToString(CultureInfo.InvariantCulture);
            }

            public override DurabilityState Decode(int schemaVersion, string payload)
            {
                if (schemaVersion != 1)
                {
                    throw new InvalidOperationException($"Unsupported durability schema {schemaVersion}.");
                }

                return new DurabilityState(int.Parse(payload, CultureInfo.InvariantCulture));
            }
        }
    }

    public sealed class JsonUtilityInventoryPersistenceCodec : ISaveCodec<InventoryPersistenceDto>
    {
        public bool ReturnDecodeFailure { get; set; }
        public int LastDecodedOuterSchemaVersion { get; private set; } = -1;
        public InventoryPersistenceDto LastDecodedDto { get; private set; }
        public InventoryPersistenceDto DecodeOverride { get; set; }

        public SaveResult<byte[]> Encode(InventoryPersistenceDto snapshot)
        {
            if (snapshot == null)
            {
                return SaveResult<byte[]>.Fail(
                    SaveStage.Encode,
                    SaveErrorCode.UnsupportedFormat,
                    "Inventory DTO is required.");
            }

            try
            {
                return SaveResult<byte[]>.Success(Encoding.UTF8.GetBytes(JsonUtility.ToJson(snapshot)));
            }
            catch (Exception exception)
            {
                return SaveResult<byte[]>.Fail(
                    SaveStage.Encode,
                    SaveErrorCode.UnsupportedFormat,
                    $"Inventory DTO JSON encode failed: {exception.Message}");
            }
        }

        public SaveResult<InventoryPersistenceDto> Decode(int schemaVersion, ReadOnlySpan<byte> bytes)
        {
            LastDecodedOuterSchemaVersion = schemaVersion;
            if (ReturnDecodeFailure)
            {
                return SaveResult<InventoryPersistenceDto>.Fail(
                    SaveStage.Decode,
                    SaveErrorCode.UnsupportedFormat,
                    "Injected DTO decode failure.");
            }

            if (DecodeOverride != null)
            {
                LastDecodedDto = DecodeOverride;
                return SaveResult<InventoryPersistenceDto>.Success(DecodeOverride);
            }

            try
            {
                var dto = JsonUtility.FromJson<InventoryPersistenceDto>(Encoding.UTF8.GetString(bytes.ToArray()));
                LastDecodedDto = dto;
                return dto == null
                    ? SaveResult<InventoryPersistenceDto>.Fail(
                        SaveStage.Decode,
                        SaveErrorCode.UnsupportedFormat,
                        "Inventory DTO JSON contained no object.")
                    : SaveResult<InventoryPersistenceDto>.Success(dto);
            }
            catch (Exception exception)
            {
                return SaveResult<InventoryPersistenceDto>.Fail(
                    SaveStage.Decode,
                    SaveErrorCode.UnsupportedFormat,
                    $"Inventory DTO JSON decode failed: {exception.Message}");
            }
        }
    }

    public sealed class InMemoryInventoryPersistenceStore : ISaveStore
    {
        private readonly List<SaveReadCandidate> _candidates = new List<SaveReadCandidate>();
        private byte[] _lastCommittedEnvelopeBytes = Array.Empty<byte>();

        public SaveCommitCapabilities Capabilities { get; set; } = SaveCommitCapabilities.BestEffortWrite;
        public SaveCommitResult? ForcedCommitResult { get; set; }

        public SaveResult<SaveReadCandidateSet> ReadCandidates(SaveSlotId slotId)
        {
            return _candidates.Count == 0
                ? SaveResult<SaveReadCandidateSet>.Fail(
                    SaveStage.Read,
                    SaveErrorCode.NotFound,
                    "No in-memory save exists.")
                : SaveResult<SaveReadCandidateSet>.Success(new SaveReadCandidateSet(_candidates.ToArray()));
        }

        public SaveCommitResult Commit(
            SaveSlotId slotId,
            ReadOnlyMemory<byte> envelopeBytes,
            SaveCommitCapabilities requiredCapabilities)
        {
            if (ForcedCommitResult.HasValue)
            {
                return ForcedCommitResult.Value;
            }

            _lastCommittedEnvelopeBytes = envelopeBytes.ToArray();
            var primaryId = SaveCandidateId.TryCreate("primary");
            if (!primaryId.Succeeded)
            {
                return SaveCommitResult.NotCommitted(
                    primaryId.Error.Stage,
                    primaryId.Error.Code,
                    primaryId.Error.Message,
                    priorCommittedRecordPreserved: true);
            }

            _candidates.Clear();
            _candidates.Add(new SaveReadCandidate(
                SaveCandidateKind.Primary,
                primaryId.Value,
                _lastCommittedEnvelopeBytes));
            return SaveCommitResult.Success();
        }

        public byte[] CopyLastCommittedEnvelopeBytes()
        {
            return (byte[])_lastCommittedEnvelopeBytes.Clone();
        }

        public void ReplaceCandidates(params SaveReadCandidate[] candidates)
        {
            _candidates.Clear();
            if (candidates != null)
            {
                _candidates.AddRange(candidates);
            }
        }
    }
}
