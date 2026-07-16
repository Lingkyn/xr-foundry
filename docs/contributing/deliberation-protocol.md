# Public deliberation protocol V1

Status: **Proposed pre-stable contract**

This protocol lets people and Agent-assisted contributors improve packages or the
collaboration mechanism without making the first proposal binding. It separates
discussion, synthesis, decision, and execution so a later contributor can recover
current state from public records rather than private chat.

The source pattern is deliberately limited. Kubernetes Enhancement Proposals use
structured, searchable proposals with explicit status, reviewers, approvers, and
implementation readiness; the process itself can evolve through proposals. XR
Foundry adopts that public-decision shape, not Kubernetes roles or authority. The
project's own [Discussion #44](https://github.com/Lingkyn/xr-foundry/discussions/44)
is non-binding source material. [RFC 0002](../rfcs/0002-public-workbench.md) defines
the repository-specific authority and self-improvement boundary.

## Four separate layers

1. **Deliberation** records a question, assumptions, options, evidence,
   counterexamples, risks, and material deltas. Publication order, verbosity,
   provider, or model identity grants no weight or authority.
2. **Synthesis** summarizes agreements, disagreements, and missing evidence. It is
   revisable and does not become a decision merely because one author wrote it.
3. **Current decision** records one bounded choice, rationale, approver, decision
   time, scope, and reopen conditions. `open` records cannot contain a decision.
4. **Execution and verification** begins only when the record is `resolved`, its
   execution state is `ready`, and a separate Task Hall checkpoint grants bounded
   execution authority. The deliberation record never grants repository access.

## Stable kernel, experiments, and adapters

- The **stable kernel** is the currently adopted Task Hall authority, security,
  lifecycle, durability, and evidence contract.
- An **experiment** tests one mechanism hypothesis in bounded files and fixtures.
  It cannot alter running-task authority or silently change the kernel.
- A **tool adapter** translates the public contract for a provider or runtime. It
  cannot become required private resume state or a second authority.

A proposal to change the collaboration mechanism uses the mechanism itself:
Discussion or RFC, one claimable checkpoint, isolated experiment, independent
review, explicit adoption criteria, and a versioned kernel change. Existing tasks
remain governed by their admitted version unless a maintainer records a migration.

## Delta-only participation

Before adding a contribution, read the current record. Add only a material delta:

- new public evidence;
- a counterexample or failed assumption;
- a genuinely different option;
- a newly identified risk or trade-off; or
- a correction to the synthesis or decision boundary.

Do not restate prior material to increase visibility. Do not score, rank, benchmark,
or permanently classify Agents or models. Evaluate the proposal and evidence.

The record contains concise, inspectable rationale. It must not require private
prompts, hidden chain-of-thought, credentials, private session logs, or verbatim
transcripts. Agent assistance may be disclosed as metadata while an accountable
GitHub identity owns the contribution.

## State transitions

| From | To | Required evidence |
| --- | --- | --- |
| `open` | `resolved` | Options and trade-offs are recorded; synthesis names remaining uncertainty; a maintainer records a bounded decision, reopen conditions, and a separate execution checkpoint. |
| `open` | `rejected` | The rejection rationale and evidence are public; `decision` stays null and execution is `not_ready` with no task. |
| `resolved` | `superseded` | A newer immutable decision record is linked; existing history is retained and execution becomes `superseded`, never `ready`. |
| `resolved` | `open` | Do not mutate history in place. Create a successor record when a reopen condition fires. |

The machine contract is
[`deliberation-record.schema.json`](deliberation-record.schema.json). The
[`open example`](deliberation-record.open.example.json) must remain non-executable;
the [`resolved example`](deliberation-record.resolved.example.json) demonstrates
the minimum decision and execution handoff without granting permission.
