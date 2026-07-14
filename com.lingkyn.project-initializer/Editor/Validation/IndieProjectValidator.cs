using System.IO;
using UnityEditor;
using UnityEngine;
using Lingkyn.Unity.ProjectInitializer.Editor.ConfigTools;

namespace Lingkyn.Unity.ProjectInitializer.Editor.Validation
{
    public static class IndieProjectValidator
    {
        public static ValidationReport ValidateIndieBaseline()
        {
            var report = new ValidationReport();
            ValidateRootEngineeringFiles(report);
            ValidateFolders(report);
            ValidateScenes(report);
            AsmdefDependencyValidator.Validate(report);
            ArchitectureAnchorValidator.Validate(report);
            NamespaceConventionValidator.Validate(report);
            ResourcesUsageValidator.Validate(report);
            MissingReferenceChecker.Validate(report);
            return report;
        }

        public static bool TryAutoFix(string issueCode)
        {
            switch (issueCode)
            {
                case "INIT_FOLDER_MISSING":
                    ProjectFolderScaffold.EnsureIndieDirectories();
                    return true;
                case "INIT_MARKER_MISSING":
                    CreateDefaultConfigs.CreateAll();
                    return true;
                default:
                    return false;
            }
        }

        static void ValidateRootEngineeringFiles(ValidationReport report)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            if (!File.Exists(Path.Combine(projectRoot, ".editorconfig")))
            {
                report.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Warning,
                    Code = "INIT_EDITORCONFIG_MISSING",
                    Message = "Missing .editorconfig at Unity project root.",
                });
            }

            if (!File.Exists(IndieDirectoryContract.ActivationMarker))
            {
                report.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Warning,
                    Code = "INIT_MARKER_MISSING",
                    Message = "Initializer marker is missing; build validation remains opt-out until initialization is run.",
                    AssetPath = IndieDirectoryContract.ActivationMarker,
                    AutoFixable = true,
                });
            }
        }

        static void ValidateFolders(ValidationReport report)
        {
            foreach (var folder in IndieDirectoryContract.RequiredFolders)
            {
                if (AssetDatabase.IsValidFolder(folder)) continue;
                report.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Code = "INIT_FOLDER_MISSING",
                    Message = $"Missing directory contract folder: {folder}",
                    AssetPath = folder,
                    SuggestedFix = "Tools/Lingkyn/Project Initializer/Initialize",
                    AutoFixable = true,
                });
            }
        }

        static void ValidateScenes(ValidationReport report)
        {
            foreach (var scenePath in IndieDirectoryContract.BaselineScenes)
            {
                if (!File.Exists(scenePath))
                {
                    report.Issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Error,
                        Code = "INIT_SCENE_MISSING",
                        Message = $"Missing baseline scene: {scenePath}",
                        AssetPath = scenePath,
                    });
                    continue;
                }

                ValidateSceneContract(scenePath, report);
            }
        }

        static void ValidateSceneContract(string scenePath, ValidationReport report)
        {
            SceneValidationScope.WithScene(scenePath, scene =>
            {
                GameObject sceneRoot = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.name == "Scene_Root") sceneRoot = root;
                }

                if (sceneRoot == null)
                {
                    report.Issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Error,
                        Code = "INIT_SCENE_ROOT_MISSING",
                        Message = $"Scene missing Scene_Root: {scenePath}",
                        ScenePath = scenePath,
                        SuggestedFix = "Tools/Lingkyn/Project Initializer/Setup Current Scene",
                        AutoFixable = true,
                    });
                    return;
                }

                if (scene.name is "MainMenu" or "Level_01" or "Sandbox")
                {
                    var lighting = sceneRoot.transform.Find("_Lighting");
                    if (lighting == null || lighting.GetComponentInChildren<Light>() == null)
                    {
                        report.Issues.Add(new ValidationIssue
                        {
                            Severity = ValidationSeverity.Warning,
                            Code = "INIT_SCENE_LIGHTING_MISSING",
                            Message = $"User-facing scene missing baseline light: {scenePath}",
                            ScenePath = scenePath,
                            SuggestedFix = "Tools/Lingkyn/Project Initializer/Setup Current Scene",
                            AutoFixable = true,
                        });
                    }
                }
            });
        }
    }
}
