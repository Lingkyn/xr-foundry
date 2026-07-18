using System;
using Lingkyn.Inventory.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Lingkyn.Inventory.UGUI
{
    public sealed class InventoryActionMenuView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Button primaryAction;
        [SerializeField] private Text label;
        [SerializeField] private InventorySkin skin;

        public event Action PrimaryActionRequested;

        public CanvasGroup Group => group;
        public Button PrimaryAction => primaryAction;
        public Text Label => label;

        private void OnEnable()
        {
            if (primaryAction != null) primaryAction.onClick.AddListener(OnPrimaryAction);
        }

        private void OnDisable()
        {
            if (primaryAction != null) primaryAction.onClick.RemoveListener(OnPrimaryAction);
        }

        public void Render(InventoryViewModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (group == null) return;
            var enabled = model.State != InventoryUiState.Disabled &&
                          model.State != InventoryUiState.Loading &&
                          model.State != InventoryUiState.Error;
            group.interactable = enabled;
            group.blocksRaycasts = enabled;
            group.alpha = enabled ? 1f : 0.45f;
            if (primaryAction != null) primaryAction.interactable = enabled;
            if (label != null) label.text = enabled ? "Primary action" : "Unavailable";
        }

        public void ApplySkin(InventorySkin value)
        {
            skin = value;
            if (skin == null) return;
            InventorySkin.StyleBackground(GetComponent<Image>(), skin.sectionColor, skin.sectionSprite);
            if (primaryAction != null)
            {
                InventorySkin.StyleBackground(primaryAction.targetGraphic as Image, skin.accentColor, skin.sectionSprite);
            }
            InventorySkin.StyleText(label, skin.textColor, skin.font);
        }

        private void OnPrimaryAction() => PrimaryActionRequested?.Invoke();
    }
}
