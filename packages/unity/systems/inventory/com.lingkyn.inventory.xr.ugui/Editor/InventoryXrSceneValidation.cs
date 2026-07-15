using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Lingkyn.Inventory.XR.UGUI.Editor
{
    public static class InventoryXrSceneValidation
    {
        [MenuItem("Tools/XR Foundry/Inventory/Validate XR Surfaces In Open Scenes")]
        public static void ValidateOpenScenes()
        {
            var surfaces = UnityEngine.Object.FindObjectsByType<InventoryWorldSpaceSurface>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (surfaces.Length == 0)
            {
                throw new InvalidOperationException("No InventoryWorldSpaceSurface exists in the loaded scenes.");
            }

            var failures = surfaces
                .Select(surface => new { Surface = surface, Report = surface.Revalidate() })
                .Where(item => !item.Report.IsValid)
                .ToArray();
            if (failures.Length > 0)
            {
                var details = failures.SelectMany(item => item.Report.Issues.Select(issue =>
                    $"{item.Surface.name}/{issue.Code}: {issue.Message}"));
                throw new InvalidOperationException("Inventory XR validation failed: " + string.Join(" | ", details));
            }

            Debug.Log($"Inventory XR validation passed for {surfaces.Length} surface(s).");
        }
    }
}
