# Quality Tiers Walkthrough (domain-only sample)

Status: authored, unexecuted, same as the rest of `staging/quality-tiers/`.

This sample needs no asset, scene, or `UnityEngine` API. It builds a tier
registry and a device capability set by explicit registration, applies a
`select_tier` / `set_override` / `reset` / `apply_preset` intent sequence,
replays the identical sequence to show the resulting fingerprint is
deterministic (QC-09), and shows a capability-gated `select_tier` failing
closed with `capability.unsupported` before any state change.

Run `QualityTiersWalkthroughSample.Run()` from a plain C# host (a console app
or an EditMode test) once this package compiles inside a Unity project; the
Core has no engine dependency and does not run inside a scene.

## Non-claims

- No device, frame rate, or renderer is touched here. Nothing in this sample
  claims a headset held a refresh rate, a render scale looked correct, or a
  frame budget was met.
- The device capability sets shown here are illustrative, not a statement
  about any named product or headset's real supported values.
