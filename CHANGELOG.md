# Changelog

All notable repository-level changes are documented here. Package-level API changes
live in each package's `CHANGELOG.md`.

## Unreleased

- Added the foundations family standard (`docs/standards/foundations/`): a
  minimal verification contract for `com.lingkyn.project-initializer` and
  `com.lingkyn.xr-baseline`, a coverage map binding its thirteen clauses to the
  six existing EditMode tests and naming the four still missing, the
  `RequiredFoldersAreUnderProjectRootAndUnique` test, and the
  `validate_foundation_assembly_references` repository rule that limits every
  foundation asmdef reference to Lingkyn or Unity-owned assemblies. LESSON-005 is now adopted
  for every live family.
- Closed every open test gap in the Inventory coverage maps: added the
  zero-quantity request test to Inventory Core and the invalid-limit diagnostic
  test to Inventory Unity, and added the `validate_inventory_isolation_rules`
  repository rule that pins the Presentation assembly to `Lingkyn.Inventory.Core`
  only and rejects `Resources.Load` and scene lookups in the authoring runtime.
  The nine remaining partial clauses are evidence gates.
- Audited every silent by-name resolution in the XR Baseline Editor tools and
  routed them through a new once-per-key `XrBaselineDiagnostics` warning, so
  `Initialize Sandbox` reports unresolved XRI, Input System, shader, or asset
  members instead of logging a clean success. LESSON-004 and LESSON-005 are now
  adopted for their remaining families, pending Editor execution.
- Authored the three Persistence Core and five Interaction Core tests their
  coverage maps had named, marking both maps fully covered pending Editor
  execution, and mapped the Inventory Unity authoring, Presentation,
  package-and-consumer, and XR gates in `coverage-map.json`, separating missing
  tests from evidence gates.
- Added clause-to-test coverage maps for the Persistence, Settings, and Interaction
  Core and Unity adapter gates, authored the six Inventory Core tests the Core map
  had named, stated in the Settings standard that accessibility category and
  feature id are open metadata, and made the XR Baseline hover visual warn once
  instead of silently idling when XRI members cannot be resolved.
- Added `scripts/run_unity_gates.py`: one command that finds a Unity Editor,
  generates a disposable host (the reference consumer, every live package, or one
  package plus its Foundry dependencies), audits exact test counts from source,
  runs each assembly in its own batchmode process, verifies every result, and
  writes a run receipt binding commit, host manifest, resolved lock, editor, and
  per-assembly verification. Covered by contract tests with a fake editor. Added
  `docs/validation/run-unity-gates.md` (what to test, how, what it proves) and the
  manual-dispatch `unity-self-hosted-gates` workflow for a maintainer-controlled
  machine registered as a self-hosted runner.
- Iterated the governance rules by maintainer direction: added prototype-stage
  operating mandates (`docs/governance/mandates/`, schema-validated) under the
  active `A0` path, recorded the weekly steward mandate, let `AGENTS.md` state that
  a mandated Agent acts within scope without asking, and marked bounded background
  automation as activated in the project profile. RFC 0006 membership, review
  windows for constitutional changes, and all GitHub permissions are unchanged.
- Added the `unity-consumer-tests` workflow: materializes the reference consumer,
  audits the exact test-case count of every test assembly from source with the new
  `scripts/audit_unity_test_inventory.py`, runs each assembly in its own pinned
  game-ci Unity process, verifies every result with
  `scripts/verify_unity_test_results.py`, and uploads the NUnit XML. It requires a
  Unity license secret and skips on fork pull requests. The audit reproduces every
  previously recorded Editor count and is covered by its own contract tests.
- Added Inventory decision material: proposal 0001 for a closed Item Kind beside
  open Tags (#85), the migration note from the single-renderer layout to the
  presentation-split graph, and a clause-to-test coverage map for the Core behavior
  gate that names six missing tests.
- Fixed the XR Baseline far-cast-distance repair (#21) to resolve the referenced
  `ICurveInteractionCaster`, return diagnostics, and fail visibly; Editor execution
  of its three new tests is pending.
- Added the consumer lessons register (`docs/standards/lessons/`): seven lessons
  drawn from Inventory's real consumer use and validation records, each with an
  explicit adopted, gap, deferred, or not-applicable disposition for every live
  package family. Repository validation now fails when a live family has not
  responded to a lesson, a gap or deferral lacks a follow-up, an evidence path is
  missing, or the register drifts from its schema or policy.
- Added a `docs/README.md` navigation index with a directory map, task-based
  reading orders, and the evidence rules shared by every documentation surface.
- Expanded the XR Baseline README and the Settings Core, Settings Unity, and
  Persistence Core package documentation with lifecycle, outcome, recovery,
  migration, factory-wiring, and non-goal detail derived from the shipped source.
  No package API, version, maturity, or evidence claim changed.
- Updated the roadmap to reflect both released Foundry batches, the 15 live Unity
  packages, the Localization queue proposal, and the XFCM composition gates.
- Documented a project-local virtual environment as the supported route for the
  pinned contract dependencies and ignored `.venv/`, `venv/`, and
  `.pytest_cache/`.
- Added proposed RFC 0006 and an inactive Agent-native membership foundation:
  `G0 x A0` current truth, `G0 x A1` phase-one target, AgentMember identity and
  mandate schemas, principal-and-lineage independence, evidence ancestry controls,
  a pinned Research-Lite manifest, and fail-closed authority boundaries. No Agent
  registry, account, GitHub App, wallet, token, chain, MCP service, or remote setting
  was activated.
- Added XFCM v0.1: RFC 0005, component/capability/composition schemas, colocated
  manifests for all 15 Unity packages, explicit renderer/XR variant slots, a
  deterministic 13-component Unity reference lock, consumer-owned lifecycle rules,
  visible pending cross-family adapters, and fail-closed composition validation.
- Kept the runtime data plane strongly typed and in process; MCP is reserved for a
  future optional external control-plane adapter. No whole-composition Unity,
  runtime, headset, or named-device evidence is claimed by structural resolution.
- Added the proposed DAO-ready XR Open Commons foundation: RFC 0004, a G0-G4
  governance maturity model, 7-day policy and 14-day constitutional review windows,
  emergency review bounds, a governance proposal form, source manifest, and
  machine checks that keep activation, automatic promotion, tokens, wallets,
  treasury, on-chain execution, organization transfer, and remote settings off.
- Hardened the repository foundation with a manually runnable Python 3.11-3.13
  validation matrix, dependency-aware pip caching, one canonical full contract
  command, a stable branch-protection aggregate check, monthly Python dependency
  updates, and validator tests that reject CI or Dependabot drift.
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
