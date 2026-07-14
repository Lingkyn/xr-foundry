using System.IO;
using UnityEngine;

namespace Lingkyn.Unity.ProjectInitializer.Editor.Validation
{
    public static class ArchitectureAnchorValidator
    {
        static readonly string[] AnchorFiles =
        {
            "ARCHITECTURE.md",
            "DEVELOPMENT_CONVENTIONS.md",
            "README.md",
        };

        public static void Validate(ValidationReport report)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            foreach (var file in AnchorFiles)
            {
                var path = Path.Combine(projectRoot, file);
                if (!File.Exists(path))
                {
                    report.Issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Error,
                        Code = "INIT_ANCHOR_FILE_MISSING",
                        Message = $"Missing anchor file: {file}",
                        AssetPath = file,
                    });
                }
            }
        }
    }
}
