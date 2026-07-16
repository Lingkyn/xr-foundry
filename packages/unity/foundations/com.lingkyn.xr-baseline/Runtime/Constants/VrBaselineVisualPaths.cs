using UnityEngine;

namespace Lingkyn.Unity.XrBaseline.Constants
{
    /// <summary>
    /// Greybox materials and props for spatial prototyping in this VR project.
    /// </summary>
    public static class VrBaselineVisualPaths
    {
        public const string MaterialsFolder = "Assets/_Project/Art/Materials";
        public const string PropsFolder = "Assets/_Project/Prefabs/Props";

        public const string Floor = MaterialsFolder + "/M_Floor.mat";
        public const string Environment = MaterialsFolder + "/M_Environment.mat";
        public const string Interactable = MaterialsFolder + "/M_Interactable.mat";
        public const string InteractableHighlight = MaterialsFolder + "/M_Interactable_Highlight.mat";
        public const string Disabled = MaterialsFolder + "/M_Disabled.mat";

        public const string FloorPlanePrefab = PropsFolder + "/FloorPlane.prefab";
        public const string GrabbableCubePrefab = PropsFolder + "/GrabbableCube.prefab";
        public const string ScaleReferencePrefab = PropsFolder + "/ScaleReference1m.prefab";

        /// <summary>XRI Starter Assets highlight affordance (optional if sample not imported).</summary>
        public const string XriHighlightAffordancePrefab =
            "Assets/Samples/XR Interaction Toolkit/3.3.1/Starter Assets/Prefabs/Affordances/HighlightInteractionAffordance.prefab";

        /// <summary>
        /// Unity Plane mesh is 10×10 m at scale 1; (10,1,10) yields a 100×100 m greybox floor.
        /// </summary>
        public static readonly Vector3 FloorPlanePrefabScale = new(10f, 1f, 10f);

        /// <summary>0.2 m grab smoke-test cube (Cube mesh is 1 m at scale 1).</summary>
        public static readonly Vector3 GrabbableCubePrefabScale = new(0.2f, 0.2f, 0.2f);

        /// <summary>Scene instance names under Sandbox hierarchy.</summary>
        public const string SandboxFloorObjectName = "FloorPlane";
        public const string SandboxGrabbableObjectName = "GrabbableCube";
        public const string SandboxEnclosureName = "GreyboxEnclosure";
        public const string SandboxScaleReferenceName = "ScaleReference1m";
        public static readonly Vector3 SandboxGrabbableLocalPosition = new(0f, 1.1f, 0.75f);

        /// <summary>Half-width of 100×100 m floor along X/Z.</summary>
        public const float EnclosureHalfExtent = 50f;

        public const float EnclosureWallHeight = 10f;
        public const float EnclosureWallThickness = 0.2f;

        /// <summary>Checker tiling on 100×100 m floor (~5 m per cell at 20×20).</summary>
        public static readonly Vector2 FloorCheckerTiling = new(20f, 20f);
    }
}
