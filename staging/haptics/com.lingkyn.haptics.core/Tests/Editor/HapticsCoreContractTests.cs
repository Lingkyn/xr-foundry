using System;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Lingkyn.Haptics.Core;

namespace Lingkyn.Haptics.Core.Editor.Tests
{
    // Authored and unexecuted: this assembly has never compiled or run. It is checked against
    // docs/standards/haptics/verification-contract.md and coverage-map.json. HC-13..HC-16 are the
    // LESSON-011 addition (actor, expected revision, state.stale) that the contract's literal text
    // does not name but every live family answers; see the note on HapticFailure.StateStale.
    public sealed class HapticsCoreContractTests
    {
        private static readonly HapticEventId GrabContact = HapticEventId.Parse("grab.contact");
        private static readonly HapticEventId EngineHum = HapticEventId.Parse("engine.hum");
        private static readonly HapticEventId ImpactPulse = HapticEventId.Parse("impact.pulse");
        private static readonly HapticProfileId DefaultProfile = HapticProfileId.Parse("default");
        private static readonly HapticProfileId BoostedProfile = HapticProfileId.Parse("boosted");

        private static HapticEventRegistry BuildRegistry()
        {
            var builder = new HapticEventRegistryBuilder();
            builder.Register(GrabContact, HapticKind.Transient, new AmplitudeRange(0f, 1f), 0.6f, new DurationRangeMs(10f, 200f), 40f, null, null, "grab contact");
            builder.Register(EngineHum, HapticKind.Continuous, new AmplitudeRange(0f, 1f), 0.3f, new DurationRangeMs(50f, 5000f), 500f, new FrequencyRangeHz(20f, 300f), 90f, "engine hum");
            builder.Register(ImpactPulse, HapticKind.Envelope, new AmplitudeRange(0f, 1f), 0.8f, new DurationRangeMs(10f, 500f), 120f, new FrequencyRangeHz(20f, 300f), 150f, "impact pulse");
            return builder.Build();
        }

        private static HapticProfileSet BuildProfiles()
        {
            var defaultProfile = new HapticProfileBuilder(DefaultProfile)
                .Add(HapticTarget.NamedDevice("glove_left"), 0.5f, true).Value
                .Build();
            var boosted = new HapticProfileBuilder(BoostedProfile)
                .Add(HapticTarget.Left, 2f, true).Value
                .Build();
            return HapticProfileSet.Create(new[] { defaultProfile, boosted });
        }

        private static HapticState InitialState() => HapticState.Initial(BuildRegistry(), BuildProfiles(), DefaultProfile);

        // ----- identity (HC-01) -----

        [Test]
        public void HapticEventIdCanonicalizesLowerCaseDottedSegments()
        {
            var id = HapticEventId.TryCreate("  grab.contact  ");
            Assert.That(id.Succeeded, Is.True, id.Message);
            Assert.That(id.Value.Value, Is.EqualTo("grab.contact"));
            Assert.That(id.Value.ToString(), Is.EqualTo("grab.contact"));
        }

        [Test]
        public void HapticEventIdRejectsMalformedTextWithIdentityMalformed()
        {
            foreach (var text in new[] { null, "", " ", "Grab.Contact", "grab contact", "grab..contact", ".grab", "grab.", "grab-contact", "grab\tcontact" })
            {
                var result = HapticEventId.TryCreate(text);
                Assert.That(result.Succeeded, Is.False, text ?? "<null>");
                Assert.That(result.Code, Is.EqualTo(HapticFailure.IdentityMalformed), text ?? "<null>");
                Assert.That(result.Message, Is.Not.Empty, text ?? "<null>");
            }
        }

        [Test]
        public void HapticEventIdentitiesWithSameCanonicalTextAreEqualValues()
        {
            var first = HapticEventId.Parse("grab.contact");
            var second = HapticEventId.Parse("  grab.contact ");
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first == second, Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(first != HapticEventId.Parse("grab.release"), Is.True);
            Assert.That(first.CompareTo(HapticEventId.Parse("grab.release")), Is.LessThan(0));
        }

        [Test]
        public void HapticProfileIdSharesCanonicalFormButNeverEqualsAHapticEventIdOfTheSameText()
        {
            var malformedProfile = HapticProfileId.TryCreate("Default Profile");
            Assert.That(malformedProfile.Succeeded, Is.False);
            Assert.That(malformedProfile.Code, Is.EqualTo(HapticFailure.IdentityMalformed));

            var profileId = HapticProfileId.Parse("grab.contact");
            var eventId = HapticEventId.Parse("grab.contact");
            Assert.That(profileId.Value, Is.EqualTo(eventId.Value));
            Assert.That(profileId.GetType(), Is.Not.EqualTo(eventId.GetType()));
        }

        [Test]
        public void HapticIdentityParseThrowsWithTheFailureMessageForMalformedIdentity()
        {
            var thrown = Assert.Throws<ArgumentException>(() => HapticEventId.Parse("Bad Id"));
            Assert.That(thrown.Message, Does.Contain("lower-case"));
            Assert.Throws<ArgumentException>(() => HapticEventId.Parse(""));
            Assert.Throws<ArgumentException>(() => HapticProfileId.Parse(null));
        }

        // ----- closed kind set and guard rails (HC-02) -----

        [Test]
        public void EveryHapticKindAcceptsAWellFormedDeclarationWithADefaultInsideEachGuardRail()
        {
            var transient = HapticEventDefinition.TryCreate(GrabContact, HapticKind.Transient, new AmplitudeRange(0f, 1f), 0.6f, new DurationRangeMs(10f, 200f), 40f);
            Assert.That(transient.Succeeded, Is.True, transient.Message);
            Assert.That(transient.Value.Frequency, Is.Null);

            var continuous = HapticEventDefinition.TryCreate(EngineHum, HapticKind.Continuous, new AmplitudeRange(0f, 1f), 0.3f, new DurationRangeMs(50f, 5000f), 500f, new FrequencyRangeHz(20f, 300f), 90f);
            Assert.That(continuous.Succeeded, Is.True, continuous.Message);
            Assert.That(continuous.Value.Frequency.Value.Min, Is.EqualTo(20f));

            var envelope = HapticEventDefinition.TryCreate(ImpactPulse, HapticKind.Envelope, new AmplitudeRange(0f, 1f), 0.8f, new DurationRangeMs(10f, 500f), 120f, new FrequencyRangeHz(20f, 300f), 150f);
            Assert.That(envelope.Succeeded, Is.True, envelope.Message);
        }

        [Test]
        public void KindDeclarationRejectsInvertedAmplitudeRangeWithKindDeclarationInvalid()
        {
            var result = HapticEventDefinition.TryCreate(GrabContact, HapticKind.Transient, new AmplitudeRange(0.8f, 0.2f), 0.5f, new DurationRangeMs(10f, 200f), 40f);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(HapticFailure.KindDeclarationInvalid));
        }

        [Test]
        public void KindDeclarationRejectsAnAmplitudeBoundOutsideZeroOneWithKindDeclarationInvalid()
        {
            var result = HapticEventDefinition.TryCreate(GrabContact, HapticKind.Transient, new AmplitudeRange(-0.1f, 1.4f), 0.5f, new DurationRangeMs(10f, 200f), 40f);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(HapticFailure.KindDeclarationInvalid));
        }

        [Test]
        public void KindDeclarationRejectsANonPositiveOrInvertedDurationBoundWithKindDeclarationInvalid()
        {
            var nonPositive = HapticEventDefinition.TryCreate(GrabContact, HapticKind.Transient, new AmplitudeRange(0f, 1f), 0.5f, new DurationRangeMs(0f, 200f), 40f);
            Assert.That(nonPositive.Succeeded, Is.False);
            Assert.That(nonPositive.Code, Is.EqualTo(HapticFailure.KindDeclarationInvalid));

            var inverted = HapticEventDefinition.TryCreate(GrabContact, HapticKind.Transient, new AmplitudeRange(0f, 1f), 0.5f, new DurationRangeMs(200f, 10f), 40f);
            Assert.That(inverted.Succeeded, Is.False);
            Assert.That(inverted.Code, Is.EqualTo(HapticFailure.KindDeclarationInvalid));
        }

        [Test]
        public void KindDeclarationRejectsAFrequencyRangeOnAKindThatDoesNotUseFrequencyWithKindDeclarationInvalid()
        {
            var result = HapticEventDefinition.TryCreate(GrabContact, HapticKind.Transient, new AmplitudeRange(0f, 1f), 0.5f, new DurationRangeMs(10f, 200f), 40f, new FrequencyRangeHz(20f, 300f), 90f);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(HapticFailure.KindDeclarationInvalid));
            Assert.That(HapticEventDefinition.KindUsesFrequency(HapticKind.Transient), Is.False);
            Assert.That(HapticEventDefinition.KindUsesFrequency(HapticKind.Continuous), Is.True);
            Assert.That(HapticEventDefinition.KindUsesFrequency(HapticKind.Envelope), Is.True);
        }

        [Test]
        public void KindDeclarationRejectsADefaultOutsideItsOwnGuardRailWithKindDeclarationInvalid()
        {
            var badAmplitude = HapticEventDefinition.TryCreate(GrabContact, HapticKind.Transient, new AmplitudeRange(0f, 0.5f), 0.9f, new DurationRangeMs(10f, 200f), 40f);
            Assert.That(badAmplitude.Succeeded, Is.False);
            Assert.That(badAmplitude.Code, Is.EqualTo(HapticFailure.KindDeclarationInvalid));

            var badDuration = HapticEventDefinition.TryCreate(GrabContact, HapticKind.Transient, new AmplitudeRange(0f, 1f), 0.5f, new DurationRangeMs(10f, 200f), 9f);
            Assert.That(badDuration.Succeeded, Is.False);
            Assert.That(badDuration.Code, Is.EqualTo(HapticFailure.KindDeclarationInvalid));

            var missingFrequencyDefault = HapticEventDefinition.TryCreate(EngineHum, HapticKind.Continuous, new AmplitudeRange(0f, 1f), 0.3f, new DurationRangeMs(50f, 5000f), 500f, new FrequencyRangeHz(20f, 300f), null);
            Assert.That(missingFrequencyDefault.Succeeded, Is.False);
            Assert.That(missingFrequencyDefault.Code, Is.EqualTo(HapticFailure.KindDeclarationInvalid));
        }

        // ----- immutable event registry (HC-03) -----

        [Test]
        public void RegistrationRejectsDuplicateIdWithEventDuplicateLeavingBuilderUnchanged()
        {
            var builder = new HapticEventRegistryBuilder();
            var first = builder.Register(GrabContact, HapticKind.Transient, new AmplitudeRange(0f, 1f), 0.6f, new DurationRangeMs(10f, 200f), 40f);
            Assert.That(first.Succeeded, Is.True, first.Message);

            var second = builder.Register(GrabContact, HapticKind.Transient, new AmplitudeRange(0f, 1f), 0.5f, new DurationRangeMs(5f, 50f), 20f);
            Assert.That(second.Succeeded, Is.False);
            Assert.That(second.Code, Is.EqualTo(HapticFailure.EventDuplicate));
            Assert.That(builder.Count, Is.EqualTo(1));
        }

        [Test]
        public void RegistrationRejectsAnInvalidDeclarationBeforeCheckingDuplicates()
        {
            var builder = new HapticEventRegistryBuilder();
            var result = builder.Register(GrabContact, HapticKind.Transient, new AmplitudeRange(1f, 0f), 0.5f, new DurationRangeMs(10f, 200f), 40f);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(HapticFailure.KindDeclarationInvalid));
            Assert.That(builder.Count, Is.EqualTo(0));
        }

        [Test]
        public void BuiltRegistryEnumeratesInCanonicalIdOrderAndCannotBeMutatedAfterwards()
        {
            var builder = new HapticEventRegistryBuilder();
            builder.Register(ImpactPulse, HapticKind.Envelope, new AmplitudeRange(0f, 1f), 0.8f, new DurationRangeMs(10f, 500f), 120f, new FrequencyRangeHz(20f, 300f), 150f);
            builder.Register(GrabContact, HapticKind.Transient, new AmplitudeRange(0f, 1f), 0.6f, new DurationRangeMs(10f, 200f), 40f);
            var registry = builder.Build();

            var ids = registry.Events.Select(definition => definition.Id.Value).ToList();
            Assert.That(ids, Is.EqualTo(new[] { "grab.contact", "impact.pulse" }));

            builder.Register(EngineHum, HapticKind.Continuous, new AmplitudeRange(0f, 1f), 0.3f, new DurationRangeMs(50f, 5000f), 500f, new FrequencyRangeHz(20f, 300f), 90f);
            Assert.That(registry.Count, Is.EqualTo(2), "a registry already built must not see a later registration");
        }

        // ----- per-target profiles over the closed target set (HC-04) -----

        [Test]
        public void ProfileBuilderRejectsAScaleOutsideTheDocumentedPositiveRangeWithProfileDeclarationInvalid()
        {
            var tooLow = new HapticProfileBuilder(DefaultProfile).Add(HapticTarget.Left, 0f, true);
            Assert.That(tooLow.Succeeded, Is.False);
            Assert.That(tooLow.Code, Is.EqualTo(HapticFailure.ProfileDeclarationInvalid));

            var tooHigh = new HapticProfileBuilder(DefaultProfile).Add(HapticTarget.Left, 10f, true);
            Assert.That(tooHigh.Succeeded, Is.False);
            Assert.That(tooHigh.Code, Is.EqualTo(HapticFailure.ProfileDeclarationInvalid));
        }

        [Test]
        public void ProfileBuilderRejectsADuplicateTargetWithinOneProfileWithProfileDeclarationInvalid()
        {
            var builder = new HapticProfileBuilder(DefaultProfile);
            var first = builder.Add(HapticTarget.Left, 1.5f, true);
            Assert.That(first.Succeeded, Is.True, first.Message);

            var second = builder.Add(HapticTarget.Left, 2f, false);
            Assert.That(second.Succeeded, Is.False);
            Assert.That(second.Code, Is.EqualTo(HapticFailure.ProfileDeclarationInvalid));
            Assert.That(builder.Count, Is.EqualTo(1));
        }

        [Test]
        public void AnUnlistedTargetDefaultsToEnabledWithScaleOfOne()
        {
            var profile = new HapticProfileBuilder(DefaultProfile).Add(HapticTarget.Left, 2f, true).Value.Build();
            Assert.That(profile.Resolve(HapticTarget.Left), Is.EqualTo((2f, true)));
            Assert.That(profile.Resolve(HapticTarget.Right), Is.EqualTo((1f, true)));
            Assert.That(profile.Resolve(HapticTarget.Both), Is.EqualTo((1f, true)));
            Assert.That(profile.Resolve(HapticTarget.NamedDevice("glove_right")), Is.EqualTo((1f, true)));
        }

        [Test]
        public void HapticProfileSetCreateThrowsOnANullProfileOrADuplicateProfileId()
        {
            Assert.Throws<ArgumentException>(() => HapticProfileSet.Create(new HapticProfile[] { null }));
            var one = new HapticProfileBuilder(DefaultProfile).Build();
            var two = new HapticProfileBuilder(DefaultProfile).Build();
            Assert.Throws<ArgumentException>(() => HapticProfileSet.Create(new[] { one, two }));
        }

        // ----- play/stop/stop_all/set_profile intents (HC-05) -----

        [Test]
        public void PlayRejectsAnUnregisteredEventWithEventUnknown()
        {
            var state = InitialState();
            var result = state.Apply(new PlayIntent(HapticEventId.Parse("no.such.event"), HapticTarget.Left));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(HapticFailure.EventUnknown));
        }

        [Test]
        public void PlayRejectsAnUnregisteredTargetWithTargetUnknown()
        {
            var state = InitialState();
            var result = state.Apply(new PlayIntent(GrabContact, HapticTarget.NamedDevice("glove_right")));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(HapticFailure.TargetUnknown));
        }

        [Test]
        public void PlayRejectsAFrequencyOverrideOnAKindThatDoesNotUseFrequencyWithKindMismatch()
        {
            var state = InitialState();
            var result = state.Apply(new PlayIntent(GrabContact, HapticTarget.Left, null, null, 90f));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(HapticFailure.KindMismatch));
        }

        [Test]
        public void PlayRejectsAnAmplitudeOverrideOrDefaultOutsideTheGuardRailWithAmplitudeOutOfRange()
        {
            var state = InitialState();
            var result = state.Apply(new PlayIntent(GrabContact, HapticTarget.Left, 1.5f));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(HapticFailure.AmplitudeOutOfRange));
        }

        [Test]
        public void PlayRejectsADurationOverrideOrDefaultOutsideTheGuardRailWithDurationOutOfRange()
        {
            var state = InitialState();
            var result = state.Apply(new PlayIntent(GrabContact, HapticTarget.Left, null, 5000f));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(HapticFailure.DurationOutOfRange));
        }

        [Test]
        public void SetProfileRejectsAnUnregisteredProfileIdWithProfileUnknown()
        {
            var state = InitialState();
            var result = state.Apply(new SetProfileIntent(HapticProfileId.Parse("no.such.profile")));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(HapticFailure.ProfileUnknown));
        }

        [Test]
        public void PlayAcceptsTheDefaultAmplitudeAndDurationWhenNoOverrideIsSupplied()
        {
            var state = InitialState();
            var result = state.Apply(new PlayIntent(GrabContact, HapticTarget.Left));
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Value.TryGetActive(HapticTarget.Left, out var playback), Is.True);
            Assert.That(playback.Amplitude, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(playback.DurationMs, Is.EqualTo(40f));
        }

        [Test]
        public void PlayOnANamedDeviceTargetTheActiveProfileDeclaresIsAccepted()
        {
            var state = InitialState();
            var result = state.Apply(new PlayIntent(GrabContact, HapticTarget.NamedDevice("glove_left")));
            Assert.That(result.Succeeded, Is.True, result.Message);
        }

        // ----- last-one-wins per target (HC-06) -----

        [Test]
        public void PlayAcceptedForATargetThatAlreadyHoldsAnActivePlaybackReplacesIt()
        {
            var state = InitialState();
            state = state.Apply(new PlayIntent(GrabContact, HapticTarget.Left)).Value;
            var replaced = state.Apply(new PlayIntent(ImpactPulse, HapticTarget.Left, 0.5f, 60f, 90f));
            Assert.That(replaced.Succeeded, Is.True, replaced.Message);
            Assert.That(replaced.Value.TryGetActive(HapticTarget.Left, out var playback), Is.True);
            Assert.That(playback.EventId, Is.EqualTo(ImpactPulse));
            Assert.That(replaced.Value.ActiveTargets.Count, Is.EqualTo(1));
        }

        [Test]
        public void StopClearsExactlyTheNamedTargetsActivePlaybackAndIsANoOpSuccessWhenItHoldsNone()
        {
            var state = InitialState();
            state = state.Apply(new PlayIntent(GrabContact, HapticTarget.Left)).Value;
            state = state.Apply(new PlayIntent(EngineHum, HapticTarget.Right)).Value;

            var stopped = state.Apply(new StopIntent(HapticTarget.Left));
            Assert.That(stopped.Succeeded, Is.True, stopped.Message);
            Assert.That(stopped.Value.TryGetActive(HapticTarget.Left, out _), Is.False);
            Assert.That(stopped.Value.TryGetActive(HapticTarget.Right, out _), Is.True);

            var noOp = stopped.Value.Apply(new StopIntent(HapticTarget.Left));
            Assert.That(noOp.Succeeded, Is.True, noOp.Message);
        }

        [Test]
        public void StopAllClearsEveryTargetsActivePlaybackAndNeverTouchesTheActiveProfile()
        {
            var state = InitialState();
            state = state.Apply(new PlayIntent(GrabContact, HapticTarget.Left)).Value;
            state = state.Apply(new PlayIntent(EngineHum, HapticTarget.Right)).Value;
            state = state.Apply(new SetProfileIntent(BoostedProfile)).Value;

            var stopped = state.Apply(new StopAllIntent());
            Assert.That(stopped.Succeeded, Is.True, stopped.Message);
            Assert.That(stopped.Value.ActiveTargets, Is.Empty);
            Assert.That(stopped.Value.ActiveProfileId, Is.EqualTo(BoostedProfile));
        }

        [Test]
        public void SetProfileNeverTouchesAlreadyActivePlaybacks()
        {
            var state = InitialState();
            state = state.Apply(new PlayIntent(GrabContact, HapticTarget.Left)).Value;
            var beforeAmplitude = state.TryGetActive(HapticTarget.Left, out var before) ? before.Amplitude : -1f;

            var switched = state.Apply(new SetProfileIntent(BoostedProfile));
            Assert.That(switched.Succeeded, Is.True, switched.Message);
            Assert.That(switched.Value.TryGetActive(HapticTarget.Left, out var after), Is.True);
            Assert.That(after.Amplitude, Is.EqualTo(beforeAmplitude));
        }

        // ----- state immutability (HC-07) -----

        [Test]
        public void AcceptedIntentProducesNewStateWhilePriorStateStaysReadableAndEqualToItself()
        {
            var state = InitialState();
            var result = state.Apply(new PlayIntent(GrabContact, HapticTarget.Left));
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(state.ActiveTargets, Is.Empty, "the prior state must stay intact");
            Assert.That(state.Equals(state), Is.True);
        }

        [Test]
        public void RejectedIntentLeavesPriorStateIntactWithTheFailureCode()
        {
            var state = InitialState();
            var result = state.Apply(new PlayIntent(HapticEventId.Parse("no.such.event"), HapticTarget.Left));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(HapticFailure.EventUnknown));
            Assert.That(state.ActiveTargets, Is.Empty);
            Assert.That(state.Revision, Is.EqualTo(0));
        }

        // ----- deterministic replay (HC-08) -----

        [Test]
        public void SameIntentSequenceOverTheSameRegistryProfileSetAndInitialStateProducesEqualStateAndFingerprint()
        {
            var registry = BuildRegistry();
            var profiles = BuildProfiles();
            var sequence = new HapticIntent[]
            {
                new PlayIntent(GrabContact, HapticTarget.Left),
                new PlayIntent(EngineHum, HapticTarget.Right),
                new SetProfileIntent(BoostedProfile),
            };

            var replayA = HapticState.Initial(registry, profiles, DefaultProfile).ApplyAll(sequence);
            var replayB = HapticState.Initial(registry, profiles, DefaultProfile).ApplyAll(sequence);

            Assert.That(replayA.State.Equals(replayB.State), Is.True);
            Assert.That(replayA.State.Fingerprint(), Is.EqualTo(replayB.State.Fingerprint()));
        }

        [Test]
        public void TwoStatesWithTheSameNetActivePlaybacksReachedByDifferentSequencesHaveEqualFingerprints()
        {
            var registry = BuildRegistry();
            var profiles = BuildProfiles();

            var direct = HapticState.Initial(registry, profiles, DefaultProfile)
                .Apply(new PlayIntent(GrabContact, HapticTarget.Left)).Value;

            var viaExtraSteps = HapticState.Initial(registry, profiles, DefaultProfile)
                .Apply(new PlayIntent(EngineHum, HapticTarget.Left)).Value
                .Apply(new StopIntent(HapticTarget.Left)).Value
                .Apply(new PlayIntent(GrabContact, HapticTarget.Left)).Value;

            Assert.That(direct.Fingerprint(), Is.EqualTo(viaExtraSteps.Fingerprint()));
            Assert.That(direct.Revision, Is.Not.EqualTo(viaExtraSteps.Revision), "revision is excluded from the fingerprint");
        }

        // ----- binding table (HC-09) -----

        [Test]
        public void BindingValidationRejectsAnUnregisteredHapticEventWithEventUnknown()
        {
            var registry = BuildRegistry();
            var bindings = new[] { new HapticBinding(HapticSourceKind.InteractionIntent, "grab.begin", HapticEventId.Parse("no.such.event"), HapticTarget.Left) };
            var result = HapticBindingTable.Validate(bindings, registry);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(HapticFailure.EventUnknown));
        }

        [Test]
        public void BindingValidationRejectsASecondBindingForTheSameSourceKindAndIdWithBindingDuplicate()
        {
            var registry = BuildRegistry();
            var bindings = new[]
            {
                new HapticBinding(HapticSourceKind.InteractionIntent, "grab.begin", GrabContact, HapticTarget.Left),
                new HapticBinding(HapticSourceKind.InteractionIntent, "grab.begin", ImpactPulse, HapticTarget.Right),
            };
            var result = HapticBindingTable.Validate(bindings, registry);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(HapticFailure.BindingDuplicate));
        }

        [Test]
        public void ValidatedBindingTableEnumeratesInCanonicalSourceKindThenSourceIdOrder()
        {
            var registry = BuildRegistry();
            var bindings = new[]
            {
                new HapticBinding(HapticSourceKind.AudioEvent, "impact.hit", ImpactPulse, HapticTarget.Both),
                new HapticBinding(HapticSourceKind.InteractionIntent, "grab.begin", GrabContact, HapticTarget.Left),
                new HapticBinding(HapticSourceKind.InteractionIntent, "engine.start", EngineHum, HapticTarget.Right),
            };
            var table = HapticBindingTable.Validate(bindings, registry).Value;
            var order = table.Bindings.Select(binding => (binding.SourceKind, binding.SourceId)).ToList();
            Assert.That(order, Is.EqualTo(new[]
            {
                (HapticSourceKind.InteractionIntent, "engine.start"),
                (HapticSourceKind.InteractionIntent, "grab.begin"),
                (HapticSourceKind.AudioEvent, "impact.hit"),
            }));
        }

        [Test]
        public void ATableResolvesOneSourceKindAndIdToAtMostOneBinding()
        {
            var registry = BuildRegistry();
            var bindings = new[] { new HapticBinding(HapticSourceKind.InteractionIntent, "grab.begin", GrabContact, HapticTarget.Left) };
            var table = HapticBindingTable.Validate(bindings, registry).Value;
            Assert.That(table.TryGet(HapticSourceKind.InteractionIntent, "grab.begin", out var found), Is.True);
            Assert.That(found.EventId, Is.EqualTo(GrabContact));
            Assert.That(table.TryGet(HapticSourceKind.AudioEvent, "grab.begin", out _), Is.False, "the same id under a different source kind is a different key");
        }

        // ----- composed dispatch (HC-10) -----

        [Test]
        public void DispatchResolvesABoundSourceToAHapticPlayIntentIndependentlyOfAnyOtherFamily()
        {
            var registry = BuildRegistry();
            var bindings = new[] { new HapticBinding(HapticSourceKind.InteractionIntent, "grab.begin", GrabContact, HapticTarget.Left) };
            var table = HapticBindingTable.Validate(bindings, registry).Value;

            var first = table.Dispatch(HapticSourceKind.InteractionIntent, "grab.begin");
            var second = table.Dispatch(HapticSourceKind.InteractionIntent, "grab.begin");

            Assert.That(first.Bound, Is.True);
            Assert.That(second.Bound, Is.True);
            Assert.That(first.Intent.EventId, Is.EqualTo(second.Intent.EventId));
            Assert.That(first.Intent.Target, Is.EqualTo(second.Intent.Target));
            Assert.That(table.Count, Is.EqualTo(1), "dispatch is a pure lookup with no side effect on the table");
        }

        [Test]
        public void DispatchOnAnUnboundSourceProducesANamedBindingUnboundResultRatherThanSilence()
        {
            var registry = BuildRegistry();
            var table = HapticBindingTable.Validate(Array.Empty<HapticBinding>(), registry).Value;

            var outcome = table.Dispatch(HapticSourceKind.AudioEvent, "no.such.source");
            Assert.That(outcome.Bound, Is.False);
            Assert.That(outcome.Code, Is.EqualTo(HapticFailure.BindingUnbound));
            Assert.That(outcome.SourceKind, Is.EqualTo(HapticSourceKind.AudioEvent));
            Assert.That(outcome.SourceId, Is.EqualTo("no.such.source"));
            Assert.That(outcome.Message, Is.Not.Empty);
        }

        [Test]
        public void DispatchOnTwoIndependentlyConstructedTablesForTheSameBoundSourceDoesNotInterfere()
        {
            var registry = BuildRegistry();
            var bindings = new[] { new HapticBinding(HapticSourceKind.AudioEvent, "impact.hit", ImpactPulse, HapticTarget.Both) };
            var tableOne = HapticBindingTable.Validate(bindings, registry).Value;
            var tableTwo = HapticBindingTable.Validate(bindings, registry).Value;

            var fromOne = tableOne.Dispatch(HapticSourceKind.AudioEvent, "impact.hit");
            var fromTwo = tableTwo.Dispatch(HapticSourceKind.AudioEvent, "impact.hit");

            Assert.That(fromOne.Bound, Is.True);
            Assert.That(fromTwo.Bound, Is.True);
            Assert.That(fromOne.Intent.EventId, Is.EqualTo(fromTwo.Intent.EventId));
            Assert.That(tableOne.Count, Is.EqualTo(tableTwo.Count));
        }

        // ----- structured results with stable codes (HC-11) -----

        [Test]
        public void EveryRejectionAboveCarriesAStableNonEmptyCodeAndTheOffendingIdOrTargetInItsMessage()
        {
            var state = InitialState();
            var unknownEvent = state.Apply(new PlayIntent(HapticEventId.Parse("no.such.event"), HapticTarget.Left));
            Assert.That(unknownEvent.Message, Does.Contain("no.such.event"));
            var unknownTarget = state.Apply(new PlayIntent(GrabContact, HapticTarget.NamedDevice("glove_right")));
            Assert.That(unknownTarget.Message, Does.Contain("glove_right"));
        }

        [Test]
        public void AValidIntentSequencePassesCleanWithEveryOutcomeAccepted()
        {
            var state = InitialState();
            var sequence = new HapticIntent[]
            {
                new PlayIntent(GrabContact, HapticTarget.Left),
                new PlayIntent(EngineHum, HapticTarget.Right),
                new SetProfileIntent(BoostedProfile),
                new StopIntent(HapticTarget.Left),
                new StopAllIntent(),
            };
            var result = state.ApplyAll(sequence);
            Assert.That(result.AllAccepted, Is.True, string.Join(", ", result.Outcomes.Where(outcome => !outcome.Accepted).Select(outcome => outcome.Code)));
            Assert.That(result.AcceptedCount, Is.EqualTo(sequence.Length));
            Assert.That(result.RejectedCount, Is.EqualTo(0));
        }

        // ----- Core purity (HC-12) -----

        [Test]
        public void CoreAssemblyReferencesNoUnityEngineAssembly()
        {
            var assembly = typeof(HapticEventRegistry).Assembly;
            var referenced = assembly.GetReferencedAssemblies().Select(name => name.Name ?? string.Empty);
            Assert.That(referenced.Any(name => name.IndexOf("UnityEngine", StringComparison.OrdinalIgnoreCase) >= 0), Is.False, string.Join(",", referenced));
        }

        [Test]
        public void NoCoreRuntimeSourceFileMentionsUnityEngineOrAnAssetOrActuatorType()
        {
            var runtimeDirectory = System.IO.Path.GetFullPath(System.IO.Path.Combine(TestSourceDirectory(), "..", "..", "Runtime"));
            Assert.That(System.IO.Directory.Exists(runtimeDirectory), Is.True, runtimeDirectory);

            var forbidden = new[] { "UnityEngine", "AudioSource", "ScriptableObject" };
            var sourceFiles = System.IO.Directory.GetFiles(runtimeDirectory, "*.cs", System.IO.SearchOption.TopDirectoryOnly);
            Assert.That(sourceFiles, Is.Not.Empty, runtimeDirectory);
            foreach (var sourceFile in sourceFiles)
            {
                var text = System.IO.File.ReadAllText(sourceFile);
                foreach (var token in forbidden)
                {
                    Assert.That(text, Does.Not.Contain(token), $"{System.IO.Path.GetFileName(sourceFile)} must not reference '{token}'.");
                }
            }
        }

        [Test]
        public void ANamedDeviceTargetsDeviceIdEntersAndLeavesTheCoreAsAnOpaqueString()
        {
            const string deviceId = "vendor.controller.left-42";
            var target = HapticTarget.NamedDevice(deviceId);
            Assert.That(target.DeviceId, Is.EqualTo(deviceId));
            Assert.That(target.ToString(), Does.Contain(deviceId));
        }

        // ----- LESSON-011: actor and expected revision (HC-13) -----

        [Test]
        public void EveryIntentDefaultsToPlayerActorWithNoExpectedRevisionWhenUsingTheShortConstructor()
        {
            var play = new PlayIntent(GrabContact, HapticTarget.Left);
            var stop = new StopIntent(HapticTarget.Left);
            var stopAll = new StopAllIntent();
            var setProfile = new SetProfileIntent(BoostedProfile);

            foreach (HapticIntent intent in new HapticIntent[] { play, stop, stopAll, setProfile })
            {
                Assert.That(intent.Actor, Is.EqualTo(IntentActor.Player), intent.Describe());
                Assert.That(intent.ExpectedRevision, Is.Null, intent.Describe());
            }
        }

        [Test]
        public void EveryIntentAcceptsAnExplicitActorFromTheClosedSetAndAnExpectedRevision()
        {
            var play = new PlayIntent(GrabContact, HapticTarget.Left, null, null, null, IntentActor.Agent, 3);
            Assert.That(play.Actor, Is.EqualTo(IntentActor.Agent));
            Assert.That(play.ExpectedRevision, Is.EqualTo(3));

            var stop = new StopIntent(HapticTarget.Left, IntentActor.Replay, 1);
            Assert.That(stop.Actor, Is.EqualTo(IntentActor.Replay));

            var import = new SetProfileIntent(BoostedProfile, IntentActor.Import, 0);
            Assert.That(import.Actor, Is.EqualTo(IntentActor.Import));
        }

        [Test]
        public void StateRevisionStartsAtZeroAndIncreasesByExactlyOneOnEveryAcceptedIntentButNeverOnARejected()
        {
            var state = InitialState();
            Assert.That(state.Revision, Is.EqualTo(0));

            var accepted = state.Apply(new PlayIntent(GrabContact, HapticTarget.Left));
            Assert.That(accepted.Succeeded, Is.True, accepted.Message);
            Assert.That(accepted.Value.Revision, Is.EqualTo(1));

            var rejected = accepted.Value.Apply(new PlayIntent(HapticEventId.Parse("no.such.event"), HapticTarget.Left));
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(accepted.Value.Revision, Is.EqualTo(1), "a rejected intent must not advance the revision it was applied to");
        }

        // ----- LESSON-011: stale expected revision (HC-14) -----

        [Test]
        public void AStaleExpectedRevisionIsRejectedWithStateStaleAndChangesNothing()
        {
            var state = InitialState().Apply(new PlayIntent(GrabContact, HapticTarget.Left)).Value;
            Assert.That(state.Revision, Is.EqualTo(1));

            var stale = state.Apply(new PlayIntent(EngineHum, HapticTarget.Right, null, null, null, IntentActor.Player, 0));
            Assert.That(stale.Succeeded, Is.False);
            Assert.That(stale.Code, Is.EqualTo(HapticFailure.StateStale));
            Assert.That(state.ActiveTargets.Count, Is.EqualTo(1));
        }

        [Test]
        public void EveryIntentTypeRejectsAStaleExpectedRevisionWithStateStaleRegardlessOfActor()
        {
            var state = InitialState().Apply(new PlayIntent(GrabContact, HapticTarget.Left)).Value;
            var staleIntents = new HapticIntent[]
            {
                new PlayIntent(EngineHum, HapticTarget.Right, null, null, null, IntentActor.Agent, 0),
                new StopIntent(HapticTarget.Left, IntentActor.Replay, 0),
                new StopAllIntent(IntentActor.Import, 0),
                new SetProfileIntent(BoostedProfile, IntentActor.Player, 0),
            };
            foreach (var intent in staleIntents)
            {
                var result = state.Apply(intent);
                Assert.That(result.Succeeded, Is.False, intent.Describe());
                Assert.That(result.Code, Is.EqualTo(HapticFailure.StateStale), intent.Describe());
            }
        }

        [Test]
        public void AMatchingExpectedRevisionIsAcceptedExactlyLikeNoExpectedRevisionAtAll()
        {
            var state = InitialState();
            var withRevision = state.Apply(new PlayIntent(GrabContact, HapticTarget.Left, null, null, null, IntentActor.Player, 0));
            var withoutRevision = state.Apply(new PlayIntent(GrabContact, HapticTarget.Left));
            Assert.That(withRevision.Succeeded, Is.True, withRevision.Message);
            Assert.That(withoutRevision.Succeeded, Is.True, withoutRevision.Message);
            Assert.That(withRevision.Value.Fingerprint(), Is.EqualTo(withoutRevision.Value.Fingerprint()));
        }

        // ----- LESSON-011: actor never changes validation (HC-15) -----

        [Test]
        public void PlayerAndAgentIssuingTheSameAcceptedIntentTakeTheSamePathAndProduceEqualResultingState()
        {
            var state = InitialState();
            var byPlayer = state.Apply(new PlayIntent(GrabContact, HapticTarget.Left));
            var byAgent = state.Apply(new PlayIntent(GrabContact, HapticTarget.Left, null, null, null, IntentActor.Agent));
            Assert.That(byPlayer.Succeeded, Is.True, byPlayer.Message);
            Assert.That(byAgent.Succeeded, Is.True, byAgent.Message);
            Assert.That(byPlayer.Value.Fingerprint(), Is.EqualTo(byAgent.Value.Fingerprint()));
        }

        [Test]
        public void PlayerAndAgentIssuingTheSameRejectedIntentGetTheSameFailureCode()
        {
            var state = InitialState();
            var byPlayer = state.Apply(new PlayIntent(HapticEventId.Parse("no.such.event"), HapticTarget.Left));
            var byAgent = state.Apply(new PlayIntent(HapticEventId.Parse("no.such.event"), HapticTarget.Left, null, null, null, IntentActor.Agent));
            Assert.That(byPlayer.Code, Is.EqualTo(byAgent.Code));
        }

        // ----- LESSON-011: the replay log (HC-16) -----

        [Test]
        public void ReplayLogRecordsTheIssuingActorAndTheRevisionAfterEveryOutcomeAcceptedOrRejected()
        {
            var state = InitialState();
            var sequence = new HapticIntent[]
            {
                new PlayIntent(GrabContact, HapticTarget.Left, null, null, null, IntentActor.Player),
                new PlayIntent(HapticEventId.Parse("no.such.event"), HapticTarget.Left, null, null, null, IntentActor.Agent),
                new StopIntent(HapticTarget.Left, IntentActor.Replay),
            };
            var result = state.ApplyAll(sequence);
            Assert.That(result.Outcomes.Count, Is.EqualTo(3));
            Assert.That(result.Outcomes[0].Actor, Is.EqualTo(IntentActor.Player));
            Assert.That(result.Outcomes[0].Accepted, Is.True);
            Assert.That(result.Outcomes[0].RevisionAfter, Is.EqualTo(1));
            Assert.That(result.Outcomes[1].Actor, Is.EqualTo(IntentActor.Agent));
            Assert.That(result.Outcomes[1].Accepted, Is.False);
            Assert.That(result.Outcomes[1].RevisionAfter, Is.EqualTo(1), "a rejected outcome records the revision it was applied to, unchanged");
            Assert.That(result.Outcomes[2].Actor, Is.EqualTo(IntentActor.Replay));
            Assert.That(result.Outcomes[2].RevisionAfter, Is.EqualTo(2));
        }

        private static string TestSourceDirectory([CallerFilePath] string sourceFilePath = "") => System.IO.Path.GetDirectoryName(sourceFilePath);
    }
}
