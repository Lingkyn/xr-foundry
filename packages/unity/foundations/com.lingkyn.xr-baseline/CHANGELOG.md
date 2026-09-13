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
- Documentation only: expanded the README with a quick start, the public
  runtime/editor surface, generated-path conventions, and non-goals. No API,
  version, maturity, or evidence change.

## 0.1.0

- Introduced the consumer-neutral `com.lingkyn.xr-baseline` identity.
- Moved runtime/editor namespaces and assemblies to `Lingkyn.Unity.XrBaseline`.
- Added a package-owned Sandbox initialization menu, tests, sample, and evidence
  boundaries for independent compile versus headset behavior.
