using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Lingkyn.Inventory.Core;

namespace Lingkyn.Inventory.UGUI
{
    public enum InventoryUiState
    {
        Empty,
        Partial,
        Full,
        Rejected,
        Selected,
        Disabled,
        Loading,
        Error,
    }

    public sealed class InventorySlotViewModel
    {
        public InventorySlotViewModel(SlotAddress address, ItemDefinitionId? definitionId, int quantity, bool selected, bool enabled)
        {
            Address = address;
            DefinitionId = definitionId;
            Quantity = quantity;
            Selected = selected;
            Enabled = enabled;
        }

        public SlotAddress Address { get; }
        public ItemDefinitionId? DefinitionId { get; }
        public int Quantity { get; }
        public bool Selected { get; }
        public bool Enabled { get; }
    }

    public sealed class InventoryViewModel
    {
        public InventoryViewModel(long revision, InventoryUiState state, IEnumerable<InventorySlotViewModel> slots, string message = "")
        {
            Revision = revision;
            State = state;
            Slots = new ReadOnlyCollection<InventorySlotViewModel>((slots ?? Array.Empty<InventorySlotViewModel>()).ToArray());
            Message = message ?? string.Empty;
        }

        public long Revision { get; }
        public InventoryUiState State { get; }
        public IReadOnlyList<InventorySlotViewModel> Slots { get; }
        public string Message { get; }
    }

    public interface IInventoryView
    {
        void Render(InventoryViewModel model);
    }

    public readonly struct InventorySlotIntent
    {
        public InventorySlotIntent(SlotAddress address, int displayIndex)
        {
            if (displayIndex < 0) throw new ArgumentOutOfRangeException(nameof(displayIndex));
            Address = address;
            DisplayIndex = displayIndex;
        }

        public SlotAddress Address { get; }
        public int DisplayIndex { get; }
    }

    public sealed class InventoryPresenter : IDisposable
    {
        private readonly InventoryAggregate _inventory;
        private readonly IInventoryView _view;
        private SlotAddress? _selected;
        private bool _disabled;

        public InventoryPresenter(InventoryAggregate inventory, IInventoryView view)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _inventory.Changed += OnChanged;
            _inventory.Restored += OnRestored;
            Refresh();
        }

        public InventoryViewModel Current { get; private set; }

        public MutationResult Execute(MutationRequest request)
        {
            if (_disabled)
            {
                Render(InventoryUiState.Disabled, "Inventory interaction is disabled.");
                throw new InvalidOperationException("InventoryPresenter cannot execute mutations while interaction is disabled.");
            }

            var result = _inventory.Execute(request);
            if (!result.Succeeded)
            {
                Render(InventoryUiState.Rejected, result.Message);
            }
            return result;
        }

        public void Select(SlotAddress address)
        {
            EnsureInteractionEnabled("select slots");
            _inventory.GetSnapshot().Get(address);
            _selected = address;
            Render(InventoryUiState.Selected);
        }

        public void SetDisabled(bool disabled)
        {
            _disabled = disabled;
            if (disabled) Render(InventoryUiState.Disabled);
            else Refresh();
        }

        public void Replay(InventoryUiState state, string message = "")
        {
            EnsureInteractionEnabled("replay presentation states");
            Render(state, message);
        }

        public void Refresh()
        {
            if (_disabled)
            {
                Render(InventoryUiState.Disabled, "Inventory interaction is disabled.");
                return;
            }

            var snapshot = _inventory.GetSnapshot();
            var slots = BuildSlots(snapshot).ToArray();
            var occupied = slots.Count(slot => slot.DefinitionId.HasValue);
            var state = occupied == 0 ? InventoryUiState.Empty : occupied == slots.Length ? InventoryUiState.Full : InventoryUiState.Partial;
            Render(state);
        }

        public void Dispose()
        {
            _inventory.Changed -= OnChanged;
            _inventory.Restored -= OnRestored;
        }

        private IEnumerable<InventorySlotViewModel> BuildSlots(InventorySnapshot snapshot)
        {
            foreach (var container in snapshot.Containers.OrderBy(item => item.Id.Value, StringComparer.Ordinal))
            {
                for (var index = 0; index < container.Capacity; index++)
                {
                    var address = new SlotAddress(container.Id, index);
                    var stack = container.Get(address);
                    yield return new InventorySlotViewModel(
                        address,
                        stack == null ? (ItemDefinitionId?)null : stack.DefinitionId,
                        stack?.Quantity ?? 0,
                        _selected.HasValue && _selected.Value == address,
                        !_disabled);
                }
            }
        }

        private void Render(InventoryUiState state, string message = "")
        {
            var snapshot = _inventory.GetSnapshot();
            Current = new InventoryViewModel(snapshot.Revision, state, BuildSlots(snapshot), message);
            _view.Render(Current);
        }

        private void EnsureInteractionEnabled(string operation)
        {
            if (!_disabled) return;
            Render(InventoryUiState.Disabled, "Inventory interaction is disabled.");
            throw new InvalidOperationException($"InventoryPresenter cannot {operation} while interaction is disabled.");
        }

        private void OnChanged(InventoryEvent _) => Refresh();
        private void OnRestored(InventorySnapshot _) => Refresh();
    }
}
