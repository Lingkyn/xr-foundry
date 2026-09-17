using UnityEngine;

namespace Lingkyn.Inventory.UIToolkit
{
    /// <summary>
    /// Injectable skin seam for the Inventory UI Toolkit adapter. It carries the shared
    /// design-language tokens (docs/standards/design-language/ui-design-language-standard.json)
    /// that the adapter maps onto its visual tree. A new instance, whether created through
    /// <see cref="CreateDefault"/> or the asset menu, holds the canonical token values, so
    /// an un-themed install already matches the shared library look. The seam carries no
    /// domain, presentation, scene, or device knowledge.
    /// </summary>
    [CreateAssetMenu(
        fileName = "InventoryUiToolkitSkin",
        menuName = "XR Foundry/Inventory/UI Toolkit Skin")]
    public sealed class InventoryUiToolkitSkin : ScriptableObject
    {
        // Canonical design-language values (sRGB, 0..1). surface.panel / section / accent.
        public static readonly Color CanonicalSurface = new Color(0.031f, 0.094f, 0.122f, 0.96f);
        public static readonly Color CanonicalSection = new Color(0.055f, 0.129f, 0.153f, 0.96f);
        public static readonly Color CanonicalAccent = new Color(0.122f, 0.592f, 0.624f, 1.0f);

        // text.primary / text.muted.
        public static readonly Color CanonicalTextPrimary = new Color(0.886f, 0.988f, 1.0f, 1.0f);
        public static readonly Color CanonicalTextMuted = new Color(0.439f, 0.616f, 0.639f, 1.0f);

        // slot_states.normal / hover / selected / disabled.
        public static readonly Color CanonicalSlotNormal = new Color(0.071f, 0.212f, 0.255f, 1.0f);
        public static readonly Color CanonicalSlotHover = new Color(0.090f, 0.345f, 0.388f, 1.0f);
        public static readonly Color CanonicalSlotSelected = new Color(0.094f, 0.494f, 0.525f, 1.0f);
        public static readonly Color CanonicalSlotDisabled = new Color(0.071f, 0.212f, 0.255f, 0.46f);

        [Header("Surfaces")]
        [Tooltip("surface.panel: outermost panel background (inventory-root).")]
        [SerializeField] private Color surface = CanonicalSurface;
        [Tooltip("surface.section: grouped region background (inventory-grid).")]
        [SerializeField] private Color section = CanonicalSection;
        [Tooltip("surface.accent: primary action background (inventory-primary-action).")]
        [SerializeField] private Color accent = CanonicalAccent;

        [Header("Text")]
        [Tooltip("text.primary: panel text, occupied slot labels, primary action label.")]
        [SerializeField] private Color textPrimary = CanonicalTextPrimary;
        [Tooltip("text.muted: message hint and empty slot labels.")]
        [SerializeField] private Color textMuted = CanonicalTextMuted;

        [Header("Slot states")]
        [Tooltip("slot_states.normal: slot background at rest.")]
        [SerializeField] private Color slotNormal = CanonicalSlotNormal;
        [Tooltip("slot_states.hover: slot background while the pointer is over an enabled slot.")]
        [SerializeField] private Color slotHover = CanonicalSlotHover;
        [Tooltip("slot_states.selected: slot background for the selected slot.")]
        [SerializeField] private Color slotSelected = CanonicalSlotSelected;
        [Tooltip("slot_states.disabled: RGB is the disabled slot background; alpha dims the whole slot so it reads as dimmed, not recolored.")]
        [SerializeField] private Color slotDisabled = CanonicalSlotDisabled;

        public Color Surface
        {
            get => surface;
            set => surface = value;
        }

        public Color Section
        {
            get => section;
            set => section = value;
        }

        public Color Accent
        {
            get => accent;
            set => accent = value;
        }

        public Color TextPrimary
        {
            get => textPrimary;
            set => textPrimary = value;
        }

        public Color TextMuted
        {
            get => textMuted;
            set => textMuted = value;
        }

        public Color SlotNormal
        {
            get => slotNormal;
            set => slotNormal = value;
        }

        public Color SlotHover
        {
            get => slotHover;
            set => slotHover = value;
        }

        public Color SlotSelected
        {
            get => slotSelected;
            set => slotSelected = value;
        }

        public Color SlotDisabled
        {
            get => slotDisabled;
            set => slotDisabled = value;
        }

        /// <summary>
        /// Creates an in-memory skin populated with the canonical design-language values.
        /// The caller owns the instance and destroys it, or saves it as an asset.
        /// </summary>
        public static InventoryUiToolkitSkin CreateDefault()
        {
            var skin = CreateInstance<InventoryUiToolkitSkin>();
            skin.name = "InventoryUiToolkitSkin (Default)";
            return skin;
        }
    }
}
