using Lingkyn.XrUiShell.Core;
using UnityEngine;

namespace Lingkyn.XrUiShell.Ugui
{
    // The UGUI adapter's one injectable skin seam: a ScriptableObject carrying a concrete value
    // for every shared design-language token this scaffold uses, with a default asset that
    // carries the canonical values from docs/standards/design-language/ui-design-language-standard.json
    // so an un-themed install already matches the shared look. No colour, radius, or hit-size
    // value lives anywhere else in this package: a bound Canvas subtree's own view reads every
    // visual value through this seam, never from a serialized per-view field or a factory
    // constant.

    /// <summary>The closed pair of text-weight values the design language names.</summary>
    public enum ShellFontWeight
    {
        Medium,
        Bold,
    }

    [CreateAssetMenu(menuName = "Lingkyn/XR UI Shell/UGUI Skin", fileName = "XrUiShellUguiSkin")]
    public sealed class UguiShellSkin : ScriptableObject
    {
        [SerializeField] private Color surfacePanel = new Color(0.031f, 0.094f, 0.122f, 0.96f);
        [SerializeField] private Color surfaceSection = new Color(0.055f, 0.129f, 0.153f, 0.96f);
        [SerializeField] private Color surfaceAccent = new Color(0.122f, 0.592f, 0.624f, 1f);
        [SerializeField] private Color textPrimary = new Color(0.886f, 0.988f, 1f, 1f);
        [SerializeField] private Color textMuted = new Color(0.439f, 0.616f, 0.639f, 1f);
        [SerializeField] private ShellFontWeight textWeightBody = ShellFontWeight.Medium;
        [SerializeField] private ShellFontWeight textWeightTitle = ShellFontWeight.Bold;
        [SerializeField] private Color slotStateNormal = new Color(0.071f, 0.212f, 0.255f, 1f);
        [SerializeField] private Color slotStateHover = new Color(0.090f, 0.345f, 0.388f, 1f);
        [SerializeField] private Color slotStateSelected = new Color(0.094f, 0.494f, 0.525f, 1f);
        [SerializeField] private Color slotStateDisabled = new Color(0.071f, 0.212f, 0.255f, 0.46f);
        [SerializeField] private float shapeCornerRadiusSmall = 8f;
        [SerializeField] private float shapeCornerRadiusMedium = 16f;
        [SerializeField] private float shapeCornerRadiusLarge = 24f;
        [SerializeField] private float hitTargetMinimum = 48f;
        [SerializeField] private float hitTargetPrimary = 60f;

        public Color SurfacePanel { get => surfacePanel; set => surfacePanel = value; }
        public Color SurfaceSection { get => surfaceSection; set => surfaceSection = value; }
        public Color SurfaceAccent { get => surfaceAccent; set => surfaceAccent = value; }
        public Color TextPrimary { get => textPrimary; set => textPrimary = value; }
        public Color TextMuted { get => textMuted; set => textMuted = value; }
        public ShellFontWeight TextWeightBody { get => textWeightBody; set => textWeightBody = value; }
        public ShellFontWeight TextWeightTitle { get => textWeightTitle; set => textWeightTitle = value; }
        public Color SlotStateNormal { get => slotStateNormal; set => slotStateNormal = value; }
        public Color SlotStateHover { get => slotStateHover; set => slotStateHover = value; }
        public Color SlotStateSelected { get => slotStateSelected; set => slotStateSelected = value; }
        public Color SlotStateDisabled { get => slotStateDisabled; set => slotStateDisabled = value; }
        public float ShapeCornerRadiusSmall { get => shapeCornerRadiusSmall; set => shapeCornerRadiusSmall = value; }
        public float ShapeCornerRadiusMedium { get => shapeCornerRadiusMedium; set => shapeCornerRadiusMedium = value; }
        public float ShapeCornerRadiusLarge { get => shapeCornerRadiusLarge; set => shapeCornerRadiusLarge = value; }
        public float HitTargetMinimum { get => hitTargetMinimum; set => hitTargetMinimum = value; }
        public float HitTargetPrimary { get => hitTargetPrimary; set => hitTargetPrimary = value; }

        /// <summary>The colour for a colour-valued token. Returns the default colour for a token
        /// this skin holds no colour for (a text-weight or size token); a caller only asks a
        /// colour-valued token because the slot it is resolving for is itself colour-valued.</summary>
        public Color GetColor(DesignToken token)
        {
            switch (token)
            {
                case DesignToken.SurfacePanel: return SurfacePanel;
                case DesignToken.SurfaceSection: return SurfaceSection;
                case DesignToken.SurfaceAccent: return SurfaceAccent;
                case DesignToken.TextPrimary: return TextPrimary;
                case DesignToken.TextMuted: return TextMuted;
                case DesignToken.SlotStateNormal: return SlotStateNormal;
                case DesignToken.SlotStateHover: return SlotStateHover;
                case DesignToken.SlotStateSelected: return SlotStateSelected;
                case DesignToken.SlotStateDisabled: return SlotStateDisabled;
                default: return default;
            }
        }

        /// <summary>The size (a corner radius or a hit-target dimension) for a size-valued token.</summary>
        public float GetSize(DesignToken token)
        {
            switch (token)
            {
                case DesignToken.ShapeCornerRadiusSmall: return ShapeCornerRadiusSmall;
                case DesignToken.ShapeCornerRadiusMedium: return ShapeCornerRadiusMedium;
                case DesignToken.ShapeCornerRadiusLarge: return ShapeCornerRadiusLarge;
                case DesignToken.HitTargetMinimum: return HitTargetMinimum;
                case DesignToken.HitTargetPrimary: return HitTargetPrimary;
                default: return default;
            }
        }

        public ShellFontWeight GetWeight(DesignToken token)
        {
            switch (token)
            {
                case DesignToken.TextWeightTitle: return TextWeightTitle;
                default: return TextWeightBody;
            }
        }

        /// <summary>The colour this skin resolves for one closed shell slot, through the given
        /// token-to-slot mapping (ordinarily <see cref="CanonicalSkinMapping"/>).</summary>
        public Color ResolveSlotColor(ShellSlot slot, SkinMapping mapping) => GetColor(mapping.Resolve(slot));

        /// <summary>The size this skin resolves for one closed shell slot.</summary>
        public float ResolveSlotSize(ShellSlot slot, SkinMapping mapping) => GetSize(mapping.Resolve(slot));
    }
}
