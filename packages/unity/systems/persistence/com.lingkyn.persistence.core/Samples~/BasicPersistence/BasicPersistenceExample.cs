using System;
using System.Text;
using Lingkyn.Persistence.Core;

namespace Lingkyn.Persistence.Samples
{
    public static class BasicPersistenceExample
    {
        public static SaveResult<string> Run()
        {
            var slotCandidate = SaveSlotId.TryCreate("slot_main");
            if (!slotCandidate.Succeeded)
            {
                return SaveResult<string>.Fail(slotCandidate.Error.Stage, slotCandidate.Error.Code, slotCandidate.Error.Message);
            }

            var migrations = new MigrationPipeline<string>(new ISaveMigration<string>[]
            {
                new DelegateMigration(0, 1, state => state + "|migrated-to-v1")
            });

            var coordinator = new SaveCoordinator<string>(
                "lingkyn.state",
                1,
                "sample",
                new Utf8StringCodec(),
                new Sha256IntegrityProvider(),
                migrations,
                new InMemoryStore(),
                SaveCommitCapabilities.BestEffortWrite);

            var save = coordinator.Save(slotCandidate.Value, "state-v0");
            if (!save.Committed)
            {
                return SaveResult<string>.Fail(save.Error.Stage, save.Error.Code, save.Error.Message);
            }

            var loaded = coordinator.LoadValidated(slotCandidate.Value, _ => SaveResult.Success());
            if (!loaded.Succeeded)
            {
                return SaveResult<string>.Fail(loaded.Error.Stage, loaded.Error.Code, loaded.Error.Message);
            }

            return SaveResult<string>.Success(loaded.Value.State);
        }

        private sealed class Utf8StringCodec : ISaveCodec<string>
        {
            public SaveResult<byte[]> Encode(string snapshot)
            {
                return SaveResult<byte[]>.Success(Encoding.UTF8.GetBytes(snapshot ?? string.Empty));
            }

            public SaveResult<string> Decode(int schemaVersion, ReadOnlySpan<byte> bytes)
            {
                return SaveResult<string>.Success(Encoding.UTF8.GetString(bytes));
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

            public string Migrate(string state)
            {
                return _apply(state);
            }
        }

        private sealed class InMemoryStore : ISaveStore
        {
            private byte[] _bytes = Array.Empty<byte>();

            public SaveCommitCapabilities Capabilities => SaveCommitCapabilities.BestEffortWrite;

            public SaveResult<SaveReadCandidateSet> ReadCandidates(SaveSlotId slotId)
            {
                if (_bytes.Length == 0)
                {
                    return SaveResult<SaveReadCandidateSet>.Fail(SaveStage.Read, SaveErrorCode.NotFound, "No save exists.");
                }

                var candidateId = SaveCandidateId.TryCreate("primary");
                if (!candidateId.Succeeded)
                {
                    return SaveResult<SaveReadCandidateSet>.Fail(candidateId.Error.Stage, candidateId.Error.Code, candidateId.Error.Message);
                }

                var candidate = new SaveReadCandidate(SaveCandidateKind.Primary, candidateId.Value, _bytes);
                return SaveResult<SaveReadCandidateSet>.Success(new SaveReadCandidateSet(new[] { candidate }));
            }

            public SaveCommitResult Commit(
                SaveSlotId slotId,
                ReadOnlyMemory<byte> envelopeBytes,
                SaveCommitCapabilities requiredCapabilities)
            {
                _bytes = envelopeBytes.ToArray();
                return SaveCommitResult.Success();
            }
        }
    }
}
