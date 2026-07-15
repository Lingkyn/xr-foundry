using UnityEditor;
using UnityEngine;
using Lingkyn.Unity.XrBaseline.Interaction;
using Lingkyn.Unity.XrBaseline.Config;

namespace Lingkyn.Unity.XrBaseline.Editor.SceneSetup
{
    /// <summary>
    /// Adds reliable hover glow to grabbable greybox cubes and disables legacy nested XRI affordance prefab.
    /// </summary>
    public static class VrBaselineHoverVisualSetup
    {
        const string AffordanceChildName = "Highlight Interaction Affordance";

        public static bool IsConfigured(GameObject root) =>
            root != null && root.GetComponent<GrabbableHoverVisual>() != null;

        public static bool NeedsConfigRefresh(GameObject root, VrBaselineConfig config)
        {
            if (root == null || config == null) return false;

            var visual = root.GetComponent<GrabbableHoverVisual>();
            if (visual == null) return true;

            var visualSo = new SerializedObject(visual);
            if (!ColorsMatch(visualSo.FindProperty("_idleBaseColor").colorValue, config.grabbableIdleBaseColor)) return true;
            if (!ColorsMatch(visualSo.FindProperty("_hoverBaseColor").colorValue, config.grabbableHoverBaseColor)) return true;
            return !ColorsMatch(visualSo.FindProperty("_hoverEmission").colorValue, config.grabbableHoverEmission);
        }

        public static void Configure(GameObject root, VrBaselineConfig config = null)
        {
            if (root == null) return;

            DisableLegacyAffordance(root);
            EnsureEmissionReadyMaterial(root);

            var visual = root.GetComponent<GrabbableHoverVisual>();
            if (visual == null) visual = root.AddComponent<GrabbableHoverVisual>();

            var visualSo = new SerializedObject(visual);
            var renderer = root.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                visualSo.FindProperty("_renderer").objectReferenceValue = renderer;
            }

            if (config != null)
            {
                visualSo.FindProperty("_idleBaseColor").colorValue = config.grabbableIdleBaseColor;
                visualSo.FindProperty("_hoverBaseColor").colorValue = config.grabbableHoverBaseColor;
                visualSo.FindProperty("_hoverEmission").colorValue = config.grabbableHoverEmission;
            }

            visualSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(visual);
            if (PrefabUtility.IsPartOfPrefabInstance(root))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
            }
        }

        static void DisableLegacyAffordance(GameObject root)
        {
            foreach (Transform child in root.transform)
            {
                if (child.name != AffordanceChildName) continue;
                child.gameObject.SetActive(false);
            }
        }

        static void EnsureEmissionReadyMaterial(GameObject root)
        {
            var renderer = root.GetComponent<MeshRenderer>();
            if (renderer == null || renderer.sharedMaterial == null) return;

            var material = renderer.sharedMaterial;
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(material);
        }

        static bool ColorsMatch(Color left, Color right) =>
            Mathf.Approximately(left.r, right.r) &&
            Mathf.Approximately(left.g, right.g) &&
            Mathf.Approximately(left.b, right.b) &&
            Mathf.Approximately(left.a, right.a);
    }
}
