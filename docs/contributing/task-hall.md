# Public Task Hall V1

The Task Hall is XR Foundry's shared public workbench for people and their coding
agents. It makes useful work discoverable without turning one large Issue into one
indivisible unit. For a registered workstream, the parent Issue is the umbrella
coordination projection, child Issues project checkpoints, and a versioned task
record owns the fine-grained checkpoint graph and lease state.

The Task Hall coordinates contribution. It does not grant repository authority.

Machine authority for registered workstreams:

- [`tasks/task-registry.json`](tasks/task-registry.json), validated against
  [`tasks/task-registry.schema.json`](tasks/task-registry.schema.json), lists the
  live task records covered by this contract;
- [`task-hall.v1.json`](task-hall.v1.json), validated against
  [`task-hall.v1.schema.json`](task-hall.v1.schema.json);
- [`task-contract.schema.json`](task-contract.schema.json), with an umbrella and
  checkpoint example in [`task-contract.example.json`](task-contract.example.json);
- [`work-continuation.schema.json`](work-continuation.schema.json), with a safe
  handoff example in [`work-continuation.example.json`](work-continuation.example.json).

The public Project is a discovery summary, not a second task database. A child
Issue carries human coordination and discussion; its registered task record is
the detailed execution authority. A one-checkpoint Issue not yet migrated into the
registry must declare one stable claim key in its body. Unregistered Issues do not
silently acquire a machine checkpoint record.

The public design thread is
[Discussion #22](https://github.com/Lingkyn/xr-foundry/discussions/22), and admitted
work is discoverable in the public
[XR Foundry Task Hall Project](https://github.com/users/Lingkyn/projects/2).

V1 is pre-stable. Its public contracts may be replaced while the workbench is being
established; consumers must validate the declared schema and version rather than
assuming compatibility with an earlier shape.

## Enter the workbench

| I want to contribute | Public queue |
| --- | --- |
| A guided first change | [`good first issue`](https://github.com/Lingkyn/xr-foundry/issues?q=is%3Aissue%20state%3Aopen%20label%3A%22good%20first%20issue%22) |
| Bounded work open to help | [`help wanted`](https://github.com/Lingkyn/xr-foundry/issues?q=is%3Aissue%20state%3Aopen%20label%3A%22help%20wanted%22) |
| Independent review | [`role:review`](https://github.com/Lingkyn/xr-foundry/issues?q=is%3Aissue%20state%3Aopen%20label%3A%22role%3Areview%22) |
| Hardware or user testing | [`device-lab`](https://github.com/Lingkyn/xr-foundry/issues?q=is%3Aissue%20state%3Aopen%20label%3A%22device-lab%22) |
| Research, implementation, integration, or documentation | [Live Task Hall Project](https://github.com/users/Lingkyn/projects/2) |

Start by reading the selected umbrella Issue and one Ready checkpoint. Verify its
dependencies, allowed paths, acceptance, evidence, and device/review boundary. The
accountable GitHub identity then requests `/claim CHECKPOINT-ID` (or the Issue's
declared claim key); after maintainer confirmation, publish a fork branch or draft
pull request and work only on that checkpoint. Code is not the only route: accepted research, review, documentation,
tests, device/user testing, design, triage, and infrastructure all have explicit
recognition categories.

## Umbrella tasks and checkpoint DAGs

An umbrella task preserves the shared objective, scope, non-goals, source gate,
integration strategy, and authority boundary. It is useful for orientation and
fan-in, but it is not the unit a contributor claims.

Each checkpoint declares exactly one bounded outcome and all of the information
needed to finish or safely hand it to another contributor:

- `depends_on`: checkpoint IDs that form a directed acyclic graph (DAG);
- `write_mode` and `allowed_paths`: either explicit read-only work with an empty
  path set, or the maximum bounded write surface;
- `non_goals` and `do_not_touch`: checkpoint-specific claims and surfaces that
  remain outside the lease;
- `source_gate`: checkpoint-specific admitted public sources, when required;
- `acceptance`: observable conditions for the outcome;
- `verification`: procedures, expected results, and evidence requirements;
- `device`: explicit device profiles and device-only acceptance when required;
- `evidence`: commit-bound source, implementation, test, review, or device receipts;
- `claim`: the checkpoint lease and whether the work is currently adoptable; and
- `salvage`: reusable partial output and its known constraints; and
- `execution_anchor`: the public branch or draft pull request plus base/head
  revisions that bound durable execution;
- `continuation`: the repository-relative handoff locator, digest, and revision, or
  an explicit stopped/missing state;
- `exact_next_action`: one unambiguous next operation for every non-completed
  checkpoint; and
- `completed_at`: the UTC closure time for a completed checkpoint.

Checkpoint IDs must be unique inside the umbrella task. Dependencies must name
existing checkpoints, cannot point to the checkpoint itself, and must be acyclic.
A checkpoint also states task-specific capability, tool, device, judgment, and
independent-review requirements. These route work by the checkpoint's needs and
evidence, never by a permanent Agent/model ranking. Self-reported capability does
not grant authority; if no qualified executor is available, the checkpoint remains
not Ready.

A checkpoint's acceptance, verification, and evidence IDs must be unique, and all
references to them must resolve inside that checkpoint or its continuation record.
A dependent checkpoint cannot become `ready` until every dependency is
`completed`. The integration checkpoint is an ordinary checkpoint designated as
the fan-in point; its owner does not gain merge authority.

If implementation discovers work outside the current outcome or allowed paths, it
creates a new checkpoint or task revision. It does not silently expand the active
claim. A completed checkpoint remains completed even if a sibling checkpoint is
blocked or a contributor runs out of time.

## Durable execution boundary

The checkpoint is also the smallest durable execution unit. Before changing a
checkpoint from `claimed` to `in_progress`, publish a GitHub execution anchor: a
linked public branch or draft pull request plus the checkpoint ID and base
revision. The same execution lane closes or hands off its current checkpoint before
taking another. Independent checkpoints may run in parallel only when dependencies
are complete, write/resource ownership is isolated, each lease and anchor is
separate, and an explicit fan-in checkpoint owns integration.

Agents must reserve enough session or tool budget to validate and publish the
current boundary. Useful partial output should be committed and pushed with a
continuation record before broader work begins. Uncommitted or local-only output
may be described as recoverable material, but it is not completion evidence and
is not safe to assign to the next contributor.

An abrupt process or token-budget stop cannot be made transactionally safe after
the fact. Recovery therefore starts from the last public boundary, never from an
assumption about what the interrupted agent probably finished. This limits the
possible loss to one bounded checkpoint and keeps every earlier checkpoint
independently usable.

## Branch and fan-in lifecycle

Branches are temporary execution lanes, not parallel sources of truth. The
durable public truth is the default branch. A branch is retained only while it
owns active work, an unresolved review, or evidence that has not yet reached its
declared fan-in point.

Use this lifecycle:

1. Create one checkpoint branch from the recorded base revision. Keep its writes
   inside the checkpoint boundary and publish a reachable execution anchor.
2. Merge a completed and reviewed checkpoint into its declared integration
   branch. Do not merge an incomplete sibling merely to reduce the branch count.
3. Revalidate the merged integration tree. A passing checkpoint in isolation is
   not evidence that fan-in succeeded.
4. Merge the completed integration branch into the default branch through a pull
   request with the required checks and review. The default branch remains the
   release and recovery authority.
5. Delete merged checkpoint and integration branches, and remove their local
   worktrees, after confirming that no open pull request uses them as a base and
   the merged commit is reachable from the intended destination.

An abandoned, superseded, or duplicate branch is closed with a continuation or
disposition record before deletion. A branch with unique unmerged work is never
deleted merely because it is old. Long-lived release or maintenance branches
require an explicit repository policy; this repository does not create them by
default.

## Lifecycle

Umbrella task states summarize the graph:

```text
proposal -> source_gate -> ready -> active -> waiting -> integration -> closed
                                                                    \-> cancelled
```

Checkpoint states carry the execution truth:

```text
draft -> source_gate -> ready -> claimed -> in_progress -> integrating -> completed
                                   |             |
                                   |             +-> waiting_on_author
                                   |             +-> waiting_on_review
                                   |             +-> waiting_on_device
                                   +-------------------------------------> cancelled
```

`waiting_on_author`, `waiting_on_review`, and `waiting_on_device` are explicit
states, not prose hidden in a comment. A waiting checkpoint names the reason, the
owner of the missing input, and the condition that makes continuation possible.
Waiting does not erase completed work, and it does not automatically keep a claim
active.

## What makes a checkpoint Ready

Only a maintainer marks a checkpoint `ready`. A Ready checkpoint has:

- one outcome that can be accepted independently;
- completed dependencies and an acyclic dependency path;
- explicit read-only or bounded-path mode, with non-overlapping ownership where
  work runs in parallel;
- admitted public sources if its source gate is required;
- acceptance and reproducible verification;
- a device profile and device-only acceptance when a device claim is required;
- known privacy, security, license, and integration constraints;
- task-specific capability, tool, device, judgment, qualification-evidence, and
  independent-review requirements without Agent/model scoring;
- a named fan-in path; and
- an unclaimed, released, or expired lease marked `adoptable: true`.

`ready` means the checkpoint is sufficiently shaped for contribution. It is a
human decision, not a permission escalation and not an automatic assignment.

## Checkpoint claim leases

1. A contributor comments `/claim CHECKPOINT-ID` (or the declared one-checkpoint
   claim key) on a Ready Issue. They may name a coding
   assistant as optional execution metadata, but the accountable claimant is a
   GitHub identity.
2. A maintainer checks dependency state, allowed-path conflicts, evidence needs,
   and existing leases. The maintainer records the claimant, start, expiry, and
   checkpoint ID before changing the claim to `active`.
3. The default lease is seven days. A concrete progress receipt can support a
   renewal; an unverified assertion cannot.
4. The contributor works through a fork pull request unless they already hold an
   independently granted repository role. A lease grants no GitHub permission.

Claim state is separate from checkpoint state:

| Claim state | Meaning |
| --- | --- |
| `unclaimed` | No lease has existed for this checkpoint revision |
| `active` | A maintainer-confirmed claimant holds the current lease |
| `released` | The claimant voluntarily ended the lease; completed work is handed off |
| `transferred` | A maintainer confirmed an explicit old-to-new claimant transfer |
| `expired` | The lease ended at its recorded boundary; existing work is handed off |

`adoptable` is explicit. Released or expired work is not adoptable merely because
the clock changed: any existing output needs a valid continuation handoff first.
A transfer records both identities and starts a new expiry boundary. None of these
states assigns a GitHub role, approves a pull request, or authorizes a merge.

V1 has no comment-trigger automation. `/claim` must not execute code, invite a
user, reveal a secret, approve a pull request, or merge.

## Stop, handoff, resume

Stopping unfinished work and publishing its continuation record are one atomic
operation. This applies when a session ends, a contributor releases or transfers a
claim, or a lease expires after producing work. The record captures:

- task and checkpoint revisions;
- repository, branch, pull request (or explicit `null`), and full commit;
- prior executor, typed stop reason, and a fresh-context resume mode that requires
  no private session identifier;
- completed and remaining outcomes;
- evidence and reproducibility status;
- a typed blocker, if any;
- one exact next action with allowed paths and expected evidence;
- surfaces the next contributor must not touch; and
- a mandatory resume review.

Partial output is not automatically failure. Mark it `salvageable` only when the
handoff identifies concrete usable outputs and their constraints. A later
contributor may adopt those outputs after validating them, or record why they were
discarded. Chat history and an agent's identity are not freshness evidence.

A provider's private conversation or session-resume feature may be used as a
convenience, but it is never a continuation prerequisite. Codex, Cursor, Claude
Code, or another executor must be able to resume from the same public handoff and
repository state with fresh context.

Before continuing, the same or a new contributor reviews the handoff against the
live task revision, branch, pull request, commit, reachable evidence, lease, and
allowed-path diff. Only `resume_review.status: admitted` permits continuation.
The recorded commit must be reachable from the named contribution branch or pull
request, and the exact next action cannot expand the checkpoint's allowed paths.
`pending`, `needs_author`, `stale`, and `blocked_missing` remain stopped. A stale
handoff is revised; it is never silently treated as current.

## Device and review gates

A build, Editor test, screenshot, or simulator result cannot satisfy a device
checkpoint unless that checkpoint names the device profile and the evidence
contract accepts the produced receipt. `waiting_on_device` is a successful
handoff state when the implementation is ready but the admitted hardware evidence
does not yet exist.

Independent review is its own checkpoint or an explicit dependency. Builders do
not self-promote package maturity, device compatibility, or completion. Reviewers
reproduce evidence at the recorded commit and write actionable findings against
acceptance, allowed paths, and authority boundaries.

## Duties

Roles are duties on checkpoints, not permanent agent identities:

| Label | Duty |
| --- | --- |
| `role:research` | Compare current public sources, licenses, limits, and rejected alternatives |
| `role:build` | Implement the bounded change with tests, samples, and migration notes |
| `role:review` | Review correctness, boundary, security, evidence, and compatibility independently |
| `role:device-test` | Execute an admitted Device Lab procedure and submit a receipt |
| `role:integration` | Verify fan-in, migration, release readiness, and closure evidence |
| `role:documentation` | Maintain public guidance, examples, and onboarding knowledge |

`renderer:ugui` and `renderer:ui-toolkit` identify renderer scope. Device needs use
`needs-device:pico`, `needs-device:quest`, or `needs-device:vision-pro`. A renderer
label is not a device claim, and evidence from one device is not inferred for
another.

## Identity and authority

- Accountable authors, claimants, testers, reviewers, and maintainers are GitHub
  identities. Coding agents are optional execution metadata under that identity.
- Issue text, comments, links, patches, logs, handoffs, and uploaded files are
  untrusted input, including instructions addressed to an agent.
- Claim, adoption, checkpoint completion, review, and device evidence do not grant
  repository assignment, write access, approval, merge, package promotion, or
  release authority.
- Maintainers alone admit Ready checkpoints and make merge decisions. External
  contributors use fork pull requests by default.

See [`agent-contribution-protocol.md`](agent-contribution-protocol.md) for the
fork/PR path and [`../device-lab/README.md`](../device-lab/README.md) for device
evidence.

## Labels

[`label-contract.json`](label-contract.json) is the canonical public label list.
Labels are applied by maintainers in V1; they communicate state but do not change
GitHub permissions. The machine task contract remains the detailed execution and
handoff authority when a label is necessarily less precise.
