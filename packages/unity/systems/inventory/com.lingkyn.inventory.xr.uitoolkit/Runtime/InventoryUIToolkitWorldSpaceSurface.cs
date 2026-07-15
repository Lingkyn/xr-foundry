using System;
using Lingkyn.Inventory.UIToolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lingkyn.Inventory.XR.UIToolkit
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument), typeof(BoxCollider), typeof(InventoryDocumentView))]
    public sealed class InventoryUIToolkitWorldSpaceSurface : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private InventoryDocumentView inventoryView;
        [SerializeField] private BoxCollider worldSpaceCollider;
        [SerializeField] private InventoryUIToolkitWorldSpaceProfile profile;

        public UIDocument Document => document;
        public InventoryDocumentView InventoryView => inventoryView;
        public BoxCollider WorldSpaceCollider => worldSpaceCollider;
        public InventoryUIToolkitWorldSpaceProfile Profile => profile;
        public InventoryUIToolkitValidationReport LastValidation { get; private set; }
        public Vector3 FacingNormal => -transform.forward;
        public bool InteractionEnabled => inventoryView != null && inventoryView.InteractionEnabled &&
                                          worldSpaceCollider != null && worldSpaceCollider.enabled;

        private void Reset() => BindComponents();

        private void Awake()
        {
            BindComponents();
            SetInteractionGate(false);
        }

        public void Configure(
            InventoryUIToolkitWorldSpaceProfile configuration,
            UIDocument targetDocument = null,
            InventoryDocumentView targetView = null,
            BoxCollider targetCollider = null)
        {
            profile = configuration != null
                ? configuration
                : throw new ArgumentNullException(nameof(configuration));
            document = targetDocument != null ? targetDocument : GetComponent<UIDocument>();
            inventoryView = targetView != null ? targetView : GetComponent<InventoryDocumentView>();
            worldSpaceCollider = targetCollider != null ? targetCollider : GetComponent<BoxCollider>();
            SetInteractionGate(false);
        }

        public void ApplyProfile()
        {
            EnsureBindings();
            profile.Validate();

            document.panelSettings = profile.PanelSettings;
            document.worldSpaceSizeMode = UIDocument.WorldSpaceSizeMode.Fixed;
            document.worldSpaceSize = profile.ReferenceResolution;
            transform.localScale = Vector3.one * profile.LocalScale;

            worldSpaceCollider.isTrigger = true;
            worldSpaceCollider.center = Vector3.zero;
            worldSpaceCollider.size = profile.ColliderSizeLocal;
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
            transform.localScale = Vector3.one * profile.LocalScale;
            transform.position = camera.transform.position +
                                 forward * profile.DefaultDistanceMeters +
                                 Vector3.up * profile.DefaultVerticalOffsetMeters;
            // A world-space UIDocument presents its front on -Transform.forward,
            // matching Unity's XRI World Space UI sample orientation.
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        public InventoryUIToolkitValidationReport Revalidate()
        {
            LastValidation = InventoryUIToolkitSceneValidator.ValidateScene(this);
            SetInteractionGate(LastValidation.IsValid);
            return LastValidation;
        }

        public void ValidateSceneOrThrow() => Revalidate().ThrowIfInvalid();

        internal void SetInteractionGate(bool enabled)
        {
            if (inventoryView != null) inventoryView.SetInteractionEnabled(enabled);
            if (worldSpaceCollider != null) worldSpaceCollider.enabled = enabled;
        }

        private void BindComponents()
        {
            if (document == null) document = GetComponent<UIDocument>();
            if (inventoryView == null) inventoryView = GetComponent<InventoryDocumentView>();
            if (worldSpaceCollider == null) worldSpaceCollider = GetComponent<BoxCollider>();
        }

        private void EnsureBindings()
        {
            BindComponents();
            if (document == null) throw new InvalidOperationException("Inventory XR UI Toolkit surface requires UIDocument.");
            if (inventoryView == null) throw new InvalidOperationException("Inventory XR UI Toolkit surface requires InventoryDocumentView.");
            if (worldSpaceCollider == null) throw new InvalidOperationException("Inventory XR UI Toolkit surface requires BoxCollider.");
            if (profile == null) throw new InvalidOperationException("Inventory XR UI Toolkit surface requires a profile.");
        }
    }
}
