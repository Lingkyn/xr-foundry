using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Lingkyn.Inventory.UGUI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Lingkyn.Inventory.XR.UGUI
{
    public enum InventoryXrIssueCode
    {
        MissingSurface,
        MissingProfile,
        InvalidProfile,
        MissingCanvas,
        CanvasNotWorldSpace,
        MissingCanvasScaler,
        MissingCanvasGroup,
        MissingTrackedRaycaster,
        ConventionalGraphicRaycasterPresent,
        MissingInventoryShell,
        InventoryShellOutsideSurface,
        EmbeddedCamera,
        EmbeddedEventSystem,
        EmbeddedXrOrigin,
        MissingEventCamera,
        HeadLockedSurface,
        MissingEventSystem,
        MultipleEventSystems,
        DisabledEventSystem,
        MissingInputModule,
        MultipleInputModules,
        IncompatibleInputModule,
        DisabledInputModule,
        DisabledXrInput,
        MissingUiInteractor,
        DisabledUiInteractor,
        UiInteractionDisabled,
        UiInteractorCannotReachSurface,
    }

    public sealed class InventoryXrValidationIssue
    {
        public InventoryXrValidationIssue(InventoryXrIssueCode code, string message)
        {
            Code = code;
            Message = message ?? string.Empty;
        }

        public InventoryXrIssueCode Code { get; }
        public string Message { get; }
    }

    public sealed class InventoryXrValidationReport
    {
        internal InventoryXrValidationReport(IEnumerable<InventoryXrValidationIssue> issues)
        {
            Issues = new ReadOnlyCollection<InventoryXrValidationIssue>((issues ?? Array.Empty<InventoryXrValidationIssue>()).ToArray());
        }

        public IReadOnlyList<InventoryXrValidationIssue> Issues { get; }
        public bool IsValid => Issues.Count == 0;
        public bool Has(InventoryXrIssueCode code) => Issues.Any(issue => issue.Code == code);

        public void ThrowIfInvalid()
        {
            if (IsValid) return;
            throw new InvalidOperationException(
                "Inventory XR scene validation failed: " +
                string.Join(" | ", Issues.Select(issue => $"{issue.Code}: {issue.Message}")));
        }
    }

    public static class InventoryXrSceneValidator
    {
        public static InventoryXrValidationReport ValidateSurface(InventoryWorldSpaceSurface surface)
        {
            var issues = new List<InventoryXrValidationIssue>();
            ValidateSurface(surface, issues);
            return new InventoryXrValidationReport(issues);
        }

        public static InventoryXrValidationReport ValidateScene(InventoryWorldSpaceSurface surface)
        {
            var eventSystems = UnityEngine.Object.FindObjectsByType<EventSystem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            return ValidateScene(surface, eventSystems, FindUiInteractors());
        }

        public static InventoryXrValidationReport ValidateScene(
            InventoryWorldSpaceSurface surface,
            IReadOnlyList<EventSystem> eventSystems)
        {
            return ValidateScene(surface, eventSystems, FindUiInteractors());
        }

        public static InventoryXrValidationReport ValidateScene(
            InventoryWorldSpaceSurface surface,
            IReadOnlyList<EventSystem> eventSystems,
            IReadOnlyList<MonoBehaviour> uiInteractors)
        {
            var issues = new List<InventoryXrValidationIssue>();
            ValidateSurface(surface, issues);

            if (surface != null)
            {
                if (surface.Canvas != null && surface.Canvas.worldCamera == null)
                {
                    Add(issues, InventoryXrIssueCode.MissingEventCamera,
                        "Bind the consumer XR camera to Canvas.worldCamera.");
                }

                if (surface.GetComponentInParent<Camera>() != null)
                {
                    Add(issues, InventoryXrIssueCode.HeadLockedSurface,
                        "The Inventory surface cannot be parented to a Camera; use a scene root or world anchor.");
                }
            }

            var systems = (eventSystems ?? Array.Empty<EventSystem>())
                .Where(item => item != null)
                .Distinct()
                .ToArray();
            if (systems.Length == 0)
            {
                Add(issues, InventoryXrIssueCode.MissingEventSystem,
                    "The consumer scene requires exactly one EventSystem with XRUIInputModule.");
            }
            else if (systems.Length > 1)
            {
                Add(issues, InventoryXrIssueCode.MultipleEventSystems,
                    $"Found {systems.Length} EventSystems; exactly one is supported.");
            }
            else
            {
                var eventSystem = systems[0];
                if (!eventSystem.isActiveAndEnabled)
                {
                    Add(issues, InventoryXrIssueCode.DisabledEventSystem,
                        "EventSystem must be active and enabled.");
                }

                var modules = eventSystem.GetComponents<BaseInputModule>();
                if (modules.Length == 0)
                {
                    Add(issues, InventoryXrIssueCode.MissingInputModule,
                        "The EventSystem requires one XRUIInputModule.");
                }
                else if (modules.Length > 1)
                {
                    Add(issues, InventoryXrIssueCode.MultipleInputModules,
                        $"Found {modules.Length} input modules; remove desktop or duplicate modules.");
                }
                else if (!(modules[0] is XRUIInputModule))
                {
                    Add(issues, InventoryXrIssueCode.IncompatibleInputModule,
                        $"Expected XRUIInputModule, found {modules[0].GetType().FullName}.");
                }
                else if (!modules[0].isActiveAndEnabled)
                {
                    Add(issues, InventoryXrIssueCode.DisabledInputModule,
                        "XRUIInputModule must be active and enabled.");
                }
                else if (!((XRUIInputModule)modules[0]).enableXRInput)
                {
                    Add(issues, InventoryXrIssueCode.DisabledXrInput,
                        "XRUIInputModule.enableXRInput must be enabled for tracked UI interaction.");
                }
            }

            ValidateUiInteractors(surface, uiInteractors, issues);

            return new InventoryXrValidationReport(issues);
        }

        private static MonoBehaviour[] FindUiInteractors() =>
            UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Where(component => component is IUIInteractor)
                .ToArray();

        private static void ValidateUiInteractors(
            InventoryWorldSpaceSurface surface,
            IReadOnlyList<MonoBehaviour> uiInteractors,
            ICollection<InventoryXrValidationIssue> issues)
        {
            var values = (uiInteractors ?? Array.Empty<MonoBehaviour>())
                .Where(component => component != null && component is IUIInteractor)
                .Distinct()
                .ToArray();
            if (values.Length == 0)
            {
                Add(issues, InventoryXrIssueCode.MissingUiInteractor,
                    "Add a consumer-owned XRI UI interactor such as XRRayInteractor, NearFarInteractor, or XRPokeInteractor.");
                return;
            }

            var active = values.Where(component => component.isActiveAndEnabled).ToArray();
            if (active.Length == 0)
            {
                Add(issues, InventoryXrIssueCode.DisabledUiInteractor,
                    "At least one XRI UI interactor must be active and enabled.");
                return;
            }

            var uiEnabled = active.Where(IsUiInteractionEnabled).ToArray();
            if (uiEnabled.Length == 0)
            {
                Add(issues, InventoryXrIssueCode.UiInteractionDisabled,
                    "At least one active XRI interactor must have UI interaction enabled.");
                return;
            }

            if (surface != null && surface.Canvas != null &&
                !uiEnabled.Any(component => CanReachSurface(component, surface)))
            {
                Add(issues, InventoryXrIssueCode.UiInteractorCannotReachSurface,
                    "No active UI interactor can reach the Inventory Canvas with its current layer mask and range.");
            }
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
                    // A custom component explicitly implementing IUIInteractor owns its
                    // own enablement contract; active-and-enabled is the public signal.
                    return true;
            }
        }

        private static bool CanReachSurface(MonoBehaviour component, InventoryWorldSpaceSurface surface)
        {
            if (!(component is XRRayInteractor ray)) return true;

            var layerBit = 1 << surface.Canvas.gameObject.layer;
            if ((ray.raycastMask.value & layerBit) == 0) return false;
            if (!PositiveFinite(ray.maxRaycastDistance)) return false;

            var corners = new Vector3[4];
            surface.RectTransform.GetWorldCorners(corners);
            var bounds = new Bounds(corners[0], Vector3.zero);
            for (var index = 1; index < corners.Length; index++) bounds.Encapsulate(corners[index]);
            var distance = Vector3.Distance(ray.transform.position, bounds.ClosestPoint(ray.transform.position));
            return distance <= ray.maxRaycastDistance + 0.01f;
        }

        private static bool PositiveFinite(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private static void ValidateSurface(
            InventoryWorldSpaceSurface surface,
            ICollection<InventoryXrValidationIssue> issues)
        {
            if (surface == null)
            {
                Add(issues, InventoryXrIssueCode.MissingSurface,
                    "Assign an InventoryWorldSpaceSurface.");
                return;
            }

            if (surface.Profile == null)
            {
                Add(issues, InventoryXrIssueCode.MissingProfile,
                    "Assign an InventoryWorldSpaceProfile.");
            }
            else
            {
                try
                {
                    surface.Profile.Validate();
                }
                catch (InvalidOperationException exception)
                {
                    Add(issues, InventoryXrIssueCode.InvalidProfile, exception.Message);
                }
            }

            if (surface.Canvas == null)
            {
                Add(issues, InventoryXrIssueCode.MissingCanvas, "Bind the world-space Canvas.");
            }
            else
            {
                if (surface.Canvas.renderMode != RenderMode.WorldSpace)
                {
                    Add(issues, InventoryXrIssueCode.CanvasNotWorldSpace,
                        "Inventory XR requires Canvas.renderMode = WorldSpace.");
                }

                if (surface.Canvas.gameObject.GetComponent<GraphicRaycaster>() != null)
                {
                    Add(issues, InventoryXrIssueCode.ConventionalGraphicRaycasterPresent,
                        "Remove GraphicRaycaster; use only TrackedDeviceGraphicRaycaster on the XR Canvas.");
                }
            }

            if (surface.CanvasScaler == null)
            {
                Add(issues, InventoryXrIssueCode.MissingCanvasScaler, "Bind the CanvasScaler.");
            }

            if (surface.CanvasGroup == null)
            {
                Add(issues, InventoryXrIssueCode.MissingCanvasGroup,
                    "Bind the fail-closed CanvasGroup.");
            }

            if (surface.TrackedRaycaster == null)
            {
                Add(issues, InventoryXrIssueCode.MissingTrackedRaycaster,
                    "Bind TrackedDeviceGraphicRaycaster.");
            }

            if (surface.Shell == null)
            {
                Add(issues, InventoryXrIssueCode.MissingInventoryShell,
                    "Bind the nested InventoryShell.");
            }
            else if (!surface.Shell.transform.IsChildOf(surface.transform))
            {
                Add(issues, InventoryXrIssueCode.InventoryShellOutsideSurface,
                    "InventoryShell must remain inside the world-space surface hierarchy.");
            }

            if (surface.GetComponentsInChildren<Camera>(true).Length > 0)
            {
                Add(issues, InventoryXrIssueCode.EmbeddedCamera,
                    "The reusable Inventory XR prefab cannot embed a Camera or XR rig.");
            }

            if (surface.GetComponentsInChildren<EventSystem>(true).Length > 0)
            {
                Add(issues, InventoryXrIssueCode.EmbeddedEventSystem,
                    "The reusable Inventory XR prefab cannot embed an EventSystem.");
            }

            var hasXrOrigin = surface.GetComponentsInChildren<Component>(true)
                .Any(component => component != null &&
                                  string.Equals(component.GetType().FullName,
                                      "Unity.XR.CoreUtils.XROrigin",
                                      StringComparison.Ordinal));
            if (hasXrOrigin)
            {
                Add(issues, InventoryXrIssueCode.EmbeddedXrOrigin,
                    "The reusable Inventory XR prefab cannot embed an XR Origin.");
            }
        }

        private static void Add(
            ICollection<InventoryXrValidationIssue> issues,
            InventoryXrIssueCode code,
            string message) => issues.Add(new InventoryXrValidationIssue(code, message));
    }
}
