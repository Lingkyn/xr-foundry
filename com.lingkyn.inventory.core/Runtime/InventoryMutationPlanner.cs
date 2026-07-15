using System;
using System.Collections.Generic;
using System.Linq;

namespace Lingkyn.Inventory.Core
{
    internal sealed class InventoryMutationPlanner
    {
        private readonly IItemDefinitionCatalog _catalog;

        public InventoryMutationPlanner(IItemDefinitionCatalog catalog)
        {
            _catalog = catalog;
        }

        public MutationPlanResult Apply(Dictionary<ContainerId, ItemStack[]> state, MutationRequest request)
        {
            switch (request.Kind)
            {
                case MutationKind.Add:
                    return ApplyAdd(state, request);
                case MutationKind.Remove:
                    return ApplyRemove(state, request);
                case MutationKind.Move:
                    return ApplyMove(state, request, false);
                case MutationKind.Transfer:
                    return ApplyMove(state, request, true);
                case MutationKind.Swap:
                    return ApplySwap(state, request);
                case MutationKind.Split:
                    return ApplySplit(state, request);
                case MutationKind.Merge:
                    return ApplyMerge(state, request);
                default:
                    return MutationPlanResult.Fail(MutationFailure.InvalidRequest, "Unknown mutation kind.");
            }
        }

        private MutationPlanResult ApplyAdd(Dictionary<ContainerId, ItemStack[]> state, MutationRequest request)
        {
            var stack = request.Stack;
            if (!_catalog.TryGet(stack.DefinitionId, out var definition))
            {
                return MutationPlanResult.Fail(MutationFailure.UnknownDefinition, $"Unknown item definition: {stack.DefinitionId}");
            }

            if (!state.TryGetValue(request.TargetContainer.Value, out var slots))
            {
                return MutationPlanResult.Fail(MutationFailure.UnknownContainer, $"Unknown container: {request.TargetContainer.Value}");
            }

            if (definition.InstanceMode == ItemInstanceMode.Unique)
            {
                if (!stack.InstanceId.HasValue || stack.Quantity != 1)
                {
                    return MutationPlanResult.Fail(MutationFailure.InvalidRequest, "A unique definition requires one unique instance id.");
                }

                if (ContainsInstance(state, stack.InstanceId.Value))
                {
                    return MutationPlanResult.Fail(MutationFailure.DuplicateInstance, $"Duplicate item instance id: {stack.InstanceId.Value}");
                }
            }
            else if (stack.InstanceId.HasValue)
            {
                return MutationPlanResult.Fail(MutationFailure.InvalidRequest, "A fungible definition cannot be added with an instance id.");
            }

            var remaining = stack.Quantity;
            var affected = new List<SlotAddress>();
            if (definition.InstanceMode == ItemInstanceMode.Fungible)
            {
                for (var index = 0; index < slots.Length && remaining > 0; index++)
                {
                    var existing = slots[index];
                    if (existing == null || existing.DefinitionId != stack.DefinitionId || existing.InstanceId.HasValue)
                    {
                        continue;
                    }

                    var accepted = Math.Min(remaining, definition.MaximumStack - existing.Quantity);
                    if (accepted <= 0)
                    {
                        continue;
                    }

                    slots[index] = existing.WithQuantity(existing.Quantity + accepted);
                    remaining -= accepted;
                    affected.Add(new SlotAddress(request.TargetContainer.Value, index));
                }
            }

            for (var index = 0; index < slots.Length && remaining > 0; index++)
            {
                if (slots[index] != null)
                {
                    continue;
                }

                var accepted = definition.InstanceMode == ItemInstanceMode.Unique
                    ? 1
                    : Math.Min(remaining, definition.MaximumStack);
                slots[index] = new ItemStack(stack.DefinitionId, accepted, stack.InstanceId);
                remaining -= accepted;
                affected.Add(new SlotAddress(request.TargetContainer.Value, index));
            }

            var acceptedQuantity = stack.Quantity - remaining;
            if (remaining > 0 && !request.AllowPartial)
            {
                return MutationPlanResult.Fail(MutationFailure.CapacityExceeded, "The target container cannot accept the full quantity.");
            }

            return acceptedQuantity == 0
                ? MutationPlanResult.Fail(MutationFailure.CapacityExceeded, "The target container cannot accept this item.")
                : MutationPlanResult.Success(acceptedQuantity, affected);
        }

        private static MutationPlanResult ApplyRemove(Dictionary<ContainerId, ItemStack[]> state, MutationRequest request)
        {
            if (!TryResolve(state, request.Source.Value, out var slots, out var stack, out var failure))
            {
                return failure;
            }

            if (stack == null)
            {
                return MutationPlanResult.Fail(MutationFailure.SourceEmpty, "The source slot is empty.");
            }

            if (request.Quantity > stack.Quantity)
            {
                return MutationPlanResult.Fail(MutationFailure.InsufficientQuantity, "The source stack does not contain the requested quantity.");
            }

            slots[request.Source.Value.Index] = request.Quantity == stack.Quantity
                ? null
                : stack.WithQuantity(stack.Quantity - request.Quantity);
            return MutationPlanResult.Success(request.Quantity, new[] { request.Source.Value });
        }

        private static MutationPlanResult ApplyMove(
            Dictionary<ContainerId, ItemStack[]> state,
            MutationRequest request,
            bool requireDifferentContainers)
        {
            var sourceAddress = request.Source.Value;
            var destinationAddress = request.Destination.Value;
            if (sourceAddress == destinationAddress)
            {
                return MutationPlanResult.Fail(MutationFailure.InvalidRequest, "Source and destination must differ.");
            }

            if (requireDifferentContainers && sourceAddress.ContainerId == destinationAddress.ContainerId)
            {
                return MutationPlanResult.Fail(MutationFailure.InvalidRequest, "A transfer must cross container boundaries.");
            }

            if (!TryResolve(state, sourceAddress, out var sourceSlots, out var source, out var sourceFailure))
            {
                return sourceFailure;
            }

            if (!TryResolve(state, destinationAddress, out var destinationSlots, out var destination, out var destinationFailure))
            {
                return destinationFailure;
            }

            if (source == null)
            {
                return MutationPlanResult.Fail(MutationFailure.SourceEmpty, "The source slot is empty.");
            }

            if (destination != null)
            {
                return MutationPlanResult.Fail(MutationFailure.DestinationOccupied, "The destination slot is occupied. Use merge or swap explicitly.");
            }

            if (request.Quantity > source.Quantity)
            {
                return MutationPlanResult.Fail(MutationFailure.InsufficientQuantity, "The source stack does not contain the requested quantity.");
            }

            if (source.IsUnique && request.Quantity != 1)
            {
                return MutationPlanResult.Fail(MutationFailure.InvalidRequest, "A unique instance moves as one item.");
            }

            destinationSlots[destinationAddress.Index] = new ItemStack(source.DefinitionId, request.Quantity, source.InstanceId);
            sourceSlots[sourceAddress.Index] = request.Quantity == source.Quantity
                ? null
                : source.WithQuantity(source.Quantity - request.Quantity);
            return MutationPlanResult.Success(request.Quantity, new[] { sourceAddress, destinationAddress });
        }

        private static MutationPlanResult ApplySwap(Dictionary<ContainerId, ItemStack[]> state, MutationRequest request)
        {
            var sourceAddress = request.Source.Value;
            var destinationAddress = request.Destination.Value;
            if (sourceAddress == destinationAddress)
            {
                return MutationPlanResult.Fail(MutationFailure.InvalidRequest, "Source and destination must differ.");
            }

            if (!TryResolve(state, sourceAddress, out var sourceSlots, out var source, out var sourceFailure))
            {
                return sourceFailure;
            }

            if (!TryResolve(state, destinationAddress, out var destinationSlots, out var destination, out var destinationFailure))
            {
                return destinationFailure;
            }

            if (source == null)
            {
                return MutationPlanResult.Fail(MutationFailure.SourceEmpty, "The source slot is empty.");
            }

            if (destination == null)
            {
                return MutationPlanResult.Fail(MutationFailure.DestinationEmpty, "The destination slot is empty. Use move explicitly.");
            }

            sourceSlots[sourceAddress.Index] = destination;
            destinationSlots[destinationAddress.Index] = source;
            return MutationPlanResult.Success(source.Quantity, new[] { sourceAddress, destinationAddress });
        }

        private static MutationPlanResult ApplySplit(Dictionary<ContainerId, ItemStack[]> state, MutationRequest request)
        {
            var sourceAddress = request.Source.Value;
            var destinationAddress = request.Destination.Value;
            if (!TryResolve(state, sourceAddress, out var sourceSlots, out var source, out var sourceFailure))
            {
                return sourceFailure;
            }

            if (!TryResolve(state, destinationAddress, out var destinationSlots, out var destination, out var destinationFailure))
            {
                return destinationFailure;
            }

            if (source == null)
            {
                return MutationPlanResult.Fail(MutationFailure.SourceEmpty, "The source slot is empty.");
            }

            if (source.IsUnique || request.Quantity >= source.Quantity)
            {
                return MutationPlanResult.Fail(MutationFailure.InvalidRequest, "A split must move less than a fungible source stack.");
            }

            if (destination != null)
            {
                return MutationPlanResult.Fail(MutationFailure.DestinationOccupied, "A split destination must be empty.");
            }

            sourceSlots[sourceAddress.Index] = source.WithQuantity(source.Quantity - request.Quantity);
            destinationSlots[destinationAddress.Index] = new ItemStack(source.DefinitionId, request.Quantity);
            return MutationPlanResult.Success(request.Quantity, new[] { sourceAddress, destinationAddress });
        }

        private MutationPlanResult ApplyMerge(Dictionary<ContainerId, ItemStack[]> state, MutationRequest request)
        {
            var sourceAddress = request.Source.Value;
            var destinationAddress = request.Destination.Value;
            if (sourceAddress == destinationAddress)
            {
                return MutationPlanResult.Fail(MutationFailure.InvalidRequest, "Source and destination must differ.");
            }

            if (!TryResolve(state, sourceAddress, out var sourceSlots, out var source, out var sourceFailure))
            {
                return sourceFailure;
            }

            if (!TryResolve(state, destinationAddress, out var destinationSlots, out var destination, out var destinationFailure))
            {
                return destinationFailure;
            }

            if (source == null)
            {
                return MutationPlanResult.Fail(MutationFailure.SourceEmpty, "The source slot is empty.");
            }

            if (destination == null)
            {
                return MutationPlanResult.Fail(MutationFailure.DestinationEmpty, "A merge destination must contain a compatible stack.");
            }

            if (source.IsUnique || destination.IsUnique || source.DefinitionId != destination.DefinitionId)
            {
                return MutationPlanResult.Fail(MutationFailure.IncompatibleStacks, "Only fungible stacks with the same definition can merge.");
            }

            if (request.Quantity > source.Quantity)
            {
                return MutationPlanResult.Fail(MutationFailure.InsufficientQuantity, "The source stack does not contain the requested quantity.");
            }

            if (!_catalog.TryGet(source.DefinitionId, out var definition))
            {
                return MutationPlanResult.Fail(MutationFailure.UnknownDefinition, $"Unknown item definition: {source.DefinitionId}");
            }

            if (destination.Quantity + request.Quantity > definition.MaximumStack)
            {
                return MutationPlanResult.Fail(MutationFailure.CapacityExceeded, "The merge would exceed the maximum stack.");
            }

            destinationSlots[destinationAddress.Index] = destination.WithQuantity(destination.Quantity + request.Quantity);
            sourceSlots[sourceAddress.Index] = request.Quantity == source.Quantity
                ? null
                : source.WithQuantity(source.Quantity - request.Quantity);
            return MutationPlanResult.Success(request.Quantity, new[] { sourceAddress, destinationAddress });
        }

        private static bool TryResolve(
            Dictionary<ContainerId, ItemStack[]> state,
            SlotAddress address,
            out ItemStack[] slots,
            out ItemStack stack,
            out MutationPlanResult failure)
        {
            if (!state.TryGetValue(address.ContainerId, out slots))
            {
                stack = null;
                failure = MutationPlanResult.Fail(MutationFailure.UnknownContainer, $"Unknown container: {address.ContainerId}");
                return false;
            }

            if (address.Index >= slots.Length)
            {
                stack = null;
                failure = MutationPlanResult.Fail(MutationFailure.InvalidSlot, $"Slot index {address.Index} is outside container {address.ContainerId}.");
                return false;
            }

            stack = slots[address.Index];
            failure = default;
            return true;
        }

        private static bool ContainsInstance(Dictionary<ContainerId, ItemStack[]> state, ItemInstanceId instanceId)
        {
            return state.Values.SelectMany(slots => slots)
                .Any(stack => stack != null && stack.InstanceId.HasValue && stack.InstanceId.Value == instanceId);
        }
    }

    internal readonly struct MutationPlanResult
    {
        private MutationPlanResult(
            bool succeeded,
            MutationFailure failure,
            string message,
            int acceptedQuantity,
            IReadOnlyList<SlotAddress> affectedAddresses)
        {
            Succeeded = succeeded;
            Failure = failure;
            Message = message;
            AcceptedQuantity = acceptedQuantity;
            AffectedAddresses = affectedAddresses;
        }

        public bool Succeeded { get; }
        public MutationFailure Failure { get; }
        public string Message { get; }
        public int AcceptedQuantity { get; }
        public IReadOnlyList<SlotAddress> AffectedAddresses { get; }

        public static MutationPlanResult Success(int acceptedQuantity, IReadOnlyList<SlotAddress> affectedAddresses) =>
            new MutationPlanResult(true, MutationFailure.None, string.Empty, acceptedQuantity, affectedAddresses);

        public static MutationPlanResult Fail(MutationFailure failure, string message) =>
            new MutationPlanResult(false, failure, message, 0, Array.Empty<SlotAddress>());
    }
}
