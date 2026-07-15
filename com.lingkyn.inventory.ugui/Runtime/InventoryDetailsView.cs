using UnityEngine;
using UnityEngine.UI;

namespace Lingkyn.Inventory.UGUI
{
    public sealed class InventoryDetailsView : MonoBehaviour
    {
        [SerializeField] private Text label;
        public void Render(InventoryViewModel model) { if (label != null) label.text = $"{model.State}: {model.Message}"; }
    }
}
