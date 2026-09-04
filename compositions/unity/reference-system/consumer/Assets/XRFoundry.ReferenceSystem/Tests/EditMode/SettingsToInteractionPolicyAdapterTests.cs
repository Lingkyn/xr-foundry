using System;
using System.Collections.Generic;
using System.Linq;
using Lingkyn.Interaction.Core;
using Lingkyn.Settings.Core;
using NUnit.Framework;
using XRFoundry.ReferenceSystem.Bindings;

namespace XRFoundry.ReferenceSystem.Tests
{
    public sealed class SettingsToInteractionPolicyAdapterTests
    {
        [Test]
        public void InitializeSynchronizesCommittedSnapshotAndPreservesUnmanagedPolicy()
        {
            var unmanagedIntent = MustIntentId("ui.unmanaged");
            var unmanagedRoute = MustRouteId("route.unmanaged");
            var originalPolicy = MustPolicy(
                new[]
                {
                    new IntentPolicyEntry(unmanagedIntent, InteractionActivationMode.Toggle, true),
                },
                new[]
                {
                    new RoutePolicyEntry(unmanagedRoute, false, 3.0, true),
                });
            var interaction = CreateInteraction(InteractionValueKind.Button, originalPolicy);

            var intentEnabled = MustDefinition("interaction.intent.enabled", SettingValueKind.Boolean, SettingValue.FromBoolean(true));
            var activation = MustDefinition(
                "interaction.intent.activation",
                SettingValueKind.Option,
                SettingValue.FromOption(MustOption(SettingsToInteractionPolicyAdapter.HoldOption)),
                options: ActivationOptions());
            var holdTicks = MustDefinition(
                "interaction.intent.hold_ticks",
                SettingValueKind.Integer,
                SettingValue.FromInteger(25),
                numeric: new NumericConstraint(0, 100, 1));
            var threshold = MustDefinition(
                "interaction.intent.threshold",
                SettingValueKind.Float,
                SettingValue.FromFloat(0.75),
                numeric: new NumericConstraint(0, 1, 0.05));
            var routeEnabled = MustDefinition("interaction.route.enabled", SettingValueKind.Boolean, SettingValue.FromBoolean(true));
            var sensitivity = MustDefinition(
                "interaction.route.sensitivity",
                SettingValueKind.Float,
                SettingValue.FromFloat(2.0),
                numeric: new NumericConstraint(0, 4, 0.25));
            var invert = MustDefinition("interaction.route.invert", SettingValueKind.Boolean, SettingValue.FromBoolean(true));
            var registry = MustRegistry(intentEnabled, activation, holdTicks, threshold, routeEnabled, sensitivity, invert);

            var adapter = new SettingsToInteractionPolicyAdapter(
                interaction.Coordinator,
                new[]
                {
                    IntentBinding(intentEnabled, interaction.IntentId, IntentPolicySettingField.Enabled),
                    IntentBinding(activation, interaction.IntentId, IntentPolicySettingField.ActivationMode),
                    IntentBinding(holdTicks, interaction.IntentId, IntentPolicySettingField.HoldDurationTicks),
                    IntentBinding(threshold, interaction.IntentId, IntentPolicySettingField.ActivationThreshold),
                },
                new[]
                {
                    RouteBinding(routeEnabled, interaction.RouteId, RoutePolicySettingField.Enabled),
                    RouteBinding(sensitivity, interaction.RouteId, RoutePolicySettingField.Sensitivity),
                    RouteBinding(invert, interaction.RouteId, RoutePolicySettingField.Invert),
                });
            var loadedValues = registry.Definitions.ToDictionary(
                definition => Scoped(definition),
                definition => definition.DefaultValue);
            loadedValues[Scoped(intentEnabled)] = SettingValue.FromBoolean(false);
            var loadedSnapshot = new SettingsSnapshot(
                7,
                loadedValues,
                new Dictionary<string, SettingValue>());
            var settings = new SettingsCoordinator(registry, loadedSnapshot, new[] { adapter });

            var initialized = adapter.Initialize(settings);

            Assert.That(initialized.Succeeded, Is.True, initialized.Diagnostic.Message);
            Assert.That(interaction.Coordinator.Policy.TryGetIntentPolicy(interaction.IntentId, out var intentPolicy), Is.True);
            Assert.That(intentPolicy.Enabled, Is.False);
            Assert.That(intentPolicy.ActivationMode, Is.EqualTo(InteractionActivationMode.Hold));
            Assert.That(intentPolicy.HoldDurationTicks, Is.EqualTo(25));
            Assert.That(intentPolicy.ActivationThreshold, Is.EqualTo(0.75).Within(1e-9));
            Assert.That(interaction.Coordinator.Policy.TryGetRoutePolicy(interaction.RouteId, out var routePolicy), Is.True);
            Assert.That(routePolicy.Enabled, Is.True);
            Assert.That(routePolicy.Sensitivity, Is.EqualTo(2.0).Within(1e-9));
            Assert.That(routePolicy.Invert, Is.True);
            Assert.That(interaction.Coordinator.Policy.TryGetIntentPolicy(unmanagedIntent, out var retainedIntent), Is.True);
            Assert.That(retainedIntent.ActivationMode, Is.EqualTo(InteractionActivationMode.Toggle));
            Assert.That(interaction.Coordinator.Policy.TryGetRoutePolicy(unmanagedRoute, out var retainedRoute), Is.True);
            Assert.That(retainedRoute, Is.EqualTo(new RoutePolicyEntry(unmanagedRoute, false, 3.0, true)));
        }

        [Test]
        public void TransactionChangesRealInteractionRouting()
        {
            var interaction = CreateInteraction();
            var routeEnabled = MustDefinition("interaction.route.enabled", SettingValueKind.Boolean, SettingValue.FromBoolean(true));
            var registry = MustRegistry(routeEnabled);
            var adapter = new SettingsToInteractionPolicyAdapter(
                interaction.Coordinator,
                routeBindings: new[] { RouteBinding(routeEnabled, interaction.RouteId, RoutePolicySettingField.Enabled) });
            var settings = new SettingsCoordinator(registry, SettingsSnapshot.CreateInitial(registry), new[] { adapter });
            Assert.That(adapter.Initialize(settings).Succeeded, Is.True);

            var before = Activate(interaction, 10);
            Assert.That(before.Events.Single().IntentId, Is.EqualTo(interaction.IntentId));

            var transaction = settings.BeginTransaction();
            transaction.StageSet(Scoped(routeEnabled), SettingValue.FromBoolean(false));
            Assert.That(settings.Apply(transaction).Outcome, Is.EqualTo(SettingsApplyOutcome.Applied));

            var disabled = interaction.Coordinator.RouteFrame(
                MustFrame(Signal(interaction, InteractionPhase.Started, 20)));
            Assert.That(disabled.Events, Is.Empty);
            Assert.That(disabled.Diagnostics.Single().Code, Is.EqualTo(InteractionValidationCode.DisabledRoute));
        }

        [Test]
        public void InvalidPolicyTransactionLeavesBothSystemsUnchangedAndCorrectionRecovers()
        {
            var interaction = CreateInteraction();
            var threshold = MustDefinition(
                "interaction.intent.threshold",
                SettingValueKind.Float,
                SettingValue.FromFloat(0.5),
                numeric: new NumericConstraint(0, 2, 0.25));
            var registry = MustRegistry(threshold);
            var adapter = new SettingsToInteractionPolicyAdapter(
                interaction.Coordinator,
                new[] { IntentBinding(threshold, interaction.IntentId, IntentPolicySettingField.ActivationThreshold) });
            var settings = new SettingsCoordinator(registry, SettingsSnapshot.CreateInitial(registry), new[] { adapter });
            Assert.That(adapter.Initialize(settings).Succeeded, Is.True);
            var originalSettings = settings.CommittedSnapshot;
            var originalPolicy = interaction.Coordinator.Policy;

            var invalid = settings.BeginTransaction();
            invalid.StageSet(Scoped(threshold), SettingValue.FromFloat(1.5));
            var rejected = settings.Apply(invalid);

            Assert.That(rejected.Outcome, Is.EqualTo(SettingsApplyOutcome.ApplicatorFailed));
            Assert.That(rejected.PrimaryFailure.ApplicatorId, Is.EqualTo(adapter.ApplicatorId));
            Assert.That(settings.CommittedSnapshot, Is.SameAs(originalSettings));
            Assert.That(interaction.Coordinator.Policy, Is.SameAs(originalPolicy));

            var corrected = settings.BeginTransaction();
            corrected.StageSet(Scoped(threshold), SettingValue.FromFloat(0.75));
            Assert.That(settings.Apply(corrected).Outcome, Is.EqualTo(SettingsApplyOutcome.Applied));
            Assert.That(interaction.Coordinator.Policy.TryGetIntentPolicy(interaction.IntentId, out var correctedPolicy), Is.True);
            Assert.That(correctedPolicy.ActivationThreshold, Is.EqualTo(0.75).Within(1e-9));
        }

        [Test]
        public void LaterApplicatorFailureRollsBackExactPolicyAndBindingCacheBeforeRetry()
        {
            var interaction = CreateInteraction();
            var routeEnabled = MustDefinition("interaction.route.enabled", SettingValueKind.Boolean, SettingValue.FromBoolean(true));
            var sensitivity = MustDefinition(
                "interaction.route.sensitivity",
                SettingValueKind.Float,
                SettingValue.FromFloat(1.0),
                numeric: new NumericConstraint(0, 4, 0.5));
            var registry = MustRegistry(routeEnabled, sensitivity);
            var adapter = new SettingsToInteractionPolicyAdapter(
                interaction.Coordinator,
                routeBindings: new[]
                {
                    RouteBinding(routeEnabled, interaction.RouteId, RoutePolicySettingField.Enabled),
                    RouteBinding(sensitivity, interaction.RouteId, RoutePolicySettingField.Sensitivity),
                },
                order: 0);
            var later = new FailOnceApplicator(routeEnabled.Key, order: 10);
            var settings = new SettingsCoordinator(
                registry,
                SettingsSnapshot.CreateInitial(registry),
                new ISettingApplicator[] { adapter, later });
            Assert.That(adapter.Initialize(settings).Succeeded, Is.True);
            var originalPolicy = interaction.Coordinator.Policy;
            var inFlight = interaction.Coordinator.RouteFrame(
                MustFrame(Signal(interaction, InteractionPhase.Started, 10)));
            Assert.That(inFlight.Events.Single().Phase, Is.EqualTo(InteractionPhase.Started));
            Assert.That(interaction.Coordinator.State.PendingPhases, Has.Count.EqualTo(1));

            var failing = settings.BeginTransaction();
            failing.StageSet(Scoped(routeEnabled), SettingValue.FromBoolean(false));
            failing.StageSet(Scoped(sensitivity), SettingValue.FromFloat(2.0));
            Assert.That(settings.Apply(failing).Outcome, Is.EqualTo(SettingsApplyOutcome.ApplicatorFailed));
            Assert.That(interaction.Coordinator.Policy, Is.SameAs(originalPolicy));
            Assert.That(settings.CommittedSnapshot.Revision, Is.EqualTo(0));
            Assert.That(interaction.Coordinator.State.PendingPhases, Is.Empty,
                "Policy rollback must not resurrect lifecycle state canceled by the attempted change.");
            var stalePerformed = interaction.Coordinator.RouteFrame(
                MustFrame(Signal(interaction, InteractionPhase.Performed, 11)));
            Assert.That(stalePerformed.Events, Is.Empty);
            Assert.That(stalePerformed.Diagnostics.Single().Code,
                Is.EqualTo(InteractionValidationCode.InvalidPhaseTransition));

            var retry = settings.BeginTransaction();
            retry.StageSet(Scoped(routeEnabled), SettingValue.FromBoolean(false));
            Assert.That(settings.Apply(retry).Outcome, Is.EqualTo(SettingsApplyOutcome.Applied));
            Assert.That(interaction.Coordinator.Policy.TryGetRoutePolicy(interaction.RouteId, out var policy), Is.True);
            Assert.That(policy.Enabled, Is.False);
            Assert.That(policy.Sensitivity, Is.EqualTo(1.0).Within(1e-9), "Rolled-back cached sensitivity leaked into retry.");
            Assert.That(later.ApplyCount, Is.EqualTo(2));
        }

        [Test]
        public void ResetRestoresDefaultPolicyValue()
        {
            var interaction = CreateInteraction();
            var routeEnabled = MustDefinition("interaction.route.enabled", SettingValueKind.Boolean, SettingValue.FromBoolean(true));
            var registry = MustRegistry(routeEnabled);
            var adapter = new SettingsToInteractionPolicyAdapter(
                interaction.Coordinator,
                routeBindings: new[] { RouteBinding(routeEnabled, interaction.RouteId, RoutePolicySettingField.Enabled) });
            var settings = new SettingsCoordinator(registry, SettingsSnapshot.CreateInitial(registry), new[] { adapter });
            Assert.That(adapter.Initialize(settings).Succeeded, Is.True);

            var disable = settings.BeginTransaction();
            disable.StageSet(Scoped(routeEnabled), SettingValue.FromBoolean(false));
            Assert.That(settings.Apply(disable).Outcome, Is.EqualTo(SettingsApplyOutcome.Applied));
            Assert.That(interaction.Coordinator.Policy.TryGetRoutePolicy(interaction.RouteId, out var disabled), Is.True);
            Assert.That(disabled.Enabled, Is.False);

            var reset = settings.BeginTransaction();
            reset.StageReset(SettingScope.User);
            Assert.That(settings.Apply(reset).Outcome, Is.EqualTo(SettingsApplyOutcome.Applied));
            Assert.That(interaction.Coordinator.Policy.TryGetRoutePolicy(interaction.RouteId, out var restored), Is.True);
            Assert.That(restored.Enabled, Is.True);
        }

        [Test]
        public void CoarseKeyAdmissionDoesNotInventScopePrecedence()
        {
            var interaction = CreateInteraction();
            var routeEnabled = MustDefinition("interaction.route.enabled", SettingValueKind.Boolean, SettingValue.FromBoolean(true));
            var registry = MustRegistry(routeEnabled);
            var adapter = new SettingsToInteractionPolicyAdapter(
                interaction.Coordinator,
                routeBindings: new[] { RouteBinding(routeEnabled, interaction.RouteId, RoutePolicySettingField.Enabled) });
            var settings = new SettingsCoordinator(registry, SettingsSnapshot.CreateInitial(registry), new[] { adapter });
            Assert.That(adapter.Initialize(settings).Succeeded, Is.True);
            Assert.That(adapter.CanApply(routeEnabled.Key), Is.True);
            var originalPolicy = interaction.Coordinator.Policy;

            var sessionOverride = settings.BeginTransaction();
            sessionOverride.StageSet(
                new ScopedSettingKey(routeEnabled.Key, SettingScope.Session),
                SettingValue.FromBoolean(false));
            Assert.That(settings.Apply(sessionOverride).Outcome, Is.EqualTo(SettingsApplyOutcome.Applied));

            Assert.That(interaction.Coordinator.Policy, Is.SameAs(originalPolicy));
            Assert.That(interaction.Coordinator.Policy.TryGetRoutePolicy(interaction.RouteId, out var policy), Is.True);
            Assert.That(policy.Enabled, Is.True);
        }

        [Test]
        public void InitializeRejectsDuplicateFieldsAndToggleForNonButtonWithoutMutation()
        {
            var firstInteraction = CreateInteraction();
            var first = MustDefinition("interaction.route.enabled.primary", SettingValueKind.Boolean, SettingValue.FromBoolean(true));
            var second = MustDefinition("interaction.route.enabled.secondary", SettingValueKind.Boolean, SettingValue.FromBoolean(false));
            var duplicateRegistry = MustRegistry(first, second);
            var duplicate = new SettingsToInteractionPolicyAdapter(
                firstInteraction.Coordinator,
                routeBindings: new[]
                {
                    RouteBinding(first, firstInteraction.RouteId, RoutePolicySettingField.Enabled),
                    RouteBinding(second, firstInteraction.RouteId, RoutePolicySettingField.Enabled),
                });
            var duplicateSettings = new SettingsCoordinator(
                duplicateRegistry,
                SettingsSnapshot.CreateInitial(duplicateRegistry),
                new[] { duplicate });
            var duplicateOriginal = firstInteraction.Coordinator.Policy;

            var duplicateResult = duplicate.Initialize(duplicateSettings);

            Assert.That(duplicateResult.Succeeded, Is.False);
            Assert.That(duplicateResult.Diagnostic.Message, Does.Contain("bound more than once"));
            Assert.That(firstInteraction.Coordinator.Policy, Is.SameAs(duplicateOriginal));

            var scalarInteraction = CreateInteraction(InteractionValueKind.Scalar);
            var activation = MustDefinition(
                "interaction.intent.activation",
                SettingValueKind.Option,
                SettingValue.FromOption(MustOption(SettingsToInteractionPolicyAdapter.ToggleOption)),
                options: ActivationOptions());
            var toggleRegistry = MustRegistry(activation);
            var toggle = new SettingsToInteractionPolicyAdapter(
                scalarInteraction.Coordinator,
                new[] { IntentBinding(activation, scalarInteraction.IntentId, IntentPolicySettingField.ActivationMode) });
            var toggleSettings = new SettingsCoordinator(
                toggleRegistry,
                SettingsSnapshot.CreateInitial(toggleRegistry),
                new[] { toggle });
            var toggleOriginal = scalarInteraction.Coordinator.Policy;

            var toggleResult = toggle.Initialize(toggleSettings);

            Assert.That(toggleResult.Succeeded, Is.False);
            Assert.That(toggleResult.Diagnostic.Message, Does.Contain("Toggle activation requires button"));
            Assert.That(scalarInteraction.Coordinator.Policy, Is.SameAs(toggleOriginal));
        }

        private static InteractionRoutingResult Activate(InteractionFixture interaction, long ticks)
        {
            var started = interaction.Coordinator.RouteFrame(
                MustFrame(Signal(interaction, InteractionPhase.Started, ticks)));
            Assert.That(started.Events.Single().Phase, Is.EqualTo(InteractionPhase.Started));
            return interaction.Coordinator.RouteFrame(
                MustFrame(Signal(interaction, InteractionPhase.Performed, ticks + 1)));
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

        private static InteractionFixture CreateInteraction(
            InteractionValueKind valueKind = InteractionValueKind.Button,
            InteractionPolicySnapshot policy = null)
        {
            var intentId = MustIntentId("ui.primary");
            var routeId = MustRouteId("route.primary");
            var contextId = MustContextId("context.ui");
            var sourceId = MustSourceId("source.primary");
            var capabilities = InteractionValue.RequiredCapabilitiesForKind(valueKind);
            var intent = IntentDefinition.Create(intentId, valueKind, capabilities, 0);
            Assert.That(intent.Succeeded, Is.True, intent.Error.ToString());
            var route = InteractionRoute.Create(
                routeId,
                contextId,
                intentId,
                sourceId,
                InteractionModality.Gamepad,
                capabilities,
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
            var coordinator = new InteractionCoordinator(registry.Value, new[] { contextId }, policy);
            return new InteractionFixture(coordinator, intentId, routeId, sourceId);
        }

        private static IntentPolicySettingBinding IntentBinding(
            SettingDefinition definition,
            IntentId intentId,
            IntentPolicySettingField field)
            => new IntentPolicySettingBinding(Scoped(definition), intentId, field);

        private static RoutePolicySettingBinding RouteBinding(
            SettingDefinition definition,
            RouteId routeId,
            RoutePolicySettingField field)
            => new RoutePolicySettingBinding(Scoped(definition), routeId, field);

        private static ScopedSettingKey Scoped(SettingDefinition definition)
            => new ScopedSettingKey(definition.Key, definition.DefaultScope);

        private static SettingDefinition MustDefinition(
            string key,
            SettingValueKind kind,
            SettingValue defaultValue,
            NumericConstraint? numeric = null,
            OptionConstraint options = null,
            SettingScope scope = SettingScope.User)
        {
            var result = SettingDefinitionValidator.ValidateBuilt(
                MustKey(key),
                kind,
                defaultValue,
                scope,
                0,
                false,
                numeric,
                null,
                options,
                default);
            Assert.That(result.Succeeded, Is.True, result.Error.Message);
            return result.Value;
        }

        private static SettingsRegistry MustRegistry(params SettingDefinition[] definitions)
        {
            var result = SettingsRegistry.Create(definitions);
            Assert.That(result.Succeeded, Is.True, result.Error.Message);
            return result.Value;
        }

        private static InteractionPolicySnapshot MustPolicy(
            IEnumerable<IntentPolicyEntry> intents,
            IEnumerable<RoutePolicyEntry> routes)
        {
            var result = InteractionPolicySnapshot.Create(intents, routes);
            Assert.That(result.Succeeded, Is.True, result.Error.ToString());
            return result.Value;
        }

        private static OptionConstraint ActivationOptions()
            => new OptionConstraint(new[]
            {
                MustOption(SettingsToInteractionPolicyAdapter.MomentaryOption),
                MustOption(SettingsToInteractionPolicyAdapter.ToggleOption),
                MustOption(SettingsToInteractionPolicyAdapter.HoldOption),
            });

        private static SettingKey MustKey(string value)
        {
            var result = SettingKey.TryCreate(value);
            Assert.That(result.Succeeded, Is.True, result.Error.Message);
            return result.Value;
        }

        private static OptionId MustOption(string value)
        {
            var result = OptionId.TryCreate(value);
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

        private sealed class FailOnceApplicator : ISettingApplicator
        {
            private readonly SettingKey _key;

            public FailOnceApplicator(SettingKey key, int order)
            {
                _key = key;
                Order = order;
            }

            public string ApplicatorId => "test.fail-once";
            public int Order { get; }
            public int ApplyCount { get; private set; }
            public bool CanApply(SettingKey key) => key.Equals(_key);

            public SettingsApplicatorStepResult Apply(IReadOnlyList<SettingChange> changes)
            {
                ApplyCount++;
                return ApplyCount == 1
                    ? SettingsApplicatorStepResult.Fail(ApplicatorId, "Injected later applicator failure.")
                    : SettingsApplicatorStepResult.Success();
            }

            public SettingsApplicatorStepResult Rollback(IReadOnlyList<SettingChange> changes)
                => SettingsApplicatorStepResult.Success();
        }
    }
}
