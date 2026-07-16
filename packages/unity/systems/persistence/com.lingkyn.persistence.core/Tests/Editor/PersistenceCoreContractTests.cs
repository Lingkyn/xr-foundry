using System;
using System.Text;
using NUnit.Framework;

namespace Lingkyn.Persistence.Core.Editor.Tests
{
    public sealed class PersistenceCoreContractTests
    {
        [Test]
        public void SaveSlotIdRejectsInvalidInputs()
        {
            var empty = SaveSlotId.TryCreate(string.Empty);
            var whitespace = SaveSlotId.TryCreate(" ");
            var separator = SaveSlotId.TryCreate("profile/main");
            var valid = SaveSlotId.TryCreate("slot_main_01");

            Assert.That(empty.Succeeded, Is.False);
            Assert.That(whitespace.Succeeded, Is.False);
            Assert.That(separator.Succeeded, Is.False);
            Assert.That(valid.Succeeded, Is.True);
        }

        [Test]
        public void EnvelopeCodecRejectsMalformedAndFutureFormats()
        {
            var malformed = SaveEnvelopeBinaryCodec.Decode(new byte[] { 0x01, 0x02, 0x03 });
            Assert.That(malformed.Succeeded, Is.False);
            Assert.That(malformed.Error.Stage, Is.EqualTo(SaveStage.Envelope));

            var valid = BuildEnvelope("state-v1", 1);
            var encoded = SaveEnvelopeBinaryCodec.Encode(valid);
            Assert.That(encoded.Succeeded, Is.True);

            var future = (byte[])encoded.Value.Clone();
            future[4] = SaveEnvelopeBinaryCodec.FormatVersion + 1;
            var futureDecode = SaveEnvelopeBinaryCodec.Decode(future);
            Assert.That(futureDecode.Succeeded, Is.False);
            Assert.That(futureDecode.Error.Code, Is.EqualTo(SaveErrorCode.FutureSchema));
        }

        [Test]
        public void EnvelopeCodecRejectsMalformedUtf8Metadata()
        {
            var valid = BuildEnvelope("state-v1", 1);
            var encoded = SaveEnvelopeBinaryCodec.Encode(valid);
            Assert.That(encoded.Succeeded, Is.True);

            var malformed = (byte[])encoded.Value.Clone();
            malformed[7] = 0xFF; // First schema-id byte with invalid UTF-8.

            var decoded = SaveEnvelopeBinaryCodec.Decode(malformed);
            Assert.That(decoded.Succeeded, Is.False);
            Assert.That(decoded.Error.Stage, Is.EqualTo(SaveStage.Envelope));
            Assert.That(decoded.Error.Code, Is.EqualTo(SaveErrorCode.UnsupportedFormat));
        }

        [Test]
        public void CoordinatorRejectsChecksumCorruptionBeforeDecode()
        {
            var store = new InMemoryStore();
            var codec = new Utf8StringCodec();
            var migrations = new MigrationPipeline<string>(Array.Empty<ISaveMigration<string>>());
            var coordinator = CreateCoordinator(store, codec, migrations);
            var slot = MustSlot("slot_main");

            var save = coordinator.Save(slot, "state-v1");
            Assert.That(save.Succeeded, Is.True);

            var corrupted = (byte[])store.Bytes.Clone();
            corrupted[corrupted.Length - 1] ^= 0x40;
            store.Bytes = corrupted;

            var loaded = coordinator.LoadValidated(slot, _ => SaveResult.Success());
            Assert.That(loaded.Succeeded, Is.False);
            Assert.That(loaded.Error.Stage, Is.EqualTo(SaveStage.Verify));
            Assert.That(loaded.Error.Code, Is.EqualTo(SaveErrorCode.CorruptPayload));
            Assert.That(codec.DecodeCallCount, Is.EqualTo(0));
        }

        [Test]
        public void MigrationPipelineAppliesDeterministicPath()
        {
            var migrations = new MigrationPipeline<string>(new ISaveMigration<string>[]
            {
                new DelegateMigration(0, 1, state => state + "|m01"),
                new DelegateMigration(1, 2, state => state + "|m12")
            });

            var result = migrations.Apply(0, 2, "seed");
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Value, Is.EqualTo("seed|m01|m12"));
        }

        [Test]
        public void MigrationPipelineRejectsMissingPath()
        {
            var migrations = new MigrationPipeline<string>(new ISaveMigration<string>[]
            {
                new DelegateMigration(0, 1, state => state + "|m01")
            });

            var result = migrations.Apply(0, 2, "seed");
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(SaveErrorCode.MissingMigration));
        }

        [Test]
        public void MigrationPipelineRejectsAmbiguousGraph()
        {
            var migrations = new MigrationPipeline<string>(new ISaveMigration<string>[]
            {
                new DelegateMigration(0, 1, state => state + "|m01"),
                new DelegateMigration(0, 2, state => state + "|m02")
            });

            var result = migrations.Apply(0, 2, "seed");
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(SaveErrorCode.AmbiguousMigration));
        }

        [Test]
        public void MigrationPipelineRejectsCyclicGraph()
        {
            var migrations = new MigrationPipeline<string>(new ISaveMigration<string>[]
            {
                new DelegateMigration(0, 1, state => state + "|m01"),
                new DelegateMigration(1, 0, state => state + "|m10")
            });

            var result = migrations.Apply(0, 1, "seed");
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(SaveErrorCode.NonMonotonicMigration));
        }

        [Test]
        public void MigrationPipelineRejectsNonMonotonicGraph()
        {
            var migrations = new MigrationPipeline<string>(new ISaveMigration<string>[]
            {
                new DelegateMigration(2, 1, state => state)
            });

            var result = migrations.Apply(0, 1, "seed");
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(SaveErrorCode.NonMonotonicMigration));
        }

        [Test]
        public void MigrationPipelineRejectsFutureStoredVersion()
        {
            var migrations = new MigrationPipeline<string>(Array.Empty<ISaveMigration<string>>());
            var result = migrations.Apply(5, 2, "seed");
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(SaveErrorCode.FutureSchema));
        }

        [Test]
        public void MigrationPipelineRejectsOvershootEdge()
        {
            var migrations = new MigrationPipeline<string>(new ISaveMigration<string>[]
            {
                new DelegateMigration(0, 3, state => state + "|m03")
            });

            var result = migrations.Apply(0, 2, "seed");
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(SaveErrorCode.OvershootMigration));
        }

        [Test]
        public void MigrationPipelineConvertsMigrationThrowsToStableResult()
        {
            var migrations = new MigrationPipeline<string>(new ISaveMigration<string>[]
            {
                new DelegateMigration(0, 1, _ => throw new InvalidOperationException("boom"))
            });

            var result = migrations.Apply(0, 1, "seed");
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error.Stage, Is.EqualTo(SaveStage.Migrate));
            Assert.That(result.Error.Code, Is.EqualTo(SaveErrorCode.ProviderFailure));
        }

        [Test]
        public void CoordinatorStopsBeforeApplyWhenValidationFails()
        {
            var store = new InMemoryStore();
            var codec = new Utf8StringCodec();
            var migrations = new MigrationPipeline<string>(new ISaveMigration<string>[]
            {
                new DelegateMigration(0, 1, state => state + "|migrated")
            });
            var coordinator = CreateCoordinator(store, codec, migrations, currentSchemaVersion: 1);
            var slot = MustSlot("slot_gate");

            store.Bytes = SaveEnvelopeBinaryCodec.Encode(BuildEnvelope("candidate", 0)).Value;
            var applyCalls = 0;
            var result = coordinator.LoadAndApply(
                slot,
                _ => SaveResult.Fail(SaveStage.Validate, SaveErrorCode.ValidateRejected, "validator rejected"),
                _ =>
                {
                    applyCalls++;
                    return SaveResult.Success();
                });

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error.Stage, Is.EqualTo(SaveStage.Validate));
            Assert.That(applyCalls, Is.EqualTo(0));
            Assert.That(codec.DecodeCallCount, Is.EqualTo(1));
        }

        [Test]
        public void CoordinatorPassesStoredSchemaVersionToCodecDecode()
        {
            var store = new InMemoryStore();
            var codec = new Utf8StringCodec();
            var coordinator = CreateCoordinator(store, codec, new MigrationPipeline<string>(Array.Empty<ISaveMigration<string>>()), currentSchemaVersion: 3);
            var slot = MustSlot("slot_schema_decode");
            store.Bytes = SaveEnvelopeBinaryCodec.Encode(BuildEnvelope("payload", 2)).Value;

            var result = coordinator.LoadValidated(slot, _ => SaveResult.Success());
            Assert.That(result.Succeeded, Is.True);
            Assert.That(codec.LastDecodeSchemaVersion, Is.EqualTo(2));
        }

        [Test]
        public void CoordinatorRejectsIntegrityAlgorithmMismatch()
        {
            var store = new InMemoryStore();
            var codec = new Utf8StringCodec();
            var migrations = new MigrationPipeline<string>(Array.Empty<ISaveMigration<string>>());
            var coordinator = new SaveCoordinator<string>(
                "lingkyn.state",
                0,
                "bc59960",
                codec,
                new FixedDigestIntegrityProvider("sha-512"),
                migrations,
                store,
                SaveCommitCapabilities.BestEffortWrite);
            var slot = MustSlot("slot_algo");

            store.Bytes = SaveEnvelopeBinaryCodec.Encode(BuildEnvelope("state-v1", 0)).Value;
            var loaded = coordinator.LoadValidated(slot, _ => SaveResult.Success());

            Assert.That(loaded.Succeeded, Is.False);
            Assert.That(loaded.Error.Stage, Is.EqualTo(SaveStage.Verify));
            Assert.That(loaded.Error.Code, Is.EqualTo(SaveErrorCode.UnsupportedFormat));
        }

        [Test]
        public void SaveEnvelopeOwnsImmutableByteCopies()
        {
            var digest = new byte[] { 0x01, 0x02, 0x03 };
            var payload = Encoding.UTF8.GetBytes("snapshot");
            var envelope = new SaveEnvelope("lingkyn.state", 1, "bc59960", DateTime.UtcNow.Ticks, "sha-256", digest, payload);

            digest[0] = 0x7F;
            payload[0] = 0x00;

            Assert.That(envelope.IntegrityDigest.Span[0], Is.EqualTo(0x01));
            Assert.That(envelope.Payload.Span[0], Is.EqualTo((byte)'s'));
        }

        [Test]
        public void CoordinatorConvertsCodecEncodeThrowsToStableResult()
        {
            var coordinator = CreateCoordinator(new InMemoryStore(), new Utf8StringCodec { ThrowOnEncode = true }, new MigrationPipeline<string>(Array.Empty<ISaveMigration<string>>()));
            var result = coordinator.Save(MustSlot("slot_encode_throw"), "payload");
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error.Stage, Is.EqualTo(SaveStage.Encode));
            Assert.That(result.Error.Code, Is.EqualTo(SaveErrorCode.ProviderFailure));
        }

        [Test]
        public void CoordinatorConvertsCodecDecodeThrowsToStableResult()
        {
            var store = new InMemoryStore();
            var codec = new Utf8StringCodec { ThrowOnDecode = true };
            var coordinator = CreateCoordinator(store, codec, new MigrationPipeline<string>(Array.Empty<ISaveMigration<string>>()));
            var slot = MustSlot("slot_decode_throw");
            store.Bytes = SaveEnvelopeBinaryCodec.Encode(BuildEnvelope("payload", 0)).Value;

            var result = coordinator.LoadValidated(slot, _ => SaveResult.Success());
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error.Stage, Is.EqualTo(SaveStage.Decode));
            Assert.That(result.Error.Code, Is.EqualTo(SaveErrorCode.ProviderFailure));
        }

        [Test]
        public void CoordinatorConvertsStoreReadThrowsToStableResult()
        {
            var store = new InMemoryStore { ThrowOnRead = true };
            var coordinator = CreateCoordinator(store, new Utf8StringCodec(), new MigrationPipeline<string>(Array.Empty<ISaveMigration<string>>()));
            var result = coordinator.LoadValidated(MustSlot("slot_read_throw"), _ => SaveResult.Success());
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error.Stage, Is.EqualTo(SaveStage.Read));
            Assert.That(result.Error.Code, Is.EqualTo(SaveErrorCode.ProviderFailure));
        }

        [Test]
        public void CoordinatorConvertsStoreCommitThrowsToStableResult()
        {
            var store = new InMemoryStore { ThrowOnCommit = true };
            var coordinator = CreateCoordinator(store, new Utf8StringCodec(), new MigrationPipeline<string>(Array.Empty<ISaveMigration<string>>()));
            var result = coordinator.Save(MustSlot("slot_commit_throw"), "payload");
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error.Stage, Is.EqualTo(SaveStage.Commit));
            Assert.That(result.Error.Code, Is.EqualTo(SaveErrorCode.ProviderFailure));
        }

        [Test]
        public void CoordinatorConvertsValidatorThrowsToStableResult()
        {
            var store = new InMemoryStore();
            var coordinator = CreateCoordinator(store, new Utf8StringCodec(), new MigrationPipeline<string>(Array.Empty<ISaveMigration<string>>()));
            var slot = MustSlot("slot_validator_throw");
            store.Bytes = SaveEnvelopeBinaryCodec.Encode(BuildEnvelope("payload", 0)).Value;

            var result = coordinator.LoadValidated(slot, _ => throw new InvalidOperationException("validator boom"));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error.Stage, Is.EqualTo(SaveStage.Validate));
            Assert.That(result.Error.Code, Is.EqualTo(SaveErrorCode.ProviderFailure));
        }

        [Test]
        public void CoordinatorConvertsApplyThrowsToStableResult()
        {
            var store = new InMemoryStore();
            var coordinator = CreateCoordinator(store, new Utf8StringCodec(), new MigrationPipeline<string>(Array.Empty<ISaveMigration<string>>()));
            var slot = MustSlot("slot_apply_throw");
            store.Bytes = SaveEnvelopeBinaryCodec.Encode(BuildEnvelope("payload", 0)).Value;

            var result = coordinator.LoadAndApply(slot, _ => SaveResult.Success(), _ => throw new InvalidOperationException("apply boom"));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error.Stage, Is.EqualTo(SaveStage.Apply));
            Assert.That(result.Error.Code, Is.EqualTo(SaveErrorCode.ProviderFailure));
        }

        [Test]
        public void CoordinatorReportsCapabilityMismatchBeforeCommit()
        {
            var store = new InMemoryStore { Capabilities = SaveCommitCapabilities.BestEffortWrite };
            var coordinator = CreateCoordinator(
                store,
                new Utf8StringCodec(),
                new MigrationPipeline<string>(Array.Empty<ISaveMigration<string>>()),
                requiredCapabilities: SaveCommitCapabilities.AtomicReplace);

            var result = coordinator.Save(MustSlot("slot_commit"), "payload");
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error.Stage, Is.EqualTo(SaveStage.Commit));
            Assert.That(result.Error.Code, Is.EqualTo(SaveErrorCode.UnsupportedCommitCapability));
            Assert.That(store.CommitCallCount, Is.EqualTo(0));
        }

        private static SaveCoordinator<string> CreateCoordinator(
            InMemoryStore store,
            Utf8StringCodec codec,
            MigrationPipeline<string> migrations,
            int currentSchemaVersion = 0,
            SaveCommitCapabilities requiredCapabilities = SaveCommitCapabilities.BestEffortWrite)
        {
            return new SaveCoordinator<string>(
                "lingkyn.state",
                currentSchemaVersion,
                "bc59960",
                codec,
                new Sha256IntegrityProvider(),
                migrations,
                store,
                requiredCapabilities);
        }

        private static SaveEnvelope BuildEnvelope(string payloadText, int schemaVersion)
        {
            var payload = Encoding.UTF8.GetBytes(payloadText);
            var digest = new Sha256IntegrityProvider().ComputeDigest(payload).Value;
            return new SaveEnvelope(
                "lingkyn.state",
                schemaVersion,
                "bc59960",
                DateTime.UtcNow.Ticks,
                "sha-256",
                digest,
                payload);
        }

        private static SaveSlotId MustSlot(string value)
        {
            var candidate = SaveSlotId.TryCreate(value);
            Assert.That(candidate.Succeeded, Is.True);
            return candidate.Value;
        }

        private sealed class Utf8StringCodec : ISaveCodec<string>
        {
            public int DecodeCallCount { get; private set; }

            public bool ThrowOnEncode { get; set; }
            public bool ThrowOnDecode { get; set; }
            public int LastDecodeSchemaVersion { get; private set; }

            public SaveResult<string> Decode(int schemaVersion, ReadOnlySpan<byte> bytes)
            {
                if (ThrowOnDecode)
                {
                    throw new InvalidOperationException("decode boom");
                }

                DecodeCallCount++;
                LastDecodeSchemaVersion = schemaVersion;
                return SaveResult<string>.Success(Encoding.UTF8.GetString(bytes));
            }

            public SaveResult<byte[]> Encode(string snapshot)
            {
                if (ThrowOnEncode)
                {
                    throw new InvalidOperationException("encode boom");
                }

                return SaveResult<byte[]>.Success(Encoding.UTF8.GetBytes(snapshot ?? string.Empty));
            }
        }

        private sealed class DelegateMigration : ISaveMigration<string>
        {
            private readonly Func<string, string> _apply;

            public DelegateMigration(int fromVersion, int toVersion, Func<string, string> apply)
            {
                FromVersion = fromVersion;
                ToVersion = toVersion;
                _apply = apply;
            }

            public int FromVersion { get; }
            public int ToVersion { get; }

            public string Migrate(string state) => _apply(state);
        }

        private sealed class InMemoryStore : ISaveStore
        {
            public byte[] Bytes { get; set; } = Array.Empty<byte>();
            public SaveCommitCapabilities Capabilities { get; set; } = SaveCommitCapabilities.BestEffortWrite;
            public int CommitCallCount { get; private set; }
            public bool ThrowOnRead { get; set; }
            public bool ThrowOnCommit { get; set; }

            public SaveResult<byte[]> Read(SaveSlotId slotId)
            {
                if (ThrowOnRead)
                {
                    throw new InvalidOperationException("read boom");
                }

                if (Bytes.Length == 0)
                {
                    return SaveResult<byte[]>.Fail(SaveStage.Read, SaveErrorCode.NotFound, "No save exists.");
                }

                return SaveResult<byte[]>.Success(Bytes);
            }

            public SaveResult Commit(
                SaveSlotId slotId,
                ReadOnlyMemory<byte> envelopeBytes,
                SaveCommitCapabilities requiredCapabilities)
            {
                if (ThrowOnCommit)
                {
                    throw new InvalidOperationException("commit boom");
                }

                CommitCallCount++;
                Bytes = envelopeBytes.ToArray();
                return SaveResult.Success();
            }
        }

        private sealed class FixedDigestIntegrityProvider : IIntegrityProvider
        {
            private readonly byte[] _digest = new byte[] { 0xAA };

            public FixedDigestIntegrityProvider(string algorithmName)
            {
                AlgorithmName = algorithmName;
            }

            public string AlgorithmName { get; }

            public SaveResult<byte[]> ComputeDigest(ReadOnlySpan<byte> payload) => SaveResult<byte[]>.Success((byte[])_digest.Clone());

            public SaveResult Verify(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> expectedDigest)
            {
                return SaveResult.Success();
            }
        }
    }
}
