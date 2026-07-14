using System;
using UnityEditor;
using UnityEngine;
using Lingkyn.Unity.XrBaseline.Config;

namespace Lingkyn.Unity.XrBaseline.Editor.SceneSetup
{
    /// <summary>
    /// Repairs XR rig interaction stack: enabled controllers/interactors, visible far rays, hover feedback.
    /// </summary>
    public static class XrRigInteractionRepair
    {
        const string LineVisualTypeName =
            "UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual, Unity.XR.Interaction.Toolkit";

        public static void Repair(GameObject rigRoot, VrBaselineConfig config = null)
        {
            if (rigRoot == null) return;

            var rayIdle = config?.rayIdleColor ?? new Color(0f, 0.78f, 1f, 0.9f);
            var rayHover = config?.rayHoverColor ?? new Color(1f, 0.55f, 0.08f, 1f);
            var castDistance = config?.farRayCastDistance ?? 30f;
            var restingLine = config?.restingVisualLineLength ?? 0.75f;
            var lineWidth = config?.rayLineWidth ?? 0.008f;

            EnableInteractionHierarchy(rigRoot);
            ConfigureLineVisuals(rigRoot, rayIdle, rayHover, restingLine, lineWidth);
            ConfigureFarCastDistances(rigRoot, castDistance);
        }

        public static void RepairGrabbableAffordance(GameObject cubeRoot)
        {
            VrBaselineHoverVisualSetup.Configure(cubeRoot);
        }

        public static bool NeedsEmissionHoverUpgrade(GameObject cubeRoot) =>
            !VrBaselineHoverVisualSetup.IsConfigured(cubeRoot);

        static void EnableInteractionHierarchy(GameObject rigRoot)
        {
            foreach (var transform in rigRoot.GetComponentsInChildren<Transform>(true))
            {
                var go = transform.gameObject;
                if (IsIntentionallyDisabledNode(go)) continue;
                if (!IsInteractionRelated(go)) continue;

                if (!go.activeSelf) go.SetActive(true);

                foreach (var behaviour in go.GetComponents<Behaviour>())
                {
                    if (behaviour == null || IsIntentionallyDisabledBehaviour(behaviour)) continue;
                    if (!behaviour.enabled) behaviour.enabled = true;
                }
            }
        }

        static bool IsInteractionRelated(GameObject go)
        {
            if (go.name.Contains("Controller Visual", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (go.name.Contains("Controller", StringComparison.OrdinalIgnoreCase) ||
                go.name.Contains("Interactor", StringComparison.OrdinalIgnoreCase) ||
                go.name.Contains("NearFar", StringComparison.OrdinalIgnoreCase) ||
                go.name.Contains("LineVisual", StringComparison.OrdinalIgnoreCase) ||
                go.name.Equals("Locomotion", StringComparison.OrdinalIgnoreCase) ||
                go.name.Equals("Teleportation", StringComparison.OrdinalIgnoreCase) ||
                go.name.Equals("Turn", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            foreach (var component in go.GetComponents<Component>())
            {
                if (component == null) continue;
                var typeName = component.GetType().Name;
                if (typeName.Contains("Interactor") ||
                    typeName.Contains("InteractionGroup") ||
                    typeName.Contains("LineVisual"))
                {
                    return true;
                }
            }

            return false;
        }

        static bool IsIntentionallyDisabledNode(GameObject go)
        {
            var name = go.name;
            if (name.Equals("Move", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Grab Move", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Climb", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return IsDuplicateXriControllerVisual(go);
        }

        static bool IsDuplicateXriControllerVisual(GameObject go)
        {
            if (!go.name.Contains("Controller Visual", StringComparison.OrdinalIgnoreCase)) return false;

            var parent = go.transform.parent;
            if (parent == null) return false;

            for (var i = 0; i < parent.childCount; i++)
            {
                var siblingName = parent.GetChild(i).name;
                if (siblingName.Contains("[Building Block]", StringComparison.OrdinalIgnoreCase) &&
                    siblingName.Contains("Controller", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        static bool IsIntentionallyDisabledBehaviour(Behaviour behaviour)
        {
            var typeName = behaviour.GetType().Name;
            return typeName.Contains("ContinuousMove") ||
                   typeName.Contains("GrabMove") ||
                   typeName.Contains("Climb");
        }

        static void ConfigureLineVisuals(
            GameObject rigRoot,
            Color rayIdle,
            Color rayHover,
            float restingLine,
            float lineWidth)
        {
            var lineVisualType = Type.GetType(LineVisualTypeName);
            if (lineVisualType == null) return;

            foreach (var lineVisual in rigRoot.GetComponentsInChildren(lineVisualType, true))
            {
                if (lineVisual is not Behaviour behaviour) continue;
                behaviour.enabled = true;

                var serialized = new SerializedObject(lineVisual);
                SetFloat(serialized, "m_RestingVisualLineLength", restingLine);
                SetFloat(serialized, "m_MaxVisualCurveDistance", 30f);
                SetBool(serialized, "m_ExtendLineToEmptyHit", true);

                ConfigureRayGradient(serialized, "m_NoValidHitProperties", rayIdle, 0.006f);
                ConfigureRayGradient(serialized, "m_HoverHitProperties", rayHover, 0.007f);
                ConfigureRayGradient(serialized, "m_SelectHitProperties", rayHover, 0.007f);

                SetLegacyRayGradient(serialized, "m_InvalidColorGradient", rayIdle);
                SetLegacyRayGradient(serialized, "m_ValidColorGradient", rayHover);
                SetFloat(serialized, "m_LineWidth", lineWidth);

                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void ConfigureRayGradient(SerializedObject serialized, string propertyName, Color color, float width)
        {
            var stateProps = serialized.FindProperty(propertyName);
            if (stateProps == null) return;

            stateProps.FindPropertyRelative("m_AdjustWidth").boolValue = true;
            stateProps.FindPropertyRelative("m_StarWidth").floatValue = width;
            stateProps.FindPropertyRelative("m_EndWidth").floatValue = width * 0.75f;
            stateProps.FindPropertyRelative("m_AdjustGradient").boolValue = true;

            var gradient = stateProps.FindPropertyRelative("m_Gradient");
            if (gradient == null) return;

            gradient.FindPropertyRelative("key0").colorValue = new Color(color.r, color.g, color.b, 0f);
            gradient.FindPropertyRelative("key1").colorValue = color;
        }

        static void SetLegacyRayGradient(SerializedObject serialized, string propertyName, Color color)
        {
            var gradient = serialized.FindProperty(propertyName);
            if (gradient == null) return;

            gradient.FindPropertyRelative("key0").colorValue = color;
            gradient.FindPropertyRelative("key1").colorValue = new Color(color.r, color.g, color.b, 0.25f);
        }

        static void ConfigureFarCastDistances(GameObject rigRoot, float castDistance)
        {
            foreach (var component in rigRoot.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;

                var typeName = component.GetType().Name;
                if (typeName is not ("FarRaycastHitInteractor" or "NearFarInteractor")) continue;

                var serialized = new SerializedObject(component);
                var castDistanceProp = serialized.FindProperty("m_CastDistance");
                if (castDistanceProp != null && castDistanceProp.floatValue < castDistance)
                {
                    castDistanceProp.floatValue = castDistance;
                }

                var farDistance = serialized.FindProperty("m_FarInteractorCastDistance");
                if (farDistance != null && farDistance.floatValue < castDistance)
                {
                    farDistance.floatValue = castDistance;
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void SetFloat(SerializedObject serialized, string propertyName, float value)
        {
            var property = serialized.FindProperty(propertyName);
            if (property != null) property.floatValue = value;
        }

        static void SetBool(SerializedObject serialized, string propertyName, bool value)
        {
            var property = serialized.FindProperty(propertyName);
            if (property != null) property.boolValue = value;
        }
    }
}
