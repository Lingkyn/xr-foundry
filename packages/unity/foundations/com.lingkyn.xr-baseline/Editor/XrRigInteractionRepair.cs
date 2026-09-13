using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Lingkyn.Unity.XrBaseline.Config;
using Object = UnityEngine.Object;

namespace Lingkyn.Unity.XrBaseline.Editor.SceneSetup
{
    /// <summary>
    /// Outcome of one XR rig interaction repair. <see cref="Diagnostics"/> names every far
    /// caster that could not be resolved or configured. An empty list means every discovered
    /// interactor was repaired or already satisfied the configured baseline; it never means
    /// "nothing was checked".
    /// </summary>
    public readonly struct XrRigInteractionRepairResult
    {
        public XrRigInteractionRepairResult(IReadOnlyList<string> configuredFarCasters, IReadOnlyList<string> diagnostics)
        {
            ConfiguredFarCasters = configuredFarCasters ?? Array.Empty<string>();
            Diagnostics = diagnostics ?? Array.Empty<string>();
        }

        /// <summary>One entry per far caster that was raised to, or already met, the configured distance.</summary>
        public IReadOnlyList<string> ConfiguredFarCasters { get; }

        /// <summary>One entry per interactor whose far cast distance could not be configured.</summary>
        public IReadOnlyList<string> Diagnostics { get; }

        public bool Succeeded => Diagnostics.Count == 0;
    }

    /// <summary>
    /// Repairs XR rig interaction stack: enabled controllers/interactors, visible far rays, hover feedback.
    /// Far cast distance is configured on the caster that each XRI 3.x NearFarInteractor actually
    /// references (any component implementing ICurveInteractionCaster); a missing or incompatible
    /// caster is reported instead of being skipped.
    /// </summary>
    public static class XrRigInteractionRepair
    {
        const string LineVisualTypeName =
            "UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual, Unity.XR.Interaction.Toolkit";
        const string CurveCasterInterfaceName = "ICurveInteractionCaster";
        const string CastDistancePropertyName = "m_CastDistance";
        const string LegacyFarDistancePropertyName = "m_FarInteractorCastDistance";

        public static XrRigInteractionRepairResult Repair(GameObject rigRoot, VrBaselineConfig config = null)
        {
            if (rigRoot == null)
            {
                return new XrRigInteractionRepairResult(
                    Array.Empty<string>(),
                    new[] { "Rig root is null; no interactor was repaired." });
            }

            var rayIdle = config?.rayIdleColor ?? new Color(0f, 0.78f, 1f, 0.9f);
            var rayHover = config?.rayHoverColor ?? new Color(1f, 0.55f, 0.08f, 1f);
            var castDistance = config?.farRayCastDistance ?? 30f;
            var restingLine = config?.restingVisualLineLength ?? 0.75f;
            var lineWidth = config?.rayLineWidth ?? 0.008f;

            EnableInteractionHierarchy(rigRoot);
            ConfigureLineVisuals(rigRoot, rayIdle, rayHover, restingLine, lineWidth);

            var configured = new List<string>();
            var diagnostics = new List<string>();
            ConfigureFarCastDistances(rigRoot, castDistance, configured, diagnostics);
            return new XrRigInteractionRepairResult(configured, diagnostics);
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

        static void ConfigureFarCastDistances(
            GameObject rigRoot,
            float castDistance,
            List<string> configured,
            List<string> diagnostics)
        {
            foreach (var component in rigRoot.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;

                var typeName = component.GetType().Name;
                if (typeName == "NearFarInteractor")
                {
                    var path = HierarchyPath(component.transform);
                    var caster = ResolveFarCaster(component);
                    if (caster != null)
                    {
                        ApplyCastDistance(caster, $"{path} -> {caster.GetType().Name}", CastDistancePropertyName, castDistance, configured, diagnostics);
                        continue;
                    }

                    // Older XRI revisions kept the far distance on the interactor itself.
                    if (TryApplyCastDistance(component, LegacyFarDistancePropertyName, castDistance, out var legacyMessage) ||
                        TryApplyCastDistance(component, CastDistancePropertyName, castDistance, out legacyMessage))
                    {
                        configured.Add($"{path} (NearFarInteractor legacy field): {legacyMessage}");
                        continue;
                    }

                    diagnostics.Add(
                        $"{path}: NearFarInteractor references no far caster implementing {CurveCasterInterfaceName} " +
                        $"and carries no legacy far distance field; castDistance {castDistance} was not applied.");
                }
                else if (typeName == "FarRaycastHitInteractor")
                {
                    ApplyCastDistance(component, $"{HierarchyPath(component.transform)} ({typeName})", CastDistancePropertyName, castDistance, configured, diagnostics);
                }
            }
        }

        /// <summary>
        /// Resolves the caster an XRI 3.x NearFarInteractor delegates far casting to. XRI is not a
        /// compile-time reference of this assembly, so the serialized reference is located by the
        /// interface it implements rather than by a hard-coded field name; a reference whose name
        /// contains "FarCaster" wins when several candidates exist.
        /// </summary>
        static Object ResolveFarCaster(Component interactor)
        {
            var serialized = new SerializedObject(interactor);
            var iterator = serialized.GetIterator();
            Object fallback = null;
            while (iterator.Next(true))
            {
                if (iterator.propertyType != SerializedPropertyType.ObjectReference) continue;
                var candidate = iterator.objectReferenceValue;
                if (candidate == null || !ImplementsInterface(candidate.GetType(), CurveCasterInterfaceName)) continue;
                if (iterator.name.IndexOf("FarCaster", StringComparison.OrdinalIgnoreCase) >= 0) return candidate;
                fallback ??= candidate;
            }

            return fallback;
        }

        static bool ImplementsInterface(Type type, string interfaceName)
        {
            foreach (var candidate in type.GetInterfaces())
            {
                if (candidate.Name == interfaceName) return true;
            }

            return false;
        }

        static void ApplyCastDistance(
            Object target,
            string label,
            string propertyName,
            float castDistance,
            List<string> configured,
            List<string> diagnostics)
        {
            if (TryApplyCastDistance(target, propertyName, castDistance, out var message))
            {
                configured.Add($"{label}: {message}");
                return;
            }

            diagnostics.Add(
                $"{label}: no float '{propertyName}' serialized field; this XR Interaction Toolkit revision is not " +
                "supported by the repair and castDistance was not applied.");
        }

        static bool TryApplyCastDistance(Object target, string propertyName, float castDistance, out string message)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.Float)
            {
                message = null;
                return false;
            }

            if (property.floatValue < castDistance)
            {
                var previous = property.floatValue;
                property.floatValue = castDistance;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                message = $"{propertyName} raised from {previous} to {castDistance}";
            }
            else
            {
                message = $"{propertyName} {property.floatValue} already meets {castDistance}";
            }

            return true;
        }

        static string HierarchyPath(Transform transform)
        {
            var path = transform.name;
            for (var parent = transform.parent; parent != null; parent = parent.parent)
            {
                path = parent.name + "/" + path;
            }

            return path;
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
