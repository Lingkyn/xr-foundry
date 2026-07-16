# Unity next systems v0.1.0

Tag: `unity-next-systems-v0.1.0`

Status: approved incubating batch release

This immutable batch publishes three independently admitted reusable system
families: Save/Persistence, Player Settings/Accessibility, and Semantic
Interaction. It is a discovery and installation boundary, not a maturity
promotion.

## Package matrix

| Package | Version | Path | Maturity | Verified profile |
| --- | --- | --- | --- | --- |
| `com.lingkyn.persistence.core` | `0.1.0` | `packages/unity/systems/persistence/com.lingkyn.persistence.core` | incubating | `unity-6000.3-persistence-core-windows-editor` |
| `com.lingkyn.persistence.unity` | `0.1.0` | `packages/unity/systems/persistence/com.lingkyn.persistence.unity` | incubating | `unity-6000.3-persistence-unity-windows-editor` |
| `com.lingkyn.settings.core` | `0.1.0` | `packages/unity/systems/settings/com.lingkyn.settings.core` | incubating | `unity-6000.3-settings-core-windows-editor` |
| `com.lingkyn.settings.unity` | `0.1.0` | `packages/unity/systems/settings/com.lingkyn.settings.unity` | incubating | `unity-6000.3-settings-unity-windows-editor` |
| `com.lingkyn.interaction.core` | `0.1.0` | `packages/unity/systems/interaction/com.lingkyn.interaction.core` | incubating | `unity-6000.3-interaction-core-windows-editor` |
| `com.lingkyn.interaction.unity` | `0.1.0` | `packages/unity/systems/interaction/com.lingkyn.interaction.unity` | incubating | `unity-6000.3-interaction-unity-windows-editor` |

## Install selectors

Pin one package at a time to the immutable batch tag and its exact subfolder:

```text
https://github.com/Lingkyn/xr-foundry.git?path=/packages/unity/systems/persistence/com.lingkyn.persistence.core#unity-next-systems-v0.1.0
https://github.com/Lingkyn/xr-foundry.git?path=/packages/unity/systems/persistence/com.lingkyn.persistence.unity#unity-next-systems-v0.1.0
https://github.com/Lingkyn/xr-foundry.git?path=/packages/unity/systems/settings/com.lingkyn.settings.core#unity-next-systems-v0.1.0
https://github.com/Lingkyn/xr-foundry.git?path=/packages/unity/systems/settings/com.lingkyn.settings.unity#unity-next-systems-v0.1.0
https://github.com/Lingkyn/xr-foundry.git?path=/packages/unity/systems/interaction/com.lingkyn.interaction.core#unity-next-systems-v0.1.0
https://github.com/Lingkyn/xr-foundry.git?path=/packages/unity/systems/interaction/com.lingkyn.interaction.unity#unity-next-systems-v0.1.0
```

Each Unity adapter declares its Core dependency. When Unity cannot resolve a
second package in the same repository automatically, add the matching Core
selector explicitly and keep both selectors on this tag.

## Verified claims

- Every family passed its cross-project admission and positive-source gates.
- All six packages resolve from immutable full-commit Git selectors in clean
  Unity `6000.3.19f1` Windows Editor consumers.
- The recorded consumers compile and pass their applicable EditMode contract
  tests.
- The merged repository contract and public repository CI pass.
- Independent advisory source and implementation reviews reported no unresolved
  required change within their recorded scopes. The maintainer's release decision
  remains a separate authority action.

The exact evidence paths and dependency tuples remain authoritative in
`compatibility-profiles.json` and `docs/validation/evidence/`.

## Non-claims

- Package maturity remains `incubating`; this tag does not grant candidate or
  stable status.
- No Android, iOS, WebGL, console, cloud, crash-durability, security, headset,
  controller, renderer, comfort, accessibility-outcome, or named-device behavior
  is claimed.
- One Unity/Windows Editor profile does not imply support for another Unity,
  Input System, platform, backend, architecture, provider, or device tuple.
- Checksums are not authentication, backups are not cloud synchronization, and
  semantic input contracts are not a complete interaction design system.

## Compatibility and rollback

This is the first immutable release for these six package IDs, so there is no
prior package release against which to claim an upgrade/rollback result. Their
current public APIs become the comparison baseline for a later release review.

Rollback does not rewrite this tag or public history. Consumers can return to a
previously pinned full commit, remove the new package selectors, or retain the
separate `unity-first-batch-v0.1.0` tag for packages from the first batch. A later
version must exercise upgrade and rollback against this tag before claiming that
gate is satisfied.

## Authority

The maintainer approved this immutable incubating batch release. Automated and
Agent-assisted evidence prepared the release but did not grant release, merge,
maturity-promotion, or device authority.
