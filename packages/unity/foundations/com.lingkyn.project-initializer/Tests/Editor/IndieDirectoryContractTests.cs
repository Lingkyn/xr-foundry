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
    }
}
