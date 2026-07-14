using System;
using UnityEditor;
using UnityEngine;
using Lingkyn.Unity.XrBaseline.Constants;

namespace Lingkyn.Unity.XrBaseline.Editor.SceneSetup
{
    /// <summary>
    /// Wires XRI Starter Assets highlight affordance onto grabbable greybox props.
    /// </summary>
    public static class VrBaselineAffordanceSetup
    {
        const string StateProviderTypeName =
            "UnityEngine.XR.Interaction.Toolkit.AffordanceSystem.State.XRInteractableAffordanceStateProvider, Unity.XR.Interaction.Toolkit";

        const string RendererHelperTypeName =
            "UnityEngine.XR.Interaction.Toolkit.AffordanceSystem.Rendering.MaterialPropertyBlockHelper, Unity.XR.Interaction.Toolkit";

        const string GrabInteractableTypeName =
            "UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable, Unity.XR.Interaction.Toolkit";

        const string AffordanceChildName = "Highlight Interaction Affordance";

        public static bool IsAffordanceAvailable =>
            Type.GetType(StateProviderTypeName) != null && LoadAffordancePrefab() != null;

        public static bool HasHighlightAffordance(GameObject cubeRoot)
        {
            var providerType = Type.GetType(StateProviderTypeName);
            if (providerType == null) return false;
            return cubeRoot.GetComponentInChildren(providerType, true) != null;
        }

        public static bool NeedsAffordanceUpgrade(GameObject cubeRoot)
        {
            if (!VrBaselineInteractableSetup.IsXriAvailable) return false;
            if (!IsAffordanceAvailable) return false;
            return !HasHighlightAffordance(cubeRoot);
        }

        public static void ConfigureHighlightAffordance(GameObject cubeRoot)
        {
            if (!VrBaselineInteractableSetup.IsXriAvailable) return;

            var affordancePrefab = LoadAffordancePrefab();
            if (affordancePrefab == null) return;

            var grabType = Type.GetType(GrabInteractableTypeName);
            var providerType = Type.GetType(StateProviderTypeName);
            if (grabType == null || providerType == null) return;

            var grab = cubeRoot.GetComponent(grabType);
            if (grab == null) return;

            var renderer = cubeRoot.GetComponent<MeshRenderer>();
            if (renderer == null) return;

            GameObject affordanceRoot = null;
            foreach (Transform child in cubeRoot.transform)
            {
                if (child.name != AffordanceChildName) continue;
                affordanceRoot = child.gameObject;
                break;
            }

            if (affordanceRoot == null)
            {
                affordanceRoot = (GameObject)PrefabUtility.InstantiatePrefab(affordancePrefab, cubeRoot.transform);
                affordanceRoot.name = AffordanceChildName;
                affordanceRoot.transform.localPosition = Vector3.zero;
                affordanceRoot.transform.localRotation = Quaternion.identity;
                affordanceRoot.transform.localScale = Vector3.one;
            }

            var provider = affordanceRoot.GetComponent(providerType);
            if (provider != null)
            {
                var providerSo = new SerializedObject(provider);
                var sourceProp = providerSo.FindProperty("m_InteractableSource");
                if (sourceProp != null)
                {
                    sourceProp.objectReferenceValue = grab;
                    providerSo.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            var helperType = Type.GetType(RendererHelperTypeName);
            if (helperType == null) return;

            foreach (var helper in affordanceRoot.GetComponentsInChildren(helperType, true))
            {
                var helperSo = new SerializedObject(helper);
                var rendererProp = helperSo.FindProperty("m_Renderer");
                if (rendererProp != null)
                {
                    rendererProp.objectReferenceValue = renderer;
                    helperSo.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        static GameObject LoadAffordancePrefab()
        {
            var fromPath = AssetDatabase.LoadAssetAtPath<GameObject>(
                VrBaselineVisualPaths.XriHighlightAffordancePrefab);
            if (fromPath != null) return fromPath;

            var guids = AssetDatabase.FindAssets("HighlightInteractionAffordance t:Prefab");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("Affordances")) continue;
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            return null;
        }
    }
}
