using System;
using Lingkyn.Inventory.UGUI;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Lingkyn.Inventory.XR.UGUI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup))]
    [RequireComponent(typeof(TrackedDeviceGraphicRaycaster))]
    public sealed class InventoryWorldSpaceSurface : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasScaler canvasScaler;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TrackedDeviceGraphicRaycaster trackedRaycaster;
        [SerializeField] private InventoryShellView shell;
        [SerializeField] private InventoryWorldSpaceProfile profile;

        public Canvas Canvas => canvas;
        public CanvasScaler CanvasScaler => canvasScaler;
        public CanvasGroup CanvasGroup => canvasGroup;
        public TrackedDeviceGraphicRaycaster TrackedRaycaster => trackedRaycaster;
        public InventoryShellView Shell => shell;
        public InventoryWorldSpaceProfile Profile => profile;
        public RectTransform RectTransform => (RectTransform)transform;
        public Camera EventCamera => canvas == null ? null : canvas.worldCamera;
        public InventoryXrValidationReport LastValidation { get; private set; }
        public bool InteractionEnabled => canvasGroup != null && canvasGroup.interactable &&
                                          canvasGroup.blocksRaycasts && trackedRaycaster != null &&
                                          trackedRaycaster.enabled;

        public void ApplyProfile()
        {
            EnsureBindings();
            profile.Validate();

            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform.sizeDelta = profile.ReferenceResolution;
            transform.localScale = Vector3.one * profile.MetersPerPixel;
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            canvasScaler.scaleFactor = 1f;
            canvasScaler.referencePixelsPerUnit = 100f;
            canvasScaler.dynamicPixelsPerUnit = profile.DynamicPixelsPerUnit;
            trackedRaycaster.ignoreReversedGraphics = profile.IgnoreReversedGraphics;
            trackedRaycaster.checkFor3DOcclusion = profile.CheckFor3DOcclusion;
        }

        public void BindEventCamera(Camera eventCamera)
        {
            if (eventCamera == null) throw new ArgumentNullException(nameof(eventCamera));
            EnsureBindings();
            canvas.worldCamera = eventCamera;
        }

        public void PlaceInFrontOf(Camera camera)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            EnsureBindings();
            profile.Validate();

            var forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(camera.transform.up, Vector3.up);
            }
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(camera.transform.right, Vector3.up);
            }
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();

            transform.SetParent(null, false);
            transform.localScale = Vector3.one * profile.MetersPerPixel;
            transform.position = camera.transform.position +
                                 forward * profile.DefaultDistanceMeters +
                                 Vector3.up * profile.DefaultVerticalOffsetMeters;
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        public void Prepare(Camera camera, bool placeAtDefaultWorldPose)
        {
            ApplyProfile();
            if (placeAtDefaultWorldPose) PlaceInFrontOf(camera);
            BindEventCamera(camera);
        }

        public InventoryXrValidationReport Revalidate()
        {
            LastValidation = InventoryXrSceneValidator.ValidateScene(this);
            SetInteractionGate(LastValidation.IsValid);
            return LastValidation;
        }

        public void ValidateSceneOrThrow() => Revalidate().ThrowIfInvalid();

        internal void SetInteractionGate(bool enabled)
        {
            if (canvasGroup != null)
            {
                canvasGroup.interactable = enabled;
                canvasGroup.blocksRaycasts = enabled;
            }

            if (trackedRaycaster != null) trackedRaycaster.enabled = enabled;
        }

        private void EnsureBindings()
        {
            if (canvas == null) throw new InvalidOperationException("InventoryWorldSpaceSurface requires its Canvas binding.");
            if (canvasScaler == null) throw new InvalidOperationException("InventoryWorldSpaceSurface requires its CanvasScaler binding.");
            if (canvasGroup == null) throw new InvalidOperationException("InventoryWorldSpaceSurface requires its CanvasGroup binding.");
            if (trackedRaycaster == null) throw new InvalidOperationException("InventoryWorldSpaceSurface requires its TrackedDeviceGraphicRaycaster binding.");
            if (shell == null) throw new InvalidOperationException("InventoryWorldSpaceSurface requires its nested InventoryShell binding.");
            if (profile == null) throw new InvalidOperationException("InventoryWorldSpaceSurface requires an InventoryWorldSpaceProfile.");
        }
    }
}
