using System.Linq;
using NUnit.Framework;
using UnityEditor;
using Lingkyn.Unity.ProjectInitializer.Editor.ConfigTools;
using Lingkyn.Unity.ProjectInitializer.Editor.Validation;

namespace Lingkyn.Unity.ProjectInitializer.Tests
{
    public sealed class IndieProjectValidatorTests
    {
        [Test]
        public void ValidateReportsMissingFoldersScenesAndMarkerWithStableCodes()
        {
            var root = DisposableProjectRoot.Create();
            try
            {
                Assert.That(AssetDatabase.IsValidFolder(root), Is.True, "The disposable root must exist and be empty.");

                var report = IndieProjectValidator.ValidateIndieBaseline(root);

                var codes = report.Issues.Select(issue => issue.Code).Distinct().ToArray();
                Assert.That(codes, Is.EquivalentTo(new[] { "INIT_FOLDER_MISSING", "INIT_SCENE_MISSING", "INIT_MARKER_MISSING" }),
                    "An empty root yields exactly the three directory-contract codes.");

                var folderIssues = report.Issues.Where(issue => issue.Code == "INIT_FOLDER_MISSING").ToList();
                Assert.That(folderIssues.Select(issue => issue.AssetPath), Is.EquivalentTo(IndieDirectoryContract.RequiredFoldersUnder(root)),
                    "Every required folder is reported once.");
                Assert.That(folderIssues.All(issue => issue.AutoFixable), Is.True, "Missing folders are auto-fixable.");

                var sceneIssues = report.Issues.Where(issue => issue.Code == "INIT_SCENE_MISSING").ToList();
                Assert.That(sceneIssues.Select(issue => issue.AssetPath), Is.EquivalentTo(IndieDirectoryContract.BaselineScenesUnder(root)),
                    "Every baseline scene is reported once.");
                Assert.That(sceneIssues.Any(issue => issue.AutoFixable), Is.False, "Missing scenes are not auto-fixable by Validate.");

                var markerIssues = report.Issues.Where(issue => issue.Code == "INIT_MARKER_MISSING").ToList();
                Assert.That(markerIssues, Has.Count.EqualTo(1), "The activation marker is reported once.");
                Assert.That(markerIssues[0].AssetPath, Is.EqualTo(IndieDirectoryContract.ActivationMarkerUnder(root)));
                Assert.That(markerIssues[0].AutoFixable, Is.True, "The missing marker is auto-fixable.");
            }
            finally
            {
                DisposableProjectRoot.Delete(root);
            }
        }
    }
}
