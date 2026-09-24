using System;
using System.Collections.Generic;
using System.Linq;

namespace Lingkyn.Haptics.Core
{
    // A binding table from another family's semantic identity to a haptic event, from any routing
    // or mixing logic that family owns. The Core never interprets a source id: it is reused text
    // (the Interaction family's IntentId, or the Audio family's event identity) carried opaquely.

    /// <summary>The closed set of source kinds a <see cref="HapticBinding"/> may name.</summary>
    public enum HapticSourceKind
    {
        InteractionIntent,
        AudioEvent,
    }

    /// <summary>One binding: a source kind and an opaque source id this family never interprets,
    /// the haptic event that source also fires, and the target that binding plays it on by
    /// default.</summary>
    public sealed class HapticBinding
    {
        public HapticBinding(HapticSourceKind sourceKind, string sourceId, HapticEventId eventId, HapticTarget defaultTarget)
        {
            SourceKind = sourceKind;
            SourceId = sourceId ?? string.Empty;
            EventId = eventId;
            DefaultTarget = defaultTarget;
        }

        public HapticSourceKind SourceKind { get; }
        public string SourceId { get; }
        public HapticEventId EventId { get; }
        public HapticTarget DefaultTarget { get; }
    }

    /// <summary>Orders binding keys by source kind, then by source id — the canonical order a
    /// validated <see cref="HapticBindingTable"/> enumerates in.</summary>
    internal sealed class HapticBindingKeyComparer : IComparer<(HapticSourceKind SourceKind, string SourceId)>
    {
        public static readonly HapticBindingKeyComparer Instance = new HapticBindingKeyComparer();

        public int Compare((HapticSourceKind SourceKind, string SourceId) x, (HapticSourceKind SourceKind, string SourceId) y)
        {
            var kindCompare = ((int)x.SourceKind).CompareTo((int)y.SourceKind);
            return kindCompare != 0 ? kindCompare : string.CompareOrdinal(x.SourceId, y.SourceId);
        }
    }

    /// <summary>The immutable, validated set of bindings. Enumerates in canonical
    /// (source kind, source id) order and resolves one source kind and id to at most one
    /// binding.</summary>
    public sealed class HapticBindingTable
    {
        private readonly SortedDictionary<(HapticSourceKind SourceKind, string SourceId), HapticBinding> _bindings;

        private HapticBindingTable(SortedDictionary<(HapticSourceKind SourceKind, string SourceId), HapticBinding> bindings)
        {
            _bindings = bindings;
        }

        public int Count => _bindings.Count;

        /// <summary>Every validated binding, in canonical (source kind, source id) order.</summary>
        public IEnumerable<HapticBinding> Bindings => _bindings.Values;

        /// <summary>Validates a list of bindings against an event registry: a binding whose haptic
        /// event id is not registered is rejected with <see cref="HapticFailure.EventUnknown"/>, and
        /// a second binding for the same source kind and id with
        /// <see cref="HapticFailure.BindingDuplicate"/>. The first invalid binding stops validation
        /// and leaves no table.</summary>
        public static HapticResult<HapticBindingTable> Validate(IEnumerable<HapticBinding> bindings, HapticEventRegistry registry)
        {
            if (bindings == null) throw new ArgumentNullException(nameof(bindings));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            var map = new SortedDictionary<(HapticSourceKind SourceKind, string SourceId), HapticBinding>(HapticBindingKeyComparer.Instance);
            foreach (var binding in bindings)
            {
                if (binding == null) throw new ArgumentException("A binding must not be null.", nameof(bindings));
                if (!registry.TryGet(binding.EventId, out _))
                {
                    return HapticResult<HapticBindingTable>.Fail(HapticFailure.EventUnknown, $"Binding for {binding.SourceKind}:'{binding.SourceId}' names haptic event '{binding.EventId}' which is not registered.");
                }
                var key = (binding.SourceKind, binding.SourceId);
                if (map.ContainsKey(key))
                {
                    return HapticResult<HapticBindingTable>.Fail(HapticFailure.BindingDuplicate, $"A binding for {binding.SourceKind}:'{binding.SourceId}' already exists.");
                }
                map[key] = binding;
            }
            return HapticResult<HapticBindingTable>.Ok(new HapticBindingTable(map));
        }

        public bool TryGet(HapticSourceKind sourceKind, string sourceId, out HapticBinding binding) =>
            _bindings.TryGetValue((sourceKind, sourceId ?? string.Empty), out binding);

        /// <summary>Resolves one source intent independently to a haptic play intent (if bound).
        /// This lookup is pure and side-effect free — it never mutates this table and never blocks
        /// on, or is blocked by, any other family's own resolution of the same source kind and id
        /// (the Audio family's own binding, and, once it exists, a feedback-effect family's), so one
        /// grab or one hit dispatches haptics, audio, and a feedback effect from one source intent
        /// without any of the three blocking the others. A source with no haptic binding produces a
        /// named <see cref="HapticFailure.BindingUnbound"/> outcome rather than doing nothing
        /// silently.</summary>
        public HapticDispatchOutcome Dispatch(HapticSourceKind sourceKind, string sourceId, IntentActor actor = IntentActor.Player, long? expectedRevision = null)
        {
            var id = sourceId ?? string.Empty;
            if (TryGet(sourceKind, id, out var binding))
            {
                var intent = new PlayIntent(binding.EventId, binding.DefaultTarget, null, null, null, actor, expectedRevision);
                return HapticDispatchOutcome.BoundTo(intent, sourceKind, id);
            }
            return HapticDispatchOutcome.Unbound(sourceKind, id);
        }
    }

    /// <summary>The result of dispatching one source kind and id through a
    /// <see cref="HapticBindingTable"/>: either a bound haptic play <see cref="Intent"/>, or a
    /// named <see cref="HapticFailure.BindingUnbound"/> outcome naming the source kind and id —
    /// never silence.</summary>
    public readonly struct HapticDispatchOutcome
    {
        private HapticDispatchOutcome(bool bound, PlayIntent intent, HapticSourceKind sourceKind, string sourceId, string code, string message)
        {
            Bound = bound;
            Intent = intent;
            SourceKind = sourceKind;
            SourceId = sourceId ?? string.Empty;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public bool Bound { get; }
        /// <summary>Meaningful only when <see cref="Bound"/> is true.</summary>
        public PlayIntent Intent { get; }
        public HapticSourceKind SourceKind { get; }
        public string SourceId { get; }
        /// <summary>Empty when bound; otherwise <see cref="HapticFailure.BindingUnbound"/>.</summary>
        public string Code { get; }
        public string Message { get; }

        public static HapticDispatchOutcome BoundTo(PlayIntent intent, HapticSourceKind sourceKind, string sourceId) =>
            new HapticDispatchOutcome(true, intent, sourceKind, sourceId, HapticFailure.None, string.Empty);

        public static HapticDispatchOutcome Unbound(HapticSourceKind sourceKind, string sourceId) =>
            new HapticDispatchOutcome(false, null, sourceKind, sourceId, HapticFailure.BindingUnbound, $"No haptic binding for {sourceKind}:'{sourceId}'.");
    }
}
