# Version-Adaptive Reference Model

Status: accepted initialization architecture.

XR Foundry separates a reusable reference from a concrete package revision. The
reference architecture, contracts, tests, and samples can be used as raw material
for more than one engine or package version. An installable package revision is
concrete: its manifest, source, dependencies, and evidence describe one bounded
implementation profile.

This distinction lets an Agent adapt Inventory to a Unity 2022, Unity 6.0, Unity
6.3, or later consumer without pretending that one untested binary already
supports every target.

## Two layers

| Layer | Version meaning | Allowed claim |
| --- | --- | --- |
| Reference and generation layer | Version-adaptive system architecture, semantic contracts, tests, samples, and adaptation rules | Suitable raw material for a target-specific candidate |
| Installable package layer | One package manifest and immutable revision with concrete engine and dependency versions | Only the exact evidence recorded for that profile |

The first layer is not a package compatibility claim. The second layer is not the
limit of what the reference can generate.

## Agent routing contract

Before selecting or generating code, an Agent records the consumer tuple:

1. engine and exact editor version;
2. renderer and UI framework;
3. installed package versions and lock state;
4. XR interaction, runtime/provider, build target, and input route when relevant;
5. requested capability and required evidence, including any named device.

The Agent then follows one route:

- **Exact verified profile:** install the immutable revision and rerun the
  consumer's own resolution, compile, and tests.
- **No exact verified profile:** select `raw_material`, regenerate or adapt a
  target-specific candidate from the reference contracts, and keep every support
  or device claim pending.
- **Small API delta:** Assembly Definition Version Defines or a narrow adapter may
  be used only when each claimed profile passes its own matrix.
- **Material API, serialization, asset, or behavior delta:** create a separately
  reviewable package revision or consumer adapter. Do not hide the difference in
  a fallback, redirect, or ambiguous renderer package.

After generation, the candidate must pass package resolution, compilation,
package tests, an independent consumer, and any applicable build/device gates.
Only then may `compatibility-profiles.json` record it as verified.

Automated claims require a structured, tuple-bound compile result and parseable
NUnit XML from a real Unity consumer lock graph. Device-runtime claims additionally
require a passing receipt in `docs/device-lab/receipts/*.json`; that receipt is
validated through the full Device Lab contract and must match the compatibility
profile, public revision, manifest, lock, package graph, runtime, build, input, and
device tuple exactly. A log sentence, arbitrary URL, local-only commit, or sibling
profile is not transferable evidence.

## Evidence isolation

Evidence never transfers automatically across:

- Unity editor versions;
- package dependency versions;
- UGUI and UI Toolkit renderers;
- desktop, Android, and visionOS build targets;
- XR providers, input routes, or named devices; or
- generated source revisions.

One source tree may support multiple profiles when conditional code remains
bounded and all profiles are tested. Otherwise, SemVer revisions preserve the
different implementations. XR Foundry does not create duplicate package folders
or an old-path compatibility tree merely to represent version history.

## Machine surface

[`compatibility-profiles.json`](../../compatibility-profiles.json) records exact
verified and pending profiles. Package manifests remain the install authority for
their own revisions; the profile catalog adds evidence and claim boundaries for
Agent selection.

This file is an evidence ledger, not a maintained version matrix. XR Foundry keeps
one current package implementation. An unmatched Unity, renderer, or tool tuple
uses the reference as raw material to generate a separately validated candidate;
it does not create a parallel maintained version tree.

## Official basis

- [Unity package manifest reference](https://docs.unity3d.com/Manual/upm-manifestPkg.html)
  defines `unity` as the lowest compatible Editor version and requires dependency
  entries to name specific SemVer versions rather than ranges.
- [Unity Assembly Definition file format](https://docs.unity3d.com/Manual/assembly-definition-file-format.html)
  provides Version Defines for bounded version-dependent compilation.
- [Unity package versioning](https://docs.unity3d.com/Manual/upm-semver.html)
  treats manifest constraints, assemblies, APIs, and referenced assets as part of
  the consumer-facing compatibility surface.

These mechanisms enable multiple tested profiles; they do not replace profile-
specific validation.
