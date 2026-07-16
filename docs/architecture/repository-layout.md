# Repository Information Architecture

Status: accepted initialization architecture.

XR Foundry is a multi-engine public reference repository whose first implemented
collection is Unity. Its repository tree must help a person or Agent discover one
reusable system without erasing the independent package boundaries needed by a
consumer.

## Canonical tree

```text
packages/
  unity/
    foundations/
      com.lingkyn.project-initializer/
      com.lingkyn.xr-baseline/
    systems/
      inventory/
        com.lingkyn.inventory.core/
        com.lingkyn.inventory.unity/
        com.lingkyn.inventory.presentation/
        com.lingkyn.inventory.ugui/
        com.lingkyn.inventory.uitoolkit/
        com.lingkyn.inventory.xr.ugui/
        com.lingkyn.inventory.xr.uitoolkit/
      persistence/
        com.lingkyn.persistence.core/
        com.lingkyn.persistence.unity/
docs/
  architecture/
  standards/
  validation/
scripts/
tests/
```

Future engine collections may add sibling roots such as `packages/unreal/` or
`packages/godot/` only when an implementation exists. Roadmap intent is not an
empty directory.

The tree is canonical but the reference knowledge is version-adaptive. Engine and
package-version variations are recorded as evidence profiles, generated package
revisions, or bounded conditional code; they do not create duplicate root layouts.
See [`version-adaptive-reference-model.md`](version-adaptive-reference-model.md).

## Discovery and dependency rules

- The public landing page presents one row per capability family. A family page
  progressively discloses its installable modules and recommended compositions.
- `package-catalog.json` and `reference-catalog.json` retain one entry per package
  because version, maturity, dependencies, evidence, and install paths differ.
- Every package leaf directory equals its `package.json` name. The catalog path is
  the repository-relative source of truth; package IDs do not encode repository
  folders.
- Unity mounts an installed package at `Packages/<package-id>` regardless of its
  source-repository path. Runtime `AssetDatabase` paths therefore continue to use
  `Packages/com.lingkyn...`, not `packages/unity/...`.
- Product-specific code, private planning, credentials, device identifiers, and
  consumer assets do not belong under this public tree.

## Git UPM layout

Unity supports a package in a repository subfolder through the `path` query. New
consumer pins use the canonical nested path before the revision anchor:

```text
https://github.com/Lingkyn/xr-foundry.git?path=/packages/unity/systems/inventory/com.lingkyn.inventory.core#<full-commit-sha>
```

This is the repository's only active package layout. Because XR Foundry is still
being initialized, it does not publish an old-path compatibility layer,
duplicate manifests, redirects, or symlink shims. Git history remains history;
it is not an additional supported repository surface.

This no-shim rule concerns repository structure. It does not prevent an Agent from
generating and validating a new package revision for another Unity or dependency
version from the same public reference standard.

Unity Package Manager does not support a Git URL as a package-to-package
dependency. Until these packages are published to a compatible registry, a Git
consumer must list every selected `com.lingkyn.*` sibling directly and pin them to
the same reviewed full commit SHA. Fetch duplication and install performance are
consumer-visible tradeoffs of this evaluation route.

## Change policy

Changing a package path requires one reviewable slice that updates the machine
catalogs, all public install examples, reference evidence paths, repository
validation, and a clean immutable Git consumer. A directory move alone is not a
completed architecture change.

## Positive public sources

- [Unity Manual: Git dependencies](https://docs.unity3d.com/Manual/upm-git.html)
  defines subfolder `path` syntax, path/revision ordering, multi-package Git
  behavior, and the package-to-package Git dependency limitation.
- [Unity-Technologies/Graphics](https://github.com/Unity-Technologies/Graphics)
  is a large first-party multi-package repository using a common `Packages/`
  surface while retaining independent package manifests.
- [vrm-c/UniVRM releases](https://github.com/vrm-c/UniVRM/releases) document
  multiple independently installed UPM packages under nested `Packages/...` paths.

These sources establish feasibility and professional precedent. XR Foundry's
engine/foundation/system taxonomy is its own public information-architecture
decision and remains subject to repository validation.
