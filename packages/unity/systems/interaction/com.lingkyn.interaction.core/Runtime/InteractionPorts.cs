using System.Collections.Generic;

namespace Lingkyn.Interaction.Core
{
    public sealed class BindingOverride
    {
        private readonly byte[] _opaqueAdapterRouteToken;

        internal BindingOverride(
            IntentId intentId,
            RouteId routeId,
            string adapterKind,
            byte[] opaqueAdapterRouteToken)
        {
            IntentId = intentId;
            RouteId = routeId;
            AdapterKind = adapterKind ?? string.Empty;
            _opaqueAdapterRouteToken = opaqueAdapterRouteToken ?? System.Array.Empty<byte>();
        }

        public IntentId IntentId { get; }
        public RouteId RouteId { get; }
        public string AdapterKind { get; }
        public IReadOnlyList<byte> OpaqueAdapterRouteToken => _opaqueAdapterRouteToken;

        public static InteractionResult<BindingOverride> Create(
            IntentId intentId,
            RouteId routeId,
            string adapterKind,
            byte[] opaqueAdapterRouteToken)
        {
            if (string.IsNullOrWhiteSpace(adapterKind))
            {
                return InteractionResult<BindingOverride>.Fail(
                    InteractionValidationCode.InvalidDefinition,
                    "Adapter kind is required.",
                    routeId.Value);
            }

            if (opaqueAdapterRouteToken == null || opaqueAdapterRouteToken.Length == 0)
            {
                return InteractionResult<BindingOverride>.Fail(
                    InteractionValidationCode.InvalidDefinition,
                    "Opaque adapter route token is required.",
                    routeId.Value);
            }

            return InteractionResult<BindingOverride>.Success(
                new BindingOverride(
                    intentId,
                    routeId,
                    adapterKind.Trim(),
                    InteractionReadOnly.CopyBytes(opaqueAdapterRouteToken)));
        }
    }

    public sealed class InteractionBindingOverrideSet
    {
        private readonly IReadOnlyList<BindingOverride> _overrides;

        private InteractionBindingOverrideSet(IReadOnlyList<BindingOverride> overrides)
        {
            _overrides = overrides ?? System.Array.Empty<BindingOverride>();
        }

        public IReadOnlyList<BindingOverride> Overrides => _overrides;

        public static InteractionResult<InteractionBindingOverrideSet> Create(IEnumerable<BindingOverride> overrides)
        {
            var list = new List<BindingOverride>();
            var seen = new HashSet<string>(System.StringComparer.Ordinal);
            if (overrides != null)
            {
                foreach (var entry in overrides)
                {
                    if (entry == null)
                    {
                        return InteractionResult<InteractionBindingOverrideSet>.Fail(
                            InteractionValidationCode.InvalidDefinition,
                            "Binding override must not be null.");
                    }

                    var key = $"{entry.IntentId.Value}\0{entry.RouteId.Value}";
                    if (!seen.Add(key))
                    {
                        return InteractionResult<InteractionBindingOverrideSet>.Fail(
                            InteractionValidationCode.DuplicateDefinition,
                            $"Duplicate binding override for intent '{entry.IntentId.Value}' and route '{entry.RouteId.Value}'.",
                            entry.RouteId.Value);
                    }

                    list.Add(entry);
                }
            }

            return InteractionResult<InteractionBindingOverrideSet>.Success(
                new InteractionBindingOverrideSet(InteractionReadOnly.FreezeList(list)));
        }

        public static InteractionBindingOverrideSet Empty { get; } =
            new InteractionBindingOverrideSet(System.Array.Empty<BindingOverride>());
    }

    public interface IInteractionPolicyPort
    {
        InteractionPolicySnapshot CurrentPolicy { get; }
    }
}
