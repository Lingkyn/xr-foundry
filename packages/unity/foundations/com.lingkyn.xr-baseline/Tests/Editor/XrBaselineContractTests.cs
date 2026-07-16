using NUnit.Framework;
using Lingkyn.Unity.XrBaseline.Config;
using Lingkyn.Unity.XrBaseline.Constants;

namespace Lingkyn.Unity.XrBaseline.Tests
{
    public sealed class XrBaselineContractTests
    {
        [Test]
        public void DefaultsAreSafeAndConsumerNeutral()
        {
            var config = UnityEngine.ScriptableObject.CreateInstance<VrBaselineConfig>();
            try
            {
                Assert.That(config.enableContinuousMove, Is.False);
                Assert.That(VrBaselineProjectPaths.SandboxScene, Does.StartWith("Assets/_Project/"));
                Assert.That(VrBaselineProjectPaths.SandboxScene.ToLowerInvariant(), Does.Not.Contain("consumer-product-name"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }
    }
}
