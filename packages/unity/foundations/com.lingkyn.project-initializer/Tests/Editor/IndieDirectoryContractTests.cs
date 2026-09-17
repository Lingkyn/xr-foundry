using System.Linq;
using NUnit.Framework;
using Lingkyn.Unity.ProjectInitializer.Editor.ConfigTools;

namespace Lingkyn.Unity.ProjectInitializer.Tests
{
    public sealed class IndieDirectoryContractTests
    {
        [Test]
        public void BaselineScenesAreConsumerNeutralAndUnderProjectRoot()
        {
            Assert.That(IndieDirectoryContract.BaselineScenes, Has.Length.EqualTo(4));
            foreach (var path in IndieDirectoryContract.BaselineScenes)
            {
                Assert.That(path, Does.StartWith(IndieDirectoryContract.ProjectRoot));
                Assert.That(path.ToLowerInvariant(), Does.Not.Contain("consumer-product-name"));
            }
        }

        [Test]
        public void RequiredFoldersAreUnderProjectRootAndUnique()
        {
            var folders = IndieDirectoryContract.RequiredFolders;
            Assert.That(folders, Is.Not.Empty);
            Assert.That(folders.Distinct().Count(), Is.EqualTo(folders.Length), "Required folders must be unique.");
            foreach (var folder in folders)
            {
                Assert.That(folder, Does.StartWith(IndieDirectoryContract.ProjectRoot + "/"), folder);
                Assert.That(folder, Does.Not.Contain("\\"), folder);
                Assert.That(folder, Does.Not.EndWith("/"), folder);
                Assert.That(folder, Does.Not.Contain("//"), folder);
                Assert.That(folder.ToLowerInvariant(), Does.Not.Contain("consumer-product-name"), folder);
            }

            Assert.That(folders, Does.Contain(IndieDirectoryContract.SettingsRoot));
            Assert.That(folders, Does.Contain(IndieDirectoryContract.ProjectRoot + "/Scenes"));
        }
    }
}
