using UnityEngine;

namespace Lingkyn.Inventory.UGUI
{
    public sealed class InventoryActionMenuView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        public void Render(InventoryViewModel model)
        {
            if (group == null) return;
            group.interactable = model.State != InventoryUiState.Disabled && model.State != InventoryUiState.Loading;
            group.alpha = group.interactable ? 1f : 0.5f;
        }
    }
}
