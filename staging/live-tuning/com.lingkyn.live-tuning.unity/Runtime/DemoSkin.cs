using System.Collections.Generic;
using Lingkyn.LiveTuning.Core;
using UnityEngine;

namespace Lingkyn.LiveTuning.Unity
{
    // A self-contained reference skin asset and binder, used by this package's own tests and by
    // a consumer as a template. It names no Inventory type: the family binds to whichever
    // renderer adapter's real skin asset a consumer supplies, through a binder written the same
    // shape as this one. Mirrors the closed member set the verification contract names for the
    // Inventory skin seams (a surface colour, a text colour, and slot-state colours), plus one
    // float member, so every kind this family supports has at least one demonstrated member.

    /// <summary>A minimal reference skin asset: one colour "surface" member, one colour "text"
    /// member, one float "corner_radius" member. A real renderer adapter's own skin asset (for
    /// example an Inventory skin) is bound the same way, through its own binder.</summary>
    [CreateAssetMenu(menuName = "Lingkyn/Live Tuning/Demo Skin", fileName = "LiveTuningDemoSkin")]
    public sealed class LiveTuningDemoSkinAsset : ScriptableObject
    {
        [SerializeField] private Color surface = new Color(0.031f, 0.094f, 0.122f, 0.96f);
        [SerializeField] private Color text = new Color(0.886f, 0.988f, 1f, 1f);
        [SerializeField] private float cornerRadius = 8f;

        public Color Surface { get => surface; set => surface = value; }
        public Color Text { get => text; set => text = value; }
        public float CornerRadius { get => cornerRadius; set => cornerRadius = value; }
    }

    /// <summary>The hand-written binder for <see cref="LiveTuningDemoSkinAsset"/>. Every member is
    /// resolved and applied through this explicit switch; nothing here uses reflection over
    /// fields or properties.</summary>
    public sealed class LiveTuningDemoSkinBinder : ISkinBinder
    {
        public const string SurfaceMember = "surface";
        public const string TextMember = "text";
        public const string CornerRadiusMember = "corner_radius";

        private static readonly string[] Members = { SurfaceMember, TextMember, CornerRadiusMember };

        public string AssetTypeName => nameof(LiveTuningDemoSkinAsset);
        public IReadOnlyCollection<string> MemberNames => Members;

        public bool CanBind(UnityEngine.Object asset) => asset is LiveTuningDemoSkinAsset;

        public bool TryGetMemberKind(string memberName, out TunableKind kind)
        {
            switch (memberName)
            {
                case SurfaceMember:
                case TextMember:
                    kind = TunableKind.Colour;
                    return true;
                case CornerRadiusMember:
                    kind = TunableKind.Float;
                    return true;
                default:
                    kind = default;
                    return false;
            }
        }

        public void Apply(UnityEngine.Object asset, string memberName, TunableValue value)
        {
            var skin = (LiveTuningDemoSkinAsset)asset;
            switch (memberName)
            {
                case SurfaceMember:
                {
                    var (r, g, b, a) = value.AsColour;
                    skin.Surface = new Color(r, g, b, a);
                    return;
                }
                case TextMember:
                {
                    var (r, g, b, a) = value.AsColour;
                    skin.Text = new Color(r, g, b, a);
                    return;
                }
                case CornerRadiusMember:
                    skin.CornerRadius = value.AsFloat;
                    return;
                default:
                    throw new System.ArgumentException($"'{AssetTypeName}' has no member '{memberName}'.", nameof(memberName));
            }
        }
    }

    /// <summary>A second, unrelated reference skin asset type with a single enumerated "density"
    /// member, used only to prove the panel host's attach path is identical for a binding from
    /// this type and one from <see cref="LiveTuningDemoSkinAsset"/>: the host never branches on
    /// which asset or binder a slot's binding names.</summary>
    [CreateAssetMenu(menuName = "Lingkyn/Live Tuning/Secondary Demo Skin", fileName = "LiveTuningSecondaryDemoSkin")]
    public sealed class LiveTuningSecondaryDemoSkinAsset : ScriptableObject
    {
        [SerializeField] private string density = "comfortable";

        public string Density { get => density; set => density = value ?? string.Empty; }
    }

    public sealed class LiveTuningSecondaryDemoSkinBinder : ISkinBinder
    {
        public const string DensityMember = "density";

        private static readonly string[] Members = { DensityMember };

        public string AssetTypeName => nameof(LiveTuningSecondaryDemoSkinAsset);
        public IReadOnlyCollection<string> MemberNames => Members;

        public bool CanBind(UnityEngine.Object asset) => asset is LiveTuningSecondaryDemoSkinAsset;

        public bool TryGetMemberKind(string memberName, out TunableKind kind)
        {
            if (memberName == DensityMember) { kind = TunableKind.Enumerated; return true; }
            kind = default;
            return false;
        }

        public void Apply(UnityEngine.Object asset, string memberName, TunableValue value)
        {
            if (memberName != DensityMember) throw new System.ArgumentException($"'{AssetTypeName}' has no member '{memberName}'.", nameof(memberName));
            ((LiveTuningSecondaryDemoSkinAsset)asset).Density = value.AsEnumerated;
        }
    }
}
