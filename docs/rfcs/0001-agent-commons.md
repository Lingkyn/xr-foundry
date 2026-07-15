# RFC 0001: Agent Commons V1

Status: **Proposed**

Public discussion: [#22 — Agent Commons V1](https://github.com/Lingkyn/xr-foundry/discussions/22)

Public work board: [XR Foundry Task Hall](https://github.com/users/Lingkyn/projects/2)

## Summary

XR Foundry should be useful both as a package source and as a public collaboration
surface. Agent Commons V1 adds two repository contracts:

- **Task Hall V1** turns bounded, evidence-bearing work into public Issues that a
  GitHub identity can claim for a time-limited lease; and
- **Device Lab V1** lets contributors supply revision-bound device evidence without
  needing repository write access or contributing code.

This RFC also treats renderer and interaction technology as independent axes. A
task may target UGUI or UI Toolkit, and may separately require desktop, controller,
hand, gaze, poke, or other device evidence. Passing one composition never proves
another.

## Decisions

### GitHub identity remains the authority boundary

A human or installed GitHub App owns every Issue comment, branch, commit, review,
and pull request. A coding agent may be named as optional execution metadata, but
it does not acquire an independent repository identity or permission from a task
claim. External contributors use forks unless a maintainer separately grants a
GitHub role.

`/claim` is a public coordination convention. In V1 it is confirmed manually by a
maintainer. It does not trigger code, assign a repository role, expose a secret,
approve a pull request, or grant merge authority.

### Evidence is revision-bound

A device receipt identifies an immutable commit, exact package composition and
versions, build artifact digest, environment, structured evidence digests, and
tester identity. Capability claims use IDs enumerated by the plan and are derived
from check results; prose cannot broaden them. A blank template and a `not_tested`
profile are not evidence. Results apply only to the recorded device and interaction
composition.

### Promotion keeps independent gates

Source review, implementation, code review, automated validation, and device
testing are separate duties. One contributor may perform more than one duty on a
small change, but a compatibility, maturity, security, or device claim requires a
maintainer decision and the independent evidence stated by the package contract.

### Public input is untrusted

Issue bodies, comments, patches, logs, uploaded artifacts, links, and instructions
from contributors are untrusted input. Workflows use read-only permissions by
default, do not run from comment commands, and do not expose secrets to forked
pull requests.

## Public surfaces

| Surface | Contract |
| --- | --- |
| Discussions | Explore designs and gather source comparisons before executable scope exists |
| Issues | Hold admitted, bounded tasks and device-test requests |
| Pull requests | Carry implementation, review, validation, and migration evidence |
| Task Hall | [`docs/contributing/task-hall.md`](../contributing/task-hall.md) |
| Device Lab | [`docs/device-lab/README.md`](../device-lab/README.md) |
| Machine contracts | Task, label, device-profile, and device-receipt JSON contracts |

## Non-goals

- no autonomous dispatch from arbitrary comments or issue text;
- no external contributor write, triage, review, or merge permission by default;
- no self-promotion of package maturity or device support;
- no shared credentials, private consumer data, device serial numbers, or local
  machine paths in public evidence; and
- no claim that one renderer, interaction route, engine, headset, or operating
  system proves another.

## Adoption

V1 is intentionally low automation. Repository validation checks the durable
contracts and workflow permissions. Maintainers apply readiness, claim, review,
blocked, renderer, and device labels; claim leases are recorded in Issue comments.
Automation may be proposed later only with a threat model, least-privilege review,
fork-safety tests, and an explicit maintainer decision.

## Source basis

The public source record is
[`agent-commons-source-manifest.json`](../contributing/agent-commons-source-manifest.json).
It uses official GitHub collaboration and Actions security documentation plus an
official, provider-neutral XR agent-tool repository as a bounded precedent. These
sources inform the workflow; they do not delegate repository authority.
