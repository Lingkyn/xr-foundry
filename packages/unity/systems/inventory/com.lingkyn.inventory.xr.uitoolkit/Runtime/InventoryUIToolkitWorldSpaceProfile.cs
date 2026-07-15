using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lingkyn.Inventory.XR.UIToolkit
{
    [CreateAssetMenu(
        fileName = "InventoryUIToolkitWorldSpaceProfile",
        menuName = "XR Foundry/Inventory/UI Toolkit World-Space Profile")]
    public sealed class InventoryUIToolkitWorldSpaceProfile : ScriptableObject
    {
        [SerializeField] private PanelSettings panelSettings;
        [SerializeField] private Vector2 referenceResolution = new Vector2(640f, 440f);
        [SerializeField] private float panelPixelsPerUnit = 100f;
        [SerializeField] private float metersPerPixel = 0.001f;
        [SerializeField] private float colliderDepthMeters = 0.02f;
        [SerializeField] private float defaultDistanceMeters = 1.25f;
        [SerializeField] private float maxInteractionDistanceMeters = 3f;
        [SerializeField] private float defaultVerticalOffsetMeters = -0.05f;

        public PanelSettings PanelSettings => panelSettings;
        public Vector2 ReferenceResolution => referenceResolution;
        public float PanelPixelsPerUnit => panelPixelsPerUnit;
        public float MetersPerPixel => metersPerPixel;
        public float ColliderDepthMeters => colliderDepthMeters;
        public float DefaultDistanceMeters => defaultDistanceMeters;
        public float MaxInteractionDistanceMeters => maxInteractionDistanceMeters;
        public float DefaultVerticalOffsetMeters => defaultVerticalOffsetMeters;
        public float LocalScale => panelPixelsPerUnit * metersPerPixel;
        public Vector2 PhysicalSizeMeters => referenceResolution * metersPerPixel;
        public Vector3 ColliderSizeLocal => new Vector3(
            referenceResolution.x / panelPixelsPerUnit,
            referenceResolution.y / panelPixelsPerUnit,
            colliderDepthMeters / LocalScale);

        public void Validate()
        {
            if (panelSettings == null)
            {
                throw new InvalidOperationException("Inventory UI Toolkit world-space profile requires PanelSettings.");
            }
            if (panelSettings.renderMode != PanelRenderMode.WorldSpace)
            {
                throw new InvalidOperationException("Inventory UI Toolkit PanelSettings must use WorldSpace render mode.");
            }
            if (!PanelKeepsExplicitCollider(panelSettings))
            {
                throw new InvalidOperationException(
                    "Inventory UI Toolkit PanelSettings collider update mode must be Keep.");
            }
            if (!PanelKeepsColliderAsTrigger(panelSettings))
            {
                throw new InvalidOperationException(
                    "Inventory UI Toolkit PanelSettings must keep its explicit collider as a trigger.");
            }
            if (!PositiveFinite(referenceResolution.x) || !PositiveFinite(referenceResolution.y))
            {
                throw new InvalidOperationException("Inventory UI Toolkit reference resolution must be finite and positive.");
            }
            if (!Finite(panelPixelsPerUnit) || panelPixelsPerUnit < 1f || panelPixelsPerUnit > 2000f)
            {
                throw new InvalidOperationException("Inventory UI Toolkit panel pixels per unit must be between 1 and 2000.");
            }
            if (!Finite(metersPerPixel) || metersPerPixel < 0.0001f || metersPerPixel > 0.01f)
            {
                throw new InvalidOperationException("Inventory UI Toolkit meters per pixel must be between 0.0001 and 0.01.");
            }
            if (!Finite(colliderDepthMeters) || colliderDepthMeters < 0.001f || colliderDepthMeters > 0.25f)
            {
                throw new InvalidOperationException("Inventory UI Toolkit collider depth must be between 0.001 and 0.25 meters.");
            }
            if (!Finite(defaultDistanceMeters) || defaultDistanceMeters < 0.25f || defaultDistanceMeters > 5f)
            {
                throw new InvalidOperationException("Inventory UI Toolkit default distance must be between 0.25 and 5 meters.");
            }
            if (!Finite(maxInteractionDistanceMeters) || maxInteractionDistanceMeters <= 0f ||
                maxInteractionDistanceMeters > 100f)
            {
                throw new InvalidOperationException(
                    "Inventory UI Toolkit maximum interaction distance must be finite, positive, and no more than 100 meters.");
            }
            if (maxInteractionDistanceMeters < defaultDistanceMeters)
            {
                throw new InvalidOperationException(
                    "Inventory UI Toolkit maximum interaction distance must cover the default placement distance.");
            }
            if (!Finite(defaultVerticalOffsetMeters) || defaultVerticalOffsetMeters < -2f || defaultVerticalOffsetMeters > 2f)
            {
                throw new InvalidOperationException("Inventory UI Toolkit vertical offset must be between -2 and 2 meters.");
            }
        }

        private static bool PositiveFinite(float value) => Finite(value) && value > 0f;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        internal static bool PanelKeepsExplicitCollider(PanelSettings settings)
        {
            try
            {
                var property = typeof(PanelSettings).GetProperty(
                    "colliderUpdateMode",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var value = property?.GetValue(settings);
                return value != null && string.Equals(value.ToString(), "Keep", StringComparison.Ordinal);
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static bool PanelKeepsColliderAsTrigger(PanelSettings settings)
        {
            try
            {
                var property = typeof(PanelSettings).GetProperty(
                    "colliderIsTrigger",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return property?.GetValue(settings) is bool value && value;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
