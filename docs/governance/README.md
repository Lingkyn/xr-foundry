# Progressive governance model

Status: **Proposed phase-one repository foundation**

This directory holds the machine-readable companion to
[`RFC 0004`](../rfcs/0004-progressive-governance.md). The human entry point is
[`GOVERNANCE.md`](../../GOVERNANCE.md). Proposal, synthesis, decision, and
execution handoff continue to use the existing
[public deliberation protocol](../contributing/deliberation-protocol.md); this
directory does not create a parallel voting or proposal system.

## Authority map

| Surface | Role |
| --- | --- |
| [`GOVERNANCE.md`](../../GOVERNANCE.md) | Human-facing current authority and participation entry |
| [`RFC 0004`](../rfcs/0004-progressive-governance.md) | Proposed constitutional decision and maturity rationale |
| [`governance-model.v1.json`](governance-model.v1.json) | Machine-readable proposed policy and observed current stage |
| [`governance-model.schema.json`](governance-model.schema.json) | Structural and fail-closed constants for the model |
| [`source-manifest.json`](source-manifest.json) | Source roles, adopted lessons, limits, and rejected assumptions |
| [`RFC 0006`](../rfcs/0006-agent-native-xr-dao.md) | Proposed Agent-native membership, identity, mandate, and independence design |
| [`agent-membership-model.v1.json`](agent-membership-model.v1.json) | Proposed `G0 x A1` agent-participation policy; inactive by construction |
| [`agent-membership-model.schema.json`](agent-membership-model.schema.json) | Fail-closed stage, authority, independence, evidence, and external-effect constants |
| [`agent-member.schema.json`](agent-member.schema.json) | Public `AgentMember` identity, capability-evidence, and mandate contract |
| [`agent-member.example.json`](agent-member.example.json) | Anonymous, unverified, non-authoritative example; not a member registry |
| [`agent-native-source-manifest.json`](agent-native-source-manifest.json) | Research-Lite references, adopted lessons, and transfer limits |
| [Deliberation record](../contributing/deliberation-record.schema.json) | Existing proposal-to-decision record with optional governance metadata |

The JSON model has `status=proposed` and `activation.active_policy=false`. Its
`current_stage=G0` records the repository's observable topology; it does not mean
the proposed maturity constitution has activated itself.

## Agent-native maturity

[`RFC 0006`](../rfcs/0006-agent-native-xr-dao.md) proposes an independent
`A0-A4` Agent maturity axis. The observed state remains `G0 x A0`; `G0 x A1`
is only a proposed first target. At `A1`, an Agent would need a stable
`agent_id`, declared lineage, accountable principal, observable action identity,
evidence-bound capability claims, and a bounded revocable mandate.

The Agent contract does not create a live registry or a repository role. Same-
principal or same-lineage Agents cannot count as independent governance actors or
formal reviewers, and evidence with the same ancestry cannot be multiplied by
generating additional reports. Human maintainers retain final authority.

## Maturity stages

### G0 — Maintainer-led open commons

The maintainer owns final repository, release, security, permission, and promotion
decisions. Anyone may contribute through public, evidence-bound routes. No proposal,
contribution, token, payment, or count grants authority.

### G1 — Participatory commons

Eligibility requires a 90-day observation window with at least three distinct human
contributors, including at least two non-maintainers, accepted evidence across at
least two contribution types, and at least two resolved public deliberations.
Eligible working groups remain advisory and receive no automatic GitHub permission.

### G2 — Multi-maintainer stewardship

Eligibility requires at least two independent active maintainers, one tested backup
release or recovery role, at least three resolved governance decisions across 180
days, and a successful release/recovery exercise. Role grants remain explicit,
scoped, revocable GitHub decisions. This is the earliest stage at which approval
counts, Code Owner enforcement, succession, or organization topology should be
reconsidered.

### G3 — Treasury-enabled commons

Eligibility additionally requires a demonstrated recurring funding need, at least
three independent treasury stewards or signers, current legal and tax review, a
public spending/accounting policy, incident and signer-recovery procedures, and a
separate accepted RFC. Eligibility does not create a wallet or authorize spending.

### G4 — Bounded on-chain governance

Eligibility additionally requires evidence that a concrete decision cannot be
handled safely off-chain, a separate accepted RFC, threat model and independent
audit, at least 30 days of public testnet exercise, timelock and emergency/recovery
design, and an exit path. An on-chain decision must never automatically control
GitHub merge, release, security, or administrator permissions.

## Promotion and rollback

- Numeric thresholds are minimum eligibility, not a score or automatic transition.
- Every promotion is a constitutional change with at least 14 days of review.
- A maintainer records the decision, evidence, dissent, migration, and reopen
  conditions in the public deliberation system.
- A stage may be suspended or rolled back when its assumptions or safety evidence
  fail. Emergency suspension follows the 72-hour record and 7-day retrospective
  boundary.
- A later stage cannot inherit evidence outside the scope recorded by an earlier
  decision.

## Phase-one repository checklist

- [x] Human governance entry and RFC 0004 draft.
- [x] Versioned model, JSON Schema, and source-role manifest.
- [x] G0-G4 eligibility, promotion, rollback, and prohibited-effect boundaries.
- [x] Token-neutral policy and explicit external-effect shutdown.
- [x] Governance metadata added to the existing deliberation contract.
- [x] Governance proposal Issue form added without changing remote settings.
- [x] README, contribution, playbook, security, and CODEOWNERS integration.
- [x] Repository validator and negative contract tests.
- [x] Proposed RFC 0006, AgentMember schema, Agent maturity model, Research-Lite
  manifest, and fail-closed Agent authority tests.
- [ ] Public 14-day constitutional review.
- [ ] Resolved deliberation and explicit maintainer adoption decision.

The unchecked items are adoption gates, not unfinished local implementation.

RFC 0006 also leaves its public review and adoption gates unchecked. Task Hall v2,
the RFC 0004 two-axis amendment, and optional XFCM external-control receipt fields
are future checkpoints, not phase-one runtime changes.

## Deferred phase-two mechanism

A later G1/G2 checkpoint may introduce a community adaptation, distribution, and
adoption registry inspired in part by mature open-product operations such as Folo.
Its trigger is real independent community distribution or adaptation that needs a
durable evidence path. Phase one deliberately avoids an empty registry, release
branch expansion, wallet integration, or social-channel authority.
