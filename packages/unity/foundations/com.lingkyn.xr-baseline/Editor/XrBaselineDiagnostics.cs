using System.Collections.Generic;
using UnityEngine;

namespace Lingkyn.Unity.XrBaseline.Editor
{
    /// <summary>
    /// One place for every "could not resolve an upstream member or asset" outcome in the
    /// XR Baseline editor tools. The tools resolve XR Interaction Toolkit and Input System
    /// members by name so the package assemblies carry no XRI reference; when a member is
    /// missing they must say so instead of returning as if the work were done
    /// (consumer lessons register, LESSON-004). Each key reports once per Editor session
    /// or until <see cref="Reset"/> runs, so a Sandbox initialization prints one line per
    /// problem rather than one per component.
    /// </summary>
    public static class XrBaselineDiagnostics
    {
        const string Prefix = "xr_baseline_unresolved";
        static readonly HashSet<string> Reported = new HashSet<string>();

        /// <summary>Keys reported since the last <see cref="Reset"/>.</summary>
        public static IReadOnlyCollection<string> ReportedKeys => Reported;

        /// <summary>
        /// Records that <paramref name="key"/> could not be resolved and logs a warning the first
        /// time. Returns true when this call produced the warning.
        /// </summary>
        public static bool Unresolved(string key, string reason, Object context = null)
        {
            if (string.IsNullOrEmpty(key)) key = "unknown";
            if (!Reported.Add(key)) return false;
            Debug.LogWarning($"{Prefix}: {key}: {reason}", context);
            return true;
        }

        /// <summary>Clears the once-per-session memory; the menu calls this before each run.</summary>
        public static void Reset() => Reported.Clear();
    }
}
