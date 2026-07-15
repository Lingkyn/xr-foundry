namespace Lingkyn.Unity.ProjectInitializer.Editor.Validation
{
    public static class NamespaceConventionValidator
    {
        public static void Validate(ValidationReport report)
        {
            // Baseline hook: expanded checks can scan script GUIDs in a later slice.
            report.Issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Info,
                Code = "INIT_NAMESPACE_SCAN_DEFERRED",
                Message = "Namespace convention scan deferred to CI or expanded validator slice.",
            });
        }
    }
}
