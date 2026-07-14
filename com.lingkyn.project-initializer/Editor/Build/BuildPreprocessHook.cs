using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using Lingkyn.Unity.ProjectInitializer.Editor.Validation;
using Lingkyn.Unity.ProjectInitializer.Editor.ConfigTools;
using System.IO;

namespace Lingkyn.Unity.ProjectInitializer.Editor.Build
{
    public sealed class BuildPreprocessHook : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!File.Exists(IndieDirectoryContract.ActivationMarker)) return;
            var validation = BuildValidator.ValidateForBuild();
            if (validation.HasCriticalIssues)
            {
                throw new BuildFailedException("Build blocked by ProjectValidator critical findings.");
            }
        }
    }
}
