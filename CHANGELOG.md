# Changelog

All notable repository-level changes are documented here. Package-level API changes
live in each package's `CHANGELOG.md`.

## Unreleased

- Established the canonical Agent Commons V1 package graph: renderer-neutral
  `com.lingkyn.inventory.presentation`, peer UGUI and UI Toolkit renderers, and
  renderer-explicit XR compositions without a speculative shared XR-core package.
- Finalized the initialization layout under `packages/unity/foundations` and
  `packages/unity/systems/inventory`. Catalogs, install URLs, reference paths, and
  validation use that single layout; no old-path compatibility surface is kept.
- Added current official Unity world-space UI Toolkit, XRI UI Toolkit, PICO
  multimodal direction, and Apple spatial-design sources with bounded roles and
  explicit non-claim limits.
- Added a provider-neutral, machine-checkable Inventory XR device receipt with a
  PICO tracked-controller acceptance profile, official PICO evidence boundaries,
  and fail-closed promotion rules for install/open, world anchoring, targeting,
  interaction states, readability, reach, occlusion, and comfort.
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
