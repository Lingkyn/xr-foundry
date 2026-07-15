# Canonical nested Git consumer validation

Status: **pending for the current package revision**.

## Evidence boundary

The current package tree must be committed and pushed before this receipt can record
a passing result. Validation will target that immutable full commit SHA, not the
mutable branch head and not evidence from an earlier package tree.

The exact consumer profile must record:

- Unity Editor and package versions from the resolved lock;
- renderer and package composition;
- build target, graphics API, scripting backend, and architecture;
- XR provider/runtime and input routes where XR packages are present;
- consumer manifest and lock-file digests;
- compile, EditMode, and PlayMode result artifacts with recomputable digests.

## Required consumers

1. A clean consumer resolving all nine canonical nested Git package selectors at
   the same immutable revision.
2. A clean non-XR UGUI consumer resolving only the Core, Presentation, and UGUI
   package graph, with no XR Foundry XR package, XRI, XR Plug-in Management, or
   OpenXR dependency in its resolved lock.
3. Renderer-explicit XR graphs where an automated XR route is claimed.

## Promotion rule

The evidence publication commit may change only package-external catalogs,
profiles, documentation, and receipts. `packages/**` must be byte-for-byte
identical to the immutable revision that was tested. A machine-readable evidence
receipt must bind every verified profile to the tested revision and exact resolved
environment tuple.

Until those checks pass, catalog and compatibility entries remain pending. No
automated result proves Android build/install/open, headset rendering, controller
behavior, visual comfort, PICO, Quest, or visionOS behavior; those claims require
separate Device Lab receipts.
