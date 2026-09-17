# Milestones: repository, community, organization

XR Foundry does not become an organization in one step. It becomes a qualified
GitHub repository first, then a community that other people and Agents contribute
to, then an organization with more than one accountable maintainer, and only after
that anything with a treasury. Each milestone is a batch of concrete deliverables
with a done criterion a reader can check. The governance stages `G0` to `G4` in
[`governance/governance-model.v1.json`](governance/governance-model.v1.json) and
[RFC 0004](rfcs/0004-progressive-governance.md) set the thresholds; this page
turns them into work.

Status words: **done** means the deliverable exists in the tree and repository
validation or a receipt proves it; **partial** names what is missing; **open**
means nothing exists yet. A milestone is reached when every batch in it is done,
not when most are.

## M1: a qualified GitHub repository (governance stage G0)

Done criterion: a stranger with a Unity Editor can clone the default branch,
install any package by Git URL at a tagged release, run the tests green, read
what is and is not proven, and land a routine change without talking to anyone.

### Batch 1a: trust surface

| Deliverable | Status |
| --- | --- |
| `LICENSE`, `README.md` install matrix, `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `SECURITY.md`, `SUPPORT.md`, `CODEOWNERS`, issue, discussion, and pull-request templates | done |
| `AGENTS.md` and [`for-agents.md`](for-agents.md): the same route for a coding Agent as for a person | done |
| [`start-here.md`](contributing/start-here.md): one install command, one verdict command, five-line pull request | done |
| [`checked-claims.md`](validation/checked-claims.md): every claim kind marked machine-checked or self-declared | done |

### Batch 1b: green main

| Deliverable | Status |
| --- | --- |
| Repository contract and Python tests on every push and pull request (`repository-contract`) | done |
| Merge-readiness verdict on every pull request (`merge-readiness`) | done |
| `repository-contract` and `merge-readiness` set as required checks on the default branch, auto-merge enabled | open: owner-only repository setting |
| Unity consumer tests running in CI (`unity-consumer-tests`) | partial: workflow exists; needs the Unity license secret |
| Every compatibility profile bound to the current default-branch commit | partial: fifteen profiles bound to the July evidence commit; every later package change is authored, unexecuted |

### Batch 1c: releases people can pin

| Deliverable | Status |
| --- | --- |
| Batch release notes with pinned selectors and non-claims ([`releases/`](releases/)) | done for `unity-next-systems-v0.1.0` |
| Git tags that match every batch `release_tag` and point at the evidence commit | open: maintainer-only; nothing in the tree proves a tag exists |
| Git-URL install of every catalogued package verified at a tag from a clean consumer project | partial: verified once at the evidence commit, not at a tag |
| Upgrade and rollback exercise for Persistence, Settings, and Interaction | open |

### Batch 1d: the seven high-frequency families in the tree

| Deliverable | Status |
| --- | --- |
| Foundations, Interaction, Settings, Persistence, Inventory in `packages/` with coverage maps that pass `validate_coverage_map_claims` | done |
| Localization promoted from `staging/localization` after one Unity run and a signed admission | partial: 39 tests authored, unexecuted |
| Audio events promoted from `staging/audio` after one Unity run and a signed admission | partial: 53 tests authored, unexecuted |
| Every coverage-map clause covered or its gap named on the open-work board | done |

### Batch 1e: one real device

| Deliverable | Status |
| --- | --- |
| One Device Lab receipt for `inventory-world-space-ui-v1` on one named headset, validated with `--device-lab-receipt` | open: the only item no Agent can do |
| One device profile leaves `not_tested` | open, follows the receipt |

### Batch 1f: proof the route works for a stranger

| Deliverable | Status |
| --- | --- |
| A cold-start receipt: someone outside the maintainer account follows `start-here.md` from a fork and lands a routine change through the verdict, with the time it took recorded | open |
| Open-work board generated from the tree (`scripts/open_work.py`) | done |
| At least five `good first issue` items that meet the certified subset in `CONTRIBUTING.md` | open: needs Issues, which the mandate forbids an Agent to create |

## M2: a community (governance stage G1)

Done criterion (the `G1` thresholds): 90 days of observation after M1, at least
three distinct human contributors of whom two are not the maintainer, at least
two contribution types, two public deliberations resolved, and a maintainer who
responds. Every number is read from the public record, not asserted.

### Batch 2a: routine contributions merge without the maintainer

| Deliverable | Status |
| --- | --- |
| Process-decided merge in effect: routine change on a mandated branch merges by auto-merge on a `ready` verdict (DLB-0002) | partial: rule written, objection window closes 2026-09-29; needs the 1b settings |
| First routine change by a non-maintainer merged through the verdict alone | open |
| First lazy-consensus resolution of a deliberation record recorded with `decided_by: process:<mandate>` | open: DLB-0001 window closes 2026-09-22 |

### Batch 2b: contribution types beyond code

| Deliverable | Status |
| --- | --- |
| Device Lab receipts from at least two testers on at least two device profiles | open |
| One independent review receipt under `validation/reviews/` from a reviewer who is not the executor | open |
| One documentation or research contribution admitted as a positive public source | open |
| `CONTRIBUTORS.md` recognition rows linked to evidence | partial: policy exists, no external rows |

### Batch 2c: the next three families through the queue

| Deliverable | Status |
| --- | --- |
| Locomotion and comfort, scene flow, XR UI shell: source manifest, verification contract, admission draft, staged Core with a coverage map, in that order (ranks 8 to 10 in [`../ROADMAP.md`](../ROADMAP.md)) | open |
| At least one of the three authored, reviewed, or device-tested by someone other than the maintainer or the steward Agent | open |

### Batch 2d: public deliberation by outsiders

| Deliverable | Status |
| --- | --- |
| One RFC or governance proposal opened by a non-maintainer and resolved on record | open |
| A second operating mandate for an Agent run by someone other than the maintainer, scoped to one family | open |

## M3: an organization (governance stage G2)

Done criterion (the `G2` thresholds): 180 days of observation, two independent
active maintainers, one tested backup release or recovery, three resolved public
deliberations, and the `G1` numbers still holding.

### Batch 3a: a second accountable maintainer

| Deliverable | Status |
| --- | --- |
| A second maintainer with review and merge rights, recorded in `CODEOWNERS` and `GOVERNANCE.md`, who has merged at least one non-routine change | open |
| A recovery drill: the second maintainer cuts a batch release from a clean clone without the first maintainer, recorded as a receipt | open |

### Batch 3b: maturity beyond incubating

| Deliverable | Status |
| --- | --- |
| At least two families pass the candidate gate in [`../ROADMAP.md`](../ROADMAP.md) (independent consumer, device evidence, API review) | open |
| One family reaches stable with an upgrade and rollback record | open |

### Batch 3c: governance adopted, not proposed

| Deliverable | Status |
| --- | --- |
| RFC 0004 and RFC 0006 move from `Proposed` to adopted through their own deliberation records | open |
| The steward mandate renewed or replaced by a recorded maintainer decision rather than extended by the Agent it covers | open |
| The rules that decide merges cannot be changed by the branch they cover without a resolved record (checked, not promised) | partial: `check_governance_boundary` covers governance paths; the CI job does not yet pass the record |

## M4 and beyond: treasury and on-chain (G3, G4)

Nothing here starts before M3 is reached. The repository stays token-neutral, no
wallet or contract exists, and the first treasury or on-chain step is its own
RFC with a 14-day window. This page lists no deliverables for it on purpose.

## How the work is picked up

Each open cell that a contributor can act on is an item in
[`contributing/work-items.json`](contributing/work-items.json), written so
that a person, the steward Agent, or any other coding assistant can take it with
no session context: what to read, where to write, what to do, and the commands
that prove it. [`contributing/work-items.md`](contributing/work-items.md) is
the protocol.

## How this page is kept honest

An Agent under mandate updates a status cell only with a link to the tree, a
receipt, or a public record that proves it. A cell that says `done` without a
proof path is a defect. The open-work board lists the open cells that need no
maintainer; the rest are the items only the maintainer can do: repository
settings, the Unity license secret or an Editor run, release tags, Issues, the
device receipt, and every promotion decision.
