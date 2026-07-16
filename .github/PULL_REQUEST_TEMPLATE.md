## Linked work

Link the umbrella Issue, exact checkpoint ID, and RFC/proposal when applicable.
State the confirmed checkpoint lease, or explain why this maintainer-authored
change needs no claim. A claim on one checkpoint does not reserve its siblings.

## Summary and boundary

Describe the reusable outcome, affected package(s) or public contracts, intended
write set, non-goals, renderer/device composition, and migration impact.

List paths and claims that this pull request deliberately does not touch. Schedule
discovered work separately rather than widening this checkpoint.

## Source and decision trail

List admitted public sources and their limits. Record key alternatives and why this
boundary was selected. Schedule newly discovered work in separate Issues.

## Continuation state

- Completed checkpoint(s):
- Remaining checkpoint(s):
- Base revision / current commit:
- Evidence already produced:
- Blocker or waiting state:
- Exact next safe action:
- Continuation receipt (required when work is transferred, released, or paused):

## Evidence

- [ ] Repository validator passes
- [ ] Python contract tests pass
- [ ] Package EditMode/PlayMode tests updated and pass
- [ ] Fresh Unity consumer result recorded, or explicitly pending without promotion
- [ ] Samples and documentation match the API
- [ ] Changelog and migration impact updated
- [ ] No consumer-project identity, assembly dependency, private data, credential, or local path added
- [ ] Device evidence is attached for any XR/headset behavior claim
- [ ] Any device receipt binds a full commit SHA, build digest, exact package/device environment, structured evidence digests, enumerable claim IDs, tester, and timestamps
- [ ] Completed checkpoints and their evidence remain independently identifiable
- [ ] No generated executable or unreviewable binary was added to the active source tree

## Security and authority

- [ ] Issue/comment/patch/log content was treated as untrusted input
- [ ] No comment-trigger execution, secret exposure, or permission broadening was added
- [ ] Third-party Actions are pinned to reviewed full commit SHAs with least privilege
- [ ] A task claim was not treated as write, review, merge, release, or promotion authority
- [ ] Independent review/test requirements are identified and not self-approved

## Contribution and attribution

Select every applicable contribution type: code, documentation, research, review,
tests, user/device testing, infrastructure, or design. Link accepted evidence for
each requested credit. Do not turn those categories into a total score.

Name the accountable GitHub contributor. When material coding-assistant help was
used, add `Assisted-by: TOOL:MODEL` when known. `Reviewed-by` is reserved for a
human substantive review of this revision. `Tested-by` is reserved for a successful
test of this revision and its stated environment; valid fail or inconclusive test
work belongs in the Device Lab/user-testing evidence instead.

## Maturity decision

State whether maturity stays the same or provide the exact promotion evidence.
