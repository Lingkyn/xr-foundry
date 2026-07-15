using System;
using UnityEngine;
using UnityEngine.UI;

namespace Lingkyn.Inventory.UGUI
{
    public sealed class InventoryActionMenuView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Button primaryAction;
        [SerializeField] private Text label;

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

        private void OnPrimaryAction() => PrimaryActionRequested?.Invoke();
    }
}
