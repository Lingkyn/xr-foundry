using UnityEngine;
using UnityEngine.UI;

namespace Lingkyn.Inventory.UGUI
{
    public sealed class InventoryItemView : MonoBehaviour
    {
        [SerializeField] private Text label;
        public void Render(InventorySlotViewModel model)
        {
            if (label != null) label.text = model.DefinitionId.HasValue ? $"{model.DefinitionId.Value} x{model.Quantity}" : "Empty";
        }
    }
}
