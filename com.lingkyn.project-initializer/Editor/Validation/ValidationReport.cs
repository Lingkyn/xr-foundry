using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lingkyn.Unity.ProjectInitializer.Editor.Validation
{
    public sealed class ValidationReport
    {
        public List<ValidationIssue> Issues { get; } = new();

        public bool HasCriticalIssues => Issues.Any(i => i.Severity == ValidationSeverity.Critical);

        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append("{\"issues\":[");
            for (var i = 0; i < Issues.Count; i++)
            {
                var issue = Issues[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"severity\":\"").Append(issue.Severity)
                    .Append("\",\"code\":\"").Append(issue.Code)
                    .Append("\",\"message\":\"").Append(issue.Message).Append("\"}");
            }

            sb.Append("]}");
            return sb.ToString();
        }
    }
}
