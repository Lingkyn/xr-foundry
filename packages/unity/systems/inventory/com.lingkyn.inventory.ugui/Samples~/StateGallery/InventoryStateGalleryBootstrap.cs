using System;
using Lingkyn.Inventory.Presentation;
using Lingkyn.Inventory.UGUI;
using UnityEngine;

namespace Lingkyn.Inventory.UGUI.Samples
{
    public sealed class InventoryStateGalleryBootstrap : MonoBehaviour
    {
        [SerializeField] private InventoryStateGallery gallery;
        [SerializeField] private InventoryUiState initialState = InventoryUiState.Partial;

        public InventoryStateGallery Gallery => gallery;

        private void Start() => Replay(initialState);

        [ContextMenu("Replay Next State")]
        public void ReplayNext()
        {
            var values = (InventoryUiState[])Enum.GetValues(typeof(InventoryUiState));
            var current = gallery == null ? initialState : gallery.State;
            Replay(values[(Array.IndexOf(values, current) + 1) % values.Length]);
        }

        public void Replay(InventoryUiState state)
        {
            if (gallery == null) throw new InvalidOperationException("Assign the shipped InventoryStateGallery instance.");
            gallery.ReplayState(state);
        }
    }
}
