using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Lingkyn.PlatformServices.Core;

namespace Lingkyn.PlatformServices.Core.Editor.Tests
{
    // Authored and unexecuted: this assembly has never compiled or run. It is checked against
    // docs/standards/platform-services/verification-contract.md and coverage-map.json. PC-16..PC-19
    // are the LESSON-011 addition (actor, expected revision, state.stale, actor-never-changes-
    // validation, the replay log) that every live family answers, the same shape staging/haptics and
    // staging/live-tuning already carry.
    public sealed class PlatformServicesCoreContractTests
    {
        private static readonly ProviderId FullProviderId = ProviderId.Parse("meta_horizon_platform");
        private static readonly ProviderId LeaderboardOnlyProviderId = ProviderId.Parse("leaderboard_only");
        private static readonly AccountId Player = AccountId.Parse("player.one");

        private static ProviderRegistry BuildRegistry()
        {
            var builder = new ProviderRegistryBuilder();
            builder.Register(FullProviderId, new[]
            {
                PlatformServiceCapability.Entitlement,
                PlatformServiceCapability.Achievement,
                PlatformServiceCapability.Leaderboard,
                PlatformServiceCapability.CloudSave,
                PlatformServiceCapability.Identity,
            }, 1_000_000);
            builder.Register(LeaderboardOnlyProviderId, new[] { PlatformServiceCapability.Leaderboard });
            return builder.Build();
        }

        private static PlatformServicesState InitialState()
        {
            var registry = BuildRegistry();
            var composed = PlatformServicesState.Initial(registry, FullProviderId, new[]
            {
                PlatformServiceCapability.Entitlement,
                PlatformServiceCapability.Achievement,
                PlatformServiceCapability.Leaderboard,
                PlatformServiceCapability.CloudSave,
            });
            Assert.That(composed.Succeeded, Is.True, composed.Message);
            return composed.Value;
        }

        private static PlatformServicesState EntitledState()
        {
            var result = InitialState().Apply(new CheckEntitlementIntent(Player, true));
            Assert.That(result.Succeeded, Is.True, result.Message);
            return result.Value;
        }

        // ----- identity (PC-01) -----

        [Test]
        public void AccountIdCanonicalizesLowerCaseDottedSegments()
        {
            var id = AccountId.TryCreate("  player.one  ");
            Assert.That(id.Succeeded, Is.True, id.Message);
            Assert.That(id.Value.Value, Is.EqualTo("player.one"));
            Assert.That(id.Value.ToString(), Is.EqualTo("player.one"));
        }

        [Test]
        public void AccountIdRejectsMalformedTextWithIdentityMalformed()
        {
            foreach (var text in new[] { null, "", " ", "Player.One", "player one", "player..one", ".player", "player.", "player-one" })
            {
                var result = AccountId.TryCreate(text);
                Assert.That(result.Succeeded, Is.False, text ?? "<null>");
                Assert.That(result.Code, Is.EqualTo(PlatformServicesFailure.IdentityMalformed), text ?? "<null>");
            }
        }

        [Test]
        public void IdentitiesWithSameCanonicalTextAreEqualValues()
        {
            var first = AccountId.Parse("player.one");
            var second = AccountId.Parse("  player.one ");
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first == second, Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(first != AccountId.Parse("player.two"), Is.True);
        }

        [Test]
        public void ProviderIdSharesCanonicalFormButNeverEqualsAnAccountIdOfTheSameText()
        {
            var malformed = ProviderId.TryCreate("Not A Provider");
            Assert.That(malformed.Succeeded, Is.False);
            Assert.That(malformed.Code, Is.EqualTo(PlatformServicesFailure.IdentityMalformed));

            var providerId = ProviderId.Parse("player.one");
            var accountId = AccountId.Parse("player.one");
            Assert.That(providerId.Value, Is.EqualTo(accountId.Value));
            Assert.That(providerId.GetType(), Is.Not.EqualTo(accountId.GetType()));
        }

        [Test]
        public void IdentityParseThrowsWithTheFailureMessageForMalformedIdentity()
        {
            var thrown = Assert.Throws<ArgumentException>(() => AccountId.Parse("Bad Id"));
            Assert.That(thrown.Message, Does.Contain("lower-case"));
            Assert.Throws<ArgumentException>(() => ProviderId.Parse(""));
        }

        // ----- closed capability set (PC-02) -----

        [Test]
        public void TryParseAcceptsExactlyTheFiveCanonicalCapabilityNames()
        {
            var expected = new Dictionary<string, PlatformServiceCapability>
            {
                ["entitlement"] = PlatformServiceCapability.Entitlement,
                ["achievement"] = PlatformServiceCapability.Achievement,
                ["leaderboard"] = PlatformServiceCapability.Leaderboard,
                ["cloud_save"] = PlatformServiceCapability.CloudSave,
                ["identity"] = PlatformServiceCapability.Identity,
            };
            foreach (var pair in expected)
            {
                var result = PlatformServiceCapabilities.TryParse(pair.Key);
                Assert.That(result.Succeeded, Is.True, pair.Key);
                Assert.That(result.Value, Is.EqualTo(pair.Value));
                Assert.That(PlatformServiceCapabilities.Name(pair.Value), Is.EqualTo(pair.Key));
            }
        }

        [Test]
        public void TryParseRejectsANameOutsideTheClosedSetWithCapabilityUnknown()
        {
            foreach (var text in new[] { null, "", "Entitlement", "purchase", "cloudsave" })
            {
                var result = PlatformServiceCapabilities.TryParse(text);
                Assert.That(result.Succeeded, Is.False, text ?? "<null>");
                Assert.That(result.Code, Is.EqualTo(PlatformServicesFailure.CapabilityUnknown), text ?? "<null>");
            }
        }

        // ----- ProviderDescriptor explicit declaration (PC-03) -----

        [Test]
        public void TryCreateRejectsAnEmptyCapabilitySubsetWithProviderDeclarationInvalid()
        {
            var result = ProviderDescriptor.TryCreate(FullProviderId, Array.Empty<PlatformServiceCapability>(), null);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(PlatformServicesFailure.ProviderDeclarationInvalid));
        }

        [Test]
        public void TryCreateRejectsACloudSaveGuardRailThatIsZeroOrNegativeWithProviderDeclarationInvalid()
        {
            var zero = ProviderDescriptor.TryCreate(FullProviderId, new[] { PlatformServiceCapability.CloudSave }, 0);
            Assert.That(zero.Succeeded, Is.False);
            Assert.That(zero.Code, Is.EqualTo(PlatformServicesFailure.ProviderDeclarationInvalid));

            var negative = ProviderDescriptor.TryCreate(FullProviderId, new[] { PlatformServiceCapability.CloudSave }, -5);
            Assert.That(negative.Succeeded, Is.False);
            Assert.That(negative.Code, Is.EqualTo(PlatformServicesFailure.ProviderDeclarationInvalid));
        }

        [Test]
        public void TryCreateRejectsCloudSaveDeclaredWithNoGuardRailWithProviderDeclarationInvalid()
        {
            var result = ProviderDescriptor.TryCreate(FullProviderId, new[] { PlatformServiceCapability.CloudSave }, null);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(PlatformServicesFailure.ProviderDeclarationInvalid));
        }

        [Test]
        public void TryCreateRejectsAGuardRailSuppliedWithoutCloudSaveDeclaredWithProviderDeclarationInvalid()
        {
            var result = ProviderDescriptor.TryCreate(FullProviderId, new[] { PlatformServiceCapability.Achievement }, 100);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(PlatformServicesFailure.ProviderDeclarationInvalid));
        }

        [Test]
        public void TryCreateAcceptsAWellFormedDeclaration()
        {
            var result = ProviderDescriptor.TryCreate(FullProviderId, new[] { PlatformServiceCapability.CloudSave, PlatformServiceCapability.Identity }, 2048);
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Value.Supports(PlatformServiceCapability.CloudSave), Is.True);
            Assert.That(result.Value.Supports(PlatformServiceCapability.Achievement), Is.False);
            Assert.That(result.Value.CloudSaveGuardRailBytes, Is.EqualTo(2048));
        }

        // ----- ProviderRegistry explicit registration (PC-04) -----

        [Test]
        public void RegistrationRejectsADuplicateProviderIdWithProviderDuplicateLeavingBuilderUnchanged()
        {
            var builder = new ProviderRegistryBuilder();
            var first = builder.Register(FullProviderId, new[] { PlatformServiceCapability.Achievement });
            Assert.That(first.Succeeded, Is.True, first.Message);

            var second = builder.Register(FullProviderId, new[] { PlatformServiceCapability.Leaderboard });
            Assert.That(second.Succeeded, Is.False);
            Assert.That(second.Code, Is.EqualTo(PlatformServicesFailure.ProviderDuplicate));
            Assert.That(builder.Count, Is.EqualTo(1));
        }

        [Test]
        public void RegistrationRejectsAnInvalidDeclarationBeforeCheckingDuplicates()
        {
            var builder = new ProviderRegistryBuilder();
            var result = builder.Register(FullProviderId, Array.Empty<PlatformServiceCapability>());
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(PlatformServicesFailure.ProviderDeclarationInvalid));
            Assert.That(builder.Count, Is.EqualTo(0));
        }

        [Test]
        public void BuiltRegistryEnumeratesInCanonicalIdOrderAndCannotBeMutatedAfterwards()
        {
            var builder = new ProviderRegistryBuilder();
            builder.Register(ProviderId.Parse("unity_gaming_services"), new[] { PlatformServiceCapability.CloudSave }, 4096);
            builder.Register(FullProviderId, new[] { PlatformServiceCapability.Achievement });
            var registry = builder.Build();

            var ids = registry.Providers.Select(descriptor => descriptor.Id.Value).ToList();
            Assert.That(ids, Is.EqualTo(new[] { "meta_horizon_platform", "unity_gaming_services" }));

            builder.Register(LeaderboardOnlyProviderId, new[] { PlatformServiceCapability.Leaderboard });
            Assert.That(registry.Count, Is.EqualTo(2), "a registry already built must not see a later registration");
        }

        // ----- composition-time fail-closed capability check (PC-05) -----

        [Test]
        public void ComposingWithAnUnregisteredProviderIsRejectedWithCapabilityUnsupported()
        {
            var registry = BuildRegistry();
            var result = PlatformServicesState.Initial(registry, ProviderId.Parse("unknown_provider"), Array.Empty<PlatformServiceCapability>());
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(PlatformServicesFailure.CapabilityUnsupported));
            Assert.That(result.Message, Does.Contain("unknown_provider"));
        }

        [Test]
        public void ComposingWithAnUndeclaredRequiredCapabilityIsRejectedNamingTheCapabilityAndProvider()
        {
            var registry = BuildRegistry();
            var result = PlatformServicesState.Initial(registry, LeaderboardOnlyProviderId, new[] { PlatformServiceCapability.Entitlement });
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(PlatformServicesFailure.CapabilityUnsupported));
            Assert.That(result.Message, Does.Contain("entitlement"));
            Assert.That(result.Message, Does.Contain("leaderboard_only"));
        }

        [Test]
        public void ComposingWithEveryRequiredCapabilityDeclaredSucceeds()
        {
            var registry = BuildRegistry();
            var result = PlatformServicesState.Initial(registry, FullProviderId, new[] { PlatformServiceCapability.Entitlement, PlatformServiceCapability.CloudSave });
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Value.Revision, Is.EqualTo(0));
        }

        // ----- intent-time capability check (PC-06) -----

        [Test]
        public void EveryIntentNamesExactlyOneClosedSetCapability()
        {
            Assert.That(new CheckEntitlementIntent(Player, true).Capability, Is.EqualTo(PlatformServiceCapability.Entitlement));
            Assert.That(new UnlockIntent("a", "k1").Capability, Is.EqualTo(PlatformServiceCapability.Achievement));
            Assert.That(new ReportScoreIntent("lb", 10, LeaderboardUploadPolicy.KeepBest, "k2").Capability, Is.EqualTo(PlatformServiceCapability.Leaderboard));
            Assert.That(new ReadLeaderboardIntent("lb", LeaderboardRange.Top).Capability, Is.EqualTo(PlatformServiceCapability.Leaderboard));
            Assert.That(new CloudWriteIntent("k", new byte[] { 1 }, "k3").Capability, Is.EqualTo(PlatformServiceCapability.CloudSave));
            Assert.That(new CloudReadIntent("k").Capability, Is.EqualTo(PlatformServiceCapability.CloudSave));
        }

        [Test]
        public void AnIntentNamingACapabilityTheActiveProviderDoesNotDeclareIsRejectedWithCapabilityUnsupported()
        {
            var registry = BuildRegistry();
            var composed = PlatformServicesState.Initial(registry, LeaderboardOnlyProviderId, new[] { PlatformServiceCapability.Leaderboard });
            Assert.That(composed.Succeeded, Is.True, composed.Message);

            var result = composed.Value.Apply(new UnlockIntent("grab.master", "key-1"));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(PlatformServicesFailure.CapabilityUnsupported));
            Assert.That(result.Message, Does.Contain("achievement"));
        }

        // ----- identity precondition (PC-07) -----

        [Test]
        public void EveryIntentOtherThanCheckEntitlementIssuedBeforeIdentityIsResolvedIsRejectedWithIdentityMissing()
        {
            var state = InitialState();
            PlatformServicesIntent[] intents =
            {
                new UnlockIntent("grab.master", "key-1"),
                new ReportScoreIntent("lb", 10, LeaderboardUploadPolicy.KeepBest, "key-2"),
                new ReadLeaderboardIntent("lb", LeaderboardRange.Top),
                new CloudWriteIntent("slot", new byte[] { 1 }, "key-3"),
                new CloudReadIntent("slot"),
            };
            foreach (var intent in intents)
            {
                var result = state.Apply(intent);
                Assert.That(result.Succeeded, Is.False, intent.Describe());
                Assert.That(result.Code, Is.EqualTo(PlatformServicesFailure.IdentityMissing), intent.Describe());
            }
        }

        [Test]
        public void CheckEntitlementItselfNeedsNoPriorIdentity()
        {
            var result = InitialState().Apply(new CheckEntitlementIntent(Player, true));
            Assert.That(result.Succeeded, Is.True, result.Message);
        }

        // ----- the three no-fold intents: check_entitlement, read_leaderboard, cloud_read (PC-08) -----

        [Test]
        public void CheckEntitlementAcceptedResolvesTheAccountIdForTheActiveProvider()
        {
            var state = EntitledState();
            Assert.That(state.TryGetResolvedAccountId(FullProviderId, out var accountId), Is.True);
            Assert.That(accountId, Is.EqualTo(Player));
        }

        [Test]
        public void CheckEntitlementNotEntitledIsRejectedWithNotEntitledAndResolvesNoIdentity()
        {
            var state = InitialState();
            var result = state.Apply(new CheckEntitlementIntent(Player, false));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(PlatformServicesFailure.NotEntitled));
            Assert.That(state.TryGetResolvedAccountId(FullProviderId, out _), Is.False);
        }

        [Test]
        public void CheckEntitlementOnAProviderThatDoesNotDeclareEntitlementIsRejectedWithCapabilityUnsupported()
        {
            var registry = BuildRegistry();
            var composed = PlatformServicesState.Initial(registry, LeaderboardOnlyProviderId, Array.Empty<PlatformServiceCapability>());
            var result = composed.Value.Apply(new CheckEntitlementIntent(Player, true));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(PlatformServicesFailure.CapabilityUnsupported));
        }

        [Test]
        public void ReadLeaderboardAndCloudReadSucceedOnceIdentityAndCapabilityHoldWithNoProjectionChange()
        {
            var state = EntitledState();
            var readLeaderboard = state.Apply(new ReadLeaderboardIntent("lb", LeaderboardRange.Top));
            Assert.That(readLeaderboard.Succeeded, Is.True, readLeaderboard.Message);
            Assert.That(readLeaderboard.Code, Is.Empty);

            var readCloud = state.Apply(new CloudReadIntent("slot"));
            Assert.That(readCloud.Succeeded, Is.True, readCloud.Message);
            Assert.That(readCloud.Code, Is.Empty);
        }

        // ----- explicit outcome folding (PC-09) -----

        [Test]
        public void UnlockWithAnAcceptedOutcomeUpdatesTheUnlockedAchievementSet()
        {
            var state = EntitledState();
            var result = state.Apply(new UnlockIntent("grab.master", "key-1", ProviderOutcome.Accepted));
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Code, Is.Empty);
            Assert.That(result.Value.IsAchievementUnlocked("grab.master"), Is.True);
        }

        [Test]
        public void UnlockWithANotEntitledOrRateLimitedOutcomeUpdatesNoProjectionButReturnsTheCode()
        {
            var state = EntitledState();
            var notEntitled = state.Apply(new UnlockIntent("grab.master", "key-1", ProviderOutcome.NotEntitled));
            Assert.That(notEntitled.Succeeded, Is.True, notEntitled.Message);
            Assert.That(notEntitled.Code, Is.EqualTo(PlatformServicesFailure.NotEntitled));
            Assert.That(notEntitled.Value.IsAchievementUnlocked("grab.master"), Is.False);

            var rateLimited = state.Apply(new UnlockIntent("grab.master", "key-2", ProviderOutcome.RateLimited));
            Assert.That(rateLimited.Succeeded, Is.True, rateLimited.Message);
            Assert.That(rateLimited.Code, Is.EqualTo(PlatformServicesFailure.RateLimited));
            Assert.That(rateLimited.Value.IsAchievementUnlocked("grab.master"), Is.False);
        }

        [Test]
        public void ReportScoreKeepBestOnlyOverwritesTheLastAcceptedScoreWhenTheNewScoreIsBetter()
        {
            var state = EntitledState();
            state = state.Apply(new ReportScoreIntent("weekly", 100, LeaderboardUploadPolicy.KeepBest, "key-1", ProviderOutcome.Accepted)).Value;
            var worse = state.Apply(new ReportScoreIntent("weekly", 50, LeaderboardUploadPolicy.KeepBest, "key-2", ProviderOutcome.Accepted));
            Assert.That(worse.Succeeded, Is.True, worse.Message);
            Assert.That(worse.Value.TryGetLastAcceptedScore("weekly", out var stillBest), Is.True);
            Assert.That(stillBest, Is.EqualTo(100));

            var better = worse.Value.Apply(new ReportScoreIntent("weekly", 150, LeaderboardUploadPolicy.KeepBest, "key-3", ProviderOutcome.Accepted));
            Assert.That(better.Value.TryGetLastAcceptedScore("weekly", out var newBest), Is.True);
            Assert.That(newBest, Is.EqualTo(150));
        }

        [Test]
        public void ReportScoreAlwaysOverwritesUnconditionallyRegardlessOfWhetherTheNewScoreIsBetter()
        {
            var state = EntitledState();
            state = state.Apply(new ReportScoreIntent("latest", 100, LeaderboardUploadPolicy.Always, "key-1", ProviderOutcome.Accepted)).Value;
            var result = state.Apply(new ReportScoreIntent("latest", 10, LeaderboardUploadPolicy.Always, "key-2", ProviderOutcome.Accepted));
            Assert.That(result.Value.TryGetLastAcceptedScore("latest", out var score), Is.True);
            Assert.That(score, Is.EqualTo(10));
        }

        [Test]
        public void CloudWriteWithAnAcceptedOutcomeStoresThePayloadExactly()
        {
            var state = EntitledState();
            var payload = new byte[] { 1, 2, 3, 4 };
            var result = state.Apply(new CloudWriteIntent("slot", payload, "key-1", ProviderOutcome.Accepted));
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Value.TryGetCloudPayload("slot", out var stored), Is.True);
            Assert.That(stored, Is.EqualTo(payload));
        }

        // ----- the offline pending-write queue (PC-10) -----

        [Test]
        public void AWriteIssuedWithNoOutcomeEnqueuesAndReturnsOfflineWithTheIdempotencyKey()
        {
            var state = EntitledState();
            var result = state.Apply(new UnlockIntent("grab.master", "key-1"));
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Code, Is.EqualTo(PlatformServicesFailure.Offline));
            Assert.That(result.Value.PendingWrites.Count, Is.EqualTo(1));
            Assert.That(result.Value.PendingWrites[0].IdempotencyKey, Is.EqualTo("key-1"));
            Assert.That(result.Value.IsAchievementUnlocked("grab.master"), Is.False);
        }

        [Test]
        public void ResubmittingTheSameIntentTypeAndKeyWithAnOutcomeResolvesAndRemovesExactlyThatQueuedEntry()
        {
            var state = EntitledState();
            state = state.Apply(new UnlockIntent("grab.master", "key-1")).Value;
            Assert.That(state.PendingWrites.Count, Is.EqualTo(1));

            var resolved = state.Apply(new UnlockIntent("grab.master", "key-1", ProviderOutcome.Accepted));
            Assert.That(resolved.Succeeded, Is.True, resolved.Message);
            Assert.That(resolved.Code, Is.Empty);
            Assert.That(resolved.Value.PendingWrites, Is.Empty);
            Assert.That(resolved.Value.IsAchievementUnlocked("grab.master"), Is.True);
        }

        [Test]
        public void TwoQueuedWritesResolveInFifoOrderRelativeToEachOther()
        {
            var state = EntitledState();
            state = state.Apply(new UnlockIntent("first.badge", "key-1")).Value;
            state = state.Apply(new UnlockIntent("second.badge", "key-2")).Value;
            Assert.That(state.PendingWrites.Select(entry => entry.IdempotencyKey), Is.EqualTo(new[] { "key-1", "key-2" }));

            state = state.Apply(new UnlockIntent("first.badge", "key-1", ProviderOutcome.Accepted)).Value;
            Assert.That(state.PendingWrites.Select(entry => entry.IdempotencyKey), Is.EqualTo(new[] { "key-2" }), "resolving the first queued entry must not disturb the second");
            Assert.That(state.IsAchievementUnlocked("first.badge"), Is.True);
            Assert.That(state.IsAchievementUnlocked("second.badge"), Is.False);

            state = state.Apply(new UnlockIntent("second.badge", "key-2", ProviderOutcome.Accepted)).Value;
            Assert.That(state.PendingWrites, Is.Empty);
            Assert.That(state.IsAchievementUnlocked("second.badge"), Is.True);
        }

        // ----- idempotency (PC-11) -----

        [Test]
        public void ResubmittingAResolvedKeyWithTheSameOutcomeReturnsTheOriginalResultWithoutReapplying()
        {
            var state = EntitledState();
            var first = state.Apply(new UnlockIntent("grab.master", "key-1", ProviderOutcome.Accepted));
            Assert.That(first.Succeeded, Is.True, first.Message);
            var resolvedRevision = first.Value.Revision;

            var second = first.Value.Apply(new UnlockIntent("grab.master", "key-1", ProviderOutcome.Accepted));
            Assert.That(second.Succeeded, Is.True, second.Message);
            Assert.That(second.Code, Is.Empty);
            Assert.That(second.Value.Revision, Is.EqualTo(resolvedRevision), "an idempotent replay hit must not reapply or advance the revision");
        }

        [Test]
        public void ResubmittingAResolvedKeyWithADifferentOutcomeIsRejectedWithDispatchMismatch()
        {
            var state = EntitledState();
            var first = state.Apply(new UnlockIntent("grab.master", "key-1", ProviderOutcome.Accepted)).Value;
            var mismatched = first.Apply(new UnlockIntent("grab.master", "key-1", ProviderOutcome.RateLimited));
            Assert.That(mismatched.Succeeded, Is.False);
            Assert.That(mismatched.Code, Is.EqualTo(PlatformServicesFailure.DispatchMismatch));
        }

        // ----- state immutability (PC-12) -----

        [Test]
        public void AcceptedIntentProducesNewStateWhilePriorStateStaysReadableAndUnchanged()
        {
            var state = EntitledState();
            var result = state.Apply(new UnlockIntent("grab.master", "key-1", ProviderOutcome.Accepted));
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(state.IsAchievementUnlocked("grab.master"), Is.False, "the prior state must stay intact");
        }

        [Test]
        public void RejectedIntentLeavesPriorStateIntactWithTheFailureCode()
        {
            var state = InitialState();
            var result = state.Apply(new UnlockIntent("grab.master", "key-1"));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(PlatformServicesFailure.IdentityMissing));
            Assert.That(state.Revision, Is.EqualTo(0));
        }

        // ----- deterministic replay (PC-13) -----

        [Test]
        public void SameIntentSequenceOverTheSameRegistryAndInitialStateProducesEqualStateAndFingerprint()
        {
            var registry = BuildRegistry();
            PlatformServicesSequenceResult Replay()
            {
                var composed = PlatformServicesState.Initial(registry, FullProviderId, new[] { PlatformServiceCapability.Entitlement, PlatformServiceCapability.Achievement }).Value;
                return composed.ApplyAll(new PlatformServicesIntent[]
                {
                    new CheckEntitlementIntent(Player, true),
                    new UnlockIntent("grab.master", "key-1", ProviderOutcome.Accepted),
                });
            }

            var replayA = Replay();
            var replayB = Replay();
            Assert.That(replayA.State.Equals(replayB.State), Is.True);
            Assert.That(replayA.State.Fingerprint(), Is.EqualTo(replayB.State.Fingerprint()));
        }

        [Test]
        public void TwoStatesWithTheSameNetProjectionsReachedByDifferentSequencesHaveEqualFingerprintsButMayDifferInRevision()
        {
            var direct = EntitledState().Apply(new UnlockIntent("grab.master", "key-1", ProviderOutcome.Accepted)).Value;

            var viaQueue = EntitledState();
            viaQueue = viaQueue.Apply(new UnlockIntent("grab.master", "key-1")).Value;
            viaQueue = viaQueue.Apply(new UnlockIntent("grab.master", "key-1", ProviderOutcome.Accepted)).Value;

            Assert.That(direct.Fingerprint(), Is.EqualTo(viaQueue.Fingerprint()));
            Assert.That(direct.Revision, Is.Not.EqualTo(viaQueue.Revision));
        }

        [Test]
        public void FingerprintOfTheOfflineQueueIsIndependentOfEnqueueOrder()
        {
            var stateA = EntitledState();
            stateA = stateA.Apply(new UnlockIntent("first.badge", "key-1")).Value;
            stateA = stateA.Apply(new UnlockIntent("second.badge", "key-2")).Value;

            var stateB = EntitledState();
            stateB = stateB.Apply(new UnlockIntent("second.badge", "key-2")).Value;
            stateB = stateB.Apply(new UnlockIntent("first.badge", "key-1")).Value;

            Assert.That(stateA.Fingerprint(), Is.EqualTo(stateB.Fingerprint()));
        }

        // ----- structured results with exactly the contract's stable codes (PC-14) -----

        [Test]
        public void PlatformServicesFailureDeclaresExactlyTheElevenContractCodesAndNoOthers()
        {
            var expected = new HashSet<string>
            {
                "identity.malformed", "capability.unknown", "provider.duplicate", "provider.declaration.invalid",
                "capability.unsupported", "identity.missing", "dispatch.mismatch", "state.stale",
                "not_entitled", "offline", "rate_limited",
            };
            var declared = typeof(PlatformServicesFailure)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.Name != nameof(PlatformServicesFailure.None))
                .Select(field => (string)field.GetRawConstantValue())
                .ToHashSet();
            Assert.That(declared, Is.EquivalentTo(expected));
        }

        [Test]
        public void AValidIntentSequencePassesCleanWithEveryOutcomeAccepted()
        {
            var state = InitialState();
            var sequence = new PlatformServicesIntent[]
            {
                new CheckEntitlementIntent(Player, true),
                new UnlockIntent("grab.master", "key-1", ProviderOutcome.Accepted),
                new ReportScoreIntent("weekly", 100, LeaderboardUploadPolicy.KeepBest, "key-2", ProviderOutcome.Accepted),
                new ReadLeaderboardIntent("weekly", LeaderboardRange.Top),
                new CloudWriteIntent("slot", new byte[] { 9 }, "key-3", ProviderOutcome.Accepted),
                new CloudReadIntent("slot"),
            };
            var result = state.ApplyAll(sequence);
            Assert.That(result.AllAccepted, Is.True, string.Join(", ", result.Outcomes.Where(outcome => !outcome.Accepted).Select(outcome => outcome.Code)));
            Assert.That(result.AcceptedCount, Is.EqualTo(sequence.Length));
            Assert.That(result.RejectedCount, Is.EqualTo(0));
        }

        // ----- Core purity (PC-15) -----

        [Test]
        public void CoreAssemblyReferencesNoUnityEngineAssembly()
        {
            var assembly = typeof(PlatformServicesState).Assembly;
            var referenced = assembly.GetReferencedAssemblies().Select(name => name.Name ?? string.Empty);
            Assert.That(referenced.Any(name => name.IndexOf("UnityEngine", StringComparison.OrdinalIgnoreCase) >= 0), Is.False, string.Join(",", referenced));
        }

        [Test]
        public void NoCoreRuntimeSourceFileMentionsUnityEngineOrAVendorSdkType()
        {
            var runtimeDirectory = System.IO.Path.GetFullPath(System.IO.Path.Combine(TestSourceDirectory(), "..", "..", "Runtime"));
            Assert.That(System.IO.Directory.Exists(runtimeDirectory), Is.True, runtimeDirectory);

            var forbidden = new[] { "UnityEngine", "ScriptableObject", "Oculus", "Steamworks", "GameCenter", "Unity.Services", "Pico." };
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
        public void AnAchievementIdLeaderboardIdAndCloudKeyEnterAndLeaveTheCoreAsOpaqueUninterpretedStrings()
        {
            const string weirdAchievementId = "Vendor Achievement #7 (beta)!";
            var state = EntitledState();
            var result = state.Apply(new UnlockIntent(weirdAchievementId, "key-1", ProviderOutcome.Accepted));
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Value.IsAchievementUnlocked(weirdAchievementId), Is.True);
        }

        // ----- LESSON-011: actor and expected revision (PC-16) -----

        [Test]
        public void EveryIntentDefaultsToPlayerActorWithNoExpectedRevisionWhenUsingTheShortConstructor()
        {
            PlatformServicesIntent[] intents =
            {
                new CheckEntitlementIntent(Player, true),
                new UnlockIntent("a", "k1"),
                new ReportScoreIntent("lb", 1, LeaderboardUploadPolicy.KeepBest, "k2"),
                new ReadLeaderboardIntent("lb", LeaderboardRange.Top),
                new CloudWriteIntent("k", new byte[] { 1 }, "k3"),
                new CloudReadIntent("k"),
            };
            foreach (var intent in intents)
            {
                Assert.That(intent.Actor, Is.EqualTo(IntentActor.Player), intent.Describe());
                Assert.That(intent.ExpectedRevision, Is.Null, intent.Describe());
            }
        }

        [Test]
        public void EveryIntentAcceptsAnExplicitActorFromTheClosedSetAndAnExpectedRevision()
        {
            var intent = new UnlockIntent("a", "k1", ProviderOutcome.Accepted, IntentActor.Agent, 3);
            Assert.That(intent.Actor, Is.EqualTo(IntentActor.Agent));
            Assert.That(intent.ExpectedRevision, Is.EqualTo(3));

            var replayIntent = new CheckEntitlementIntent(Player, true, IntentActor.Replay, 0);
            Assert.That(replayIntent.Actor, Is.EqualTo(IntentActor.Replay));
        }

        [Test]
        public void StateRevisionStartsAtZeroAndIncreasesByExactlyOneOnAnAcceptedIntentButNeverOnARejected()
        {
            var state = InitialState();
            Assert.That(state.Revision, Is.EqualTo(0));

            var accepted = state.Apply(new CheckEntitlementIntent(Player, true));
            Assert.That(accepted.Succeeded, Is.True, accepted.Message);
            Assert.That(accepted.Value.Revision, Is.EqualTo(1));

            var rejected = accepted.Value.Apply(new CheckEntitlementIntent(Player, false));
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(accepted.Value.Revision, Is.EqualTo(1), "a rejected intent must not advance the revision it was applied to");
        }

        // ----- LESSON-011: stale expected revision (PC-17) -----

        [Test]
        public void AStaleExpectedRevisionIsRejectedWithStateStaleAndChangesNothing()
        {
            var state = EntitledState();
            Assert.That(state.Revision, Is.EqualTo(1));

            var stale = state.Apply(new UnlockIntent("a", "k1", ProviderOutcome.Accepted, IntentActor.Player, 0));
            Assert.That(stale.Succeeded, Is.False);
            Assert.That(stale.Code, Is.EqualTo(PlatformServicesFailure.StateStale));
            Assert.That(state.IsAchievementUnlocked("a"), Is.False);
        }

        [Test]
        public void EveryIntentTypeRejectsAStaleExpectedRevisionWithStateStaleRegardlessOfActor()
        {
            var state = EntitledState();
            PlatformServicesIntent[] staleIntents =
            {
                new UnlockIntent("a", "k1", ProviderOutcome.Accepted, IntentActor.Agent, 0),
                new ReportScoreIntent("lb", 1, LeaderboardUploadPolicy.KeepBest, "k2", ProviderOutcome.Accepted, IntentActor.Replay, 0),
                new ReadLeaderboardIntent("lb", LeaderboardRange.Top, IntentActor.Import, 0),
                new CloudWriteIntent("k", new byte[] { 1 }, "k3", ProviderOutcome.Accepted, IntentActor.Player, 0),
                new CloudReadIntent("k", IntentActor.Player, 0),
            };
            foreach (var intent in staleIntents)
            {
                var result = state.Apply(intent);
                Assert.That(result.Succeeded, Is.False, intent.Describe());
                Assert.That(result.Code, Is.EqualTo(PlatformServicesFailure.StateStale), intent.Describe());
            }
        }

        [Test]
        public void AMatchingExpectedRevisionIsAcceptedExactlyLikeNoExpectedRevisionAtAll()
        {
            var state = EntitledState();
            var withRevision = state.Apply(new UnlockIntent("a", "k1", ProviderOutcome.Accepted, IntentActor.Player, 1));
            var withoutRevision = state.Apply(new UnlockIntent("a", "k2", ProviderOutcome.Accepted));
            Assert.That(withRevision.Succeeded, Is.True, withRevision.Message);
            Assert.That(withoutRevision.Succeeded, Is.True, withoutRevision.Message);
            Assert.That(withRevision.Value.IsAchievementUnlocked("a"), Is.EqualTo(withoutRevision.Value.IsAchievementUnlocked("a")));
        }

        // ----- LESSON-011: actor never changes validation (PC-18) -----

        [Test]
        public void PlayerAndAgentIssuingTheSameAcceptedIntentTakeTheSamePathAndProduceEqualResultingState()
        {
            var state = EntitledState();
            var byPlayer = state.Apply(new UnlockIntent("a", "key-player", ProviderOutcome.Accepted));
            var byAgent = state.Apply(new UnlockIntent("a", "key-agent", ProviderOutcome.Accepted, IntentActor.Agent));
            Assert.That(byPlayer.Succeeded, Is.True, byPlayer.Message);
            Assert.That(byAgent.Succeeded, Is.True, byAgent.Message);
            Assert.That(byPlayer.Value.IsAchievementUnlocked("a"), Is.EqualTo(byAgent.Value.IsAchievementUnlocked("a")));
        }

        [Test]
        public void PlayerAndAgentIssuingTheSameRejectedIntentGetTheSameFailureCode()
        {
            var state = InitialState();
            var byPlayer = state.Apply(new UnlockIntent("a", "key-player"));
            var byAgent = state.Apply(new UnlockIntent("a", "key-agent", null, IntentActor.Agent));
            Assert.That(byPlayer.Code, Is.EqualTo(byAgent.Code));
            Assert.That(byPlayer.Code, Is.EqualTo(PlatformServicesFailure.IdentityMissing));
        }

        // ----- LESSON-011: the replay log (PC-19) -----

        [Test]
        public void ReplayLogRecordsTheIssuingActorAndTheRevisionAfterEveryOutcomeAcceptedOrRejected()
        {
            var state = InitialState();
            var sequence = new PlatformServicesIntent[]
            {
                new CheckEntitlementIntent(Player, true, IntentActor.Player, null),
                new UnlockIntent("a", "k1", ProviderOutcome.Accepted, IntentActor.Agent, null),
                new UnlockIntent("a", "k1", ProviderOutcome.RateLimited, IntentActor.Replay, null),
            };
            var result = state.ApplyAll(sequence);
            Assert.That(result.Outcomes.Count, Is.EqualTo(3));
            Assert.That(result.Outcomes[0].Actor, Is.EqualTo(IntentActor.Player));
            Assert.That(result.Outcomes[0].Accepted, Is.True);
            Assert.That(result.Outcomes[0].RevisionAfter, Is.EqualTo(1));
            Assert.That(result.Outcomes[1].Actor, Is.EqualTo(IntentActor.Agent));
            Assert.That(result.Outcomes[1].Accepted, Is.True);
            Assert.That(result.Outcomes[1].RevisionAfter, Is.EqualTo(2));
            Assert.That(result.Outcomes[2].Actor, Is.EqualTo(IntentActor.Replay));
            Assert.That(result.Outcomes[2].Code, Is.EqualTo(PlatformServicesFailure.DispatchMismatch), "the same key resubmitted with a different outcome than the recorded one is a mismatch, not a fresh resolution");
        }

        private static string TestSourceDirectory([CallerFilePath] string sourceFilePath = "") => System.IO.Path.GetDirectoryName(sourceFilePath);
    }
}
