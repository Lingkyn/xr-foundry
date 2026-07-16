using System;
using System.Collections.Generic;
using System.Linq;

namespace Lingkyn.Interaction.Core
{
    public sealed class IntentDefinition
    {
        private readonly IReadOnlyList<string> _metadataKeys;

        internal IntentDefinition(
            IntentId id,
            InteractionValueKind valueKind,
            InteractionCapability requiredCapabilities,
            int dispatchOrder,
            IReadOnlyList<string> metadataKeys)
        {
            Id = id;
            ValueKind = valueKind;
            RequiredCapabilities = requiredCapabilities;
            DispatchOrder = dispatchOrder;
            _metadataKeys = metadataKeys ?? Array.Empty<string>();
        }

        public IntentId Id { get; }
        public InteractionValueKind ValueKind { get; }
        public InteractionCapability RequiredCapabilities { get; }
        public int DispatchOrder { get; }
        public IReadOnlyList<string> MetadataKeys => _metadataKeys;

        public static InteractionResult<IntentDefinition> Create(
            IntentId id,
            InteractionValueKind valueKind,
            InteractionCapability requiredCapabilities,
            int dispatchOrder,
            IEnumerable<string> metadataKeys = null)
        {
            if (dispatchOrder < 0)
            {
                return InteractionResult<IntentDefinition>.Fail(
                    InteractionValidationCode.InvalidDefinition,
                    "Dispatch order must be non-negative.",
                    id.Value);
            }

            var kindCapability = InteractionValue.RequiredCapabilitiesForKind(valueKind);
            if ((requiredCapabilities & kindCapability) != kindCapability)
            {
                return InteractionResult<IntentDefinition>.Fail(
                    InteractionValidationCode.CapabilityMismatch,
                    $"Intent '{id.Value}' must declare at least the capabilities required by value kind '{valueKind}'.",
                    id.Value);
            }

            var keys = new List<string>();
            if (metadataKeys != null)
            {
                foreach (var key in metadataKeys)
                {
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        return InteractionResult<IntentDefinition>.Fail(
                            InteractionValidationCode.InvalidDefinition,
                            "Metadata keys must not be empty.",
                            id.Value);
                    }

                    if (keys.Contains(key, StringComparer.Ordinal))
                    {
                        return InteractionResult<IntentDefinition>.Fail(
                            InteractionValidationCode.DuplicateDefinition,
                            $"Duplicate metadata key '{key}'.",
                            id.Value);
                    }

                    keys.Add(key);
                }
            }

            return InteractionResult<IntentDefinition>.Success(
                new IntentDefinition(
                    id,
                    valueKind,
                    requiredCapabilities,
                    dispatchOrder,
                    InteractionReadOnly.FreezeList(keys)));
        }
    }

    public sealed class InteractionRoute
    {
        private readonly byte[] _opaqueBindingDescriptor;

        internal InteractionRoute(
            RouteId id,
            ContextId contextId,
            IntentId intentId,
            SourceId sourceSelector,
            InteractionModality sourceModality,
            InteractionCapability sourceCapabilities,
            byte[] opaqueBindingDescriptor,
            int routeOrder)
        {
            Id = id;
            ContextId = contextId;
            IntentId = intentId;
            SourceSelector = sourceSelector;
            SourceModality = sourceModality;
            SourceCapabilities = sourceCapabilities;
            _opaqueBindingDescriptor = opaqueBindingDescriptor ?? Array.Empty<byte>();
            RouteOrder = routeOrder;
        }

        public RouteId Id { get; }
        public ContextId ContextId { get; }
        public IntentId IntentId { get; }
        /// <summary>
        /// Authoring-time association for adapter conversion. Runtime admission uses
        /// <see cref="RouteId"/>; observed source evidence travels on <see cref="SourceSignal.SourceId"/>.
        /// </summary>
        public SourceId SourceSelector { get; }
        public InteractionModality SourceModality { get; }
        public InteractionCapability SourceCapabilities { get; }
        public int RouteOrder { get; }
        public IReadOnlyList<byte> OpaqueBindingDescriptor => _opaqueBindingDescriptor;

        public static InteractionResult<InteractionRoute> Create(
            RouteId id,
            ContextId contextId,
            IntentId intentId,
            SourceId sourceSelector,
            InteractionModality sourceModality,
            InteractionCapability sourceCapabilities,
            byte[] opaqueBindingDescriptor,
            int routeOrder)
        {
            if (routeOrder < 0)
            {
                return InteractionResult<InteractionRoute>.Fail(
                    InteractionValidationCode.InvalidDefinition,
                    "Route order must be non-negative.",
                    id.Value);
            }

            return InteractionResult<InteractionRoute>.Success(
                new InteractionRoute(
                    id,
                    contextId,
                    intentId,
                    sourceSelector,
                    sourceModality,
                    sourceCapabilities,
                    InteractionReadOnly.CopyBytes(opaqueBindingDescriptor),
                    routeOrder));
        }
    }

    public sealed class InteractionContextDefinition
    {
        private readonly IReadOnlyList<RouteId> _routeIds;

        internal InteractionContextDefinition(ContextId id, int priority, IReadOnlyList<RouteId> routeIds)
        {
            Id = id;
            Priority = priority;
            _routeIds = routeIds ?? Array.Empty<RouteId>();
        }

        public ContextId Id { get; }
        public int Priority { get; }
        public IReadOnlyList<RouteId> RouteIds => _routeIds;

        public static InteractionResult<InteractionContextDefinition> Create(
            ContextId id,
            int priority,
            IEnumerable<RouteId> routeIds)
        {
            var list = new List<RouteId>();
            if (routeIds != null)
            {
                foreach (var routeId in routeIds)
                {
                    if (list.Any(existing => existing.Equals(routeId)))
                    {
                        return InteractionResult<InteractionContextDefinition>.Fail(
                            InteractionValidationCode.DuplicateDefinition,
                            $"Context '{id.Value}' contains duplicate route '{routeId.Value}'.",
                            id.Value);
                    }

                    list.Add(routeId);
                }
            }

            return InteractionResult<InteractionContextDefinition>.Success(
                new InteractionContextDefinition(id, priority, InteractionReadOnly.FreezeList(list)));
        }
    }

    public sealed class BindingSuggestion
    {
        private readonly byte[] _opaqueProposedBinding;

        internal BindingSuggestion(
            BindingSuggestionId id,
            IntentId intentId,
            RouteId routeId,
            string adapterKind,
            byte[] opaqueProposedBinding)
        {
            Id = id;
            IntentId = intentId;
            RouteId = routeId;
            AdapterKind = adapterKind ?? string.Empty;
            _opaqueProposedBinding = opaqueProposedBinding ?? Array.Empty<byte>();
        }

        public BindingSuggestionId Id { get; }
        public IntentId IntentId { get; }
        public RouteId RouteId { get; }
        public string AdapterKind { get; }
        public IReadOnlyList<byte> OpaqueProposedBinding => _opaqueProposedBinding;

        public static InteractionResult<BindingSuggestion> Create(
            BindingSuggestionId id,
            IntentId intentId,
            RouteId routeId,
            string adapterKind,
            byte[] opaqueProposedBinding)
        {
            if (string.IsNullOrWhiteSpace(adapterKind))
            {
                return InteractionResult<BindingSuggestion>.Fail(
                    InteractionValidationCode.InvalidBindingSuggestion,
                    "Adapter kind is required.",
                    id.Value);
            }

            return InteractionResult<BindingSuggestion>.Success(
                new BindingSuggestion(
                    id,
                    intentId,
                    routeId,
                    adapterKind.Trim(),
                    InteractionReadOnly.CopyBytes(opaqueProposedBinding)));
        }
    }
}
