using System;
using UnityEditor;
using UnityEngine;

namespace Lingkyn.Unity.XrBaseline.Editor.SceneSetup
{
    /// <summary>
    /// Adds XRI grab components when the Interaction Toolkit package is present.
    /// Uses reflection so runtime assemblies stay free of XRI references.
    /// </summary>
    public static class VrBaselineInteractableSetup
    {
        const string GrabInteractableTypeName =
            "UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable, Unity.XR.Interaction.Toolkit";

        public static bool IsXriAvailable => Type.GetType(GrabInteractableTypeName) != null;

        public static bool HasGrabInteractable(GameObject root)
        {
            var grabType = Type.GetType(GrabInteractableTypeName);
            return grabType != null && root.GetComponent(grabType) != null;
        }

        public static bool HasConfiguredGrabCollider(GameObject root)
        {
            if (root.GetComponent<Collider>() == null) return false;

            var grabType = Type.GetType(GrabInteractableTypeName);
            if (grabType == null) return true;

            var grab = root.GetComponent(grabType);
            if (grab == null) return false;

            var serializedGrab = new SerializedObject(grab);
            var collidersProp = serializedGrab.FindProperty("m_Colliders");
            if (collidersProp == null || collidersProp.arraySize == 0) return false;

            return collidersProp.GetArrayElementAtIndex(0).objectReferenceValue != null;
        }

        public static bool NeedsGrabbableUpgrade(GameObject root)
        {
            if (!HasGrabInteractable(root)) return true;
            return !HasConfiguredGrabCollider(root);
        }

        public static void ConfigureGrabbableCube(GameObject root)
        {
            var grabType = Type.GetType(GrabInteractableTypeName);
            if (grabType == null) return;

            var collider = root.GetComponent<Collider>() ?? root.AddComponent<BoxCollider>();

            var rb = root.GetComponent<Rigidbody>();
            if (rb == null) rb = root.AddComponent<Rigidbody>();
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            var grab = root.GetComponent(grabType) ?? root.AddComponent(grabType);
            var serializedGrab = new SerializedObject(grab);
            var collidersProp = serializedGrab.FindProperty("m_Colliders");
            if (collidersProp != null)
            {
                collidersProp.arraySize = 1;
                collidersProp.GetArrayElementAtIndex(0).objectReferenceValue = collider;
                serializedGrab.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
