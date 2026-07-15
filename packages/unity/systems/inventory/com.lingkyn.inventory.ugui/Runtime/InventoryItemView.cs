using Lingkyn.Inventory.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Lingkyn.Inventory.UGUI
{
    public sealed class InventoryItemView : MonoBehaviour
    {
        [SerializeField] private Text label;

        public Text Label => label;

        public void Render(InventorySlotViewModel model)
        {
            if (model == null) throw new System.ArgumentNullException(nameof(model));
            if (label != null)
            {
                label.text = model.DefinitionId.HasValue
                    ? $"{model.DefinitionId.Value} x{model.Quantity}"
                    : "Empty";
            }
        }

        internal void Clear()
        {
            if (label != null) label.text = "Empty";
        }
    }
}
