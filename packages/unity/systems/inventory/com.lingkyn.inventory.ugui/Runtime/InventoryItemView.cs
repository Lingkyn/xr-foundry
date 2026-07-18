using Lingkyn.Inventory.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Lingkyn.Inventory.UGUI
{
    public sealed class InventoryItemView : MonoBehaviour
    {
        [SerializeField] private Text label;
        [SerializeField] private InventorySkin skin;

        public Text Label => label;

        public void Render(InventorySlotViewModel model)
        {
            if (model == null) throw new System.ArgumentNullException(nameof(model));
            if (label != null)
            {
                var occupied = model.DefinitionId.HasValue;
                label.text = occupied
                    ? $"{model.DefinitionId.Value} x{model.Quantity}"
                    : "Empty";
                if (skin != null) label.color = occupied ? skin.textColor : skin.mutedTextColor;
            }
        }

        public void ApplySkin(InventorySkin value)
        {
            skin = value;
            if (skin != null) InventorySkin.StyleText(label, skin.textColor, skin.font);
        }

        internal void Clear()
        {
            if (label != null)
            {
                label.text = "Empty";
                if (skin != null) label.color = skin.mutedTextColor;
            }
        }
    }
}
