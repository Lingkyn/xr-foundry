using System;
using UnityEngine;

namespace Lingkyn.Unity.XrBaseline.Editor.SceneSetup
{
    /// <summary>
    /// Ensures HMD tracking is wired on the XR rig camera (vendor-neutral).
    /// </summary>
    public static class XrCameraTrackingRepair
    {
        const string TrackedPoseDriverTypeName =
            "UnityEngine.InputSystem.XR.TrackedPoseDriver, Unity.InputSystem";

        public static void Repair(GameObject rigRoot)
        {
            if (rigRoot == null) return;

            foreach (var camera in rigRoot.GetComponentsInChildren<Camera>(true))
            {
                camera.enabled = true;
                camera.stereoTargetEye = StereoTargetEyeMask.Both;
                EnsureTrackedPoseDriver(camera.gameObject);
            }

            foreach (var component in rigRoot.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component.GetType().Name != "XROrigin") continue;
                if (component is Behaviour behaviour) behaviour.enabled = true;
            }
        }

        public static bool HasValidHeadTracking(GameObject rigRoot)
        {
            if (rigRoot == null) return false;

            Camera headCamera = null;
            foreach (var camera in rigRoot.GetComponentsInChildren<Camera>(true))
            {
                if (!camera.enabled) continue;
                if (camera.CompareTag("MainCamera"))
                {
                    headCamera = camera;
                    break;
                }

                headCamera ??= camera;
            }

            if (headCamera == null) return false;

            var tpdType = Type.GetType(TrackedPoseDriverTypeName);
            if (tpdType == null) return true;

            var driver = headCamera.GetComponent(tpdType);
            return driver is Behaviour behaviour && behaviour.enabled;
        }

        static void EnsureTrackedPoseDriver(GameObject cameraObject)
        {
            var tpdType = Type.GetType(TrackedPoseDriverTypeName);
            if (tpdType == null) return;

            var driver = cameraObject.GetComponent(tpdType) ?? cameraObject.AddComponent(tpdType);
            if (driver is Behaviour behaviour) behaviour.enabled = true;
        }
    }
}
