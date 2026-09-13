# Changelog

## Unreleased

- Fixed #21: `XrRigInteractionRepair` now configures `castDistance` on the caster
  each XRI 3.x `NearFarInteractor` references (resolved by the
  `ICurveInteractionCaster` interface, not a hard-coded field), keeps a longer
  authored value, and returns `XrRigInteractionRepairResult` whose diagnostics name
  every interactor it could not configure. `GenericXrRigFactory` logs each
  diagnostic as an error instead of reporting a successful Sandbox. Added three
  EditMode tests (short caster raised, missing caster reported, empty rig). The
  test assembly now references `Unity.XR.Interaction.Toolkit`. Not yet executed in
  a Unity Editor; the `unity-consumer-tests` workflow or a maintainer run must
  confirm it before the fix is cited as evidence.
- Documentation only: the package's Editor gate is now written down in
  `docs/standards/foundations/verification-contract.md` and mapped to the five
  existing EditMode tests in `docs/standards/foundations/coverage-map.json`, which
  also names the two tests still missing (Initialize Sandbox unresolved warning,
  hover visual warning).
- Added `XrBaselineDiagnostics` (Editor) and routed every by-name resolution in
  the Sandbox Editor tools through it: XRI grab interactable, teleportation area,
  line visual, tracked pose driver, affordance, scene placer, asset factory,
  enclosure, and scale-reference lookups now log one
  `xr_baseline_unresolved: <key>: <reason>` warning per key instead of silently
  skipping. `Initialize Sandbox` resets the key set first and ends with
  `xr_baseline_initialized_with_unresolved` when any key was reported, so a
  partially configured Sandbox is never logged as a clean success. Added one
  EditMode test for the once-per-key behavior. Not yet executed in a Unity Editor.
- `GrabbableHoverVisual` now logs one warning per component when XR Interaction
  Toolkit is missing, the GameObject has no `XRGrabInteractable`, or the `isHovered`
  property cannot be resolved, instead of silently never lighting up.
- Documentation only: expanded the README with a quick start, the public
  runtime/editor surface, generated-path conventions, and non-goals. No API,
  version, maturity, or evidence change.

## 0.1.0

- Introduced the consumer-neutral `com.lingkyn.xr-baseline` identity.
- Moved runtime/editor namespaces and assemblies to `Lingkyn.Unity.XrBaseline`.
- Added a package-owned Sandbox initialization menu, tests, sample, and evidence
  boundaries for independent compile versus headset behavior.
