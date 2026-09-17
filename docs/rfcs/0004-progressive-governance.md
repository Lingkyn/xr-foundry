# RFC 0004: Progressive governance for the XR Foundry Open Commons

Status: **Proposed**

Public deliberation: **not opened by this local implementation**

Related foundations:

- [RFC 0001: Agent Commons V1](0001-agent-commons.md)
- [RFC 0002: Public Workbench and Contribution Recognition V1](0002-public-workbench.md)
- [RFC 0003: Foundry V1 production line](0003-foundry-production-line.md)
- [Public deliberation protocol](../contributing/deliberation-protocol.md)

## Summary

XR Foundry should grow as a **DAO-ready XR Open Commons**: a public, durable,
evidence-backed infrastructure project that can broaden stewardship without making
tokens, wallets, a second database, or on-chain voting prerequisites for useful
participation.

This RFC proposes a five-stage governance maturity model. The repository is
observably at `G0`, a maintainer-led open commons. Later stages add participation,
maintainer redundancy, financial stewardship, and only then bounded on-chain
execution. Stage thresholds establish eligibility; they never create an automatic
promotion or a repository role.

The proposed machine contract is
[`docs/governance/governance-model.v1.json`](../governance/governance-model.v1.json).
It remains inactive while this RFC is Proposed.

## Problem

The repository already defines strong public contribution, evidence, continuity,
recognition, security, and production gates. It does not yet provide one clear answer
to four governance questions:

1. What can a public participant decide today?
2. What evidence would justify broader stewardship later?
3. Which events may never grant repository authority automatically?
4. How can future treasury or on-chain mechanisms be evaluated without making them
   an assumed destination?

Without a shared contract, "DAO" can ambiguously mean open contribution, token
voting, treasury management, organization ownership, or full autonomous execution.
That ambiguity risks premature financial infrastructure and could weaken the
existing separation between contribution evidence and revocable GitHub permission.

## Decision

Adopt, only after the constitutional review and decision gates below, a progressive
governance model with these invariants:

- GitHub identity and GitHub's permission ledger remain the repository authority
  boundary.
- Public deliberation is advisory until an accountable maintainer records a bounded
  decision.
- Contribution evidence, acknowledgement, governance participation, financial
  support, and repository permission remain separate records.
- Counts and time are eligibility evidence only; they cannot trigger promotion.
- Every governance-stage change is constitutional and receives at least 14 days of
  public review.
- Token holdings, payments, credentials, task claims, reactions, or Agent output
  never grant votes, roles, merge rights, release authority, or administrator access.
- Treasury and on-chain mechanisms require independent RFCs and cannot be activated
  by this document.

## Decision classes

### Routine repository change

Ordinary code, documentation, package, test, or evidence work follows the existing
Task Hall and pull-request lifecycle. It has no governance waiting period unless it
also changes policy, authority, or a maturity stage.

### Governance policy

A policy change receives at least 7 calendar days of public review. Its record names
the problem, options, evidence, risks, affected authority, review opening time,
earliest decision time, decision, approver, and reopen conditions.

### Constitutional change

A change to authority, role semantics, governance stages, promotion gates,
token/treasury boundaries, or this RFC receives at least 14 calendar days of public
review. A material rewrite restarts the window. Adoption requires a resolved
deliberation, an explicit maintainer decision, matching human and machine contracts,
and a separate executable checkpoint when behavior changes.

### Security emergency

A maintainer may contain an imminent security, privacy, impersonation, financial,
or permission risk immediately. The narrow action is recorded within 72 hours and a
safe public retrospective is opened within 7 days. Disclosure may be delayed only
to protect affected people or the fix. Emergency action cannot permanently amend
the constitution or erase history.

## Governance maturity

### G0 — Maintainer-led open commons

This is the current topology. The maintainer makes final repository, security,
release, promotion, settings, and permission decisions. Public participants propose
and contribute through existing evidence-bound routes. The repository has no
governance wallet, treasury, token dependency, or on-chain executor.

### G1 — Participatory commons

Eligibility requires all of the following within a continuous 90-day observation
window:

- at least three distinct accountable human contributors;
- at least two are non-maintainers;
- accepted contribution evidence covers at least two contribution types; and
- at least two public deliberations reach a resolved decision.

G1 may charter advisory working groups and regular public governance review. It
does not grant organization membership, repository permission, or a binding public
vote.

### G2 — Multi-maintainer stewardship

Eligibility requires:

- at least two independent, active maintainers;
- at least one tested backup release or recovery role;
- at least three resolved governance decisions across 180 days;
- one successful release, security, or recovery exercise; and
- documented responsibility, succession, recusal, revocation, and incident paths.

Role changes remain explicit, scoped, revocable GitHub decisions. After G2
eligibility is accepted, a separate change may reconsider the sole-maintainer
required-approval and Code Owner rules without weakening status checks, thread
resolution, deletion, or force-push protection.

### G3 — Treasury-enabled commons

Eligibility requires:

- a demonstrated recurring funding or shared-cost need;
- at least three independent treasury stewards or signers;
- current legal, tax, accounting, security, privacy, and jurisdiction review;
- a public budget, spending, conflict, recusal, reporting, and audit policy;
- signer loss, compromise, emergency stop, and recovery procedures; and
- a separate accepted treasury RFC.

G3 eligibility is not authorization to open a wallet, create a multisig, collect
funds, issue an asset, or spend money. Those are external operations with separate
approval and execution evidence.

### G4 — Bounded on-chain governance

Eligibility requires:

- a specific decision class for which off-chain governance has proven inadequate;
- a separate accepted on-chain RFC;
- a threat model, independent audit, and publicly reviewable implementation;
- at least 30 days of testnet exercise with failure and recovery evidence;
- quorum, delegation, timelock, upgrade, guardian, emergency, and exit rules; and
- a safe off-chain recovery path.

On-chain execution remains bounded to the capability explicitly admitted by its
RFC. It must not automatically control GitHub merge, release, security response,
administrator, or organization-owner permission.

## Promotion, suspension, and rollback

Promotion uses a mixed evidence gate:

1. Numeric and time thresholds establish minimum eligibility.
2. Qualitative evidence demonstrates judgment, reliability, safety, collaboration,
   recovery, and current project need.
3. A constitutional RFC and deliberation receive at least 14 days of review.
4. A maintainer explicitly accepts, rejects, or defers the transition.
5. Human documentation, machine contracts, repository settings, and recovery
   instructions change only through separate reviewed checkpoints.

No metric automatically causes a transition. A stage may be suspended or rolled
back when its assumptions, staffing, security, legal basis, or recovery capability
fail. Emergency suspension follows the security-emergency record and retrospective
rules; permanent rollback follows a constitutional decision.

## Roles and authority

Phase one recognizes three operational responsibilities:

- **public contributor** — may propose and contribute evidence-bound work; advisory
  only;
- **maintainer** — holds current integration, release, security, settings, and
  permission responsibility through GitHub; and
- **security responder** — a maintainer acting within the private-reporting and
  immediate-containment boundary.

Future working-group, steward, treasury, signer, delegate, guardian, or on-chain
executor roles do not exist merely because this RFC names them. Each needs the
applicable stage, accepted charter or RFC, explicit assignment, least privilege,
revocation, and recovery.

## Deliberation and execution

This RFC reuses the existing deliberation record. Governance records add four
optional, all-or-none fields:

- `decision_class`;
- `governance_stage`;
- `review_opened_at`; and
- `review_not_before`.

The validator checks the 7- or 14-day minimum and rejects a resolved decision made
before `review_not_before`. The security-emergency class may decide immediately but
retains the separate 72-hour record and 7-day retrospective obligation.

A resolved governance decision is still not executable authority. Repository work
begins only through a separate admitted Task Hall checkpoint, and permission changes
remain explicit GitHub operations.

## Token-neutral phase one

Phase one has no governance token, voting asset, wallet, treasury, multisig, smart
contract, or on-chain executor. The governance model forbids any mapping from token
balance, financial contribution, contribution volume, or public credit to voting or
repository rights.

"Token-neutral" does not promise that XR Foundry will never use a financial or
on-chain tool. It means later adoption must prove a concrete need and pass its own
legal, safety, threat-model, recovery, governance, and explicit-authorization gates.

## Source basis and inference boundary

The source-role record is
[`docs/governance/source-manifest.json`](../governance/source-manifest.json).
Project RFCs and the deliberation contract provide the governance basis. GitHub
documentation provides platform and permission semantics. Kubernetes provides a
bounded responsibility-ladder precedent without transferring its roles.

[Folo](https://github.com/RSSNext/Folo/tree/465b997e89bde007fcac32257baec6a2ded73164)
is classified as a mature open-product
community growth, distribution, and release-operations case. It supports a possible
future community adaptation/distribution registry and contributor-facing release
practice. It is not a DAO governance precedent; its wallet or asset features,
license, branding, social channels, and release topology are not adopted.

The G0-G4 model and thresholds are XR Foundry design decisions. They are not claims
that any cited project uses this exact governance system.

## Phase-one adoption path

The repository-local implementation may land while this RFC remains Proposed. That
slice includes the human entry points, machine model and Schema, source manifest,
deliberation metadata, proposal form, validation, and tests. It performs no external
account, organization, wallet, treasury, token, contract, on-chain, push, PR, or
remote-settings operation.

Activation requires:

1. open a public constitutional deliberation;
2. hold review for at least 14 days;
3. resolve the record with an explicit maintainer decision;
4. if accepted, update the RFC and model status together through a separate Task
   Hall checkpoint; and
5. rerun repository validation and independent review.

## Non-goals

- no GitHub organization migration or account switch;
- no wallet, treasury, multisig, token, NFT, contract, or on-chain deployment;
- no binding reaction, poll, contribution-count, payment, or token vote;
- no automatic role or stage promotion;
- no governance bot or comment-trigger execution;
- no duplicate proposal, voting, reputation, or permission database;
- no empty community-adoption registry before real phase-two evidence exists; and
- no change to the authority of work already admitted under earlier contracts.

## Reopen and supersession

Reopen this decision if the model blocks legitimate low-risk participation, a
security or legal assumption changes, the maintainer topology changes, or public
governance exercises show that a threshold is unsafe or unhelpful.

Do not rewrite an accepted decision in place. Preserve history and create a
successor deliberation and RFC revision. A successor must state migration, affected
roles and work, recovery, evidence, and why its validation is at least as strong.
