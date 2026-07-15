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
        private readonly ItemStateFragmentRegistry _stateFragmentRegistry;
        private Dictionary<ContainerId, ItemStack[]> _state;

        public InventoryAggregate(
            InventoryId id,
            IItemDefinitionCatalog catalog,
            IEnumerable<ContainerDefinition> containers,
            IEnumerable<IInventoryPolicy> policies = null,
            ItemStateFragmentRegistry stateFragmentRegistry = null)
        {
            IdentifierGuard.Require(id.Value, nameof(id));
            Id = id;
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _planner = new InventoryMutationPlanner(_catalog);
            _stateFragmentRegistry = stateFragmentRegistry ?? new ItemStateFragmentRegistry();
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
        public event Action<InventorySnapshot> Restored;
        public event Action<Exception> ObserverFaulted;

        public InventorySnapshot GetSnapshot() => new InventorySnapshot(Id, Revision, _state);

        public PersistenceEnvelope CreatePersistenceEnvelope() =>
            new PersistenceEnvelope(InventoryPersistence.CurrentSchemaVersion, GetSnapshot());

        public InventoryRestoreResult Restore(
            PersistenceEnvelope envelope,
            IEnumerable<IInventoryStateMigration> migrations = null)
        {
            var revisionBefore = Revision;
            var schemaBefore = envelope?.SchemaVersion ?? 0;
            if (!InventoryPersistence.TryMigrate(
                    envelope,
                    migrations,
                    out var state,
                    out var migrationFailure,
                    out var migrationMessage))
            {
                return RestoreFailure(
                    migrationFailure,
                    migrationMessage,
                    schemaBefore,
                    revisionBefore);
            }

            if (!string.Equals(state.InventoryId, Id.Value, StringComparison.Ordinal))
            {
                return RestoreFailure(
                    InventoryRestoreFailure.InventoryMismatch,
                    $"State belongs to Inventory '{state.InventoryId}', not '{Id.Value}'.",
                    schemaBefore,
                    revisionBefore);
            }

            var containers = state.Containers.ToDictionary(
                container => new ContainerId(container.ContainerId));
            if (containers.Count != _state.Count || _state.Keys.Any(id => !containers.ContainsKey(id)))
            {
                return RestoreFailure(
                    InventoryRestoreFailure.ContainerMismatch,
                    "Persisted containers do not match the configured Inventory containers.",
                    schemaBefore,
                    revisionBefore);
            }

            var working = new Dictionary<ContainerId, ItemStack[]>();
            var instanceIds = new HashSet<ItemInstanceId>();
            foreach (var configured in _state)
            {
                var persisted = containers[configured.Key];
                if (persisted.Capacity != configured.Value.Length)
                {
                    return RestoreFailure(
                        InventoryRestoreFailure.CapacityMismatch,
                        $"Container '{configured.Key}' expects {configured.Value.Length} slots but state contains {persisted.Capacity}.",
                        schemaBefore,
                        revisionBefore);
                }

                var slots = new ItemStack[persisted.Capacity];
                for (var index = 0; index < persisted.Capacity; index++)
                {
                    var slot = persisted.Slots[index];
                    if (slot == null)
                    {
                        continue;
                    }

                    var definitionId = new ItemDefinitionId(slot.DefinitionId);
                    if (!_catalog.TryGet(definitionId, out var definition))
                    {
                        return RestoreFailure(
                            InventoryRestoreFailure.MissingDefinition,
                            $"Container '{configured.Key}' slot {index} references missing definition '{definitionId}'.",
                            schemaBefore,
                            revisionBefore);
                    }

                    var instanceId = string.IsNullOrWhiteSpace(slot.InstanceId)
                        ? (ItemInstanceId?)null
                        : new ItemInstanceId(slot.InstanceId);
                    var invalidUnique = definition.InstanceMode == ItemInstanceMode.Unique
                        && (!instanceId.HasValue || slot.Quantity != 1);
                    var invalidFungible = definition.InstanceMode == ItemInstanceMode.Fungible
                        && (instanceId.HasValue || slot.StateFragments.Count > 0);
                    if (slot.Quantity > definition.MaximumStack
                        || invalidUnique
                        || invalidFungible
                        || (slot.StateFragments.Count > 0 && !instanceId.HasValue))
                    {
                        return RestoreFailure(
                            InventoryRestoreFailure.InvalidStack,
                            $"Container '{configured.Key}' slot {index} violates definition '{definitionId}'.",
                            schemaBefore,
                            revisionBefore);
                    }

                    if (instanceId.HasValue && !instanceIds.Add(instanceId.Value))
                    {
                        return RestoreFailure(
                            InventoryRestoreFailure.DuplicateInstance,
                            $"Instance '{instanceId.Value}' appears more than once.",
                            schemaBefore,
                            revisionBefore);
                    }

                    var fragments = new List<ItemStateFragment>();
                    foreach (var fragment in slot.StateFragments)
                    {
                        try
                        {
                            fragments.Add(_stateFragmentRegistry.Rehydrate(
                                new ItemStateFragmentTypeId(fragment.TypeId),
                                fragment.SchemaVersion,
                                fragment.Payload));
                        }
                        catch (Exception exception)
                        {
                            return RestoreFailure(
                                InventoryRestoreFailure.InvalidInstanceState,
                                $"Container '{configured.Key}' slot {index} has invalid instance state: {exception.Message}",
                                schemaBefore,
                                revisionBefore);
                        }
                    }

                    slots[index] = new ItemStack(definitionId, slot.Quantity, instanceId, fragments);
                }

                working.Add(configured.Key, slots);
            }

            _state = working;
            Revision = state.Revision;
            RaiseRestored(GetSnapshot());
            return new InventoryRestoreResult(
                true,
                InventoryRestoreFailure.None,
                string.Empty,
                schemaBefore,
                InventoryPersistence.CurrentSchemaVersion,
                revisionBefore,
                Revision);
        }

        public MutationResult Execute(MutationRequest request)
        {
            var revisionBefore = Revision;
            var shapeFailure = ValidateRequestShape(request);
            if (shapeFailure.HasValue)
            {
                return Failure(shapeFailure.Value, "The mutation request is incomplete or invalid.", request?.Quantity ?? 0, revisionBefore);
            }

            var stateFailure = ValidateInstanceState(request);
            if (stateFailure != null)
            {
                return Failure(
                    MutationFailure.InvalidInstanceState,
                    stateFailure,
                    request.Quantity,
                    revisionBefore);
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
                case MutationKind.SetInstanceState:
                    return !request.Source.HasValue || request.StateFragment == null
                        ? MutationFailure.InvalidRequest
                        : (MutationFailure?)null;
                case MutationKind.RemoveInstanceState:
                    return !request.Source.HasValue || !request.StateFragmentTypeId.HasValue
                        ? MutationFailure.InvalidRequest
                        : (MutationFailure?)null;
                default:
                    return MutationFailure.InvalidRequest;
            }
        }

        private string ValidateInstanceState(MutationRequest request)
        {
            if (request.Kind == MutationKind.Add && request.Stack != null)
            {
                foreach (var fragment in request.Stack.StateFragments)
                {
                    if (!_stateFragmentRegistry.TryValidate(fragment, out var message))
                    {
                        return message;
                    }
                }
            }

            if (request.Kind == MutationKind.SetInstanceState
                && !_stateFragmentRegistry.TryValidate(request.StateFragment, out var stateMessage))
            {
                return stateMessage;
            }

            return null;
        }

        private static MutationResult Failure(MutationFailure failure, string message, int requestedQuantity, long revision)
        {
            return new MutationResult(false, failure, message, requestedQuantity, 0, revision, revision, Array.Empty<SlotAddress>());
        }

        private static InventoryRestoreResult RestoreFailure(
            InventoryRestoreFailure failure,
            string message,
            int schemaVersionBefore,
            long revision)
        {
            return new InventoryRestoreResult(
                false,
                failure,
                message,
                schemaVersionBefore,
                InventoryPersistence.CurrentSchemaVersion,
                revision,
                revision);
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
                    RaiseObserverFault(exception);
                }
            }
        }

        private void RaiseRestored(InventorySnapshot snapshot)
        {
            var handlers = Restored;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<InventorySnapshot> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(snapshot);
                }
                catch (Exception exception)
                {
                    RaiseObserverFault(exception);
                }
            }
        }

        private void RaiseObserverFault(Exception exception)
        {
            var handlers = ObserverFaulted;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<Exception> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(exception);
                }
                catch
                {
                    // Observer diagnostics must never alter or mask a committed mutation.
                }
            }
        }

    }
}
