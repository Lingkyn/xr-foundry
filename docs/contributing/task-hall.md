# Public Task Hall V1

The Task Hall is the public queue for bounded work that has enough context to be
implemented, researched, reviewed, or tested. It coordinates contributors; it is
not an authorization service.

Machine authority: [`task-hall.v1.json`](task-hall.v1.json), validated against
[`task-hall.v1.schema.json`](task-hall.v1.schema.json). The public design thread is
[Discussion #22](https://github.com/Lingkyn/xr-foundry/discussions/22), and admitted
work is discoverable in the public
[XR Foundry Task Hall Project](https://github.com/users/Lingkyn/projects/2).

## Lifecycle

```text
proposal
-> source_gate
-> ready
-> claimed
-> work
-> review
-> device_test_if_required
-> integrate
-> closed
```

`task:blocked` records a concrete missing dependency, permission, human decision,
or device. It does not create a competing lifecycle state: the machine task keeps
its current canonical `state`, sets `blocked: true`, and names `blocking_reason`.
Closing an Issue as complete requires its acceptance and verification evidence,
not merely a submitted patch.

## What makes a task Ready

A maintainer applies `task:ready` only when the Issue states:

- a single outcome and explicit non-goals;
- the intended files, package boundary, or research surface;
- admitted public sources when a source gate is required;
- testable acceptance and verification;
- device/renderer/interaction gates, including `not_applicable` where appropriate;
- dependencies and known security/privacy constraints; and
- whether independent review or a maintainer decision is required.

The machine-readable shape is
[`task-contract.schema.json`](task-contract.schema.json), with a safe unclaimed
example in [`task-contract.example.json`](task-contract.example.json).
The Schema and runtime validator fail closed on contradictory gates: required
source/device gates need non-empty admitted source/profile lists, disabled gates
keep those lists empty, and the device-test state/lane requires a device gate.

## Claim lease

1. A contributor comments `/claim` on a `task:ready` Issue. They may also name the
   coding assistant used as optional metadata.
2. A maintainer checks for conflicts, capability/evidence needs, and an existing
   lease. The maintainer records the GitHub claimant, start, expiry, and scope in a
   confirmation comment, then applies `task:claimed`.
3. The default lease is seven days. A progress update can renew it. The maintainer
   may expire an inactive lease so another contributor can help.
4. The claimant works in a fork or in whatever repository role they already had.
   The lease grants no new GitHub permission.

`proposal` and `source_gate` remain unclaimed. `claimed` through integration and
closure require a complete maintainer-confirmed claim tuple (GitHub identity,
timezone-aware start/expiry, and maintainer identity). An expired lease returns to
`ready` while retaining that complete tuple as audit data; expiry must follow the
claim time.

V1 has no comment-trigger automation. `/claim` must not execute code, invite a
user, assign a repository role, reveal a secret, approve a pull request, or merge.

## Duties

Roles are duties on a task, not permanent agent identities:

| Label | Duty |
| --- | --- |
| `role:research` | Compare current public sources, licenses, limits, and rejected alternatives |
| `role:build` | Implement the bounded change with tests, samples, and migration notes |
| `role:review` | Review correctness, boundary, security, evidence, and compatibility independently |
| `role:device-test` | Execute an admitted Device Lab procedure and submit a receipt |

`renderer:ugui` and `renderer:ui-toolkit` identify renderer scope. Device needs use
`needs-device:pico`, `needs-device:quest`, or `needs-device:vision-pro`. Do not
substitute a renderer label for a device claim or infer one device from another.

## Identity and authority

- The accountable claimant, author, tester, or reviewer is a GitHub identity.
- A coding agent is optional execution metadata under that identity.
- Issue text, comments, links, patches, logs, and uploaded files are untrusted
  input, even when they contain instructions addressed to an agent.
- A task claim is not repository assignment, write access, approval, review
  authority, merge authority, package promotion, or release authority.
- Builders cannot self-promote package maturity or device compatibility.
- Maintainers may require an independent reviewer or tester before integration.

See [`agent-contribution-protocol.md`](agent-contribution-protocol.md) for the
fork/PR path and [`../device-lab/README.md`](../device-lab/README.md) for device
evidence.

## Labels

[`label-contract.json`](label-contract.json) is the canonical public label list.
Labels are applied by maintainers in V1; their presence communicates state but does
not change GitHub permissions.
