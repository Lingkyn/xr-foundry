using UnityEditor;
using UnityEngine;

namespace Lingkyn.Inventory.XR.UGUI.Editor
{
    [CustomEditor(typeof(InventoryWorldSpaceSurface))]
    public sealed class InventoryWorldSpaceSurfaceEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var surface = (InventoryWorldSpaceSurface)target;
            EditorGUILayout.Space();
            if (GUILayout.Button("Validate Inventory XR Scene"))
            {
                var report = surface.Revalidate();
                if (report.IsValid)
                {
                    Debug.Log("Inventory XR scene validation passed; local surface interaction is open.", surface);
                }
                else
                {
                    foreach (var issue in report.Issues)
                    {
                        Debug.LogError($"{issue.Code}: {issue.Message}", surface);
                    }
                }
            }
        }
    }
}
