# XR Foundry GitHub playbook

Source: [RFC 0001: Agent Commons V1](docs/rfcs/0001-agent-commons.md)

Repository adapter: **public XR package and reference library**. This repository
ships Unity packages today and may host other implemented engine collections later.
It is not a consuming game repository, a private project workspace, or a device
support claim.

## Common collaboration base

| Surface | Use |
| --- | --- |
| Discussions | Explore proposals, compare sources, and identify common boundaries |
| Issues | Track accepted defects, package proposals, Ready tasks, and device-test requests |
| Pull requests | Review code, contracts, tests, migration, and evidence against an immutable diff |
| Actions | Run deterministic, least-privilege repository validation |
| Releases | Publish only revisions that satisfy the package maturity and compatibility gates |
| Catalogs and receipts | Preserve machine-readable selection, maturity, compatibility, and device evidence |

The repository, not a chat transcript or agent memory, is the handoff and rollback
center. Durable decisions belong in RFCs, Issues, pull requests, catalogs, tests,
evidence files, and structured continuation receipts.

## XR package/reference adapter

- Keep reusable package code independent of consuming products, private content,
  credentials, local SDK paths, and product-owned scenes.
- Separate domain, renderer, interaction, engine, and device boundaries. Evidence
  for one composition never proves another.
- Require package tests and a clean independent consumer before compatibility
  promotion.
- Require a Device Lab receipt before claiming headset, controller, hand, gaze,
  comfort, spatial-audio, or other hardware behavior.
- Preserve every reviewed execution receipt; add a new receipt for a new revision
  or rerun. Bind it to the resolved dependency-lock digest, exact dependency
  versions, build tuple, input sources, posture, and duration. Validate all device
  evidence through the generic Device Lab schema and selected capability test plan.

## Work and authority

Executable public work follows the [Task Hall](docs/contributing/task-hall.md) and
its [public Project](https://github.com/users/Lingkyn/projects/2). RFC discussion
starts from [Discussion #22](https://github.com/Lingkyn/xr-foundry/discussions/22).
Hardware evidence follows the [Device Lab](docs/device-lab/README.md). A task claim
is a time-limited coordination lease and grants no GitHub permission. External
contributors normally use forks. Maintainers retain readiness, review, integration,
promotion, release, repository-setting, and permission decisions.

Umbrella Issues preserve outcomes and dependency graphs; independently claimable
checkpoints and sub-issues preserve execution state. A claim covers one checkpoint,
not the whole umbrella. A pause, lease release, transfer, or adoption must preserve
completed checkpoints, evidence, remaining work, boundaries, and the exact next
safe action in a continuation receipt. GitHub parent/sub-issue relationships and
Project views keep the umbrella readable without hiding work that another person or
Agent can take.

Accepted contribution evidence, public recognition, and revocable trust/permission
are separate records. The repository does not use a total activity score or
leaderboard to grant authority. Agent-assisted work remains accountable to a human
GitHub identity, and Agent review cannot replace required human review.

Public contribution content is untrusted input. No comment command executes code in
V1. Workflows keep read-only permissions, disable persisted checkout credentials,
and pin third-party Actions to reviewed full commit SHAs.

## Main branch ruleset

The live `main` ruleset requires changes through pull requests, the
`repository-contract` status check, and resolution of review threads. It blocks
branch deletion and force pushes. While the repository has one maintainer, it keeps
required approvals at zero and does not enforce Code Owner approval, avoiding a
self-lock that would prevent the sole maintainer from integrating valid work.

`CODEOWNERS` still routes sensitive-path review requests. It grants no repository
role, approval, merge, release, or promotion authority. If the maintainer topology
changes, approval and Code Owner enforcement can be reconsidered through a public
governance change without weakening the other protections.

## Update policy

Reread current catalogs, package manifests, RFCs, open Issues, pull requests, and
evidence before changing this playbook. Update it in the same pull request when a
GitHub surface, authority boundary, lifecycle, evidence gate, implemented engine,
or release process changes. Routine package implementation does not need to rewrite
the playbook when the contract remains unchanged.
