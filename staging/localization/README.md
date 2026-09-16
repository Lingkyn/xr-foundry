# Localization staging

Status: **staging material, not a live package.** Nothing here is in the package
catalog, a batch, a compatibility profile, or a release. The manifest is named
`package.staging.json` on purpose so repository validation does not treat this
directory as a live `com.lingkyn.*` package.

This directory holds the first implementation of the Localization family named in
`docs/foundry/queue/next-batch.json` (`NEXT-LOCALIZATION`). It exists because the
production line cannot register a new package without a verified Unity
compatibility profile, and no Unity run has happened since the last recorded
evidence commit. The code is written and tested on paper; it has not compiled.

## What is here

| Path | Content |
| --- | --- |
| `com.lingkyn.localization.core/Runtime/LocalizationCore.cs` | Engine-light Core: `LocaleId`, `MessageId`, `MessageTemplate`, `CldrPluralRules`, `MessageTable`, `LocalizationCatalog`, `LocalizationValidator`, results and diagnostics |
| `com.lingkyn.localization.core/Tests/Editor/LocalizationCoreContractTests.cs` | 28 EditMode tests mapped in `docs/standards/localization/coverage-map.json` |
| `docs/standards/localization/` | Standard README, source manifest, verification contract, coverage map, admission and blueprint drafts |

## How it moves into the tree

One Unity run turns this into a live package. The person or Agent with the Editor:

1. Copies `docs/standards/localization/admission.draft.json` to
   `docs/foundry/admissions/localization.v1.json` (a maintainer decision) and
   `blueprint.draft.json` to `docs/foundry/blueprints/localization-core.v1.json`.
2. Runs `python scripts/scaffold_unity_package.py docs/foundry/blueprints/localization-core.v1.json --output-root . --write`,
   then replaces the generated scaffold sources with the files here and renames
   `package.staging.json` to `package.json`, adding `.meta` files for every asset.
3. Adds the package to `package-catalog.json`, `component-catalog.json`,
   `capability-registry.json`, a building batch, and the lessons-register
   dispositions listed in `docs/standards/localization/README.md`.
4. Runs `python scripts/run_unity_gates.py --host com.lingkyn.localization.core`
   and records the compatibility profile from the receipt.
5. Runs the repository contract and the merge-readiness verdict, then opens the
   pull request.

Until step 4 happens, every claim about this code is "authored, unexecuted".
