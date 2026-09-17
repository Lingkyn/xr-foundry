using System;
using UnityEditor;
using UnityEngine;
using Lingkyn.Unity.XrBaseline.Constants;
using Lingkyn.Unity.XrBaseline.Editor;

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
            if (affordancePrefab == null)
            {
                XrBaselineDiagnostics.Unresolved("xri.starter-assets.highlight-affordance-prefab", "the XRI Starter Assets highlight affordance prefab was not found; import the Starter Assets sample. No affordance was added.", cubeRoot);
                return;
            }

            var grabType = Type.GetType(GrabInteractableTypeName);
            var providerType = Type.GetType(StateProviderTypeName);
            if (grabType == null || providerType == null)
            {
                XrBaselineDiagnostics.Unresolved("xri.affordance-state-provider", "XRGrabInteractable or XRInteractableAffordanceStateProvider is not loaded in this XRI revision; no affordance was added.", cubeRoot);
                return;
            }

            var grab = cubeRoot.GetComponent(grabType);
            if (grab == null)
            {
                XrBaselineDiagnostics.Unresolved("sandbox.cube.grab-interactable", "the grabbable cube has no XRGrabInteractable; run the interactable setup first. No affordance was added.", cubeRoot);
                return;
            }

            var renderer = cubeRoot.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                XrBaselineDiagnostics.Unresolved("sandbox.cube.renderer", "the grabbable cube has no MeshRenderer; no affordance was added.", cubeRoot);
                return;
            }

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
                else
                {
                    XrBaselineDiagnostics.Unresolved("xri.affordance.m_InteractableSource", "the affordance state provider exposes no m_InteractableSource field in this XRI revision; the affordance is not bound to the cube.", affordanceRoot);
                }
            }
            else
            {
                XrBaselineDiagnostics.Unresolved("xri.affordance.provider-missing", "the instantiated affordance prefab carries no XRInteractableAffordanceStateProvider; the affordance is not bound to the cube.", affordanceRoot);
            }

            var helperType = Type.GetType(RendererHelperTypeName);
            if (helperType == null)
            {
                XrBaselineDiagnostics.Unresolved("xri.material-property-block-helper", "MaterialPropertyBlockHelper is not loaded in this XRI revision; the affordance renderer was not bound.", affordanceRoot);
                return;
            }

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
