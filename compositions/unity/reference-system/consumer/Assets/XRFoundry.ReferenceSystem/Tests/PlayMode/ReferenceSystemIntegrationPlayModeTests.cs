using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Lingkyn.Interaction.Core;
using Lingkyn.Inventory.Core;
using Lingkyn.Inventory.Presentation;
using Lingkyn.Persistence.Core;
using Lingkyn.Persistence.Unity;
using Lingkyn.Settings.Core;
using NUnit.Framework;
using UnityEngine.TestTools;
using XRFoundry.ReferenceSystem.Bindings;

namespace XRFoundry.ReferenceSystem.Tests
{
    public sealed class ReferenceSystemIntegrationPlayModeTests
    {
        private static readonly InventoryId PlayerInventoryId = new InventoryId("player");
        private static readonly ContainerId BagId = new ContainerId("bag");
        private static readonly ItemDefinitionId PotionId = new ItemDefinitionId("potion");
        private static readonly SlotAddress FirstSlot = new SlotAddress(BagId, 0);

        [UnityTest]
        public IEnumerator SettingsInteractionInventoryPersistenceRoundTripCrossesFrames()
        {
            var interaction = CreateInteraction();
            var inventory = CreateInventory();
            var presenter = new InventoryPresenter(inventory, new RecordingView());

            try
            {
                SeedPotion(inventory, 2);
                var selection = new InteractionToInventoryIntentAdapter(
                    presenter,
                    interaction.IntentId,
                    new Dictionary<RouteId, SlotAddress>
                    {
                        [interaction.RouteId] = FirstSlot,
                    });

                var routeEnabled = MustBooleanDefinition("interaction.inventory.route.enabled", true);
                var scopedRouteEnabled = new ScopedSettingKey(routeEnabled.Key, routeEnabled.DefaultScope);
                var settingsAdapter = new SettingsToInteractionPolicyAdapter(
                    interaction.Coordinator,
                    routeBindings: new[]
                    {
                        new RoutePolicySettingBinding(
                            scopedRouteEnabled,
                            interaction.RouteId,
                            RoutePolicySettingField.Enabled),
                    });
                var settingsRegistry = MustSettingsRegistry(routeEnabled);
                var settings = new SettingsCoordinator(
                    settingsRegistry,
                    SettingsSnapshot.CreateInitial(settingsRegistry),
                    new[] { settingsAdapter });
                var initialized = settingsAdapter.Initialize(settings);
                Assert.That(initialized.Succeeded, Is.True, initialized.Diagnostic.Message);

                ApplyBoolean(settings, scopedRouteEnabled, false);
                var disabled = interaction.Coordinator.RouteFrame(
                    MustFrame(Signal(interaction, InteractionPhase.Started, 10)),
                    selection.Handle);

                Assert.That(disabled.Events, Is.Empty);
                Assert.That(disabled.Dispatches, Is.Empty);
                Assert.That(disabled.Diagnostics.Single().Code, Is.EqualTo(InteractionValidationCode.DisabledRoute));
                Assert.That(presenter.Current.Slots.All(slot => !slot.Selected), Is.True);

                yield return null;

                ApplyBoolean(settings, scopedRouteEnabled, true);
                var started = interaction.Coordinator.RouteFrame(
                    MustFrame(Signal(interaction, InteractionPhase.Started, 20)),
                    selection.Handle);
                var performed = interaction.Coordinator.RouteFrame(
                    MustFrame(Signal(interaction, InteractionPhase.Performed, 21)),
                    selection.Handle);

                Assert.That(started.Events.Single().Phase, Is.EqualTo(InteractionPhase.Started));
                Assert.That(performed.Dispatches.Single().HandlerOutcome,
                    Is.EqualTo(InteractionHandlerOutcome.Accepted));
                Assert.That(Slot(presenter, FirstSlot).Selected, Is.True);

                var savedRevision = inventory.Revision;
                var savedQuantity = Total(inventory);
                var store = new InMemorySaveStore();
                var persistence = new InventoryToPersistenceAdapter(
                    inventory,
                    CreateSaveCoordinator(store));
                var saveSlot = MustSaveSlot("playmode_roundtrip");
                var committed = persistence.Save(saveSlot);
                Assert.That(committed.Committed, Is.True, committed.Error.Message);

                var removed = inventory.Execute(MutationRequest.Remove(FirstSlot, 1));
                Assert.That(removed.Succeeded, Is.True, removed.Message);
                Assert.That(inventory.Revision, Is.GreaterThan(savedRevision));
                Assert.That(Total(inventory), Is.EqualTo(savedQuantity - 1));

                yield return null;

                var loaded = persistence.Load(saveSlot);

                Assert.That(loaded.Succeeded, Is.True, loaded.Error.Message);
                Assert.That(loaded.Value.SelectedCandidateKind, Is.EqualTo(SaveCandidateKind.Primary));
                Assert.That(loaded.Value.RecoveryOccurred, Is.False);
                Assert.That(loaded.Value.RestoreResult.Succeeded, Is.True);
                Assert.That(inventory.Revision, Is.EqualTo(savedRevision));
                Assert.That(Total(inventory), Is.EqualTo(savedQuantity));
                Assert.That(presenter.Current.Revision, Is.EqualTo(savedRevision));
                Assert.That(Slot(presenter, FirstSlot).Quantity, Is.EqualTo(savedQuantity));
            }
            finally
            {
                presenter.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator CorruptPrimaryRecoversFromValidBackupWithoutLeakingPrimaryState()
        {
            var inventory = CreateInventory();
            SeedPotion(inventory, 2);
            var store = new InMemorySaveStore();
            var persistence = new InventoryToPersistenceAdapter(
                inventory,
                CreateSaveCoordinator(store, SaveRecoveryPolicy.PrimaryThenBackup));
            var saveSlot = MustSaveSlot("playmode_recovery");
            var committed = persistence.Save(saveSlot);
            Assert.That(committed.Committed, Is.True, committed.Error.Message);

            var savedRevision = inventory.Revision;
            var savedQuantity = Total(inventory);
            var validBackup = store.CopyLastCommittedEnvelopeBytes();
            var corruptedPrimary = (byte[])validBackup.Clone();
            corruptedPrimary[corruptedPrimary.Length - 1] ^= 0x40;

            var removed = inventory.Execute(MutationRequest.Remove(FirstSlot, 1));
            Assert.That(removed.Succeeded, Is.True, removed.Message);
            var changedRevision = inventory.Revision;
            Assert.That(Total(inventory), Is.EqualTo(savedQuantity - 1));

            store.ReplaceCandidates(
                Candidate(SaveCandidateKind.Primary, "primary_corrupt", corruptedPrimary),
                Candidate(SaveCandidateKind.Backup, "backup_valid", validBackup));

            yield return null;

            var loaded = persistence.Load(saveSlot);

            Assert.That(loaded.Succeeded, Is.True, loaded.Error.Message);
            Assert.That(loaded.Value.SelectedCandidateKind, Is.EqualTo(SaveCandidateKind.Backup));
            Assert.That(loaded.Value.SelectedCandidateId.Value, Is.EqualTo("backup_valid"));
            Assert.That(loaded.Value.RecoveryOccurred, Is.True);
            Assert.That(loaded.Value.PrimaryFailureDiagnostic, Is.Not.Null);
            Assert.That(loaded.Value.PrimaryFailureDiagnostic.Value.Stage, Is.EqualTo(SaveStage.Verify));
            Assert.That(loaded.Value.PrimaryFailureDiagnostic.Value.Code, Is.EqualTo(SaveErrorCode.CorruptPayload));
            Assert.That(loaded.Value.RestoreResult.Succeeded, Is.True);
            Assert.That(loaded.Value.RestoreResult.RevisionBefore, Is.EqualTo(changedRevision));
            Assert.That(inventory.Revision, Is.EqualTo(savedRevision));
            Assert.That(Total(inventory), Is.EqualTo(savedQuantity));
        }

        private static SaveCoordinator<InventoryPersistenceDto> CreateSaveCoordinator(
            InMemorySaveStore store,
            SaveRecoveryPolicy recoveryPolicy = SaveRecoveryPolicy.PrimaryOnly)
        {
            return new SaveCoordinator<InventoryPersistenceDto>(
                InventoryToPersistenceAdapter.OuterPersistenceSchemaId,
                InventoryToPersistenceAdapter.OuterPersistenceSchemaVersion,
                "playmode-integration",
                new JsonUtilitySaveCodec<InventoryPersistenceDto>(),
                new Sha256IntegrityProvider(),
                new MigrationPipeline<InventoryPersistenceDto>(Array.Empty<ISaveMigration<InventoryPersistenceDto>>()),
                store,
                SaveCommitCapabilities.BestEffortWrite,
                recoveryPolicy: recoveryPolicy);
        }

        private static InteractionFixture CreateInteraction()
        {
            var intentId = MustIntentId("inventory.select-slot");
            var routeId = MustRouteId("route.inventory.first-slot");
            var contextId = MustContextId("context.inventory");
            var sourceId = MustSourceId("source.inventory.first-slot");
            var intent = IntentDefinition.Create(
                intentId,
                InteractionValueKind.Button,
                InteractionCapability.Digital,
                0);
            Assert.That(intent.Succeeded, Is.True, intent.Error.ToString());
            var route = InteractionRoute.Create(
                routeId,
                contextId,
                intentId,
                sourceId,
                InteractionModality.Gamepad,
                InteractionCapability.Digital,
                null,
                0);
            Assert.That(route.Succeeded, Is.True, route.Error.ToString());
            var context = InteractionContextDefinition.Create(contextId, 0, new[] { routeId });
            Assert.That(context.Succeeded, Is.True, context.Error.ToString());
            var registry = InteractionRegistry.Create(
                new[] { intent.Value },
                new[] { context.Value },
                new[] { route.Value });
            Assert.That(registry.Succeeded, Is.True, registry.Error.ToString());
            return new InteractionFixture(
                new InteractionCoordinator(registry.Value, new[] { contextId }),
                intentId,
                routeId,
                sourceId);
        }

        private static InventoryAggregate CreateInventory()
        {
            return new InventoryAggregate(
                PlayerInventoryId,
                new ItemDefinitionCatalog(new[]
                {
                    new ItemDefinition(PotionId, 5, ItemInstanceMode.Fungible),
                }),
                new[] { new ContainerDefinition(BagId, 2) });
        }

        private static void SeedPotion(InventoryAggregate inventory, int quantity)
        {
            var result = inventory.Execute(MutationRequest.Add(new ItemStack(PotionId, quantity), BagId));
            Assert.That(result.Succeeded, Is.True, result.Message);
        }

        private static void ApplyBoolean(
            SettingsCoordinator settings,
            ScopedSettingKey key,
            bool value)
        {
            var transaction = settings.BeginTransaction();
            transaction.StageSet(key, SettingValue.FromBoolean(value));
            var result = settings.Apply(transaction);
            Assert.That(result.Outcome, Is.EqualTo(SettingsApplyOutcome.Applied));
        }

        private static SourceSignal Signal(
            InteractionFixture interaction,
            InteractionPhase phase,
            long timestampTicks)
        {
            return new SourceSignal(
                interaction.RouteId,
                interaction.SourceId,
                InteractionModality.Gamepad,
                InteractionCapability.Digital,
                InteractionValue.FromButton(true),
                phase,
                timestampTicks,
                0);
        }

        private static InteractionFrame MustFrame(params SourceSignal[] signals)
        {
            var result = InteractionFrame.Create(signals);
            Assert.That(result.Succeeded, Is.True, result.Error.ToString());
            return result.Value;
        }

        private static InventorySlotViewModel Slot(InventoryPresenter presenter, SlotAddress address)
        {
            return presenter.Current.Slots.Single(slot => slot.Address.Equals(address));
        }

        private static int Total(InventoryAggregate inventory)
        {
            return inventory.GetSnapshot().Containers
                .SelectMany(container => container.Slots)
                .Where(stack => stack != null)
                .Sum(stack => stack.Quantity);
        }

        private static SettingDefinition MustBooleanDefinition(string key, bool defaultValue)
        {
            var result = SettingDefinitionValidator.ValidateBuilt(
                MustSettingKey(key),
                SettingValueKind.Boolean,
                SettingValue.FromBoolean(defaultValue),
                SettingScope.User,
                0,
                false,
                null,
                null,
                null,
                default);
            Assert.That(result.Succeeded, Is.True, result.Error.Message);
            return result.Value;
        }

        private static SettingsRegistry MustSettingsRegistry(params SettingDefinition[] definitions)
        {
            var result = SettingsRegistry.Create(definitions);
            Assert.That(result.Succeeded, Is.True, result.Error.Message);
            return result.Value;
        }

        private static SettingKey MustSettingKey(string value)
        {
            var result = SettingKey.TryCreate(value);
            Assert.That(result.Succeeded, Is.True, result.Error.Message);
            return result.Value;
        }

        private static IntentId MustIntentId(string value)
        {
            var result = IntentId.TryCreate(value);
            Assert.That(result.Succeeded, Is.True, result.Error.ToString());
            return result.Value;
        }

        private static ContextId MustContextId(string value)
        {
            var result = ContextId.TryCreate(value);
            Assert.That(result.Succeeded, Is.True, result.Error.ToString());
            return result.Value;
        }

        private static RouteId MustRouteId(string value)
        {
            var result = RouteId.TryCreate(value);
            Assert.That(result.Succeeded, Is.True, result.Error.ToString());
            return result.Value;
        }

        private static SourceId MustSourceId(string value)
        {
            var result = SourceId.TryCreate(value);
            Assert.That(result.Succeeded, Is.True, result.Error.ToString());
            return result.Value;
        }

        private static SaveSlotId MustSaveSlot(string value)
        {
            var result = SaveSlotId.TryCreate(value);
            Assert.That(result.Succeeded, Is.True, result.Error.Message);
            return result.Value;
        }

        private static SaveReadCandidate Candidate(
            SaveCandidateKind kind,
            string id,
            byte[] bytes)
        {
            var candidateId = SaveCandidateId.TryCreate(id);
            Assert.That(candidateId.Succeeded, Is.True, candidateId.Error.Message);
            return new SaveReadCandidate(kind, candidateId.Value, bytes);
        }

        private sealed class InteractionFixture
        {
            public InteractionFixture(
                InteractionCoordinator coordinator,
                IntentId intentId,
                RouteId routeId,
                SourceId sourceId)
            {
                Coordinator = coordinator;
                IntentId = intentId;
                RouteId = routeId;
                SourceId = sourceId;
            }

            public InteractionCoordinator Coordinator { get; }
            public IntentId IntentId { get; }
            public RouteId RouteId { get; }
            public SourceId SourceId { get; }
        }

        private sealed class RecordingView : IInventoryView
        {
            public void Render(InventoryViewModel model)
            {
            }
        }

        private sealed class InMemorySaveStore : ISaveStore
        {
            private readonly List<SaveReadCandidate> _candidates = new List<SaveReadCandidate>();
            private SaveSlotId _slotId;
            private byte[] _lastCommittedEnvelopeBytes = Array.Empty<byte>();

            public SaveCommitCapabilities Capabilities => SaveCommitCapabilities.BestEffortWrite;

            public SaveResult<SaveReadCandidateSet> ReadCandidates(SaveSlotId slotId)
            {
                if (!_slotId.Equals(slotId) || _candidates.Count == 0)
                {
                    return SaveResult<SaveReadCandidateSet>.Fail(
                        SaveStage.Read,
                        SaveErrorCode.NotFound,
                        "No in-memory save exists for this slot.");
                }

                return SaveResult<SaveReadCandidateSet>.Success(
                    new SaveReadCandidateSet(_candidates.ToArray()));
            }

            public SaveCommitResult Commit(
                SaveSlotId slotId,
                ReadOnlyMemory<byte> envelopeBytes,
                SaveCommitCapabilities requiredCapabilities)
            {
                _slotId = slotId;
                _lastCommittedEnvelopeBytes = envelopeBytes.ToArray();
                _candidates.Clear();
                _candidates.Add(Candidate(
                    SaveCandidateKind.Primary,
                    "primary",
                    _lastCommittedEnvelopeBytes));
                return SaveCommitResult.Success();
            }

            public byte[] CopyLastCommittedEnvelopeBytes()
            {
                return (byte[])_lastCommittedEnvelopeBytes.Clone();
            }

            public void ReplaceCandidates(params SaveReadCandidate[] candidates)
            {
                _candidates.Clear();
                _candidates.AddRange(candidates ?? Array.Empty<SaveReadCandidate>());
            }
        }
    }
}
