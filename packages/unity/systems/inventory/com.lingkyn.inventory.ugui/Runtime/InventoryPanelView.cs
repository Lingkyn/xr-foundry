using Lingkyn.Inventory.Presentation;
using UnityEngine;
using UnityEngine.UI;

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

        public void ApplySkin(InventorySkin skin)
        {
            if (skin != null) InventorySkin.StyleBackground(GetComponent<Image>(), skin.panelColor, skin.panelSprite);
            if (grid != null) grid.ApplySkin(skin);
            if (details != null) details.ApplySkin(skin);
            if (actionMenu != null) actionMenu.ApplySkin(skin);
        }
    }
}
