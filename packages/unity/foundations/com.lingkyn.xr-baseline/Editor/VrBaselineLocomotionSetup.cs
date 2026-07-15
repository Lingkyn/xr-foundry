using System;
using UnityEditor;
using UnityEngine;

namespace Lingkyn.Unity.XrBaseline.Editor.SceneSetup
{
    /// <summary>
    /// Optional XRI locomotion helpers for greybox floors (teleport areas).
    /// Uses reflection so runtime assemblies stay free of XRI references.
    /// </summary>
    public static class VrBaselineLocomotionSetup
    {
        const string TeleportAreaTypeName =
            "UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationArea, Unity.XR.Interaction.Toolkit";

        /// <summary>XRI Teleport interaction layer (bit 31).</summary>
        public const int TeleportInteractionLayerMask = 1 << 31;

        public static bool IsXriLocomotionAvailable => Type.GetType(TeleportAreaTypeName) != null;

        public static bool HasTeleportArea(GameObject floor)
        {
            var teleportType = Type.GetType(TeleportAreaTypeName);
            return teleportType != null && floor.GetComponent(teleportType) != null;
        }

        public static bool HasValidTeleportLayers(GameObject floor)
        {
            var teleportType = Type.GetType(TeleportAreaTypeName);
            if (teleportType == null) return false;
            var teleportArea = floor.GetComponent(teleportType);
            if (teleportArea == null) return false;

            var serializedArea = new SerializedObject(teleportArea);
            var layersProp = serializedArea.FindProperty("m_InteractionLayers.m_Bits");
            return layersProp != null && layersProp.intValue == TeleportInteractionLayerMask;
        }

        public static void ConfigureFloorTeleportArea(GameObject floor)
        {
            var teleportType = Type.GetType(TeleportAreaTypeName);
            if (teleportType == null) return;

            var collider = floor.GetComponent<Collider>();
            if (collider == null) return;

            var teleportArea = floor.GetComponent(teleportType) ?? floor.AddComponent(teleportType);
            var serializedArea = new SerializedObject(teleportArea);

            var collidersProp = serializedArea.FindProperty("m_Colliders");
            if (collidersProp != null)
            {
                collidersProp.arraySize = 1;
                collidersProp.GetArrayElementAtIndex(0).objectReferenceValue = collider;
            }

            var layersProp = serializedArea.FindProperty("m_InteractionLayers.m_Bits");
            if (layersProp != null)
            {
                layersProp.intValue = TeleportInteractionLayerMask;
            }

            serializedArea.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
