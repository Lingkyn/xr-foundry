using System.Linq;
using Lingkyn.Inventory.Core;
using Lingkyn.Inventory.Presentation;
using UnityEngine;

namespace Lingkyn.Inventory.UGUI
{
    public sealed class InventoryStateGallery : MonoBehaviour, IInventoryView
    {
        [SerializeField] private InventoryShellView shell;
        [SerializeField] private InventoryUiState state;

        public InventoryShellView Shell => shell;
        public InventoryViewModel LastModel { get; private set; }

        public void Render(InventoryViewModel model)
        {
            if (model == null) throw new System.ArgumentNullException(nameof(model));
            LastModel = model;
            state = model.State;
            shell?.Render(model);
        }

        public void ReplayState(InventoryUiState next)
        {
            var occupied = next == InventoryUiState.Empty || next == InventoryUiState.Loading || next == InventoryUiState.Error
                ? 0
                : next == InventoryUiState.Full
                    ? 3
                    : 2;
            var enabled = next != InventoryUiState.Disabled && next != InventoryUiState.Loading && next != InventoryUiState.Error;
            var container = new ContainerId("state-gallery");
            var ids = new[] { "compass", "rope", "torch" };
            var slots = ids.Select((id, index) => new InventorySlotViewModel(
                new SlotAddress(container, index),
                index < occupied ? new ItemDefinitionId(id) : (ItemDefinitionId?)null,
                index < occupied ? index + 1 : 0,
                next == InventoryUiState.Selected && index == 1,
                enabled));
            Render(new InventoryViewModel(LastModel?.Revision ?? 0, next, slots, $"State Gallery: {next}"));
        }

        public InventoryUiState State => state;
    }
}
