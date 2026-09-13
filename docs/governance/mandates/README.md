# Operating mandates

An operating mandate is the written authorization under which an Agent works in
this repository without asking a person for each step. It is the prototype-stage
instrument of the current `A0` participation path: the Agent acts under an
accountable human GitHub identity, and the mandate binds what it may do, where,
until when, and how it is revoked.

Mandates live here as JSON records validated against
[`operating-mandate.schema.json`](operating-mandate.schema.json). They are not
AgentMember records under RFC 0006; RFC 0006 membership stays proposed and inactive.
A mandate grants no GitHub write, review, merge, release, or administrative
permission, and every action it allows is still subject to repository validation,
pull-request review, and the maintainer's merge decision.

## Rules

- **Grant.** While the repository is at `G0` with one maintainer, that maintainer
  grants a mandate by recording it here with a decision note. No review window
  applies to an `A0` operating mandate. The 7-day and 14-day windows in
  `GOVERNANCE.md` continue to apply to policy and constitutional changes, including
  any move to `A1` membership.
- **Act.** An Agent holding an unexpired, unrevoked mandate performs the allowed
  actions within `resource_scope` without asking. It asks only for actions outside
  the mandate, and it never performs a forbidden action.
- **Report.** Every run ends with a public report of what changed, the validation
  result, and what only a human can do. Silence is not a report.
- **Revoke.** The principal or the maintainer revokes by setting
  `revocation.status` to `revoked` with a timestamp, or by deleting the scheduling
  trigger named in the record. Revocation needs no review window.
- **Expire.** Mandates expire; renewal is a new decision note.

## Records

| Mandate | Agent | Principal | Scope | Expires |
| --- | --- | --- | --- | --- |
| [`weekly-steward.mandate.json`](weekly-steward.mandate.json) | Claude Code weekly steward routine | `github:user:Lingkyn` | branch `claude/xr-foundry-repo-setup-tjd09q`, documentation, standards, lessons register, scripts, tests | 2026-12-31 |
