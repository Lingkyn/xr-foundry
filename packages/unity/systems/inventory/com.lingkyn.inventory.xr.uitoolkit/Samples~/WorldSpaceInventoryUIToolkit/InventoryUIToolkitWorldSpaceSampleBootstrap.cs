using System.Linq;
using Lingkyn.Inventory.Core;
using Lingkyn.Inventory.Presentation;
using Lingkyn.Inventory.UIToolkit;
using Lingkyn.Inventory.XR.UIToolkit;
using UnityEngine;

namespace Lingkyn.Inventory.XR.UIToolkit.Samples
{
    [RequireComponent(typeof(InventoryUIToolkitWorldSpaceSurface))]
    public sealed class InventoryUIToolkitWorldSpaceSampleBootstrap : MonoBehaviour
    {
        [SerializeField] private InventoryUiState initialState = InventoryUiState.Partial;
        private InventoryUIToolkitWorldSpaceSurface _surface;

        public InventoryViewModel LastModel { get; private set; }
        public InventoryUIToolkitValidationReport LastValidation { get; private set; }

        private void Start()
        {
            _surface = GetComponent<InventoryUIToolkitWorldSpaceSurface>();
            ReplayState(initialState);
            LastValidation = _surface.Revalidate();
        }

        public void ReplayState(InventoryUiState state)
        {
            if (_surface == null) _surface = GetComponent<InventoryUIToolkitWorldSpaceSurface>();
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
            var items = new[] { "compass", "rope", "torch", "map", "radio", "water" };
            var slots = items.Select((item, index) => new InventorySlotViewModel(
                new SlotAddress(new ContainerId("world-space-gallery"), index),
                index < occupied ? new ItemDefinitionId(item) : (ItemDefinitionId?)null,
                index < occupied ? index + 1 : 0,
                state == InventoryUiState.Selected && index == 1,
                enabled));
            LastModel = new InventoryViewModel(
                LastModel?.Revision + 1 ?? 0,
                state,
                slots,
                $"World-Space State Gallery: {state}");
            _surface.InventoryView.Render(LastModel);
        }
    }
}
