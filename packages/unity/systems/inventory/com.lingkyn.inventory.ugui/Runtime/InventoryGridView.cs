using System;
using System.Collections.Generic;
using Lingkyn.Inventory.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Lingkyn.Inventory.UGUI
{
    public sealed class InventoryGridView : MonoBehaviour
    {
        [SerializeField] private Transform contentRoot;
        [SerializeField] private InventorySlotView slotTemplate;

        private readonly List<InventorySlotView> _slots = new List<InventorySlotView>();
        private InventorySkin _skin;

        public event Action<InventorySlotIntent> ActivationRequested;
        public event Action<InventorySlotIntent> SelectionRequested;
        [Obsolete("Use ActivationRequested so the stable SlotAddress is preserved.")]
        public event Action<int> SlotActivated;
        [Obsolete("Use SelectionRequested so the stable SlotAddress is preserved.")]
        public event Action<int> SlotSelected;

        public InventoryViewModel LastModel { get; private set; }
        public Transform ContentRoot => contentRoot;
        public InventorySlotView SlotTemplate => slotTemplate;
        public IReadOnlyList<InventorySlotView> SlotViews => _slots;
        public int ActiveSlotCount { get; private set; }

        public void ConfigureTemplate(InventorySlotView template, Transform root = null)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            if (_slots.Count > 0 || LastModel != null)
            {
                throw new InvalidOperationException("Configure the InventoryGridView template before its first Render call.");
            }

            slotTemplate = template;
            contentRoot = root != null ? root : template.transform.parent;
            EnsureConfigured();
            ApplyTemplateLayout();
        }

        public void ApplySkin(InventorySkin skin)
        {
            _skin = skin;
            if (skin != null) InventorySkin.StyleBackground(GetComponent<Image>(), skin.sectionColor, skin.sectionSprite);
            if (slotTemplate != null) slotTemplate.ApplySkin(skin);
            foreach (var slot in _slots)
            {
                if (slot != null) slot.ApplySkin(skin);
            }
        }

        public void Render(InventoryViewModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            EnsureConfigured();
            LastModel = model;
            InitializeSlots();
            ApplyTemplateLayout();
            EnsureCapacity(model.Slots.Count);

            for (var index = 0; index < _slots.Count; index++)
            {
                var active = index < model.Slots.Count;
                if (!active && _slots[index].gameObject.activeSelf && EventSystem.current != null)
                {
                    var selected = EventSystem.current.currentSelectedGameObject;
                    if (selected != null && (selected == _slots[index].gameObject || selected.transform.IsChildOf(_slots[index].transform)))
                    {
                        EventSystem.current.SetSelectedGameObject(null);
                    }
                }
                if (active)
                {
                    // Bind before enabling so OnEnable observers can never see a pooled slot's old address.
                    _slots[index].Bind(index, model.Slots[index]);
                    _slots[index].gameObject.SetActive(true);
                }
                else
                {
                    _slots[index].Unbind();
                    _slots[index].gameObject.SetActive(false);
                }
            }

            ActiveSlotCount = model.Slots.Count;
        }

        private void InitializeSlots()
        {
            if (_slots.Count > 0) return;
            AddSlot(slotTemplate);
        }

        private void EnsureCapacity(int count)
        {
            while (_slots.Count < count)
            {
                var slot = Instantiate(slotTemplate, contentRoot);
                slot.name = $"{slotTemplate.name}_{_slots.Count}";
                if (_skin != null) slot.ApplySkin(_skin);
                AddSlot(slot);
            }
        }

        private void AddSlot(InventorySlotView slot)
        {
            _slots.Add(slot);
            slot.ActivationRequested += OnActivationRequested;
            slot.SelectionRequested += OnSelectionRequested;
        }

        private void OnDestroy()
        {
            foreach (var slot in _slots)
            {
                if (slot == null) continue;
                slot.ActivationRequested -= OnActivationRequested;
                slot.SelectionRequested -= OnSelectionRequested;
            }
        }

        private void EnsureConfigured()
        {
            if (contentRoot == null) throw new InvalidOperationException("InventoryGridView requires a content root.");
            if (slotTemplate == null) throw new InvalidOperationException("InventoryGridView requires a bound slot template.");
            if (!slotTemplate.transform.IsChildOf(contentRoot))
            {
                throw new InvalidOperationException("InventoryGridView slot template must be a child of its content root.");
            }
        }

        private void ApplyTemplateLayout()
        {
            var layout = contentRoot.GetComponent<GridLayoutGroup>();
            if (layout == null) return;

            var element = slotTemplate.GetComponent<LayoutElement>();
            var rect = slotTemplate.RectTransform.rect;
            var width = element != null && element.preferredWidth > 0f ? element.preferredWidth : rect.width;
            var height = element != null && element.preferredHeight > 0f ? element.preferredHeight : rect.height;
            if (width > 0f && height > 0f) layout.cellSize = new Vector2(width, height);
        }

        private void OnActivationRequested(InventorySlotIntent intent)
        {
            ActivationRequested?.Invoke(intent);
#pragma warning disable CS0618
            SlotActivated?.Invoke(intent.DisplayIndex);
#pragma warning restore CS0618
        }

        private void OnSelectionRequested(InventorySlotIntent intent)
        {
            SelectionRequested?.Invoke(intent);
#pragma warning disable CS0618
            SlotSelected?.Invoke(intent.DisplayIndex);
#pragma warning restore CS0618
        }
    }
}
