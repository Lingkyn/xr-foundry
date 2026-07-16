using UnityEngine;

namespace Lingkyn.Unity.XrBaseline.Config
{
    /// <summary>
    /// Per-project tuning for generic VR greybox baseline (Scaff).
    /// After editing, run Tools/Lingkyn/XR Baseline/Apply Config, then rebuild for headset.
    /// </summary>
    [CreateAssetMenu(fileName = "VrBaselineConfig", menuName = "Lingkyn/XR Baseline Config")]
    public sealed class VrBaselineConfig : ScriptableObject
    {
        public const string DefaultAssetPath = "Assets/_Project/Data/Config/VrBaselineConfig.asset";

        [Header("Greybox Materials")]
        public Color floorBaseColor = new(0.72f, 0.72f, 0.74f, 1f);
        public Color interactableBaseColor = new(0.15f, 0.38f, 0.78f, 1f);
        public float interactableSmoothness = 0.35f;

        [Header("Hover Visual (GrabbableHoverVisual)")]
        public Color grabbableIdleBaseColor = new(0.15f, 0.38f, 0.78f, 1f);
        public Color grabbableHoverBaseColor = new(0.45f, 0.72f, 1f, 1f);
        [ColorUsage(true, true)]
        public Color grabbableHoverEmission = new(3.5f, 1.2f, 0.15f, 1f);

        [Header("Sandbox Lighting")]
        public float directionalLightIntensity = 0.9f;
        public Color directionalLightColor = new(1f, 0.96f, 0.9f, 1f);
        public float ambientIntensity = 0.88f;
        public Color ambientSkyColor = new(0.42f, 0.45f, 0.5f, 1f);
        public Color ambientEquatorColor = new(0.36f, 0.38f, 0.42f, 1f);
        public Color ambientGroundColor = new(0.28f, 0.28f, 0.3f, 1f);

        [Header("Interaction Rays")]
        public float farRayCastDistance = 30f;
        public float restingVisualLineLength = 0.75f;
        public Color rayIdleColor = new(0f, 0.78f, 1f, 0.9f);
        public Color rayHoverColor = new(1f, 0.55f, 0.08f, 1f);
        public float rayLineWidth = 0.008f;

        [Header("Locomotion")]
        [Tooltip("When true, XR Baseline initialization keeps stick locomotion enabled.")]
        public bool enableContinuousMove;
    }
}
