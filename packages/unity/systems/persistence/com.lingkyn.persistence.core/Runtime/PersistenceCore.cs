using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

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
        CyclicMigration,
        NonMonotonicMigration,
        CorruptPayload,
        UnsupportedCommitCapability,
        IoDenied,
        OutOfSpace,
        Cancelled,
        ValidateRejected,
        ProviderFailure
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
        public override int GetHashCode() => ((int)Stage * 397) ^ (int)Code ^ StringComparer.Ordinal.GetHashCode(Message);
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

            SchemaId = schemaId;
            SchemaVersion = schemaVersion;
            CommitId = commitId;
            TimestampUtcTicks = timestampUtcTicks;
            IntegrityAlgorithm = integrityAlgorithm;
            IntegrityDigest = integrityDigest;
            Payload = payload;
        }

        public string SchemaId { get; }
        public int SchemaVersion { get; }
        public string CommitId { get; }
        public long TimestampUtcTicks { get; }
        public string IntegrityAlgorithm { get; }
        public byte[] IntegrityDigest { get; }
        public byte[] Payload { get; }
    }

    public static class SaveEnvelopeBinaryCodec
    {
        private static readonly byte[] Magic = { (byte)'L', (byte)'P', (byte)'S', (byte)'C' };
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

            var schemaBytes = Encoding.UTF8.GetBytes(envelope.SchemaId);
            var commitBytes = Encoding.UTF8.GetBytes(envelope.CommitId);
            var algorithmBytes = Encoding.UTF8.GetBytes(envelope.IntegrityAlgorithm);
            var digestBytes = envelope.IntegrityDigest;
            var payloadBytes = envelope.Payload;

            if (schemaBytes.Length == 0 || schemaBytes.Length > MaxSchemaIdBytes
                || commitBytes.Length == 0 || commitBytes.Length > MaxCommitIdBytes
                || algorithmBytes.Length == 0 || algorithmBytes.Length > MaxAlgorithmBytes
                || digestBytes.Length == 0 || digestBytes.Length > MaxDigestBytes
                || payloadBytes.Length > MaxPayloadBytes)
            {
                return SaveResult<byte[]>.Fail(SaveStage.Envelope, SaveErrorCode.UnsupportedFormat, "Envelope exceeds codec bounds.");
            }

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
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

            value = Encoding.UTF8.GetString(slice);
            return !string.IsNullOrWhiteSpace(value);
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
        SaveResult<TState> Decode(ReadOnlySpan<byte> bytes);
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

    public interface ISaveStore
    {
        SaveCommitCapabilities Capabilities { get; }
        SaveResult<byte[]> Read(SaveSlotId slotId);
        SaveResult Commit(SaveSlotId slotId, ReadOnlyMemory<byte> envelopeBytes, SaveCommitCapabilities requiredCapabilities);
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
            foreach (var migration in migrations)
            {
                if (migration == null)
                {
                    continue;
                }

                if (migration.ToVersion <= migration.FromVersion)
                {
                    _graphError = SaveErrorCode.NonMonotonicMigration;
                    continue;
                }

                if (_edgeByFromVersion.ContainsKey(migration.FromVersion))
                {
                    _graphError = SaveErrorCode.AmbiguousMigration;
                    continue;
                }

                _edgeByFromVersion[migration.FromVersion] = migration;
            }
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

                currentState = edge.Migrate(currentState);
                currentVersion = edge.ToVersion;
            }

            return SaveResult<TState>.Success(currentState);
        }
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

        public SaveCoordinator(
            string schemaId,
            int currentSchemaVersion,
            string commitId,
            ISaveCodec<TState> codec,
            IIntegrityProvider integrityProvider,
            MigrationPipeline<TState> migrationPipeline,
            ISaveStore store,
            SaveCommitCapabilities requiredCapabilities)
        {
            _schemaId = string.IsNullOrWhiteSpace(schemaId) ? throw new ArgumentException("Schema id is required.", nameof(schemaId)) : schemaId;
            _currentSchemaVersion = currentSchemaVersion >= 0 ? currentSchemaVersion : throw new ArgumentOutOfRangeException(nameof(currentSchemaVersion));
            _commitId = string.IsNullOrWhiteSpace(commitId) ? throw new ArgumentException("Commit id is required.", nameof(commitId)) : commitId;
            _codec = codec ?? throw new ArgumentNullException(nameof(codec));
            _integrityProvider = integrityProvider ?? throw new ArgumentNullException(nameof(integrityProvider));
            _migrationPipeline = migrationPipeline ?? throw new ArgumentNullException(nameof(migrationPipeline));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _requiredCapabilities = requiredCapabilities;
        }

        public SaveResult Save(SaveSlotId slotId, TState snapshot)
        {
            if (string.IsNullOrEmpty(slotId.Value))
            {
                return SaveResult.Fail(SaveStage.Snapshot, SaveErrorCode.InvalidSlot, "Save slot is invalid.");
            }

            var encoded = _codec.Encode(snapshot);
            if (!encoded.Succeeded)
            {
                return SaveResult.Fail(encoded.Error.Stage, encoded.Error.Code, encoded.Error.Message);
            }

            var digest = _integrityProvider.ComputeDigest(encoded.Value);
            if (!digest.Succeeded)
            {
                return SaveResult.Fail(digest.Error.Stage, digest.Error.Code, digest.Error.Message);
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
                return SaveResult.Fail(serializedEnvelope.Error.Stage, serializedEnvelope.Error.Code, serializedEnvelope.Error.Message);
            }

            if ((_store.Capabilities & _requiredCapabilities) != _requiredCapabilities)
            {
                return SaveResult.Fail(
                    SaveStage.Commit,
                    SaveErrorCode.UnsupportedCommitCapability,
                    $"Store capabilities {_store.Capabilities} do not satisfy required {_requiredCapabilities}.");
            }

            var committed = _store.Commit(slotId, serializedEnvelope.Value, _requiredCapabilities);
            if (!committed.Succeeded)
            {
                return committed;
            }

            return SaveResult.Success();
        }

        public SaveResult<TState> LoadValidated(SaveSlotId slotId, Func<TState, SaveResult> validator)
        {
            if (validator == null)
            {
                throw new ArgumentNullException(nameof(validator));
            }

            if (string.IsNullOrEmpty(slotId.Value))
            {
                return SaveResult<TState>.Fail(SaveStage.Snapshot, SaveErrorCode.InvalidSlot, "Save slot is invalid.");
            }

            var read = _store.Read(slotId);
            if (!read.Succeeded)
            {
                return SaveResult<TState>.Fail(read.Error.Stage, read.Error.Code, read.Error.Message);
            }

            var decodedEnvelope = SaveEnvelopeBinaryCodec.Decode(read.Value);
            if (!decodedEnvelope.Succeeded)
            {
                return SaveResult<TState>.Fail(decodedEnvelope.Error.Stage, decodedEnvelope.Error.Code, decodedEnvelope.Error.Message);
            }

            var envelope = decodedEnvelope.Value;
            if (!string.Equals(envelope.SchemaId, _schemaId, StringComparison.Ordinal))
            {
                return SaveResult<TState>.Fail(SaveStage.Envelope, SaveErrorCode.UnsupportedFormat, "Schema id mismatch.");
            }

            if (envelope.SchemaVersion > _currentSchemaVersion)
            {
                return SaveResult<TState>.Fail(SaveStage.Migrate, SaveErrorCode.FutureSchema, "Stored schema is newer than supported schema.");
            }

            var verified = _integrityProvider.Verify(envelope.Payload, envelope.IntegrityDigest);
            if (!verified.Succeeded)
            {
                return SaveResult<TState>.Fail(verified.Error.Stage, verified.Error.Code, verified.Error.Message);
            }

            var decodedState = _codec.Decode(envelope.Payload);
            if (!decodedState.Succeeded)
            {
                return SaveResult<TState>.Fail(decodedState.Error.Stage, decodedState.Error.Code, decodedState.Error.Message);
            }

            var migrated = _migrationPipeline.Apply(envelope.SchemaVersion, _currentSchemaVersion, decodedState.Value);
            if (!migrated.Succeeded)
            {
                return SaveResult<TState>.Fail(migrated.Error.Stage, migrated.Error.Code, migrated.Error.Message);
            }

            var validated = validator(migrated.Value);
            if (!validated.Succeeded)
            {
                return SaveResult<TState>.Fail(
                    SaveStage.Validate,
                    validated.Error.Code == SaveErrorCode.None ? SaveErrorCode.ValidateRejected : validated.Error.Code,
                    validated.Error.Message);
            }

            return SaveResult<TState>.Success(migrated.Value);
        }

        public SaveResult LoadAndApply(
            SaveSlotId slotId,
            Func<TState, SaveResult> validator,
            Func<TState, SaveResult> apply)
        {
            if (apply == null)
            {
                throw new ArgumentNullException(nameof(apply));
            }

            var loaded = LoadValidated(slotId, validator);
            if (!loaded.Succeeded)
            {
                return SaveResult.Fail(loaded.Error.Stage, loaded.Error.Code, loaded.Error.Message);
            }

            var applied = apply(loaded.Value);
            if (!applied.Succeeded)
            {
                return SaveResult.Fail(SaveStage.Apply, applied.Error.Code, applied.Error.Message);
            }

            return SaveResult.Success();
        }
    }
}
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

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
        Apply,
    }

    public enum SaveErrorCode
    {
        None,
        InvalidSlotId,
        NotFound,
        UnsupportedFormat,
        FutureSchema,
        MissingMigration,
        AmbiguousMigration,
        CyclicMigration,
        NonMonotonicMigration,
        CorruptPayload,
        UnsupportedCommitCapability,
        ValidationRejected,
        ProviderFailure,
    }

    public readonly struct SaveResult
    {
        private SaveResult(bool success, SaveStage stage, SaveErrorCode errorCode, string message)
        {
            Success = success;
            Stage = stage;
            ErrorCode = errorCode;
            Message = message ?? string.Empty;
        }

        public bool Success { get; }
        public SaveStage Stage { get; }
        public SaveErrorCode ErrorCode { get; }
        public string Message { get; }

        public static SaveResult Ok(SaveStage stage) => new SaveResult(true, stage, SaveErrorCode.None, string.Empty);

        public static SaveResult Fail(SaveStage stage, SaveErrorCode errorCode, string message)
            => new SaveResult(false, stage, errorCode, message);
    }

    public readonly struct SaveResult<T>
    {
        private SaveResult(bool success, SaveStage stage, SaveErrorCode errorCode, string message, T value, bool hasValue)
        {
            Success = success;
            Stage = stage;
            ErrorCode = errorCode;
            Message = message ?? string.Empty;
            Value = value;
            HasValue = hasValue;
        }

        public bool Success { get; }
        public SaveStage Stage { get; }
        public SaveErrorCode ErrorCode { get; }
        public string Message { get; }
        public bool HasValue { get; }
        public T Value { get; }

        public static SaveResult<T> Ok(SaveStage stage, T value) => new SaveResult<T>(true, stage, SaveErrorCode.None, string.Empty, value, true);

        public static SaveResult<T> Fail(SaveStage stage, SaveErrorCode errorCode, string message)
            => new SaveResult<T>(false, stage, errorCode, message, default(T), false);
    }

    public readonly struct SaveSlotId : IEquatable<SaveSlotId>
    {
        public SaveSlotId(string value)
        {
            Value = Require(value, nameof(value));
        }

        public string Value { get; }

        public bool Equals(SaveSlotId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SaveSlotId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;

        public static bool operator ==(SaveSlotId left, SaveSlotId right) => left.Equals(right);
        public static bool operator !=(SaveSlotId left, SaveSlotId right) => !left.Equals(right);

        private static string Require(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Save slot id cannot be empty or whitespace.", parameterName);
            }

            if (value.Length > 64)
            {
                throw new ArgumentException("Save slot id cannot exceed 64 characters.", parameterName);
            }

            if (value == "." || value == "..")
            {
                throw new ArgumentException("Save slot id cannot be a filesystem relative marker.", parameterName);
            }

            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                var allowed = character is >= 'a' and <= 'z'
                    or >= 'A' and <= 'Z'
                    or >= '0' and <= '9'
                    or '-'
                    or '_'
                    or '.';
                if (!allowed)
                {
                    throw new ArgumentException("Save slot id supports [A-Za-z0-9._-] only.", parameterName);
                }
            }

            return value;
        }
    }

    public sealed class SaveEnvelope
    {
        public SaveEnvelope(
            int formatVersion,
            string schemaId,
            int schemaVersion,
            string commitId,
            long utcTicks,
            string integrityAlgorithm,
            byte[] digest,
            byte[] payload)
        {
            if (formatVersion < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(formatVersion), "Envelope format must be positive.");
            }

            if (schemaVersion < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(schemaVersion), "Schema version cannot be negative.");
            }

            SchemaId = RequireNonEmpty(schemaId, nameof(schemaId));
            CommitId = RequireNonEmpty(commitId, nameof(commitId));
            IntegrityAlgorithm = RequireNonEmpty(integrityAlgorithm, nameof(integrityAlgorithm));
            FormatVersion = formatVersion;
            SchemaVersion = schemaVersion;
            UtcTicks = utcTicks;
            Digest = digest ?? throw new ArgumentNullException(nameof(digest));
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        }

        public int FormatVersion { get; }
        public string SchemaId { get; }
        public int SchemaVersion { get; }
        public string CommitId { get; }
        public long UtcTicks { get; }
        public string IntegrityAlgorithm { get; }
        public byte[] Digest { get; }
        public byte[] Payload { get; }

        private static string RequireNonEmpty(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", parameterName);
            }

            return value;
        }
    }

    public static class SaveEnvelopeBinaryCodec
    {
        public const int CurrentFormatVersion = 1;
        public const int MaxSchemaIdBytes = 128;
        public const int MaxCommitIdBytes = 128;
        public const int MaxAlgorithmBytes = 64;
        public const int MaxDigestBytes = 128;
        public const int MaxPayloadBytes = 4 * 1024 * 1024;

        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("LPSV");

        public static byte[] Encode(SaveEnvelope envelope)
        {
            if (envelope == null)
            {
                throw new ArgumentNullException(nameof(envelope));
            }

            using var stream = new MemoryStream();
            stream.Write(Magic, 0, Magic.Length);
            WriteInt32(stream, envelope.FormatVersion);
            WriteString(stream, envelope.SchemaId, MaxSchemaIdBytes);
            WriteInt32(stream, envelope.SchemaVersion);
            WriteString(stream, envelope.CommitId, MaxCommitIdBytes);
            WriteInt64(stream, envelope.UtcTicks);
            WriteString(stream, envelope.IntegrityAlgorithm, MaxAlgorithmBytes);
            WriteBytes(stream, envelope.Digest, MaxDigestBytes);
            WriteBytes(stream, envelope.Payload, MaxPayloadBytes);
            return stream.ToArray();
        }

        public static SaveResult<SaveEnvelope> Decode(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length < 4 + 4)
            {
                return SaveResult<SaveEnvelope>.Fail(SaveStage.Envelope, SaveErrorCode.UnsupportedFormat, "Envelope is too short.");
            }

            try
            {
                var offset = 0;
                if (!bytes.Slice(offset, 4).SequenceEqual(Magic))
                {
                    return SaveResult<SaveEnvelope>.Fail(SaveStage.Envelope, SaveErrorCode.UnsupportedFormat, "Envelope magic does not match.");
                }

                offset += 4;
                var formatVersion = ReadInt32(bytes, ref offset);
                if (formatVersion > CurrentFormatVersion)
                {
                    return SaveResult<SaveEnvelope>.Fail(SaveStage.Envelope, SaveErrorCode.UnsupportedFormat, "Envelope format is from a future version.");
                }

                if (formatVersion < 1)
                {
                    return SaveResult<SaveEnvelope>.Fail(SaveStage.Envelope, SaveErrorCode.UnsupportedFormat, "Envelope format must be positive.");
                }

                var schemaId = ReadString(bytes, ref offset, MaxSchemaIdBytes);
                var schemaVersion = ReadInt32(bytes, ref offset);
                var commitId = ReadString(bytes, ref offset, MaxCommitIdBytes);
                var utcTicks = ReadInt64(bytes, ref offset);
                var algorithm = ReadString(bytes, ref offset, MaxAlgorithmBytes);
                var digest = ReadBytes(bytes, ref offset, MaxDigestBytes);
                var payload = ReadBytes(bytes, ref offset, MaxPayloadBytes);

                if (offset != bytes.Length)
                {
                    return SaveResult<SaveEnvelope>.Fail(SaveStage.Envelope, SaveErrorCode.UnsupportedFormat, "Envelope has trailing bytes.");
                }

                var envelope = new SaveEnvelope(formatVersion, schemaId, schemaVersion, commitId, utcTicks, algorithm, digest, payload);
                return SaveResult<SaveEnvelope>.Ok(SaveStage.Envelope, envelope);
            }
            catch (InvalidDataException exception)
            {
                return SaveResult<SaveEnvelope>.Fail(SaveStage.Envelope, SaveErrorCode.UnsupportedFormat, exception.Message);
            }
        }

        private static void WriteInt32(Stream stream, int value)
        {
            Span<byte> buffer = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
            stream.Write(buffer);
        }

        private static int ReadInt32(ReadOnlySpan<byte> bytes, ref int offset)
        {
            EnsureRemaining(bytes, offset, 4);
            var value = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, 4));
            offset += 4;
            return value;
        }

        private static void WriteInt64(Stream stream, long value)
        {
            Span<byte> buffer = stackalloc byte[8];
            BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
            stream.Write(buffer);
        }

        private static long ReadInt64(ReadOnlySpan<byte> bytes, ref int offset)
        {
            EnsureRemaining(bytes, offset, 8);
            var value = BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(offset, 8));
            offset += 8;
            return value;
        }

        private static void WriteString(Stream stream, string value, int maxBytes)
        {
            var encoded = Encoding.UTF8.GetBytes(value ?? string.Empty);
            if (encoded.Length == 0 || encoded.Length > maxBytes)
            {
                throw new InvalidDataException($"String field length must be within 1..{maxBytes} bytes.");
            }

            WriteInt32(stream, encoded.Length);
            stream.Write(encoded, 0, encoded.Length);
        }

        private static string ReadString(ReadOnlySpan<byte> bytes, ref int offset, int maxBytes)
        {
            var length = ReadInt32(bytes, ref offset);
            if (length <= 0 || length > maxBytes)
            {
                throw new InvalidDataException($"String field length must be within 1..{maxBytes} bytes.");
            }

            EnsureRemaining(bytes, offset, length);
            var value = Encoding.UTF8.GetString(bytes.Slice(offset, length));
            offset += length;
            return value;
        }

        private static void WriteBytes(Stream stream, byte[] value, int maxBytes)
        {
            var source = value ?? Array.Empty<byte>();
            if (source.Length <= 0 || source.Length > maxBytes)
            {
                throw new InvalidDataException($"Byte field length must be within 1..{maxBytes} bytes.");
            }

            WriteInt32(stream, source.Length);
            stream.Write(source, 0, source.Length);
        }

        private static byte[] ReadBytes(ReadOnlySpan<byte> bytes, ref int offset, int maxBytes)
        {
            var length = ReadInt32(bytes, ref offset);
            if (length <= 0 || length > maxBytes)
            {
                throw new InvalidDataException($"Byte field length must be within 1..{maxBytes} bytes.");
            }

            EnsureRemaining(bytes, offset, length);
            var result = bytes.Slice(offset, length).ToArray();
            offset += length;
            return result;
        }

        private static void EnsureRemaining(ReadOnlySpan<byte> bytes, int offset, int requiredLength)
        {
            if (offset < 0 || requiredLength < 0 || bytes.Length - offset < requiredLength)
            {
                throw new InvalidDataException("Envelope ended unexpectedly.");
            }
        }
    }

    public interface ISaveCodec<TState>
    {
        byte[] Encode(TState state);
        TState Decode(ReadOnlySpan<byte> payload);
    }

    public interface IIntegrityProvider
    {
        string Algorithm { get; }
        byte[] Compute(ReadOnlySpan<byte> payload);
        bool Verify(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> expectedDigest);
    }

    public sealed class Sha256IntegrityProvider : IIntegrityProvider
    {
        private static readonly bool Available = ProbeAvailability();

        public string Algorithm => "SHA-256";

        public static bool IsAvailable() => Available;

        public byte[] Compute(ReadOnlySpan<byte> payload)
        {
            if (!Available)
            {
                throw new PlatformNotSupportedException("SHA-256 is unavailable in this runtime.");
            }

            using var sha256 = SHA256.Create();
            return sha256.ComputeHash(payload.ToArray());
        }

        public bool Verify(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> expectedDigest)
        {
            var actual = Compute(payload);
            return CryptographicOperations.FixedTimeEquals(actual, expectedDigest);
        }

        private static bool ProbeAvailability()
        {
            try
            {
                using var instance = SHA256.Create();
                return instance != null;
            }
            catch
            {
                return false;
            }
        }
    }

    public interface ISaveMigration<TState>
    {
        int FromVersion { get; }
        int ToVersion { get; }
        TState Migrate(TState state);
    }

    public sealed class MigrationPipeline<TState>
    {
        private readonly int currentVersion;
        private readonly Dictionary<int, ISaveMigration<TState>> stepsBySourceVersion;

        private MigrationPipeline(int currentVersion, Dictionary<int, ISaveMigration<TState>> stepsBySourceVersion)
        {
            this.currentVersion = currentVersion;
            this.stepsBySourceVersion = stepsBySourceVersion;
        }

        public static SaveResult<MigrationPipeline<TState>> Create(int currentVersion, IEnumerable<ISaveMigration<TState>> migrations)
        {
            if (currentVersion < 0)
            {
                return SaveResult<MigrationPipeline<TState>>.Fail(SaveStage.Migrate, SaveErrorCode.NonMonotonicMigration, "Current version cannot be negative.");
            }

            var migrationList = (migrations ?? Enumerable.Empty<ISaveMigration<TState>>()).ToList();
            var map = new Dictionary<int, ISaveMigration<TState>>();
            foreach (var migration in migrationList)
            {
                if (migration == null)
                {
                    return SaveResult<MigrationPipeline<TState>>.Fail(SaveStage.Migrate, SaveErrorCode.ProviderFailure, "Migration step cannot be null.");
                }

                if (map.ContainsKey(migration.FromVersion))
                {
                    return SaveResult<MigrationPipeline<TState>>.Fail(SaveStage.Migrate, SaveErrorCode.AmbiguousMigration, "Multiple migration edges share the same source version.");
                }

                map.Add(migration.FromVersion, migration);
            }

            foreach (var step in map.Values)
            {
                var cursor = step.ToVersion;
                var seen = new HashSet<int> { step.FromVersion };
                while (map.TryGetValue(cursor, out var next))
                {
                    if (!seen.Add(cursor))
                    {
                        return SaveResult<MigrationPipeline<TState>>.Fail(SaveStage.Migrate, SaveErrorCode.CyclicMigration, "Migration graph contains a cycle.");
                    }

                    cursor = next.ToVersion;
                }
            }

            foreach (var migration in map.Values)
            {
                if (migration.FromVersion >= migration.ToVersion)
                {
                    return SaveResult<MigrationPipeline<TState>>.Fail(SaveStage.Migrate, SaveErrorCode.NonMonotonicMigration, "Migration edges must be strictly increasing.");
                }
            }

            return SaveResult<MigrationPipeline<TState>>.Ok(SaveStage.Migrate, new MigrationPipeline<TState>(currentVersion, map));
        }

        public SaveResult<TState> Migrate(int storedVersion, TState state)
        {
            if (storedVersion > currentVersion)
            {
                return SaveResult<TState>.Fail(SaveStage.Migrate, SaveErrorCode.FutureSchema, "Stored schema version is newer than current version.");
            }

            if (storedVersion == currentVersion)
            {
                return SaveResult<TState>.Ok(SaveStage.Migrate, state);
            }

            var currentState = state;
            var cursor = storedVersion;
            while (cursor < currentVersion)
            {
                if (!stepsBySourceVersion.TryGetValue(cursor, out var step))
                {
                    return SaveResult<TState>.Fail(SaveStage.Migrate, SaveErrorCode.MissingMigration, $"Missing migration step from {cursor}.");
                }

                try
                {
                    currentState = step.Migrate(currentState);
                }
                catch (Exception exception)
                {
                    return SaveResult<TState>.Fail(SaveStage.Migrate, SaveErrorCode.ProviderFailure, $"Migration failed: {exception.Message}");
                }

                cursor = step.ToVersion;
            }

            return SaveResult<TState>.Ok(SaveStage.Migrate, currentState);
        }
    }

    [Flags]
    public enum SaveCommitCapability
    {
        None = 0,
        AtomicReplace = 1 << 0,
        RecoverableReplace = 1 << 1,
        BestEffortWrite = 1 << 2,
    }

    public interface ISaveStore
    {
        SaveCommitCapability Capabilities { get; }
        SaveResult<ReadOnlyMemory<byte>> Read(SaveSlotId slotId);
        SaveResult Commit(SaveSlotId slotId, ReadOnlyMemory<byte> envelopeBytes, SaveCommitCapability requiredCapability);
    }

    public sealed class SaveCoordinator<TState>
    {
        private readonly string schemaId;
        private readonly int currentSchemaVersion;
        private readonly SaveCommitCapability requiredCommitCapability;
        private readonly ISaveStore store;
        private readonly ISaveCodec<TState> codec;
        private readonly IIntegrityProvider integrityProvider;
        private readonly MigrationPipeline<TState> migrationPipeline;

        public SaveCoordinator(
            string schemaId,
            int currentSchemaVersion,
            SaveCommitCapability requiredCommitCapability,
            ISaveStore store,
            ISaveCodec<TState> codec,
            IIntegrityProvider integrityProvider,
            MigrationPipeline<TState> migrationPipeline)
        {
            if (string.IsNullOrWhiteSpace(schemaId))
            {
                throw new ArgumentException("Schema id cannot be empty.", nameof(schemaId));
            }

            if (currentSchemaVersion < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(currentSchemaVersion), "Schema version cannot be negative.");
            }

            this.schemaId = schemaId;
            this.currentSchemaVersion = currentSchemaVersion;
            this.requiredCommitCapability = requiredCommitCapability;
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.codec = codec ?? throw new ArgumentNullException(nameof(codec));
            this.integrityProvider = integrityProvider ?? throw new ArgumentNullException(nameof(integrityProvider));
            this.migrationPipeline = migrationPipeline ?? throw new ArgumentNullException(nameof(migrationPipeline));
        }

        public SaveResult Save(SaveSlotId slotId, TState snapshot, string commitId, long utcTicks)
        {
            if (!HasCapability(store.Capabilities, requiredCommitCapability))
            {
                return SaveResult.Fail(SaveStage.Commit, SaveErrorCode.UnsupportedCommitCapability, "Store does not provide the required commit capability.");
            }

            byte[] payload;
            try
            {
                payload = codec.Encode(snapshot);
            }
            catch (Exception exception)
            {
                return SaveResult.Fail(SaveStage.Encode, SaveErrorCode.ProviderFailure, $"Encode failed: {exception.Message}");
            }

            byte[] digest;
            try
            {
                digest = integrityProvider.Compute(payload);
            }
            catch (Exception exception)
            {
                return SaveResult.Fail(SaveStage.Integrity, SaveErrorCode.ProviderFailure, $"Integrity compute failed: {exception.Message}");
            }

            byte[] envelopeBytes;
            try
            {
                var envelope = new SaveEnvelope(
                    SaveEnvelopeBinaryCodec.CurrentFormatVersion,
                    schemaId,
                    currentSchemaVersion,
                    commitId ?? "unknown",
                    utcTicks,
                    integrityProvider.Algorithm,
                    digest,
                    payload);
                envelopeBytes = SaveEnvelopeBinaryCodec.Encode(envelope);
            }
            catch (Exception exception)
            {
                return SaveResult.Fail(SaveStage.Envelope, SaveErrorCode.UnsupportedFormat, $"Envelope encode failed: {exception.Message}");
            }

            return store.Commit(slotId, envelopeBytes, requiredCommitCapability);
        }

        public SaveResult<TState> Load(SaveSlotId slotId, Func<TState, SaveResult> validator)
        {
            var readResult = store.Read(slotId);
            if (!readResult.Success)
            {
                return SaveResult<TState>.Fail(readResult.Stage, readResult.ErrorCode, readResult.Message);
            }

            var envelopeResult = SaveEnvelopeBinaryCodec.Decode(readResult.Value.Span);
            if (!envelopeResult.Success)
            {
                return SaveResult<TState>.Fail(envelopeResult.Stage, envelopeResult.ErrorCode, envelopeResult.Message);
            }

            var envelope = envelopeResult.Value;
            if (!string.Equals(envelope.SchemaId, schemaId, StringComparison.Ordinal))
            {
                return SaveResult<TState>.Fail(SaveStage.Envelope, SaveErrorCode.UnsupportedFormat, "Schema id does not match.");
            }

            if (envelope.SchemaVersion > currentSchemaVersion)
            {
                return SaveResult<TState>.Fail(SaveStage.Migrate, SaveErrorCode.FutureSchema, "Envelope schema version is newer than current schema version.");
            }

            if (!string.Equals(envelope.IntegrityAlgorithm, integrityProvider.Algorithm, StringComparison.Ordinal))
            {
                return SaveResult<TState>.Fail(SaveStage.Verify, SaveErrorCode.UnsupportedFormat, "Integrity algorithm is unsupported.");
            }

            var verified = integrityProvider.Verify(envelope.Payload, envelope.Digest);
            if (!verified)
            {
                return SaveResult<TState>.Fail(SaveStage.Verify, SaveErrorCode.CorruptPayload, "Integrity verification failed.");
            }

            TState decoded;
            try
            {
                decoded = codec.Decode(envelope.Payload);
            }
            catch (Exception exception)
            {
                return SaveResult<TState>.Fail(SaveStage.Decode, SaveErrorCode.ProviderFailure, $"Decode failed: {exception.Message}");
            }

            var migratedResult = migrationPipeline.Migrate(envelope.SchemaVersion, decoded);
            if (!migratedResult.Success)
            {
                return SaveResult<TState>.Fail(migratedResult.Stage, migratedResult.ErrorCode, migratedResult.Message);
            }

            if (validator != null)
            {
                var validationResult = validator(migratedResult.Value);
                if (!validationResult.Success)
                {
                    return SaveResult<TState>.Fail(SaveStage.Validate, validationResult.ErrorCode == SaveErrorCode.None ? SaveErrorCode.ValidationRejected : validationResult.ErrorCode, validationResult.Message);
                }
            }

            return SaveResult<TState>.Ok(SaveStage.Apply, migratedResult.Value);
        }

        private static bool HasCapability(SaveCommitCapability actual, SaveCommitCapability required)
        {
            if (required == SaveCommitCapability.None)
            {
                return true;
            }

            return (actual & required) == required;
        }
    }
}
