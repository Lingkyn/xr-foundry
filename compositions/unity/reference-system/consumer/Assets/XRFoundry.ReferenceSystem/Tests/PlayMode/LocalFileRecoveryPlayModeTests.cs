using System;
using System.Collections;
using System.IO;
using System.Linq;
using Lingkyn.Inventory.Core;
using Lingkyn.Persistence.Core;
using Lingkyn.Persistence.Unity;
using NUnit.Framework;
using UnityEngine.TestTools;
using XRFoundry.ReferenceSystem.Bindings;

namespace XRFoundry.ReferenceSystem.Tests
{
    public sealed class LocalFileRecoveryPlayModeTests
    {
        private static readonly InventoryId PlayerInventoryId = new InventoryId("player");
        private static readonly ContainerId BagId = new ContainerId("bag");
        private static readonly ItemDefinitionId PotionId = new ItemDefinitionId("potion");
        private static readonly SlotAddress FirstSlot = new SlotAddress(BagId, 0);

        [UnityTest]
        public IEnumerator ZeroBytePrimaryRecoversFromValidLocalFileBackupWithoutLeakingPrimaryState()
        {
            var tempRoot = Path.Combine(
                Path.GetTempPath(),
                "xr-foundry-reference-recovery-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            try
            {
                var inventory = CreateInventory();
                SeedPotion(inventory, 2);
                var store = new LocalFileSaveStore(
                    new InjectedPersistentDataRootProvider(tempRoot),
                    "saves",
                    ".save",
                    LocalFileCommitStrategy.BestEffortDirectWrite);
                var persistence = new InventoryToPersistenceAdapter(
                    inventory,
                    CreateSaveCoordinator(store));
                var saveSlot = MustSaveSlot("playmode_local_file_recovery");
                var committed = persistence.Save(saveSlot);
                Assert.That(committed.Committed, Is.True, committed.Error.Message);

                var savedRevision = inventory.Revision;
                var savedQuantity = Total(inventory);
                var saveDirectory = Path.Combine(tempRoot, "saves");
                var primaryPath = Path.Combine(saveDirectory, saveSlot.Value + ".save");
                var backupPath = Path.Combine(saveDirectory, saveSlot.Value + ".backup.save");
                var validBackup = File.ReadAllBytes(primaryPath);
                File.WriteAllBytes(backupPath, validBackup);
                File.WriteAllBytes(primaryPath, Array.Empty<byte>());

                var removed = inventory.Execute(MutationRequest.Remove(FirstSlot, 1));
                Assert.That(removed.Succeeded, Is.True, removed.Message);
                var changedRevision = inventory.Revision;
                Assert.That(Total(inventory), Is.EqualTo(savedQuantity - 1));

                yield return null;

                var loaded = persistence.Load(saveSlot);

                Assert.That(loaded.Succeeded, Is.True, loaded.Error.Message);
                Assert.That(loaded.Value.SelectedCandidateKind, Is.EqualTo(SaveCandidateKind.Backup));
                Assert.That(loaded.Value.SelectedCandidateId.Value, Is.EqualTo("backup"));
                Assert.That(loaded.Value.RecoveryOccurred, Is.True);
                Assert.That(loaded.Value.PrimaryFailureDiagnostic, Is.Not.Null);
                Assert.That(loaded.Value.PrimaryFailureDiagnostic.Value.Stage, Is.EqualTo(SaveStage.Envelope));
                Assert.That(loaded.Value.PrimaryFailureDiagnostic.Value.Code, Is.EqualTo(SaveErrorCode.UnsupportedFormat));
                Assert.That(loaded.Value.RestoreResult.Succeeded, Is.True);
                Assert.That(loaded.Value.RestoreResult.RevisionBefore, Is.EqualTo(changedRevision));
                Assert.That(inventory.Revision, Is.EqualTo(savedRevision));
                Assert.That(Total(inventory), Is.EqualTo(savedQuantity));
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            }
        }

        private static SaveCoordinator<InventoryPersistenceDto> CreateSaveCoordinator(ISaveStore store)
        {
            return new SaveCoordinator<InventoryPersistenceDto>(
                InventoryToPersistenceAdapter.OuterPersistenceSchemaId,
                InventoryToPersistenceAdapter.OuterPersistenceSchemaVersion,
                "playmode-local-file-recovery",
                new JsonUtilitySaveCodec<InventoryPersistenceDto>(),
                new Sha256IntegrityProvider(),
                new MigrationPipeline<InventoryPersistenceDto>(Array.Empty<ISaveMigration<InventoryPersistenceDto>>()),
                store,
                SaveCommitCapabilities.BestEffortWrite,
                recoveryPolicy: SaveRecoveryPolicy.PrimaryThenBackup);
        }

        private static InventoryAggregate CreateInventory()
        {
            return new InventoryAggregate(
                PlayerInventoryId,
                new ItemDefinitionCatalog(new[]
                {
                    new ItemDefinition(PotionId, 5, ItemInstanceMode.Fungible),
                }),
                new[] { new ContainerDefinition(BagId, 2) });
        }

        private static void SeedPotion(InventoryAggregate inventory, int quantity)
        {
            var result = inventory.Execute(MutationRequest.Add(new ItemStack(PotionId, quantity), BagId));
            Assert.That(result.Succeeded, Is.True, result.Message);
        }

        private static int Total(InventoryAggregate inventory)
        {
            return inventory.GetSnapshot().Containers
                .SelectMany(container => container.Slots)
                .Where(stack => stack != null)
                .Sum(stack => stack.Quantity);
        }

        private static SaveSlotId MustSaveSlot(string value)
        {
            var result = SaveSlotId.TryCreate(value);
            Assert.That(result.Succeeded, Is.True, result.Error.Message);
            return result.Value;
        }
    }
}
