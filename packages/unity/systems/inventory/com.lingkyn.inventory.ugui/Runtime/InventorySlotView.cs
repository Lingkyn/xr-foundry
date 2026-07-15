using System;
using Lingkyn.Inventory.Core;
using Lingkyn.Inventory.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Lingkyn.Inventory.UGUI
{
    public sealed class InventorySlotView : MonoBehaviour,
        IPointerClickHandler,
        ISubmitHandler,
        ISelectHandler,
        IDeselectHandler,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        [SerializeField] private int slotIndex;
        [SerializeField] private InventoryItemView itemView;
        [SerializeField] private Selectable selectionControl;
        [SerializeField] private Image background;
        [SerializeField] private Color normalColor = new Color(0.10f, 0.14f, 0.18f, 1f);
        [SerializeField] private Color hoverColor = new Color(0.12f, 0.35f, 0.42f, 1f);
        [SerializeField] private Color selectedColor = new Color(0.10f, 0.65f, 0.72f, 1f);
        [SerializeField] private Color disabledColor = new Color(0.12f, 0.12f, 0.12f, 0.55f);

        private bool _modelSelected;
        private bool _navigationSelected;
        private bool _hovered;
        private InventorySlotIntent? _intent;

        public event Action<InventorySlotIntent> ActivationRequested;
        public event Action<InventorySlotIntent> SelectionRequested;
        [Obsolete("Use ActivationRequested so the stable SlotAddress is preserved.")]
        public event Action<int> Activated;
        [Obsolete("Use SelectionRequested so the stable SlotAddress is preserved.")]
        public event Action<int> Selected;

        public int SlotIndex => slotIndex;
        public InventoryItemView ItemView => itemView;
        public Selectable SelectionControl => selectionControl;
        public Image Background => background;
        public bool Interactable => selectionControl == null || selectionControl.IsInteractable();
        public RectTransform RectTransform => (RectTransform)transform;
        public SlotAddress? Address => _intent?.Address;

        public void Bind(int index, InventorySlotViewModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            slotIndex = index;
            _intent = new InventorySlotIntent(model.Address, index);
            _modelSelected = model.Selected;
            if (!model.Enabled)
            {
                _hovered = false;
                _navigationSelected = false;
            }
            if (selectionControl != null) selectionControl.interactable = model.Enabled;
            itemView?.Render(model);
            RefreshVisual();
        }

        internal void Unbind()
        {
            slotIndex = -1;
            _intent = null;
            _modelSelected = false;
            _hovered = false;
            _navigationSelected = false;
            if (selectionControl != null) selectionControl.interactable = false;
            itemView?.Clear();
            RefreshVisual();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button == PointerEventData.InputButton.Left && Interactable)
            {
                RaiseActivation();
            }
        }

        public void OnSubmit(BaseEventData _)
        {
            if (Interactable) RaiseActivation();
        }

        public void OnSelect(BaseEventData _)
        {
            _navigationSelected = true;
            RefreshVisual();
            if (Interactable) RaiseSelection();
        }

        public void OnDeselect(BaseEventData _)
        {
            _navigationSelected = false;
            RefreshVisual();
        }

        public void OnPointerEnter(PointerEventData _)
        {
            _hovered = true;
            RefreshVisual();
        }

        public void OnPointerExit(PointerEventData _)
        {
            _hovered = false;
            RefreshVisual();
        }

        private void OnDisable()
        {
            ResetTransientState();
        }

        internal void ResetTransientState()
        {
            _hovered = false;
            _navigationSelected = false;
            RefreshVisual();
        }

        private void RaiseActivation()
        {
            if (!_intent.HasValue) throw new InvalidOperationException("Bind the InventorySlotView before activating it.");
            ActivationRequested?.Invoke(_intent.Value);
#pragma warning disable CS0618
            Activated?.Invoke(slotIndex);
#pragma warning restore CS0618
        }

        private void RaiseSelection()
        {
            if (!_intent.HasValue) throw new InvalidOperationException("Bind the InventorySlotView before selecting it.");
            SelectionRequested?.Invoke(_intent.Value);
#pragma warning disable CS0618
            Selected?.Invoke(slotIndex);
#pragma warning restore CS0618
        }

        private void RefreshVisual()
        {
            if (background == null) return;
            background.color = !Interactable
                ? disabledColor
                : _modelSelected || _navigationSelected
                    ? selectedColor
                    : _hovered
                        ? hoverColor
                        : normalColor;
        }
    }
}
