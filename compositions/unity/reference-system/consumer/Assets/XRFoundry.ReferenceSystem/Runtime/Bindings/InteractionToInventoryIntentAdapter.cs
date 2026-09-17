using System;
using System.Collections.Generic;
using Lingkyn.Interaction.Core;
using Lingkyn.Inventory.Core;
using Lingkyn.Inventory.Presentation;

namespace XRFoundry.ReferenceSystem.Bindings
{
    /// <summary>
    /// Consumer-owned binding from routed semantic interaction events to stable
    /// inventory slot intents. Physical input activation remains the interaction
    /// system's responsibility.
    /// </summary>
    public sealed class InteractionToInventoryIntentAdapter
    {
        private readonly InventoryPresenter _presenter;
        private readonly IntentId _selectionIntentId;
        private readonly IReadOnlyDictionary<RouteId, SlotAddress> _slotByRoute;

        public InteractionToInventoryIntentAdapter(
            InventoryPresenter presenter,
            IntentId selectionIntentId,
            IReadOnlyDictionary<RouteId, SlotAddress> slotByRoute)
        {
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            if (string.IsNullOrEmpty(selectionIntentId.Value))
            {
                throw new ArgumentException("A selection intent identity is required.", nameof(selectionIntentId));
            }

            if (slotByRoute == null)
            {
                throw new ArgumentNullException(nameof(slotByRoute));
            }

            var frozen = new Dictionary<RouteId, SlotAddress>();
            foreach (var entry in slotByRoute)
            {
                if (string.IsNullOrEmpty(entry.Key.Value))
                {
                    throw new ArgumentException("Route identities must not be empty.", nameof(slotByRoute));
                }

                if (string.IsNullOrEmpty(entry.Value.ContainerId.Value))
                {
                    throw new ArgumentException("Slot addresses must identify a container.", nameof(slotByRoute));
                }

                frozen.Add(entry.Key, entry.Value);
            }

            _selectionIntentId = selectionIntentId;
            _slotByRoute = frozen;
        }

        public InteractionHandlerOutcome Handle(SemanticInteractionEvent semanticEvent)
        {
            if (semanticEvent.Phase != InteractionPhase.Performed
                || !semanticEvent.IntentId.Equals(_selectionIntentId))
            {
                return InteractionHandlerOutcome.Deferred;
            }

            if (!_slotByRoute.TryGetValue(semanticEvent.RouteId, out var address))
            {
                return InteractionHandlerOutcome.Failed;
            }

            var current = _presenter.Current;
            if (current == null)
            {
                return InteractionHandlerOutcome.Failed;
            }

            InventorySlotViewModel slot = null;
            var displayIndex = -1;
            for (var index = 0; index < current.Slots.Count; index++)
            {
                if (!current.Slots[index].Address.Equals(address))
                {
                    continue;
                }

                if (slot != null)
                {
                    return InteractionHandlerOutcome.Failed;
                }

                slot = current.Slots[index];
                displayIndex = index;
            }

            if (slot == null || displayIndex < 0)
            {
                return InteractionHandlerOutcome.Failed;
            }

            if (!slot.Enabled)
            {
                return InteractionHandlerOutcome.Rejected;
            }

            try
            {
                var intent = new InventorySlotIntent(address, displayIndex);
                _presenter.Select(intent.Address);
                return InteractionHandlerOutcome.Accepted;
            }
            catch (InvalidOperationException)
            {
                return InteractionHandlerOutcome.Rejected;
            }
            catch (ArgumentException)
            {
                return InteractionHandlerOutcome.Failed;
            }
            catch (KeyNotFoundException)
            {
                return InteractionHandlerOutcome.Failed;
            }
        }
    }
}
