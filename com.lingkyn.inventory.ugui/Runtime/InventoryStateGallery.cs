using UnityEngine;

namespace Lingkyn.Inventory.UGUI
{
    public sealed class InventoryStateGallery : MonoBehaviour, IInventoryView
    {
        [SerializeField] private InventoryUiState state;
        public InventoryViewModel LastModel { get; private set; }
        public void Render(InventoryViewModel model) { LastModel = model; state = model.State; }
        public void ReplayState(InventoryUiState next) => state = next;
        public InventoryUiState State => state;
    }
}
