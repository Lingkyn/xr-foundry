# Foundations package-family standard

Status: incubating reference, verification contract only

The foundations family holds the two consumer-neutral Unity packages every other
family assumes: `com.lingkyn.project-initializer` (folder, scene, prefab, anchor
document, validation, and build-preprocessor scaffold) and
`com.lingkyn.xr-baseline` (greybox Sandbox rig, props, configuration, interaction
affordances, and rig repair tools). Both are Editor tooling with a thin runtime
surface, so this standard is deliberately smaller than the system families: it
carries no positive-source manifest or architecture contract yet, only the
verification contract and its coverage record.

## Capability boundary

- Project Initializer owns the `Assets/_Project` directory contract, four baseline
  scenes, empty extension prefabs, project anchor documents, an Input Actions seed,
  an activation marker, and an idempotent `Validate` report with stable issue
  codes. It never compiles against a consumer runtime assembly.
- XR Baseline owns the Sandbox scene layout, greybox materials and props, the
  `VrBaselineConfig` asset, XR rig placement and repair, locomotion and affordance
  setup, and the smoke-build settings switch. It references XR Interaction Toolkit,
  Input System, XR Management, and OpenXR at the versions pinned in its package
  manifest and resolves several XRI, Input System, shader, and sample-asset members
  by name.

Neither package owns gameplay, product content, vendor SDK loaders, or consumer
assemblies. Consumer adapters may wrap the generated paths without changing package
identity.

## Evidence boundary

EditMode tests and independent compile evidence prove that a package resolves, that
its constants and defaults stay consumer-neutral, and that its Editor tools return
explicit diagnostics instead of silent success. They do not prove that a generated
consumer project runs, that a Sandbox rig tracks, grabs, teleports, or feels
comfortable on a headset, or that any tuple other than the compatibility profile's
Unity, XRI, Input System, and provider versions behaves the same. Headset claims
need a Device Lab receipt.

See also:

- [`verification-contract.md`](verification-contract.md)
- [`coverage-map.json`](coverage-map.json)
- [`../lessons/lessons-register.json`](../lessons/lessons-register.json)
  (foundations dispositions, including LESSON-004 on by-name resolution)

## Coverage record

[`coverage-map.json`](coverage-map.json) maps every clause of the two Editor gates
in the verification contract to named tests and lists the tests still missing. An
unmapped or partial clause is an open gap, not implied coverage, and the map is not
execution evidence.
