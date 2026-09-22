using System;
using System.Collections.Generic;
using Lingkyn.LiveTuning.Core;

namespace Lingkyn.LiveTuning.Unity
{
    // Import of a device override file only when its path is passed explicitly. Every override
    // that names an unknown id, a mismatched kind, or an out-of-range value is reported with the
    // Core's own code and skipped; the remaining overrides are applied.

    /// <summary>The file-read seam an import goes through, so an EditMode test can inject a fake
    /// instead of depending on real disk state.</summary>
    public interface ITuningFileReader
    {
        bool TryReadAllText(string path, out string contents);
    }

    public sealed class RealTuningFileReader : ITuningFileReader
    {
        public bool TryReadAllText(string path, out string contents)
        {
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
            {
                contents = null;
                return false;
            }
            contents = System.IO.File.ReadAllText(path);
            return true;
        }
    }

    public sealed class TuningImportResult
    {
        public TuningImportResult(TuningState state, IReadOnlyList<TokenOverrideOutcome> outcomes)
        {
            State = state;
            Outcomes = outcomes;
        }

        public TuningState State { get; }
        /// <summary>One outcome per override entry in the file, in file order: accepted, or
        /// skipped with the Core's own code (tunable.unknown, tunable.kind.mismatch, or
        /// tunable.out_of_range).</summary>
        public IReadOnlyList<TokenOverrideOutcome> Outcomes { get; }
    }

    public static class TuningImport
    {
        /// <summary>Imports the override file at <paramref name="explicitPath"/> onto
        /// <paramref name="state"/>. Never imports anything without an explicit, non-empty path:
        /// that is a caller defect, not a data problem, so it throws rather than returning a
        /// silent no-op. A missing file is a by-name target that is missing (LESSON-004) and is
        /// reported with binding.target.missing, the same code a missing binding asset carries.</summary>
        public static LiveTuningResult<TuningImportResult> ImportFromPath(TuningState state, string explicitPath, ITuningFileReader reader)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (string.IsNullOrEmpty(explicitPath)) throw new ArgumentException("An import path must be passed explicitly; nothing is imported without one.", nameof(explicitPath));
            if (reader == null) throw new ArgumentNullException(nameof(reader));

            if (!reader.TryReadAllText(explicitPath, out var text))
            {
                return LiveTuningResult<TuningImportResult>.Fail(LiveTuningUnityFailure.BindingTargetMissing, $"Device override file '{explicitPath}' does not exist.");
            }
            var parsed = TokenOverrideDocument.Parse(text);
            if (!parsed.Succeeded) return parsed.As<TuningImportResult>();

            var report = parsed.Value.ApplySkippingFailures(state);
            return LiveTuningResult<TuningImportResult>.Ok(new TuningImportResult(report.State, report.Outcomes));
        }
    }
}
