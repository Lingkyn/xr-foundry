using System.IO;
using UnityEngine;

namespace Lingkyn.Unity.ProjectInitializer.Editor.Validation
{
    public static class ResourcesUsageValidator
    {
        public static void Validate(ValidationReport report)
        {
            var resourcesRoot = Path.Combine(Application.dataPath, "Resources");
            if (!Directory.Exists(resourcesRoot)) return;

            foreach (var file in Directory.GetFiles(resourcesRoot, "*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".meta")) continue;
                var relative = "Assets" + file.Substring(Application.dataPath.Length).Replace('\\', '/');
                report.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Warning,
                    Code = "INIT_RESOURCES_USAGE_REQUIRES_REASON",
                    Message = $"Resources asset requires documented reason: {relative}",
                    AssetPath = relative,
                });
            }
        }
    }
}
