using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Lingkyn.Inventory.Core
{
    public enum MutationKind
    {
        Add,
        Remove,
        Move,
        Swap,
        Split,
        Merge,
        Transfer,
        SetInstanceState,
        RemoveInstanceState,
    }

    public enum MutationFailure
    {
        None,
        InvalidRequest,
        UnknownDefinition,
        UnknownContainer,
        InvalidSlot,
        SourceEmpty,
        DestinationOccupied,
        DestinationEmpty,
        IncompatibleStacks,
        InsufficientQuantity,
        CapacityExceeded,
        DuplicateInstance,
        InvalidInstanceState,
        PolicyRejected,
    }

    public sealed class MutationRequest
    {
        private MutationRequest(
            MutationKind kind,
            ItemStack stack,
            ContainerId? targetContainer,
            SlotAddress? source,
            SlotAddress? destination,
            int quantity,
            bool allowPartial,
            ItemStateFragment stateFragment,
            ItemStateFragmentTypeId? stateFragmentTypeId)
        {
            Kind = kind;
            Stack = stack;
            TargetContainer = targetContainer;
            Source = source;
            Destination = destination;
            Quantity = quantity;
            AllowPartial = allowPartial;
            StateFragment = stateFragment;
            StateFragmentTypeId = stateFragmentTypeId;
        }

        public MutationKind Kind { get; }
        public ItemStack Stack { get; }
        public ContainerId? TargetContainer { get; }
        public SlotAddress? Source { get; }
        public SlotAddress? Destination { get; }
        public int Quantity { get; }
        public bool AllowPartial { get; }
        public ItemStateFragment StateFragment { get; }
        public ItemStateFragmentTypeId? StateFragmentTypeId { get; }

        public static MutationRequest Add(ItemStack stack, ContainerId targetContainer, bool allowPartial = false)
        {
            if (stack == null)
            {
                throw new ArgumentNullException(nameof(stack));
            }

            return new MutationRequest(MutationKind.Add, stack, targetContainer, null, null, stack.Quantity, allowPartial, null, null);
        }

        public static MutationRequest Remove(SlotAddress source, int quantity) =>
            new MutationRequest(MutationKind.Remove, null, null, source, null, quantity, false, null, null);

        public static MutationRequest Move(SlotAddress source, SlotAddress destination, int quantity) =>
            new MutationRequest(MutationKind.Move, null, null, source, destination, quantity, false, null, null);

        public static MutationRequest Swap(SlotAddress source, SlotAddress destination) =>
            new MutationRequest(MutationKind.Swap, null, null, source, destination, 0, false, null, null);

        public static MutationRequest Split(SlotAddress source, SlotAddress destination, int quantity) =>
            new MutationRequest(MutationKind.Split, null, null, source, destination, quantity, false, null, null);

        public static MutationRequest Merge(SlotAddress source, SlotAddress destination, int quantity) =>
            new MutationRequest(MutationKind.Merge, null, null, source, destination, quantity, false, null, null);

        public static MutationRequest Transfer(SlotAddress source, SlotAddress destination, int quantity) =>
            new MutationRequest(MutationKind.Transfer, null, null, source, destination, quantity, false, null, null);

        public static MutationRequest SetInstanceState(SlotAddress source, ItemStateFragment fragment)
        {
            if (fragment == null)
            {
                throw new ArgumentNullException(nameof(fragment));
            }

            return new MutationRequest(
                MutationKind.SetInstanceState,
                null,
                null,
                source,
                null,
                1,
                false,
                fragment,
                fragment.TypeId);
        }

        public static MutationRequest RemoveInstanceState(
            SlotAddress source,
            ItemStateFragmentTypeId fragmentTypeId) =>
            new MutationRequest(
                MutationKind.RemoveInstanceState,
                null,
                null,
                source,
                null,
                1,
                false,
                null,
                fragmentTypeId);
    }

    public readonly struct PolicyDecision
    {
        private PolicyDecision(bool allowed, string reason)
        {
            Allowed = allowed;
            Reason = reason ?? string.Empty;
        }

        public bool Allowed { get; }
        public string Reason { get; }
        public static PolicyDecision Allow() => new PolicyDecision(true, string.Empty);
        public static PolicyDecision Reject(string reason) => new PolicyDecision(false, reason);
    }

    public sealed class InventoryPolicyContext
    {
        internal InventoryPolicyContext(
            InventorySnapshot snapshot,
            MutationRequest request,
            IItemDefinitionCatalog catalog)
        {
            Snapshot = snapshot;
            Request = request;
            Catalog = catalog;
        }

        public InventorySnapshot Snapshot { get; }
        public MutationRequest Request { get; }
        public IItemDefinitionCatalog Catalog { get; }
    }

    public interface IInventoryPolicy
    {
        PolicyDecision Evaluate(InventoryPolicyContext context);
    }

    public sealed class MutationResult
    {
        internal MutationResult(
            bool succeeded,
            MutationFailure failure,
            string message,
            int requestedQuantity,
            int acceptedQuantity,
            long revisionBefore,
            long revisionAfter,
            IEnumerable<SlotAddress> affectedAddresses)
        {
            Succeeded = succeeded;
            Failure = failure;
            Message = message ?? string.Empty;
            RequestedQuantity = requestedQuantity;
            AcceptedQuantity = acceptedQuantity;
            RevisionBefore = revisionBefore;
            RevisionAfter = revisionAfter;
            AffectedAddresses = new ReadOnlyCollection<SlotAddress>(new List<SlotAddress>(affectedAddresses ?? Array.Empty<SlotAddress>()));
        }

        public bool Succeeded { get; }
        public MutationFailure Failure { get; }
        public string Message { get; }
        public int RequestedQuantity { get; }
        public int AcceptedQuantity { get; }
        public int RemainderQuantity => Math.Max(0, RequestedQuantity - AcceptedQuantity);
        public long RevisionBefore { get; }
        public long RevisionAfter { get; }
        public IReadOnlyList<SlotAddress> AffectedAddresses { get; }
    }

    public sealed class InventoryEvent
    {
        internal InventoryEvent(MutationKind kind, long revision, IEnumerable<SlotAddress> affectedAddresses)
        {
            Kind = kind;
            Revision = revision;
            AffectedAddresses = new ReadOnlyCollection<SlotAddress>(new List<SlotAddress>(affectedAddresses));
        }

        public MutationKind Kind { get; }
        public long Revision { get; }
        public IReadOnlyList<SlotAddress> AffectedAddresses { get; }
    }
}
