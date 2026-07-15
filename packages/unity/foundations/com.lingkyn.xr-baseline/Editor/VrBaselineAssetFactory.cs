using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Lingkyn.Unity.XrBaseline.Editor.SceneSetup;
using Lingkyn.Unity.XrBaseline.Config;
using Lingkyn.Unity.XrBaseline.Constants;

namespace Lingkyn.Unity.XrBaseline.Editor.ConfigTools
{
    /// <summary>
    /// Creates vendor-neutral VR greybox materials and prefabs (floor, grabbable test cube).
    /// Does not create project-specific gameplay/content placeholders.
    /// </summary>
    public static class VrBaselineAssetFactory
    {
        public static void EnsureBaselineAssets(VrBaselineConfig config = null)
        {
            config ??= VrBaselineConfigAccess.EnsureExists();
            EnsureFolder(VrBaselineVisualPaths.MaterialsFolder);
            EnsureFolder(VrBaselineVisualPaths.PropsFolder);

            CreateFloorMaterial(config);
            UpgradeLitMaterial(
                VrBaselineVisualPaths.Interactable,
                config.interactableBaseColor,
                config.interactableSmoothness,
                Color.black);
            CreateLit(VrBaselineVisualPaths.Interactable, config.interactableBaseColor, config.interactableSmoothness);
            UpgradeEmissiveMaterial(
                VrBaselineVisualPaths.InteractableHighlight,
                new Color(0.45f, 0.78f, 0.95f),
                new Color(0.12f, 0.35f, 0.45f),
                0.45f);
            CreateEmissive(
                VrBaselineVisualPaths.InteractableHighlight,
                new Color(0.45f, 0.78f, 0.95f),
                new Color(0.12f, 0.35f, 0.45f),
                0.45f);
            CreateLit(VrBaselineVisualPaths.Environment, new Color(0.55f, 0.58f, 0.62f), 0.08f);
            CreateLit(VrBaselineVisualPaths.Disabled, new Color(0.25f, 0.25f, 0.27f), 0.05f);

            CreateFloorPlanePrefab();
            CreateGrabbableCubePrefab(config);
            VrBaselineScaleReferenceSetup.EnsureScaleReferencePrefab();

            AssetDatabase.SaveAssets();
        }

        static void CreateFloorPlanePrefab()
        {
            var path = VrBaselineVisualPaths.FloorPlanePrefab;
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                var needsScale = existing.transform.localScale != VrBaselineVisualPaths.FloorPlanePrefabScale;
                var needsCollider = existing.GetComponent<Collider>() == null;
                var needsTeleport = VrBaselineLocomotionSetup.IsXriLocomotionAvailable &&
                    (!VrBaselineLocomotionSetup.HasTeleportArea(existing) ||
                     !VrBaselineLocomotionSetup.HasValidTeleportLayers(existing));
                if (!needsScale && !needsCollider && !needsTeleport) return;

                var upgrade = (GameObject)PrefabUtility.InstantiatePrefab(existing);
                upgrade.transform.localScale = VrBaselineVisualPaths.FloorPlanePrefabScale;
                if (upgrade.GetComponent<Collider>() == null)
                {
                    upgrade.AddComponent<MeshCollider>();
                }

                Apply(upgrade, VrBaselineVisualPaths.Floor);
                VrBaselineLocomotionSetup.ConfigureFloorTeleportArea(upgrade);
                PrefabUtility.SaveAsPrefabAsset(upgrade, path);
                Object.DestroyImmediate(upgrade);
                return;
            }

            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "FloorPlane";
            plane.transform.localScale = VrBaselineVisualPaths.FloorPlanePrefabScale;
            Apply(plane, VrBaselineVisualPaths.Floor);
            VrBaselineLocomotionSetup.ConfigureFloorTeleportArea(plane);
            Save(plane, path);
        }

        static void CreateGrabbableCubePrefab(VrBaselineConfig config)
        {
            var path = VrBaselineVisualPaths.GrabbableCubePrefab;
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                var needsScale = existing.transform.localScale != VrBaselineVisualPaths.GrabbableCubePrefabScale;
                var needsAffordance = VrBaselineAffordanceSetup.NeedsAffordanceUpgrade(existing);
                var needsGrab = VrBaselineInteractableSetup.NeedsGrabbableUpgrade(existing);
                var needsHover = XrRigInteractionRepair.NeedsEmissionHoverUpgrade(existing);
                var needsConfigRefresh = VrBaselineHoverVisualSetup.NeedsConfigRefresh(existing, config);
                if (!needsScale && !needsAffordance && !needsGrab && !needsHover && !needsConfigRefresh) return;

                var upgrade = (GameObject)PrefabUtility.InstantiatePrefab(existing);
                upgrade.transform.localScale = VrBaselineVisualPaths.GrabbableCubePrefabScale;
                Apply(upgrade, VrBaselineVisualPaths.Interactable);
                VrBaselineInteractableSetup.ConfigureGrabbableCube(upgrade);
                VrBaselineHoverVisualSetup.Configure(upgrade, config);
                PrefabUtility.SaveAsPrefabAsset(upgrade, path);
                Object.DestroyImmediate(upgrade);
                return;
            }

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "GrabbableCube";
            cube.transform.localScale = VrBaselineVisualPaths.GrabbableCubePrefabScale;
            Apply(cube, VrBaselineVisualPaths.Interactable);
            VrBaselineInteractableSetup.ConfigureGrabbableCube(cube);
            VrBaselineHoverVisualSetup.Configure(cube, config);
            Save(cube, path);
        }

        static void CreateFloorMaterial(VrBaselineConfig config)
        {
            var path = VrBaselineVisualPaths.Floor;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) return;

            var checker = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Checker-Gray.png");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
                EnsureParentFolder(path);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.SetColor("_BaseColor", config.floorBaseColor);
            material.SetFloat("_Smoothness", 0.12f);
            material.SetFloat("_Metallic", 0f);
            if (checker != null)
            {
                material.SetTexture("_BaseMap", checker);
                material.SetTextureScale("_BaseMap", VrBaselineVisualPaths.FloorCheckerTiling);
            }

            EditorUtility.SetDirty(material);
        }

        static void CreateLit(string path, Color color, float smoothness)
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) return;

            var material = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            EnsureParentFolder(path);
            AssetDatabase.CreateAsset(material, path);
        }

        static void UpgradeLitMaterial(string path, Color color, float smoothness, Color emission)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) return;

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetColor("_EmissionColor", emission);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(material);
        }

        static void UpgradeEmissiveMaterial(string path, Color baseColor, Color emission, float smoothness)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) return;

            material.SetColor("_BaseColor", baseColor);
            material.SetColor("_EmissionColor", emission);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
        }

        static void CreateEmissive(string path, Color baseColor, Color emission, float smoothness)
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) return;

            var material = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            material.SetColor("_BaseColor", baseColor);
            material.SetColor("_EmissionColor", emission);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            material.SetFloat("_Smoothness", smoothness);
            EnsureParentFolder(path);
            AssetDatabase.CreateAsset(material, path);
        }

        static void Apply(GameObject go, string materialPath)
        {
            var renderer = go.GetComponent<MeshRenderer>();
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (renderer != null && material != null) renderer.sharedMaterial = material;
        }

        static void Save(GameObject instance, string path)
        {
            EnsureParentFolder(path);
            PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            EnsureParentFolder(folder + "/placeholder");
        }

        static void EnsureParentFolder(string assetPath)
        {
            var folder = System.IO.Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;

            var parts = folder.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
