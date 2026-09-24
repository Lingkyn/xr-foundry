# Platform services package-family standard

Status: proposal in the source-gate queue (`NEXT-PLATFORM-SERVICES`); this is
the source gate only. No staged Core or Unity adapter implementation, package
directory, package id, catalog entry, maturity, release, or device status
exists in the tree.

This standard defines platform services as a closed capability set
(entitlement, achievement, leaderboard, cloud_save, identity), a provider
descriptor built by explicit registration that declares which capabilities a
vendor provider supports, typed intents (`check_entitlement`, `unlock`,
`report_score`, `read_leaderboard`, `cloud_write`, `cloud_read`) applied to
an immutable state with deterministic replay, and an offline pending-write
queue keyed by idempotency keys that replays in order, with a thin Unity
adapter over the store and platform vendor SDKs planned separately. It does
not define any product's store listing, purchase flow, achievement or
leaderboard content, or account tuning. It is derived only from the positive
public sources in [`source-manifest.json`](source-manifest.json).

[`docs/benchmarks/shipped-game-gap-matrix.md`](../../benchmarks/shipped-game-gap-matrix.md)
ranks platform services thirteenth and records why it belongs in the queue
now: every shipped rhythm-action or object-interaction shape needs an
entitlement check the store certifies at launch, and most need achievements,
leaderboards, or cloud save behind the same signed-in account.

## Capability boundary

The family separates:

- account and provider identity (`AccountId`, `ProviderId`) from any vendor
  SDK session, token, or account object;
- a closed platform-service capability set (`entitlement`, `achievement`,
  `leaderboard`, `cloud_save`, `identity`) from any store-specific feature
  name;
- an immutable `ProviderRegistry` built by explicit registration of each
  provider's supported capability subset and, for `cloud_save`, its payload
  guard rail, from any reflection, attribute scan, or platform-detection
  probe;
- `check_entitlement`, `unlock`, `report_score`, `read_leaderboard`,
  `cloud_write`, and `cloud_read` intents applied to an immutable
  `PlatformServicesState` whose only content is, per provider, the resolved
  identity, the entitlement status, the unlocked-achievement set, the last
  accepted score per leaderboard, and the cloud key/payload map, from any
  control, UI, or gameplay event that raises them;
- an ordered, idempotency-keyed pending-write queue for a write issued while
  the provider was never reached, from any consumer-owned retry loop or
  local cache; and
- structured results with stable failure codes (`identity.malformed`,
  `capability.unknown`, `provider.duplicate`, `provider.declaration.invalid`,
  `capability.unsupported`, `identity.missing`, `dispatch.mismatch`,
  `state.stale`, `not_entitled`, `offline`, `rate_limited`) from any store
  console, editor tooling, or platform review.

The family is a typed intent and state substrate, not a storefront, a
purchasing flow, or an account-management screen. It does not author, host,
or price a store listing, does not own the Settings family's definitions or
the Persistence family's save pipeline, and makes no claim that a store
granted entitlement, unlocked an achievement, accepted a score, or persisted
a cloud file.

## Why it composes

- **Persistence.** The Persistence family
  ([`docs/standards/persistence/README.md`](../persistence/README.md)) owns
  local save orchestration: staging, integrity, flush, and recovery over a
  consumer-owned document. A consumer may mirror the same document into this
  family's `cloud_write`/`cloud_read` capability so a local save and a cloud
  save carry the same bytes, but this family never calls into Persistence's
  pipeline and Persistence's evidence is not re-derived here; `cloud_save` is
  a provider-hosted document, not a second local save slot.
- **Settings.** The Settings family
  ([`docs/standards/settings/README.md`](../settings/README.md)) owns
  setting definitions, profiles, and applicators. A signed-in-as row or a
  linked-account row on a settings screen is a Settings-adjacent display
  reading this family's `identity` capability, not a setting this family
  defines; this family holds no dependency on Settings and Settings' model
  is not re-derived here.
- **Interaction, Audio, Haptics, XR UI shell, Live tuning.** None applies
  here. Platform-service intents are typically raised at a small number of
  explicit call sites (boot, a results screen, a save point), not from a
  per-frame semantic intent a binding table would route, and the family
  renders nothing and owns no visual skin or theme seam.

## Planned package boundary

| Layer | Owns | Must not own |
| --- | --- | --- |
| Engine-light Core `com.lingkyn.platform-services.core` (package id reserved only after admission) | account and provider identity; the closed capability set; the provider registry and explicit registration; the six typed intents; immutable state; the offline pending-write queue and idempotent resolution; deterministic replay and fingerprint; structured results | Vendor SDK types, network sockets, HTTP clients, store listings, purchase flows, product achievement/leaderboard content, Persistence's save pipeline, Settings' definitions |
| Unity adapter `com.lingkyn.platform-services.unity` (package id reserved only after admission) | thin per-vendor provider adapters behind `IPlatformServicesProvider` (Meta Horizon Platform SDK, PICO platform SDK, Steamworks, Apple Game Center and StoreKit, Unity Gaming Services Cloud Save); the injectable fake provider; the boot-time entitlement gate with a consumer-owned fallback intent; explicit diagnostics for every by-name or optional resolution; idempotency-key mapping to each vendor's own dedupe mechanism; an explicit connectivity signal driving pending-queue draining | Capability and provider semantics, intent validation rules, product store content, scene singletons, static instances, scene search, reflection discovery, platform detection |

## Evidence boundary

An EditMode run can prove capability and provider-descriptor validation,
composition-time fail-closed checks, intent validation and outcome folding,
the offline pending-write queue and idempotent resolution, deterministic
replay, the entitlement gate's fail-closed boot behavior, and
provider-adapter construction and diagnostics, for the declared tuple. It
proves the plumbing only. It cannot prove that a store granted entitlement,
unlocked an achievement, accepted a score, or persisted a cloud file, or
anything about a named provider's, store's, or account's actual behavior;
whether a store's review process approves a build, and whether an account's
recorded state matches this family's state, is a human or process
observation recorded in a store sandbox log or a Device Lab receipt, as
[`verification-contract.md`](verification-contract.md) states in its claim
ceiling.

## Lessons register dispositions (to be added to the register when the family goes live)

| Lesson | Disposition | Rationale |
| --- | --- | --- |
| LESSON-001 closed classification vs open tags | adopted | The platform-service capability set and every failure code are closed enums, and `AccountId`/`ProviderId` are validated identities; achievement ids, leaderboard ids, and cloud keys are opaque, vendor-defined content strings the Core never classifies, matching the boundary Persistence already draws around its own opaque identities |
| LESSON-002 migration path | deferred | No release exists yet; the first release records its compatibility policy, and the first breaking change to a capability name, an intent shape, or a stable failure code ships a migration note in the same commit |
| LESSON-003 single-workstation gate | adopted | Tests will run in `run_unity_gates.py` and the consumer workflow like every family; no gate in this standard depends on a maintainer workstation, and the fake provider means no gate depends on a real store account either |
| LESSON-004 by-name resolution fails closed | adopted | The Core resolves nothing by name. The Unity adapter gate requires an explicit diagnostic and a negative test for every by-name or optional resolution: a vendor SDK singleton accessor, an optional callback registration, and an SDK-initialized check, each pinned to the validated vendor SDK version |
| LESSON-005 clause coverage | deferred | No staged implementation or coverage map exists yet; the staged implementation item writes `coverage-map.json` mapping every Core and Unity adapter clause to named tests before any clause is cited as evidence |
| LESSON-006 sibling pinning | deferred | Applies at the first Git-consumer validation; the README install matrix will pin the Core and Unity adapter packages to one full commit SHA |
| LESSON-007 skin seam | not_applicable | The family renders nothing and owns no visual skin or theme seam; it drives vendor platform SDKs, not a renderer |
| LESSON-008 version bump is a verification claim | adopted | No package version exists; the first scaffold enters at 0.1.0 and stays there until its first verified compatibility profile |
| LESSON-009 tunable surface as data | not_applicable | The family exposes no injectable skin/theme or live-apply config seam a developer-tuning scaffold would attach to; provider configuration is authored, explicitly-referenced data converted once into the Core's immutable descriptor, not a live-apply seam a runtime host re-invokes |
| LESSON-010 developer tool is a peer client, never a dependency | not_applicable | The family owns no shell, dock, or UI panel; if a future debug inspector for entitlement, achievement, leaderboard, or cloud-save state is added, it attaches as a peer client of the XR UI shell or a host application, and this family holds no dependency on it either way |
| LESSON-011 one intent channel for people and agents | adopted | Every intent carries an `IntentActor` from the closed set `player`, `agent`, `replay`, `import`, and an optional expected revision against the state's monotonically increasing revision; a stale expected revision is rejected with `state.stale` before the intent's own rule runs, which is exactly the guard the offline pending-write queue needs so a delayed resolution can never silently overwrite another actor's already-accepted change |

## Next steps

1. A person confirms every URL, page path, section anchor, version pin,
   license, and open-source maintenance state in
   [`source-manifest.json`](source-manifest.json).
2. The maintainer copies [`admission.draft.json`](admission.draft.json) into
   `docs/foundry/admissions/` as the durable record through the
   [system admission gate](../../foundry/system-admission.md).
3. Once admitted, the staged implementation authors the Core and the Unity
   adapter under `staging/platform-services/` against
   [`verification-contract.md`](verification-contract.md), with
   `coverage-map.json` mapping every Core and Unity adapter clause to a
   named test (no partial or unmapped clause). The code is authored and
   unexecuted until a Unity run produces a compatibility profile, and
   nothing enters `packages/` before admission and a green gate.
4. A person or Agent with a Unity Editor runs
   `python scripts/run_unity_gates.py --host com.lingkyn.platform-services.core`,
   records the resulting compatibility profile, and either confirms the
   coverage map or turns it red.
5. The first store sandbox or Device Lab evidence for this family records
   entitlement, achievement, leaderboard, and cloud-save behaviour on a real
   store build as a human or process observation, and never as a package
   claim.

See also:

- [`verification-contract.md`](verification-contract.md)
- [`source-manifest.json`](source-manifest.json)
- [`admission.draft.json`](admission.draft.json)
- [`next-batch.json`](../../foundry/queue/next-batch.json), the queue this
  candidate joins, and the
  [system admission gate](../../foundry/system-admission.md)
- [`lessons-register.json`](../lessons/lessons-register.json)
- [`persistence/README.md`](../persistence/README.md), the local save-pipeline
  family this family's `cloud_save` capability is context-adjacent to
- [`settings/README.md`](../settings/README.md), the settings family this
  family's `identity` capability is context-adjacent to
- [`shipped-game-gap-matrix.md`](../../benchmarks/shipped-game-gap-matrix.md),
  which ranks this family thirteenth and states why the shipped shapes need it
- [`haptics/README.md`](../haptics/README.md) and
  [`locomotion/README.md`](../locomotion/README.md), sibling standards this
  family is modelled on
