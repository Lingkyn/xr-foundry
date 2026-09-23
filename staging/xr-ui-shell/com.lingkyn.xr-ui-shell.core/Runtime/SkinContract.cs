using System;
using System.Collections.Generic;
using System.Linq;

namespace Lingkyn.XrUiShell.Core
{
    // The skin contract: the closed set of shared design-language tokens the Core carries by
    // name (docs/standards/design-language/ui-design-language-standard.json), the closed set of
    // named shell slots, and the mapping between them. The Core holds only token and slot names,
    // never a colour, radius, or hit-size value; every adapter's own skin seam supplies the
    // values for the tokens this mapping names.

    /// <summary>The closed set of design-language tokens the Core gate names. Adding a member is
    /// a breaking change.</summary>
    public enum DesignToken
    {
        SurfacePanel,
        SurfaceSection,
        SurfaceAccent,
        TextPrimary,
        TextMuted,
        TextWeightBody,
        TextWeightTitle,
        SlotStateNormal,
        SlotStateHover,
        SlotStateSelected,
        SlotStateDisabled,
        ShapeCornerRadiusSmall,
        ShapeCornerRadiusMedium,
        ShapeCornerRadiusLarge,
        HitTargetMinimum,
        HitTargetPrimary,
    }

    /// <summary>The closed set of named shell slots the Core gate names. Adding a member is a
    /// breaking change.</summary>
    public enum ShellSlot
    {
        PanelBackground,
        PanelSection,
        PanelTitleText,
        PanelBodyText,
        PanelMutedText,
        PanelCorner,
        MenuWristBackground,
        MenuHandBackground,
        ControlNormal,
        ControlHover,
        ControlSelected,
        ControlDisabled,
        ControlAccent,
        ControlCorner,
        ControlHitMinimum,
        ControlHitPrimary,
    }

    /// <summary>Round-trips a <see cref="DesignToken"/> to and from the exact dotted name the
    /// verification contract and the design-language standard use, so a mapping built from raw
    /// text (the shape a consumer or an adapter would actually pass) can be validated.</summary>
    public static class DesignTokenNames
    {
        private static readonly (DesignToken Token, string Name)[] Entries =
        {
            (DesignToken.SurfacePanel, "surface.panel"),
            (DesignToken.SurfaceSection, "surface.section"),
            (DesignToken.SurfaceAccent, "surface.accent"),
            (DesignToken.TextPrimary, "text.primary"),
            (DesignToken.TextMuted, "text.muted"),
            (DesignToken.TextWeightBody, "text.weight.body"),
            (DesignToken.TextWeightTitle, "text.weight.title"),
            (DesignToken.SlotStateNormal, "slot_states.normal"),
            (DesignToken.SlotStateHover, "slot_states.hover"),
            (DesignToken.SlotStateSelected, "slot_states.selected"),
            (DesignToken.SlotStateDisabled, "slot_states.disabled"),
            (DesignToken.ShapeCornerRadiusSmall, "shape.corner_radius.small"),
            (DesignToken.ShapeCornerRadiusMedium, "shape.corner_radius.medium"),
            (DesignToken.ShapeCornerRadiusLarge, "shape.corner_radius.large"),
            (DesignToken.HitTargetMinimum, "hit_target.minimum"),
            (DesignToken.HitTargetPrimary, "hit_target.primary"),
        };

        /// <summary>Every admitted token name, in declaration order.</summary>
        public static IReadOnlyList<string> AllNames { get; } = Entries.Select(e => e.Name).ToList();

        public static string ToName(DesignToken token) => Entries.First(e => e.Token == token).Name;

        public static bool TryParse(string name, out DesignToken token)
        {
            foreach (var entry in Entries)
            {
                if (string.Equals(entry.Name, name, StringComparison.Ordinal))
                {
                    token = entry.Token;
                    return true;
                }
            }
            token = default;
            return false;
        }
    }

    /// <summary>Round-trips a <see cref="ShellSlot"/> to and from the exact dotted name the
    /// verification contract uses.</summary>
    public static class ShellSlotNames
    {
        private static readonly (ShellSlot Slot, string Name)[] Entries =
        {
            (ShellSlot.PanelBackground, "panel.background"),
            (ShellSlot.PanelSection, "panel.section"),
            (ShellSlot.PanelTitleText, "panel.title.text"),
            (ShellSlot.PanelBodyText, "panel.body.text"),
            (ShellSlot.PanelMutedText, "panel.muted.text"),
            (ShellSlot.PanelCorner, "panel.corner"),
            (ShellSlot.MenuWristBackground, "menu.wrist.background"),
            (ShellSlot.MenuHandBackground, "menu.hand.background"),
            (ShellSlot.ControlNormal, "control.normal"),
            (ShellSlot.ControlHover, "control.hover"),
            (ShellSlot.ControlSelected, "control.selected"),
            (ShellSlot.ControlDisabled, "control.disabled"),
            (ShellSlot.ControlAccent, "control.accent"),
            (ShellSlot.ControlCorner, "control.corner"),
            (ShellSlot.ControlHitMinimum, "control.hit.minimum"),
            (ShellSlot.ControlHitPrimary, "control.hit.primary"),
        };

        /// <summary>Every admitted slot name, in declaration order. The skin contract requires a
        /// canonical mapping to resolve every one of these.</summary>
        public static IReadOnlyList<string> AllNames { get; } = Entries.Select(e => e.Name).ToList();
        public static IReadOnlyList<ShellSlot> AllSlots { get; } = Entries.Select(e => e.Slot).ToList();

        public static string ToName(ShellSlot slot) => Entries.First(e => e.Slot == slot).Name;

        public static bool TryParse(string name, out ShellSlot slot)
        {
            foreach (var entry in Entries)
            {
                if (string.Equals(entry.Name, name, StringComparison.Ordinal))
                {
                    slot = entry.Slot;
                    return true;
                }
            }
            slot = default;
            return false;
        }
    }

    /// <summary>The immutable, validated mapping from every closed shell slot to one shared
    /// design-language token. A built mapping always resolves every slot; <see cref="Build"/> is
    /// the only way to obtain one.</summary>
    public sealed class SkinMapping
    {
        private readonly Dictionary<ShellSlot, DesignToken> _map;

        private SkinMapping(Dictionary<ShellSlot, DesignToken> map)
        {
            _map = map;
        }

        public DesignToken Resolve(ShellSlot slot) => _map[slot];

        public IEnumerable<KeyValuePair<ShellSlot, DesignToken>> Entries => _map;

        internal static SkinMapping FromValidatedMap(Dictionary<ShellSlot, DesignToken> map) => new SkinMapping(map);
    }

    /// <summary>Collects slot-to-token entries by name (the shape a consumer or an adapter would
    /// actually pass) and validates each immediately.</summary>
    public sealed class SkinMappingBuilder
    {
        private readonly Dictionary<ShellSlot, DesignToken> _map = new Dictionary<ShellSlot, DesignToken>();

        public int Count => _map.Count;

        /// <summary>Maps one slot to one token, both by their exact contract name. Rejects a
        /// token name outside the closed design-language set with token.unknown, and a slot name
        /// outside the closed shell-slot set with slot.unknown, leaving the builder unchanged
        /// either way.</summary>
        public ShellResult<SkinMappingBuilder> Map(string slotName, string tokenName)
        {
            if (!DesignTokenNames.TryParse(tokenName, out var token))
            {
                return ShellResult<SkinMappingBuilder>.Fail(ShellFailure.TokenUnknown, $"'{tokenName}' is not one of the shared design-language tokens.", tokenName ?? string.Empty);
            }
            if (!ShellSlotNames.TryParse(slotName, out var slot))
            {
                return ShellResult<SkinMappingBuilder>.Fail(ShellFailure.SlotUnknown, $"'{slotName}' is not one of the closed shell slots.", slotName ?? string.Empty);
            }
            _map[slot] = token;
            return ShellResult<SkinMappingBuilder>.Ok(this);
        }

        /// <summary>The same mapping, by the typed enum values directly.</summary>
        public ShellResult<SkinMappingBuilder> Map(ShellSlot slot, DesignToken token) => Map(ShellSlotNames.ToName(slot), DesignTokenNames.ToName(token));

        /// <summary>Produces the immutable mapping. Rejects an incomplete mapping (any of the
        /// sixteen closed slots left unmapped) with slot.unmapped, naming the first unmapped slot
        /// in canonical order.</summary>
        public ShellResult<SkinMapping> Build()
        {
            foreach (var slot in ShellSlotNames.AllSlots)
            {
                if (!_map.ContainsKey(slot))
                {
                    var name = ShellSlotNames.ToName(slot);
                    return ShellResult<SkinMapping>.Fail(ShellFailure.SlotUnmapped, $"Slot '{name}' has no token mapping.", name);
                }
            }
            return ShellResult<SkinMapping>.Ok(SkinMapping.FromValidatedMap(new Dictionary<ShellSlot, DesignToken>(_map)));
        }
    }

    /// <summary>The canonical mapping the family ships: every shell slot mapped to the token the
    /// shared design-language standard names for that role. Resolves every slot.</summary>
    public static class CanonicalSkinMapping
    {
        public static SkinMapping Build()
        {
            var builder = new SkinMappingBuilder()
                .Map(ShellSlot.PanelBackground, DesignToken.SurfacePanel).Value
                .Map(ShellSlot.PanelSection, DesignToken.SurfaceSection).Value
                .Map(ShellSlot.PanelTitleText, DesignToken.TextPrimary).Value
                .Map(ShellSlot.PanelBodyText, DesignToken.TextPrimary).Value
                .Map(ShellSlot.PanelMutedText, DesignToken.TextMuted).Value
                .Map(ShellSlot.PanelCorner, DesignToken.ShapeCornerRadiusMedium).Value
                .Map(ShellSlot.MenuWristBackground, DesignToken.SurfaceSection).Value
                .Map(ShellSlot.MenuHandBackground, DesignToken.SurfaceSection).Value
                .Map(ShellSlot.ControlNormal, DesignToken.SlotStateNormal).Value
                .Map(ShellSlot.ControlHover, DesignToken.SlotStateHover).Value
                .Map(ShellSlot.ControlSelected, DesignToken.SlotStateSelected).Value
                .Map(ShellSlot.ControlDisabled, DesignToken.SlotStateDisabled).Value
                .Map(ShellSlot.ControlAccent, DesignToken.SurfaceAccent).Value
                .Map(ShellSlot.ControlCorner, DesignToken.ShapeCornerRadiusSmall).Value
                .Map(ShellSlot.ControlHitMinimum, DesignToken.HitTargetMinimum).Value
                .Map(ShellSlot.ControlHitPrimary, DesignToken.HitTargetPrimary).Value;
            var result = builder.Build();
            if (!result.Succeeded) throw new InvalidOperationException("The canonical skin mapping must resolve every slot: " + result.Message);
            return result.Value;
        }
    }
}
