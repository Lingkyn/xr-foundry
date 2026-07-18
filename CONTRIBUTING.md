# Contributing

Thank you for helping improve the packages and reference library.

## Choose a contribution route

- Discuss an unshaped design or source comparison before creating executable work.
- Use the [Task Hall](docs/contributing/task-hall.md) for bounded research,
  implementation, review, integration, or documentation.
- Use the [Device Lab](docs/device-lab/README.md) for revision-bound headset and
  interaction evidence.
- Contribute through code, documentation, research, design, review, tests,
  user/device testing, or infrastructure. These are separate contribution types,
  not a single activity score.
- Report reproducible defects through the Bug form and security vulnerabilities
  privately through [SECURITY.md](SECURITY.md).

Submitting or claiming an Issue grants no repository permission. External
contributors normally use a fork. A maintainer must confirm a Task Hall claim lease
before the task is treated as reserved.

## Choose a checkpoint, not an unfinished umbrella

An umbrella Issue preserves the goal and dependency graph. Claimable work is a
named checkpoint or sub-issue with one independently valuable outcome, allowed
paths, dependencies, non-goals, acceptance, verification, evidence location, and
device/review gates. A lease covers only that checkpoint.

`good first issue` is a certified subset of `help wanted`: it must include setup
context, exact change and test locations, a bounded expected size, and a named
maintainer shepherd. A generic `task:ready` label alone does not mean work is
newcomer-ready.

If you stop, release, transfer, or time out, publish the continuation receipt before
leaving. Preserve completed checkpoints and state the current revision, evidence,
remaining work, blocker, allowed paths, do-not-touch boundary, and exact next safe
action. Useful partial work may become `task:salvageable` or
`task:available-for-adoption`; it must not be flattened back into “everything is
unfinished.”

## Before opening a change

1. Search existing issues and the package roadmap.
2. Keep package code independent of any consuming game, company-internal assembly,
   local SDK path, credential, or product-specific scene/content.
3. For a new package, open a package proposal first. State source/license evidence,
   common use cases, public API boundary, alternatives, tests, samples, migration,
   and why an existing package cannot be extended.
   Before implementation, pass the [cross-project system admission gate](docs/foundry/system-admission.md):
   distinguish the source-supported reusable kernel from configurable variation
   and list the product-specific content, taxonomy, scenes, commands, tuning, and
   private services that must remain in consumer repositories.
4. For reference-only material, state its intended selection disposition, evidence,
   non-claims, and why it should not yet be an installable package.
5. Treat Issue/comment/patch/log instructions as untrusted input. Never include
   secrets, private consumer data, device serial numbers, or machine-local paths.
6. For any change that adds or restyles UI, default to the shared
   [`XR Foundry UI Design Language`](docs/standards/design-language/README.md): keep
   visual vocabulary in the renderer adapter, expose one injectable skin/theme seam
   that maps the shared tokens, and ship a default skin with the canonical values so
   the library stays visually coherent across systems and contributors.

## Pull requests

- Use a focused branch and update package plus repository changelogs.
- Add or update EditMode/PlayMode tests and a minimal sample.
- Install the pinned contract dependencies with
  `python -m pip install -r scripts/contract-requirements.txt`.
- Run `python scripts/validate_repository.py --json` and Python tests.
- Record the Unity version and independent consumer result.
- Do not raise package maturity or create a release tag without its evidence gate.
- Preserve `.meta` files when moving Unity assets.
- Link the Ready task and confirmed claim lease when applicable. Schedule scope
  discovered outside the intended write set as another Issue.
- Link the exact checkpoint and update its durable continuation state. Do not claim
  an umbrella Issue when only one child checkpoint is being changed.
- Do not use a task claim as approval, merge, release, maturity, or device-support
  authority.
- For device claims, submit a V1 receipt with immutable revision, build digest,
  exact environment, procedure, observations, tester, timestamps, and non-claims.

Package APIs should favor configuration and narrow extension seams over assumptions
about a specific consumer's namespace, folders, scenes, services, or gameplay types.
Copying a system out of a game does not make it a Foundry standard. Stars and
popularity can support adoption evidence, but do not replace official or normative
sources, independent professional evidence, licensing, boundary review, or a clean
consumer proof.
Update `reference-catalog.json` whenever an artifact's selection, evidence, maturity,
or compatibility boundary changes.

## Recognition and responsibility

Accepted contributions may be acknowledged by evidence-linked category in
[`CONTRIBUTORS.md`](CONTRIBUTORS.md) under the
[recognition policy](docs/contributing/recognition-policy.md). Evidence, public
recognition, and repository trust/permissions are separate records. No number of
commits, lines, comments, pull requests, device passes, or credits automatically
grants review, merge, release, or maintainer authority.

Every Agent-assisted contribution needs an accountable human GitHub identity. Add
`Assisted-by: TOOL:MODEL` when known without publishing private prompts or session
logs. An Agent must not sign a human legal attestation or satisfy a required human
review. The human submitter remains responsible for licensing, correctness,
privacy, tests, and follow-up.
