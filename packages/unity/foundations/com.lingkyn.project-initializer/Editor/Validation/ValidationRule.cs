namespace Lingkyn.Unity.ProjectInitializer.Editor.Validation
{
    public delegate void ValidationRuleDelegate(ValidationReport report);

    public sealed class ValidationRule
    {
        public string Code { get; }
        public ValidationRuleDelegate Evaluate { get; }

        public ValidationRule(string code, ValidationRuleDelegate evaluate)
        {
            Code = code;
            Evaluate = evaluate;
        }
    }
}
