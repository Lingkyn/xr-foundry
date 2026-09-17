# Changelog

## Unreleased

- Closed the `Initialize Sandbox` unresolved-warning clause (XB-06) with a
  root-injectable seam. The Editor assembly gains the internal
  `SandboxInitializationOptions` (project root, scene path, `NewSceneMode`, save
  flag, rig source resolver) and `VrBaselineProjectLayout` (every generated path
  derived from one root; `Default` reproduces the fixed `Assets/_Project`
  constants), plus internal overloads on `XrBaselineMenu.InitializeSandbox`,
  `VrBaselineConfigAccess`, `VrBaselineAssetsSetup`, `VrBaselineAssetFactory`,
  `VrBaselineScenePlacer`, `VrBaselineEnclosureSetup`,
  `VrBaselineScaleReferenceSetup`, `XrPrefabFactory`, and `GenericXrRigFactory`.
  Every existing public signature delegates to the default layout, and the menu
  item calls `InitializeSandbox(SandboxInitializationOptions.Default)`, so the
  user-facing behaviour, paths, and log lines are unchanged. The Editor assembly
  now declares `InternalsVisibleTo("Lingkyn.XrBaseline.Editor.Tests")` in
  `Editor/AssemblyInfo.cs`, matching the runtime assembly. Added three EditMode
  tests in
  `XrBaselineSandboxInitializationTests`:
  `InitializeSandboxResetsStaleKeysAndWarnsOnceWhenAKeyWasReported` (a stale
  pre-seeded key is gone before the injected resolver runs, the resolver's
  reported key survives, and exactly one `xr_baseline_initialized_with_unresolved`
  line ends the run), `InitializeSandboxLogsCleanSuccessWhenNoKeyWasReported`
  (one `xr_baseline_initialized` line, no unresolved line), and
  `DefaultOptionsMatchTheFixedProjectPathConstants`. The tests run the whole
  pipeline against a disposable `Assets/__XrBaselineTests_<guid>` root and an
  additive scene, deleted in `TearDown`. Reading the source while wiring the
  seam showed that a missing XRI Starter Assets rig source logs a plain
  `xr_baseline:` warning without reporting a diagnostics key, so that state
  alone still ends in `xr_baseline_initialized`; the tests therefore inject a
  resolver that reports through `XrBaselineDiagnostics.Unresolved`, and the
  rig-source behaviour is left unchanged. The test assembly now holds nine
  tests. All C# in this change is authored and has not been compiled or executed
  in a Unity Editor.
- Added the EditMode test `MissingGrabInteractableWarnsOncePerComponent` for the
  `GrabbableHoverVisual` once-per-component warning clause (XB-07). The runtime
  component gains an internal `TryResolveGrabInteractable()` seam that re-runs the
  same by-name resolution as `Awake`, `OnEnable`, and `LateUpdate`, and the runtime
  assembly now declares `InternalsVisibleTo("Lingkyn.XrBaseline.Editor.Tests")`
  in `Runtime/AssemblyInfo.cs`. No public API change. The test assembly now holds
  six tests. Not yet executed in a Unity Editor. The `Initialize Sandbox`
  unresolved-warning clause (XB-06) stays open: the menu targets the fixed
  `Assets/_Project` scene and asset paths, so it has no disposable-root entry yet.
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
