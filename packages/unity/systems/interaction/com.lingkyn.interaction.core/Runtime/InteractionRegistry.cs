using System;
using System.Collections.Generic;
using System.Linq;

namespace Lingkyn.Interaction.Core
{
    public sealed class InteractionRegistry
    {
        private readonly IntentDefinition[] _intents;
        private readonly InteractionContextDefinition[] _contexts;
        private readonly InteractionRoute[] _routes;
        private readonly BindingSuggestion[] _bindingSuggestions;
        private readonly Dictionary<string, IntentDefinition> _intentLookup;
        private readonly Dictionary<string, InteractionContextDefinition> _contextLookup;
        private readonly Dictionary<string, InteractionRoute> _routeLookup;
        private readonly Dictionary<string, BindingSuggestion> _bindingSuggestionLookup;

        private InteractionRegistry(
            IReadOnlyList<IntentDefinition> intents,
            IReadOnlyList<InteractionContextDefinition> contexts,
            IReadOnlyList<InteractionRoute> routes,
            IReadOnlyList<BindingSuggestion> bindingSuggestions)
        {
            _intents = intents.OrderBy(intent => intent.Id, Comparer<IntentId>.Default).ToArray();
            _contexts = contexts.OrderByDescending(context => context.Priority)
                .ThenBy(context => context.Id, Comparer<ContextId>.Default)
                .ToArray();
            _routes = routes
                .OrderBy(route => route.ContextId, Comparer<ContextId>.Default)
                .ThenBy(route => route.Id, Comparer<RouteId>.Default)
                .ToArray();
            _bindingSuggestions = bindingSuggestions
                .OrderBy(suggestion => suggestion.Id, Comparer<BindingSuggestionId>.Default)
                .ToArray();

            _intentLookup = BuildLookup(_intents, intent => intent.Id.Value);
            _contextLookup = BuildLookup(_contexts, context => context.Id.Value);
            _routeLookup = BuildLookup(_routes, route => route.Id.Value);
            _bindingSuggestionLookup = BuildLookup(_bindingSuggestions, suggestion => suggestion.Id.Value);
        }

        public IReadOnlyList<IntentDefinition> Intents => Array.AsReadOnly(_intents);
        public IReadOnlyList<InteractionContextDefinition> Contexts => Array.AsReadOnly(_contexts);
        public IReadOnlyList<InteractionRoute> Routes => Array.AsReadOnly(_routes);
        public IReadOnlyList<BindingSuggestion> BindingSuggestions => Array.AsReadOnly(_bindingSuggestions);

        public bool TryGetIntent(IntentId intentId, out IntentDefinition definition) =>
            _intentLookup.TryGetValue(intentId.Value ?? string.Empty, out definition);

        public bool TryGetContext(ContextId contextId, out InteractionContextDefinition definition) =>
            _contextLookup.TryGetValue(contextId.Value ?? string.Empty, out definition);

        public bool TryGetRoute(RouteId routeId, out InteractionRoute route) =>
            _routeLookup.TryGetValue(routeId.Value ?? string.Empty, out route);

        public bool TryGetBindingSuggestion(BindingSuggestionId suggestionId, out BindingSuggestion suggestion) =>
            _bindingSuggestionLookup.TryGetValue(suggestionId.Value ?? string.Empty, out suggestion);

        public static InteractionResult<InteractionRegistry> Create(
            IEnumerable<IntentDefinition> intents,
            IEnumerable<InteractionContextDefinition> contexts,
            IEnumerable<InteractionRoute> routes,
            IEnumerable<BindingSuggestion> bindingSuggestions = null)
        {
            if (intents == null || contexts == null || routes == null)
            {
                return InteractionResult<InteractionRegistry>.Fail(
                    InteractionValidationCode.InvalidDefinition,
                    "Intents, contexts, and routes are required.");
            }

            var intentList = new List<IntentDefinition>();
            var intentSeen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var intent in intents)
            {
                if (intent == null)
                {
                    return InteractionResult<InteractionRegistry>.Fail(
                        InteractionValidationCode.InvalidDefinition,
                        "Intent definition must not be null.");
                }

                if (!intentSeen.Add(intent.Id.Value))
                {
                    return InteractionResult<InteractionRegistry>.Fail(
                        InteractionValidationCode.DuplicateIdentity,
                        "Duplicate intent identity detected.",
                        intent.Id.Value);
                }

                intentList.Add(intent);
            }

            var routeList = new List<InteractionRoute>();
            var routeSeen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var route in routes)
            {
                if (route == null)
                {
                    return InteractionResult<InteractionRegistry>.Fail(
                        InteractionValidationCode.InvalidDefinition,
                        "Route must not be null.");
                }

                if (string.IsNullOrEmpty(route.Id.Value) || !routeSeen.Add(route.Id.Value))
                {
                    return InteractionResult<InteractionRegistry>.Fail(
                        InteractionValidationCode.DuplicateIdentity,
                        $"Duplicate route identity '{route.Id.Value}'.",
                        route.Id.Value);
                }

                if (!intentSeen.Contains(route.IntentId.Value))
                {
                    return InteractionResult<InteractionRegistry>.Fail(
                        InteractionValidationCode.UnknownIntent,
                        $"Route '{route.Id.Value}' references unknown intent '{route.IntentId.Value}'.",
                        route.Id.Value);
                }

                routeList.Add(route);
            }

            var contextList = new List<InteractionContextDefinition>();
            var contextSeen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var context in contexts)
            {
                if (context == null)
                {
                    return InteractionResult<InteractionRegistry>.Fail(
                        InteractionValidationCode.InvalidDefinition,
                        "Context must not be null.");
                }

                if (!contextSeen.Add(context.Id.Value))
                {
                    return InteractionResult<InteractionRegistry>.Fail(
                        InteractionValidationCode.DuplicateIdentity,
                        "Duplicate context identity detected.",
                        context.Id.Value);
                }

                foreach (var routeId in context.RouteIds)
                {
                    if (!routeSeen.Contains(routeId.Value))
                    {
                        return InteractionResult<InteractionRegistry>.Fail(
                            InteractionValidationCode.InvalidDefinition,
                            $"Context '{context.Id.Value}' references route '{routeId.Value}' that is not owned by the context.",
                            context.Id.Value);
                    }
                }

                contextList.Add(context);
            }

            foreach (var route in routeList)
            {
                if (!contextSeen.Contains(route.ContextId.Value))
                {
                    return InteractionResult<InteractionRegistry>.Fail(
                        InteractionValidationCode.UnknownContext,
                        $"Route '{route.Id.Value}' references unknown context '{route.ContextId.Value}'.",
                        route.Id.Value);
                }

                if (!TryContextContainsRoute(contextList, route))
                {
                    return InteractionResult<InteractionRegistry>.Fail(
                        InteractionValidationCode.InvalidDefinition,
                        $"Route '{route.Id.Value}' is not listed by context '{route.ContextId.Value}'.",
                        route.Id.Value);
                }
            }

            var suggestionList = new List<BindingSuggestion>();
            var suggestionSeen = new HashSet<string>(StringComparer.Ordinal);
            if (bindingSuggestions != null)
            {
                foreach (var suggestion in bindingSuggestions)
                {
                    if (suggestion == null)
                    {
                        return InteractionResult<InteractionRegistry>.Fail(
                            InteractionValidationCode.InvalidBindingSuggestion,
                            "Binding suggestion must not be null.");
                    }

                    if (!suggestionSeen.Add(suggestion.Id.Value))
                    {
                        return InteractionResult<InteractionRegistry>.Fail(
                            InteractionValidationCode.DuplicateIdentity,
                            "Duplicate binding suggestion identity detected.",
                            suggestion.Id.Value);
                    }

                    if (!intentSeen.Contains(suggestion.IntentId.Value))
                    {
                        return InteractionResult<InteractionRegistry>.Fail(
                            InteractionValidationCode.UnknownIntent,
                            $"Binding suggestion '{suggestion.Id.Value}' references unknown intent '{suggestion.IntentId.Value}'.",
                            suggestion.Id.Value);
                    }

                    if (!routeList.Any(route =>
                            route.Id.Equals(suggestion.RouteId)
                            && route.IntentId.Equals(suggestion.IntentId)))
                    {
                        return InteractionResult<InteractionRegistry>.Fail(
                            InteractionValidationCode.UnknownRoute,
                            $"Binding suggestion '{suggestion.Id.Value}' references unknown route '{suggestion.RouteId.Value}'.",
                            suggestion.Id.Value);
                    }

                    suggestionList.Add(suggestion);
                }
            }

            return InteractionResult<InteractionRegistry>.Success(
                new InteractionRegistry(intentList, contextList, routeList, suggestionList));
        }

        private static bool TryContextContainsRoute(IEnumerable<InteractionContextDefinition> contexts, InteractionRoute route)
        {
            foreach (var context in contexts)
            {
                if (!context.Id.Equals(route.ContextId))
                {
                    continue;
                }

                return context.RouteIds.Any(routeId => routeId.Equals(route.Id));
            }

            return false;
        }

        private static Dictionary<string, T> BuildLookup<T>(IEnumerable<T> items, Func<T, string> keySelector)
        {
            var lookup = new Dictionary<string, T>(StringComparer.Ordinal);
            foreach (var item in items)
            {
                lookup[keySelector(item)] = item;
            }

            return lookup;
        }

    }
}
