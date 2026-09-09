using UnityEngine;
using UnityEngine.UI;

namespace Lingkyn.Inventory.UGUI
{
    /// <summary>
    /// Reusable visual skin for the UGUI Inventory adapter.
    ///
    /// The renderer-neutral presentation contract (<c>com.lingkyn.inventory.presentation</c>)
    /// carries no visual vocabulary, so all look-and-feel lives in the renderer adapter.
    /// This ScriptableObject is that seam for UGUI: a single injectable source of palette,
    /// optional rounded 9-slice sprites, and font, keyed off slot interaction state. A
    /// consumer can author its own <see cref="InventorySkin"/> asset and inject it to fully
    /// restyle the adapter without forking prefabs or code. When no skin is assigned the
    /// views fall back to <see cref="CreateDefault"/>.
    ///
    /// Default values are a translucent "spatial glass" palette aligned with the UI Toolkit
    /// sibling (<c>InventoryDocument.uss</c>) and informed by public spatial-UI design
    /// language (visionOS windows/materials, PICO OS spatial UI, Meta Horizon MR). This
    /// package hardcodes no consumer product, scene, or brand.
    /// </summary>
    [CreateAssetMenu(menuName = "XR Foundry/Inventory/Inventory Skin", fileName = "InventorySkin")]
    public sealed class InventorySkin : ScriptableObject
    {
        [Header("Surfaces")]
        [Tooltip("Panel (outermost) background. Alpha < 1 keeps the spatial scene visible behind the glass.")]
        public Color panelColor = new Color(0.031f, 0.094f, 0.122f, 0.96f);
        [Tooltip("Section background (grid, details, action menu).")]
        public Color sectionColor = new Color(0.055f, 0.129f, 0.153f, 0.96f);
        [Tooltip("Accent used for the primary action / emphasis.")]
        public Color accentColor = new Color(0.122f, 0.592f, 0.624f, 1f);

        [Header("Text")]
        public Color textColor = new Color(0.886f, 0.988f, 1f, 1f);
        [Tooltip("Secondary / de-emphasized text (empty slots, hints).")]
        public Color mutedTextColor = new Color(0.439f, 0.616f, 0.639f, 1f);
        [Tooltip("Optional font override. Null keeps whatever the prefab authored.")]
        public Font font;

        [Header("Slot states")]
        public Color slotNormalColor = new Color(0.071f, 0.212f, 0.255f, 1f);
        public Color slotHoverColor = new Color(0.090f, 0.345f, 0.388f, 1f);
        public Color slotSelectedColor = new Color(0.094f, 0.494f, 0.525f, 1f);
        [Tooltip("Disabled slots reuse the normal hue at reduced opacity so the grid reads as dimmed, not recolored.")]
        public Color slotDisabledColor = new Color(0.071f, 0.212f, 0.255f, 0.46f);

        [Header("Optional rounded sprites (9-slice). Null = flat fill.")]
        public Sprite panelSprite;
        public Sprite sectionSprite;
        public Sprite slotSprite;

        /// <summary>Runtime fallback skin used when no asset is injected. Callers own the lifetime.</summary>
        public static InventorySkin CreateDefault() => CreateInstance<InventorySkin>();

        internal static void StyleBackground(Image image, Color color, Sprite sprite)
        {
            if (image == null) return;
            image.color = color;
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
            }
        }

        internal static void StyleText(Text label, Color color, Font font)
        {
            if (label == null) return;
            label.color = color;
            if (font != null) label.font = font;
        }
    }
}
