using Lingkyn.Inventory.Presentation;
using UnityEngine;

namespace Lingkyn.Inventory.UGUI
{
    public sealed class InventoryShellView : MonoBehaviour, IInventoryView
    {
        [SerializeField] private InventoryPanelView panel;

        public InventoryPanelView Panel => panel;
        public InventoryViewModel LastModel { get; private set; }

        public void Render(InventoryViewModel model)
        {
            if (model == null) throw new System.ArgumentNullException(nameof(model));
            LastModel = model;
            if (panel != null) panel.Render(model);
        }
    }
}
