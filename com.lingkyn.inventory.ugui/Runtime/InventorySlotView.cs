using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Lingkyn.Inventory.UGUI
{
    public sealed class InventorySlotView : MonoBehaviour, IPointerClickHandler, ISubmitHandler, ISelectHandler
    {
        [SerializeField] private int slotIndex;
        [SerializeField] private InventoryItemView itemView;
        public event Action<int> Activated;
        public event Action<int> Selected;
        public int SlotIndex => slotIndex;
        public InventoryItemView ItemView => itemView;
        public void OnPointerClick(PointerEventData _) => Activated?.Invoke(slotIndex);
        public void OnSubmit(BaseEventData _) => Activated?.Invoke(slotIndex);
        public void OnSelect(BaseEventData _) => Selected?.Invoke(slotIndex);
    }
}
