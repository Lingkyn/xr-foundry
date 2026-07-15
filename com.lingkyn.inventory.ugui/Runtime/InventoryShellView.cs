using UnityEngine;

namespace Lingkyn.Inventory.UGUI
{
    public sealed class InventoryShellView : MonoBehaviour, IInventoryView
    {
        [SerializeField] private InventoryPanelView panel;
        public InventoryViewModel LastModel { get; private set; }
        public void Render(InventoryViewModel model) { LastModel = model; if (panel != null) panel.Render(model); }
    }
}
