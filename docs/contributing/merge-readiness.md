# Merge readiness: the process decides, a person executes

Status: **incubating repository contract** (prototype-stage rule in
`GOVERNANCE.md`, "Process-decided merges and lazy consensus", revertible; objection
surface `DLB-0002`)

XR Foundry wants "may this change merge?" to be answered by a process, not by one
person's mood. The answer is computed from public, reproducible facts by
`scripts/merge_readiness.py` and written down as a verdict. For a routine change
on a branch named by a live operating mandate, GitHub auto-merge executes the
merge once the verdict is `ready` and the required checks pass; no person clicks.
For everything else the verdict tells the maintainer what the process concluded,
and it makes any merge against a blocked verdict a visible override that needs a
recorded reason.

## The loop without a person

1. An Agent under a mandate pushes to its mandated branch and opens a pull request
   with GitHub auto-merge enabled.
2. CI runs `repository-contract` and `merge-readiness`. The `merge-readiness` job
   fails whenever the verdict is blocked, and on a mandated branch also whenever
   the change is not `process_merge_eligible`.
3. When both required checks are green, GitHub merges. No workflow holds a write
   token; GitHub executes the merge under the repository's branch protection.
4. When a check is red, the pull request waits. The Agent's next run reads the
   verdict, fixes what it names, and pushes again. A non-routine change waits for a
   person by design.

One-time setup only the repository owner can do: allow auto-merge in the
repository settings, and protect `main` with `repository-contract` and
`merge-readiness` as required status checks. Until then the verdict is published
but a person still clicks merge.

**Undo.** Revert the commit named in `CHANGELOG.md` for this rule, or set the
steward mandate's `revocation.status` to `revoked` to stop the automation at once.

This is the repository's split between process accountability and individual
accountability:

| Layer | What it owns | Where it is recorded |
| --- | --- | --- |
| Process | The rules a change must satisfy, evaluated the same way for everyone: merge result, repository contract, version-evidence rule, changelog discipline, maturity boundary, governance review windows, independent review | `scripts/merge_readiness.py`, `scripts/validate_repository.py`, `tests/`, the CI `merge-readiness` job summary |
| Person | The named GitHub identities that authored, reviewed, tested, and merged the change, and any reason for merging against the verdict | Commit trailers (`Assisted-by`, `Reviewed-by`, `Tested-by`), review approvals, device receipts, the merge commit, the deliberation record |

A green verdict never grants merge permission, and a mandate never lets an Agent
merge. RFC 0007 proposes the path from advisory verdict to binding check.

## Run it

```text
python scripts/merge_readiness.py --base origin/main --head HEAD --markdown
python scripts/merge_readiness.py --head my-branch --pr-metadata pr.json --json
```

The command reads git history, may create a temporary detached worktree of the
merged tree to run the canonical repository contract, and removes it afterwards. It
never writes to the working tree, pushes, comments, or merges. Exit code 0 means
`ready`; 1 means `blocked`; 2 means the inputs could not be evaluated.

Pull-request metadata is a small JSON object:

```json
{"author_login": "someone", "draft": false, "reviews": [{"login": "reviewer", "state": "APPROVED"}]}
```

In CI the `merge-readiness` job builds it from the GitHub REST reviews payload with
`--github-reviews`, `--pr-author`, and `--pr-draft`, and reports review status as
information because GitHub's own required-review protection is the binding check.

## The checks

| Check | Passes when | Otherwise |
| --- | --- | --- |
| `no_merge_conflict` | The head merges into the base cleanly | `fail`, with the conflicted paths |
| `repository_contract` | `python scripts/validate_repository.py --json --run-contract-tests` passes on the merged tree | `fail` with the first errors; `unknown` when skipped or conflicted |
| `version_bump_carries_evidence` | No `package.json` version changed, or every changed version is recorded in `package-catalog.json` and a verified compatibility profile in the same change (LESSON-008) | `fail` naming the package and the missing record |
| `changelogs_updated` | Every package whose files changed also changed its `CHANGELOG.md`; repository-level changes outside `docs/` and packages changed the root `CHANGELOG.md` | `fail` naming the missing changelog |
| `maturity_unchanged` | No `maturity` field changed in a component manifest or the package catalog | `fail`: a promotion is a separate evidence decision |
| `unity_evidence` | No Unity package source changed, or a `docs/validation/*.json` receipt in the change names a commit after which no package source changed | `info`: merge is allowed only without maturity, release, or device claims |
| `governance_review_window` | No governance rule changed; or a new `Status: **Proposed**` RFC was only added; or a deliberation record with `governance_policy` or `constitutional_change` class whose window closed is supplied and is either resolved with a decision dated after `review_not_before`, or still open with no `risk` or `counterexample` delta (lazy consensus; `--no-lazy-consensus` disables it) | `fail`: the rule change owes its 7-day or 14-day public review, or a person must resolve an objection |
| `not_draft` | The pull request is not a draft | `fail` |
| `independent_review` | A GitHub identity other than the author (bots excluded) approved and nobody still requests changes | `fail`, or `info` with `--reviews-informational`; `unknown` without metadata |
| `mandated_branch` | `--head-branch` matches a branch pattern of an unrevoked, unexpired operating mandate at the head commit | `fail` (never blocks the verdict; it only removes process-merge eligibility); `info` without a branch |

Verdict: `ready` only when no check is `fail` or `unknown`. `info` never blocks.
The report also states `decision_class` (`routine_change` when no governance path,
maturity, or package version changed) and `process_merge_eligible`, which is true
only for a `ready`, routine, non-draft change on a mandated branch. Eligibility is
what the CI job enforces on mandated branches; it is not a permission.

Governance paths are `GOVERNANCE.md`, the governance and Agent-membership models,
`docs/rfcs/`, the Task Hall documents, the deliberation protocol, and `CODEOWNERS`.
Operating-mandate records are reported but not blocked: the governance text says a
mandate is granted or revoked by a recorded maintainer decision without a window.

## What the verdict is not

- It is not execution evidence. `unity_evidence: info` means the code has not run
  in an Editor; the compatibility profiles and Device Lab receipts stay the only
  evidence surfaces.
- It is not a review. It checks that an independent review exists, not that the
  review was good.
- It is not permission. The mandate, the verdict, and CI grant no GitHub role.
- It does not shorten a governance review window. An `open` deliberation record
  satisfies it only after the window closed with no objection delta.

## Overriding a blocked verdict

A maintainer may still merge a blocked change, for example to contain a security
problem. The override must be recorded: name the blocking check IDs and the reason
in the merge commit or pull request, and open a deliberation record when the
override touched a governance rule. Repeated overrides of the same check are a
signal that the rule or the process, not the person, needs to change, through the
deliberation protocol.
