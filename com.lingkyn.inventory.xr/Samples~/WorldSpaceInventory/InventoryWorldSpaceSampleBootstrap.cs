using System;
using Lingkyn.Inventory.UGUI;
using UnityEngine;

namespace Lingkyn.Inventory.XR.Samples
{
    public sealed class InventoryWorldSpaceSampleBootstrap : MonoBehaviour
    {
        [SerializeField] private InventoryWorldSpaceSurface surface;
        [SerializeField] private InventoryStateGallery gallery;
        [SerializeField] private InventoryUiState initialState = InventoryUiState.Partial;

        public InventoryWorldSpaceSurface Surface => surface;
        public InventoryStateGallery Gallery => gallery;

        private void Start()
        {
            if (surface == null) throw new InvalidOperationException("Assign the Inventory world-space surface.");
            if (gallery == null) throw new InvalidOperationException("Assign the Inventory state gallery.");
            if (surface.EventCamera == null)
            {
                var eventCamera = Camera.main;
                if (eventCamera == null) throw new InvalidOperationException("Inventory XR sample requires a Main Camera.");
                surface.BindEventCamera(eventCamera);
            }

            Replay(initialState);
            surface.ValidateSceneOrThrow();
        }

        [ContextMenu("Replay Next State")]
        public void ReplayNext()
        {
            var values = (InventoryUiState[])Enum.GetValues(typeof(InventoryUiState));
            Replay(values[(Array.IndexOf(values, initialState) + 1) % values.Length]);
        }

        public void Replay(InventoryUiState state)
        {
            if (surface == null) throw new InvalidOperationException("Assign the Inventory world-space surface.");
            if (gallery == null) throw new InvalidOperationException("Assign the Inventory state gallery.");
            initialState = state;
            gallery.ReplayState(state);
        }
    }
}
