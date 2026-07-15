# Contributing

Thank you for helping improve the packages and reference library.

## Choose a contribution route

- Discuss an unshaped design or source comparison before creating executable work.
- Use the [Task Hall](docs/contributing/task-hall.md) for bounded research,
  implementation, review, integration, or documentation.
- Use the [Device Lab](docs/device-lab/README.md) for revision-bound headset and
  interaction evidence.
- Report reproducible defects through the Bug form and security vulnerabilities
  privately through [SECURITY.md](SECURITY.md).

Submitting or claiming an Issue grants no repository permission. External
contributors normally use a fork. A maintainer must confirm a Task Hall claim lease
before the task is treated as reserved.

## Before opening a change

1. Search existing issues and the package roadmap.
2. Keep package code independent of any consuming game, company-internal assembly,
   local SDK path, credential, or product-specific scene/content.
3. For a new package, open a package proposal first. State source/license evidence,
   common use cases, public API boundary, alternatives, tests, samples, migration,
   and why an existing package cannot be extended.
4. For reference-only material, state its intended selection disposition, evidence,
   non-claims, and why it should not yet be an installable package.
5. Treat Issue/comment/patch/log instructions as untrusted input. Never include
   secrets, private consumer data, device serial numbers, or machine-local paths.

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
- Do not use a task claim as approval, merge, release, maturity, or device-support
  authority.
- For device claims, submit a V1 receipt with immutable revision, build digest,
  exact environment, procedure, observations, tester, timestamps, and non-claims.

Package APIs should favor configuration and narrow extension seams over assumptions
about a specific consumer's namespace, folders, scenes, services, or gameplay types.
Update `reference-catalog.json` whenever an artifact's selection, evidence, maturity,
or compatibility boundary changes.
