using System;
using System.Collections.Generic;
using System.Linq;
using Lingkyn.Inventory.Presentation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lingkyn.Inventory.UIToolkit
{
    public static class InventoryDocumentContract
    {
        public const string Root = "inventory-root";
        public const string State = "inventory-state";
        public const string Message = "inventory-message";
        public const string Grid = "inventory-grid";
        public const string Details = "inventory-details";
        public const string PrimaryAction = "inventory-primary-action";

        public static IReadOnlyList<string> FindMissingParts(VisualElement root)
        {
            if (root == null) return new[] { Root, State, Message, Grid, Details, PrimaryAction };
            var missing = new List<string>();
            Require<VisualElement>(root, Root, missing);
            Require<Label>(root, State, missing);
            Require<Label>(root, Message, missing);
            Require<VisualElement>(root, Grid, missing);
            Require<Label>(root, Details, missing);
            Require<Button>(root, PrimaryAction, missing);
            return missing;
        }

        private static void Require<T>(VisualElement root, string name, ICollection<string> missing)
            where T : VisualElement
        {
            if (root.Q<T>(name) == null) missing.Add(name);
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class InventoryDocumentView : MonoBehaviour, IInventoryView
    {
        private const string SelectedClass = "inventory-slot--selected";

        [SerializeField] private UIDocument document;
        [Tooltip("Optional design-language skin. Leave empty to keep the stylesheet-driven look; assign or call ApplySkin to restyle without forking the document.")]
        [SerializeField] private InventoryUiToolkitSkin skin;

        private readonly List<SlotBinding> _slots = new List<SlotBinding>();
        private VisualElement _root;
        private Label _state;
        private Label _message;
        private VisualElement _grid;
        private Label _details;
        private Button _primaryAction;
        private InventorySlotIntent? _selectedIntent;
        private bool _interactionEnabled = true;

        public event Action<InventorySlotIntent> ActivationRequested;
        public event Action<InventorySlotIntent> SelectionRequested;

        public UIDocument Document => document;

        /// <summary>The injected skin, or null when the stylesheet alone drives the look.</summary>
        public InventoryUiToolkitSkin Skin => skin;

        public InventoryViewModel LastModel { get; private set; }
        public VisualElement BoundRoot => _root;
        public IReadOnlyList<Button> SlotButtons => _slots.Select(slot => slot.Button).ToArray();
        public bool InteractionEnabled => _interactionEnabled;
        public bool IsBound => _root != null;

        private void Reset() => document = GetComponent<UIDocument>();

        private void OnEnable()
        {
            if (document == null) document = GetComponent<UIDocument>();
            if (document != null && document.visualTreeAsset != null && document.rootVisualElement != null)
            {
                Bind(document.rootVisualElement);
            }
        }

        private void OnDisable() => Unbind();

        public void Bind(UIDocument source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            document = source;
            Bind(source.rootVisualElement);
        }

        public void Bind(VisualElement root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            var missing = InventoryDocumentContract.FindMissingParts(root);
            if (missing.Count > 0)
            {
                throw new InvalidOperationException(
                    "Inventory UI Toolkit document is missing required named elements: " +
                    string.Join(", ", missing));
            }

            Unbind();
            _root = root.Q<VisualElement>(InventoryDocumentContract.Root);
            _state = root.Q<Label>(InventoryDocumentContract.State);
            _message = root.Q<Label>(InventoryDocumentContract.Message);
            _grid = root.Q<VisualElement>(InventoryDocumentContract.Grid);
            _details = root.Q<Label>(InventoryDocumentContract.Details);
            _primaryAction = root.Q<Button>(InventoryDocumentContract.PrimaryAction);
            _primaryAction.clicked += OnPrimaryActionClicked;
            ApplyShellSkin();
            if (LastModel != null) Render(LastModel);
            else RefreshInteractionGate();
        }

        /// <summary>
        /// Injects a design-language skin. Token values are written as inline styles on the
        /// bound root, grid, message, primary action, and every current or later slot, so the
        /// shipped USS selectors keep working underneath. Passing null clears every inline
        /// value the seam wrote and returns the document to its stylesheet-driven look.
        /// </summary>
        public void ApplySkin(InventoryUiToolkitSkin value)
        {
            skin = value;
            if (_root == null) return;
            ApplyShellSkin();
            foreach (var slot in _slots)
            {
                ApplySlotSkin(slot);
            }
        }

        public void Render(InventoryViewModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            EnsureBound();
            LastModel = model;
            _selectedIntent = null;

            foreach (InventoryUiState value in Enum.GetValues(typeof(InventoryUiState)))
            {
                _root.EnableInClassList(StateClass(value), value == model.State);
            }

            _state.text = model.State.ToString();
            _message.text = string.IsNullOrWhiteSpace(model.Message)
                ? $"Revision {model.Revision}"
                : model.Message;
            _message.style.display = string.IsNullOrWhiteSpace(_message.text)
                ? DisplayStyle.None
                : DisplayStyle.Flex;

            ClearSlots();
            for (var index = 0; index < model.Slots.Count; index++)
            {
                AddSlot(index, model.Slots[index]);
            }

            var selected = _slots.FirstOrDefault(slot => slot.Model.Selected);
            if (selected != null)
            {
                _selectedIntent = selected.Intent;
                UpdateDetails(selected.Model);
            }
            else
            {
                _details.text = model.Slots.Count == 0 ? "No inventory slots" : "Select an item";
            }

            RefreshInteractionGate();
        }

        public void SetInteractionEnabled(bool enabled)
        {
            _interactionEnabled = enabled;
            RefreshInteractionGate();
        }

        public bool TryActivate(int displayIndex)
        {
            if (!TryGetEnabledBinding(displayIndex, out var binding)) return false;
            ActivationRequested?.Invoke(binding.Intent);
            return true;
        }

        public bool TrySelect(int displayIndex)
        {
            if (!TryGetEnabledBinding(displayIndex, out var binding)) return false;
            Select(binding);
            return true;
        }

        private void AddSlot(int displayIndex, InventorySlotViewModel model)
        {
            var intent = new InventorySlotIntent(model.Address, displayIndex);
            var button = new Button
            {
                name = $"inventory-slot-{displayIndex}",
                text = model.DefinitionId.HasValue
                    ? $"{model.DefinitionId.Value.Value}  x{model.Quantity}"
                    : "Empty",
                focusable = true,
                tooltip = $"{model.Address.ContainerId.Value}:{model.Address.Index}",
            };
            button.AddToClassList("inventory-slot");
            button.EnableInClassList("inventory-slot--occupied", model.DefinitionId.HasValue);
            button.EnableInClassList("inventory-slot--empty", !model.DefinitionId.HasValue);
            button.EnableInClassList(SelectedClass, model.Selected);

            SlotBinding binding = null;
            Action clicked = () =>
            {
                if (_interactionEnabled && binding.Model.Enabled)
                {
                    ActivationRequested?.Invoke(binding.Intent);
                }
            };
            EventCallback<FocusInEvent> focused = _ =>
            {
                if (_interactionEnabled && binding.Model.Enabled) Select(binding);
            };
            EventCallback<PointerEnterEvent> pointerEntered = _ =>
            {
                binding.Hovered = true;
                ApplySlotSkin(binding);
            };
            EventCallback<PointerLeaveEvent> pointerLeft = _ =>
            {
                binding.Hovered = false;
                ApplySlotSkin(binding);
            };
            binding = new SlotBinding(button, model, intent, clicked, focused, pointerEntered, pointerLeft);
            button.clicked += clicked;
            button.RegisterCallback(focused);
            button.RegisterCallback(pointerEntered);
            button.RegisterCallback(pointerLeft);
            _slots.Add(binding);
            _grid.Add(button);
            ApplySlotSkin(binding);
        }

        private void Select(SlotBinding selected)
        {
            _selectedIntent = selected.Intent;
            foreach (var slot in _slots)
            {
                slot.Button.EnableInClassList(SelectedClass, ReferenceEquals(slot, selected));
            }
            UpdateDetails(selected.Model);
            RefreshInteractionGate();
            SelectionRequested?.Invoke(selected.Intent);
        }

        private void UpdateDetails(InventorySlotViewModel model)
        {
            _details.text = model.DefinitionId.HasValue
                ? $"{model.DefinitionId.Value.Value} · Quantity {model.Quantity}"
                : $"Empty slot · {model.Address.ContainerId.Value}:{model.Address.Index}";
        }

        private void OnPrimaryActionClicked()
        {
            if (_interactionEnabled && _selectedIntent.HasValue)
            {
                ActivationRequested?.Invoke(_selectedIntent.Value);
            }
        }

        private void RefreshInteractionGate()
        {
            foreach (var slot in _slots)
            {
                slot.Button.SetEnabled(_interactionEnabled && slot.Model.Enabled);
                slot.Button.EnableInClassList(
                    "inventory-slot--disabled",
                    !_interactionEnabled || !slot.Model.Enabled);
                ApplySlotSkin(slot);
            }

            if (_primaryAction != null)
            {
                var selectedEnabled = _selectedIntent.HasValue &&
                                      _slots.Any(slot => slot.Intent.Address == _selectedIntent.Value.Address &&
                                                         slot.Model.Enabled);
                _primaryAction.SetEnabled(_interactionEnabled && selectedEnabled);
            }

            _root?.EnableInClassList("inventory-root--interaction-disabled", !_interactionEnabled);
        }

        private bool TryGetEnabledBinding(int displayIndex, out SlotBinding binding)
        {
            binding = displayIndex >= 0 && displayIndex < _slots.Count ? _slots[displayIndex] : null;
            return binding != null && _interactionEnabled && binding.Model.Enabled;
        }

        private void ClearSlots()
        {
            foreach (var slot in _slots)
            {
                slot.Button.clicked -= slot.Clicked;
                slot.Button.UnregisterCallback(slot.Focused);
                slot.Button.UnregisterCallback(slot.PointerEntered);
                slot.Button.UnregisterCallback(slot.PointerLeft);
                slot.Button.RemoveFromHierarchy();
            }
            _slots.Clear();
            _grid?.Clear();
        }

        /// <summary>
        /// Writes the shell tokens (surface, section, text, accent) as inline styles, or
        /// clears them when no skin is injected so the stylesheet values apply unchanged.
        /// </summary>
        private void ApplyShellSkin()
        {
            if (_root == null) return;
            if (skin == null)
            {
                _root.style.backgroundColor = StyleKeyword.Null;
                _root.style.color = StyleKeyword.Null;
                _grid.style.backgroundColor = StyleKeyword.Null;
                _message.style.color = StyleKeyword.Null;
                _primaryAction.style.backgroundColor = StyleKeyword.Null;
                _primaryAction.style.color = StyleKeyword.Null;
                return;
            }

            _root.style.backgroundColor = skin.Surface;
            _root.style.color = skin.TextPrimary;
            _grid.style.backgroundColor = skin.Section;
            _message.style.color = skin.TextMuted;
            _primaryAction.style.backgroundColor = skin.Accent;
            _primaryAction.style.color = skin.TextPrimary;
        }

        /// <summary>
        /// Resolves one slot's state (disabled, selected, hover, normal) against the skin and
        /// writes it inline. Inline values outrank the USS :hover and modifier selectors, so the
        /// state model is applied here whenever a skin is present; without a skin every inline
        /// value is cleared and the stylesheet keeps driving the slot.
        /// </summary>
        private void ApplySlotSkin(SlotBinding slot)
        {
            var style = slot.Button.style;
            if (skin == null)
            {
                style.backgroundColor = StyleKeyword.Null;
                style.color = StyleKeyword.Null;
                style.opacity = StyleKeyword.Null;
                return;
            }

            var enabled = _interactionEnabled && slot.Model.Enabled;
            if (!enabled) slot.Hovered = false;

            Color background;
            if (!enabled)
            {
                var disabled = skin.SlotDisabled;
                background = new Color(disabled.r, disabled.g, disabled.b, 1f);
            }
            else if (slot.Button.ClassListContains(SelectedClass))
            {
                background = skin.SlotSelected;
            }
            else if (slot.Hovered)
            {
                background = skin.SlotHover;
            }
            else
            {
                background = skin.SlotNormal;
            }

            style.backgroundColor = background;
            style.color = slot.Model.DefinitionId.HasValue ? skin.TextPrimary : skin.TextMuted;
            if (enabled) style.opacity = StyleKeyword.Null;
            else style.opacity = skin.SlotDisabled.a;
        }

        private void Unbind()
        {
            ClearSlots();
            if (_primaryAction != null) _primaryAction.clicked -= OnPrimaryActionClicked;
            _root = null;
            _state = null;
            _message = null;
            _grid = null;
            _details = null;
            _primaryAction = null;
            _selectedIntent = null;
        }

        private void EnsureBound()
        {
            if (_root != null) return;
            if (document == null) document = GetComponent<UIDocument>();
            if (document == null || document.rootVisualElement == null)
            {
                throw new InvalidOperationException("InventoryDocumentView requires an active UIDocument or an explicit VisualElement binding.");
            }
            Bind(document.rootVisualElement);
        }

        private static string StateClass(InventoryUiState state) =>
            "inventory-state--" + state.ToString().ToLowerInvariant();

        private sealed class SlotBinding
        {
            public SlotBinding(
                Button button,
                InventorySlotViewModel model,
                InventorySlotIntent intent,
                Action clicked,
                EventCallback<FocusInEvent> focused,
                EventCallback<PointerEnterEvent> pointerEntered,
                EventCallback<PointerLeaveEvent> pointerLeft)
            {
                Button = button;
                Model = model;
                Intent = intent;
                Clicked = clicked;
                Focused = focused;
                PointerEntered = pointerEntered;
                PointerLeft = pointerLeft;
            }

            public Button Button { get; }
            public InventorySlotViewModel Model { get; }
            public InventorySlotIntent Intent { get; }
            public Action Clicked { get; }
            public EventCallback<FocusInEvent> Focused { get; }
            public EventCallback<PointerEnterEvent> PointerEntered { get; }
            public EventCallback<PointerLeaveEvent> PointerLeft { get; }
            public bool Hovered { get; set; }
        }
    }
}
