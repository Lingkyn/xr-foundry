# Changelog

All notable repository-level changes are documented here. Package-level API changes
live in each package's `CHANGELOG.md`.

## Unreleased

- Added the renderer-neutral Settings family: typed definitions and values,
  profiles, scoped transactions, whole-snapshot constraints, deterministic
  apply/reverse rollback, persistence seams, accessibility discoverability
  metadata, and a ScriptableObject Unity authoring adapter.
- Verified Settings Core (25 tests) and the combined Core/Unity graph (31 tests)
  from immutable Git UPM pins in clean Unity 6000.3.19f1 Windows Editor consumers.
- Added Foundry V1: a public lifecycle, official-source manifest, machine-readable
  first Unity batch, next source-gate queue, release policy, and deterministic
  dry-run-first Unity package scaffolder.
- Added fast structure feedback separately from the full repository contract and
  preserved exact-consumer and named-device evidence as stronger independent gates.
- Registered all nine implemented Unity packages in the first incubating batch
  without promoting maturity or inventing device claims.
- Established the canonical renderer-neutral Inventory package graph:
  `com.lingkyn.inventory.presentation`, peer UGUI and UI Toolkit renderers, and
  renderer-explicit XR compositions without a speculative shared XR-core package.
- Finalized the initialization layout under `packages/unity/foundations` and
  `packages/unity/systems/inventory`. Catalogs, install URLs, reference paths, and
  validation use that single layout; no old-path compatibility surface is kept.
- Added a version-adaptive reference contract: standards and tests may generate a
  target-specific candidate, while each installable manifest and support claim
  remains bound to an exact machine-validated compatibility profile.
- Bound the finalized package tree to nine exact Unity 6000.3.19f1 Editor
  compatibility profiles with immutable manifests, resolved locks, compile
  receipts, and applicable EditMode/PlayMode NUnit results. Evidence does not
  transfer across version, dependency, renderer, build, input, runtime, or device
  tuples.
- Added current official Unity world-space UI Toolkit, XRI UI Toolkit, PICO
  multimodal direction, and Apple spatial-design sources with bounded roles and
  explicit non-claim limits.
- Added a provider-neutral, machine-checkable Device Lab route with an Inventory
  world-space UI plan, PICO tracked-controller profile, and fail-closed promotion
  rules for install/open, world anchoring, targeting, interaction states,
  readability, reach, occlusion, and comfort.
- Added a provider-neutral, machine-checkable Inventory XR device receipt with a
  PICO tracked-controller acceptance profile, official PICO evidence boundaries,
  and fail-closed promotion rules for install/open, world anchoring, targeting,
  interaction states, readability, reach, occlusion, and comfort.
- Added the optional `com.lingkyn.inventory.xr` incubating package with a
  provider-neutral world-space prefab, ScriptableObject profile, fail-closed scene
  validation, tracked-ray and real XRI poke tests, and an imported setup sample;
  this earlier single-renderer route was subsequently replaced by the canonical
  renderer-explicit XR graph above, while Android and PICO evidence remain separate
  promotion gates.
- Added Agent Commons V1: a public, lease-based Task Hall and revision-bound Device
  Lab with machine-readable task/profile/receipt/label contracts, safe public forms,
  explicit permission boundaries, and provider-neutral agent guidance.
- Hardened validation CI with least-privilege permissions, fork-safe checkout,
  action pins at reviewed full commit SHAs, concurrency cancellation, and a bounded
  timeout; comment commands remain non-executable coordination text.
- Added parsed YAML workflow policy enforcement, runtime Draft 2020-12 task/device
  Schema validation, lifecycle/lease/gate invariants, enumerable Device Lab claims,
  deterministic result derivation, and extension-neutral public leakage scanning.
- Established UGUI `0.2.0` directly as the incubating renderer projection with
  functional nested prefabs, stable-address intents, bounded scrolling, semantic
  state samples, prefab-backed raycast tests, and cross-layer projection gates.
- Reopened the UGUI candidate after a structure-only false positive; `0.1.1`
  now ships functional nested prefabs, stable-address intents, bounded scrolling,
  semantic state samples, prefab-backed raycast tests, and cross-layer projection gates.
- Promoted UGUI `0.1.1` to candidate after immutable Git install, Input System-only
  sample setup, full package tests, and consumer-owned prefab-variant upgrade,
  rollback, and final-upgrade evidence passed; retained `0.1.0` as superseded history.
- Reconciled Inventory implementation, roadmap, package/reference catalogs, and
  promotion evidence; added a fail-closed projection-coherence validator and
  explicit earliest-unsatisfied gates for every Inventory package layer.
- Added the first Inventory Package Family standard with a positive-external-source
  whitelist, engine-light core and optional Unity UI/XR package boundaries, nested
  prefab composition, persistence and transaction contracts, and promotion gates.
- Added repository checks that reject consumer material, screened-out candidates,
  and non-positive sources from Inventory derivation inputs.
- Added the independently authored `com.lingkyn.inventory.core` incubating package,
  including atomic mutations, immutable snapshots, structured failures, policies,
  events, persistence envelopes, and deterministic/stateful invariant tests.
- Established the clean-history XR Foundry public foundation.
- Added consumer-neutral `com.lingkyn.*` Unity package identities.
- Added package and reference catalogs, coding-agent discovery adapters, maturity,
  validation, tests, samples, CI, community guidance, and security reporting.
- Root-anchored Unity build-output ignores so nested package build source remains
  tracked, with Git URL consumer and namespace-link regression validation.
