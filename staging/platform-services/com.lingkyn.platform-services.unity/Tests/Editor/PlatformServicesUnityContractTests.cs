using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Lingkyn.PlatformServices.Core;
using Lingkyn.PlatformServices.Unity;
using UnityEngine;

namespace Lingkyn.PlatformServices.Unity.Editor.Tests
{
    // Authored and unexecuted: this assembly has never compiled or run. It is checked against
    // docs/standards/platform-services/verification-contract.md and coverage-map.json. No test here
    // claims that a store granted entitlement, unlocked an achievement, accepted a score, or
    // persisted a cloud file: every assertion is against the recording fake, a fixed probe, or the
    // in-process Core state.
    public sealed class PlatformServicesUnityContractTests
    {
        private static readonly ProviderId FullProviderId = ProviderId.Parse("meta_horizon_platform");

        private static PlatformServicesState BuildState()
        {
            var builder = new ProviderRegistryBuilder();
            builder.Register(FullProviderId, new[]
            {
                PlatformServiceCapability.Entitlement,
                PlatformServiceCapability.Achievement,
                PlatformServiceCapability.Leaderboard,
                PlatformServiceCapability.CloudSave,
            });
            var registry = builder.Build();
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

        // ----- thin per-vendor adapter shells behind IPlatformServicesProvider (PU-01) -----

        private static IEnumerable<IPlatformServicesProvider> AllVendorShells()
        {
            yield return new MetaPlatformProvider();
            yield return new PicoPlatformProvider();
            yield return new SteamworksProvider();
            yield return new AppleGameCenterProvider();
            yield return new UnityGamingServicesProvider();
        }

        [Test]
        public void EveryVendorShellReportsProviderSdkMissingForEveryCapabilityCallWithoutItsDefine()
        {
            foreach (var shell in AllVendorShells())
            {
                AssertDiagnostic(shell.CheckEntitlement(), shell);
                AssertDiagnostic(shell.Unlock("a", "k1"), shell);
                AssertDiagnostic(shell.ReportScore("lb", 1, LeaderboardUploadPolicy.KeepBest, "k2"), shell);
                AssertDiagnostic(shell.ReadLeaderboard("lb", LeaderboardRange.Top), shell);
                AssertDiagnostic(shell.CloudWrite("k", new byte[] { 1 }, "k3"), shell);
                AssertDiagnostic(shell.CloudRead("k"), shell);
            }
        }

        private static void AssertDiagnostic(PlatformServicesProviderCallResult result, IPlatformServicesProvider shell)
        {
            Assert.That(result.HasDiagnostic, Is.True, shell.GetType().Name);
            Assert.That(result.DiagnosticCode, Is.EqualTo(PlatformServicesUnityFailure.ProviderSdkMissing), shell.GetType().Name);
        }

        [Test]
        public void EveryVendorShellNamesItsOwnVendorInTheDiagnosticMessage()
        {
            Assert.That(new MetaPlatformProvider().CheckEntitlement().DiagnosticMessage, Is.EqualTo("meta_horizon_platform"));
            Assert.That(new PicoPlatformProvider().CheckEntitlement().DiagnosticMessage, Is.EqualTo("pico_platform"));
            Assert.That(new SteamworksProvider().CheckEntitlement().DiagnosticMessage, Is.EqualTo("steamworks"));
            Assert.That(new AppleGameCenterProvider().CheckEntitlement().DiagnosticMessage, Is.EqualTo("apple_game_center"));
            Assert.That(new UnityGamingServicesProvider().CheckEntitlement().DiagnosticMessage, Is.EqualTo("unity_gaming_services"));
        }

        [Test]
        public void AVendorShellDiagnosticReachesTheRuntimeCallerWithoutTouchingTheCoreOrQueueingItOffline()
        {
            var runtime = new PlatformServicesUnityRuntime(BuildState(), new MetaPlatformProvider(), new FixedConnectivitySignal(true));
            runtime.ApplyConsumerOwnedFallback();

            var outcome = runtime.Unlock("a", "k1");

            Assert.That(outcome.Accepted, Is.False);
            Assert.That(outcome.Code, Is.EqualTo(PlatformServicesUnityFailure.ProviderSdkMissing));
            Assert.That(runtime.State.Revision, Is.EqualTo(0), "a diagnostic must never reach the Core");
            Assert.That(runtime.State.PendingWrites, Is.Empty, "a diagnostic must never be queued offline");
        }

        // ----- source-rule: no vendor SDK namespace outside its owning shell (PU-02) -----

        private static readonly IReadOnlyDictionary<string, string> VendorTokenOwners = new Dictionary<string, string>
        {
            ["Oculus.Platform"] = "MetaPlatformProvider.cs",
            ["Pico.Platform"] = "PicoPlatformProvider.cs",
            ["Steamworks"] = "SteamworksProvider.cs",
            ["GameCenter"] = "AppleGameCenterProvider.cs",
            ["Unity.Services.CloudSave"] = "UnityGamingServicesProvider.cs",
        };

        [Test]
        public void NoVendorSdkNamespaceTokenAppearsOutsideItsOwningShellFile()
        {
            foreach (var pair in VendorTokenOwners)
            {
                foreach (var (path, text) in RuntimeSourceFiles())
                {
                    if (!text.Contains(pair.Key)) continue;
                    Assert.That(Path.GetFileName(path), Is.EqualTo(pair.Value), $"'{pair.Key}' must appear only in {pair.Value}.");
                }
            }
        }

        // ----- the generic, define-independent vendor readiness gate (PU-03) -----

        [Test]
        public void VendorReadinessGateReturnsSingletonMissingWhenTheSingletonProbeFails()
        {
            var gate = new VendorReadinessGate(new FixedVendorSingletonProbe(false), new FixedVendorInitializedProbe(true), new FixedVendorCallbackProbe(true));
            Assert.That(gate.CheckReadiness(), Is.EqualTo(PlatformServicesUnityFailure.ProviderSingletonMissing));
        }

        [Test]
        public void VendorReadinessGateReturnsUninitializedWhenTheInitializedProbeFailsAfterTheSingletonResolves()
        {
            var gate = new VendorReadinessGate(new FixedVendorSingletonProbe(true), new FixedVendorInitializedProbe(false), new FixedVendorCallbackProbe(true));
            Assert.That(gate.CheckReadiness(), Is.EqualTo(PlatformServicesUnityFailure.ProviderUninitialized));
        }

        [Test]
        public void VendorReadinessGateReturnsCallbackUnregisteredWhenOnlyTheCallbackProbeFails()
        {
            var gate = new VendorReadinessGate(new FixedVendorSingletonProbe(true), new FixedVendorInitializedProbe(true), new FixedVendorCallbackProbe(false));
            Assert.That(gate.CheckReadiness(), Is.EqualTo(PlatformServicesUnityFailure.ProviderCallbackUnregistered));
        }

        [Test]
        public void VendorReadinessGateReturnsNullWhenEveryProbePasses()
        {
            var gate = new VendorReadinessGate(new FixedVendorSingletonProbe(true), new FixedVendorInitializedProbe(true), new FixedVendorCallbackProbe(true));
            Assert.That(gate.CheckReadiness(), Is.Null);
        }

        [Test]
        public void VendorReadinessGateConstructionRequiresExplicitProbes()
        {
            Assert.Throws<ArgumentNullException>(() => new VendorReadinessGate(null, new FixedVendorInitializedProbe(true), new FixedVendorCallbackProbe(true)));
            Assert.Throws<ArgumentNullException>(() => new VendorReadinessGate(new FixedVendorSingletonProbe(true), null, new FixedVendorCallbackProbe(true)));
            Assert.Throws<ArgumentNullException>(() => new VendorReadinessGate(new FixedVendorSingletonProbe(true), new FixedVendorInitializedProbe(true), null));
        }

        // ----- the injectable RecordingPlatformProvider fake (PU-04) -----

        [Test]
        public void RecordingProviderRecordsExactlyWhichCapabilityTargetAndIdempotencyKeyItReceived()
        {
            var provider = new RecordingPlatformProvider();
            provider.Unlock("grab.master", "key-1");
            provider.ReportScore("weekly", 100, LeaderboardUploadPolicy.KeepBest, "key-2");
            provider.CloudWrite("slot", new byte[] { 1 }, "key-3");

            Assert.That(provider.ReceivedCalls, Is.EqualTo(new[]
            {
                "unlock:grab.master:key-1",
                "report_score:weekly:100:KeepBest:key-2",
                "cloud_write:slot:key-3",
            }));
        }

        [Test]
        public void RecordingProviderMaintainsItsOwnInMemoryAchievementLeaderboardAndCloudStore()
        {
            var provider = new RecordingPlatformProvider();
            provider.Unlock("grab.master", "key-1");
            provider.ReportScore("weekly", 100, LeaderboardUploadPolicy.KeepBest, "key-2");
            provider.CloudWrite("slot", new byte[] { 7 }, "key-3");

            Assert.That(provider.UnlockedAchievements, Contains.Item("grab.master"));
            Assert.That(provider.LeaderboardStore["weekly"], Is.EqualTo(100));
            Assert.That(provider.CloudStore["slot"], Is.EqualTo(new byte[] { 7 }));
        }

        // ----- the boot entitlement gate (PU-05) -----

        [Test]
        public void EveryOtherCapabilityCallIsBlockedWithEntitlementGateBlockedBeforeCheckEntitlementSucceeds()
        {
            var provider = new RecordingPlatformProvider();
            var runtime = new PlatformServicesUnityRuntime(BuildState(), provider, new FixedConnectivitySignal(true));

            var unlock = runtime.Unlock("a", "k1");
            var score = runtime.ReportScore("lb", 1, LeaderboardUploadPolicy.KeepBest, "k2");
            var read = runtime.ReadLeaderboard("lb", LeaderboardRange.Top);
            var write = runtime.CloudWrite("k", new byte[] { 1 }, "k3");
            var cloudRead = runtime.CloudRead("k");

            foreach (var outcome in new[] { unlock, score, read, write, cloudRead })
            {
                Assert.That(outcome.Accepted, Is.False);
                Assert.That(outcome.Code, Is.EqualTo(PlatformServicesUnityFailure.EntitlementGateBlocked));
            }
            Assert.That(provider.ReceivedCalls, Is.Empty, "a gate-blocked call must never reach the provider");
            Assert.That(runtime.State.Revision, Is.EqualTo(0), "a gate-blocked call must never reach the Core");
        }

        [Test]
        public void ANotEntitledCheckEntitlementLeavesTheGateClosed()
        {
            var provider = new RecordingPlatformProvider { Entitled = false };
            var runtime = new PlatformServicesUnityRuntime(BuildState(), provider, new FixedConnectivitySignal(true));

            var checkResult = runtime.CheckEntitlement();
            Assert.That(checkResult.Accepted, Is.False);
            Assert.That(checkResult.Code, Is.EqualTo(PlatformServicesFailure.NotEntitled));
            Assert.That(runtime.EntitlementGateOpen, Is.False);

            var blocked = runtime.Unlock("a", "k1");
            Assert.That(blocked.Code, Is.EqualTo(PlatformServicesUnityFailure.EntitlementGateBlocked));
        }

        [Test]
        public void AnAcceptedCheckEntitlementOpensTheGate()
        {
            var provider = new RecordingPlatformProvider { Entitled = true };
            var runtime = new PlatformServicesUnityRuntime(BuildState(), provider, new FixedConnectivitySignal(true));

            var checkResult = runtime.CheckEntitlement();
            Assert.That(checkResult.Accepted, Is.True, checkResult.Message);
            Assert.That(runtime.EntitlementGateOpen, Is.True);

            var unlock = runtime.Unlock("a", "k1");
            Assert.That(unlock.Code, Is.Not.EqualTo(PlatformServicesUnityFailure.EntitlementGateBlocked));
        }

        [Test]
        public void ApplyConsumerOwnedFallbackOpensTheGateWithoutCheckEntitlement()
        {
            var provider = new RecordingPlatformProvider();
            var runtime = new PlatformServicesUnityRuntime(BuildState(), provider, new FixedConnectivitySignal(true));

            Assert.That(runtime.EntitlementGateOpen, Is.False);
            runtime.ApplyConsumerOwnedFallback();
            Assert.That(runtime.EntitlementGateOpen, Is.True);

            var unlock = runtime.Unlock("a", "k1");
            Assert.That(unlock.Code, Is.Not.EqualTo(PlatformServicesUnityFailure.EntitlementGateBlocked));
        }

        [Test]
        public void NoRuntimeSourceFileCallsSceneManagerOrApplicationQuit()
        {
            foreach (var (path, text) in RuntimeSourceFiles())
            {
                Assert.That(text, Does.Not.Contain("SceneManager"), path);
                Assert.That(text, Does.Not.Contain("Application.Quit"), path);
            }
        }

        // ----- explicit idempotency-key mapping (PU-06) -----

        [Test]
        public void ARetriedCallForTheSameIdempotencyKeyIsNeverSentToTheProviderTwice()
        {
            var provider = new RecordingPlatformProvider();
            var runtime = new PlatformServicesUnityRuntime(BuildState(), provider, new FixedConnectivitySignal(true));
            runtime.CheckEntitlement();

            runtime.Unlock("grab.master", "key-1");
            runtime.Unlock("grab.master", "key-1");

            Assert.That(provider.ReceivedCalls.Count(call => call.StartsWith("unlock:")), Is.EqualTo(1), "the second call with the same key must be served from the dedupe record, never resent");
        }

        [Test]
        public void ADifferentIdempotencyKeyIsSentAsANewCall()
        {
            var provider = new RecordingPlatformProvider();
            var runtime = new PlatformServicesUnityRuntime(BuildState(), provider, new FixedConnectivitySignal(true));
            runtime.CheckEntitlement();

            runtime.Unlock("grab.master", "key-1");
            runtime.Unlock("grab.master", "key-2");

            Assert.That(provider.ReceivedCalls.Count(call => call.StartsWith("unlock:")), Is.EqualTo(2));
        }

        // ----- explicit connectivity signal and explicit-only queue draining (PU-07) -----

        [Test]
        public void AWriteIssuedWhileOfflineNeverCallsTheProviderAndQueuesInTheCore()
        {
            var provider = new RecordingPlatformProvider();
            var runtime = new PlatformServicesUnityRuntime(BuildState(), provider, new FixedConnectivitySignal(false));
            runtime.CheckEntitlement();

            var outcome = runtime.Unlock("grab.master", "key-1");
            Assert.That(outcome.Code, Is.EqualTo(PlatformServicesFailure.Offline));
            Assert.That(provider.ReceivedCalls.Any(call => call.StartsWith("unlock:")), Is.False, "an offline write must never reach the provider");
            Assert.That(runtime.State.PendingWrites.Count, Is.EqualTo(1));
        }

        [Test]
        public void DrainPendingWritesResolvesQueuedEntriesOnlyWhenExplicitlyCalled()
        {
            var offlineProvider = new RecordingPlatformProvider();
            var offlineSignal = new FixedConnectivitySignal(false);
            var runtime = new PlatformServicesUnityRuntime(BuildState(), offlineProvider, offlineSignal);
            runtime.CheckEntitlement();
            runtime.Unlock("grab.master", "key-1");
            Assert.That(runtime.State.PendingWrites.Count, Is.EqualTo(1), "the queue must not drain on its own");

            var reconnectedRuntime = new PlatformServicesUnityRuntime(runtime.State, offlineProvider, new FixedConnectivitySignal(true));
            var drained = reconnectedRuntime.DrainPendingWrites();

            Assert.That(drained.Count, Is.EqualTo(1));
            Assert.That(reconnectedRuntime.State.PendingWrites, Is.Empty);
            Assert.That(offlineProvider.ReceivedCalls.Count(call => call.StartsWith("unlock:")), Is.EqualTo(1), "the queued write is dispatched exactly once, from the explicit drain call");
        }

        [Test]
        public void NoRuntimeSourceFileDeclaresAPerFrameUpdateLoopOrPolling()
        {
            foreach (var (path, text) in RuntimeSourceFiles())
            {
                Assert.That(text, Does.Not.Contain("void Update("), path);
                Assert.That(text, Does.Not.Contain("MonoBehaviour"), path);
                Assert.That(text, Does.Not.Contain("Coroutine"), path);
                Assert.That(text, Does.Not.Contain("InvokeRepeating"), path);
            }
        }

        // ----- ScriptableObject provider configuration (PU-08) -----

        [Test]
        public void ProviderConfigConvertsDeterministicallyToTheCoresImmutableDescriptorWithoutMutatingTheAsset()
        {
            var asset = ScriptableObject.CreateInstance<PlatformServicesProviderConfigAsset>();
            asset.ProviderId = "meta_horizon_platform";
            asset.Entitlement = true;
            asset.CloudSave = true;
            asset.CloudSaveGuardRailBytes = 4096;

            var descriptor = PlatformServicesProviderConfigConverter.Convert(asset);

            Assert.That(descriptor.Id, Is.EqualTo(FullProviderId));
            Assert.That(descriptor.Supports(PlatformServiceCapability.Entitlement), Is.True);
            Assert.That(descriptor.Supports(PlatformServiceCapability.Achievement), Is.False);
            Assert.That(descriptor.CloudSaveGuardRailBytes, Is.EqualTo(4096));
            Assert.That(asset.CloudSaveGuardRailBytes, Is.EqualTo(4096), "conversion must not mutate the authored asset");
        }

        [Test]
        public void ProviderConfigValidationReportsAnInvalidGuardRailWithProviderDeclarationInvalidAndTheFieldPath()
        {
            var asset = ScriptableObject.CreateInstance<PlatformServicesProviderConfigAsset>();
            asset.ProviderId = "meta_horizon_platform";
            asset.CloudSave = true;
            asset.CloudSaveGuardRailBytes = 0;

            var report = PlatformServicesProviderConfigConverter.Validate(asset);

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Diagnostics[0].Code, Is.EqualTo(PlatformServicesFailure.ProviderDeclarationInvalid));
            Assert.That(report.Diagnostics[0].FieldPath, Is.EqualTo("capabilities"));
            Assert.That(report.Diagnostics[0].Source, Is.SameAs(asset));
        }

        [Test]
        public void ProviderConfigValidationReportsAnEmptyCapabilitySubsetWithProviderDeclarationInvalid()
        {
            var asset = ScriptableObject.CreateInstance<PlatformServicesProviderConfigAsset>();
            asset.ProviderId = "meta_horizon_platform";

            var report = PlatformServicesProviderConfigConverter.Validate(asset);

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Diagnostics[0].Code, Is.EqualTo(PlatformServicesFailure.ProviderDeclarationInvalid));
        }

        [Test]
        public void ProviderConfigValidationReportsAMalformedProviderIdWithIdentityMalformed()
        {
            var asset = ScriptableObject.CreateInstance<PlatformServicesProviderConfigAsset>();
            asset.ProviderId = "Not A Valid Id";

            var report = PlatformServicesProviderConfigConverter.Validate(asset);

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Diagnostics[0].Code, Is.EqualTo(PlatformServicesFailure.IdentityMalformed));
            Assert.That(report.Diagnostics[0].FieldPath, Is.EqualTo("providerId"));
        }

        [Test]
        public void ProviderConfigConversionThrowsWithTheSameReportOnAnInvalidAsset()
        {
            var asset = ScriptableObject.CreateInstance<PlatformServicesProviderConfigAsset>();
            asset.ProviderId = "meta_horizon_platform";

            var thrown = Assert.Throws<PlatformServicesProviderConfigException>(() => PlatformServicesProviderConfigConverter.Convert(asset));
            Assert.That(thrown.Report.IsValid, Is.False);
        }

        // ----- construction with explicit references only; independence (PU-09) -----

        [Test]
        public void RuntimeConstructionRequiresExplicitReferences()
        {
            Assert.Throws<ArgumentNullException>(() => new PlatformServicesUnityRuntime(null, new RecordingPlatformProvider(), new FixedConnectivitySignal(true)));
            Assert.Throws<ArgumentNullException>(() => new PlatformServicesUnityRuntime(BuildState(), null, new FixedConnectivitySignal(true)));
            Assert.Throws<ArgumentNullException>(() => new PlatformServicesUnityRuntime(BuildState(), new RecordingPlatformProvider(), null));
        }

        [Test]
        public void TwoRuntimesConstructedSideBySideShareNoState()
        {
            var firstProvider = new RecordingPlatformProvider();
            var secondProvider = new RecordingPlatformProvider();
            var firstRuntime = new PlatformServicesUnityRuntime(BuildState(), firstProvider, new FixedConnectivitySignal(true));
            var secondRuntime = new PlatformServicesUnityRuntime(BuildState(), secondProvider, new FixedConnectivitySignal(true));

            firstRuntime.CheckEntitlement();
            firstRuntime.Unlock("a", "k1");

            Assert.That(firstProvider.ReceivedCalls, Is.Not.Empty);
            Assert.That(secondProvider.ReceivedCalls, Is.Empty);
            Assert.That(secondRuntime.EntitlementGateOpen, Is.False);
            Assert.That(ReferenceEquals(firstRuntime.State, secondRuntime.State), Is.False);
        }

        [Test]
        public void NoRuntimeSourceFileUsesResourcesLoadASceneSearchOrASingleton()
        {
            var forbidden = new[] { "Resources.Load", "FindObjectOfType", "FindObjectsOfType", "GameObject.Find", "static readonly PlatformServicesUnityRuntime Instance" };
            foreach (var (path, text) in RuntimeSourceFiles())
            {
                foreach (var token in forbidden)
                {
                    Assert.That(text, Does.Not.Contain(token), $"{path} must not use '{token}'.");
                }
            }
        }

        // ----- the IPersistedDocumentSource seam (PU-10) -----

        [Test]
        public void PersistenceMirroredCloudWriteBuildsACloudWriteIntentFromTheSameBytesTheDocumentSourceHolds()
        {
            var payload = new byte[] { 1, 2, 3 };
            var source = new FixedPersistedDocumentSource(payload);

            var built = PersistenceMirroredCloudWrite.TryBuildIntent(source, "save.slot.0", "key-1", out var intent);

            Assert.That(built, Is.True);
            Assert.That(intent.Key, Is.EqualTo("save.slot.0"));
            Assert.That(intent.Payload, Is.EqualTo(payload));
        }

        [Test]
        public void PersistenceMirroredCloudWriteReturnsFalseWithoutADocumentRatherThanBuildingAnEmptyWrite()
        {
            var source = new FixedPersistedDocumentSource(null);

            var built = PersistenceMirroredCloudWrite.TryBuildIntent(source, "save.slot.0", "key-1", out var intent);

            Assert.That(built, Is.False);
            Assert.That(intent, Is.Null);
        }

        private static IEnumerable<(string Path, string Text)> RuntimeSourceFiles()
        {
            var runtimeDirectory = Path.GetFullPath(Path.Combine(TestSourceDirectory(), "..", "..", "Runtime"));
            Assert.That(Directory.Exists(runtimeDirectory), Is.True, runtimeDirectory);
            var sourceFiles = Directory.GetFiles(runtimeDirectory, "*.cs", SearchOption.AllDirectories);
            Assert.That(sourceFiles, Is.Not.Empty, runtimeDirectory);
            foreach (var sourceFile in sourceFiles)
            {
                yield return (sourceFile, File.ReadAllText(sourceFile));
            }
        }

        private static string TestSourceDirectory([CallerFilePath] string sourceFilePath = "") => Path.GetDirectoryName(sourceFilePath);
    }
}
