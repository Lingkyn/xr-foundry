using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Lingkyn.Persistence.Core
{
    public enum SaveStage
    {
        Snapshot,
        Encode,
        Integrity,
        StageWrite,
        Flush,
        Commit,
        Read,
        Envelope,
        Verify,
        Decode,
        Migrate,
        Validate,
        Apply
    }

    public enum SaveErrorCode
    {
        None = 0,
        InvalidSlot,
        NotFound,
        UnsupportedFormat,
        FutureSchema,
        MissingMigration,
        AmbiguousMigration,
        CyclicMigration, // Defensive fallback when traversal revisits a version.
        NonMonotonicMigration, // Graph contains a back-edge or stagnant edge; takes precedence during graph validation.
        CorruptPayload,
        UnsupportedCommitCapability,
        IoDenied,
        OutOfSpace,
        Cancelled,
        ValidateRejected,
        ProviderFailure,
        OvershootMigration
    }

    public readonly struct SaveError : IEquatable<SaveError>
    {
        public SaveError(SaveStage stage, SaveErrorCode code, string message)
        {
            Stage = stage;
            Code = code;
            Message = message ?? string.Empty;
        }

        public SaveStage Stage { get; }
        public SaveErrorCode Code { get; }
        public string Message { get; }

        public bool Equals(SaveError other)
        {
            return Stage == other.Stage
                && Code == other.Code
                && string.Equals(Message, other.Message, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is SaveError other && Equals(other);
        public override int GetHashCode()
        {
            var message = Message ?? string.Empty;
            return ((int)Stage * 397) ^ (int)Code ^ StringComparer.Ordinal.GetHashCode(message);
        }
        public override string ToString() => $"{Stage}:{Code} {Message}";
    }

    public readonly struct SaveResult
    {
        private SaveResult(bool succeeded, SaveError error)
        {
            Succeeded = succeeded;
            Error = error;
        }

        public bool Succeeded { get; }
        public SaveError Error { get; }

        public static SaveResult Success() => new SaveResult(true, default);
        public static SaveResult Fail(SaveStage stage, SaveErrorCode code, string message)
            => new SaveResult(false, new SaveError(stage, code, message));
    }

    public readonly struct SaveResult<T>
    {
        private SaveResult(bool succeeded, T value, SaveError error)
        {
            Succeeded = succeeded;
            Value = value;
            Error = error;
        }

        public bool Succeeded { get; }
        public T Value { get; }
        public SaveError Error { get; }

        public static SaveResult<T> Success(T value) => new SaveResult<T>(true, value, default);
        public static SaveResult<T> Fail(SaveStage stage, SaveErrorCode code, string message)
            => new SaveResult<T>(false, default, new SaveError(stage, code, message));
    }

    public enum SaveDiagnosticSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    public readonly struct SaveDiagnostic
    {
        public SaveDiagnostic(SaveDiagnosticSeverity severity, SaveStage stage, SaveErrorCode code, string message)
        {
            Severity = severity;
            Stage = stage;
            Code = code;
            Message = message ?? string.Empty;
        }

        public SaveDiagnosticSeverity Severity { get; }
        public SaveStage Stage { get; }
        public SaveErrorCode Code { get; }
        public string Message { get; }
    }

    public readonly struct SaveCommitResult
    {
        private static readonly SaveDiagnostic[] EmptyDiagnostics = Array.Empty<SaveDiagnostic>();

        private SaveCommitResult(bool committed, bool priorCommittedRecordPreserved, SaveError error, IReadOnlyList<SaveDiagnostic> diagnostics)
        {
            Committed = committed;
            PriorCommittedRecordPreserved = priorCommittedRecordPreserved;
            Error = error;
            Diagnostics = diagnostics ?? EmptyDiagnostics;
        }

        public bool Committed { get; }
        public bool PriorCommittedRecordPreserved { get; }
        public SaveError Error { get; }
        public IReadOnlyList<SaveDiagnostic> Diagnostics { get; }

        public static SaveCommitResult Success(IReadOnlyList<SaveDiagnostic> diagnostics = null)
            => new SaveCommitResult(true, false, default, diagnostics);

        public static SaveCommitResult NotCommitted(
            SaveStage stage,
            SaveErrorCode code,
            string message,
            bool priorCommittedRecordPreserved,
            IReadOnlyList<SaveDiagnostic> diagnostics = null)
            => new SaveCommitResult(
                false,
                priorCommittedRecordPreserved,
                new SaveError(stage, code, message),
                diagnostics);

        public SaveCommitResult WithDiagnostics(IReadOnlyList<SaveDiagnostic> diagnostics)
            => new SaveCommitResult(Committed, PriorCommittedRecordPreserved, Error, diagnostics);

        public bool IsContradictory(out string reason)
        {
            if (Committed && Error.Code != SaveErrorCode.None)
            {
                reason = "committed=true cannot include an error payload.";
                return true;
            }

            if (!Committed && Error.Code == SaveErrorCode.None)
            {
                reason = "committed=false must include an error payload.";
                return true;
            }

            if (Committed && PriorCommittedRecordPreserved)
            {
                reason = "committed=true cannot report priorCommittedRecordPreserved=true.";
                return true;
            }

            reason = null;
            return false;
        }
    }

    public readonly struct SaveSlotId : IEquatable<SaveSlotId>
    {
        private const int MaxLength = 64;
        private readonly string _value;

        private SaveSlotId(string value)
        {
            _value = value;
        }

        public string Value => _value ?? string.Empty;

        public static SaveResult<SaveSlotId> TryCreate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return SaveResult<SaveSlotId>.Fail(SaveStage.Snapshot, SaveErrorCode.InvalidSlot, "Save slot cannot be empty.");
            }

            if (value.Length > MaxLength)
            {
                return SaveResult<SaveSlotId>.Fail(SaveStage.Snapshot, SaveErrorCode.InvalidSlot, "Save slot exceeds max length.");
            }

            for (var index = 0; index < value.Length; index++)
            {
                var current = value[index];
                if (!(char.IsLetterOrDigit(current) || current == '-' || current == '_'))
                {
                    return SaveResult<SaveSlotId>.Fail(SaveStage.Snapshot, SaveErrorCode.InvalidSlot, "Save slot contains unsupported characters.");
                }
            }

            return SaveResult<SaveSlotId>.Success(new SaveSlotId(value));
        }

        public bool Equals(SaveSlotId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SaveSlotId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public override string ToString() => Value;
        public static bool operator ==(SaveSlotId left, SaveSlotId right) => left.Equals(right);
        public static bool operator !=(SaveSlotId left, SaveSlotId right) => !left.Equals(right);
    }

    public sealed class SaveEnvelope
    {
        private readonly byte[] _integrityDigest;
        private readonly byte[] _payload;

        public SaveEnvelope(
            string schemaId,
            int schemaVersion,
            string commitId,
            long timestampUtcTicks,
            string integrityAlgorithm,
            byte[] integrityDigest,
            byte[] payload)
        {
            if (string.IsNullOrWhiteSpace(schemaId))
            {
                throw new ArgumentException("Schema id is required.", nameof(schemaId));
            }

            if (schemaVersion < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(schemaVersion), "Schema version cannot be negative.");
            }

            if (string.IsNullOrWhiteSpace(commitId))
            {
                throw new ArgumentException("Commit id is required.", nameof(commitId));
            }

            if (string.IsNullOrWhiteSpace(integrityAlgorithm))
            {
                throw new ArgumentException("Integrity algorithm is required.", nameof(integrityAlgorithm));
            }

            if (integrityDigest == null || integrityDigest.Length == 0)
            {
                throw new ArgumentException("Integrity digest is required.", nameof(integrityDigest));
            }

            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            _integrityDigest = CopyBytes(integrityDigest);
            _payload = CopyBytes(payload);
            SchemaId = schemaId;
            SchemaVersion = schemaVersion;
            CommitId = commitId;
            TimestampUtcTicks = timestampUtcTicks;
            IntegrityAlgorithm = integrityAlgorithm;
            IntegrityDigest = _integrityDigest;
            Payload = _payload;
        }

        public string SchemaId { get; }
        public int SchemaVersion { get; }
        public string CommitId { get; }
        public long TimestampUtcTicks { get; }
        public string IntegrityAlgorithm { get; }
        public ReadOnlyMemory<byte> IntegrityDigest { get; }
        public ReadOnlyMemory<byte> Payload { get; }

        private static byte[] CopyBytes(byte[] source)
        {
            var copy = new byte[source.Length];
            Buffer.BlockCopy(source, 0, copy, 0, source.Length);
            return copy;
        }
    }

    public static class SaveEnvelopeBinaryCodec
    {
        private static readonly byte[] Magic = { (byte)'L', (byte)'P', (byte)'S', (byte)'C' };
        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);
        public const byte FormatVersion = 1;
        public const int MaxSchemaIdBytes = 128;
        public const int MaxCommitIdBytes = 128;
        public const int MaxAlgorithmBytes = 32;
        public const int MaxDigestBytes = 64;
        public const int MaxPayloadBytes = 4 * 1024 * 1024;

        public static SaveResult<byte[]> Encode(SaveEnvelope envelope)
        {
            if (envelope == null)
            {
                return SaveResult<byte[]>.Fail(SaveStage.Envelope, SaveErrorCode.UnsupportedFormat, "Envelope is required.");
            }

            var schemaBytes = StrictUtf8.GetBytes(envelope.SchemaId);
            var commitBytes = StrictUtf8.GetBytes(envelope.CommitId);
            var algorithmBytes = StrictUtf8.GetBytes(envelope.IntegrityAlgorithm);
            var digestBytes = envelope.IntegrityDigest.ToArray();
            var payloadBytes = envelope.Payload.ToArray();

            if (schemaBytes.Length == 0 || schemaBytes.Length > MaxSchemaIdBytes
                || commitBytes.Length == 0 || commitBytes.Length > MaxCommitIdBytes
                || algorithmBytes.Length == 0 || algorithmBytes.Length > MaxAlgorithmBytes
                || digestBytes.Length == 0 || digestBytes.Length > MaxDigestBytes
                || payloadBytes.Length > MaxPayloadBytes)
            {
                return SaveResult<byte[]>.Fail(SaveStage.Envelope, SaveErrorCode.UnsupportedFormat, "Envelope exceeds codec bounds.");
            }

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, StrictUtf8, true))
            {
                writer.Write(Magic);
                writer.Write(FormatVersion);
                writer.Write((ushort)schemaBytes.Length);
                writer.Write(schemaBytes);
                writer.Write(envelope.SchemaVersion);
                writer.Write((ushort)commitBytes.Length);
                writer.Write(commitBytes);
                writer.Write(envelope.TimestampUtcTicks);
                writer.Write((byte)algorithmBytes.Length);
                writer.Write(algorithmBytes);
                writer.Write((byte)digestBytes.Length);
                writer.Write(digestBytes);
                writer.Write(payloadBytes.Length);
                writer.Write(payloadBytes);
                writer.Flush();
                return SaveResult<byte[]>.Success(stream.ToArray());
            }
        }

        public static SaveResult<SaveEnvelope> Decode(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length < 4 + 1 + 2 + 4 + 2 + 8 + 1 + 1 + 4)
            {
                return SaveResult<SaveEnvelope>.Fail(SaveStage.Envelope, SaveErrorCode.UnsupportedFormat, "Envelope is too short.");
            }

            var cursor = 0;
            if (!TryReadExact(bytes, ref cursor, Magic.Length, out var magicSlice)
                || !magicSlice.SequenceEqual(Magic))
            {
                return SaveResult<SaveEnvelope>.Fail(SaveStage.Envelope, SaveErrorCode.UnsupportedFormat, "Envelope magic mismatch.");
            }

            if (!TryReadByte(bytes, ref cursor, out var version))
            {
                return SaveResult<SaveEnvelope>.Fail(SaveStage.Envelope, SaveErrorCode.UnsupportedFormat, "Envelope format version is missing.");
            }

            if (version > FormatVersion)
            {
                return SaveResult<SaveEnvelope>.Fail(SaveStage.Envelope, SaveErrorCode.FutureSchema, "Envelope format version is newer than supported.");
            }

            if (version != FormatVersion)
            {
                return SaveResult<SaveEnvelope>.Fail(SaveStage.Envelope, SaveErrorCode.UnsupportedFormat, "Envelope format version is unsupported.");
            }

            if (!TryReadUInt16(bytes, ref cursor, out var schemaLength) || schemaLength == 0 || schemaLength > MaxSchemaIdBytes)
            {
                return SaveResult<SaveEnvelope>.Fail(SaveStage.Envelope, SaveErrorCode.UnsupportedFormat, "Schema id length is invalid.");
            }

            if (!TryReadUtf8(bytes, ref cursor, schemaLength, out var schemaId))
            {
                return SaveResult<SaveEnvelope>.Fail(SaveStage.Envelope, SaveErrorCode.UnsupportedFormat, "Schema id bytes are invalid.");
            }

            if (!TryReadInt32(bytes, ref cursor, out var schemaVersion) || schemaVersion < 0)
            {
                return SaveResult<SaveEnvelope>.Fail(SaveStage.Envelope, SaveErrorCode.UnsupportedFormat, "Schema version is invalid.");
            }

            if (!TryReadUInt16(bytes, ref cursor, out var commitLength) || commitLength == 0 || commitLength > MaxCommitIdBytes)
            {
                return SaveResult<SaveEnvelope>.Fail(SaveStage.Envelope, SaveErrorCode.UnsupportedFormat, "Commit id length is invalid.");
            }

            if (!TryReadUtf8(bytes, ref cursor, commitLength, out var commitId))
            {
                return SaveResult<SaveEnvelope>.Fail(SaveStage.Envelope, SaveErrorCode.UnsupportedFormat, "Commit id bytes are invalid.");
            }

            if (!TryReadInt64(bytes, ref cursor, out var timestampUtcTicks))
            {
                return SaveResult<SaveEnvelope>.Fail(SaveStage.Envelope, SaveErrorCode.UnsupportedFormat, "Timestamp is invalid.");
            }

            if (!TryReadByte(bytes, ref cursor, out var algorithmLength)
                || algorithmLength == 0
                || algorithmLength > MaxAlgorithmBytes)
            {
                return SaveResult<SaveEnvelope>.Fail(SaveStage.Envelope, SaveErrorCode.UnsupportedFormat, "Integrity algorithm length is invalid.");
            }

            if (!TryReadUtf8(bytes, ref cursor, algorithmLength, out var algorithm))
            {
                return SaveResult<SaveEnvelope>.Fail(SaveStage.Envelope, SaveErrorCode.UnsupportedFormat, "Integrity algorithm bytes are invalid.");
            }

            if (!TryReadByte(bytes, ref cursor, out var digestLength)
                || digestLength == 0
                || digestLength > MaxDigestBytes)
            {
                return SaveResult<SaveEnvelope>.Fail(SaveStage.Envelope, SaveErrorCode.UnsupportedFormat, "Digest length is invalid.");
            }

            if (!TryReadBytes(bytes, ref cursor, digestLength, out var digest))
            {
                return SaveResult<SaveEnvelope>.Fail(SaveStage.Envelope, SaveErrorCode.UnsupportedFormat, "Digest bytes are invalid.");
            }

            if (!TryReadInt32(bytes, ref cursor, out var payloadLength)
                || payloadLength < 0
                || payloadLength > MaxPayloadBytes)
            {
                return SaveResult<SaveEnvelope>.Fail(SaveStage.Envelope, SaveErrorCode.UnsupportedFormat, "Payload length is invalid.");
            }

            if (!TryReadBytes(bytes, ref cursor, payloadLength, out var payload))
            {
                return SaveResult<SaveEnvelope>.Fail(SaveStage.Envelope, SaveErrorCode.UnsupportedFormat, "Payload bytes are invalid.");
            }

            if (cursor != bytes.Length)
            {
                return SaveResult<SaveEnvelope>.Fail(SaveStage.Envelope, SaveErrorCode.UnsupportedFormat, "Envelope has trailing bytes.");
            }

            return SaveResult<SaveEnvelope>.Success(
                new SaveEnvelope(schemaId, schemaVersion, commitId, timestampUtcTicks, algorithm, digest, payload));
        }

        private static bool TryReadExact(ReadOnlySpan<byte> source, ref int cursor, int length, out ReadOnlySpan<byte> slice)
        {
            if (cursor > source.Length || source.Length - cursor < length)
            {
                slice = default;
                return false;
            }

            slice = source.Slice(cursor, length);
            cursor += length;
            return true;
        }

        private static bool TryReadByte(ReadOnlySpan<byte> source, ref int cursor, out byte value)
        {
            if (!TryReadExact(source, ref cursor, 1, out var slice))
            {
                value = 0;
                return false;
            }

            value = slice[0];
            return true;
        }

        private static bool TryReadUInt16(ReadOnlySpan<byte> source, ref int cursor, out ushort value)
        {
            if (!TryReadExact(source, ref cursor, 2, out var slice))
            {
                value = 0;
                return false;
            }

            value = BinaryPrimitives.ReadUInt16LittleEndian(slice);
            return true;
        }

        private static bool TryReadInt32(ReadOnlySpan<byte> source, ref int cursor, out int value)
        {
            if (!TryReadExact(source, ref cursor, 4, out var slice))
            {
                value = 0;
                return false;
            }

            value = BinaryPrimitives.ReadInt32LittleEndian(slice);
            return true;
        }

        private static bool TryReadInt64(ReadOnlySpan<byte> source, ref int cursor, out long value)
        {
            if (!TryReadExact(source, ref cursor, 8, out var slice))
            {
                value = 0;
                return false;
            }

            value = BinaryPrimitives.ReadInt64LittleEndian(slice);
            return true;
        }

        private static bool TryReadUtf8(ReadOnlySpan<byte> source, ref int cursor, int length, out string value)
        {
            if (!TryReadExact(source, ref cursor, length, out var slice))
            {
                value = null;
                return false;
            }

            try
            {
                value = StrictUtf8.GetString(slice.ToArray());
                return !string.IsNullOrWhiteSpace(value);
            }
            catch (DecoderFallbackException)
            {
                value = null;
                return false;
            }
        }

        private static bool TryReadBytes(ReadOnlySpan<byte> source, ref int cursor, int length, out byte[] bytes)
        {
            if (!TryReadExact(source, ref cursor, length, out var slice))
            {
                bytes = null;
                return false;
            }

            bytes = slice.ToArray();
            return true;
        }
    }

    [Flags]
    public enum SaveCommitCapabilities
    {
        None = 0,
        BestEffortWrite = 1 << 0,
        AtomicReplace = 1 << 1,
        RecoverableReplace = 1 << 2
    }

    public interface ISaveCodec<TState>
    {
        SaveResult<byte[]> Encode(TState snapshot);
        SaveResult<TState> Decode(int schemaVersion, ReadOnlySpan<byte> bytes);
    }

    public interface IIntegrityProvider
    {
        string AlgorithmName { get; }
        SaveResult<byte[]> ComputeDigest(ReadOnlySpan<byte> payload);
        SaveResult Verify(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> expectedDigest);
    }

    public sealed class Sha256IntegrityProvider : IIntegrityProvider
    {
        public string AlgorithmName => "sha-256";

        public SaveResult<byte[]> ComputeDigest(ReadOnlySpan<byte> payload)
        {
            try
            {
                using (var sha256 = SHA256.Create())
                {
                    return SaveResult<byte[]>.Success(sha256.ComputeHash(payload.ToArray()));
                }
            }
            catch (Exception exception) when (exception is PlatformNotSupportedException || exception is CryptographicException)
            {
                return SaveResult<byte[]>.Fail(SaveStage.Integrity, SaveErrorCode.ProviderFailure, $"SHA-256 unavailable: {exception.Message}");
            }
        }

        public SaveResult Verify(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> expectedDigest)
        {
            var computed = ComputeDigest(payload);
            if (!computed.Succeeded)
            {
                return SaveResult.Fail(computed.Error.Stage, computed.Error.Code, computed.Error.Message);
            }

            if (!CryptographicOperations.FixedTimeEquals(computed.Value, expectedDigest.ToArray()))
            {
                return SaveResult.Fail(SaveStage.Verify, SaveErrorCode.CorruptPayload, "Payload checksum does not match.");
            }

            return SaveResult.Success();
        }
    }

    public interface ISaveMigration<TState>
    {
        int FromVersion { get; }
        int ToVersion { get; }
        TState Migrate(TState state);
    }

    public enum SaveCandidateKind
    {
        Primary = 0,
        Backup = 1,
        Staging = 2
    }

    public readonly struct SaveCandidateId : IEquatable<SaveCandidateId>
    {
        private const int MaxLength = 64;
        private readonly string _value;

        private SaveCandidateId(string value)
        {
            _value = value;
        }

        public string Value => _value ?? string.Empty;

        public static SaveResult<SaveCandidateId> TryCreate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return SaveResult<SaveCandidateId>.Fail(SaveStage.Read, SaveErrorCode.InvalidSlot, "Candidate id cannot be empty.");
            }

            if (value.Length > MaxLength)
            {
                return SaveResult<SaveCandidateId>.Fail(SaveStage.Read, SaveErrorCode.InvalidSlot, "Candidate id exceeds max length.");
            }

            for (var index = 0; index < value.Length; index++)
            {
                var current = value[index];
                if (!(char.IsLetterOrDigit(current) || current == '-' || current == '_'))
                {
                    return SaveResult<SaveCandidateId>.Fail(SaveStage.Read, SaveErrorCode.InvalidSlot, "Candidate id contains unsupported characters.");
                }
            }

            return SaveResult<SaveCandidateId>.Success(new SaveCandidateId(value));
        }

        public bool Equals(SaveCandidateId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SaveCandidateId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public override string ToString() => Value;
        public static bool operator ==(SaveCandidateId left, SaveCandidateId right) => left.Equals(right);
        public static bool operator !=(SaveCandidateId left, SaveCandidateId right) => !left.Equals(right);
    }

    public sealed class SaveReadCandidate
    {
        private readonly byte[] _bytes;

        public SaveReadCandidate(SaveCandidateKind kind, SaveCandidateId id, ReadOnlySpan<byte> bytes)
        {
            if (kind != SaveCandidateKind.Primary
                && kind != SaveCandidateKind.Backup
                && kind != SaveCandidateKind.Staging)
            {
                throw new ArgumentException("Candidate kind is undefined.", nameof(kind));
            }

            if (string.IsNullOrEmpty(id.Value))
            {
                throw new ArgumentException("Candidate id cannot be empty.", nameof(id));
            }

            if (bytes.Length == 0)
            {
                throw new ArgumentException("Candidate bytes cannot be empty.", nameof(bytes));
            }

            Kind = kind;
            Id = id;
            _bytes = CopyCandidateBytes(bytes);
            Bytes = _bytes;
        }

        public SaveCandidateKind Kind { get; }
        public SaveCandidateId Id { get; }
        public ReadOnlyMemory<byte> Bytes { get; }

        private static byte[] CopyCandidateBytes(ReadOnlySpan<byte> source)
        {
            var copy = new byte[source.Length];
            source.CopyTo(copy);
            return copy;
        }
    }

    public readonly struct SaveReadCandidateSet
    {
        private static readonly SaveReadCandidate[] EmptyCandidates = Array.Empty<SaveReadCandidate>();
        private readonly SaveReadCandidate[] _candidates;

        public SaveReadCandidateSet(IReadOnlyList<SaveReadCandidate> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                _candidates = EmptyCandidates;
                return;
            }

            var snapshot = new SaveReadCandidate[candidates.Count];
            for (var index = 0; index < candidates.Count; index++)
            {
                snapshot[index] = candidates[index];
            }

            _candidates = snapshot;
        }

        public IReadOnlyList<SaveReadCandidate> Candidates => _candidates ?? EmptyCandidates;

        public static SaveReadCandidateSet Empty => new SaveReadCandidateSet(EmptyCandidates);
    }

    public enum SaveRecoveryPolicy
    {
        PrimaryOnly = 0,
        PrimaryThenBackup = 1
    }

    public readonly struct SaveLoadReceipt<TState>
    {
        public SaveLoadReceipt(
            TState state,
            SaveCandidateKind selectedCandidateKind,
            SaveCandidateId selectedCandidateId,
            bool recoveryOccurred,
            SaveDiagnostic? primaryFailureDiagnostic = null)
        {
            State = state;
            SelectedCandidateKind = selectedCandidateKind;
            SelectedCandidateId = selectedCandidateId;
            RecoveryOccurred = recoveryOccurred;
            PrimaryFailureDiagnostic = primaryFailureDiagnostic;
        }

        public TState State { get; }
        public SaveCandidateKind SelectedCandidateKind { get; }
        public SaveCandidateId SelectedCandidateId { get; }
        public bool RecoveryOccurred { get; }
        public SaveDiagnostic? PrimaryFailureDiagnostic { get; }
    }

    public interface ISaveStore
    {
        SaveCommitCapabilities Capabilities { get; }
        SaveResult<SaveReadCandidateSet> ReadCandidates(SaveSlotId slotId);
        SaveCommitResult Commit(SaveSlotId slotId, ReadOnlyMemory<byte> envelopeBytes, SaveCommitCapabilities requiredCapabilities);
    }

    public readonly struct SaveCommitNotification<TState>
    {
        public SaveCommitNotification(SaveSlotId slotId, TState snapshot, ReadOnlyMemory<byte> envelopeBytes)
        {
            SlotId = slotId;
            Snapshot = snapshot;
            EnvelopeBytes = envelopeBytes;
        }

        public SaveSlotId SlotId { get; }
        public TState Snapshot { get; }
        public ReadOnlyMemory<byte> EnvelopeBytes { get; }
    }

    public interface ISaveCommitObserver<TState>
    {
        void OnCommitted(SaveCommitNotification<TState> notification);
    }

    public sealed class MigrationPipeline<TState>
    {
        private readonly Dictionary<int, ISaveMigration<TState>> _edgeByFromVersion;
        private readonly SaveErrorCode? _graphError;

        public MigrationPipeline(IEnumerable<ISaveMigration<TState>> migrations)
        {
            if (migrations == null)
            {
                throw new ArgumentNullException(nameof(migrations));
            }

            _edgeByFromVersion = new Dictionary<int, ISaveMigration<TState>>();
            SaveErrorCode? graphError = null;
            foreach (var migration in migrations)
            {
                if (migration == null)
                {
                    continue;
                }

                if (migration.ToVersion <= migration.FromVersion)
                {
                    graphError = SelectGraphError(graphError, SaveErrorCode.NonMonotonicMigration);
                    continue;
                }

                if (_edgeByFromVersion.ContainsKey(migration.FromVersion))
                {
                    graphError = SelectGraphError(graphError, SaveErrorCode.AmbiguousMigration);
                    continue;
                }

                _edgeByFromVersion[migration.FromVersion] = migration;
            }

            _graphError = graphError;
        }

        public SaveResult<TState> Apply(int storedVersion, int targetVersion, TState state)
        {
            if (_graphError.HasValue)
            {
                return SaveResult<TState>.Fail(SaveStage.Migrate, _graphError.Value, "Migration graph is invalid.");
            }

            if (storedVersion > targetVersion)
            {
                return SaveResult<TState>.Fail(SaveStage.Migrate, SaveErrorCode.FutureSchema, "Stored schema version is newer than target schema version.");
            }

            var currentState = state;
            var currentVersion = storedVersion;
            var visited = new HashSet<int>();
            while (currentVersion < targetVersion)
            {
                if (!visited.Add(currentVersion))
                {
                    return SaveResult<TState>.Fail(SaveStage.Migrate, SaveErrorCode.CyclicMigration, "Migration graph is cyclic.");
                }

                if (!_edgeByFromVersion.TryGetValue(currentVersion, out var edge))
                {
                    return SaveResult<TState>.Fail(SaveStage.Migrate, SaveErrorCode.MissingMigration, "Migration path is incomplete.");
                }

                if (edge.ToVersion <= currentVersion)
                {
                    return SaveResult<TState>.Fail(SaveStage.Migrate, SaveErrorCode.NonMonotonicMigration, "Migration edge is non-monotonic.");
                }

                if (edge.ToVersion > targetVersion)
                {
                    return SaveResult<TState>.Fail(SaveStage.Migrate, SaveErrorCode.OvershootMigration, "Migration edge overshoots target schema version.");
                }

                try
                {
                    currentState = edge.Migrate(currentState);
                }
                catch (Exception exception)
                {
                    return SaveResult<TState>.Fail(SaveStage.Migrate, SaveErrorCode.ProviderFailure, $"Migration threw exception: {exception.Message}");
                }

                currentVersion = edge.ToVersion;
            }

            return SaveResult<TState>.Success(currentState);
        }

        private static SaveErrorCode? SelectGraphError(SaveErrorCode? current, SaveErrorCode candidate)
        {
            if (!current.HasValue || GetGraphErrorPriority(candidate) > GetGraphErrorPriority(current.Value))
            {
                return candidate;
            }

            return current;
        }

        private static int GetGraphErrorPriority(SaveErrorCode code)
        {
            switch (code)
            {
                case SaveErrorCode.NonMonotonicMigration:
                    return 2;
                case SaveErrorCode.AmbiguousMigration:
                    return 1;
                case SaveErrorCode.CyclicMigration:
                    return 0;
                default:
                    return -1;
            }
        }
    }

    public static class SaveRecoveryCandidateSelector
    {
        private static readonly SaveDiagnostic PrimaryMissingDiagnostic = new SaveDiagnostic(
            SaveDiagnosticSeverity.Error,
            SaveStage.Read,
            SaveErrorCode.NotFound,
            "Primary candidate is missing.");

        public static bool IsEligiblePrimaryFailureForBackup(SaveError error)
        {
            if (error.Code == SaveErrorCode.NotFound)
            {
                return true;
            }

            if (error.Stage == SaveStage.Envelope && error.Code == SaveErrorCode.UnsupportedFormat)
            {
                return true;
            }

            if (error.Stage == SaveStage.Verify && error.Code == SaveErrorCode.CorruptPayload)
            {
                return true;
            }

            return false;
        }

        public static SaveResult<SaveRecoverySelection> Select(
            SaveReadCandidateSet candidateSet,
            SaveRecoveryPolicy recoveryPolicy,
            string expectedSchemaId,
            IIntegrityProvider integrityProvider)
        {
            if (integrityProvider == null)
            {
                return SaveResult<SaveRecoverySelection>.Fail(SaveStage.Read, SaveErrorCode.ProviderFailure, "Integrity provider is required.");
            }

            if (string.IsNullOrWhiteSpace(expectedSchemaId))
            {
                return SaveResult<SaveRecoverySelection>.Fail(SaveStage.Read, SaveErrorCode.ProviderFailure, "Schema id is required.");
            }

            if (!IsDefinedRecoveryPolicy(recoveryPolicy))
            {
                return SaveResult<SaveRecoverySelection>.Fail(SaveStage.Read, SaveErrorCode.ProviderFailure, "Recovery policy is undefined.");
            }

            var structure = ValidateCandidateStructure(candidateSet);
            if (!structure.Succeeded)
            {
                return SaveResult<SaveRecoverySelection>.Fail(structure.Error.Stage, structure.Error.Code, structure.Error.Message);
            }

            var primary = structure.Value.Primary;
            var backup = structure.Value.Backup;

            if (primary == null && backup == null)
            {
                return SaveResult<SaveRecoverySelection>.Fail(SaveStage.Read, SaveErrorCode.NotFound, "No selectable save candidates exist.");
            }

            if (primary != null)
            {
                var primaryAccepted = TryAcceptCandidateEnvelope(primary, expectedSchemaId, integrityProvider);
                if (primaryAccepted.Succeeded)
                {
                    return SaveResult<SaveRecoverySelection>.Success(
                        new SaveRecoverySelection(primary, false, null));
                }

                if (recoveryPolicy == SaveRecoveryPolicy.PrimaryOnly || backup == null)
                {
                    return SaveResult<SaveRecoverySelection>.Fail(
                        primaryAccepted.Error.Stage,
                        primaryAccepted.Error.Code,
                        primaryAccepted.Error.Message);
                }

                if (!IsEligiblePrimaryFailureForBackup(primaryAccepted.Error))
                {
                    return SaveResult<SaveRecoverySelection>.Fail(
                        primaryAccepted.Error.Stage,
                        primaryAccepted.Error.Code,
                        primaryAccepted.Error.Message);
                }

                var primaryDiagnostic = new SaveDiagnostic(
                    SaveDiagnosticSeverity.Error,
                    primaryAccepted.Error.Stage,
                    primaryAccepted.Error.Code,
                    primaryAccepted.Error.Message);

                var backupAccepted = TryAcceptCandidateEnvelope(backup, expectedSchemaId, integrityProvider);
                if (!backupAccepted.Succeeded)
                {
                    return SaveResult<SaveRecoverySelection>.Fail(
                        primaryAccepted.Error.Stage,
                        primaryAccepted.Error.Code,
                        primaryAccepted.Error.Message);
                }

                return SaveResult<SaveRecoverySelection>.Success(
                    new SaveRecoverySelection(backup, true, primaryDiagnostic));
            }

            if (recoveryPolicy == SaveRecoveryPolicy.PrimaryOnly)
            {
                return SaveResult<SaveRecoverySelection>.Fail(SaveStage.Read, SaveErrorCode.NotFound, "Primary candidate is required.");
            }

            var backupOnlyAccepted = TryAcceptCandidateEnvelope(backup, expectedSchemaId, integrityProvider);
            if (!backupOnlyAccepted.Succeeded)
            {
                return SaveResult<SaveRecoverySelection>.Fail(
                    backupOnlyAccepted.Error.Stage,
                    backupOnlyAccepted.Error.Code,
                    backupOnlyAccepted.Error.Message);
            }

            return SaveResult<SaveRecoverySelection>.Success(
                new SaveRecoverySelection(backup, true, PrimaryMissingDiagnostic));
        }

        private static bool IsDefinedCandidateKind(SaveCandidateKind kind)
        {
            return kind == SaveCandidateKind.Primary
                || kind == SaveCandidateKind.Backup
                || kind == SaveCandidateKind.Staging;
        }

        private static bool IsDefinedRecoveryPolicy(SaveRecoveryPolicy recoveryPolicy)
        {
            return recoveryPolicy == SaveRecoveryPolicy.PrimaryOnly
                || recoveryPolicy == SaveRecoveryPolicy.PrimaryThenBackup;
        }

        private static SaveResult<(SaveReadCandidate Primary, SaveReadCandidate Backup)> ValidateCandidateStructure(SaveReadCandidateSet candidateSet)
        {
            SaveReadCandidate primary = null;
            SaveReadCandidate backup = null;
            var seenIds = new HashSet<string>(StringComparer.Ordinal);

            for (var index = 0; index < candidateSet.Candidates.Count; index++)
            {
                var candidate = candidateSet.Candidates[index];
                if (candidate == null)
                {
                    return SaveResult<(SaveReadCandidate, SaveReadCandidate)>.Fail(
                        SaveStage.Read,
                        SaveErrorCode.ProviderFailure,
                        "Candidate set contains a null entry.");
                }

                if (string.IsNullOrEmpty(candidate.Id.Value))
                {
                    return SaveResult<(SaveReadCandidate, SaveReadCandidate)>.Fail(
                        SaveStage.Read,
                        SaveErrorCode.InvalidSlot,
                        "Candidate set contains an empty candidate id.");
                }

                if (!seenIds.Add(candidate.Id.Value))
                {
                    return SaveResult<(SaveReadCandidate, SaveReadCandidate)>.Fail(
                        SaveStage.Read,
                        SaveErrorCode.ProviderFailure,
                        "Candidate set contains duplicate candidate ids.");
                }

                if (!IsDefinedCandidateKind(candidate.Kind))
                {
                    return SaveResult<(SaveReadCandidate, SaveReadCandidate)>.Fail(
                        SaveStage.Read,
                        SaveErrorCode.ProviderFailure,
                        "Candidate set contains an undefined candidate kind.");
                }

                switch (candidate.Kind)
                {
                    case SaveCandidateKind.Primary:
                        if (primary != null)
                        {
                            return SaveResult<(SaveReadCandidate, SaveReadCandidate)>.Fail(
                                SaveStage.Read,
                                SaveErrorCode.ProviderFailure,
                                "Candidate set contains duplicate primary candidates.");
                        }

                        primary = candidate;
                        break;
                    case SaveCandidateKind.Backup:
                        if (backup != null)
                        {
                            return SaveResult<(SaveReadCandidate, SaveReadCandidate)>.Fail(
                                SaveStage.Read,
                                SaveErrorCode.ProviderFailure,
                                "Candidate set contains duplicate backup candidates.");
                        }

                        backup = candidate;
                        break;
                    case SaveCandidateKind.Staging:
                        break;
                    default:
                        return SaveResult<(SaveReadCandidate, SaveReadCandidate)>.Fail(
                            SaveStage.Read,
                            SaveErrorCode.ProviderFailure,
                            "Candidate set contains an undefined candidate kind.");
                }
            }

            return SaveResult<(SaveReadCandidate, SaveReadCandidate)>.Success((primary, backup));
        }

        private static SaveResult TryAcceptCandidateEnvelope(
            SaveReadCandidate candidate,
            string expectedSchemaId,
            IIntegrityProvider integrityProvider)
        {
            var decodedEnvelope = SaveEnvelopeBinaryCodec.Decode(candidate.Bytes.Span);
            if (!decodedEnvelope.Succeeded)
            {
                return SaveResult.Fail(decodedEnvelope.Error.Stage, decodedEnvelope.Error.Code, decodedEnvelope.Error.Message);
            }

            var envelope = decodedEnvelope.Value;
            if (!string.Equals(envelope.SchemaId, expectedSchemaId, StringComparison.Ordinal))
            {
                return SaveResult.Fail(SaveStage.Envelope, SaveErrorCode.UnsupportedFormat, "Schema id mismatch.");
            }

            if (!string.Equals(envelope.IntegrityAlgorithm, integrityProvider.AlgorithmName, StringComparison.Ordinal))
            {
                return SaveResult.Fail(SaveStage.Verify, SaveErrorCode.UnsupportedFormat, "Envelope integrity algorithm does not match configured provider.");
            }

            SaveResult verified;
            try
            {
                verified = integrityProvider.Verify(envelope.Payload.Span, envelope.IntegrityDigest.Span);
            }
            catch (Exception exception)
            {
                return SaveResult.Fail(SaveStage.Verify, SaveErrorCode.ProviderFailure, $"Integrity verify threw exception: {exception.Message}");
            }

            if (!verified.Succeeded)
            {
                return SaveResult.Fail(verified.Error.Stage, verified.Error.Code, verified.Error.Message);
            }

            return SaveResult.Success();
        }
    }

    public readonly struct SaveRecoverySelection
    {
        public SaveRecoverySelection(SaveReadCandidate selectedCandidate, bool recoveryOccurred, SaveDiagnostic? primaryFailureDiagnostic)
        {
            SelectedCandidate = selectedCandidate ?? throw new ArgumentNullException(nameof(selectedCandidate));
            RecoveryOccurred = recoveryOccurred;
            PrimaryFailureDiagnostic = primaryFailureDiagnostic;
        }

        public SaveReadCandidate SelectedCandidate { get; }
        public bool RecoveryOccurred { get; }
        public SaveDiagnostic? PrimaryFailureDiagnostic { get; }
    }

    public sealed class SaveCoordinator<TState>
    {
        private readonly string _schemaId;
        private readonly int _currentSchemaVersion;
        private readonly string _commitId;
        private readonly ISaveCodec<TState> _codec;
        private readonly IIntegrityProvider _integrityProvider;
        private readonly MigrationPipeline<TState> _migrationPipeline;
        private readonly ISaveStore _store;
        private readonly SaveCommitCapabilities _requiredCapabilities;
        private readonly SaveRecoveryPolicy _recoveryPolicy;
        private readonly List<ISaveCommitObserver<TState>> _postCommitObservers;

        public SaveCoordinator(
            string schemaId,
            int currentSchemaVersion,
            string commitId,
            ISaveCodec<TState> codec,
            IIntegrityProvider integrityProvider,
            MigrationPipeline<TState> migrationPipeline,
            ISaveStore store,
            SaveCommitCapabilities requiredCapabilities,
            IEnumerable<ISaveCommitObserver<TState>> postCommitObservers = null,
            SaveRecoveryPolicy recoveryPolicy = SaveRecoveryPolicy.PrimaryOnly)
        {
            _schemaId = string.IsNullOrWhiteSpace(schemaId) ? throw new ArgumentException("Schema id is required.", nameof(schemaId)) : schemaId;
            _currentSchemaVersion = currentSchemaVersion >= 0 ? currentSchemaVersion : throw new ArgumentOutOfRangeException(nameof(currentSchemaVersion));
            _commitId = string.IsNullOrWhiteSpace(commitId) ? throw new ArgumentException("Commit id is required.", nameof(commitId)) : commitId;
            _codec = codec ?? throw new ArgumentNullException(nameof(codec));
            _integrityProvider = integrityProvider ?? throw new ArgumentNullException(nameof(integrityProvider));
            _migrationPipeline = migrationPipeline ?? throw new ArgumentNullException(nameof(migrationPipeline));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _requiredCapabilities = requiredCapabilities;
            _recoveryPolicy = recoveryPolicy;
            _postCommitObservers = new List<ISaveCommitObserver<TState>>();
            if (postCommitObservers != null)
            {
                foreach (var observer in postCommitObservers)
                {
                    if (observer != null)
                    {
                        _postCommitObservers.Add(observer);
                    }
                }
            }
        }

        public void RegisterPostCommitObserver(ISaveCommitObserver<TState> observer)
        {
            if (observer == null)
            {
                throw new ArgumentNullException(nameof(observer));
            }

            _postCommitObservers.Add(observer);
        }

        public SaveCommitResult Save(SaveSlotId slotId, TState snapshot, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(slotId.Value))
            {
                return SaveCommitResult.NotCommitted(SaveStage.Snapshot, SaveErrorCode.InvalidSlot, "Save slot is invalid.", true);
            }

            SaveResult<byte[]> encoded;
            try
            {
                encoded = _codec.Encode(snapshot);
            }
            catch (Exception exception)
            {
                return SaveCommitResult.NotCommitted(SaveStage.Encode, SaveErrorCode.ProviderFailure, $"Codec encode threw exception: {exception.Message}", true);
            }

            if (!encoded.Succeeded)
            {
                return SaveCommitResult.NotCommitted(encoded.Error.Stage, encoded.Error.Code, encoded.Error.Message, true);
            }

            SaveResult<byte[]> digest;
            try
            {
                digest = _integrityProvider.ComputeDigest(encoded.Value);
            }
            catch (Exception exception)
            {
                return SaveCommitResult.NotCommitted(SaveStage.Integrity, SaveErrorCode.ProviderFailure, $"Integrity provider threw exception: {exception.Message}", true);
            }

            if (!digest.Succeeded)
            {
                return SaveCommitResult.NotCommitted(digest.Error.Stage, digest.Error.Code, digest.Error.Message, true);
            }

            var envelope = new SaveEnvelope(
                _schemaId,
                _currentSchemaVersion,
                _commitId,
                DateTime.UtcNow.Ticks,
                _integrityProvider.AlgorithmName,
                digest.Value,
                encoded.Value);
            var serializedEnvelope = SaveEnvelopeBinaryCodec.Encode(envelope);
            if (!serializedEnvelope.Succeeded)
            {
                return SaveCommitResult.NotCommitted(
                    serializedEnvelope.Error.Stage,
                    serializedEnvelope.Error.Code,
                    serializedEnvelope.Error.Message,
                    true);
            }

            if ((_store.Capabilities & _requiredCapabilities) != _requiredCapabilities)
            {
                return SaveCommitResult.NotCommitted(
                    SaveStage.Commit,
                    SaveErrorCode.UnsupportedCommitCapability,
                    $"Store capabilities {_store.Capabilities} do not satisfy required {_requiredCapabilities}.",
                    true);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return SaveCommitResult.NotCommitted(SaveStage.Commit, SaveErrorCode.Cancelled, "Save cancelled before commit.", true);
            }

            SaveCommitResult committed;
            try
            {
                committed = _store.Commit(slotId, serializedEnvelope.Value, _requiredCapabilities);
            }
            catch (Exception exception)
            {
                return SaveCommitResult.NotCommitted(
                    SaveStage.Commit,
                    SaveErrorCode.ProviderFailure,
                    $"Store commit threw exception: {exception.Message}",
                    false);
            }

            if (committed.IsContradictory(out var contradiction))
            {
                return SaveCommitResult.NotCommitted(
                    SaveStage.Commit,
                    SaveErrorCode.ProviderFailure,
                    $"Store returned contradictory commit outcome: {contradiction}",
                    false);
            }

            if (!committed.Committed)
            {
                return committed;
            }

            List<SaveDiagnostic> diagnostics = null;
            if (committed.Diagnostics.Count > 0)
            {
                diagnostics = new List<SaveDiagnostic>(committed.Diagnostics);
            }

            if (_postCommitObservers.Count > 0)
            {
                var notification = new SaveCommitNotification<TState>(slotId, snapshot, serializedEnvelope.Value);
                for (var index = 0; index < _postCommitObservers.Count; index++)
                {
                    var observer = _postCommitObservers[index];
                    try
                    {
                        observer.OnCommitted(notification);
                    }
                    catch (Exception exception)
                    {
                        if (diagnostics == null)
                        {
                            diagnostics = new List<SaveDiagnostic>();
                        }

                        diagnostics.Add(
                            new SaveDiagnostic(
                                SaveDiagnosticSeverity.Warning,
                                SaveStage.Commit,
                                SaveErrorCode.ProviderFailure,
                                $"Post-commit observer[{index}] threw exception: {exception.Message}"));
                    }
                }
            }

            return diagnostics == null ? committed : committed.WithDiagnostics(diagnostics.ToArray());
        }

        public SaveResult<SaveLoadReceipt<TState>> LoadValidated(SaveSlotId slotId, Func<TState, SaveResult> validator)
        {
            if (validator == null)
            {
                return SaveResult<SaveLoadReceipt<TState>>.Fail(SaveStage.Validate, SaveErrorCode.ProviderFailure, "Validator delegate is required.");
            }

            if (string.IsNullOrEmpty(slotId.Value))
            {
                return SaveResult<SaveLoadReceipt<TState>>.Fail(SaveStage.Snapshot, SaveErrorCode.InvalidSlot, "Save slot is invalid.");
            }

            SaveResult<SaveReadCandidateSet> read;
            try
            {
                read = _store.ReadCandidates(slotId);
            }
            catch (Exception exception)
            {
                return SaveResult<SaveLoadReceipt<TState>>.Fail(SaveStage.Read, SaveErrorCode.ProviderFailure, $"Store read threw exception: {exception.Message}");
            }

            if (!read.Succeeded)
            {
                return SaveResult<SaveLoadReceipt<TState>>.Fail(read.Error.Stage, read.Error.Code, read.Error.Message);
            }

            SaveResult<SaveRecoverySelection> selected;
            try
            {
                selected = SaveRecoveryCandidateSelector.Select(
                    read.Value,
                    _recoveryPolicy,
                    _schemaId,
                    _integrityProvider);
            }
            catch (Exception exception)
            {
                return SaveResult<SaveLoadReceipt<TState>>.Fail(SaveStage.Read, SaveErrorCode.ProviderFailure, $"Recovery selection threw exception: {exception.Message}");
            }

            if (!selected.Succeeded)
            {
                return SaveResult<SaveLoadReceipt<TState>>.Fail(selected.Error.Stage, selected.Error.Code, selected.Error.Message);
            }

            var decodedEnvelope = SaveEnvelopeBinaryCodec.Decode(selected.Value.SelectedCandidate.Bytes.Span);
            if (!decodedEnvelope.Succeeded)
            {
                return SaveResult<SaveLoadReceipt<TState>>.Fail(decodedEnvelope.Error.Stage, decodedEnvelope.Error.Code, decodedEnvelope.Error.Message);
            }

            var envelope = decodedEnvelope.Value;
            if (envelope.SchemaVersion > _currentSchemaVersion)
            {
                return SaveResult<SaveLoadReceipt<TState>>.Fail(SaveStage.Migrate, SaveErrorCode.FutureSchema, "Stored schema is newer than supported schema.");
            }

            SaveResult<TState> decodedState;
            try
            {
                decodedState = _codec.Decode(envelope.SchemaVersion, envelope.Payload.Span);
            }
            catch (Exception exception)
            {
                return SaveResult<SaveLoadReceipt<TState>>.Fail(SaveStage.Decode, SaveErrorCode.ProviderFailure, $"Codec decode threw exception: {exception.Message}");
            }

            if (!decodedState.Succeeded)
            {
                return SaveResult<SaveLoadReceipt<TState>>.Fail(decodedState.Error.Stage, decodedState.Error.Code, decodedState.Error.Message);
            }

            var migrated = _migrationPipeline.Apply(envelope.SchemaVersion, _currentSchemaVersion, decodedState.Value);
            if (!migrated.Succeeded)
            {
                return SaveResult<SaveLoadReceipt<TState>>.Fail(migrated.Error.Stage, migrated.Error.Code, migrated.Error.Message);
            }

            SaveResult validated;
            try
            {
                validated = validator(migrated.Value);
            }
            catch (Exception exception)
            {
                return SaveResult<SaveLoadReceipt<TState>>.Fail(SaveStage.Validate, SaveErrorCode.ProviderFailure, $"Validator delegate threw exception: {exception.Message}");
            }

            if (!validated.Succeeded)
            {
                return SaveResult<SaveLoadReceipt<TState>>.Fail(
                    SaveStage.Validate,
                    validated.Error.Code == SaveErrorCode.None ? SaveErrorCode.ValidateRejected : validated.Error.Code,
                    validated.Error.Message);
            }

            var receipt = new SaveLoadReceipt<TState>(
                migrated.Value,
                selected.Value.SelectedCandidate.Kind,
                selected.Value.SelectedCandidate.Id,
                selected.Value.RecoveryOccurred,
                selected.Value.PrimaryFailureDiagnostic);

            return SaveResult<SaveLoadReceipt<TState>>.Success(receipt);
        }

        public SaveResult LoadAndApply(
            SaveSlotId slotId,
            Func<TState, SaveResult> validator,
            Func<TState, SaveResult> apply)
        {
            if (apply == null)
            {
                return SaveResult.Fail(SaveStage.Apply, SaveErrorCode.ProviderFailure, "Apply delegate is required.");
            }

            var loaded = LoadValidated(slotId, validator);
            if (!loaded.Succeeded)
            {
                return SaveResult.Fail(loaded.Error.Stage, loaded.Error.Code, loaded.Error.Message);
            }

            SaveResult applied;
            try
            {
                applied = apply(loaded.Value.State);
            }
            catch (Exception exception)
            {
                return SaveResult.Fail(SaveStage.Apply, SaveErrorCode.ProviderFailure, $"Apply delegate threw exception: {exception.Message}");
            }

            if (!applied.Succeeded)
            {
                return SaveResult.Fail(SaveStage.Apply, applied.Error.Code, applied.Error.Message);
            }

            return SaveResult.Success();
        }
    }
}
