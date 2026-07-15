using UnityEngine;

namespace Lingkyn.Inventory.UGUI
{
    public sealed class InventoryPanelView : MonoBehaviour
    {
        [SerializeField] private InventoryGridView grid;
        [SerializeField] private InventoryDetailsView details;
        [SerializeField] private InventoryActionMenuView actionMenu;
        public void Render(InventoryViewModel model)
        {
            if (grid != null) grid.Render(model);
            if (details != null) details.Render(model);
            if (actionMenu != null) actionMenu.Render(model);
        }
    }
}
