namespace Lingkyn.Unity.ProjectInitializer.Editor.Validation
{
    public enum ValidationSeverity
    {
        Info,
        Warning,
        Error,
        Critical,
    }

    public sealed class ValidationIssue
    {
        public ValidationSeverity Severity;
        public string Code;
        public string Message;
        public string AssetPath;
        public string ScenePath;
        public string ObjectPath;
        public string SuggestedFix;
        public bool AutoFixable;
    }
}
