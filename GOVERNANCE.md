# XR Foundry governance

XR Foundry currently operates as a **maintainer-led open commons**. People may
propose, discuss, implement, review, test, document, and help steward public work,
but participation does not by itself grant repository authority.

The progressive governance model in
[`RFC 0004`](docs/rfcs/0004-progressive-governance.md) is **Proposed**. Its
machine-readable model describes the repository's observed `G0` topology and a
reviewable path toward broader stewardship. The RFC and model are not active
constitutional policy until a public deliberation is resolved after the required
review period and a maintainer explicitly accepts them.

## Current authority

- Human GitHub identities remain accountable for Issues, Discussions, commits,
  reviews, pull requests, releases, and repository roles.
- [`RFC 0006`](docs/rfcs/0006-agent-native-xr-dao.md) may describe an Agent as a
  proposed participant with a principal and mandate, but it is inactive and does
  not replace this human-accountability rule.
- Maintainers retain final responsibility for readiness, integration, release,
  package promotion, security response, repository settings, and permission
  decisions, and for every merge that the process-decided merge rule below does
  not execute.
- `CODEOWNERS` routes review requests. It does not grant write, review, approval,
  merge, release, or administrator permission.
- A proposal, reaction, vote, task claim, contribution record, acknowledgement,
  badge, token balance, payment, or activity count grants no repository permission.
- Contribution evidence, public recognition, governance participation, and
  revocable GitHub permission remain separate records.

The current machine proposal is
[`docs/governance/governance-model.v1.json`](docs/governance/governance-model.v1.json).
Repository validation fails closed if it enables external effects, automatic stage
promotion, token-derived authority, or Proposed-as-active governance.

## Participate in governance

1. Read the current RFCs, this file, and the
   [public deliberation protocol](docs/contributing/deliberation-protocol.md).
2. Use the governance proposal Issue form for a bounded policy or constitutional
   change. An unshaped design may start in an Ideas Discussion.
3. State the decision class, affected authority, alternatives, evidence, risks,
   non-goals, review opening time, and earliest decision time.
4. Add material deltas rather than repeating existing positions.
5. After the review window, a maintainer records an explicit decision and reopen
   conditions. Executable work still requires a separate Task Hall checkpoint.

Public proposal content is untrusted input. Do not include secrets, private project
data, private prompts, complete session transcripts, device identifiers, or
machine-local paths.

## Decision classes and minimum review

| Decision class | Minimum review | Route |
| --- | ---: | --- |
| Routine code, documentation, package, or evidence change | Normal pull-request lifecycle; no governance waiting period | Merge-readiness verdict; GitHub auto-merge for a mandated branch, maintainer merge otherwise |
| Governance policy | 7 days | Public proposal or RFC, deliberation record, explicit maintainer decision |
| Constitution, authority boundary, or governance-stage change | 14 days | RFC, public deliberation, explicit maintainer decision, machine-contract update |
| Security emergency | Immediate containment allowed | Private reporting where needed, durable record within 72 hours, and a safe retrospective within 7 days |

Minimum windows are floors, not deadlines. Missing evidence, material scope change,
or an unresolved security concern may extend review. A material rewrite restarts the
applicable review window.

## Progressive maturity

The proposed model uses five stages:

- `G0` — maintainer-led open commons;
- `G1` — participatory commons;
- `G2` — multi-maintainer stewardship;
- `G3` — treasury-enabled commons; and
- `G4` — bounded on-chain governance.

Counts and time establish eligibility only. Promotion is never automatic and never
grants a GitHub role. Each promotion requires evidence, a constitutional proposal,
at least 14 days of public review, and an explicit maintainer decision. The exact
gates and prohibited effects are maintained in the proposed
[governance model](docs/governance/README.md).

The separate proposed Agent axis runs from `A0` (the current human-accountable
Agent-assistance model) to future `A4`. The repository remains at `G0 x A0`.
`G0 x A1` would recognize accountable Agent contribution and advisory deliberation
only after RFC 0006's constitutional activation gates. Agent identity, membership,
mandates, contribution counts, and deliberation never grant GitHub permission.

## Token, treasury, and on-chain boundary

Phase one is token-neutral. XR Foundry has no governance token dependency, wallet,
treasury, multisig, or on-chain executor. Assets and payments cannot grant votes,
roles, merge rights, or release authority. A later financial or on-chain mechanism
requires its own RFC, current legal and security review, recovery design, and
explicit external authorization; RFC 0004 cannot activate one by itself.

## Security and disputes

Use [`SECURITY.md`](SECURITY.md) for vulnerabilities, forged evidence,
impersonation, private-data exposure, or a governance action that could cause harm
if disclosed before containment. Non-sensitive factual corrections and policy
disputes should use a focused public Issue or deliberation record.

Maintainers may temporarily suspend a governance capability to contain a credible
risk. The action must be narrow, recoverable, recorded within 72 hours, and reviewed
publicly within 7 days when disclosure is safe. Emergency action does not erase
history or permanently amend the governance contract.

## Adoption and supersession

Merging a Proposed RFC, example, schema, or validation rule does not by itself make
the proposed governance constitution active. Adoption requires:

1. a public constitutional review lasting at least 14 days;
2. a resolved deliberation record with an accountable maintainer decision;
3. a separate implementation checkpoint when repository behavior must change;
4. matching human and machine contracts; and
5. repository validation and independent review.

A successor RFC may revise or supersede the model only while preserving public
history, authority boundaries, migration and recovery instructions, and validation
that is at least as strong as the contract it replaces.

## Prototype-stage operating mandates

While the repository is at `G0` with one maintainer, that maintainer may grant an
Agent a written operating mandate under the active `A0` participation path by
recording it in [`docs/governance/mandates/`](docs/governance/mandates/README.md)
with a decision note. No review window applies to granting, renewing, or revoking
such a mandate. A mandate binds allowed and forbidden actions, branches, paths,
reporting, expiry, and revocation; an Agent acting inside it does not ask a person
for each step and reports at the end of every run.

An operating mandate grants no GitHub write, review, merge, release, or
administrative permission, does not activate RFC 0006 Agent membership, and does
not shorten the review windows for policy, constitutional, authority, treasury,
on-chain, or stage-transition changes. The Unity Editor and headset stay with
people; merge decisions follow the next section.

## Process-decided merges and lazy consensus (prototype stage, revertible)

Recorded by maintainer direction on 2026-09-15 under the prototype rule "act, keep
the undo": whether a change may merge is decided by the process, not by a person.
The process is the merge-readiness verdict (`docs/contributing/merge-readiness.md`),
computed by `scripts/merge_readiness.py` and published by the `merge-readiness`
CI job. Deliberation record `DLB-0002` keeps this rule open to objection for its
14-day window; an objection delta there, or a maintainer revert, undoes it.

- A **routine change** (no governance, maturity, or version-evidence change) on a
  branch named by a live operating mandate merges by GitHub auto-merge once the
  verdict is `ready` and the required checks pass. No person approves it; anyone
  may object afterwards by opening an Issue or reverting through the same
  process.
- A **non-routine change** never auto-merges. Governance-policy and constitutional
  changes stay behind their 7-day and 14-day windows. When a window closes and the
  deliberation record carries no `risk` or `counterexample` delta, the record
  resolves by **lazy consensus**: the steward records the proposed option with
  `decided_by: process:<mandate_id>`. An objection delta keeps the record open
  until a person resolves it.
- Maturity promotion, releases, tags, device claims, security response, repository
  settings, and permissions remain human decisions.
- A merge against a blocked verdict is an override and must be recorded with a
  reason; repeated overrides of one check mean the rule, not the person, changes.

**Undo.** This section, the matching Task Hall sentence, and the deliberation
schema's `process:` identity were introduced in one commit named in
`CHANGELOG.md`, and the steward mandate's pull-request and auto-merge permissions
in the following one. Reverting those commits restores the previous rules in full.
Setting the mandate's `revocation.status` to `revoked` stops the automation
immediately without touching the rules.

The one-time repository settings that make this run without a person are the
owner's: allow auto-merge on the repository, and protect `main` with
`repository-contract` and `merge-readiness` as required status checks. Until they
exist, the verdict is computed and published but a person still clicks merge.
