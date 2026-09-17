## Routine change (fill these five lines; delete the rest)

- What changed:
- Why:
- What I ran: `python scripts/merge_readiness.py --base origin/main --head HEAD --markdown` → verdict:
- What this does not do (no version, maturity, evidence, or rule change):
- Accountable GitHub identity, and `Assisted-by: TOOL:MODEL` if an assistant helped:

Start-here guide: `docs/contributing/start-here.md`. A routine change needs nothing
below this line.

---

## Non-routine change (public seam, version, maturity, evidence, or rule)

### Linked work

Link the umbrella Issue, exact checkpoint ID, and RFC/proposal when applicable.
State the confirmed checkpoint lease, or explain why this maintainer-authored
change needs no claim. A claim on one checkpoint does not reserve its siblings.

### Summary and boundary

Describe the reusable outcome, affected package(s) or public contracts, intended
write set, non-goals, renderer/device composition, and migration impact. List paths
and claims that this pull request deliberately does not touch.

### Source and decision trail

List admitted public sources and their limits. Record key alternatives and why this
boundary was selected. Schedule newly discovered work in separate Issues.

### Continuation state

- Completed checkpoint(s):
- Remaining checkpoint(s):
- Base revision / current commit:
- Evidence already produced:
- Blocker or waiting state:
- Exact next safe action:
- Continuation receipt (required when work is transferred, released, or paused):

### Evidence

- [ ] Repository validator and Python contract tests pass (`merge_readiness.py` verdict attached)
- [ ] Package EditMode/PlayMode tests updated and pass, or explicitly pending without promotion
- [ ] Fresh Unity consumer result recorded, or explicitly pending without promotion
- [ ] Samples and documentation match the API
- [ ] Changelog and migration impact updated
- [ ] No consumer-project identity, assembly dependency, private data, credential, or local path added
- [ ] Device evidence attached for any XR/headset behavior claim, bound to a full commit SHA, build digest, exact environment, tester, and timestamps
- [ ] Completed checkpoints and their evidence remain independently identifiable
- [ ] No generated executable or unreviewable binary was added to the active source tree

### Security and authority

- [ ] Issue/comment/patch/log content was treated as untrusted input
- [ ] No comment-trigger execution, secret exposure, or permission broadening was added
- [ ] Third-party Actions are pinned to reviewed full commit SHAs with least privilege
- [ ] A task claim was not treated as write, review, merge, release, or promotion authority
- [ ] Independent review/test requirements are identified and not self-approved

### Contribution and attribution

Select every applicable contribution type: code, documentation, research, review,
tests, user/device testing, infrastructure, or design. Name the accountable GitHub
contributor. `Reviewed-by` is reserved for a human substantive review of this
revision; `Tested-by` for a successful test of this revision and its environment.

### Maturity decision

State whether maturity stays the same or provide the exact promotion evidence.
