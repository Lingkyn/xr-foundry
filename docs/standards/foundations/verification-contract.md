# Foundations verification contract

This contract is minimal on purpose. It names the behaviors an EditMode test can
prove for each foundation package and the claim ceiling that no package test can
raise. Each clause maps to named tests in
[`coverage-map.json`](coverage-map.json).

## Project Initializer Editor gate

`com.lingkyn.project-initializer`, test assembly
`Lingkyn.ProjectInitializer.Editor.Tests`.

- Every baseline scene path is under `IndieDirectoryContract.ProjectRoot` and
  contains no consumer product name.
- Every required folder is under the project root, uses forward slashes with no
  trailing slash, is unique, and contains no consumer product name.
- Running the folder scaffold twice reuses every existing folder and creates
  nothing new the second time.
- `Validate` reports each missing required folder, baseline scene, and activation
  marker with a stable issue code (`INIT_FOLDER_MISSING`, `INIT_SCENE_MISSING`,
  `INIT_MARKER_MISSING`) and marks the folder and marker issues auto-fixable.
- The Editor and test assembly definitions reference no consumer runtime assembly.

## XR Baseline Editor gate

`com.lingkyn.xr-baseline`, test assembly `Lingkyn.XrBaseline.Editor.Tests`.

- A freshly created `VrBaselineConfig` keeps continuous move disabled and every
  generated path constant stays under `Assets/_Project` with no consumer product
  name.
- Rig repair configures the caster that each `NearFarInteractor` references,
  raises a shorter cast distance to the configured value, and never lowers a longer
  authored value.
- An interactor without a resolvable far caster produces a failed result whose
  diagnostic names the interactor, never a silent success.
- A rig with no interactors succeeds, configures nothing, and claims nothing.
- Every by-name resolution of an XRI, Input System, shader, or sample-asset member
  reports through `XrBaselineDiagnostics.Unresolved` once per key, and the key set
  can be reset.
- `Initialize Sandbox` resets the key set first and ends with an
  `xr_baseline_initialized_with_unresolved` warning when any key was reported.
- `GrabbableHoverVisual` warns once per component when XR Interaction Toolkit,
  `XRGrabInteractable`, or `isHovered` cannot be resolved instead of idling.

## Claim ceiling

An EditMode run proves only the tuple recorded in the compatibility profile. It
cannot prove that a generated consumer project builds or runs, that a Sandbox rig
tracks, grabs, teleports, or renders comfortably on any headset, or that a
different Unity, XRI, Input System, or provider version resolves the same members.
Headset behavior needs a Device Lab receipt bound to a full commit SHA; a passing
test suite is never that receipt.
