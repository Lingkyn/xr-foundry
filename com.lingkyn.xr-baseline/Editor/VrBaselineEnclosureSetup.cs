using UnityEditor;
using UnityEngine;
using Lingkyn.Unity.XrBaseline.Constants;

namespace Lingkyn.Unity.XrBaseline.Editor.SceneSetup
{
    /// <summary>
    /// Greybox perimeter walls so Sandbox reads as an enclosed volume, not an infinite sky dome.
    /// </summary>
    public static class VrBaselineEnclosureSetup
    {
        static readonly (string name, Vector3 position, Vector3 scale)[] WallSpecs =
        {
            ("GreyboxWall_North", new Vector3(0f, VrBaselineVisualPaths.EnclosureWallHeight * 0.5f, VrBaselineVisualPaths.EnclosureHalfExtent),
                new Vector3(VrBaselineVisualPaths.EnclosureHalfExtent * 2f, VrBaselineVisualPaths.EnclosureWallHeight, VrBaselineVisualPaths.EnclosureWallThickness)),
            ("GreyboxWall_South", new Vector3(0f, VrBaselineVisualPaths.EnclosureWallHeight * 0.5f, -VrBaselineVisualPaths.EnclosureHalfExtent),
                new Vector3(VrBaselineVisualPaths.EnclosureHalfExtent * 2f, VrBaselineVisualPaths.EnclosureWallHeight, VrBaselineVisualPaths.EnclosureWallThickness)),
            ("GreyboxWall_East", new Vector3(VrBaselineVisualPaths.EnclosureHalfExtent, VrBaselineVisualPaths.EnclosureWallHeight * 0.5f, 0f),
                new Vector3(VrBaselineVisualPaths.EnclosureWallThickness, VrBaselineVisualPaths.EnclosureWallHeight, VrBaselineVisualPaths.EnclosureHalfExtent * 2f)),
            ("GreyboxWall_West", new Vector3(-VrBaselineVisualPaths.EnclosureHalfExtent, VrBaselineVisualPaths.EnclosureWallHeight * 0.5f, 0f),
                new Vector3(VrBaselineVisualPaths.EnclosureWallThickness, VrBaselineVisualPaths.EnclosureWallHeight, VrBaselineVisualPaths.EnclosureHalfExtent * 2f)),
        };

        public static void EnsureEnclosure(Transform environmentParent)
        {
            if (environmentParent == null) return;

            var material = AssetDatabase.LoadAssetAtPath<Material>(VrBaselineVisualPaths.Environment);
            if (material == null) return;

            var enclosure = environmentParent.Find(VrBaselineVisualPaths.SandboxEnclosureName);
            if (enclosure == null)
            {
                var go = new GameObject(VrBaselineVisualPaths.SandboxEnclosureName);
                go.transform.SetParent(environmentParent, false);
                enclosure = go.transform;
            }

            foreach (var spec in WallSpecs)
            {
                EnsureWall(enclosure, spec.name, spec.position, spec.scale, material);
            }
        }

        static void EnsureWall(Transform parent, string wallName, Vector3 localPosition, Vector3 localScale, Material material)
        {
            var wall = parent.Find(wallName);
            if (wall == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = wallName;
                go.transform.SetParent(parent, false);
                wall = go.transform;
            }

            wall.localPosition = localPosition;
            wall.localRotation = Quaternion.identity;
            wall.localScale = localScale;

            var renderer = wall.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = material;

            var collider = wall.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
        }
    }
}
