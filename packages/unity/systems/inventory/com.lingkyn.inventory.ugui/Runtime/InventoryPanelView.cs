using Lingkyn.Inventory.Presentation;
using UnityEngine;

namespace Lingkyn.Inventory.UGUI
{
    public sealed class InventoryPanelView : MonoBehaviour
    {
        [SerializeField] private InventoryGridView grid;
        [SerializeField] private InventoryDetailsView details;
        [SerializeField] private InventoryActionMenuView actionMenu;

        public InventoryGridView Grid => grid;
        public InventoryDetailsView Details => details;
        public InventoryActionMenuView ActionMenu => actionMenu;

        public void Render(InventoryViewModel model)
        {
            if (model == null) throw new System.ArgumentNullException(nameof(model));
            if (grid != null) grid.Render(model);
            if (details != null) details.Render(model);
            if (actionMenu != null) actionMenu.Render(model);
        }
    }
}
