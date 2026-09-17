using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Lingkyn.Persistence.Core;
using Lingkyn.Settings.Core;

namespace XRFoundry.ReferenceSystem.Bindings
{
    [Serializable]
    public sealed class SettingsValuePersistenceDto
    {
        public string Kind;
        public string Payload;
    }

    [Serializable]
    public sealed class SettingsKnownValuePersistenceDto
    {
        public string Key;
        public string Scope;
        public SettingsValuePersistenceDto Value;
    }

    [Serializable]
    public sealed class SettingsUnknownValuePersistenceDto
    {
        public string RawKey;
        public SettingsValuePersistenceDto Value;
    }

    [Serializable]
    public sealed class SettingsSnapshotPersistenceDto
    {
        public string Revision;
        public SettingsKnownValuePersistenceDto[] KnownValues;
        public SettingsUnknownValuePersistenceDto[] UnknownValues;
    }

    /// <summary>
    /// Canonical, dictionary-free mapping used by the Unity JSON persistence codec.
    /// Unknown settings stay in a separate raw-key bucket so values using this
    /// schema's known value kinds survive without being treated as registered settings.
    /// The wire shape intentionally does not preserve unknown value kinds or arbitrary
    /// JSON fields.
    /// </summary>
    public static class SettingsSnapshotPersistenceDtoMapper
    {
        private const string BooleanKindTag = "boolean";
        private const string IntegerKindTag = "integer";
        private const string FloatKindTag = "float";
        private const string StringKindTag = "string";
        private const string OptionKindTag = "option";

        public static SaveResult<SettingsSnapshotPersistenceDto> FromSnapshot(SettingsSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return SaveResult<SettingsSnapshotPersistenceDto>.Fail(
                    SaveStage.Snapshot,
                    SaveErrorCode.ValidateRejected,
                    "Settings snapshot is required.");
            }

            if (snapshot.Revision < 0)
            {
                return SaveResult<SettingsSnapshotPersistenceDto>.Fail(
                    SaveStage.Snapshot,
                    SaveErrorCode.ValidateRejected,
                    "Settings revision cannot be negative.");
            }

            if (snapshot.Revision == long.MaxValue)
            {
                return InvalidDtoSnapshot(
                    "Settings revision cannot be safely incremented.");
            }

            var known = new List<SettingsKnownValuePersistenceDto>();
            foreach (var pair in snapshot.KnownValues.OrderBy(pair => pair.Key))
            {
                var key = SettingKey.TryCreate(pair.Key.Key.Value);
                if (!key.Succeeded)
                {
                    return InvalidDtoSnapshot(
                        $"Known setting key is invalid: {key.Error.Message}");
                }

                var scope = FromScope(pair.Key.Scope);
                if (!scope.Succeeded)
                {
                    return InvalidDtoSnapshot(
                        $"Known setting '{pair.Key.Key.Value}' scope is invalid: {scope.Error.Message}");
                }

                var mappedValue = FromValue(pair.Value);
                if (!mappedValue.Succeeded)
                {
                    return SaveResult<SettingsSnapshotPersistenceDto>.Fail(
                        mappedValue.Error.Stage,
                        mappedValue.Error.Code,
                        $"Known setting '{pair.Key.Key.Value}' cannot be persisted: {mappedValue.Error.Message}");
                }

                known.Add(new SettingsKnownValuePersistenceDto
                {
                    Key = key.Value.Value,
                    Scope = scope.Value,
                    Value = mappedValue.Value,
                });
            }

            var unknown = new List<SettingsUnknownValuePersistenceDto>();
            foreach (var pair in snapshot.UnknownValues.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                if (string.IsNullOrEmpty(pair.Key))
                {
                    return InvalidDtoSnapshot("Unknown setting raw key is required.");
                }

                var mappedValue = FromValue(pair.Value);
                if (!mappedValue.Succeeded)
                {
                    return SaveResult<SettingsSnapshotPersistenceDto>.Fail(
                        mappedValue.Error.Stage,
                        mappedValue.Error.Code,
                        $"Unknown setting '{pair.Key}' cannot be persisted: {mappedValue.Error.Message}");
                }

                unknown.Add(new SettingsUnknownValuePersistenceDto
                {
                    RawKey = pair.Key,
                    Value = mappedValue.Value,
                });
            }

            return SaveResult<SettingsSnapshotPersistenceDto>.Success(
                new SettingsSnapshotPersistenceDto
                {
                    Revision = snapshot.Revision.ToString(CultureInfo.InvariantCulture),
                    KnownValues = known.ToArray(),
                    UnknownValues = unknown.ToArray(),
                });
        }

        public static SaveResult Validate(SettingsSnapshotPersistenceDto dto)
        {
            var mapped = ToSnapshot(dto);
            return mapped.Succeeded
                ? SaveResult.Success()
                : SaveResult.Fail(mapped.Error.Stage, mapped.Error.Code, mapped.Error.Message);
        }

        public static SaveResult<SettingsSnapshot> ToSnapshot(SettingsSnapshotPersistenceDto dto)
        {
            if (dto == null)
            {
                return InvalidSnapshot("Settings persistence DTO is required.");
            }

            if (dto.Revision == null)
            {
                return InvalidSnapshot("Settings revision is required.");
            }

            if (!long.TryParse(
                    dto.Revision,
                    NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out var revision))
            {
                return InvalidSnapshot($"Settings revision '{dto.Revision}' is invalid.");
            }

            if (!string.Equals(
                    dto.Revision,
                    revision.ToString(CultureInfo.InvariantCulture),
                    StringComparison.Ordinal))
            {
                return InvalidSnapshot($"Settings revision '{dto.Revision}' is not canonical.");
            }

            if (revision < 0)
            {
                return InvalidSnapshot("Settings revision cannot be negative.");
            }

            if (revision == long.MaxValue)
            {
                return InvalidSnapshot("Settings revision cannot be safely incremented.");
            }

            if (dto.KnownValues == null)
            {
                return InvalidSnapshot("Known settings array is required.");
            }

            if (dto.UnknownValues == null)
            {
                return InvalidSnapshot("Unknown settings array is required.");
            }

            var known = new Dictionary<ScopedSettingKey, SettingValue>();
            var previousKnownKey = default(ScopedSettingKey);
            var hasPreviousKnownKey = false;
            for (var index = 0; index < dto.KnownValues.Length; index++)
            {
                var record = dto.KnownValues[index];
                if (record == null)
                {
                    return InvalidSnapshot($"Known setting[{index}] is required.");
                }

                var key = SettingKey.TryCreate(record.Key);
                if (!key.Succeeded)
                {
                    return InvalidSnapshot($"Known setting[{index}] key is invalid: {key.Error.Message}");
                }

                var scope = ToScope(record.Scope, $"Known setting[{index}]");
                if (!scope.Succeeded)
                {
                    return SaveResult<SettingsSnapshot>.Fail(
                        scope.Error.Stage,
                        scope.Error.Code,
                        scope.Error.Message);
                }

                var value = ToValue(record.Value, $"Known setting[{index}] '{record.Key}'");
                if (!value.Succeeded)
                {
                    return SaveResult<SettingsSnapshot>.Fail(
                        value.Error.Stage,
                        value.Error.Code,
                        value.Error.Message);
                }

                var scopedKey = new ScopedSettingKey(key.Value, scope.Value);
                if (known.ContainsKey(scopedKey))
                {
                    return InvalidSnapshot(
                        $"Duplicate known setting '{record.Key}' in scope '{record.Scope}'.");
                }

                if (hasPreviousKnownKey && previousKnownKey.CompareTo(scopedKey) >= 0)
                {
                    return InvalidSnapshot("Known settings are not in canonical key/scope order.");
                }

                known[scopedKey] = value.Value;
                previousKnownKey = scopedKey;
                hasPreviousKnownKey = true;
            }

            var unknown = new Dictionary<string, SettingValue>(StringComparer.Ordinal);
            string previousUnknownKey = null;
            for (var index = 0; index < dto.UnknownValues.Length; index++)
            {
                var record = dto.UnknownValues[index];
                if (record == null)
                {
                    return InvalidSnapshot($"Unknown setting[{index}] is required.");
                }

                // Unknown keys are deliberately not parsed as SettingKey. They are
                // opaque forward-compatible identifiers and must round-trip exactly.
                if (string.IsNullOrEmpty(record.RawKey))
                {
                    return InvalidSnapshot($"Unknown setting[{index}] raw key is required.");
                }

                var value = ToValue(record.Value, $"Unknown setting[{index}] '{record.RawKey}'");
                if (!value.Succeeded)
                {
                    return SaveResult<SettingsSnapshot>.Fail(
                        value.Error.Stage,
                        value.Error.Code,
                        value.Error.Message);
                }

                if (unknown.ContainsKey(record.RawKey))
                {
                    return InvalidSnapshot($"Duplicate unknown setting raw key '{record.RawKey}'.");
                }

                if (previousUnknownKey != null
                    && string.Compare(previousUnknownKey, record.RawKey, StringComparison.Ordinal) >= 0)
                {
                    return InvalidSnapshot("Unknown settings are not in canonical raw-key order.");
                }

                unknown[record.RawKey] = value.Value;
                previousUnknownKey = record.RawKey;
            }

            return SaveResult<SettingsSnapshot>.Success(
                new SettingsSnapshot(revision, known, unknown));
        }

        private static SaveResult<SettingsValuePersistenceDto> FromValue(SettingValue value)
        {
            var dto = new SettingsValuePersistenceDto();

            switch (value.Kind)
            {
                case SettingValueKind.Boolean:
                    dto.Kind = BooleanKindTag;
                    dto.Payload = value.BooleanValue ? "true" : "false";
                    break;
                case SettingValueKind.Integer:
                    dto.Kind = IntegerKindTag;
                    dto.Payload = value.IntegerValue.ToString(CultureInfo.InvariantCulture);
                    break;
                case SettingValueKind.Float:
                    if (double.IsNaN(value.FloatValue) || double.IsInfinity(value.FloatValue))
                    {
                        return InvalidValue("Floating-point setting must be finite.");
                    }

                    dto.Kind = FloatKindTag;
                    dto.Payload = value.FloatValue.ToString("R", CultureInfo.InvariantCulture);
                    break;
                case SettingValueKind.String:
                    dto.Kind = StringKindTag;
                    dto.Payload = value.StringValue ?? string.Empty;
                    break;
                case SettingValueKind.Option:
                {
                    var option = OptionId.TryCreate(value.OptionValue.Value);
                    if (!option.Succeeded)
                    {
                        return InvalidValue($"Option setting id is invalid: {option.Error.Message}");
                    }

                    dto.Kind = OptionKindTag;
                    dto.Payload = option.Value.Value;
                    break;
                }
                default:
                    return InvalidValue($"Setting value kind '{value.Kind}' is unsupported.");
            }

            return SaveResult<SettingsValuePersistenceDto>.Success(dto);
        }

        private static SaveResult<SettingValue> ToValue(SettingsValuePersistenceDto dto, string context)
        {
            if (dto == null)
            {
                return InvalidMappedValue($"{context} value is required.");
            }

            if (dto.Kind == null)
            {
                return InvalidMappedValue($"{context} kind is required.");
            }

            switch (dto.Kind)
            {
                case BooleanKindTag:
                    if (dto.Payload == null)
                    {
                        return InvalidMappedValue($"{context} boolean payload is required.");
                    }

                    if (string.Equals(dto.Payload, "true", StringComparison.Ordinal))
                    {
                        return SaveResult<SettingValue>.Success(SettingValue.FromBoolean(true));
                    }

                    if (string.Equals(dto.Payload, "false", StringComparison.Ordinal))
                    {
                        return SaveResult<SettingValue>.Success(SettingValue.FromBoolean(false));
                    }

                    return InvalidMappedValue(
                        $"{context} boolean payload '{dto.Payload}' is not canonical.");
                case IntegerKindTag:
                    if (dto.Payload == null)
                    {
                        return InvalidMappedValue($"{context} integer payload is required.");
                    }

                    if (!long.TryParse(
                            dto.Payload,
                            NumberStyles.AllowLeadingSign,
                            CultureInfo.InvariantCulture,
                            out var integerValue))
                    {
                        return InvalidMappedValue(
                            $"{context} integer payload '{dto.Payload}' is invalid.");
                    }

                    if (!string.Equals(
                            dto.Payload,
                            integerValue.ToString(CultureInfo.InvariantCulture),
                            StringComparison.Ordinal))
                    {
                        return InvalidMappedValue(
                            $"{context} integer payload '{dto.Payload}' is not canonical.");
                    }

                    return SaveResult<SettingValue>.Success(SettingValue.FromInteger(integerValue));
                case FloatKindTag:
                    if (dto.Payload == null)
                    {
                        return InvalidMappedValue($"{context} float payload is required.");
                    }

                    if (!double.TryParse(
                            dto.Payload,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out var floatValue)
                        || double.IsNaN(floatValue)
                        || double.IsInfinity(floatValue))
                    {
                        return InvalidMappedValue(
                            $"{context} float payload '{dto.Payload}' is invalid or non-finite.");
                    }

                    if (!string.Equals(
                            dto.Payload,
                            floatValue.ToString("R", CultureInfo.InvariantCulture),
                            StringComparison.Ordinal))
                    {
                        return InvalidMappedValue(
                            $"{context} float payload '{dto.Payload}' is not canonical.");
                    }

                    return SaveResult<SettingValue>.Success(SettingValue.FromFloat(floatValue));
                case StringKindTag:
                    if (dto.Payload == null)
                    {
                        return InvalidMappedValue($"{context} string payload is required.");
                    }

                    return SaveResult<SettingValue>.Success(SettingValue.FromString(dto.Payload));
                case OptionKindTag:
                {
                    if (dto.Payload == null)
                    {
                        return InvalidMappedValue($"{context} option payload is required.");
                    }

                    var option = OptionId.TryCreate(dto.Payload);
                    if (!option.Succeeded)
                    {
                        return InvalidMappedValue($"{context} option id is invalid: {option.Error.Message}");
                    }

                    return SaveResult<SettingValue>.Success(SettingValue.FromOption(option.Value));
                }
                default:
                    return InvalidMappedValue($"{context} kind '{dto.Kind}' is unsupported.");
            }
        }

        private static SaveResult<string> FromScope(SettingScope scope)
        {
            switch (scope)
            {
                case SettingScope.Global:
                    return SaveResult<string>.Success("global");
                case SettingScope.User:
                    return SaveResult<string>.Success("user");
                case SettingScope.Profile:
                    return SaveResult<string>.Success("profile");
                case SettingScope.Session:
                    return SaveResult<string>.Success("session");
                default:
                    return SaveResult<string>.Fail(
                        SaveStage.Snapshot,
                        SaveErrorCode.ValidateRejected,
                        $"Setting scope '{scope}' is unsupported.");
            }
        }

        private static SaveResult<SettingScope> ToScope(string scope, string context)
        {
            if (scope == null)
            {
                return SaveResult<SettingScope>.Fail(
                    SaveStage.Validate,
                    SaveErrorCode.ValidateRejected,
                    $"{context} scope is required.");
            }

            switch (scope)
            {
                case "global":
                    return SaveResult<SettingScope>.Success(SettingScope.Global);
                case "user":
                    return SaveResult<SettingScope>.Success(SettingScope.User);
                case "profile":
                    return SaveResult<SettingScope>.Success(SettingScope.Profile);
                case "session":
                    return SaveResult<SettingScope>.Success(SettingScope.Session);
                default:
                    return SaveResult<SettingScope>.Fail(
                        SaveStage.Validate,
                        SaveErrorCode.ValidateRejected,
                        $"{context} scope '{scope}' is unsupported.");
            }
        }

        private static SaveResult<SettingsSnapshot> InvalidSnapshot(string message)
        {
            return SaveResult<SettingsSnapshot>.Fail(
                SaveStage.Validate,
                SaveErrorCode.ValidateRejected,
                message);
        }

        private static SaveResult<SettingsSnapshotPersistenceDto> InvalidDtoSnapshot(string message)
        {
            return SaveResult<SettingsSnapshotPersistenceDto>.Fail(
                SaveStage.Snapshot,
                SaveErrorCode.ValidateRejected,
                message);
        }

        private static SaveResult<SettingsValuePersistenceDto> InvalidValue(string message)
        {
            return SaveResult<SettingsValuePersistenceDto>.Fail(
                SaveStage.Snapshot,
                SaveErrorCode.ValidateRejected,
                message);
        }

        private static SaveResult<SettingValue> InvalidMappedValue(string message)
        {
            return SaveResult<SettingValue>.Fail(
                SaveStage.Validate,
                SaveErrorCode.ValidateRejected,
                message);
        }
    }

    public sealed class SettingsPersistenceLoadReceipt
    {
        internal SettingsPersistenceLoadReceipt(
            SettingsSnapshot snapshot,
            SaveCandidateKind selectedCandidateKind,
            SaveCandidateId selectedCandidateId,
            bool recoveryOccurred,
            SaveDiagnostic? primaryFailureDiagnostic)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            SelectedCandidateKind = selectedCandidateKind;
            SelectedCandidateId = selectedCandidateId;
            RecoveryOccurred = recoveryOccurred;
            PrimaryFailureDiagnostic = primaryFailureDiagnostic;
        }

        public SettingsSnapshot Snapshot { get; }
        public SaveCandidateKind SelectedCandidateKind { get; }
        public SaveCandidateId SelectedCandidateId { get; }
        public bool RecoveryOccurred { get; }
        public SaveDiagnostic? PrimaryFailureDiagnostic { get; }
    }

    /// <summary>
    /// Bridges Settings Core's repository port to Persistence Core. The adapter
    /// owns no settings state: SettingsCoordinator remains the state authority. This
    /// adapter owns the canonical inner settings wire DTO, while SaveCoordinator and
    /// its ISaveStore own the outer envelope and durability. A Persistence.Unity
    /// codec/store can be supplied when composing the system.
    /// </summary>
    public sealed class SettingsPersistenceRehydrationAdapter : ISettingsSnapshotRepository
    {
        public const string OuterPersistenceSchemaId = "xr-foundry.reference-system.settings";
        public const int OuterPersistenceSchemaVersion = 1;

        private readonly SaveCoordinator<SettingsSnapshotPersistenceDto> _saveCoordinator;
        private readonly SaveSlotId _slotId;

        public SettingsPersistenceRehydrationAdapter(
            SaveCoordinator<SettingsSnapshotPersistenceDto> saveCoordinator,
            SaveSlotId slotId)
        {
            _saveCoordinator = saveCoordinator ?? throw new ArgumentNullException(nameof(saveCoordinator));
            if (string.IsNullOrEmpty(slotId.Value))
            {
                throw new ArgumentException("Settings save slot is invalid.", nameof(slotId));
            }

            _slotId = slotId;
        }

        public SaveCommitResult? LastSaveCommitResult { get; private set; }
        public SaveError? LastLoadError { get; private set; }
        public SaveCandidateKind? LastSelectedCandidateKind { get; private set; }
        public SaveCandidateId? LastSelectedCandidateId { get; private set; }
        public bool LastLoadRecoveryOccurred { get; private set; }
        public SaveDiagnostic? LastPrimaryFailureDiagnostic { get; private set; }

        public SettingsCoordinator CreateCoordinator(
            SettingsRegistry registry,
            SettingsSnapshot initialSnapshot,
            IEnumerable<ISettingApplicator> applicators = null,
            IEnumerable<ISettingsConstraint> constraints = null)
        {
            return new SettingsCoordinator(
                registry,
                initialSnapshot,
                applicators,
                constraints,
                this);
        }

        public SaveCommitResult Persist(
            SettingsSnapshot snapshot,
            CancellationToken cancellationToken = default)
        {
            var dto = SettingsSnapshotPersistenceDtoMapper.FromSnapshot(snapshot);
            SaveCommitResult result;
            if (!dto.Succeeded)
            {
                result = SaveCommitResult.NotCommitted(
                    dto.Error.Stage,
                    dto.Error.Code,
                    dto.Error.Message,
                    priorCommittedRecordPreserved: true);
            }
            else
            {
                result = _saveCoordinator.Save(_slotId, dto.Value, cancellationToken);
            }

            LastSaveCommitResult = result;
            return result;
        }

        public SaveResult<SettingsPersistenceLoadReceipt> LoadSnapshot()
        {
            LastLoadError = null;
            LastSelectedCandidateKind = null;
            LastSelectedCandidateId = null;
            LastLoadRecoveryOccurred = false;
            LastPrimaryFailureDiagnostic = null;

            var loaded = _saveCoordinator.LoadValidated(
                _slotId,
                SettingsSnapshotPersistenceDtoMapper.Validate);
            if (!loaded.Succeeded)
            {
                LastLoadError = loaded.Error;
                return SaveResult<SettingsPersistenceLoadReceipt>.Fail(
                    loaded.Error.Stage,
                    loaded.Error.Code,
                    loaded.Error.Message);
            }

            var mapped = SettingsSnapshotPersistenceDtoMapper.ToSnapshot(loaded.Value.State);
            if (!mapped.Succeeded)
            {
                LastLoadError = mapped.Error;
                return SaveResult<SettingsPersistenceLoadReceipt>.Fail(
                    mapped.Error.Stage,
                    mapped.Error.Code,
                    mapped.Error.Message);
            }

            var receipt = new SettingsPersistenceLoadReceipt(
                mapped.Value,
                loaded.Value.SelectedCandidateKind,
                loaded.Value.SelectedCandidateId,
                loaded.Value.RecoveryOccurred,
                loaded.Value.PrimaryFailureDiagnostic);
            // Retain only diagnostics. The loaded SettingsSnapshot is transferred
            // to SettingsCoordinator and is never cached by this adapter.
            LastSelectedCandidateKind = receipt.SelectedCandidateKind;
            LastSelectedCandidateId = receipt.SelectedCandidateId;
            LastLoadRecoveryOccurred = receipt.RecoveryOccurred;
            LastPrimaryFailureDiagnostic = receipt.PrimaryFailureDiagnostic;
            return SaveResult<SettingsPersistenceLoadReceipt>.Success(receipt);
        }

        /// <summary>
        /// Implements Settings Core's intentionally lossy repository result. Call
        /// <see cref="LoadSnapshot"/> or inspect <see cref="LastLoadError"/> when the
        /// exact persistence stage and error code are required.
        /// </summary>
        public SettingsResult<SettingsSnapshot> Load()
        {
            var loaded = LoadSnapshot();
            if (!loaded.Succeeded)
            {
                return SettingsResult<SettingsSnapshot>.Fail(
                    SettingsValidationCode.InvalidKey,
                    FormatPersistenceFailure(loaded.Error));
            }

            return SettingsResult<SettingsSnapshot>.Success(loaded.Value.Snapshot);
        }

        public SettingsPersistResult Save(SettingsSnapshot snapshot)
        {
            var saved = Persist(snapshot);
            return saved.Committed
                ? SettingsPersistResult.Success()
                : SettingsPersistResult.Fail(FormatPersistenceFailure(saved.Error));
        }

        private static string FormatPersistenceFailure(SaveError error)
        {
            return $"{error.Stage}:{error.Code} {error.Message}";
        }
    }
}
