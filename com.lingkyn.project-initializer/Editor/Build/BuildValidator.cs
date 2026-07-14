using Lingkyn.Unity.ProjectInitializer.Editor.Validation;

namespace Lingkyn.Unity.ProjectInitializer.Editor.Build
{
    public static class BuildValidator
    {
        public static ValidationReport ValidateForBuild()
        {
            var report = IndieProjectValidator.ValidateIndieBaseline();
            if (report.Issues.Exists(i => i.Severity == ValidationSeverity.Error || i.Severity == ValidationSeverity.Critical))
            {
                report.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Critical,
                    Code = "INIT_BUILD_VALIDATION_FAILED",
                    Message = "Build validation found blocking initializer issues.",
                });
            }

            return report;
        }
    }
}
