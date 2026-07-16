using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Lingkyn.Inventory.UIToolkit;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Lingkyn.Inventory.XR.UIToolkit
{
    public enum InventoryUIToolkitIssueCode
    {
        UnsupportedUnityVersion,
        MissingSurface,
        MissingProfile,
        InvalidProfile,
        MissingDocument,
        DocumentOutsideSurface,
        MissingVisualTree,
        MissingInventoryView,
        InventoryViewOutsideSurface,
        InventoryViewNotBound,
        MissingCollider,
        ColliderOutsideSurface,
        ColliderNotTrigger,
        InvalidColliderGeometry,
        ColliderCenterMismatch,
        ColliderSizeMismatch,
        PanelSettingsMismatch,
        PanelNotWorldSpace,
        PanelColliderUpdateModeMismatch,
        PanelColliderTriggerDisabled,
        DocumentNotFixedSize,
        DocumentSizeMismatch,
        EmbeddedCamera,
        EmbeddedEventSystem,
        EmbeddedXrOrigin,
        EmbeddedXrUiToolkitManager,
        HeadLockedSurface,
        MissingXrUiToolkitManager,
        MultipleXrUiToolkitManagers,
        DisabledXrUiToolkitManager,
        MissingUiInteractor,
        DisabledUiInteractor,
        UiInteractionDisabled,
        UiInteractorCannotReachSurface,
        MultipleEventSystems,
        DisabledEventSystem,
        MissingPanelInputConfiguration,
        MultiplePanelInputConfigurations,
        DisabledPanelInputConfiguration,
        InvalidPanelInputRedirection,
        WorldSpaceInputDisabled,
        InteractionLayerMismatch,
        InvalidMaxInteractionDistance,
        PanelInputCannotReachSurface,
        UiToolkitEventsBypassed,
    }

    public sealed class InventoryUIToolkitValidationIssue
    {
        public InventoryUIToolkitValidationIssue(InventoryUIToolkitIssueCode code, string message)
        {
            Code = code;
            Message = message ?? string.Empty;
        }

        public InventoryUIToolkitIssueCode Code { get; }
        public string Message { get; }
    }

    public sealed class InventoryUIToolkitValidationReport
    {
        internal InventoryUIToolkitValidationReport(IEnumerable<InventoryUIToolkitValidationIssue> issues)
        {
            Issues = new ReadOnlyCollection<InventoryUIToolkitValidationIssue>(
                (issues ?? Array.Empty<InventoryUIToolkitValidationIssue>()).ToArray());
        }

        public IReadOnlyList<InventoryUIToolkitValidationIssue> Issues { get; }
        public bool IsValid => Issues.Count == 0;
        public bool Has(InventoryUIToolkitIssueCode code) => Issues.Any(issue => issue.Code == code);

        public void ThrowIfInvalid()
        {
            if (IsValid) return;
            throw new InvalidOperationException(
                "Inventory XR UI Toolkit validation failed: " +
                string.Join(" | ", Issues.Select(issue => $"{issue.Code}: {issue.Message}")));
        }
    }

    public static class InventoryUIToolkitSceneValidator
    {
        public static InventoryUIToolkitValidationReport ValidateSurface(
            InventoryUIToolkitWorldSpaceSurface surface)
        {
            var issues = new List<InventoryUIToolkitValidationIssue>();
            ValidateSurface(surface, issues);
            return new InventoryUIToolkitValidationReport(issues);
        }

        public static InventoryUIToolkitValidationReport ValidateScene(
            InventoryUIToolkitWorldSpaceSurface surface)
        {
            return ValidateScene(
                surface,
                UnityEngine.Object.FindObjectsByType<XRUIToolkitManager>(FindObjectsInactive.Include, FindObjectsSortMode.None),
                UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None),
                UnityEngine.Object.FindObjectsByType<PanelInputConfiguration>(FindObjectsInactive.Include, FindObjectsSortMode.None),
                UnityEngine.Object.FindObjectsByType<UIInputModule>(FindObjectsInactive.Include, FindObjectsSortMode.None),
                FindUiInteractors());
        }

        public static InventoryUIToolkitValidationReport ValidateScene(
            InventoryUIToolkitWorldSpaceSurface surface,
            IReadOnlyList<XRUIToolkitManager> managers,
            IReadOnlyList<EventSystem> eventSystems,
            IReadOnlyList<PanelInputConfiguration> panelInputConfigurations,
            IReadOnlyList<UIInputModule> uiInputModules,
            IReadOnlyList<MonoBehaviour> uiInteractors)
        {
            var issues = new List<InventoryUIToolkitValidationIssue>();
            ValidateSurface(surface, issues);

            if (surface != null && surface.GetComponentInParent<Camera>() != null)
            {
                Add(issues, InventoryUIToolkitIssueCode.HeadLockedSurface,
                    "The Inventory UI Toolkit surface cannot be parented to a Camera; use a world root or anchor.");
            }

            var activeManagers = Distinct(managers);
            if (activeManagers.Length == 0)
            {
                Add(issues, InventoryUIToolkitIssueCode.MissingXrUiToolkitManager,
                    "The consumer scene requires exactly one active XRUIToolkitManager.");
            }
            else if (activeManagers.Length > 1)
            {
                Add(issues, InventoryUIToolkitIssueCode.MultipleXrUiToolkitManagers,
                    $"Found {activeManagers.Length} XRUIToolkitManager components; keep exactly one.");
            }
            else if (!activeManagers[0].isActiveAndEnabled)
            {
                Add(issues, InventoryUIToolkitIssueCode.DisabledXrUiToolkitManager,
                    "XRUIToolkitManager must be active and enabled.");
            }

            var activeUiInteractors = ValidateUiInteractors(surface, uiInteractors, issues);

            var systems = Distinct(eventSystems);
            if (systems.Length > 1)
            {
                Add(issues, InventoryUIToolkitIssueCode.MultipleEventSystems,
                    $"Found {systems.Length} EventSystems; keep at most one for deterministic UI interoperability.");
            }
            else if (systems.Length == 1 && !systems[0].isActiveAndEnabled)
            {
                Add(issues, InventoryUIToolkitIssueCode.DisabledEventSystem,
                    "The scene EventSystem is disabled; remove it or enable and configure it.");
            }

            if (systems.Length > 0)
            {
                ValidatePanelInputConfiguration(
                    surface,
                    panelInputConfigurations,
                    activeUiInteractors,
                    issues);
                foreach (var module in Distinct(uiInputModules))
                {
                    if (module.bypassUIToolkitEvents)
                    {
                        Add(issues, InventoryUIToolkitIssueCode.UiToolkitEventsBypassed,
                            $"{module.GetType().Name}.bypassUIToolkitEvents must be false when UI Toolkit is active.");
                    }
                }
            }

            return new InventoryUIToolkitValidationReport(issues);
        }

        public static bool IsSupportedUnityVersion(string unityVersion)
        {
            if (string.IsNullOrWhiteSpace(unityVersion)) return false;
            var parts = unityVersion.Split('.');
            if (parts.Length < 3 || !TryLeadingNumber(parts[0], out var major) ||
                !TryLeadingNumber(parts[1], out var minor) || !TryLeadingNumber(parts[2], out var patch))
            {
                return false;
            }
            if (major != 6000) return major > 6000;
            if (minor != 3) return minor > 3;
            return patch >= 8;
        }

        private static void ValidateSurface(
            InventoryUIToolkitWorldSpaceSurface surface,
            ICollection<InventoryUIToolkitValidationIssue> issues)
        {
            if (!IsSupportedUnityVersion(Application.unityVersion))
            {
                Add(issues, InventoryUIToolkitIssueCode.UnsupportedUnityVersion,
                    "World-space Inventory UI Toolkit requires Unity 6000.3.8f1 or newer on the 6000.3 line.");
            }
            if (surface == null)
            {
                Add(issues, InventoryUIToolkitIssueCode.MissingSurface,
                    "Assign an InventoryUIToolkitWorldSpaceSurface.");
                return;
            }

            var profileIsValid = false;
            if (surface.Profile == null)
            {
                Add(issues, InventoryUIToolkitIssueCode.MissingProfile,
                    "Assign an InventoryUIToolkitWorldSpaceProfile.");
            }
            else
            {
                try
                {
                    surface.Profile.Validate();
                    profileIsValid = true;
                }
                catch (InvalidOperationException exception)
                {
                    Add(issues, InventoryUIToolkitIssueCode.InvalidProfile, exception.Message);
                }
            }

            if (surface.Document == null)
            {
                Add(issues, InventoryUIToolkitIssueCode.MissingDocument, "Bind UIDocument.");
            }
            else
            {
                if (surface.Document.gameObject != surface.gameObject)
                {
                    Add(issues, InventoryUIToolkitIssueCode.DocumentOutsideSurface,
                        "UIDocument must be on the same GameObject as InventoryUIToolkitWorldSpaceSurface.");
                }
                if (surface.Document.visualTreeAsset == null)
                {
                    Add(issues, InventoryUIToolkitIssueCode.MissingVisualTree,
                        "UIDocument requires the Inventory VisualTreeAsset or a compatible replacement.");
                }
                if (surface.Profile != null && surface.Document.panelSettings != surface.Profile.PanelSettings)
                {
                    Add(issues, InventoryUIToolkitIssueCode.PanelSettingsMismatch,
                        "UIDocument PanelSettings must match the assigned world-space profile.");
                }
                if (surface.Document.panelSettings == null ||
                    surface.Document.panelSettings.renderMode != PanelRenderMode.WorldSpace)
                {
                    Add(issues, InventoryUIToolkitIssueCode.PanelNotWorldSpace,
                        "UIDocument PanelSettings must use WorldSpace render mode.");
                }
                if (surface.Document.panelSettings != null &&
                    !InventoryUIToolkitWorldSpaceProfile.PanelKeepsExplicitCollider(surface.Document.panelSettings))
                {
                    Add(issues, InventoryUIToolkitIssueCode.PanelColliderUpdateModeMismatch,
                        "World-space PanelSettings.colliderUpdateMode must be Keep for the explicit surface collider.");
                }
                if (surface.Document.panelSettings != null &&
                    !InventoryUIToolkitWorldSpaceProfile.PanelKeepsColliderAsTrigger(surface.Document.panelSettings))
                {
                    Add(issues, InventoryUIToolkitIssueCode.PanelColliderTriggerDisabled,
                        "World-space PanelSettings.colliderIsTrigger must be enabled.");
                }
                if (surface.Document.worldSpaceSizeMode != UIDocument.WorldSpaceSizeMode.Fixed)
                {
                    Add(issues, InventoryUIToolkitIssueCode.DocumentNotFixedSize,
                        "Inventory world-space UIDocument must use Fixed size mode.");
                }
                if (surface.Profile != null &&
                    Vector2.Distance(surface.Document.worldSpaceSize, surface.Profile.ReferenceResolution) > 0.01f)
                {
                    Add(issues, InventoryUIToolkitIssueCode.DocumentSizeMismatch,
                        "UIDocument world-space size must match the assigned profile reference resolution.");
                }
            }

            if (surface.InventoryView == null)
            {
                Add(issues, InventoryUIToolkitIssueCode.MissingInventoryView,
                    "Bind InventoryDocumentView.");
            }
            else if (surface.InventoryView.gameObject != surface.gameObject)
            {
                Add(issues, InventoryUIToolkitIssueCode.InventoryViewOutsideSurface,
                    "InventoryDocumentView must be on the same GameObject as InventoryUIToolkitWorldSpaceSurface.");
            }
            else if (!surface.InventoryView.IsBound)
            {
                Add(issues, InventoryUIToolkitIssueCode.InventoryViewNotBound,
                    "InventoryDocumentView must bind the UIDocument named-element contract before XR interaction opens.");
            }
            if (surface.WorldSpaceCollider == null)
            {
                Add(issues, InventoryUIToolkitIssueCode.MissingCollider,
                    "Bind an explicit BoxCollider for UI Toolkit world-space picking.");
            }
            else
            {
                if (surface.WorldSpaceCollider.gameObject != surface.gameObject)
                {
                    Add(issues, InventoryUIToolkitIssueCode.ColliderOutsideSurface,
                        "BoxCollider must be on the same GameObject as InventoryUIToolkitWorldSpaceSurface.");
                }
                if (!surface.WorldSpaceCollider.isTrigger)
                {
                    Add(issues, InventoryUIToolkitIssueCode.ColliderNotTrigger,
                        "The explicit world-space BoxCollider must be a trigger.");
                }
                if (!Finite(surface.WorldSpaceCollider.center) || !PositiveFinite(surface.WorldSpaceCollider.size))
                {
                    Add(issues, InventoryUIToolkitIssueCode.InvalidColliderGeometry,
                        "BoxCollider center must be finite and its size must be finite and positive on every axis.");
                }
                if (!Approximately(surface.WorldSpaceCollider.center, Vector3.zero))
                {
                    Add(issues, InventoryUIToolkitIssueCode.ColliderCenterMismatch,
                        "BoxCollider center must exactly match the profile-applied center (0, 0, 0).");
                }
                if (profileIsValid &&
                    !Approximately(surface.WorldSpaceCollider.size, surface.Profile.ColliderSizeLocal))
                {
                    Add(issues, InventoryUIToolkitIssueCode.ColliderSizeMismatch,
                        "BoxCollider size must match the profile-derived local size on every axis.");
                }
            }

            if (surface.GetComponentsInChildren<Camera>(true).Length > 0)
            {
                Add(issues, InventoryUIToolkitIssueCode.EmbeddedCamera,
                    "The reusable Inventory surface cannot embed a Camera or XR rig.");
            }
            if (surface.GetComponentsInChildren<EventSystem>(true).Length > 0)
            {
                Add(issues, InventoryUIToolkitIssueCode.EmbeddedEventSystem,
                    "The reusable Inventory surface cannot embed an EventSystem.");
            }
            if (surface.GetComponentsInChildren<XRUIToolkitManager>(true).Length > 0)
            {
                Add(issues, InventoryUIToolkitIssueCode.EmbeddedXrUiToolkitManager,
                    "XRUIToolkitManager is a consumer-scene singleton and cannot be embedded in the surface.");
            }
            if (surface.GetComponentsInChildren<Component>(true).Any(component =>
                    component != null && string.Equals(component.GetType().FullName,
                        "Unity.XR.CoreUtils.XROrigin", StringComparison.Ordinal)))
            {
                Add(issues, InventoryUIToolkitIssueCode.EmbeddedXrOrigin,
                    "The reusable Inventory surface cannot embed an XR Origin.");
            }
        }

        private static MonoBehaviour[] FindUiInteractors() =>
            UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Where(component => component is IUIInteractor && component is IXRInteractor)
                .ToArray();

        private static MonoBehaviour[] ValidateUiInteractors(
            InventoryUIToolkitWorldSpaceSurface surface,
            IReadOnlyList<MonoBehaviour> uiInteractors,
            ICollection<InventoryUIToolkitValidationIssue> issues)
        {
            var values = (uiInteractors ?? Array.Empty<MonoBehaviour>())
                .Where(component => component != null && component is IUIInteractor && component is IXRInteractor)
                .Distinct()
                .ToArray();
            if (values.Length == 0)
            {
                Add(issues, InventoryUIToolkitIssueCode.MissingUiInteractor,
                    "Add a consumer-owned XRI UI interactor such as XRRayInteractor, NearFarInteractor, or XRPokeInteractor.");
                return Array.Empty<MonoBehaviour>();
            }

            var active = values.Where(component => component.isActiveAndEnabled).ToArray();
            if (active.Length == 0)
            {
                Add(issues, InventoryUIToolkitIssueCode.DisabledUiInteractor,
                    "At least one real XRI UI interactor must be active and enabled.");
                return Array.Empty<MonoBehaviour>();
            }

            var uiEnabled = active.Where(IsUiInteractionEnabled).ToArray();
            if (uiEnabled.Length == 0)
            {
                Add(issues, InventoryUIToolkitIssueCode.UiInteractionDisabled,
                    "At least one active XRI interactor must have UI interaction enabled.");
                return Array.Empty<MonoBehaviour>();
            }

            if (surface != null && surface.WorldSpaceCollider != null &&
                !uiEnabled.Any(component => CanInteractorReachSurface(component, surface.WorldSpaceCollider)))
            {
                Add(issues, InventoryUIToolkitIssueCode.UiInteractorCannotReachSurface,
                    "No active UI interactor can reach the Inventory collider with its current layer mask and range.");
            }

            return uiEnabled;
        }

        private static bool IsUiInteractionEnabled(MonoBehaviour component)
        {
            switch (component)
            {
                case XRRayInteractor ray:
                    return ray.enableUIInteraction;
                case NearFarInteractor nearFar:
                    return nearFar.enableUIInteraction;
                case XRPokeInteractor poke:
                    return poke.enableUIInteraction;
                default:
                    // A custom active IXRInteractor + IUIInteractor owns its public
                    // enablement contract; known XRI interactors are checked explicitly.
                    return true;
            }
        }

        private static bool CanInteractorReachSurface(MonoBehaviour component, BoxCollider collider)
        {
            if (!(component is XRRayInteractor ray)) return true;

            var layerBit = 1 << collider.gameObject.layer;
            if ((ray.raycastMask.value & layerBit) == 0 || !PositiveFinite(ray.maxRaycastDistance))
            {
                return false;
            }

            var origin = ray.rayOriginTransform != null ? ray.rayOriginTransform : ray.transform;
            return DistanceToColliderBounds(origin.position, collider) <= ray.maxRaycastDistance + 0.001f;
        }

        private static void ValidatePanelInputConfiguration(
            InventoryUIToolkitWorldSpaceSurface surface,
            IReadOnlyList<PanelInputConfiguration> configurations,
            IReadOnlyList<MonoBehaviour> activeUiInteractors,
            ICollection<InventoryUIToolkitValidationIssue> issues)
        {
            var values = Distinct(configurations);
            if (values.Length == 0)
            {
                Add(issues, InventoryUIToolkitIssueCode.MissingPanelInputConfiguration,
                    "An EventSystem is present; add PanelInputConfiguration with redirection set to Never.");
                return;
            }
            if (values.Length > 1)
            {
                Add(issues, InventoryUIToolkitIssueCode.MultiplePanelInputConfigurations,
                    $"Found {values.Length} PanelInputConfiguration components; keep exactly one.");
                return;
            }

            var configuration = values[0];
            if (!configuration.isActiveAndEnabled)
            {
                Add(issues, InventoryUIToolkitIssueCode.DisabledPanelInputConfiguration,
                    "PanelInputConfiguration must be active and enabled.");
            }
            if (configuration.panelInputRedirection != PanelInputConfiguration.PanelInputRedirection.Never)
            {
                Add(issues, InventoryUIToolkitIssueCode.InvalidPanelInputRedirection,
                    "PanelInputConfiguration must use No input redirection (Never) for XRI UI Toolkit.");
            }
            if (!configuration.processWorldSpaceInput)
            {
                Add(issues, InventoryUIToolkitIssueCode.WorldSpaceInputDisabled,
                    "PanelInputConfiguration must process world-space input.");
            }

            if (surface == null || surface.WorldSpaceCollider == null) return;

            var layerBit = 1 << surface.WorldSpaceCollider.gameObject.layer;
            if ((configuration.interactionLayers.value & layerBit) == 0)
            {
                Add(issues, InventoryUIToolkitIssueCode.InteractionLayerMismatch,
                    "PanelInputConfiguration.interactionLayers must include the Inventory surface layer.");
            }

            if (!PositiveFinite(configuration.maxInteractionDistance))
            {
                Add(issues, InventoryUIToolkitIssueCode.InvalidMaxInteractionDistance,
                    "PanelInputConfiguration.maxInteractionDistance must be finite and positive.");
            }
            else if ((activeUiInteractors ?? Array.Empty<MonoBehaviour>()).Count > 0 &&
                     !(activeUiInteractors ?? Array.Empty<MonoBehaviour>()).Any(component =>
                         DistanceToColliderBounds(InteractorOrigin(component).position, surface.WorldSpaceCollider) <=
                         configuration.maxInteractionDistance + 0.001f))
            {
                Add(issues, InventoryUIToolkitIssueCode.PanelInputCannotReachSurface,
                    "PanelInputConfiguration.maxInteractionDistance cannot reach the Inventory collider from any active UI interactor.");
            }
        }

        private static Transform InteractorOrigin(MonoBehaviour component)
        {
            var ray = component as XRRayInteractor;
            return ray != null && ray.rayOriginTransform != null ? ray.rayOriginTransform : component.transform;
        }

        private static float DistanceToColliderBounds(Vector3 worldPoint, BoxCollider collider)
        {
            if (collider == null || !Finite(collider.center) || !PositiveFinite(collider.size))
            {
                return float.PositiveInfinity;
            }

            var relative = collider.transform.InverseTransformPoint(worldPoint) - collider.center;
            var half = collider.size * 0.5f;
            var closestRelative = new Vector3(
                Mathf.Clamp(relative.x, -half.x, half.x),
                Mathf.Clamp(relative.y, -half.y, half.y),
                Mathf.Clamp(relative.z, -half.z, half.z));
            var closestWorld = collider.transform.TransformPoint(collider.center + closestRelative);
            return Vector3.Distance(worldPoint, closestWorld);
        }

        private static T[] Distinct<T>(IReadOnlyList<T> values) where T : UnityEngine.Object =>
            (values ?? Array.Empty<T>()).Where(value => value != null).Distinct().ToArray();

        private static bool TryLeadingNumber(string value, out int number)
        {
            number = 0;
            var count = 0;
            while (count < value.Length && char.IsDigit(value[count])) count++;
            return count > 0 && int.TryParse(value.Substring(0, count), out number);
        }

        private static bool Approximately(Vector3 left, Vector3 right) =>
            (left - right).sqrMagnitude <= 0.00000001f;

        private static bool PositiveFinite(float value) => Finite(value) && value > 0f;
        private static bool PositiveFinite(Vector3 value) =>
            PositiveFinite(value.x) && PositiveFinite(value.y) && PositiveFinite(value.z);
        private static bool Finite(Vector3 value) =>
            Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static void Add(
            ICollection<InventoryUIToolkitValidationIssue> issues,
            InventoryUIToolkitIssueCode code,
            string message) => issues.Add(new InventoryUIToolkitValidationIssue(code, message));
    }
}
