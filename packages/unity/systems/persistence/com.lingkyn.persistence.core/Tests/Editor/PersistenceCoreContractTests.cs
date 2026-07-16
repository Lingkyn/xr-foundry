using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace Lingkyn.Persistence.Core.Tests
{
    public sealed class PersistenceCoreContractTests
    {
        [Test]
        public void SaveSlotIdRejectsInvalidCharacters()
        {
            Assert.Throws<ArgumentException>(() => _ = new SaveSlotId("slot/1"));
            Assert.Throws<ArgumentException>(() => _ = new SaveSlotId(".."));
            Assert.Throws<ArgumentException>(() => _ = new SaveSlotId(" "));
            Assert.DoesNotThrow(() => _ = new SaveSlotId("profile_01.quick"));
        }

        [Test]
        public void EnvelopeDecodeRejectsMalformedAndFutureFormats()
        {
            var malformed = SaveEnvelopeBinaryCodec.Decode(new byte[] { 0x01, 0x02, 0x03 });
            Assert.That(malformed.Success, Is.False);
            Assert.That(malformed.Stage, Is.EqualTo(SaveStage.Envelope));

            var envelope = BuildEnvelope(1, "commit", Encoding.UTF8.GetBytes("payload"));
            var encoded = SaveEnvelopeBinaryCodec.Encode(envelope);
            OverwriteFormatVersion(encoded, SaveEnvelopeBinaryCodec.CurrentFormatVersion + 1);
            var future = SaveEnvelopeBinaryCodec.Decode(encoded);
            Assert.That(future.Success, Is.False);
            Assert.That(future.ErrorCode, Is.EqualTo(SaveErrorCode.UnsupportedFormat));
        }

        [Test]
        public void LoadRejectsCorruptedChecksumBeforeDecode()
        {
            var codec = new TestCodec();
            var store = new TestStore(SaveCommitCapability.AtomicReplace);
            var integrity = new Sha256IntegrityProvider();
            var pipeline = MigrationPipeline<string>.Create(1, Array.Empty<ISaveMigration<string>>()).Value;

            var coordinator = new SaveCoordinator<string>(
                "schema",
                1,
                SaveCommitCapability.AtomicReplace,
                store,
                codec,
                integrity,
                pipeline);

            var payload = codec.Encode("saved");
            var digest = integrity.Compute(payload);
            digest[0] ^= 0xFF;
            var envelope = new SaveEnvelope(1, "schema", 1, "commit", DateTime.UtcNow.Ticks, integrity.Algorithm, digest, payload);
            store.LoadBytes = SaveEnvelopeBinaryCodec.Encode(envelope);

            var result = coordinator.Load(new SaveSlotId("slot-1"), _ => SaveResult.Ok(SaveStage.Validate));

            Assert.That(result.Success, Is.False);
            Assert.That(result.Stage, Is.EqualTo(SaveStage.Verify));
            Assert.That(result.ErrorCode, Is.EqualTo(SaveErrorCode.CorruptPayload));
            Assert.That(codec.DecodeCount, Is.EqualTo(0));
        }

        [Test]
        public void MigrationPipelineSucceedsWithDeterministicOrderedSteps()
        {
            var pipelineResult = MigrationPipeline<int>.Create(
                3,
                new ISaveMigration<int>[]
                {
                    new DelegateMigration<int>(1, 2, state => state + 10),
                    new DelegateMigration<int>(2, 3, state => state + 20),
                });

            Assert.That(pipelineResult.Success, Is.True);
            var migrated = pipelineResult.Value.Migrate(1, 5);
            Assert.That(migrated.Success, Is.True);
            Assert.That(migrated.Value, Is.EqualTo(35));
        }

        [Test]
        public void MigrationPipelineRejectsMissingAmbiguousCyclicAndNonMonotonic()
        {
            var missingPipeline = MigrationPipeline<int>.Create(3, new ISaveMigration<int>[]
            {
                new DelegateMigration<int>(1, 2, state => state),
            });
            var missingResult = missingPipeline.Value.Migrate(1, 7);
            Assert.That(missingResult.Success, Is.False);
            Assert.That(missingResult.ErrorCode, Is.EqualTo(SaveErrorCode.MissingMigration));

            var ambiguous = MigrationPipeline<int>.Create(3, new ISaveMigration<int>[]
            {
                new DelegateMigration<int>(1, 2, state => state),
                new DelegateMigration<int>(1, 3, state => state),
            });
            Assert.That(ambiguous.Success, Is.False);
            Assert.That(ambiguous.ErrorCode, Is.EqualTo(SaveErrorCode.AmbiguousMigration));

            var cyclic = MigrationPipeline<int>.Create(4, new ISaveMigration<int>[]
            {
                new DelegateMigration<int>(1, 2, state => state),
                new DelegateMigration<int>(2, 1, state => state),
            });
            Assert.That(cyclic.Success, Is.False);
            Assert.That(cyclic.ErrorCode, Is.EqualTo(SaveErrorCode.CyclicMigration));

            var nonMonotonic = MigrationPipeline<int>.Create(4, new ISaveMigration<int>[]
            {
                new DelegateMigration<int>(3, 2, state => state),
            });
            Assert.That(nonMonotonic.Success, Is.False);
            Assert.That(nonMonotonic.ErrorCode, Is.EqualTo(SaveErrorCode.NonMonotonicMigration));
        }

        [Test]
        public void MigrationRejectsFutureVersionBeforeReturningState()
        {
            var pipeline = MigrationPipeline<string>.Create(
                2,
                new ISaveMigration<string>[]
                {
                    new DelegateMigration<string>(1, 2, state => state + "-m2"),
                }).Value;

            var result = pipeline.Migrate(5, "state");
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(SaveErrorCode.FutureSchema));
            Assert.That(result.HasValue, Is.False);
        }

        [Test]
        public void CoordinatorEnforcesStageOrderAndFailClosed()
        {
            var trace = new List<string>();
            var codec = new TestCodec(trace);
            var integrity = new TraceIntegrityProvider(trace);
            var store = new TestStore(SaveCommitCapability.AtomicReplace, trace);

            var pipeline = MigrationPipeline<string>.Create(
                2,
                new ISaveMigration<string>[]
                {
                    new DelegateMigration<string>(1, 2, state =>
                    {
                        trace.Add("migrate");
                        return $"{state}-m";
                    }),
                }).Value;

            var coordinator = new SaveCoordinator<string>(
                "schema",
                2,
                SaveCommitCapability.AtomicReplace,
                store,
                codec,
                integrity,
                pipeline);

            var payload = codec.Encode("input");
            var digest = integrity.Compute(payload);
            store.LoadBytes = SaveEnvelopeBinaryCodec.Encode(new SaveEnvelope(1, "schema", 1, "commit", 1, integrity.Algorithm, digest, payload));

            var validationRuns = 0;
            var result = coordinator.Load(
                new SaveSlotId("slot"),
                _ =>
                {
                    validationRuns++;
                    trace.Add("validate");
                    return SaveResult.Fail(SaveStage.Validate, SaveErrorCode.ValidationRejected, "reject");
                });

            Assert.That(result.Success, Is.False);
            Assert.That(result.Stage, Is.EqualTo(SaveStage.Validate));
            Assert.That(result.HasValue, Is.False);
            Assert.That(validationRuns, Is.EqualTo(1));
            CollectionAssert.AreEqual(
                new[] { "read", "verify", "decode", "migrate", "validate" },
                trace);
        }

        [Test]
        public void SaveFailsWhenRequiredCommitCapabilityIsMissing()
        {
            var codec = new TestCodec();
            var store = new TestStore(SaveCommitCapability.BestEffortWrite);
            var integrity = new Sha256IntegrityProvider();
            var pipeline = MigrationPipeline<string>.Create(1, Array.Empty<ISaveMigration<string>>()).Value;
            var coordinator = new SaveCoordinator<string>(
                "schema",
                1,
                SaveCommitCapability.AtomicReplace,
                store,
                codec,
                integrity,
                pipeline);

            var result = coordinator.Save(new SaveSlotId("slot"), "state", "commit", DateTime.UtcNow.Ticks);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(SaveErrorCode.UnsupportedCommitCapability));
        }

        private static SaveEnvelope BuildEnvelope(int schemaVersion, string commitId, byte[] payload)
        {
            var integrity = new Sha256IntegrityProvider();
            var digest = integrity.Compute(payload);
            return new SaveEnvelope(1, "schema", schemaVersion, commitId, 1L, integrity.Algorithm, digest, payload);
        }

        private static void OverwriteFormatVersion(byte[] bytes, int value)
        {
            var offset = 4;
            bytes[offset + 0] = (byte)(value & 0xFF);
            bytes[offset + 1] = (byte)((value >> 8) & 0xFF);
            bytes[offset + 2] = (byte)((value >> 16) & 0xFF);
            bytes[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        private sealed class DelegateMigration<TState> : ISaveMigration<TState>
        {
            private readonly Func<TState, TState> step;

            public DelegateMigration(int fromVersion, int toVersion, Func<TState, TState> step)
            {
                FromVersion = fromVersion;
                ToVersion = toVersion;
                this.step = step;
            }

            public int FromVersion { get; }
            public int ToVersion { get; }

            public TState Migrate(TState state) => step(state);
        }

        private sealed class TestCodec : ISaveCodec<string>
        {
            private readonly IList<string> trace;

            public TestCodec(IList<string> trace = null)
            {
                this.trace = trace;
            }

            public int DecodeCount { get; private set; }

            public byte[] Encode(string state) => Encoding.UTF8.GetBytes(state ?? string.Empty);

            public string Decode(ReadOnlySpan<byte> payload)
            {
                DecodeCount++;
                trace?.Add("decode");
                return Encoding.UTF8.GetString(payload);
            }
        }

        private sealed class TraceIntegrityProvider : IIntegrityProvider
        {
            private readonly IList<string> trace;
            private readonly Sha256IntegrityProvider inner = new Sha256IntegrityProvider();

            public TraceIntegrityProvider(IList<string> trace)
            {
                this.trace = trace;
            }

            public string Algorithm => inner.Algorithm;

            public byte[] Compute(ReadOnlySpan<byte> payload) => inner.Compute(payload);

            public bool Verify(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> expectedDigest)
            {
                trace?.Add("verify");
                return inner.Verify(payload, expectedDigest);
            }
        }

        private sealed class TestStore : ISaveStore
        {
            private readonly IList<string> trace;

            public TestStore(SaveCommitCapability capabilities, IList<string> trace = null)
            {
                Capabilities = capabilities;
                this.trace = trace;
            }

            public SaveCommitCapability Capabilities { get; }

            public byte[] LoadBytes { get; set; }

            public SaveResult<ReadOnlyMemory<byte>> Read(SaveSlotId slotId)
            {
                trace?.Add("read");
                if (LoadBytes == null)
                {
                    return SaveResult<ReadOnlyMemory<byte>>.Fail(SaveStage.Read, SaveErrorCode.NotFound, "No save exists.");
                }

                return SaveResult<ReadOnlyMemory<byte>>.Ok(SaveStage.Read, new ReadOnlyMemory<byte>(LoadBytes));
            }

            public SaveResult Commit(SaveSlotId slotId, ReadOnlyMemory<byte> envelopeBytes, SaveCommitCapability requiredCapability)
            {
                trace?.Add("commit");
                if ((Capabilities & requiredCapability) != requiredCapability)
                {
                    return SaveResult.Fail(SaveStage.Commit, SaveErrorCode.UnsupportedCommitCapability, "Capability mismatch.");
                }

                LoadBytes = envelopeBytes.ToArray();
                return SaveResult.Ok(SaveStage.Commit);
            }
        }
    }
}
