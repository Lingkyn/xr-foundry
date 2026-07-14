using UnityEditor;
using UnityEngine;

namespace Lingkyn.Unity.ProjectInitializer.Editor.Validation
{
    public static class MissingReferenceChecker
    {
        public static void Validate(ValidationReport report)
        {
            var guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/_Project/Data/Config" });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
                if (asset == null)
                {
                    report.Issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Error,
                        Code = "INIT_MISSING_REFERENCE",
                        Message = $"Missing config asset reference: {path}",
                        AssetPath = path,
                    });
                }
            }
        }
    }
}
