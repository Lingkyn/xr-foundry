using NUnit.Framework;
using UnityEditor;
using Lingkyn.Unity.ProjectInitializer.Editor.ConfigTools;

namespace Lingkyn.Unity.ProjectInitializer.Tests
{
    public sealed class ProjectFolderScaffoldTests
    {
        [Test]
        public void ScaffoldingTwiceReusesEveryFolderAndCreatesNothingNew()
        {
            var root = DisposableProjectRoot.NewPath();
            try
            {
                var folders = IndieDirectoryContract.RequiredFoldersUnder(root);

                var first = ProjectFolderScaffold.EnsureDirectories(root, folders);

                Assert.That(AssetDatabase.IsValidFolder(root), Is.True, "The first run must create the project root.");
                Assert.That(first.CreatedFolders, Is.EquivalentTo(folders), "The first run must create every contract folder.");
                Assert.That(first.ReusedFolders, Is.Empty, "Nothing exists before the first run, so nothing can be reused.");
                Assert.That(first.CreatedGitKeeps, Is.Not.Empty, "Empty leaf folders receive a .gitkeep on the first run.");

                var second = ProjectFolderScaffold.EnsureDirectories(root, folders);

                Assert.That(second.CreatedFolders, Is.Empty, "The second run must create no folder.");
                Assert.That(second.ReusedFolders, Is.EquivalentTo(folders), "The second run must reuse every folder.");
                Assert.That(second.CreatedGitKeeps, Is.Empty, "The second run must write no .gitkeep.");
            }
            finally
            {
                DisposableProjectRoot.Delete(root);
            }
        }
    }
}
