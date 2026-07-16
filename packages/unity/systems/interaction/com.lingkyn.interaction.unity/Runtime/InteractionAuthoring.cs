using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lingkyn.Interaction.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lingkyn.Interaction.Unity
{
    [CreateAssetMenu(menuName = "Lingkyn/Interaction/Intent", fileName = "InteractionIntent")]
    public sealed class InteractionIntentAsset : ScriptableObject
    {
        [SerializeField] private string _intentId = "ui.confirm";
        [SerializeField] private InteractionValueKind _valueKind = InteractionValueKind.Button;
        [SerializeField] private InteractionCapability _requiredCapabilities = InteractionCapability.Digital;
        [SerializeField, Min(0)] private int _dispatchOrder;
        [SerializeField] private List<string> _metadataKeys = new List<string>();

        public string IntentId => _intentId;
        public InteractionValueKind ValueKind => _valueKind;
        public InteractionCapability RequiredCapabilities => _requiredCapabilities;
        public int DispatchOrder => _dispatchOrder;
        public IReadOnlyList<string> MetadataKeys => (_metadataKeys ?? new List<string>()).AsReadOnly();
    }

    [CreateAssetMenu(menuName = "Lingkyn/Interaction/Route", fileName = "InteractionRoute")]
    public sealed class InteractionRouteAsset : ScriptableObject
    {
        [SerializeField] private string _routeId = "ui.confirm.primary";
        [SerializeField] private InteractionIntentAsset _intent;
        [SerializeField] private string _sourceSelector = "player.primary";
        [SerializeField] private InteractionModality _sourceModality = InteractionModality.Unknown;
        [SerializeField] private InteractionCapability _sourceCapabilities = InteractionCapability.Digital;
        [SerializeField] private InputActionReference _action;
        [SerializeField, Min(0)] private int _routeOrder;

        public string RouteId => _routeId;
        public InteractionIntentAsset Intent => _intent;
        public string SourceSelector => _sourceSelector;
        public InteractionModality SourceModality => _sourceModality;
        public InteractionCapability SourceCapabilities => _sourceCapabilities;
        public InputActionReference Action => _action;
        public int RouteOrder => _routeOrder;
    }

    [CreateAssetMenu(menuName = "Lingkyn/Interaction/Context", fileName = "InteractionContext")]
    public sealed class InteractionContextAsset : ScriptableObject
    {
        [SerializeField] private string _contextId = "ui";
        [SerializeField] private int _priority;
        [SerializeField] private List<InteractionRouteAsset> _routes = new List<InteractionRouteAsset>();

        public string ContextId => _contextId;
        public int Priority => _priority;
        public IReadOnlyList<InteractionRouteAsset> Routes => (_routes ?? new List<InteractionRouteAsset>()).AsReadOnly();
    }

    [CreateAssetMenu(menuName = "Lingkyn/Interaction/Binding Suggestion", fileName = "InteractionBindingSuggestion")]
    public sealed class InteractionBindingSuggestionAsset : ScriptableObject
    {
        [SerializeField] private string _suggestionId = "ui.confirm.default";
        [SerializeField] private InteractionRouteAsset _route;
        [SerializeField] private string _proposedBindingPath = "<Keyboard>/enter";

        public string SuggestionId => _suggestionId;
        public InteractionRouteAsset Route => _route;
        public string ProposedBindingPath => _proposedBindingPath;
    }

    [CreateAssetMenu(menuName = "Lingkyn/Interaction/Registry", fileName = "InteractionRegistry")]
    public sealed class InteractionRegistryAsset : ScriptableObject
    {
        [SerializeField] private List<InteractionIntentAsset> _intents = new List<InteractionIntentAsset>();
        [SerializeField] private List<InteractionContextAsset> _contexts = new List<InteractionContextAsset>();
        [SerializeField] private List<InteractionBindingSuggestionAsset> _bindingSuggestions = new List<InteractionBindingSuggestionAsset>();

        public IReadOnlyList<InteractionIntentAsset> Intents => (_intents ?? new List<InteractionIntentAsset>()).AsReadOnly();
        public IReadOnlyList<InteractionContextAsset> Contexts => (_contexts ?? new List<InteractionContextAsset>()).AsReadOnly();
        public IReadOnlyList<InteractionBindingSuggestionAsset> BindingSuggestions =>
            (_bindingSuggestions ?? new List<InteractionBindingSuggestionAsset>()).AsReadOnly();
    }

    public enum InteractionAssetIssueCode
    {
        NullAsset = 0,
        InvalidIdentity = 1,
        DuplicateIdentity = 2,
        InvalidDefinition = 3,
        MissingReference = 4,
        MissingInputAction = 5,
        UnknownIntent = 6,
        UnknownRoute = 7,
        CoreConversionFailed = 8,
    }

    public readonly struct InteractionAssetIssue : IEquatable<InteractionAssetIssue>
    {
        public InteractionAssetIssue(
            InteractionAssetIssueCode code,
            string assetName,
            string fieldPath,
            int index,
            string subject,
            string message)
        {
            Code = code;
            AssetName = assetName ?? string.Empty;
            FieldPath = fieldPath ?? string.Empty;
            Index = index;
            Subject = subject ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public InteractionAssetIssueCode Code { get; }
        public string AssetName { get; }
        public string FieldPath { get; }
        public int Index { get; }
        public string Subject { get; }
        public string Message { get; }

        public bool Equals(InteractionAssetIssue other) =>
            Code == other.Code && Index == other.Index
            && string.Equals(AssetName, other.AssetName, StringComparison.Ordinal)
            && string.Equals(FieldPath, other.FieldPath, StringComparison.Ordinal)
            && string.Equals(Subject, other.Subject, StringComparison.Ordinal)
            && string.Equals(Message, other.Message, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is InteractionAssetIssue other && Equals(other);
        public override int GetHashCode() => HashCode.Combine((int)Code, AssetName, FieldPath, Index, Subject, Message);
    }

    public sealed class InteractionAssetValidationReport
    {
        private readonly InteractionAssetIssue[] _issues;

        internal InteractionAssetValidationReport(IEnumerable<InteractionAssetIssue> issues)
        {
            _issues = (issues ?? Array.Empty<InteractionAssetIssue>()).ToArray();
        }

        public bool IsValid => _issues.Length == 0;
        public IReadOnlyList<InteractionAssetIssue> Issues => Array.AsReadOnly(_issues);
    }

    public sealed class InteractionUnityRegistry
    {
        private readonly InputRouteBinding[] _routeBindings;

        internal InteractionUnityRegistry(InteractionRegistry coreRegistry, IEnumerable<InputRouteBinding> routeBindings)
        {
            CoreRegistry = coreRegistry ?? throw new ArgumentNullException(nameof(coreRegistry));
            _routeBindings = (routeBindings ?? Array.Empty<InputRouteBinding>())
                .OrderBy(binding => binding.RouteId)
                .ToArray();
        }

        public InteractionRegistry CoreRegistry { get; }
        public IReadOnlyList<InputRouteBinding> RouteBindings => Array.AsReadOnly(_routeBindings);

        public bool TryGetRouteBinding(RouteId routeId, out InputRouteBinding binding)
        {
            binding = _routeBindings.FirstOrDefault(item => item.RouteId.Equals(routeId));
            return binding != null;
        }
    }

    public sealed class InteractionAuthoringResult
    {
        internal InteractionAuthoringResult(
            InteractionUnityRegistry registry,
            InteractionAssetValidationReport validation)
        {
            Registry = registry;
            Validation = validation ?? new InteractionAssetValidationReport(null);
        }

        public bool Succeeded => Registry != null && Validation.IsValid;
        public InteractionUnityRegistry Registry { get; }
        public InteractionAssetValidationReport Validation { get; }
    }

    public static class InteractionAuthoringConverter
    {
        private const string AdapterKind = "unity-input-system/1.14";

        public static InteractionAssetValidationReport Validate(InteractionRegistryAsset asset) =>
            Build(asset).Validation;

        public static InteractionAuthoringResult Convert(InteractionRegistryAsset asset) => Build(asset);

        private static InteractionAuthoringResult Build(InteractionRegistryAsset asset)
        {
            var issues = new List<InteractionAssetIssue>();
            if (asset == null)
            {
                issues.Add(Issue(InteractionAssetIssueCode.NullAsset, null, "$", -1, string.Empty,
                    "Registry asset is required."));
                return Failed(issues);
            }

            var intents = new List<IntentDefinition>();
            var intentByAsset = new Dictionary<InteractionIntentAsset, IntentDefinition>();
            var intentIds = new HashSet<string>(StringComparer.Ordinal);
            var issueLocations = new Dictionary<string, IssueLocation>(StringComparer.Ordinal);
            for (var index = 0; index < asset.Intents.Count; index++)
            {
                var source = asset.Intents[index];
                var path = $"intents[{index}]";
                if (source == null)
                {
                    issues.Add(Issue(InteractionAssetIssueCode.NullAsset, asset, path, index, string.Empty,
                        "Intent asset must not be null."));
                    continue;
                }

                var id = IntentId.TryCreate(source.IntentId);
                if (!id.Succeeded)
                {
                    issues.Add(CoreIssue(source, path + ".intentId", index, source.IntentId, id.Error));
                    continue;
                }

                if (!intentIds.Add(id.Value.Value))
                {
                    issues.Add(Issue(InteractionAssetIssueCode.DuplicateIdentity, source, path + ".intentId",
                        index, id.Value.Value, "Intent identity must be unique in one registry asset."));
                    continue;
                }

                var definition = IntentDefinition.Create(
                    id.Value,
                    source.ValueKind,
                    source.RequiredCapabilities,
                    source.DispatchOrder,
                    source.MetadataKeys);
                if (!definition.Succeeded)
                {
                    issues.Add(CoreIssue(source, path, index, id.Value.Value, definition.Error));
                    continue;
                }

                intents.Add(definition.Value);
                intentByAsset.Add(source, definition.Value);
                AddLocation(issueLocations, definition.Value.Id.Value, source, path, index);
            }

            var contexts = new List<InteractionContextDefinition>();
            var routes = new List<InteractionRoute>();
            var routeBindings = new List<InputRouteBinding>();
            var routeByAsset = new Dictionary<InteractionRouteAsset, InteractionRoute>();
            var contextIds = new HashSet<string>(StringComparer.Ordinal);
            var routeIds = new HashSet<string>(StringComparer.Ordinal);

            for (var contextIndex = 0; contextIndex < asset.Contexts.Count; contextIndex++)
            {
                var sourceContext = asset.Contexts[contextIndex];
                var contextPath = $"contexts[{contextIndex}]";
                if (sourceContext == null)
                {
                    issues.Add(Issue(InteractionAssetIssueCode.NullAsset, asset, contextPath, contextIndex,
                        string.Empty, "Context asset must not be null."));
                    continue;
                }

                var contextId = ContextId.TryCreate(sourceContext.ContextId);
                if (!contextId.Succeeded)
                {
                    issues.Add(CoreIssue(sourceContext, contextPath + ".contextId", contextIndex,
                        sourceContext.ContextId, contextId.Error));
                    continue;
                }

                if (!contextIds.Add(contextId.Value.Value))
                {
                    issues.Add(Issue(InteractionAssetIssueCode.DuplicateIdentity, sourceContext,
                        contextPath + ".contextId", contextIndex, contextId.Value.Value,
                        "Context identity must be unique in one registry asset."));
                    continue;
                }

                var contextRouteIds = new List<RouteId>();
                for (var routeIndex = 0; routeIndex < sourceContext.Routes.Count; routeIndex++)
                {
                    var sourceRoute = sourceContext.Routes[routeIndex];
                    var routePath = $"{contextPath}.routes[{routeIndex}]";
                    if (sourceRoute == null)
                    {
                        issues.Add(Issue(InteractionAssetIssueCode.NullAsset, sourceContext, routePath,
                            routeIndex, string.Empty, "Route asset must not be null."));
                        continue;
                    }

                    if (sourceRoute.Intent == null || !intentByAsset.TryGetValue(sourceRoute.Intent, out var intent))
                    {
                        issues.Add(Issue(InteractionAssetIssueCode.UnknownIntent, sourceRoute,
                            routePath + ".intent", routeIndex, sourceRoute.Intent == null ? string.Empty : sourceRoute.Intent.IntentId,
                            "Route must reference an intent admitted by the same registry asset."));
                        continue;
                    }

                    var routeId = RouteId.TryCreate(sourceRoute.RouteId);
                    var sourceId = SourceId.TryCreate(sourceRoute.SourceSelector);
                    if (!routeId.Succeeded)
                    {
                        issues.Add(CoreIssue(sourceRoute, routePath + ".routeId", routeIndex,
                            sourceRoute.RouteId, routeId.Error));
                        continue;
                    }
                    if (!sourceId.Succeeded)
                    {
                        issues.Add(CoreIssue(sourceRoute, routePath + ".sourceSelector", routeIndex,
                            sourceRoute.SourceSelector, sourceId.Error));
                        continue;
                    }
                    if (!routeIds.Add(routeId.Value.Value))
                    {
                        issues.Add(Issue(InteractionAssetIssueCode.DuplicateIdentity, sourceRoute,
                            routePath + ".routeId", routeIndex, routeId.Value.Value,
                            "Route identity must be globally unique in one registry asset."));
                        continue;
                    }

                    var action = sourceRoute.Action == null ? null : sourceRoute.Action.action;
                    if (action == null || action.id == Guid.Empty)
                    {
                        issues.Add(Issue(InteractionAssetIssueCode.MissingInputAction, sourceRoute,
                            routePath + ".action", routeIndex, routeId.Value.Value,
                            "Route requires an explicit InputActionReference with a stable action GUID."));
                        continue;
                    }

                    var descriptor = Encoding.UTF8.GetBytes("input-system-action:" + action.id.ToString("D"));
                    var route = InteractionRoute.Create(
                        routeId.Value,
                        contextId.Value,
                        intent.Id,
                        sourceId.Value,
                        sourceRoute.SourceModality,
                        sourceRoute.SourceCapabilities,
                        descriptor,
                        sourceRoute.RouteOrder);
                    if (!route.Succeeded)
                    {
                        issues.Add(CoreIssue(sourceRoute, routePath, routeIndex, routeId.Value.Value, route.Error));
                        continue;
                    }

                    routes.Add(route.Value);
                    contextRouteIds.Add(route.Value.Id);
                    routeByAsset.Add(sourceRoute, route.Value);
                    AddLocation(issueLocations, route.Value.Id.Value, sourceRoute, routePath, routeIndex);
                    routeBindings.Add(new InputRouteBinding(
                        route.Value.Id,
                        route.Value.IntentId,
                        route.Value.SourceSelector,
                        route.Value.SourceModality,
                        route.Value.SourceCapabilities,
                        intent.ValueKind,
                        sourceRoute.Action));
                }

                var context = InteractionContextDefinition.Create(
                    contextId.Value,
                    sourceContext.Priority,
                    contextRouteIds);
                if (!context.Succeeded)
                {
                    issues.Add(CoreIssue(sourceContext, contextPath, contextIndex, contextId.Value.Value, context.Error));
                    continue;
                }
                contexts.Add(context.Value);
                AddLocation(issueLocations, context.Value.Id.Value, sourceContext, contextPath, contextIndex);
            }

            var suggestions = new List<BindingSuggestion>();
            var suggestionIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < asset.BindingSuggestions.Count; index++)
            {
                var source = asset.BindingSuggestions[index];
                var path = $"bindingSuggestions[{index}]";
                if (source == null)
                {
                    issues.Add(Issue(InteractionAssetIssueCode.NullAsset, asset, path, index, string.Empty,
                        "Binding suggestion asset must not be null."));
                    continue;
                }
                if (source.Route == null || !routeByAsset.TryGetValue(source.Route, out var route))
                {
                    issues.Add(Issue(InteractionAssetIssueCode.UnknownRoute, source, path + ".route", index,
                        source.Route == null ? string.Empty : source.Route.RouteId,
                        "Binding suggestion must reference a route admitted by the same registry asset."));
                    continue;
                }

                var id = BindingSuggestionId.TryCreate(source.SuggestionId);
                if (!id.Succeeded || string.IsNullOrWhiteSpace(source.ProposedBindingPath))
                {
                    var error = id.Succeeded
                        ? new InteractionError(InteractionValidationCode.InvalidBindingSuggestion,
                            "Proposed binding path must not be empty.", source.SuggestionId)
                        : id.Error;
                    issues.Add(CoreIssue(source, path, index, source.SuggestionId, error));
                    continue;
                }
                if (!suggestionIds.Add(id.Value.Value))
                {
                    issues.Add(Issue(InteractionAssetIssueCode.DuplicateIdentity, source,
                        path + ".suggestionId", index, id.Value.Value,
                        "Binding suggestion identity must be unique in one registry asset."));
                    continue;
                }

                var suggestion = BindingSuggestion.Create(
                    id.Value,
                    route.IntentId,
                    route.Id,
                    AdapterKind,
                    Encoding.UTF8.GetBytes(source.ProposedBindingPath.Trim()));
                if (!suggestion.Succeeded)
                {
                    issues.Add(CoreIssue(source, path, index, source.SuggestionId, suggestion.Error));
                    continue;
                }
                suggestions.Add(suggestion.Value);
                AddLocation(issueLocations, suggestion.Value.Id.Value, source, path, index);
            }

            if (issues.Count > 0)
                return Failed(issues);

            var registry = InteractionRegistry.Create(intents, contexts, routes, suggestions);
            if (!registry.Succeeded)
            {
                if (!string.IsNullOrEmpty(registry.Error.Subject)
                    && issueLocations.TryGetValue(registry.Error.Subject, out var location))
                {
                    issues.Add(CoreIssue(
                        location.Asset,
                        location.FieldPath,
                        location.Index,
                        registry.Error.Subject,
                        registry.Error));
                }
                else
                {
                    issues.Add(CoreIssue(asset, "$", -1, registry.Error.Subject, registry.Error));
                }
                return Failed(issues);
            }

            return new InteractionAuthoringResult(
                new InteractionUnityRegistry(registry.Value, routeBindings),
                new InteractionAssetValidationReport(issues));
        }

        private static InteractionAuthoringResult Failed(IEnumerable<InteractionAssetIssue> issues) =>
            new InteractionAuthoringResult(null, new InteractionAssetValidationReport(issues));

        private static InteractionAssetIssue CoreIssue(
            UnityEngine.Object asset,
            string fieldPath,
            int index,
            string subject,
            InteractionError error) =>
            Issue(InteractionAssetIssueCode.CoreConversionFailed, asset, fieldPath, index, subject,
                $"{error.Code}: {error.Message}");

        private static InteractionAssetIssue Issue(
            InteractionAssetIssueCode code,
            UnityEngine.Object asset,
            string fieldPath,
            int index,
            string subject,
            string message) =>
            new InteractionAssetIssue(code, asset == null ? string.Empty : asset.name,
                fieldPath, index, subject, message);

        private static void AddLocation(
            IDictionary<string, IssueLocation> locations,
            string subject,
            UnityEngine.Object asset,
            string fieldPath,
            int index)
        {
            if (!string.IsNullOrEmpty(subject) && !locations.ContainsKey(subject))
                locations.Add(subject, new IssueLocation(asset, fieldPath, index));
        }

        private sealed class IssueLocation
        {
            public IssueLocation(UnityEngine.Object asset, string fieldPath, int index)
            {
                Asset = asset;
                FieldPath = fieldPath;
                Index = index;
            }

            public UnityEngine.Object Asset { get; }
            public string FieldPath { get; }
            public int Index { get; }
        }
    }
}
