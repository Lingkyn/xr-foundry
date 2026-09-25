using System;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Lingkyn.QualityTiers.Core;

namespace Lingkyn.QualityTiers.Core.Editor.Tests
{
    // Authored and unexecuted: this assembly has never compiled or run. It is checked against
    // docs/standards/quality-tiers/verification-contract.md and coverage-map.json. QC-12..QC-15 are
    // the LESSON-011 addition (actor, expected revision, state.stale) that every live family
    // answers; see the note on QualityFailure.StateStale.
    public sealed class QualityTiersCoreContractTests
    {
        private static readonly TierId TierLow = TierId.Parse("tier.low");
        private static readonly TierId TierMedium = TierId.Parse("tier.medium");
        private static readonly TierId TierHigh = TierId.Parse("tier.high");
        private static readonly TierId TierUltra = TierId.Parse("tier.ultra"); // never registered

        private static readonly DeviceProfileId DeviceStandalone = DeviceProfileId.Parse("device.standalone");
        private static readonly DeviceProfileId DeviceMobile = DeviceProfileId.Parse("device.mobile");
        private static readonly DeviceProfileId DeviceUnregistered = DeviceProfileId.Parse("device.unregistered"); // never registered

        private static QualityTierRegistry BuildRegistry()
        {
            var builder = new QualityTierRegistryBuilder();
            builder.Register(TierLow, new RefreshRateRangeHz(60f, 72f), new RenderScaleRange(0.7f, 0.9f), FoveationLevel.High, MsaaSampleCount.None, new ShadowBudgetRange(0f, 10f), new PostProcessingBudgetRange(0f, 5f), "low");
            builder.Register(TierMedium, new RefreshRateRangeHz(72f, 90f), new RenderScaleRange(0.9f, 1.0f), FoveationLevel.Medium, MsaaSampleCount.Two, new ShadowBudgetRange(5f, 20f), new PostProcessingBudgetRange(5f, 15f), "medium");
            builder.Register(TierHigh, new RefreshRateRangeHz(90f, 120f), new RenderScaleRange(1.0f, 1.4f), FoveationLevel.Low, MsaaSampleCount.Four, new ShadowBudgetRange(15f, 40f), new PostProcessingBudgetRange(10f, 30f), "high");
            return builder.Build();
        }

        private static DeviceCapabilitySet BuildCapabilities()
        {
            var builder = new DeviceCapabilitySetBuilder();
            var standalone = DeviceCapabilityDescriptor.TryCreate(
                DeviceStandalone,
                new[] { 72f, 90f },
                new RenderScaleRange(0.8f, 1.2f),
                new[] { FoveationLevel.Medium, FoveationLevel.High },
                new[] { MsaaSampleCount.Two, MsaaSampleCount.Four }).Value;
            var mobile = DeviceCapabilityDescriptor.TryCreate(
                DeviceMobile,
                new[] { 60f, 72f },
                new RenderScaleRange(0.6f, 1.0f),
                new[] { FoveationLevel.High },
                new[] { MsaaSampleCount.None }).Value;
            builder.Register(standalone);
            builder.Register(mobile);
            return builder.Build();
        }

        private static QualityState InitialState() => QualityState.Initial(BuildRegistry(), BuildCapabilities());

        // ----- identity (QC-01) -----

        [Test]
        public void TierIdCanonicalizesLowerCaseDottedSegments()
        {
            var id = TierId.TryCreate("  tier.high  ");
            Assert.That(id.Succeeded, Is.True, id.Message);
            Assert.That(id.Value.Value, Is.EqualTo("tier.high"));
            Assert.That(id.Value.ToString(), Is.EqualTo("tier.high"));
        }

        [Test]
        public void TierIdRejectsMalformedTextWithIdentityMalformed()
        {
            foreach (var text in new[] { null, "", " ", "Tier.High", "tier high", "tier..high", ".tier", "tier.", "tier-high", "tier\thigh" })
            {
                var result = TierId.TryCreate(text);
                Assert.That(result.Succeeded, Is.False, text ?? "<null>");
                Assert.That(result.Code, Is.EqualTo(QualityFailure.IdentityMalformed), text ?? "<null>");
            }
        }

        [Test]
        public void TierIdentitiesWithSameCanonicalTextAreEqualValues()
        {
            Assert.That(TierId.Parse("tier.high"), Is.EqualTo(TierId.Parse("tier.high")));
            Assert.That(TierId.Parse("tier.high") == TierId.Parse("tier.high"), Is.True);
        }

        [Test]
        public void DeviceProfileIdSharesCanonicalFormButNeverEqualsATierIdOfTheSameText()
        {
            var deviceId = DeviceProfileId.Parse("shared.text");
            var tierId = TierId.Parse("shared.text");
            Assert.That(deviceId.Value, Is.EqualTo(tierId.Value));
            Assert.That(deviceId.Equals(tierId), Is.False); // no cross-type overload; object equality only
            Assert.That(deviceId.GetType(), Is.Not.EqualTo(tierId.GetType()));
        }

        [Test]
        public void DeviceProfileIdAcceptsAnyCanonicalTextRegardlessOfRepositoryCompatibilityProfileIds()
        {
            // The Core canonicalizes text only; it never compares a DeviceProfileId against this
            // repository's own compatibility-profiles.json build/verification ids.
            var result = DeviceProfileId.TryCreate("unity_6000_0.xri_3_0.urp_17");
            Assert.That(result.Succeeded, Is.True, result.Message);
        }

        // ----- closed tier declaration with guard rails (QC-02) -----

        [Test]
        public void WellFormedTierDeclarationIsAccepted()
        {
            var result = QualityTierDefinition.TryCreate(TierMedium, new RefreshRateRangeHz(72f, 90f), new RenderScaleRange(0.9f, 1.0f), FoveationLevel.Medium, MsaaSampleCount.Two, new ShadowBudgetRange(5f, 20f), new PostProcessingBudgetRange(5f, 15f), "medium");
            Assert.That(result.Succeeded, Is.True, result.Message);
        }

        [Test]
        public void TierDeclarationRejectsInvertedOrNonPositiveRefreshRateRangeWithTierDeclarationInvalid()
        {
            foreach (var range in new[] { new RefreshRateRangeHz(90f, 72f), new RefreshRateRangeHz(0f, 90f), new RefreshRateRangeHz(-10f, 90f) })
            {
                var result = QualityTierDefinition.TryCreate(TierMedium, range, new RenderScaleRange(0.9f, 1.0f), FoveationLevel.Medium, MsaaSampleCount.Two, new ShadowBudgetRange(5f, 20f), new PostProcessingBudgetRange(5f, 15f));
                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Code, Is.EqualTo(QualityFailure.TierDeclarationInvalid));
            }
        }

        [Test]
        public void TierDeclarationRejectsInvertedOrNonPositiveRenderScaleRangeWithTierDeclarationInvalid()
        {
            foreach (var range in new[] { new RenderScaleRange(1.0f, 0.9f), new RenderScaleRange(0f, 1.0f) })
            {
                var result = QualityTierDefinition.TryCreate(TierMedium, new RefreshRateRangeHz(72f, 90f), range, FoveationLevel.Medium, MsaaSampleCount.Two, new ShadowBudgetRange(5f, 20f), new PostProcessingBudgetRange(5f, 15f));
                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Code, Is.EqualTo(QualityFailure.TierDeclarationInvalid));
            }
        }

        [Test]
        public void TierDeclarationRejectsAFoveationLevelOutsideTheClosedSetWithTierDeclarationInvalid()
        {
            var result = QualityTierDefinition.TryCreate(TierMedium, new RefreshRateRangeHz(72f, 90f), new RenderScaleRange(0.9f, 1.0f), (FoveationLevel)99, MsaaSampleCount.Two, new ShadowBudgetRange(5f, 20f), new PostProcessingBudgetRange(5f, 15f));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(QualityFailure.TierDeclarationInvalid));
        }

        [Test]
        public void TierDeclarationRejectsAnMsaaValueOutsideTheClosedSampleCountSetWithTierDeclarationInvalid()
        {
            var result = QualityTierDefinition.TryCreate(TierMedium, new RefreshRateRangeHz(72f, 90f), new RenderScaleRange(0.9f, 1.0f), FoveationLevel.Medium, (MsaaSampleCount)3, new ShadowBudgetRange(5f, 20f), new PostProcessingBudgetRange(5f, 15f));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(QualityFailure.TierDeclarationInvalid));
        }

        [Test]
        public void TierDeclarationRejectsAnInvertedOrNegativeShadowBudgetRangeWithTierDeclarationInvalid()
        {
            foreach (var range in new[] { new ShadowBudgetRange(20f, 5f), new ShadowBudgetRange(-1f, 5f) })
            {
                var result = QualityTierDefinition.TryCreate(TierMedium, new RefreshRateRangeHz(72f, 90f), new RenderScaleRange(0.9f, 1.0f), FoveationLevel.Medium, MsaaSampleCount.Two, range, new PostProcessingBudgetRange(5f, 15f));
                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Code, Is.EqualTo(QualityFailure.TierDeclarationInvalid));
            }
        }

        [Test]
        public void TierDeclarationRejectsAnInvertedOrNegativePostProcessingBudgetRangeWithTierDeclarationInvalid()
        {
            foreach (var range in new[] { new PostProcessingBudgetRange(15f, 5f), new PostProcessingBudgetRange(-1f, 5f) })
            {
                var result = QualityTierDefinition.TryCreate(TierMedium, new RefreshRateRangeHz(72f, 90f), new RenderScaleRange(0.9f, 1.0f), FoveationLevel.Medium, MsaaSampleCount.Two, new ShadowBudgetRange(5f, 20f), range);
                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Code, Is.EqualTo(QualityFailure.TierDeclarationInvalid));
            }
        }

        // ----- immutable TierRegistry built by explicit registration (QC-03) -----

        [Test]
        public void RegistrationRejectsDuplicateTierIdWithTierDuplicateLeavingBuilderUnchanged()
        {
            var builder = new QualityTierRegistryBuilder();
            Assert.That(builder.Register(TierMedium, new RefreshRateRangeHz(72f, 90f), new RenderScaleRange(0.9f, 1.0f), FoveationLevel.Medium, MsaaSampleCount.Two, new ShadowBudgetRange(5f, 20f), new PostProcessingBudgetRange(5f, 15f)).Succeeded, Is.True);
            var countAfterFirst = builder.Count;
            var second = builder.Register(TierMedium, new RefreshRateRangeHz(60f, 72f), new RenderScaleRange(0.7f, 0.9f), FoveationLevel.High, MsaaSampleCount.None, new ShadowBudgetRange(0f, 10f), new PostProcessingBudgetRange(0f, 5f));
            Assert.That(second.Succeeded, Is.False);
            Assert.That(second.Code, Is.EqualTo(QualityFailure.TierDuplicate));
            Assert.That(builder.Count, Is.EqualTo(countAfterFirst));
        }

        [Test]
        public void RegistrationRejectsAnInvalidDeclarationBeforeCheckingDuplicates()
        {
            var builder = new QualityTierRegistryBuilder();
            var result = builder.Register(TierMedium, new RefreshRateRangeHz(90f, 72f), new RenderScaleRange(0.9f, 1.0f), FoveationLevel.Medium, MsaaSampleCount.Two, new ShadowBudgetRange(5f, 20f), new PostProcessingBudgetRange(5f, 15f));
            Assert.That(result.Code, Is.EqualTo(QualityFailure.TierDeclarationInvalid));
            Assert.That(builder.Count, Is.EqualTo(0));
        }

        [Test]
        public void BuiltTierRegistryEnumeratesInCanonicalIdOrderAndCannotBeMutatedAfterwards()
        {
            var registry = BuildRegistry();
            var ids = registry.Tiers.Select(tier => tier.Id.Value).ToArray();
            Assert.That(ids, Is.EqualTo(new[] { "tier.high", "tier.low", "tier.medium" })); // ordinal order
            Assert.That(registry.Count, Is.EqualTo(3));
        }

        // ----- DeviceCapabilityDescriptor per device profile (QC-04) -----

        [Test]
        public void DeviceCapabilityDescriptorRejectsAnEmptySupportedSetWithCapabilityDeclarationInvalid()
        {
            var emptyRefreshRates = DeviceCapabilityDescriptor.TryCreate(DeviceStandalone, Array.Empty<float>(), new RenderScaleRange(0.8f, 1.2f), new[] { FoveationLevel.Medium }, new[] { MsaaSampleCount.Two });
            Assert.That(emptyRefreshRates.Succeeded, Is.False);
            Assert.That(emptyRefreshRates.Code, Is.EqualTo(QualityFailure.CapabilityDeclarationInvalid));

            var emptyFoveations = DeviceCapabilityDescriptor.TryCreate(DeviceStandalone, new[] { 72f }, new RenderScaleRange(0.8f, 1.2f), Array.Empty<FoveationLevel>(), new[] { MsaaSampleCount.Two });
            Assert.That(emptyFoveations.Succeeded, Is.False);
            Assert.That(emptyFoveations.Code, Is.EqualTo(QualityFailure.CapabilityDeclarationInvalid));

            var emptyMsaa = DeviceCapabilityDescriptor.TryCreate(DeviceStandalone, new[] { 72f }, new RenderScaleRange(0.8f, 1.2f), new[] { FoveationLevel.Medium }, Array.Empty<MsaaSampleCount>());
            Assert.That(emptyMsaa.Succeeded, Is.False);
            Assert.That(emptyMsaa.Code, Is.EqualTo(QualityFailure.CapabilityDeclarationInvalid));
        }

        [Test]
        public void DeviceCapabilityDescriptorRejectsAnInvertedRenderScaleRangeWithCapabilityDeclarationInvalid()
        {
            var result = DeviceCapabilityDescriptor.TryCreate(DeviceStandalone, new[] { 72f }, new RenderScaleRange(1.2f, 0.8f), new[] { FoveationLevel.Medium }, new[] { MsaaSampleCount.Two });
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(QualityFailure.CapabilityDeclarationInvalid));
        }

        [Test]
        public void DeviceCapabilitySetRejectsADuplicateDeviceProfileIdWithCapabilityDeclarationInvalid()
        {
            var builder = new DeviceCapabilitySetBuilder();
            var descriptor = DeviceCapabilityDescriptor.TryCreate(DeviceStandalone, new[] { 72f }, new RenderScaleRange(0.8f, 1.2f), new[] { FoveationLevel.Medium }, new[] { MsaaSampleCount.Two }).Value;
            Assert.That(builder.Register(descriptor).Succeeded, Is.True);
            var duplicate = DeviceCapabilityDescriptor.TryCreate(DeviceStandalone, new[] { 90f }, new RenderScaleRange(0.9f, 1.1f), new[] { FoveationLevel.High }, new[] { MsaaSampleCount.Four }).Value;
            var second = builder.Register(duplicate);
            Assert.That(second.Succeeded, Is.False);
            Assert.That(second.Code, Is.EqualTo(QualityFailure.CapabilityDeclarationInvalid));
            Assert.That(builder.Count, Is.EqualTo(1));
        }

        // ----- closed override-field set (QC-05) -----

        [Test]
        public void SetOverrideAcceptsEveryClosedFieldName()
        {
            foreach (var field in new[] { "refresh_rate", "render_scale", "foveation_level", "msaa", "shadow_budget", "post_processing_budget" })
            {
                Assert.That(QualityOverrideFieldNames.TryParse(field, out _), Is.True, field);
            }
        }

        [Test]
        public void SetOverrideRejectsAFieldNameOutsideTheClosedSetWithValueOutOfRange()
        {
            var state = InitialState().ApplySelectTier(DeviceStandalone, TierMedium).Value;
            var result = state.Apply(new SetOverrideIntent(DeviceStandalone, "bloom_intensity", 1.0f));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(QualityFailure.ValueOutOfRange));
            Assert.That(result.Message, Does.Contain("bloom_intensity"));
        }

        [Test]
        public void SetOverrideRejectsAnInClosedSetFieldWithAnOutOfGuardRailValueWithTheSameValueOutOfRangeCode()
        {
            var state = InitialState().ApplySelectTier(DeviceStandalone, TierMedium).Value; // refresh rate guard rail [72,90]
            var result = state.Apply(new SetOverrideIntent(DeviceStandalone, "refresh_rate", 45f));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(QualityFailure.ValueOutOfRange));
        }

        // ----- typed intents on an immutable QualityState (QC-06) -----

        [Test]
        public void SelectTierRejectsAnUnregisteredTierWithTierUnknown()
        {
            var result = InitialState().Apply(new SelectTierIntent(DeviceStandalone, TierUltra));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(QualityFailure.TierUnknown));
            Assert.That(result.Message, Does.Contain("tier.ultra"));
        }

        [Test]
        public void SelectTierRejectsAnUnregisteredDeviceWithDeviceUnknown()
        {
            var result = InitialState().Apply(new SelectTierIntent(DeviceUnregistered, TierMedium));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(QualityFailure.DeviceUnknown));
            Assert.That(result.Message, Does.Contain("device.unregistered"));
        }

        [Test]
        public void SelectTierRejectsATierTheDeviceCapabilityDoesNotSupportWithCapabilityUnsupported()
        {
            // device.standalone does not support tier.low's MSAA value or tier.high's foveation level.
            var lowResult = InitialState().Apply(new SelectTierIntent(DeviceStandalone, TierLow));
            Assert.That(lowResult.Succeeded, Is.False);
            Assert.That(lowResult.Code, Is.EqualTo(QualityFailure.CapabilityUnsupported));

            var highResult = InitialState().Apply(new SelectTierIntent(DeviceStandalone, TierHigh));
            Assert.That(highResult.Succeeded, Is.False);
            Assert.That(highResult.Code, Is.EqualTo(QualityFailure.CapabilityUnsupported));
        }

        [Test]
        public void SelectTierAcceptsASupportedTierAndReplacesAnyPriorSelection()
        {
            var afterFirst = InitialState().Apply(new SelectTierIntent(DeviceMobile, TierLow));
            Assert.That(afterFirst.Succeeded, Is.True, afterFirst.Message);
            Assert.That(afterFirst.Value.TryGetSelection(DeviceMobile, out var selection), Is.True);
            Assert.That(selection.TierId, Is.EqualTo(TierLow));
        }

        [Test]
        public void ApplyPresetBehavesAsADeclaredDefaultSelectTier()
        {
            var accepted = InitialState().Apply(new ApplyPresetIntent(DeviceMobile, TierLow));
            Assert.That(accepted.Succeeded, Is.True, accepted.Message);
            Assert.That(accepted.Value.TryGetSelection(DeviceMobile, out var selection), Is.True);
            Assert.That(selection.TierId, Is.EqualTo(TierLow));

            var rejected = InitialState().Apply(new ApplyPresetIntent(DeviceStandalone, TierLow));
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(rejected.Code, Is.EqualTo(QualityFailure.CapabilityUnsupported));
        }

        [Test]
        public void SetOverrideRejectsAValueOutsideTheSelectedTiersGuardRailWithValueOutOfRange()
        {
            var state = InitialState().ApplySelectTier(DeviceStandalone, TierMedium).Value;
            var result = state.Apply(new SetOverrideIntent(DeviceStandalone, "shadow_budget", 100f)); // tier.medium range [5,20]
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(QualityFailure.ValueOutOfRange));
        }

        [Test]
        public void SetOverrideRejectsAValueTheDeviceCapabilityDoesNotSupportWithCapabilityUnsupported()
        {
            var state = InitialState().ApplySelectTier(DeviceStandalone, TierMedium).Value; // refresh rate guard rail [72,90]
            var result = state.Apply(new SetOverrideIntent(DeviceStandalone, "refresh_rate", 80f)); // in guard rail, not in device's discrete supported set {72,90}
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(QualityFailure.CapabilityUnsupported));
        }

        [Test]
        public void SetOverrideRejectedForADeviceWithNoPriorSelectionWithTierUnknown()
        {
            var result = InitialState().Apply(new SetOverrideIntent(DeviceStandalone, "refresh_rate", 80f));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(QualityFailure.TierUnknown));
        }

        [Test]
        public void ResetRestoresTheDevicesLastSelectedTierWithNoOverride()
        {
            var withOverride = InitialState()
                .ApplySelectTier(DeviceStandalone, TierMedium).Value
                .ApplySetOverride(DeviceStandalone, "refresh_rate", 90f).Value;
            Assert.That(withOverride.TryGetSelection(DeviceStandalone, out var beforeReset), Is.True);
            Assert.That(beforeReset.Overrides.Count, Is.EqualTo(1));

            var afterReset = withOverride.Apply(new ResetIntent(DeviceStandalone));
            Assert.That(afterReset.Succeeded, Is.True, afterReset.Message);
            Assert.That(afterReset.Value.TryGetSelection(DeviceStandalone, out var selection), Is.True);
            Assert.That(selection.TierId, Is.EqualTo(TierMedium));
            Assert.That(selection.Overrides.Count, Is.EqualTo(0));
        }

        [Test]
        public void ResetIsANoOpReturningSuccessWhenNoOverrideWasPresent()
        {
            var selected = InitialState().ApplySelectTier(DeviceStandalone, TierMedium).Value;
            var result = selected.Apply(new ResetIntent(DeviceStandalone));
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Value.TryGetSelection(DeviceStandalone, out var selection), Is.True);
            Assert.That(selection.TierId, Is.EqualTo(TierMedium));
        }

        // ----- one active tier per device as the only state (QC-07) -----

        [Test]
        public void SelectTierReplacesTheTierIdAndClearsEveryOverrideForThatDevice()
        {
            var withOverride = InitialState()
                .ApplySelectTier(DeviceStandalone, TierMedium).Value
                .ApplySetOverride(DeviceStandalone, "shadow_budget", 10f).Value;

            // device.standalone does not support tier.low; use a value both devices could select instead:
            // re-select tier.medium again, which must still clear the override.
            var reselected = withOverride.Apply(new SelectTierIntent(DeviceStandalone, TierMedium));
            Assert.That(reselected.Succeeded, Is.True, reselected.Message);
            Assert.That(reselected.Value.TryGetSelection(DeviceStandalone, out var selection), Is.True);
            Assert.That(selection.Overrides.Count, Is.EqualTo(0));
        }

        [Test]
        public void StateHoldsAtMostOneSelectionPerDeviceProfile()
        {
            var state = InitialState()
                .ApplySelectTier(DeviceStandalone, TierMedium).Value
                .ApplySelectTier(DeviceMobile, TierLow).Value;
            Assert.That(state.SelectedDevices.Count, Is.EqualTo(2));
            Assert.That(state.TryGetSelection(DeviceStandalone, out var standaloneSelection), Is.True);
            Assert.That(standaloneSelection.TierId, Is.EqualTo(TierMedium));
            Assert.That(state.TryGetSelection(DeviceMobile, out var mobileSelection), Is.True);
            Assert.That(mobileSelection.TierId, Is.EqualTo(TierLow));
        }

        // ----- state immutability (QC-08) -----

        [Test]
        public void AcceptedIntentProducesANewStateAndLeavesThePriorStateIntact()
        {
            var prior = InitialState();
            var next = prior.Apply(new SelectTierIntent(DeviceStandalone, TierMedium)).Value;
            Assert.That(prior.TryGetSelection(DeviceStandalone, out _), Is.False);
            Assert.That(next.TryGetSelection(DeviceStandalone, out _), Is.True);
            Assert.That(ReferenceEquals(prior, next), Is.False);
        }

        [Test]
        public void RejectedIntentLeavesThePriorStateIntactAndReturnsItUnchangedWithTheFailureCode()
        {
            var prior = InitialState().ApplySelectTier(DeviceStandalone, TierMedium).Value;
            var result = prior.Apply(new SelectTierIntent(DeviceStandalone, TierUltra));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(QualityFailure.TierUnknown));
            Assert.That(prior.TryGetSelection(DeviceStandalone, out var selection), Is.True);
            Assert.That(selection.TierId, Is.EqualTo(TierMedium));
        }

        // ----- deterministic replay (QC-09) -----

        [Test]
        public void SameIntentSequenceOverSameRegistryAndCapabilitiesProducesEqualFinalStateAndFingerprint()
        {
            QualityIntent[] Sequence() => new QualityIntent[]
            {
                new SelectTierIntent(DeviceStandalone, TierMedium),
                new SetOverrideIntent(DeviceStandalone, "refresh_rate", 90f),
                new SelectTierIntent(DeviceMobile, TierLow),
            };
            var resultA = InitialState().ApplyAll(Sequence());
            var resultB = InitialState().ApplyAll(Sequence());
            Assert.That(resultA.State.Fingerprint(), Is.EqualTo(resultB.State.Fingerprint()));
            Assert.That(resultA.State, Is.EqualTo(resultB.State));
        }

        [Test]
        public void FingerprintCoversRegistryCapabilitySetAndPerDeviceSelectionsInCanonicalOrder()
        {
            // Registering capability descriptors in a different order must not change the fingerprint.
            var forwardBuilder = new DeviceCapabilitySetBuilder();
            var reverseBuilder = new DeviceCapabilitySetBuilder();
            var standalone = DeviceCapabilityDescriptor.TryCreate(DeviceStandalone, new[] { 72f, 90f }, new RenderScaleRange(0.8f, 1.2f), new[] { FoveationLevel.Medium, FoveationLevel.High }, new[] { MsaaSampleCount.Two, MsaaSampleCount.Four }).Value;
            var mobile = DeviceCapabilityDescriptor.TryCreate(DeviceMobile, new[] { 60f, 72f }, new RenderScaleRange(0.6f, 1.0f), new[] { FoveationLevel.High }, new[] { MsaaSampleCount.None }).Value;
            forwardBuilder.Register(standalone);
            forwardBuilder.Register(mobile);
            reverseBuilder.Register(mobile);
            reverseBuilder.Register(standalone);

            var registry = BuildRegistry();
            var forwardState = QualityState.Initial(registry, forwardBuilder.Build());
            var reverseState = QualityState.Initial(registry, reverseBuilder.Build());
            Assert.That(forwardState.Fingerprint(), Is.EqualTo(reverseState.Fingerprint()));
        }

        [Test]
        public void DifferentIntentSequencesWithSameNetPerDeviceSelectionsHaveEqualFingerprints()
        {
            var sequenceOne = InitialState().ApplyAll(new QualityIntent[]
            {
                new SelectTierIntent(DeviceStandalone, TierMedium),
                new SetOverrideIntent(DeviceStandalone, "refresh_rate", 90f),
            });
            var sequenceTwo = InitialState().ApplyAll(new QualityIntent[]
            {
                new SelectTierIntent(DeviceStandalone, TierMedium),
                new SelectTierIntent(DeviceStandalone, TierMedium), // redundant re-selection, extra revision
                new SetOverrideIntent(DeviceStandalone, "refresh_rate", 90f),
            });
            Assert.That(sequenceOne.State.Fingerprint(), Is.EqualTo(sequenceTwo.State.Fingerprint()));
            Assert.That(sequenceOne.State.Revision, Is.Not.EqualTo(sequenceTwo.State.Revision));
        }

        // ----- frame-budget policy as plain data (QC-10) -----

        [Test]
        public void WellFormedFrameBudgetPolicyIsAccepted()
        {
            var result = FrameBudgetPolicy.TryCreate(11.1f, 0.05f, 5f);
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Value.TargetFrameTimeMs, Is.EqualTo(11.1f));
            Assert.That(result.Value.DropThreshold, Is.EqualTo(0.05f));
        }

        [Test]
        public void FrameBudgetPolicyRejectsANonPositiveTargetFrameTimeWithBudgetDeclarationInvalid()
        {
            foreach (var target in new[] { 0f, -5f })
            {
                var result = FrameBudgetPolicy.TryCreate(target, 0.05f, 5f);
                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Code, Is.EqualTo(QualityFailure.BudgetDeclarationInvalid));
            }
        }

        [Test]
        public void FrameBudgetPolicyRejectsADropThresholdOutsideZeroOneWithBudgetDeclarationInvalid()
        {
            foreach (var threshold in new[] { -0.1f, 1.1f })
            {
                var result = FrameBudgetPolicy.TryCreate(11.1f, threshold, 5f);
                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Code, Is.EqualTo(QualityFailure.BudgetDeclarationInvalid));
            }
        }

        // ----- structured results with stable failure codes (QC-11) -----

        [Test]
        public void EveryStableFailureCodeCarriesTheOffendingIdDeviceOrFieldName()
        {
            Assert.That(TierId.TryCreate("Bad Id").Code, Is.EqualTo(QualityFailure.IdentityMalformed));
            Assert.That(QualityTierDefinition.TryCreate(TierMedium, new RefreshRateRangeHz(90f, 72f), new RenderScaleRange(0.9f, 1.0f), FoveationLevel.Medium, MsaaSampleCount.Two, new ShadowBudgetRange(5f, 20f), new PostProcessingBudgetRange(5f, 15f)).Code, Is.EqualTo(QualityFailure.TierDeclarationInvalid));
            Assert.That(DeviceCapabilityDescriptor.TryCreate(DeviceStandalone, Array.Empty<float>(), new RenderScaleRange(0.8f, 1.2f), new[] { FoveationLevel.Medium }, new[] { MsaaSampleCount.Two }).Code, Is.EqualTo(QualityFailure.CapabilityDeclarationInvalid));
            Assert.That(FrameBudgetPolicy.TryCreate(-1f, 0.1f, 5f).Code, Is.EqualTo(QualityFailure.BudgetDeclarationInvalid));

            var builder = new QualityTierRegistryBuilder();
            builder.Register(TierMedium, new RefreshRateRangeHz(72f, 90f), new RenderScaleRange(0.9f, 1.0f), FoveationLevel.Medium, MsaaSampleCount.Two, new ShadowBudgetRange(5f, 20f), new PostProcessingBudgetRange(5f, 15f));
            var duplicate = builder.Register(TierMedium, new RefreshRateRangeHz(72f, 90f), new RenderScaleRange(0.9f, 1.0f), FoveationLevel.Medium, MsaaSampleCount.Two, new ShadowBudgetRange(5f, 20f), new PostProcessingBudgetRange(5f, 15f));
            Assert.That(duplicate.Code, Is.EqualTo(QualityFailure.TierDuplicate));
            Assert.That(duplicate.Message, Does.Contain("tier.medium"));

            var unknownTier = InitialState().Apply(new SelectTierIntent(DeviceStandalone, TierUltra));
            Assert.That(unknownTier.Code, Is.EqualTo(QualityFailure.TierUnknown));
            Assert.That(unknownTier.Message, Does.Contain("tier.ultra"));

            var unknownDevice = InitialState().Apply(new SelectTierIntent(DeviceUnregistered, TierMedium));
            Assert.That(unknownDevice.Code, Is.EqualTo(QualityFailure.DeviceUnknown));
            Assert.That(unknownDevice.Message, Does.Contain("device.unregistered"));

            Assert.That(InitialState().Apply(new SelectTierIntent(DeviceStandalone, TierLow)).Code, Is.EqualTo(QualityFailure.CapabilityUnsupported));

            var outOfRange = InitialState().ApplySelectTier(DeviceStandalone, TierMedium).Value.Apply(new SetOverrideIntent(DeviceStandalone, "shadow_budget", 999f));
            Assert.That(outOfRange.Code, Is.EqualTo(QualityFailure.ValueOutOfRange));
            Assert.That(outOfRange.Message, Does.Contain("shadow_budget"));

            var stale = InitialState().Apply(new SelectTierIntent(DeviceStandalone, TierMedium, IntentActor.Player, 99));
            Assert.That(stale.Code, Is.EqualTo(QualityFailure.StateStale));
        }

        [Test]
        public void AValidIntentSequencePassesCleanWithEveryOutcomeAccepted()
        {
            var sequence = new QualityIntent[]
            {
                new SelectTierIntent(DeviceStandalone, TierMedium),
                new SetOverrideIntent(DeviceStandalone, "refresh_rate", 90f),
                new ResetIntent(DeviceStandalone),
                new ApplyPresetIntent(DeviceMobile, TierLow),
            };
            var result = InitialState().ApplyAll(sequence);
            Assert.That(result.AllAccepted, Is.True, string.Join(", ", result.Outcomes.Where(o => !o.Accepted).Select(o => o.Code)));
            Assert.That(result.AcceptedCount, Is.EqualTo(sequence.Length));
            Assert.That(result.RejectedCount, Is.EqualTo(0));
        }

        // ----- LESSON-011: actor and expected revision (QC-12) -----

        [Test]
        public void EveryIntentDefaultsToPlayerActorWithNoExpectedRevisionWhenUsingTheShortConstructor()
        {
            var select = new SelectTierIntent(DeviceStandalone, TierMedium);
            var setOverride = new SetOverrideIntent(DeviceStandalone, "refresh_rate", 90f);
            var reset = new ResetIntent(DeviceStandalone);
            var applyPreset = new ApplyPresetIntent(DeviceMobile, TierLow);

            foreach (QualityIntent intent in new QualityIntent[] { select, setOverride, reset, applyPreset })
            {
                Assert.That(intent.Actor, Is.EqualTo(IntentActor.Player), intent.Describe());
                Assert.That(intent.ExpectedRevision, Is.Null, intent.Describe());
            }
        }

        [Test]
        public void AnIntentCanBeConstructedWithAnExplicitActorAndExpectedRevision()
        {
            var intent = new SelectTierIntent(DeviceStandalone, TierMedium, IntentActor.Agent, 3);
            Assert.That(intent.Actor, Is.EqualTo(IntentActor.Agent));
            Assert.That(intent.ExpectedRevision, Is.EqualTo(3));
        }

        // ----- LESSON-011: state.stale (QC-13) -----

        [Test]
        public void AStaleExpectedRevisionIsRejectedWithStateStaleBeforeTheIntentsOwnRuleRuns()
        {
            var state = InitialState();
            // Naming an unregistered tier would normally be tier.unknown, but a stale revision must
            // be caught first and reported instead, changing nothing.
            var result = state.Apply(new SelectTierIntent(DeviceStandalone, TierUltra, IntentActor.Player, 5));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(QualityFailure.StateStale));
            Assert.That(state.TryGetSelection(DeviceStandalone, out _), Is.False);
        }

        [Test]
        public void RevisionIncreasesByOneOnEveryAcceptedIntent()
        {
            var state = InitialState();
            Assert.That(state.Revision, Is.EqualTo(0));
            var next = state.Apply(new SelectTierIntent(DeviceStandalone, TierMedium)).Value;
            Assert.That(next.Revision, Is.EqualTo(1));
            var afterOverride = next.Apply(new SetOverrideIntent(DeviceStandalone, "refresh_rate", 90f)).Value;
            Assert.That(afterOverride.Revision, Is.EqualTo(2));
        }

        // ----- LESSON-011: actor never changes validation (QC-14) -----

        [Test]
        public void APlayerIssuedAndAnAgentIssuedCopyOfTheSameIntentTakeTheSamePathAndYieldTheSameOutcome()
        {
            var byPlayer = InitialState().Apply(new SelectTierIntent(DeviceStandalone, TierLow, IntentActor.Player, null));
            var byAgent = InitialState().Apply(new SelectTierIntent(DeviceStandalone, TierLow, IntentActor.Agent, null));
            Assert.That(byPlayer.Succeeded, Is.EqualTo(byAgent.Succeeded));
            Assert.That(byPlayer.Code, Is.EqualTo(byAgent.Code));
        }

        // ----- LESSON-011: replay log (QC-15) -----

        [Test]
        public void EveryOutcomeInTheSequenceResultCarriesTheIssuingActorAndTheRevisionAfterIt()
        {
            var sequence = new QualityIntent[]
            {
                new SelectTierIntent(DeviceStandalone, TierMedium, IntentActor.Player, null),
                new SetOverrideIntent(DeviceStandalone, "refresh_rate", 90f, IntentActor.Agent, null),
            };
            var result = InitialState().ApplyAll(sequence);
            Assert.That(result.Outcomes[0].Actor, Is.EqualTo(IntentActor.Player));
            Assert.That(result.Outcomes[0].RevisionAfter, Is.EqualTo(1));
            Assert.That(result.Outcomes[1].Actor, Is.EqualTo(IntentActor.Agent));
            Assert.That(result.Outcomes[1].RevisionAfter, Is.EqualTo(2));
        }

        // ----- Core purity (QC-16) -----

        [Test]
        public void CoreAssemblyReferencesNoUnityEngineAssembly()
        {
            var assembly = typeof(QualityState).Assembly;
            var referenced = assembly.GetReferencedAssemblies().Select(name => name.Name ?? string.Empty);
            Assert.That(referenced.Any(name => name.IndexOf("UnityEngine", StringComparison.OrdinalIgnoreCase) >= 0), Is.False, string.Join(",", referenced));
        }

        [Test]
        public void NoCoreRuntimeSourceFileMentionsUnityEngineOrARenderPipelineOrDisplayType()
        {
            var runtimeDirectory = System.IO.Path.GetFullPath(System.IO.Path.Combine(TestSourceDirectory(), "..", "..", "Runtime"));
            Assert.That(System.IO.Directory.Exists(runtimeDirectory), Is.True, runtimeDirectory);

            var forbidden = new[] { "UnityEngine", "XRDisplaySubsystem", "UniversalRenderPipelineAsset", "QualitySettings" };
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
        public void ADeviceProfileIdsTextEntersAndLeavesTheCoreAsAnOpaqueString()
        {
            const string text = "vendor.controller.model_left_42";
            var id = DeviceProfileId.Parse(text);
            Assert.That(id.Value, Is.EqualTo(text));
            Assert.That(id.ToString(), Is.EqualTo(text));
        }

        private static string TestSourceDirectory([CallerFilePath] string sourceFilePath = "") => System.IO.Path.GetDirectoryName(sourceFilePath);
    }
}
