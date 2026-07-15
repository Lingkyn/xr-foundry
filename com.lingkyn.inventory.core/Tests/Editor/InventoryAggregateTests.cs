using System;
using System.Linq;
using NUnit.Framework;

namespace Lingkyn.Inventory.Core.Tests
{
    public sealed class InventoryAggregateTests
    {
        private static readonly ItemDefinitionId PotionId = new ItemDefinitionId("potion");
        private static readonly ItemDefinitionId SwordId = new ItemDefinitionId("sword");
        private static readonly ContainerId BagId = new ContainerId("bag");
        private static readonly ContainerId StashId = new ContainerId("stash");

        [Test]
        public void AddFillsStacksAndFailedOverflowIsAtomic()
        {
            var inventory = CreateInventory(bagCapacity: 2);

            var first = inventory.Execute(MutationRequest.Add(new ItemStack(PotionId, 8), BagId));
            Assert.That(first.Succeeded, Is.True);
            Assert.That(inventory.GetSnapshot().Get(new SlotAddress(BagId, 0)).Quantity, Is.EqualTo(5));
            Assert.That(inventory.GetSnapshot().Get(new SlotAddress(BagId, 1)).Quantity, Is.EqualTo(3));

            var beforeFailure = Describe(inventory.GetSnapshot());
            var failure = inventory.Execute(MutationRequest.Add(new ItemStack(PotionId, 3), BagId));
            Assert.That(failure.Succeeded, Is.False);
            Assert.That(failure.Failure, Is.EqualTo(MutationFailure.CapacityExceeded));
            Assert.That(failure.RevisionAfter, Is.EqualTo(first.RevisionAfter));
            Assert.That(Describe(inventory.GetSnapshot()), Is.EqualTo(beforeFailure));

            var partial = inventory.Execute(MutationRequest.Add(new ItemStack(PotionId, 3), BagId, allowPartial: true));
            Assert.That(partial.Succeeded, Is.True);
            Assert.That(partial.AcceptedQuantity, Is.EqualTo(2));
            Assert.That(partial.RemainderQuantity, Is.EqualTo(1));
        }

        [Test]
        public void UniqueInstancesRequireIdentityAndRejectDuplicates()
        {
            var inventory = CreateInventory(bagCapacity: 2);
            var instanceId = new ItemInstanceId("sword-001");

            var missingIdentity = inventory.Execute(MutationRequest.Add(new ItemStack(SwordId, 1), BagId));
            Assert.That(missingIdentity.Failure, Is.EqualTo(MutationFailure.InvalidRequest));

            var first = inventory.Execute(MutationRequest.Add(new ItemStack(SwordId, 1, instanceId), BagId));
            Assert.That(first.Succeeded, Is.True);

            var duplicate = inventory.Execute(MutationRequest.Add(new ItemStack(SwordId, 1, instanceId), BagId));
            Assert.That(duplicate.Succeeded, Is.False);
            Assert.That(duplicate.Failure, Is.EqualTo(MutationFailure.DuplicateInstance));
        }

        [Test]
        public void SplitAndMergeConserveQuantity()
        {
            var inventory = CreateInventory(bagCapacity: 2);
            inventory.Execute(MutationRequest.Add(new ItemStack(PotionId, 5), BagId));

            var split = inventory.Execute(MutationRequest.Split(
                new SlotAddress(BagId, 0),
                new SlotAddress(BagId, 1),
                2));
            Assert.That(split.Succeeded, Is.True);
            Assert.That(Total(inventory.GetSnapshot()), Is.EqualTo(5));
            Assert.That(inventory.GetSnapshot().Get(new SlotAddress(BagId, 0)).Quantity, Is.EqualTo(3));
            Assert.That(inventory.GetSnapshot().Get(new SlotAddress(BagId, 1)).Quantity, Is.EqualTo(2));

            var merge = inventory.Execute(MutationRequest.Merge(
                new SlotAddress(BagId, 1),
                new SlotAddress(BagId, 0),
                2));
            Assert.That(merge.Succeeded, Is.True);
            Assert.That(Total(inventory.GetSnapshot()), Is.EqualTo(5));
            Assert.That(inventory.GetSnapshot().Get(new SlotAddress(BagId, 1)), Is.Null);
        }

        [Test]
        public void MoveSwapAndTransferHaveExplicitSemantics()
        {
            var inventory = CreateInventory(bagCapacity: 3, stashCapacity: 2);
            inventory.Execute(MutationRequest.Add(new ItemStack(PotionId, 4), BagId));
            inventory.Execute(MutationRequest.Add(new ItemStack(SwordId, 1, new ItemInstanceId("sword-001")), BagId));

            var move = inventory.Execute(MutationRequest.Move(
                new SlotAddress(BagId, 0),
                new SlotAddress(BagId, 2),
                2));
            Assert.That(move.Succeeded, Is.True);

            var swap = inventory.Execute(MutationRequest.Swap(
                new SlotAddress(BagId, 1),
                new SlotAddress(BagId, 2)));
            Assert.That(swap.Succeeded, Is.True);
            Assert.That(inventory.GetSnapshot().Get(new SlotAddress(BagId, 2)).InstanceId.Value.Value, Is.EqualTo("sword-001"));

            var transfer = inventory.Execute(MutationRequest.Transfer(
                new SlotAddress(BagId, 0),
                new SlotAddress(StashId, 0),
                2));
            Assert.That(transfer.Succeeded, Is.True);
            Assert.That(inventory.GetSnapshot().Get(new SlotAddress(StashId, 0)).Quantity, Is.EqualTo(2));
        }

        [Test]
        public void RejectedPolicyLeavesStateAndRevisionUnchanged()
        {
            var inventory = CreateInventory(bagCapacity: 2, policies: new[] { new RejectAllPolicy() });
            var eventCount = 0;
            inventory.Changed += _ => eventCount++;

            var result = inventory.Execute(MutationRequest.Add(new ItemStack(PotionId, 1), BagId));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Failure, Is.EqualTo(MutationFailure.PolicyRejected));
            Assert.That(inventory.Revision, Is.Zero);
            Assert.That(eventCount, Is.Zero);
        }

        [Test]
        public void PolicyCanInspectDefinitionMetadataWithoutConsumerCoupling()
        {
            var inventory = CreateInventory(bagCapacity: 2, policies: new[] { new RequireTagForAddsPolicy("consumable") });

            var potion = inventory.Execute(MutationRequest.Add(new ItemStack(PotionId, 1), BagId));
            var sword = inventory.Execute(MutationRequest.Add(
                new ItemStack(SwordId, 1, new ItemInstanceId("sword-001")),
                BagId));

            Assert.That(potion.Succeeded, Is.True);
            Assert.That(sword.Failure, Is.EqualTo(MutationFailure.PolicyRejected));
        }

        [Test]
        public void ChangeEventObservesCommittedStateAndObserverFailureDoesNotUndoMutation()
        {
            var inventory = CreateInventory(bagCapacity: 1);
            long observedRevision = -1;
            var observerFaults = 0;
            inventory.Changed += inventoryEvent => observedRevision = inventory.GetSnapshot().Revision;
            inventory.Changed += _ => throw new InvalidOperationException("observer failure");
            inventory.ObserverFaulted += _ => observerFaults++;

            var result = inventory.Execute(MutationRequest.Add(new ItemStack(PotionId, 1), BagId));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(observedRevision, Is.EqualTo(1));
            Assert.That(observerFaults, Is.EqualTo(1));
            Assert.That(Total(inventory.GetSnapshot()), Is.EqualTo(1));
        }

        [Test]
        public void SnapshotsDoNotChangeAfterLaterMutations()
        {
            var inventory = CreateInventory(bagCapacity: 2);
            inventory.Execute(MutationRequest.Add(new ItemStack(PotionId, 2), BagId));
            var oldSnapshot = inventory.GetSnapshot();

            inventory.Execute(MutationRequest.Add(new ItemStack(PotionId, 2), BagId));

            Assert.That(Total(oldSnapshot), Is.EqualTo(2));
            Assert.That(Total(inventory.GetSnapshot()), Is.EqualTo(4));
            var envelope = new PersistenceEnvelope(1, oldSnapshot);
            Assert.That(envelope.Revision, Is.EqualTo(oldSnapshot.Revision));
        }

        [Test]
        public void DefaultIdentifiersCannotEnterAuthoredDomainObjects()
        {
            Assert.Throws<ArgumentException>(() =>
                new ItemDefinition(default, 1, ItemInstanceMode.Fungible));
            Assert.Throws<ArgumentException>(() =>
                new ContainerDefinition(default, 1));
            Assert.Throws<ArgumentException>(() =>
                new ItemStack(default, 1));
        }

        [Test]
        public void PersistenceRoundTripRestoresEquivalentStateAtomically()
        {
            var inventory = CreateInventory(bagCapacity: 3, stashCapacity: 2);
            inventory.Execute(MutationRequest.Add(new ItemStack(PotionId, 7), BagId));
            inventory.Execute(MutationRequest.Add(
                new ItemStack(SwordId, 1, new ItemInstanceId("sword-001")),
                StashId));
            var envelope = inventory.CreatePersistenceEnvelope();
            var expected = Describe(envelope.Snapshot);

            inventory.Execute(MutationRequest.Remove(new SlotAddress(BagId, 0), 3));
            var result = inventory.Restore(envelope);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.SchemaVersionAfter, Is.EqualTo(InventoryPersistence.CurrentSchemaVersion));
            Assert.That(Describe(inventory.GetSnapshot()), Is.EqualTo(expected));
            Assert.That(inventory.Revision, Is.EqualTo(envelope.Revision));
        }

        [Test]
        public void InvalidPersistenceStateLeavesInventoryAndRevisionUnchanged()
        {
            var inventory = CreateInventory(bagCapacity: 2);
            inventory.Execute(MutationRequest.Add(new ItemStack(PotionId, 2), BagId));
            var before = Describe(inventory.GetSnapshot());
            var revisionBefore = inventory.Revision;
            var invalid = new PersistenceEnvelope(
                InventoryPersistence.CurrentSchemaVersion,
                new InventoryStateData(
                    "player",
                    99,
                    new[]
                    {
                        new InventoryContainerState(
                            "bag",
                            new InventorySlotState[]
                            {
                                new InventorySlotState("potion", 99),
                                null,
                            }),
                    }));

            var result = inventory.Restore(invalid);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Failure, Is.EqualTo(InventoryRestoreFailure.InvalidStack));
            Assert.That(inventory.Revision, Is.EqualTo(revisionBefore));
            Assert.That(Describe(inventory.GetSnapshot()), Is.EqualTo(before));
        }

        [Test]
        public void RestoreRejectsDuplicateUniqueInstancesWithoutMutation()
        {
            var inventory = CreateInventory(bagCapacity: 2);
            var duplicate = new PersistenceEnvelope(
                InventoryPersistence.CurrentSchemaVersion,
                new InventoryStateData(
                    "player",
                    5,
                    new[]
                    {
                        new InventoryContainerState(
                            "bag",
                            new[]
                            {
                                new InventorySlotState("sword", 1, "same-instance"),
                                new InventorySlotState("sword", 1, "same-instance"),
                            }),
                    }));

            var result = inventory.Restore(duplicate);

            Assert.That(result.Failure, Is.EqualTo(InventoryRestoreFailure.DuplicateInstance));
            Assert.That(inventory.Revision, Is.Zero);
            Assert.That(Total(inventory.GetSnapshot()), Is.Zero);
        }

        [Test]
        public void PreviousSchemaRequiresAndRunsOneDeterministicMigration()
        {
            var inventory = CreateInventory(bagCapacity: 2);
            var previous = new PersistenceEnvelope(
                1,
                new InventoryStateData(
                    "player",
                    7,
                    new[]
                    {
                        new InventoryContainerState(
                            "legacy-bag",
                            new InventorySlotState[]
                            {
                                new InventorySlotState("legacy-potion", 3),
                                null,
                            }),
                    }));

            var missing = inventory.Restore(previous);
            Assert.That(missing.Failure, Is.EqualTo(InventoryRestoreFailure.MissingMigration));
            Assert.That(inventory.Revision, Is.Zero);

            var migrated = inventory.Restore(previous, new[] { new SchemaOneToTwoMigration() });

            Assert.That(migrated.Succeeded, Is.True);
            Assert.That(migrated.SchemaVersionBefore, Is.EqualTo(1));
            Assert.That(migrated.SchemaVersionAfter, Is.EqualTo(2));
            Assert.That(inventory.Revision, Is.EqualTo(7));
            Assert.That(inventory.GetSnapshot().Get(new SlotAddress(BagId, 0)).DefinitionId, Is.EqualTo(PotionId));
            Assert.That(inventory.GetSnapshot().Get(new SlotAddress(BagId, 0)).Quantity, Is.EqualTo(3));
        }

        [Test]
        public void RestoreObserversSeeCommittedStateAndCannotRollbackIt()
        {
            var inventory = CreateInventory(bagCapacity: 2);
            inventory.Execute(MutationRequest.Add(new ItemStack(PotionId, 2), BagId));
            var envelope = inventory.CreatePersistenceEnvelope();
            inventory.Execute(MutationRequest.Remove(new SlotAddress(BagId, 0), 2));
            InventorySnapshot observed = null;
            Exception observerFault = null;
            inventory.Restored += snapshot => observed = snapshot;
            inventory.Restored += _ => throw new InvalidOperationException("observer failure");
            inventory.ObserverFaulted += exception => observerFault = exception;

            var result = inventory.Restore(envelope);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(observed, Is.Not.Null);
            Assert.That(observed.Revision, Is.EqualTo(envelope.Revision));
            Assert.That(observed.Get(new SlotAddress(BagId, 0)).Quantity, Is.EqualTo(2));
            Assert.That(observerFault, Is.TypeOf<InvalidOperationException>());
            Assert.That(inventory.GetSnapshot().Get(new SlotAddress(BagId, 0)).Quantity, Is.EqualTo(2));
        }

        private static InventoryAggregate CreateInventory(
            int bagCapacity,
            int? stashCapacity = null,
            IInventoryPolicy[] policies = null)
        {
            var definitions = new[]
            {
                new ItemDefinition(PotionId, 5, ItemInstanceMode.Fungible, new[] { "consumable" }),
                new ItemDefinition(SwordId, 1, ItemInstanceMode.Unique, new[] { "equipment" }),
            };
            var containers = stashCapacity.HasValue
                ? new[]
                {
                    new ContainerDefinition(BagId, bagCapacity),
                    new ContainerDefinition(StashId, stashCapacity.Value),
                }
                : new[] { new ContainerDefinition(BagId, bagCapacity) };
            return new InventoryAggregate(
                new InventoryId("player"),
                new ItemDefinitionCatalog(definitions),
                containers,
                policies);
        }

        private static int Total(InventorySnapshot snapshot)
        {
            return snapshot.Containers.SelectMany(container => container.Slots)
                .Where(stack => stack != null)
                .Sum(stack => stack.Quantity);
        }

        private static string Describe(InventorySnapshot snapshot)
        {
            return string.Join("|", snapshot.Containers
                .OrderBy(container => container.Id.Value)
                .SelectMany(container => container.Slots.Select((stack, index) =>
                    stack == null
                        ? $"{container.Id}:{index}:empty"
                        : $"{container.Id}:{index}:{stack.DefinitionId}:{stack.Quantity}:{stack.InstanceId}")));
        }

        private sealed class RejectAllPolicy : IInventoryPolicy
        {
            public PolicyDecision Evaluate(InventoryPolicyContext context) =>
                PolicyDecision.Reject("Rejected by test policy.");
        }

        private sealed class RequireTagForAddsPolicy : IInventoryPolicy
        {
            private readonly string _requiredTag;

            public RequireTagForAddsPolicy(string requiredTag) => _requiredTag = requiredTag;

            public PolicyDecision Evaluate(InventoryPolicyContext context)
            {
                if (context.Request.Kind != MutationKind.Add)
                {
                    return PolicyDecision.Allow();
                }

                return context.Catalog.TryGet(context.Request.Stack.DefinitionId, out var definition)
                    && definition.Tags.Contains(_requiredTag)
                    ? PolicyDecision.Allow()
                    : PolicyDecision.Reject("Definition is not admitted by this container policy.");
            }
        }

        private sealed class SchemaOneToTwoMigration : IInventoryStateMigration
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
                                slot.InstanceId)))));
            }
        }
    }
}
