using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Lingkyn.Inventory.Unity.Editor
{
    internal static class InventoryAuthoringEditorDiagnostics
    {
        public static string Format(InventoryAuthoringDiagnostic diagnostic)
        {
            var path = diagnostic.Source == null
                ? "<missing asset>"
                : AssetDatabase.GetAssetPath(diagnostic.Source);
            if (string.IsNullOrWhiteSpace(path))
            {
                path = diagnostic.Source == null ? "<missing asset>" : diagnostic.Source.name;
            }

            return $"{path} :: {diagnostic.FieldPath} :: {diagnostic.Code} :: {diagnostic.Message}";
        }

        public static void Draw(InventoryAuthoringReport report)
        {
            foreach (var diagnostic in report.Diagnostics)
            {
                EditorGUILayout.HelpBox(
                    Format(diagnostic),
                    diagnostic.Severity == InventoryAuthoringSeverity.Error
                        ? MessageType.Error
                        : MessageType.Warning);
            }
        }
    }

    [CustomEditor(typeof(ItemCatalogAsset))]
    internal sealed class ItemCatalogAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var report = InventoryAuthoringValidation.Validate((ItemCatalogAsset)target);
            InventoryAuthoringEditorDiagnostics.Draw(report);
        }
    }

    [CustomEditor(typeof(ItemDefinitionAsset))]
    internal sealed class ItemDefinitionAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var report = InventoryAuthoringValidation.Validate((ItemDefinitionAsset)target);
            InventoryAuthoringEditorDiagnostics.Draw(report);
        }
    }

    [CustomEditor(typeof(InventoryDefinitionAsset))]
    internal sealed class InventoryDefinitionAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var report = InventoryAuthoringValidation.Validate((InventoryDefinitionAsset)target);
            InventoryAuthoringEditorDiagnostics.Draw(report);
        }
    }

    [CustomEditor(typeof(ContainerDefinitionAsset))]
    internal sealed class ContainerDefinitionAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var report = InventoryAuthoringValidation.Validate((ContainerDefinitionAsset)target);
            InventoryAuthoringEditorDiagnostics.Draw(report);
        }
    }

    internal static class InventoryAuthoringValidationMenu
    {
        [MenuItem("Tools/XR Foundry/Inventory/Validate All Authoring Assets")]
        private static void ValidateAll()
        {
            var diagnostics = new List<InventoryAuthoringDiagnostic>();
            diagnostics.AddRange(Load<ItemCatalogAsset>().SelectMany(asset => InventoryAuthoringValidation.Validate(asset).Diagnostics));
            diagnostics.AddRange(Load<InventoryDefinitionAsset>().SelectMany(asset => InventoryAuthoringValidation.Validate(asset).Diagnostics));
            diagnostics.AddRange(Load<ItemDefinitionAsset>().SelectMany(asset => InventoryAuthoringValidation.Validate(asset).Diagnostics));
            diagnostics.AddRange(Load<ContainerDefinitionAsset>().SelectMany(asset => InventoryAuthoringValidation.Validate(asset).Diagnostics));

            var errors = diagnostics.Where(item => item.Severity == InventoryAuthoringSeverity.Error).ToArray();
            if (errors.Length == 0)
            {
                Debug.Log("Inventory authoring validation passed.");
                return;
            }

            foreach (var error in errors)
            {
                Debug.LogError(InventoryAuthoringEditorDiagnostics.Format(error), error.Source);
            }

            Debug.LogError($"Inventory authoring validation failed with {errors.Length} error(s).");
        }

        private static IEnumerable<T> Load<T>() where T : UnityEngine.Object =>
            AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(asset => asset != null);
    }
}
