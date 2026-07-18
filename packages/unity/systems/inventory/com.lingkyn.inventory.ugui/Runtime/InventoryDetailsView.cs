using Lingkyn.Inventory.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Lingkyn.Inventory.UGUI
{
    public sealed class InventoryDetailsView : MonoBehaviour
    {
        [SerializeField] private Text label;
        [SerializeField] private InventorySkin skin;

        public Text Label => label;

        public void Render(InventoryViewModel model)
        {
            if (model == null) throw new System.ArgumentNullException(nameof(model));
            if (label != null)
            {
                label.text = string.IsNullOrEmpty(model.Message)
                    ? model.State.ToString()
                    : $"{model.State}: {model.Message}";
            }
        }

        public void ApplySkin(InventorySkin value)
        {
            skin = value;
            if (skin == null) return;
            InventorySkin.StyleBackground(GetComponent<Image>(), skin.sectionColor, skin.sectionSprite);
            InventorySkin.StyleText(label, skin.textColor, skin.font);
        }
    }
}
