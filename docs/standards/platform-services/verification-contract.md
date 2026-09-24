# Platform services verification contract

## Source gate

- Every derivation input is positive, public, and role-bounded in
  [`source-manifest.json`](source-manifest.json).
- No consumer project, private store credential, product achievement or
  leaderboard content, or prior product entitlement code is a derivation
  input. The Persistence family's stage/integrity/flush pipeline and the
  Settings family's definition/profile/applicator model in this repository
  are context only: they are the seams a consumer may wire this family next
  to, and they contribute no derivation input and no evidence for this
  family's capability, provider, or intent semantics.
- A person confirms the source URLs, page paths, section anchors, versions,
  the license, and the maintenance state of each open-source implementation
  before the admission record is signed, because the authoring environment
  could not fetch them.

## Core gate

Deterministic tests must cover:

- identity: `AccountId` and `ProviderId` as validated identities with a
  canonical form, rejection of empty, whitespace, and malformed identifiers
  with a stable `identity.malformed` failure, and value equality between two
  identities with the same text; the two identity kinds share no
  cross-type equality;
- a closed platform-service capability set: `entitlement`, `achievement`,
  `leaderboard`, `cloud_save`, and `identity`; nothing outside this set is a
  capability, and a capability name outside the set is rejected with
  `capability.unknown`;
- a `ProviderDescriptor` declared by explicit registration: a `ProviderId`,
  the closed subset of capabilities that provider supports, and, only for
  `cloud_save`, a positive payload-size guard rail in bytes; a duplicate
  provider id is rejected with `provider.duplicate`, an empty capability
  subset or a `cloud_save` guard rail that is zero or negative is rejected
  with `provider.declaration.invalid`, and a rejected registration leaves
  the registry unchanged;
- an immutable `ProviderRegistry` built from registered descriptors,
  enumerating providers in canonical id order and unmodifiable after
  construction; composing a runtime that selects a provider for a required
  capability the selected provider's descriptor does not declare is
  rejected at composition time with `capability.unsupported`, naming the
  capability and the provider, before any intent can be issued against that
  composition;
- typed intents applied to an immutable `PlatformServicesState`:
  `check_entitlement`, `unlock` (achievement id), `report_score`
  (leaderboard id, score, an upload policy from a closed set),
  `read_leaderboard` (leaderboard id, a closed range selector), `cloud_write`
  (key, opaque payload, idempotency key), and `cloud_read` (key); every
  intent names the capability it exercises, and an intent naming a
  capability the active provider's descriptor does not declare is rejected
  with `capability.unsupported` rather than attempted;
- an identity precondition: every intent other than `check_entitlement`
  issued before an `AccountId` has been resolved for the active provider is
  rejected with `identity.missing`, and no achievement, score, leaderboard
  read, or cloud operation is recorded against an unresolved identity;
- explicit outcome folding: `unlock`, `report_score`, and `cloud_write`
  carry an optional caller-supplied outcome (`accepted`, `not_entitled`,
  `rate_limited`) reflecting what the provider actually reported; an
  `accepted` outcome updates the corresponding projection (the
  unlocked-achievement set, the leaderboard's last accepted score under its
  upload policy, or the cloud key's stored payload), and any other outcome
  updates no projection and is returned as the intent's structured result;
- the offline pending-write queue: `unlock`, `report_score`, and
  `cloud_write` issued with no outcome (the provider was never reached)
  enqueue into an ordered, immutable pending-write queue keyed by the
  intent's idempotency key rather than updating any projection, returning
  `offline` as the intent's structured result together with the assigned
  idempotency key; the same intent type resubmitted with the same
  idempotency key and now an outcome resolves and removes exactly that
  queued entry, in FIFO order relative to other resolutions, and folds the
  outcome exactly as an immediate write would have;
- idempotency: resubmitting an intent whose idempotency key has already
  been resolved returns the originally recorded result without reapplying
  it, and resubmitting the same key with a different outcome than the one
  already recorded is rejected with `dispatch.mismatch` rather than
  silently overwriting the prior result;
- state immutability: an accepted intent (queued, resolved, or immediately
  applied) produces a new state and leaves the prior state intact; a
  rejected intent leaves the prior state intact and returns it unchanged
  together with the failure code;
- deterministic replay: the same intent sequence over the same provider
  registry and initial state produces an equal final state and an equal
  fingerprint; the fingerprint covers the provider-registry fingerprint,
  the resolved account id per provider, the unlocked-achievement set, the
  last accepted score per leaderboard, the cloud key/payload map, and the
  pending-write queue's contents in canonical idempotency-key order;
- structured results with stable failure codes for a malformed identity, an
  unknown capability, a duplicate or invalid provider declaration, an
  unsupported capability at composition or intent time, a missing identity,
  a dispatch mismatch, a stale revision, `not_entitled`, `offline`, and
  `rate_limited`, each carrying the offending id, capability, or idempotency
  key, plus a valid intent sequence passing clean with every outcome
  accepted.

The Core references no vendor SDK type, holds no network socket, HTTP
client, or platform SDK type, and calls no store; an account id, achievement
id, leaderboard id, and cloud key enter and leave the Core as opaque
strings, and an outcome is a plain closed-set value the caller (the Unity
adapter, in tests a fake provider) supplies, never a value the Core infers
from a network call it never makes.

- one intent channel for people and agents (LESSON-011): every intent
  (`check_entitlement`, `unlock`, `report_score`, `read_leaderboard`,
  `cloud_write`, `cloud_read`) carries an `IntentActor` from the closed set
  `player`, `agent`, `replay`, `import`, and an optional expected revision;
  `PlatformServicesState` carries a monotonically increasing revision; a
  stale expected revision is rejected with `state.stale` before the
  intent's own rule runs and changes nothing; the actor never changes
  validation, so a player-issued and an agent-issued copy of the same
  intent take the same path and yield the same outcome and the same
  resulting state; and every outcome in the replay log carries the issuing
  actor and the revision after it.

## Unity adapter gate

EditMode tests must cover:

- thin provider adapters per vendor SDK behind one
  `IPlatformServicesProvider` interface (the Meta Horizon Platform SDK, the
  PICO platform SDK, Steamworks, Apple Game Center and StoreKit, and Unity
  Gaming Services Cloud Save), each constructed with explicit references
  and pinned to the validated SDK versions recorded in the compatibility
  profile; no vendor SDK call exists outside its own adapter assembly, and a
  source rule test over the Core and every other adapter assembly proves
  zero reference to a vendor SDK namespace it does not own;
- an injectable `IPlatformServicesProvider` fake with an in-memory account,
  achievement set, leaderboard, and cloud store, so Core-facing and adapter
  tests assert exactly which capability was exercised, which outcome was
  supplied, and which idempotency key was used, without a network or a
  vendor SDK dependency;
- an entitlement gate at boot: the runtime issues `check_entitlement` before
  any other capability's intents are accepted, and while entitlement is
  unresolved or reports `not_entitled`, every other intent is rejected with
  an explicit diagnostic naming the provider and the reported code; the
  gate fails closed (no default-entitled fallback), and the only way past
  it is a consumer-owned fallback intent the consumer explicitly defines and
  wires, never a silent bypass inside the adapter;
- an explicit diagnostic for every by-name or optional resolution: a vendor
  SDK singleton accessor, an optional callback or delegate registration, and
  an SDK-initialized check the adapter performs before its first call must
  report an explicit failure with a stable code when the target is missing,
  uninitialized, or unsupported, carrying a negative test for that case;
  silence or a reported success is a defect (LESSON-004);
- explicit idempotency-key mapping: the adapter maps the Core's opaque
  idempotency key to the vendor SDK's own request-deduplication mechanism
  when the vendor exposes one, and synthesizes a client-side dedupe record
  when it does not, so a retried call is never sent to the vendor twice for
  the same key; this mapping is explicit adapter logic, never a Core
  assumption about any vendor's deduplication behavior;
- an explicit connectivity signal: the adapter observes connectivity (or
  the vendor SDK's own online/offline callback) as an explicit input it
  passes to the intent, never a value the runtime polls or infers
  internally on a per-frame loop; a per-frame retry loop inside the adapter
  is a defect, and pending-queue draining runs only from an explicit
  reconnection event or an explicit consumer-issued drain call;
- explicit `ScriptableObject` construction for any Unity-authored provider
  configuration (a provider's capability subset, its cloud payload guard
  rail), converted deterministically to the Core's immutable
  `ProviderDescriptor` without mutating the authored asset; an asset with an
  invalid guard rail or an empty capability subset reports the Core's
  `provider.declaration.invalid` with the field path and the source asset,
  and construction throws with the same report;
- construction with explicit references only: a runtime is built from an
  explicit provider registry, an explicit active-provider selection, and an
  explicit `IPlatformServicesProvider`; no scene singleton, static instance,
  scene search, or reflection discovery resolves any of them, and two
  independently constructed runtimes over two fake providers that share no
  state are tested side by side so an intent accepted on one changes no
  projection, queue entry, or provider call of the other.

No test claims that a store granted entitlement, unlocked an achievement,
accepted a score, or persisted a cloud file, and no test reads a real
network response.

## Claim ceiling

An EditMode run proves capability and provider-descriptor validation,
composition-time fail-closed checks, intent validation and outcome folding,
the offline pending-write queue and idempotent resolution, deterministic
replay, the entitlement gate's fail-closed boot behavior, and
provider-adapter construction and diagnostics, for the tuple recorded in
the compatibility profile. It proves the plumbing only: that an accepted
intent reached the injectable provider port with the exact capability,
payload, and idempotency key the Core computed. Store approval, real
entitlement, and real leaderboard behaviour are receipts from a real store
build on a device, never package claims; no test in this family's gates
claims a store granted entitlement, unlocked an achievement, accepted a
score, or persisted a cloud file. Whether a store's review process approves
a build, whether an account's entitlement, achievement, leaderboard, or
cloud state matches what the family recorded, and any named-provider or
named-account behavior are human or process observations recorded in a
store sandbox log or a Device Lab receipt (for an XR headset store) that
binds the observation to a full commit SHA, the exact dependency tuple, the
named provider and account, the provider-registry fingerprint, the measured
outcome, and the tester identity; `not_tested` is never evidence.
