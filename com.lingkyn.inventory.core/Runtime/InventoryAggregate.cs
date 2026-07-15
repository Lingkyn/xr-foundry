using System;
using System.Collections.Generic;
using System.Linq;

namespace Lingkyn.Inventory.Core
{
    public sealed class InventoryAggregate
    {
        private readonly IItemDefinitionCatalog _catalog;
        private readonly InventoryMutationPlanner _planner;
        private readonly List<IInventoryPolicy> _policies;
        private Dictionary<ContainerId, ItemStack[]> _state;

        public InventoryAggregate(
            InventoryId id,
            IItemDefinitionCatalog catalog,
            IEnumerable<ContainerDefinition> containers,
            IEnumerable<IInventoryPolicy> policies = null)
        {
            IdentifierGuard.Require(id.Value, nameof(id));
            Id = id;
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _planner = new InventoryMutationPlanner(_catalog);
            if (containers == null)
            {
                throw new ArgumentNullException(nameof(containers));
            }

            _state = new Dictionary<ContainerId, ItemStack[]>();
            foreach (var container in containers)
            {
                if (container == null)
                {
                    throw new ArgumentException("Container definitions cannot contain null entries.", nameof(containers));
                }

                if (_state.ContainsKey(container.Id))
                {
                    throw new ArgumentException($"Duplicate container id: {container.Id}", nameof(containers));
                }

                _state.Add(container.Id, new ItemStack[container.Capacity]);
            }

            if (_state.Count == 0)
            {
                throw new ArgumentException("An Inventory must contain at least one container.", nameof(containers));
            }

            _policies = new List<IInventoryPolicy>(policies ?? Array.Empty<IInventoryPolicy>());
            if (_policies.Any(policy => policy == null))
            {
                throw new ArgumentException("Policies cannot contain null entries.", nameof(policies));
            }
        }

        public InventoryId Id { get; }
        public long Revision { get; private set; }
        public event Action<InventoryEvent> Changed;
        public event Action<Exception> ObserverFaulted;

        public InventorySnapshot GetSnapshot() => new InventorySnapshot(Id, Revision, _state);

        public MutationResult Execute(MutationRequest request)
        {
            var revisionBefore = Revision;
            var shapeFailure = ValidateRequestShape(request);
            if (shapeFailure.HasValue)
            {
                return Failure(shapeFailure.Value, "The mutation request is incomplete or invalid.", request?.Quantity ?? 0, revisionBefore);
            }

            var snapshot = GetSnapshot();
            var policyContext = new InventoryPolicyContext(snapshot, request, _catalog);
            foreach (var policy in _policies)
            {
                var decision = policy.Evaluate(policyContext);
                if (!decision.Allowed)
                {
                    return Failure(MutationFailure.PolicyRejected, decision.Reason, request.Quantity, revisionBefore);
                }
            }

            var working = snapshot.CopyState();
            var attempt = _planner.Apply(working, request);

            if (!attempt.Succeeded)
            {
                return Failure(attempt.Failure, attempt.Message, request.Quantity, revisionBefore);
            }

            _state = working;
            Revision++;
            var result = new MutationResult(
                true,
                MutationFailure.None,
                string.Empty,
                request.Quantity,
                attempt.AcceptedQuantity,
                revisionBefore,
                Revision,
                attempt.AffectedAddresses);
            RaiseChanged(new InventoryEvent(request.Kind, Revision, attempt.AffectedAddresses));
            return result;
        }

        private static MutationFailure? ValidateRequestShape(MutationRequest request)
        {
            if (request == null)
            {
                return MutationFailure.InvalidRequest;
            }

            switch (request.Kind)
            {
                case MutationKind.Add:
                    return request.Stack == null || !request.TargetContainer.HasValue
                        ? MutationFailure.InvalidRequest
                        : (MutationFailure?)null;
                case MutationKind.Remove:
                    return !request.Source.HasValue || request.Quantity < 1
                        ? MutationFailure.InvalidRequest
                        : (MutationFailure?)null;
                case MutationKind.Move:
                case MutationKind.Split:
                case MutationKind.Merge:
                case MutationKind.Transfer:
                    return !request.Source.HasValue || !request.Destination.HasValue || request.Quantity < 1
                        ? MutationFailure.InvalidRequest
                        : (MutationFailure?)null;
                case MutationKind.Swap:
                    return !request.Source.HasValue || !request.Destination.HasValue
                        ? MutationFailure.InvalidRequest
                        : (MutationFailure?)null;
                default:
                    return MutationFailure.InvalidRequest;
            }
        }

        private static MutationResult Failure(MutationFailure failure, string message, int requestedQuantity, long revision)
        {
            return new MutationResult(false, failure, message, requestedQuantity, 0, revision, revision, Array.Empty<SlotAddress>());
        }

        private void RaiseChanged(InventoryEvent inventoryEvent)
        {
            var handlers = Changed;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<InventoryEvent> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(inventoryEvent);
                }
                catch (Exception exception)
                {
                    ObserverFaulted?.Invoke(exception);
                }
            }
        }

    }
}
