using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Lingkyn.Unity.XrBaseline.Config;
using Lingkyn.Unity.XrBaseline.Constants;
using Lingkyn.Unity.XrBaseline.Editor;

namespace Lingkyn.Unity.XrBaseline.Editor.SceneSetup
{
    /// <summary>
    /// Places generic XR greybox props into a consumer Sandbox smoke-test scene.
    /// </summary>
    public static class VrBaselineScenePlacer
    {
        const string InteractionManagerTypeName =
            "UnityEngine.XR.Interaction.Toolkit.XRInteractionManager, Unity.XR.Interaction.Toolkit";

        public static void PlaceGreybox(Scene scene, Transform sceneRoot, VrBaselineConfig config = null)
        {
            EnsureInteractionManager(scene, sceneRoot);
            var environment = sceneRoot.Find("_World/Environment");
            EnsureFloor(environment);
            EnsureGrabbableCube(sceneRoot, config);
            if (environment != null)
            {
                VrBaselineEnclosureSetup.EnsureEnclosure(environment);
                VrBaselineScaleReferenceSetup.EnsureScaleReference(environment);
            }

            VrBaselineSandboxLightingSetup.Apply(scene, sceneRoot, config);
        }

        static void EnsureInteractionManager(Scene scene, Transform sceneRoot)
        {
            var managerType = Type.GetType(InteractionManagerTypeName);
            if (managerType == null)
            {
                XrBaselineDiagnostics.Unresolved("xri.interaction-manager", "XRInteractionManager type is not loaded; no interaction manager was placed in the Sandbox.");
                return;
            }

            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren(managerType, true) != null) return;
            }

            var systems = sceneRoot.Find("_Systems");
            var parent = systems != null ? systems : sceneRoot;
            var managerGo = new GameObject("XR Interaction Manager");
            managerGo.transform.SetParent(parent, false);
            managerGo.AddComponent(managerType);
        }

        static void EnsureFloor(Transform environment)
        {
            if (environment == null) return;

            var legacyFloor = environment.Find("SandboxFloor");
            if (legacyFloor != null)
            {
                UnityEngine.Object.DestroyImmediate(legacyFloor.gameObject);
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VrBaselineVisualPaths.FloorPlanePrefab);
            if (prefab == null)
            {
                XrBaselineDiagnostics.Unresolved("sandbox.floor-prefab", $"the floor prefab is missing at {VrBaselineVisualPaths.FloorPlanePrefab}; no floor was placed.");
                return;
            }

            var existing = environment.Find(VrBaselineVisualPaths.SandboxFloorObjectName);
            if (existing != null)
            {
                SyncPrefabScale(existing, prefab);
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, environment);
            instance.name = VrBaselineVisualPaths.SandboxFloorObjectName;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            SyncPrefabScale(instance.transform, prefab);
        }

        static void EnsureGrabbableCube(Transform sceneRoot, VrBaselineConfig config)
        {
            var interactables = sceneRoot.Find("_Gameplay/Interactables");
            if (interactables == null)
            {
                XrBaselineDiagnostics.Unresolved("sandbox.hierarchy.interactables", "the Sandbox hierarchy has no _Gameplay/Interactables node; no grabbable cube was placed.", sceneRoot);
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VrBaselineVisualPaths.GrabbableCubePrefab);
            if (prefab == null)
            {
                XrBaselineDiagnostics.Unresolved("sandbox.grabbable-cube-prefab", $"the grabbable cube prefab is missing at {VrBaselineVisualPaths.GrabbableCubePrefab}; no cube was placed.");
                return;
            }

            var existing = interactables.Find(VrBaselineVisualPaths.SandboxGrabbableObjectName);
            if (existing != null)
            {
                SyncPrefabScale(existing, prefab);
                existing.localPosition = VrBaselineVisualPaths.SandboxGrabbableLocalPosition;
                VrBaselineHoverVisualSetup.Configure(existing.gameObject, config);
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, interactables);
            instance.name = VrBaselineVisualPaths.SandboxGrabbableObjectName;
            instance.transform.localPosition = VrBaselineVisualPaths.SandboxGrabbableLocalPosition;
            instance.transform.localRotation = Quaternion.identity;
            SyncPrefabScale(instance.transform, prefab);
            VrBaselineHoverVisualSetup.Configure(instance, config);
        }

        static void SyncPrefabScale(Transform instance, GameObject prefab)
        {
            instance.localScale = prefab.transform.localScale;
        }
    }
}
