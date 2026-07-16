using UnityEditor;

namespace Lingkyn.Unity.ProjectInitializer.Editor.Validation
{
    public static class AsmdefDependencyValidator
    {
        public static void Validate(ValidationReport report)
        {
            var projectAsmdefs = AssetDatabase.FindAssets("t:AssemblyDefinitionAsset", new[] { "Assets/_Project" });
            if (projectAsmdefs.Length > 0) return;
            report.Issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Info,
                Code = "INIT_ASMDEF_NOT_YET_DEFINED",
                Message = "No consumer assembly definitions exist under Assets/_Project yet; add them when runtime code is introduced.",
            });
        }
    }
}
