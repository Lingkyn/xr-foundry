using System;
using UnityEngine;

namespace Lingkyn.Inventory.XR.UGUI
{
    [CreateAssetMenu(fileName = "InventoryWorldSpaceProfile", menuName = "XR Foundry/Inventory/World-Space Profile")]
    public sealed class InventoryWorldSpaceProfile : ScriptableObject
    {
        [SerializeField] private Vector2 referenceResolution = new Vector2(640f, 440f);
        [SerializeField] private float metersPerPixel = 0.001f;
        [SerializeField] private float dynamicPixelsPerUnit = 10f;
        [SerializeField] private float defaultDistanceMeters = 1.25f;
        [SerializeField] private float defaultVerticalOffsetMeters = -0.05f;
        [SerializeField] private bool ignoreReversedGraphics = true;
        [SerializeField] private bool checkFor3DOcclusion;

        public Vector2 ReferenceResolution => referenceResolution;
        public float MetersPerPixel => metersPerPixel;
        public float DynamicPixelsPerUnit => dynamicPixelsPerUnit;
        public float DefaultDistanceMeters => defaultDistanceMeters;
        public float DefaultVerticalOffsetMeters => defaultVerticalOffsetMeters;
        public bool IgnoreReversedGraphics => ignoreReversedGraphics;
        public bool CheckFor3DOcclusion => checkFor3DOcclusion;
        public Vector2 PhysicalSizeMeters => referenceResolution * metersPerPixel;

        public void Validate()
        {
            if (!Finite(referenceResolution.x) || !Finite(referenceResolution.y) ||
                referenceResolution.x <= 0f || referenceResolution.y <= 0f)
            {
                throw new InvalidOperationException("Inventory world-space reference resolution must be finite and positive.");
            }

            if (!Finite(metersPerPixel) || metersPerPixel < 0.0001f || metersPerPixel > 0.01f)
            {
                throw new InvalidOperationException("Inventory world-space meters per pixel must be between 0.0001 and 0.01.");
            }

            if (!Finite(dynamicPixelsPerUnit) || dynamicPixelsPerUnit < 1f || dynamicPixelsPerUnit > 100f)
            {
                throw new InvalidOperationException("Inventory world-space dynamic pixels per unit must be between 1 and 100.");
            }

            if (!Finite(defaultDistanceMeters) || defaultDistanceMeters < 0.25f || defaultDistanceMeters > 5f)
            {
                throw new InvalidOperationException("Inventory world-space default distance must be between 0.25 and 5 meters.");
            }

            if (!Finite(defaultVerticalOffsetMeters) || defaultVerticalOffsetMeters < -2f || defaultVerticalOffsetMeters > 2f)
            {
                throw new InvalidOperationException("Inventory world-space vertical offset must be between -2 and 2 meters.");
            }
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
