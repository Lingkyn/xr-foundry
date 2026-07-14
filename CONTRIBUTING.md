# Contributing

Thank you for helping improve the packages and reference library.

## Before opening a change

1. Search existing issues and the package roadmap.
2. Keep package code independent of any consuming game, company-internal assembly,
   local SDK path, credential, or product-specific scene/content.
3. For a new package, open a package proposal first. State source/license evidence,
   common use cases, public API boundary, alternatives, tests, samples, migration,
   and why an existing package cannot be extended.
4. For reference-only material, state its intended selection disposition, evidence,
   non-claims, and why it should not yet be an installable package.

## Pull requests

- Use a focused branch and update package plus repository changelogs.
- Add or update EditMode/PlayMode tests and a minimal sample.
- Run `python scripts/validate_repository.py --json` and Python tests.
- Record the Unity version and independent consumer result.
- Do not raise package maturity or create a release tag without its evidence gate.
- Preserve `.meta` files when moving Unity assets.

Package APIs should favor configuration and narrow extension seams over assumptions
about a specific consumer's namespace, folders, scenes, services, or gameplay types.
Update `reference-catalog.json` whenever an artifact's selection, evidence, maturity,
or compatibility boundary changes.
