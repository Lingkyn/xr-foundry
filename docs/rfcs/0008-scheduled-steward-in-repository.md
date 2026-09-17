# RFC 0008: Scheduled steward execution inside the repository

Status: **Proposed**

Activation: **none until deliberation record `DLB-0004` resolves; the workflow file
lands only after that, and only with a person confirming the pinned action SHA and
adding the secrets**

Public deliberation: **DLB-0004, open until 2026-10-01T19:00:00Z**

Related decisions:

- [RFC 0001: Agent Commons V1](0001-agent-commons.md)
- [RFC 0007: Process-decided merge readiness](0007-process-decided-merge-readiness.md)
- [Operating mandates](../governance/mandates/README.md)
- [Work items](../contributing/work-items.md)

## Summary

The maintainer's stated model is: a person supplies tokens and a repository; Agents
do the rest. Today the steward runs from one Agent vendor's hosted session, on
schedules that live in that vendor's account, with that person's credentials. If
that session, account, or vendor disappears, the repository stops moving and
nothing in the tree records why. This RFC proposes that the steward's schedule
and entry point live in the repository itself:

1. One designated workflow, `.github/workflows/steward.yml`, runs on a `schedule`
   trigger (and `workflow_dispatch`), checks out the default branch, and starts an
   Agent runner with an API key held as a repository secret. The runner reads
   `AGENTS.md`, takes the next open work item, and works under the same mandate,
   verdict, and contract as any other contributor.
2. The workflow's own `GITHUB_TOKEN` stays read-only. Pushing a branch and opening
   a pull request use a separate fine-grained token secret scoped to this
   repository with contents and pull-request write only; branch protection on
   `main` and the required `repository-contract` check remain the merge gate. No
   token in the workflow can change settings, tags, releases, or protection rules.
3. The runner is provider-neutral in the contract: the workflow calls
   `scripts/steward_entry.sh`, which starts whichever Agent CLI the present secret
   selects. The first implementation is one vendor's action pinned to a full
   commit SHA that a person confirms; a second vendor is a routine change to that
   script, not a governance change.

## Problem

`validate_workflow_security` admits only `pull_request`, `push`, and
`workflow_dispatch` triggers and forbids write-capable permissions, which was
right when every workflow was a check. A steward that must run without a person
present needs a `schedule` trigger and a way to push its branch. Allowing that
without review would widen the attack surface that rule exists to close: a
scheduled job with a write token and an LLM reading untrusted Issues is exactly
the shape prompt-injection attacks target.

## Decision

Amend the workflow security rule as follows, and nothing more:

- A `schedule` trigger is admitted only on a workflow whose file name is listed in
  `docs/governance/mandates/*.mandate.json` under a new `scheduled_workflows`
  field of an unexpired, unrevoked mandate. Every other workflow keeps the V1
  trigger set.
- Such a workflow keeps `permissions` read-only. Any write happens through a
  named secret (`STEWARD_GIT_TOKEN`) that the validator requires to be referenced
  only from that workflow and only in steps that do not run on `pull_request`
  events from forks.
- The workflow must set `persist-credentials: false`, pin every action to a full
  SHA, and pass no Issue or comment body to the runner as an instruction; the
  runner reads GitHub content itself as untrusted input, as `AGENTS.md` already
  requires.
- The runner may only push to branches matching the mandate's `branch_patterns`;
  a ruleset on `main` and on tags enforces the rest on the GitHub side, and the
  mandate records that the ruleset exists.

The undo is one revert of the rule amendment plus deleting the workflow file; the
secrets are the owner's to remove.

## Non-goals

- No change to what a routine merge is, who can approve, or what the verdict
  blocks.
- No wallet, treasury, or on-chain step.
- No expansion of the steward mandate's allowed actions beyond where it runs.

## Source basis

The pattern (a scheduled workflow with a scoped token driving an agent runner) is
how the maintained agent actions of the major providers document their own CI use;
their documentation could not be fetched from the authoring environment and must
be confirmed by a person before the pinned SHA is recorded.
