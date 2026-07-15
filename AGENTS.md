# XR Foundry Agent Guide

Use this repository as a versioned package source and an evidence-backed reference
library. Do not treat it as a bag of code to copy wholesale.

## Operating sequence

1. Read `reference-catalog.json` to select the smallest relevant artifact.
2. Read the artifact's manifest, README, documentation, tests, samples, changelog,
   license, maturity, compatibility, and validation evidence.
3. Choose one disposition: `install`, `extend_public_seam`, `reference_only`,
   `raw_material`, or `reject`.
4. Keep consumer-specific assemblies, scenes, content, branding, platform adapters,
   and secrets in the consuming repository.
5. Pin installs to a reviewed immutable commit or release.
6. Run `python scripts/validate_repository.py --json`, the repository tests, and
   the consuming project's own resolution/compile/tests.
7. Require real-device evidence before claiming XR runtime, controller, comfort,
   spatial-audio, or headset behavior. For Inventory XR, use
   `docs/validation/inventory-xr-device-receipt-template.md` and validate the
   completed JSON receipt with `--device-receipt`.

For Inventory presentation, select the renderer explicitly. The neutral contract
lives in `com.lingkyn.inventory.presentation`; UGUI and UI Toolkit are sibling
adapters, with renderer-named XR compositions. Never transfer automated or device
evidence from one renderer composition to the other.

## Current implementation boundary

Unity packages are currently implemented. Unreal Engine and Godot are roadmap
directions only. Do not generate empty engine folders or claim support without a
working implementation and evidence.

## Change boundary

New reusable systems start as proposals and incubating reference entries. Compare
official sources, maintained professional implementations, strongly adopted open
source projects, and existing project raw material. Verify license, maintenance,
architecture, tests, compatibility, migration, and independent-consumer behavior;
popularity alone is not a standard.

Provider adapters must stay thin. Shared facts belong in `reference-catalog.json`,
package manifests, tests, and public documentation—not duplicated in model-specific
instruction files.
