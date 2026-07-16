using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Lingkyn.Unity.XrBaseline.Interaction;
using Lingkyn.Unity.XrBaseline.Constants;
using Lingkyn.Unity.XrBaseline.Config;

namespace Lingkyn.Unity.XrBaseline.Editor.SceneSetup
{
    /// <summary>
    /// Vendor-neutral XR Origin setup using XRI Starter Assets sample prefab.
    /// </summary>
    public static class GenericXrRigFactory
    {
        public const string RigObjectName = "XROriginRig";

        public static GameObject EnsureRig(Scene scene, Transform playerParent, VrBaselineConfig config = null)
        {
            var existing = FindRigUnderPlayer(playerParent) ?? FindRigInScene(scene);
            if (existing != null)
            {
                ReparentUnderPlayer(existing, playerParent);
                XrCameraTrackingRepair.Repair(existing);
                XrRigInteractionRepair.Repair(existing, config);
                EnsureXrRigAnchor(existing);
                return existing;
            }

            RemoveStaleRigsUnder(playerParent);

            var source = XrPrefabFactory.LoadOrResolveOriginSource();
            if (source == null) return null;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, playerParent);
            instance.name = RigObjectName;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            instance.SetActive(true);

            XrCameraTrackingRepair.Repair(instance);
            XrRigInteractionRepair.Repair(instance, config);
            EnsureXrRigAnchor(instance);
            return instance;
        }

        public static GameObject FindRigInScene(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = FindRigInHierarchy(root.transform);
                if (found != null) return found;
            }

            return null;
        }

        static GameObject FindRigUnderPlayer(Transform playerParent)
        {
            if (playerParent == null) return null;
            for (var i = 0; i < playerParent.childCount; i++)
            {
                var child = playerParent.GetChild(i);
                if (IsXrOriginRoot(child.gameObject)) return child.gameObject;
            }

            return null;
        }

        static GameObject FindRigInHierarchy(Transform parent)
        {
            if (IsXrOriginRoot(parent.gameObject)) return parent.gameObject;
            for (var i = 0; i < parent.childCount; i++)
            {
                var found = FindRigInHierarchy(parent.GetChild(i));
                if (found != null) return found;
            }

            return null;
        }

        public static bool IsXrOriginRoot(GameObject go)
        {
            if (go.name.Contains("XR Origin") || go.name == RigObjectName) return true;
            foreach (var component in go.GetComponents<Component>())
            {
                if (component != null && component.GetType().Name == "XROrigin") return true;
            }

            return false;
        }

        static void RemoveStaleRigsUnder(Transform playerParent)
        {
            for (var i = playerParent.childCount - 1; i >= 0; i--)
            {
                var child = playerParent.GetChild(i);
                if (IsXrOriginRoot(child.gameObject) || child.name.Contains("[Building Block]"))
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        static void ReparentUnderPlayer(GameObject rigRoot, Transform playerParent)
        {
            if (rigRoot == null || playerParent == null) return;
            rigRoot.transform.SetParent(playerParent, false);
            rigRoot.transform.localPosition = Vector3.zero;
            rigRoot.transform.localRotation = Quaternion.identity;
            rigRoot.transform.localScale = Vector3.one;
            rigRoot.SetActive(true);
        }

        static void EnsureXrRigAnchor(GameObject rig)
        {
            var origin = FindXrOriginGameObject(rig) ?? rig;
            if (origin.GetComponent<XrRigAnchor>() != null) return;
            origin.AddComponent<XrRigAnchor>();
        }

        static GameObject FindXrOriginGameObject(GameObject rig)
        {
            foreach (var component in rig.GetComponents<Component>())
            {
                if (component != null && component.GetType().Name == "XROrigin") return rig;
            }

            foreach (var component in rig.GetComponentsInChildren<Component>(true))
            {
                if (component != null && component.GetType().Name == "XROrigin")
                {
                    return component.gameObject;
                }
            }

            return rig;
        }
    }
}
