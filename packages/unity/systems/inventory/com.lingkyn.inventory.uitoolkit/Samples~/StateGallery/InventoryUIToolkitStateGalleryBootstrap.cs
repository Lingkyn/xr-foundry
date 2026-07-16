using System.Linq;
using Lingkyn.Inventory.Core;
using Lingkyn.Inventory.Presentation;
using Lingkyn.Inventory.UIToolkit;
using UnityEngine;

namespace Lingkyn.Inventory.UIToolkit.Samples
{
    [RequireComponent(typeof(InventoryDocumentView))]
    public sealed class InventoryUIToolkitStateGalleryBootstrap : MonoBehaviour
    {
        [SerializeField] private InventoryUiState initialState = InventoryUiState.Partial;
        private InventoryDocumentView _view;

        public InventoryViewModel LastModel { get; private set; }

        private void Start()
        {
            _view = GetComponent<InventoryDocumentView>();
            ReplayState(initialState);
        }

        public void ReplayState(InventoryUiState state)
        {
            if (_view == null) _view = GetComponent<InventoryDocumentView>();
            var occupied = state == InventoryUiState.Empty ||
                           state == InventoryUiState.Loading ||
                           state == InventoryUiState.Error
                ? 0
                : state == InventoryUiState.Full
                    ? 6
                    : 3;
            var enabled = state != InventoryUiState.Disabled &&
                          state != InventoryUiState.Loading &&
                          state != InventoryUiState.Error;
            var itemIds = new[] { "compass", "rope", "torch", "map", "radio", "water" };
            var slots = itemIds.Select((itemId, index) => new InventorySlotViewModel(
                new SlotAddress(new ContainerId("state-gallery"), index),
                index < occupied ? new ItemDefinitionId(itemId) : (ItemDefinitionId?)null,
                index < occupied ? index + 1 : 0,
                state == InventoryUiState.Selected && index == 1,
                enabled));
            LastModel = new InventoryViewModel(
                LastModel?.Revision + 1 ?? 0,
                state,
                slots,
                $"State Gallery: {state}");
            _view.Render(LastModel);
        }
    }
}
