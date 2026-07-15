using UnityEngine;

namespace Lingkyn.Inventory.UGUI
{
    public sealed class InventoryGridView : MonoBehaviour
    {
        public InventoryViewModel LastModel { get; private set; }
        public void Render(InventoryViewModel model) => LastModel = model;
    }
}
