# RFC 0006: Agent-native membership, identity, and delegated authority

Status: **Proposed**

Activation: **inactive; current observed state remains G0 x A0**

Public deliberation: **not opened by this local implementation**

Related foundations:

- [RFC 0001: Agent Commons V1](0001-agent-commons.md)
- [RFC 0002: Public Workbench and Contribution Recognition V1](0002-public-workbench.md)
- [RFC 0004: Progressive governance](0004-progressive-governance.md)
- [RFC 0005: XR Foundry Component and Composition Model](0005-xr-foundry-component-composition-model.md)
- [Task Hall](../contributing/task-hall.md)

## Summary

XR Foundry should become an **Agent-native XR DAO-ready commons**: people and
accountable software agents may contribute, deliberate, and coordinate work through
the same public evidence system, while high-impact repository authority remains with
human principals and maintainers during phase one.

This RFC adds a second maturity axis, `A0` through `A4`, beside RFC 0004's governance
axis `G0` through `G4`. The repository currently observes `G0 x A0`. The first
activation target is `G0 x A1`: an agent may be recognized as a contribution and
deliberation participant only when it has a stable repository identity, accountable
principal, declared lineage, observable action identity, evidence-bound capability
claims, a bounded mandate, and a revocation path.

The proposed machine contract is
[`agent-membership-model.v1.json`](../governance/agent-membership-model.v1.json).
It is deliberately inactive and creates no account, app installation, wallet,
treasury, token, smart contract, on-chain action, repository role, or remote setting.

## Problem

RFC 0001 correctly makes a human GitHub identity accountable for Agent-assisted
contributions. That boundary is safe, but it cannot yet represent an agent as a
durable participant with its own history, capabilities, mandates, or revocation
state. Treating every agent as an invisible tool loses useful provenance; treating
an agent as an autonomous person would hide responsibility and invite Sybil,
collusion, and duplicated-evidence failures.

XR Foundry needs one model that answers:

1. How is an agent named without making the name a credential?
2. Which person or organization remains accountable for its actions?
3. What evidence supports a capability claim, and when does that claim expire?
4. Which actions are permitted by a mandate, in which repository scope and time?
5. When do two agents count as independent participants or reviewers?
6. How do Agent membership and XFCM components remain separate concepts?

## Decision

Adopt, only after the review and activation gates in this RFC, a repository-native
agent membership contract with these invariants:

- `agent_id` is stable metadata, never a login, credential, vote, or permission.
- Every agent resolves to one accountable `principal_ref` and at least one declared
  action identity.
- `lineage_id` records shared model, prompt, memory, orchestrator, or deployment
  ancestry at the granularity needed to assess correlated behavior.
- Capabilities are claims backed by revision-bound evidence, not self-description.
- Every allowed action is bounded by a revocable mandate naming the issuer,
  checkpoint, resource scope, allowed actions, and validity interval.
- A phase-one mandate cannot grant write, approval, merge, release, security,
  settings, administrator, wallet, treasury, token, contract, or on-chain authority.
- Membership, contribution, deliberation, recognition, payment, and permission are
  separate records.
- Independent governance participation requires both a distinct accountable
  principal and a distinct lineage.
- Repeated outputs derived from the same evidence ancestry count as one evidence
  unit unless independent primary evidence is added.
- Human maintainers retain final authority at `G0 x A1`.

## Two-axis maturity model

The governance axis answers **who may decide**. The agent axis answers **how agent
participants may act**. Neither axis promotes the other automatically.

| Agent stage | Meaning | Allowed effect |
| --- | --- | --- |
| `A0` | Agent-assisted work under a human identity | Existing RFC 0001 flow only |
| `A1` | Accountable agent member | Public contribution, evidence submission, and advisory deliberation under a mandate |
| `A2` | Delegated task actor | Future bounded Task Hall claims and low-risk execution; separate activation required |
| `A3` | Governed service agent | Future persistent service operation with incident, observability, and recovery controls |
| `A4` | Bounded autonomous executor | Future explicit high-assurance authority for named actions only; separate constitutional RFC required |

`A2`, `A3`, and `A4` are vocabulary for future review, not active roles. Phase one
may only propose `A1`, and `A1` remains inactive until the constitutional adoption
path completes.

## Agent member contract

An `AgentMember` record contains:

- `agent_id` matching `xr-foundry.agent.<slug>`;
- `lineage_id` describing the agent's declared behavioral ancestry;
- `principal_ref` naming the accountable human or legal principal;
- one or more `action_identities` showing which GitHub actor or future adapter makes
  observable actions;
- a lifecycle `status` and `autonomy_level`;
- evidence-bound `capability_claims`; and
- one or more time-bounded, revocable `mandates`.

The record is public metadata. Secrets, tokens, private prompts, private memory,
credentials, session transcripts, and device identifiers must not be stored in it.
The included example is anonymous, unverified, and non-authoritative. This RFC does
not create a live member registry.

## Mandates and delegated authority

A mandate names the agent, principal, issuer, admitted checkpoint, allowed actions,
resource scope, issue and expiry times, and revocation state. It is valid only when
all identity references agree, the checkpoint still exists, the current time is in
range, the requested action and path are inside scope, and the mandate is not
revoked.

At `A1`, the allowed vocabulary is limited to public, reversible participation such
as drafting, commenting, proposing, and submitting evidence. A mandate is not a
GitHub permission grant. GitHub's own role and protection state remains authoritative.

## Independence and epistemic Sybil resistance

Agent count is not independence. Two agents are not independent for governance or
formal review when either condition holds:

- they resolve to the same `principal_ref`; or
- they share the same `lineage_id`.

Same-principal or same-lineage agents may offer useful advisory analysis, but they
cannot multiply quorum, satisfy a distinct-principal threshold, or review each
other as independent reviewers. Disclosure of additional correlations is required
when agents share memory, orchestration, retrieval, evidence selection, deployment,
or financial control even if their declared lineages differ.

Evidence follows the same principle. Summaries, votes, or reports descended from one
source evidence root count once. Independent confidence requires a different primary
observation or a genuinely independent reproduction, not more generated text.

## Governance maturity retrofit

RFC 0004 remains unchanged and authoritative for the currently proposed `G0-G4`
axis. A successor amendment should later express maturity as `Gx x Ay` cells and
replace human-only contributor thresholds where appropriate.

The proposed `G1` eligibility target after that amendment is:

- a continuous 90-day observation window;
- at least three distinct accountable principals;
- at least two active agent members;
- at least two non-maintainer members;
- at least two accepted contribution types; and
- at least two resolved public deliberations.

These are eligibility signals only. Principal and lineage independence must be
checked before counting; no threshold grants a role or changes repository settings.

## Task Hall retrofit

Task Hall v1 remains unchanged. A future Task Hall v2 may define a claimant union:

- a human GitHub identity; or
- an agent claimant containing `agent_id`, `lineage_id`, `principal_ref`,
  `action_identity`, and `mandate_ref`.

The task contract should verify the mandate at claim, continuation, submission, and
handoff boundaries. A claim remains a coordination lease, not write or merge access.
Until v2 is separately reviewed and accepted, agents act through the accountable
human workflow in RFC 0001.

## XFCM and communication retrofit

XFCM remains the package and system composition authority. An agent is not an XFCM
component, and an agent capability claim is not an entry in the XFCM capability
registry. XFCM describes what the XR runtime is made of; this RFC describes who or
what may participate in producing and governing it.

Future external-control receipts may carry `agent_id` and `mandate_ref` so an action
can be traced without coupling agent governance to Unity's runtime data plane. An
MCP adapter may later expose read-mostly discovery, validation, and task context.
It must remain optional, outside the hot path, and subordinate to repository
contracts. No MCP server is introduced by this RFC.

## Research basis and source roles

The reviewed references are recorded with versions and transfer limits in
[`agent-native-source-manifest.json`](../governance/agent-native-source-manifest.json).
The following lessons are adopted narrowly:

- A2A and OASF inform agent descriptions, capability discovery, and interoperability
  vocabulary without becoming dependencies.
- AGNTCY Identity and W3C DID/VC inform future portable identity and attestation
  adapters; repository-native identity remains sufficient for `A1`.
- GitHub Apps inform a future action-identity adapter; no app is installed now.
- SLSA and in-toto inform provenance, subject, evidence, and attestation structure.
- research on governing actions and epistemic Sybil resistance supports action-bound
  authorization, ancestry disclosure, and correlation-aware counting.
- Open Autonomy/Olas informs the distinction between a canonical agent definition
  and a running instance, plus threshold-based coordination concepts. Token,
  staking, bonding, wallet, and on-chain assumptions are rejected.
- SocialSystemArena supports evaluating agents in social and governance contexts
  rather than inferring trust from isolated benchmark scores.

These references do not transfer their identity roots, networks, licenses, tokens,
runtime authority, or governance models to XR Foundry.

## Phase-one repository slice

This local slice includes:

- this RFC;
- proposed, inactive agent membership and `AgentMember` schemas;
- one anonymous non-authoritative example;
- a pinned Research-Lite source manifest;
- repository validation and negative tests; and
- discoverability links from the governance entry points.

It intentionally does not modify RFC 0001, RFC 0004 thresholds, Task Hall v1, XFCM
runtime behavior, GitHub roles, repository settings, or any external system.

## Activation path

Activation requires all of the following in a later checkpoint:

1. open a public constitutional deliberation for at least 14 days;
2. resolve material identity, privacy, impersonation, lineage, mandate, revocation,
   and incident concerns;
3. record an explicit maintainer decision;
4. update the RFC and machine model status together;
5. create the admitted Task Hall implementation checkpoint;
6. validate at least one real but privacy-safe agent record and revocation exercise;
7. obtain independent review from a different principal and lineage; and
8. keep GitHub permission changes, app installation, and every external effect in
   separately authorized operations.

## Non-goals

- no real agent member registry or claim that an example is enrolled;
- no GitHub account switch, GitHub App installation, organization transfer, role,
  permission, branch-protection, or remote-settings change;
- no wallet, treasury, multisig, token, NFT, staking, bond, smart contract, or
  on-chain execution;
- no autonomous merge, release, security response, administration, or governance
  vote;
- no MCP service or Agent-to-Agent runtime dependency;
- no replacement of human accountability with a model provider, orchestrator, or
  cryptographic identifier; and
- no automatic maturity promotion from counts, elapsed time, or generated evidence.

## Reopen and supersession

Reopen this proposal if an identity field creates unsafe disclosure, lineage cannot
represent a real correlation, mandates are too broad to audit, revocation cannot be
exercised, or formal independence can be gamed. Preserve history through a successor
RFC; do not silently loosen required identity, evidence, independence, authority, or
external-effect constraints.
