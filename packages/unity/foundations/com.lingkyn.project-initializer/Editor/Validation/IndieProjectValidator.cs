using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Lingkyn.Unity.ProjectInitializer.Editor.ConfigTools;

namespace Lingkyn.Unity.ProjectInitializer.Editor.Validation
{
    public static class IndieProjectValidator
    {
        /// <summary>
        /// Validates the whole indie baseline: root engineering files, the directory contract under
        /// <see cref="IndieDirectoryContract.ProjectRoot"/>, and the project-wide rules.
        /// </summary>
        public static ValidationReport ValidateIndieBaseline()
        {
            var report = new ValidationReport();
            ValidateRootEngineeringFiles(report);
            ValidateDirectoryContract(report, IndieDirectoryContract.ProjectRoot);
            AsmdefDependencyValidator.Validate(report);
            ArchitectureAnchorValidator.Validate(report);
            NamespaceConventionValidator.Validate(report);
            ResourcesUsageValidator.Validate(report);
            MissingReferenceChecker.Validate(report);
            return report;
        }

        /// <summary>
        /// Validates only the directory contract as it would apply under <paramref name="projectRoot"/>:
        /// the activation marker (<c>INIT_MARKER_MISSING</c>, auto-fixable), every required folder
        /// (<c>INIT_FOLDER_MISSING</c>, auto-fixable), and every baseline scene
        /// (<c>INIT_SCENE_MISSING</c>, plus the scene-content checks for scenes that exist).
        /// Project-wide rules and the <c>.editorconfig</c> check are not part of this overload.
        /// </summary>
        public static ValidationReport ValidateIndieBaseline(string projectRoot)
        {
            var report = new ValidationReport();
            ValidateDirectoryContract(report, projectRoot);
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
            var unityProjectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            if (!File.Exists(Path.Combine(unityProjectRoot, ".editorconfig")))
            {
                report.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Warning,
                    Code = "INIT_EDITORCONFIG_MISSING",
                    Message = "Missing .editorconfig at Unity project root.",
                });
            }
        }

        static void ValidateDirectoryContract(ValidationReport report, string projectRoot)
        {
            ValidateMarker(report, IndieDirectoryContract.ActivationMarkerUnder(projectRoot));
            ValidateFolders(report, IndieDirectoryContract.RequiredFoldersUnder(projectRoot));
            ValidateScenes(report, IndieDirectoryContract.BaselineScenesUnder(projectRoot));
        }

        static void ValidateMarker(ValidationReport report, string markerPath)
        {
            if (File.Exists(markerPath)) return;
            report.Issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Warning,
                Code = "INIT_MARKER_MISSING",
                Message = "Initializer marker is missing; build validation remains opt-out until initialization is run.",
                AssetPath = markerPath,
                AutoFixable = true,
            });
        }

        static void ValidateFolders(ValidationReport report, IEnumerable<string> requiredFolders)
        {
            foreach (var folder in requiredFolders)
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

        static void ValidateScenes(ValidationReport report, IEnumerable<string> baselineScenes)
        {
            foreach (var scenePath in baselineScenes)
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
