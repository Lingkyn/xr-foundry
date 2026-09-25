# Platform services staging

Status: **staging material, not a live package. Authored, unexecuted.** Nothing
here is in the package catalog, a batch, a compatibility profile, or a release.
The manifest is named `package.staging.json` on purpose so repository
validation does not treat this directory as a live `com.lingkyn.*` package.

This directory holds the first implementation of the Platform Services family
named in `docs/standards/platform-services/README.md`. It exists because the
production line cannot register a new package without a verified Unity
compatibility profile, and no Unity run has happened yet. The code is written
and tested on paper against
[`verification-contract.md`](../../docs/standards/platform-services/verification-contract.md);
it has not compiled.

## What is here

| Path | Content |
| --- | --- |
| `com.lingkyn.platform-services.core/Runtime/*.cs` | Engine-light Core: validated `AccountId`/`ProviderId` identity with one canonical dotted-segment form, the closed `PlatformServiceCapability` set (entitlement, achievement, leaderboard, cloud_save, identity) with `capability.unknown` for anything outside it, the immutable `ProviderRegistry` and `ProviderRegistryBuilder` built only by explicit registration of a `ProviderDescriptor` (its capability subset and, only for cloud_save, a positive payload-size guard rail), composition-time fail-closed capability checks, six typed intents (`CheckEntitlementIntent`, `UnlockIntent`, `ReportScoreIntent`, `ReadLeaderboardIntent`, `CloudWriteIntent`, `CloudReadIntent`) applied to the immutable `PlatformServicesState` with the identity precondition, explicit outcome folding, the offline pending-write queue, idempotent resolution, deterministic replay and a fingerprint, and the one-intent-channel actor/expected-revision/state.stale shape (LESSON-011) |
| `com.lingkyn.platform-services.core/Tests/Editor/PlatformServicesCoreContractTests.cs` | 55 EditMode tests mapped in `docs/standards/platform-services/coverage-map.json` |
| `com.lingkyn.platform-services.core/Samples~/PlatformServicesWalkthrough/` | Domain-only sample: a provider registry built by explicit registration, a composed initial state, a full check_entitlement/unlock/report_score/read_leaderboard/cloud_write/cloud_read intent sequence including an offline queue-and-resolve pass, and a replayed sequence; no vendor SDK, asset, or scene |
| `com.lingkyn.platform-services.unity/Runtime/*.cs` | Unity adapter: one `IPlatformServicesProvider` interface, thin per-vendor shells (`MetaPlatformProvider`, `PicoPlatformProvider`, `SteamworksProvider`, `AppleGameCenterProvider`, `UnityGamingServicesProvider`) each behind its own compile-time define (`XRFOUNDRY_META_PLATFORM` and the sibling defines) so the assembly compiles with no vendor SDK present and every call reports `provider.sdk_missing` without the define, the generic define-independent `VendorReadinessGate` and its three probes (LESSON-004), the `RecordingPlatformProvider` fake with its own in-memory account/achievement/leaderboard/cloud store, `PlatformServicesUnityRuntime` with the boot entitlement gate (fails closed; opens only on an accepted check_entitlement or an explicit consumer-owned fallback), the `ClientSideDedupeStore` idempotency-key mapping, the explicit `IPlatformServicesConnectivitySignal` and explicit-only `DrainPendingWrites`, `PlatformServicesProviderConfigAsset` as a `ScriptableObject` converted deterministically through `PlatformServicesProviderConfigConverter`, and the `IPersistedDocumentSource` seam (`PersistenceMirroredCloudWrite`) that composes a `cloud_write` on the same bytes a local save's document holds without depending on the Persistence package |
| `com.lingkyn.platform-services.unity/Tests/Editor/PlatformServicesUnityContractTests.cs` | 31 EditMode tests for the adapter gate, driven through fakes and fixed probes for every seam — never a store or a device |
| `docs/standards/platform-services/` | Standard README, source manifest, verification contract, coverage map, admission draft |

## One intent channel, and the LESSON-011 addition

`PlatformServicesState.Apply` is the one channel through which a person's UI,
an agent adapter, a replay, or an import all change platform-services state:
every intent (`check_entitlement`, `unlock`, `report_score`,
`read_leaderboard`, `cloud_write`, `cloud_read`) carries an `IntentActor`
(`player`, the default, `agent`, `replay`, or `import`) used only for
attribution and replay, and an optional expected revision that a stale value
rejects with `state.stale` before the intent's own rule ever runs — never a
second write path, and never a different validation rule for a different
actor. `PlatformServicesUnityRuntime`'s methods call this one entry point and
no other. This is the one-intent-channel Core-gate clause of
`docs/standards/platform-services/verification-contract.md` (LESSON-011), the
identical shape `staging/haptics` and `staging/live-tuning` carry;
`docs/standards/platform-services/coverage-map.json` rows PC-16 to PC-19 map
it to its tests. `docs/standards/lessons/lessons-register.json` has no
platform-services row yet for LESSON-011 (or for the other ten lessons);
`docs/standards/platform-services/README.md`'s own lessons table already
records the intended `adopted` disposition, but copying it into the actual
register is a maintainer record outside this package's own paths, made when
the family is admitted.

## How it moves into the tree

One Unity run turns this into a live package. The person or Agent with the
Editor:

1. Copies `docs/standards/platform-services/admission.draft.json` to
   `docs/foundry/admissions/platform-services.v1.json` (a maintainer decision)
   and writes the `platform-services-core` and `platform-services-unity`
   blueprints under `docs/foundry/blueprints/`.
2. Runs `python scripts/scaffold_unity_package.py docs/foundry/blueprints/platform-services-core.v1.json --output-root . --write`
   (and the same for the `platform-services-unity` blueprint), then replaces
   the generated scaffold sources with the files here and renames each
   `package.staging.json` to `package.json`, adding `.meta` files for every
   asset.
3. Adds the package to `package-catalog.json`, `component-catalog.json`,
   `capability-registry.json`, a building batch, and the lessons-register
   dispositions listed in `docs/standards/platform-services/README.md` (plus a
   LESSON-011 disposition, not yet recorded there).
4. Runs `python scripts/run_unity_gates.py --host com.lingkyn.platform-services.core`
   and records the compatibility profile from the receipt. This is also the
   run that can turn `PU-01`'s `partial` coverage green: with a vendor SDK
   package actually installed and its compile-time define enabled, the real
   call path behind that vendor's `#if` block compiles and can be tested
   against the vendor's own pinned version.
5. Runs the repository contract and the merge-readiness verdict, then opens
   the pull request.

Until step 4 happens, every claim about this code is "authored, unexecuted".

## Non-claims

- No test claims that a store granted entitlement, unlocked an achievement,
  accepted a score, or persisted a cloud file, or that any request reached a
  real network endpoint (the contract's claim ceiling). `RecordingPlatformProvider`,
  `FixedVendorSingletonProbe`, `FixedVendorInitializedProbe`,
  `FixedVendorCallbackProbe`, `FixedConnectivitySignal`, and
  `FixedPersistedDocumentSource` are all in-memory fakes; nothing here has
  reached a vendor SDK, a store, or a network socket.
- No vendor SDK is bundled with this package. Every per-vendor shell
  (`MetaPlatformProvider`, `PicoPlatformProvider`, `SteamworksProvider`,
  `AppleGameCenterProvider`, `UnityGamingServicesProvider`) compiles and is
  tested only along its no-define `provider.sdk_missing` path; the real call
  path behind each vendor's own compile-time define is illustrative source
  text only, and `docs/standards/platform-services/coverage-map.json` marks
  the clause it belongs to (`PU-01`) `partial` rather than claiming it is
  proven.
- No claim of real entitlement, achievement, leaderboard, or cloud-save
  behaviour on a named store or account. Whether a store's review process
  approves a build, and whether an account's entitlement, achievement,
  leaderboard, or cloud state matches what this family recorded, is a human
  or process observation recorded in a store sandbox log or a Device Lab
  receipt, per `docs/standards/platform-services/README.md`'s evidence
  boundary and this family's next steps — never a package claim.
- No app id, API key, store credential, or other secret appears anywhere in
  this staging tree; every example provider id, achievement id, leaderboard
  id, and cloud key is a plain illustrative string.
- No package id, catalog entry, maturity, release, or device status. Those
  exist only after admission and a green Unity gate.
