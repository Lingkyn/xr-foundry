using System;
using System.Collections.Generic;
using System.Linq;

namespace Lingkyn.Settings.Core
{
    public sealed class SettingsCoordinator
    {
        private readonly SettingsRegistry _registry;
        private readonly IReadOnlyList<ISettingApplicator> _applicators;
        private readonly IReadOnlyList<ISettingsConstraint> _constraints;
        private readonly ISettingsSnapshotRepository _repository;
        private SettingsSnapshot _committed;

        public SettingsCoordinator(
            SettingsRegistry registry,
            SettingsSnapshot initialSnapshot,
            IEnumerable<ISettingApplicator> applicators = null,
            IEnumerable<ISettingsConstraint> constraints = null,
            ISettingsSnapshotRepository repository = null)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _committed = initialSnapshot ?? throw new ArgumentNullException(nameof(initialSnapshot));
            _applicators = SortApplicators(applicators);
            _constraints = SettingsReadOnly.FreezeList(constraints ?? Array.Empty<ISettingsConstraint>());
            _repository = repository;
        }

        public SettingsSnapshot CommittedSnapshot => _committed;
        public SettingsRegistry Registry => _registry;

        public event Action<IReadOnlyList<SettingChange>> ChangesApplied;

        public SettingsTransaction BeginTransaction()
            => new SettingsTransaction(_committed.Revision);

        public void Cancel(SettingsTransaction transaction)
        {
            if (transaction == null)
            {
                throw new ArgumentNullException(nameof(transaction));
            }
        }

        public SettingsApplyResult Apply(SettingsTransaction transaction)
        {
            if (transaction == null)
            {
                throw new ArgumentNullException(nameof(transaction));
            }

            if (transaction.BaseRevision != _committed.Revision)
            {
                return SettingsApplyResult.Stale(_committed.Revision);
            }

            var candidateResult = MaterializeCandidate(transaction);
            if (!candidateResult.Succeeded)
            {
                return SettingsApplyResult.ValidationFailed(_committed.Revision, candidateResult.Error);
            }

            var candidate = candidateResult.Value;
            var validation = ValidateCandidate(candidate);
            if (!validation.Succeeded)
            {
                return SettingsApplyResult.ValidationFailed(_committed.Revision, validation.Error);
            }

            var changes = ComputeChanges(_registry, _committed, candidate);
            if (changes.Count == 0)
            {
                return SettingsApplyResult.NoOp(_committed.Revision);
            }

            var applicatorResult = RunApplicators(changes);
            if (applicatorResult.Outcome == SettingsApplyOutcome.ApplicatorFailed
                || applicatorResult.Outcome == SettingsApplyOutcome.RollbackFailed)
            {
                return applicatorResult;
            }

            var nextRevision = _committed.Revision + 1;
            _committed = candidate.WithRevision(nextRevision);
            ChangesApplied?.Invoke(changes);

            if (_repository != null)
            {
                var persist = _repository.Save(_committed);
                if (!persist.Succeeded)
                {
                    return SettingsApplyResult.AppliedNotPersisted(_committed.Revision, changes, persist.Message);
                }
            }

            return SettingsApplyResult.Applied(_committed.Revision, changes);
        }

        public SettingsResult<SettingsSnapshot> LoadFromRepository()
        {
            if (_repository == null)
            {
                return SettingsResult<SettingsSnapshot>.Fail(
                    SettingsValidationCode.InvalidKey,
                    "No snapshot repository configured.");
            }

            var loaded = _repository.Load();
            if (!loaded.Succeeded)
            {
                return loaded;
            }

            var validated = SettingsSnapshotValidator.ValidateLoaded(_registry, loaded.Value);
            if (!validated.Succeeded)
            {
                return validated;
            }

            _committed = validated.Value;
            return SettingsResult<SettingsSnapshot>.Success(_committed);
        }

        internal static List<SettingChange> ComputeChanges(
            SettingsRegistry registry,
            SettingsSnapshot committed,
            SettingsSnapshot candidate)
        {
            var changes = new List<SettingChange>();
            var keys = new HashSet<ScopedSettingKey>();
            foreach (var pair in committed.KnownValues) keys.Add(pair.Key);
            foreach (var pair in candidate.KnownValues) keys.Add(pair.Key);

            foreach (var scopedKey in keys.OrderBy(k => k))
            {
                var hadOldValue = committed.TryGetKnownValue(scopedKey, out var oldValue);
                var hasNewValue = candidate.TryGetKnownValue(scopedKey, out var newValue);
                if (!hadOldValue && !hasNewValue)
                {
                    continue;
                }

                if (hadOldValue && hasNewValue && oldValue.Equals(newValue))
                {
                    continue;
                }

                if (!registry.TryGetDefinition(scopedKey.Key, out var definition))
                {
                    continue;
                }

                changes.Add(new SettingChange(scopedKey, hadOldValue, oldValue, hasNewValue, newValue, definition));
            }

            return changes;
        }

        private SettingsApplyResult RunApplicators(IReadOnlyList<SettingChange> changes)
        {
            var applied = new List<ISettingApplicator>();
            var appliedChanges = new List<SettingChange>();

            foreach (var applicator in _applicators)
            {
                var subset = changes.Where(c => applicator.CanApply(c.Key)).ToList();
                if (subset.Count == 0)
                {
                    continue;
                }

                var result = applicator.Apply(subset);
                if (!result.Succeeded)
                {
                    var rollbackDiagnostics = RollbackApplied(applied, appliedChanges);
                    var outcome = rollbackDiagnostics.Any(d => !string.IsNullOrEmpty(d.Message))
                        ? SettingsApplyOutcome.RollbackFailed
                        : SettingsApplyOutcome.ApplicatorFailed;
                    return outcome == SettingsApplyOutcome.RollbackFailed
                        ? SettingsApplyResult.RollbackFailed(_committed.Revision, result.Diagnostic, rollbackDiagnostics)
                        : SettingsApplyResult.ApplicatorFailed(_committed.Revision, result.Diagnostic, rollbackDiagnostics);
                }

                applied.Add(applicator);
                appliedChanges.AddRange(subset);
            }

            return SettingsApplyResult.Applied(_committed.Revision, changes);
        }

        private List<SettingsApplicatorDiagnostic> RollbackApplied(
            IReadOnlyList<ISettingApplicator> applied,
            IReadOnlyList<SettingChange> appliedChanges)
        {
            var diagnostics = new List<SettingsApplicatorDiagnostic>();
            for (var i = applied.Count - 1; i >= 0; i--)
            {
                var applicator = applied[i];
                var subset = appliedChanges.Where(c => applicator.CanApply(c.Key)).ToList();
                if (subset.Count == 0)
                {
                    continue;
                }

                var rollback = applicator.Rollback(subset);
                if (!rollback.Succeeded)
                {
                    diagnostics.Add(rollback.Diagnostic);
                }
            }

            return diagnostics;
        }

        private SettingsResult<SettingsSnapshot> MaterializeCandidate(SettingsTransaction transaction)
        {
            var known = new Dictionary<ScopedSettingKey, SettingValue>(_committed.KnownValues);
            var unknown = new Dictionary<string, SettingValue>(_committed.UnknownValues, StringComparer.Ordinal);

            foreach (var command in transaction.Commands)
            {
                switch (command.Kind)
                {
                    case SettingsTransactionCommandKind.Set:
                    {
                        var scopedValidation = ScopedSettingKey.TryCreate(command.ScopedKey.Key, command.ScopedKey.Scope);
                        if (!scopedValidation.Succeeded)
                        {
                            return SettingsResult<SettingsSnapshot>.Fail(
                                scopedValidation.Error.Code,
                                scopedValidation.Error.Message,
                                scopedValidation.Error.Key);
                        }

                        if (!TryGetDefinition(command.ScopedKey.Key, out _))
                        {
                            return SettingsResult<SettingsSnapshot>.Fail(
                                SettingsValidationCode.InvalidKey,
                                "Cannot set unknown setting key.",
                                command.ScopedKey.Key);
                        }

                        known[scopedValidation.Value] = command.Value;
                        break;
                    }
                    case SettingsTransactionCommandKind.ResetScope:
                    {
                        var scopeValidation = SettingScopeValidator.Validate(command.ResetScope);
                        if (!scopeValidation.Succeeded)
                        {
                            return SettingsResult<SettingsSnapshot>.Fail(
                                scopeValidation.Error.Code,
                                scopeValidation.Error.Message);
                        }

                        ResetScope(command.ResetScope, known);
                        break;
                    }
                    case SettingsTransactionCommandKind.ApplyProfile:
                    {
                        if (command.Profile == null)
                        {
                            return SettingsResult<SettingsSnapshot>.Fail(
                                SettingsValidationCode.InvalidProfileLayer,
                                "Profile command requires a profile.");
                        }

                        var profileResult = ApplyProfileLayers(command.Profile, known);
                        if (!profileResult.Succeeded)
                        {
                            return SettingsResult<SettingsSnapshot>.Fail(
                                profileResult.Error.Code,
                                profileResult.Error.Message,
                                profileResult.Error.Key);
                        }

                        break;
                    }
                    default:
                        return SettingsResult<SettingsSnapshot>.Fail(
                            SettingsValidationCode.InvalidKey,
                            "Unknown transaction command.");
                }
            }

            return SettingsResult<SettingsSnapshot>.Success(
                new SettingsSnapshot(transaction.BaseRevision, known, unknown));
        }

        private void ResetScope(SettingScope scope, Dictionary<ScopedSettingKey, SettingValue> known)
        {
            var toRemove = new List<ScopedSettingKey>();
            foreach (var pair in known)
            {
                if (pair.Key.Scope == scope)
                {
                    toRemove.Add(pair.Key);
                }
            }

            for (var i = 0; i < toRemove.Count; i++)
            {
                known.Remove(toRemove[i]);
            }

            foreach (var definition in _registry.Definitions)
            {
                if (definition.DefaultScope == scope)
                {
                    known[new ScopedSettingKey(definition.Key, definition.DefaultScope)] = definition.DefaultValue;
                }
            }
        }

        private SettingsResult ApplyProfileLayers(
            SettingsProfile profile,
            Dictionary<ScopedSettingKey, SettingValue> known)
        {
            foreach (var layer in profile.Layers)
            {
                if (layer == null)
                {
                    return SettingsResult.Fail(
                        SettingsValidationCode.InvalidProfileLayer,
                        "Profile layer must not be null.");
                }

                foreach (var pair in layer.Overrides)
                {
                    if (string.IsNullOrEmpty(pair.Key.Value))
                    {
                        return SettingsResult.Fail(
                            SettingsValidationCode.InvalidKey,
                            "Profile override key must not be empty.");
                    }

                    if (!TryGetDefinition(pair.Key, out var definition))
                    {
                        return SettingsResult.Fail(
                            SettingsValidationCode.InvalidKey,
                            "Profile override references unknown setting key.",
                            pair.Key);
                    }

                    if (pair.Value.Kind != definition.Kind)
                    {
                        return SettingsResult.Fail(
                            SettingsValidationCode.KindMismatch,
                            "Profile override kind mismatch.",
                            pair.Key);
                    }

                    var valueValidation = SettingDefinitionValidator.ValidateValue(definition, pair.Value);
                    if (!valueValidation.Succeeded)
                    {
                        return valueValidation;
                    }

                    known[new ScopedSettingKey(pair.Key, definition.DefaultScope)] = pair.Value;
                }
            }

            return SettingsResult.Success();
        }

        private SettingsResult ValidateCandidate(SettingsSnapshot candidate)
        {
            foreach (var pair in candidate.KnownValues)
            {
                var scopeValidation = SettingScopeValidator.Validate(pair.Key.Scope);
                if (!scopeValidation.Succeeded)
                {
                    return SettingsResult.Fail(
                        scopeValidation.Error.Code,
                        $"Candidate snapshot contains invalid scope for key '{pair.Key.Key.Value}'.",
                        pair.Key.Key);
                }

                if (!TryGetDefinition(pair.Key.Key, out var definition))
                {
                    return SettingsResult.Fail(
                        SettingsValidationCode.InvalidKey,
                        $"Candidate snapshot contains unregistered key '{pair.Key.Key.Value}'.",
                        pair.Key.Key);
                }

                var valueValidation = SettingDefinitionValidator.ValidateValue(definition, pair.Value);
                if (!valueValidation.Succeeded)
                {
                    return valueValidation;
                }
            }

            foreach (var constraint in _constraints)
            {
                var result = constraint.Validate(_registry, candidate);
                if (!result.Succeeded)
                {
                    return SettingsResult.Fail(
                        SettingsValidationCode.CrossConstraintViolation,
                        result.Error.Message);
                }
            }

            return SettingsResult.Success();
        }

        private bool TryGetDefinition(SettingKey key, out SettingDefinition definition)
            => _registry.TryGetDefinition(key, out definition);

        private static IReadOnlyList<ISettingApplicator> SortApplicators(IEnumerable<ISettingApplicator> applicators)
        {
            return SettingsReadOnly.FreezeList(
                (applicators ?? Array.Empty<ISettingApplicator>())
                .OrderBy(a => a.Order)
                .ThenBy(a => a.ApplicatorId, StringComparer.Ordinal)
                .ToArray());
        }
    }
}
