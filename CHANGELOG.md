# Changelog

All notable repository-level changes are documented here. Package-level API changes
live in each package's `CHANGELOG.md`.

## Unreleased

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
