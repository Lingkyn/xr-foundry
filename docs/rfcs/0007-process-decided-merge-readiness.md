# RFC 0007: Process-decided merge readiness

Status: **Proposed**

Activation: **inactive; the merge decision stays with a maintainer at G0 x A0**

Public deliberation: **not opened by this local implementation**

Related decisions:

- [RFC 0001: Agent Commons V1](0001-agent-commons.md)
- [RFC 0004: Progressive governance](0004-progressive-governance.md)
- [RFC 0006: Agent-native membership, identity, and delegated authority](0006-agent-native-xr-dao.md)
- [Merge readiness contract](../contributing/merge-readiness.md)
- [Task Hall](../contributing/task-hall.md)

## Summary

XR Foundry should decide whether a change may merge by a published process rather
than by a person's judgment, while a named person stays accountable for executing
that decision and for any override. This RFC proposes three steps, each with its own
review and each reversible:

1. **Advisory verdict (implemented now, no authority change).**
   `scripts/merge_readiness.py` computes a `ready` or `blocked` verdict from the
   repository's existing rules and the CI `merge-readiness` job publishes it on
   every pull request. A maintainer still merges. A merge against a blocked verdict
   is an override that must be recorded with a reason.
2. **Binding verdict (governance policy, 7-day review).** The `merge-readiness`
   job becomes a required status check beside `repository-contract`, and the
   independent-review check becomes blocking instead of informational. A maintainer
   can still merge only what the process passed, and the override path becomes a
   deliberation record rather than a comment.
3. **Process-executed routine merges (constitutional, 14-day review).** For the
   `routine_change` decision class only, a merge whose verdict is `ready`, whose
   review comes from a different principal and lineage than the author, and whose
   changes touch no governance path may be executed by an accountable automation
   under a recorded mandate. Governance, maturity, release, security, and device
   claims stay with people.

Steps 2 and 3 are not activated by merging this file. Step 3 also depends on
RFC 0006 for the principal and lineage vocabulary it needs.

## Problem

Today the repository has strong, fail-closed checks for what a change contains,
but "may this merge?" is still answered by a single maintainer reading the pull
request. That has three costs:

- Contributors and Agents cannot predict the answer or reproduce it locally, so
  work stalls waiting for a person (PR #81 has waited since July 2026 on a version
  rule that a script can state in one line).
- The maintainer is the only accountable party for every merge, which is the
  bus-factor problem RFC 0004 already names for `G2`.
- The distinction between "the process rejected this" and "a person chose not to
  merge this" is invisible in the public record.

## Design

The process layer owns rules. The person layer owns identities and overrides.

| Question | Process answer | Person answer |
| --- | --- | --- |
| Does the change merge cleanly and pass the repository contract? | `no_merge_conflict`, `repository_contract` | none |
| May a package version move? | `version_bump_carries_evidence` (LESSON-008) | the evidence run's tester identity |
| Is the change recorded? | `changelogs_updated` | the author |
| Is this a promotion or a rule change in disguise? | `maturity_unchanged`, `governance_review_window` | the maintainer decision in a deliberation record |
| Was it reviewed by someone else? | `independent_review` | the reviewer's GitHub identity |
| Why was a blocked change merged anyway? | none | the recorded override reason |

The verdict schema is `xr-foundry.merge_readiness.v1`. Its `authority` block states
that the verdict is not binding and grants no permission until step 2 is adopted.

## Activation gates

Step 2 requires all of the following in a later checkpoint:

1. a public governance-policy deliberation open for at least 7 days;
2. one month of advisory verdicts on real pull requests with no false `blocked`
   verdict left unexplained, recorded as deltas on that deliberation;
3. an explicit maintainer decision;
4. the branch-protection change performed as a separately authorized repository
   setting, never by a workflow or an Agent; and
5. this RFC and `docs/contributing/merge-readiness.md` updated together.

Step 3 additionally requires RFC 0006 activation, a mandate whose allowed actions
name the merge operation and the `routine_change` class only, a tested revocation
and rollback exercise, and a 14-day constitutional deliberation.

## Non-goals

- no change to who may merge today;
- no branch-protection, GitHub App, or repository-setting change by this file;
- no automation of governance, maturity, release, security, or device decisions;
- no replacement of human review by generated review;
- no token, wallet, or on-chain mechanism.

## Reopen conditions

- A verdict passes a change that later needed a revert; the rule set is revised
  before step 2 proceeds.
- RFC 0006 changes the definition of an independent reviewer.
