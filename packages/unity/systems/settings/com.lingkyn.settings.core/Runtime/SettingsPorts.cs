using System.Collections.Generic;

namespace Lingkyn.Settings.Core
{
    public readonly struct SettingsApplicatorDiagnostic
    {
        public SettingsApplicatorDiagnostic(string applicatorId, string message)
        {
            ApplicatorId = applicatorId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string ApplicatorId { get; }
        public string Message { get; }
    }

    public readonly struct SettingsApplicatorStepResult
    {
        public bool Succeeded { get; }
        public SettingsApplicatorDiagnostic Diagnostic { get; }

        private SettingsApplicatorStepResult(bool succeeded, SettingsApplicatorDiagnostic diagnostic)
        {
            Succeeded = succeeded;
            Diagnostic = diagnostic;
        }

        public static SettingsApplicatorStepResult Success() => new SettingsApplicatorStepResult(true, default);
        public static SettingsApplicatorStepResult Fail(string applicatorId, string message)
            => new SettingsApplicatorStepResult(false, new SettingsApplicatorDiagnostic(applicatorId, message));
    }

    public interface ISettingApplicator
    {
        string ApplicatorId { get; }
        int Order { get; }
        bool CanApply(SettingKey key);
        SettingsApplicatorStepResult Apply(IReadOnlyList<SettingChange> changes);
        SettingsApplicatorStepResult Rollback(IReadOnlyList<SettingChange> changes);
    }

    public interface ISettingsConstraint
    {
        string ConstraintId { get; }
        SettingsResult Validate(SettingsRegistry registry, SettingsSnapshot candidate);
    }

    public readonly struct SettingsPersistResult
    {
        public bool Succeeded { get; }
        public string Message { get; }

        private SettingsPersistResult(bool succeeded, string message)
        {
            Succeeded = succeeded;
            Message = message ?? string.Empty;
        }

        public static SettingsPersistResult Success() => new SettingsPersistResult(true, string.Empty);
        public static SettingsPersistResult Fail(string message) => new SettingsPersistResult(false, message);
    }

    public interface ISettingsSnapshotRepository
    {
        SettingsResult<SettingsSnapshot> Load();
        SettingsPersistResult Save(SettingsSnapshot snapshot);
    }

    public readonly struct SettingsApplyResult
    {
        private static readonly SettingChange[] EmptyChanges = new SettingChange[0];
        private static readonly SettingsApplicatorDiagnostic[] EmptyDiagnostics = new SettingsApplicatorDiagnostic[0];

        private SettingsApplyResult(
            SettingsApplyOutcome outcome,
            long committedRevision,
            IReadOnlyList<SettingChange> changes,
            SettingsValidationError validationError,
            SettingsApplicatorDiagnostic primaryFailure,
            IReadOnlyList<SettingsApplicatorDiagnostic> rollbackDiagnostics,
            string persistenceMessage)
        {
            Outcome = outcome;
            CommittedRevision = committedRevision;
            Changes = SettingsReadOnly.FreezeList(changes ?? EmptyChanges);
            ValidationError = validationError;
            PrimaryFailure = primaryFailure;
            RollbackDiagnostics = SettingsReadOnly.FreezeList(rollbackDiagnostics ?? EmptyDiagnostics);
            PersistenceMessage = persistenceMessage ?? string.Empty;
        }

        public SettingsApplyOutcome Outcome { get; }
        public long CommittedRevision { get; }
        public IReadOnlyList<SettingChange> Changes { get; }
        public SettingsValidationError ValidationError { get; }
        public SettingsApplicatorDiagnostic PrimaryFailure { get; }
        public IReadOnlyList<SettingsApplicatorDiagnostic> RollbackDiagnostics { get; }
        public string PersistenceMessage { get; }

        public static SettingsApplyResult NoOp(long revision)
            => new SettingsApplyResult(SettingsApplyOutcome.NoOp, revision, EmptyChanges, default, default, EmptyDiagnostics, string.Empty);

        public static SettingsApplyResult Stale(long revision)
            => new SettingsApplyResult(SettingsApplyOutcome.StaleTransaction, revision, EmptyChanges, default, default, EmptyDiagnostics, string.Empty);

        public static SettingsApplyResult ValidationFailed(long revision, SettingsValidationError error)
            => new SettingsApplyResult(SettingsApplyOutcome.ValidationFailed, revision, EmptyChanges, error, default, EmptyDiagnostics, string.Empty);

        public static SettingsApplyResult ApplicatorFailed(
            long revision,
            SettingsApplicatorDiagnostic primaryFailure,
            IReadOnlyList<SettingsApplicatorDiagnostic> rollbackDiagnostics)
            => new SettingsApplyResult(
                SettingsApplyOutcome.ApplicatorFailed,
                revision,
                EmptyChanges,
                default,
                primaryFailure,
                rollbackDiagnostics,
                string.Empty);

        public static SettingsApplyResult RollbackFailed(
            long revision,
            SettingsApplicatorDiagnostic primaryFailure,
            IReadOnlyList<SettingsApplicatorDiagnostic> rollbackDiagnostics)
            => new SettingsApplyResult(
                SettingsApplyOutcome.RollbackFailed,
                revision,
                EmptyChanges,
                default,
                primaryFailure,
                rollbackDiagnostics,
                string.Empty);

        public static SettingsApplyResult Applied(long revision, IReadOnlyList<SettingChange> changes)
            => new SettingsApplyResult(SettingsApplyOutcome.Applied, revision, changes, default, default, EmptyDiagnostics, string.Empty);

        public static SettingsApplyResult AppliedNotPersisted(long revision, IReadOnlyList<SettingChange> changes, string message)
            => new SettingsApplyResult(SettingsApplyOutcome.AppliedNotPersisted, revision, changes, default, default, EmptyDiagnostics, message);
    }
}
