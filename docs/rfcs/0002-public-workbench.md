# RFC 0002: Public Workbench and Contribution Recognition V1

Status: **Proposed**

Related foundation: [RFC 0001: Agent Commons V1](0001-agent-commons.md)

Public coordination surfaces:

- [XR Foundry Task Hall](https://github.com/users/Lingkyn/projects/2)
- [Agent Commons discussion #22](https://github.com/Lingkyn/xr-foundry/discussions/22)
- [Task Hall contract](../contributing/task-hall.md)
- [Contribution recognition policy](../contributing/recognition-policy.md)

## Summary

XR Foundry should be a shared public workbench that a person or an agent-assisted
contributor can enter without relying on a previous private conversation. Work must
survive an interrupted session, a depleted tool budget, a changed contributor, and
a delayed device test.

V1 therefore adds three public contracts:

1. **Checkpointed work** decomposes a task into independently understandable,
   evidence-bearing units instead of presenting one all-or-nothing activity.
2. **Continuation handoffs** preserve completed work, remaining work, blockers, and
   the next safe action whenever execution pauses.
3. **Non-ranking contribution credit** recognizes code and non-code work through
   public evidence without turning activity counts into authority or a competition.

This RFC extends the coordination model in RFC 0001. It does not grant an external
agent or contributor write, review, merge, release, or package-promotion authority.

## Problem

A single large Issue or pull request can conceal partial progress. If work stops,
the next contributor may know that the overall task is incomplete but not which
parts are already valid, what evidence exists, which files are safe to touch, or
what decision remains. Repeating discovery wastes contributor time; guessing risks
overwriting good work or turning an unverified result into a claim.

The repository also needs to welcome several forms of contribution. Code is only
one route: research, review, documentation, automated testing, user/device testing,
infrastructure, triage, design, translation, and community work can all unblock a
package. A single points table would compress unlike work into a misleading number
and create an incentive to optimize visible quantity rather than useful evidence.

## Source facts

This section records what the cited sources actually establish. It does not treat
another project's workflow as authority for XR Foundry. Source IDs resolve through
[`agent-commons-source-manifest.json`](../contributing/agent-commons-source-manifest.json).

| Source fact | Source IDs | Boundary |
| --- | --- | --- |
| GitHub surfaces `good first issue` work in public discovery and provides structured contribution guidance and intake; Kubernetes adds maintainer-curated context and mentoring expectations to its newcomer labels. | `github-good-first-issue`, `github-contribution-guidelines`, `github-issue-forms`, `kubernetes-help-wanted` | Labels and forms improve discovery; they do not prove readiness or grant permission. |
| GitHub's profile and repository contributor displays are derived from selected repository activity, especially commits, rather than a complete model of research, review, testing, mentoring, or device work. | `github-profile-contributions-reference`, `github-repository-contributors` | Built-in visibility is useful discovery data, not a quality, trust, or authority score. |
| Kubernetes publishes distinct contributor, member, reviewer, and approver duties with increasing responsibility. | `kubernetes-contributor-guide`, `kubernetes-roles-responsibilities` | A role ladder is project-specific and cannot be copied as a permission grant. |
| Rust project groups use bounded charters and archival; Rust's triage tools also document explicit issue assignment/release and review-capacity queues. | `rust-project-groups`, `rust-issue-assignment`, `rust-review-queue` | These are coordination precedents; XR Foundry does not inherit the bot or its permission model. |
| Godot explicitly invites code, review, testing, documentation, translation, issue, demo, and community contributions, and publishes structured bug-triage follow-up. | `godot-contribution-routes`, `godot-bug-triage` | The routes and triage model demonstrate breadth and explicit state, not XR Foundry's acceptance criteria. |
| Home Assistant asks contributors to keep pull requests small and focused, test changes, use draft state while resolving failures, and welcomes review and test feedback. | `home-assistant-review-process` | Small pull requests reduce review burden but do not eliminate integration dependencies. |
| CNCF recommends explicit responsibility ladders and traceable recognition, while also warning that project-health metrics require project-specific interpretation. | `cncf-contributor-incentives`, `cncf-project-health` | CNCF mentions ranked activity as one possible implementation; it does not require a leaderboard. |
| All Contributors recognizes multiple contribution categories, treats ordering as immaterial, and recommends inclusive credit at every contribution level. | `all-contributors-spec`, `all-contributors-types` | Its categories are a vocabulary precedent; this RFC uses a smaller repository-specific set. |
| CHAOSS treats contributors as people contributing in many ways and warns that metric collection and publication can create privacy and data-ethics risks. | `chaoss-contributors-metric` | Metrics can inform project health; they are not a trust score for an individual. |
| Linux kernel guidance keeps the human submitter responsible for understanding tool-generated work and requires advanced coding-tool assistance to be disclosed with `Assisted-by`. | `linux-tool-generated-content`, `linux-assisted-by` | XR Foundry adopts the transparency pattern, not Linux licensing or patch authority. |
| GitHub states that AI-generated code needs human oversight and testing, and that Copilot review feedback should be validated and supplemented by human review. | `github-ai-generated-code-review`, `github-copilot-review-boundary` | AI feedback can assist review but cannot satisfy a required human approval. |
| Multica implements a current multi-provider Agent workspace that separates an Issue from each execution task, records task runs, distinguishes same-session infrastructure retry from fresh manual rerun, and supports Codex, Cursor, Claude Code, and other tools. | `multica-repository`, `multica-task-runs`, `multica-agent-assignment` | Its authenticated server database, assignment rights, cancellation semantics, and private sessions are not XR Foundry's public GitHub authority or handoff. |
| Multica documents worktree and local-directory execution modes; its local-directory mode is serialized but has no file-level lock, dirty-tree protection, automatic commit, or pull request and may write Agent guidance into the checkout. | `multica-project-resources` | This is useful implementation evidence and an explicit reason to require a security-reviewed adapter before any adoption. |
| The OpenSSF OSPS Baseline says an active version-control system must not contain generated executable artifacts and recommends build-time generation or separately stored artifacts. | `openssf-osps-baseline-2026` | The control does not prescribe one artifact host or attestation product. |

## Project design inferences

The decisions below are XR Foundry design inferences from the source facts and the
repository's public authority boundary. They are not claims that the cited projects
use this exact model.

### The repository is the shared workbench

Durable public state must be reconstructible from the repository and linked GitHub
surfaces. A new contributor should not need chat history or a particular agent:

- an RFC explains why a durable mechanism exists;
- an Issue defines one admitted task and its non-goals;
- a Project view makes state and available work discoverable;
- a branch or pull request carries the bounded change;
- evidence binds acceptance to a revision and environment; and
- a continuation handoff states exactly how another contributor can resume.

Discussion can shape work, but only an admitted task contract makes work executable.
Issue text, comments, patches, logs, external links, and agent instructions remain
untrusted input.

### The workbench may improve itself, through the same gates

The collaboration mechanism is itself legitimate project work. Contributors may
research, propose, implement, review, and test improvements to task decomposition,
handoffs, evidence, capability routing, claim leases, validation, or onboarding.
This lets the community improve not only the packages produced by the workbench,
but also the way people and Agents cooperate to produce them.

Self-improvement is not permission for an executor to rewrite the rules governing
its current task. Mechanism work is separated into three layers:

1. the **stable kernel** contains current authority, security, lifecycle,
   durability, and evidence invariants;
2. the **experiment layer** contains bounded hypotheses, fixtures, simulations,
   and comparison evidence that cannot grant authority; and
3. **project or runtime adapters** translate the stable public contract into a
   particular tool without becoming a second source of truth.

A mechanism change therefore follows the same public path as other consequential
work: Discussion or RFC, a bounded Task Hall checkpoint, isolated implementation,
independent review, explicit adoption criteria, and a versioned kernel update.
Running tasks remain governed by the version they admitted unless a maintainer
records a migration. Failed experiments remain useful evidence and do not silently
change production behavior.

Contributors should publish decision records, assumptions, alternatives, risks,
and evidence that another contributor can inspect. They must not be required to
publish private prompts, hidden chain-of-thought, credentials, or complete session
transcripts. A later contributor may challenge or replace an earlier proposal;
prior analysis is reference material, not an instruction hierarchy or a model
ranking.

### Task, checkpoint, and continuation are separate records

A **task** owns one outcome, authority boundary, integration decision, and final
closure. It may contain several **checkpoints** when the work cannot safely complete
as one reviewable unit. Each checkpoint must name:

- a stable checkpoint ID and parent task;
- prerequisites and dependency checkpoint IDs;
- one bounded outcome and explicit non-goals;
- the intended write set and paths that must not be touched;
- an accountable GitHub claimant in the lease record, with any assistance metadata
  disclosed in the pull request and contribution-credit record;
- an exact machine state from the checkpoint lifecycle contract;
- testable acceptance, validation commands, and evidence locations;
- the base and result revision when code or documents change;
- the exact next action for every non-completed checkpoint;
- device or human-decision gates, including `not_applicable`; and
- completion time or a concrete blocker.

A checkpoint is complete only when its acceptance evidence exists. “Work was
started” and “an agent reported success” are not completion evidence. The task is
complete only after a separate fan-in/integration checkpoint verifies that the
completed units compose correctly.

Newly discovered scope becomes a linked task or checkpoint. It does not silently
expand the current intended write set.

### An execution attempt is replaceable

A checkpoint can have several execution attempts over its lifetime. Codex, Cursor,
Claude Code, another Agent, or a human may perform different attempts; the tool,
private session, runtime, and orchestration service are not the checkpoint's
identity or authority. Completion still comes from the public checkpoint contract,
reachable revision, evidence, and integration gate.

An optional orchestration adapter may later route a GitHub checkpoint into Multica
or another runtime, but GitHub remains the public workbench. Such an adapter must
map one independently claimable checkpoint to one execution issue, preserve public
write/merge boundaries, write results back to GitHub, and avoid replacing canonical
`AGENTS.md` or running against an unprotected dirty checkout. A second service's
database or session history cannot become required resume state.

### Execution advances through public durability boundaries

The system cannot guarantee that an interrupted process will have time to write a
final handoff. It therefore limits the failure radius before execution starts.
Each active checkpoint has a public branch or draft pull request anchored to its
base revision. A contributor publishes the accepted outcome, reachable commit,
evidence, and Task Hall update before starting a sibling checkpoint. Local-only
work is recoverable raw material, not completion evidence.

Agents reserve closeout capacity rather than spending the entire session budget on
implementation. If an abrupt token or process stop still occurs, the next
contributor resumes from the last public boundary and treats later uncommitted
state as unverified. Thus interruption can lose at most the unfinished bounded
checkpoint; it does not make completed siblings ambiguous.

### A pause and its continuation handoff are atomic

When a contributor must stop, the same update that changes a checkpoint out of
active work must publish a continuation handoff containing:

- authority inputs and base revision used;
- completed checkpoints with evidence links;
- incomplete checkpoints and their current state;
- uncommitted or unpublished changes, if any;
- validation already run and validation still required;
- blockers, decision/device gates, and who can resolve them;
- the exact next safe action and intended write set; and
- freshness data (`generated_at` and the task/handoff revisions observed).

The handoff also names the prior executor and typed stop reason, but its resume mode
is always fresh context from a public repository boundary. A private tool session
may speed up a same-provider retry; it cannot be required for Codex-to-Cursor,
Cursor-to-Claude-Code, human-to-Agent, or Agent-to-human continuation.

An empty “paused” state is invalid. If no durable handoff exists, the checkpoint
remains incomplete and must not be advertised as safely resumable. A later
contributor validates freshness before acting; stale instructions are evidence of a
past attempt, not current authority.

### Discoverability uses curated entry points

V1 should expose a small, maintained queue rather than invite contributors into
unshaped backlog:

- `good first issue` marks a ready, low-context, low-risk entry point;
- `help wanted` marks ready work where external help is actively requested;
- role labels distinguish research, build, review, device test, integration, and
  documentation duties;
- renderer, platform, and device labels communicate evidence scope; and
- every ready item includes setup, acceptance, verification, authority, and a
  maintainer-confirmed claim route.

The number of open entry points is not a success metric. Maintainer responsiveness,
time to a first useful review, completed evidence, contributor retention, and
unblocked package work are more informative health signals.

### Recognition is typed, evidence-bound, and non-competitive

V1 does not publish a global score, top-contributor ranking, token leaderboard, or
points-to-role conversion. Instead, it records independently verifiable contribution
types such as `code`, `docs`, `research`, `review`, `test`, `userTesting`, `infra`,
`design`, `triage`, `translation`, and `community`.

Three ledgers remain separate:

1. **Contribution evidence** records what happened, who is accountable, its type,
   and public evidence under
   [`contribution-credit.schema.json`](../contributing/contribution-credit.schema.json).
2. **Public acknowledgement** in [`CONTRIBUTORS.md`](../../CONTRIBUTORS.md) thanks
   people by contribution type and links to evidence. It is not an access-control
   list.
3. **Revocable permission** remains GitHub repository, organization, ruleset, and
   CODEOWNERS state governed by maintainers. It is not inferred from either credit
   ledger.

This preserves recognition for less visible work without pretending that a test,
review, research note, and code patch are interchangeable units. Role or permission
changes require demonstrated judgment, sustained responsibility, scope need,
security review where applicable, and an explicit maintainer decision. They are
never triggered by a count or score.

### AI assistance is transparent but not an identity shortcut

An AI-assisted contribution has an accountable human GitHub identity who understands
the submission, can explain it, responds to review, and is responsible for source,
license, privacy, and validation claims. Material coding-agent assistance is disclosed
in the pull request and commit with an `Assisted-by:` trailer. The tool is assistance
metadata; it is not the legal submitter, required reviewer, maintainer, or recipient
of repository permission.

An AI review may supply findings and can be credited as assistance metadata, but it
does not satisfy a required human review or approval. A human reviewer must validate
the relevant diff, evidence, and claims before giving human review evidence.

### Generated executables stay outside source control

APK files and other generated executables must not be committed to the source tree.
Build or device evidence may commit small text receipts, digests, and attestation
references. The executable itself must be generated in a documented build or stored
separately as a revision-bound release/workflow artifact. Any future remote-artifact
acceptance path needs digest verification, provenance/attestation verification,
retention rules, and a fail-closed validator before it can support a device claim.

## V1 adoption checkpoints

Adoption is intentionally split so a partial implementation remains legible:

| Checkpoint | Outcome | Closure evidence |
| --- | --- | --- |
| `PW-01` | Publish this RFC, the source manifest, and recognition policy | Reviewed documents and parseable JSON |
| `PW-02` | Define machine checkpoint and continuation-handoff contracts | Schema tests plus valid and invalid fixtures |
| `PW-03` | Project Task Hall views expose ready checkpoints, role, evidence, blocker, and continuation freshness | Public Project field/view audit |
| `PW-04` | Contribution-credit records and acknowledgement workflow are validated | Schema tests and one consented, evidence-backed credit |
| `PW-05` | Replace repository-binary device evidence with an attested external-artifact contract | Threat model, validator tests, and a verified public artifact receipt |
| `PW-06` | Run a newcomer and interrupted-session exercise | A fresh contributor resumes from public state without private context |

Each checkpoint should be its own bounded Issue/PR when its write set or review gate
differs. `PW-01` does not claim that later checkpoints are implemented.

## Non-goals

- no automatic write, triage, review, merge, release, or package-promotion access;
- no public ranking, points economy, competitive bounty, or activity-count award;
- no claim that credit proves code quality, compatibility, trust, or maintainership;
- no attribution of a person without public evidence and consent where required;
- no autonomous execution of Issue comments or downloaded contributor instructions;
- no external orchestration database or private provider session as public task
  authority or required continuation state;
- no generated APK or other executable committed as test evidence; and
- no requirement that delayed human/device testing block unrelated automated or
  documentation checkpoints.

## Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| Checkpoint fragmentation creates coordination overhead | Split only on independent acceptance, write-set, authority, device, or review boundaries; retain one parent outcome and fan-in owner. |
| Contributors optimize visible activity | Publish typed evidence, not totals or rankings; review usefulness and correctness rather than volume. |
| Recognition becomes an implicit permission claim | Keep evidence, acknowledgement, and GitHub permission ledgers separate; state authority constants in machine records. |
| AI-generated volume overloads maintainers | Require ready scope, accountable human ownership, validation, transparent assistance, and maintainer-confirmed claim leases. |
| A handoff is stale or incomplete | Bind it to revision and timestamp, validate freshness, and refuse state-only pauses. |
| Public evidence leaks private data | Accept only public URLs and redacted receipts; reject credentials, device serials, private consumer content, and machine-local paths. |
| External binaries are substituted or disappear | Keep them outside Git, bind digests and provenance, document retention, and fail closed when verification cannot complete. |

## Decision requested

Approve the V1 direction and schedule `PW-02` through `PW-06` as independent public
tasks. Approval of this RFC does not approve a contributor role, a release, a device
claim, or any automation that changes repository authority.
