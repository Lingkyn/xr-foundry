# Quality tiers verification contract

## Source gate

- Every derivation input is positive, public, and role-bounded in
  [`source-manifest.json`](source-manifest.json).
- No consumer project, private performance profile, product frame-rate target,
  or prior product quality-switching code is a derivation input. The Settings
  family's option/profile/applicator model and the Persistence family's
  save/load and migration model in this repository are context only: they are
  seams a consumer's hook may compose on, and they contribute no derivation
  input and no evidence for tier, capability, or frame-budget semantics.
- A person confirms the source URLs, page paths, section anchors, versions,
  the license, and the maintenance state of the open-source implementation
  before the admission record is signed, because the authoring environment
  could not fetch them.

## Core gate

Deterministic tests must cover:

- tier and device identity: `TierId` with a canonical form (lower-case
  segments joined by `.`), rejection of empty, whitespace, and malformed
  identifiers with a stable `identity.malformed` failure, and value equality
  between two identities with the same text; `DeviceProfileId` shares the same
  rules and has no equality with a `TierId` of the same text; a `DeviceProfileId`
  is a validated identity this family defines for its own capability and preset
  keying and is never equal to, derived from, or checked against this
  repository's own build/verification compatibility-profile ids;
- a closed tier declaration with guard rails: a `QualityTierDefinition`
  declares (id, a refresh-rate guard rail as a closed positive range in hertz,
  a render-scale guard rail as a closed positive range, a closed
  `FoveationLevel` value from a small closed set (`off`, `low`, `medium`,
  `high`), an MSAA guard rail from a closed sample-count set, a shadow budget
  and a post-processing budget each as a closed non-negative range, and a
  label); a declaration with an inverted range, a non-positive refresh-rate or
  render-scale bound, a foveation level outside the closed set, or an MSAA
  value outside the closed sample-count set is rejected with
  `tier.declaration.invalid`;
- an immutable `TierRegistry` built by explicit registration of a validated
  declaration: registering an id twice is rejected with `tier.duplicate`, a
  rejected registration leaves the registry unchanged, and a built registry
  enumerates its tiers in canonical id order and cannot be mutated afterwards;
- a `DeviceCapabilityDescriptor` per device profile built the same explicit
  way: a closed set of supported refresh rates, a supported render-scale
  range, a closed set of supported foveation levels, and a closed set of
  supported MSAA sample counts; a descriptor with an empty supported set, an
  inverted range, or a duplicate device profile id is rejected with
  `capability.declaration.invalid`;
- a closed override-field set: `set_override`'s field name is drawn from a
  closed set (`refresh_rate`, `render_scale`, `foveation_level`, `msaa`,
  `shadow_budget`, `post_processing_budget`); a field name outside this set is
  rejected with `value.out_of_range` naming the field, distinct from a value
  inside the set but outside its guard rail, which the same code also names;
- typed intents applied to an immutable `QualityState`: `select_tier` (device
  profile id, tier id), `set_override` (device profile id, one field name from
  the closed override-field set, and a value), `reset` (device profile id),
  and `apply_preset` (device profile id, tier id, treated as the device's
  declared default); `select_tier` or `apply_preset` naming an unregistered
  tier is rejected with `tier.unknown`, naming an unregistered device profile
  with `device.unknown`, and naming a tier whose refresh rate, render scale,
  foveation level, or MSAA value the named device's capability descriptor does
  not support is rejected with `capability.unsupported` before any state
  change, never partially applied; `set_override` supplying a value outside
  the selected tier's own guard rail is rejected with `value.out_of_range`,
  and a value the device's capability descriptor does not support is rejected
  with `capability.unsupported`; `reset` restores the device's last selected
  tier with no override and is a no-op returning success when no override was
  present;
- one active tier per device as the only state: a state holds, per device
  profile id, at most one selected tier id and at most one set of field
  overrides; a `select_tier` or `apply_preset` accepted for a device that
  already holds a selection replaces the tier id and clears every override for
  that device; `set_override` accepted for a device with no prior selection is
  rejected with `tier.unknown` naming no tier as selected;
- state immutability: a state is immutable, an accepted intent produces a new
  state and leaves the prior state intact, and a rejected intent leaves the
  prior state intact and returns it unchanged with the failure code;
- deterministic replay: the same intent sequence over the same registry,
  capability-descriptor set, and initial state produces an equal final state
  and an equal fingerprint; the fingerprint covers the tier-registry
  fingerprint, the capability-descriptor-set fingerprint, and the
  per-device-profile selected-tier-and-override entries in canonical
  device-profile order; two states reached by different intent sequences with
  the same net per-device selections have equal fingerprints;
- a frame-budget policy as plain data, not an engine type: a
  `FrameBudgetPolicy` declares a target frame time in milliseconds and a drop
  threshold (a maximum tolerated fraction of frames exceeding the target
  frame time over a declared sampling window), both as positive numeric
  values with no dependency on any renderer, subsystem, or platform type;
  constructing one with a non-positive target frame time or a drop threshold
  outside `[0, 1]` is rejected with `budget.declaration.invalid`;
- structured results with stable failure codes for a malformed identity, an
  invalid tier declaration, an invalid capability declaration, an invalid
  frame-budget declaration, a duplicate tier, an unknown tier, an unknown
  device, an unsupported capability, an out-of-range override value, and a
  stale expected revision, each carrying the offending id, device, or field
  name, plus a valid intent sequence passing clean with every outcome
  accepted;
- one intent channel for people and agents (LESSON-011): every
  `QualityIntent` (`select_tier`, `set_override`, `reset`, `apply_preset`)
  carries an `IntentActor` from the closed set `player`, `agent`, `replay`,
  `import`, and an optional expected revision; `QualityState` carries a
  monotonically increasing revision; a stale expected revision is rejected
  with `state.stale` before the intent's own rule runs and changes nothing;
  the actor never changes validation, so a player-issued and an agent-issued
  copy of the same intent take the same path and yield the same outcome and
  the same resulting state; and every outcome in the replay log carries the
  issuing actor and the revision after it.

The Core references no engine type, holds no `UnityEngine` type, reads no
render-pipeline asset, queries no display, and switches no renderer; refresh
rate, render scale, MSAA, shadow budget, post-processing budget, and the
frame-budget policy's target frame time and drop threshold are plain numeric
values, and a `DeviceProfileId`'s text enters and leaves the Core as an
opaque, validated string.

## Unity adapter gate

EditMode tests must cover:

- a thin adapter over the XR display subsystem for refresh rate, with
  explicit resolution: the adapter reads the active `XRDisplaySubsystem`
  through an explicit, injected accessor rather than a scene search or a
  singleton lookup, pinned to the validated Unity, XR display, and XR
  Interaction Toolkit versions; a missing or unresolved display subsystem
  reports `display.unresolved` with the route name, and construction throws
  with the same report, carrying a negative test for the missing subsystem
  (LESSON-004);
- explicit render-scale and foveation-level application through the same XR
  settings and display-subsystem seams, each behind its own accessor: setting
  a value the runtime reports as unsupported (queried through the
  capability-descriptor accessor before the write, never inferred from a
  silent no-op) reports `capability.unsupported.degraded` naming the runtime,
  the requested value, and the value actually applied, and never reports that
  the requested value took effect when it did not;
- an injectable `IRendererOutputSink` and `IDeviceCapabilityProbe` with
  in-memory test doubles so tests assert exactly what was requested and what
  the fake reported as applied for every accepted intent — the resolved
  device, the effective refresh rate, render scale, foveation level, MSAA
  sample count, and the shadow and post-processing budgets — without a device
  or a runtime dependency; the runtime never calls a route directly, only
  through the sink and the probe, and a rejected intent reaches neither;
- MSAA, shadow, and post-processing budgets applied through an explicit URP
  renderer-asset swap: a `QualityRendererAssetMap` binds each registered tier
  to an explicit `UniversalRenderPipelineAsset` reference constructed at
  authoring time, never resolved by name, path, or `Resources.Load`; a tier
  with no bound asset reports `renderer.asset.unbound` naming the tier, and
  construction throws with the same report, carrying a negative test for the
  unbound tier (LESSON-004);
- a frame-time sampler that reports measured frame time as plain data over an
  explicit sampling window with no claim of comfort or of a passed frame
  budget: the sampler reports the sampled frame times and the fraction
  exceeding the `FrameBudgetPolicy`'s target frame time as a computed value
  the caller reads, and it never asserts, logs, or returns a "comfortable" or
  "certified" verdict;
- an explicit diagnostic for every by-name or optional resolution: the
  display-subsystem accessor, an `XRSettings` accessor, a render-pipeline
  asset reference, or an Adaptive Performance provider query the adapter
  resolves by name, by serialized field, or by an optional lookup must report
  an explicit failure with a stable code when the target is missing or
  unsupported, carrying a negative test for the missing target; silence or a
  reported success is a defect (LESSON-004);
- a hook the Settings family may consume, exposed as one public seam and
  nothing more: a pure function from a Settings-owned option value to a
  `select_tier` or `set_override` intent, taking the current registry,
  capability descriptor, and device profile id as explicit parameters; the
  Unity adapter assembly references no Settings type, and the hook's own
  tests construct the intent and assert its shape without invoking any
  Settings package;
- construction with explicit references only and no polling: a runtime is
  built from an explicit registry, an explicit capability-descriptor set, an
  explicit renderer-asset map, an explicit device-capability probe, and an
  explicit output sink; no scene singleton, static instance, scene search, or
  reflection discovery resolves any of them; the runtime is driven only by
  explicit `select_tier`, `set_override`, `reset`, and `apply_preset` calls it
  receives and holds no per-frame update loop that re-applies a tier on its
  own; two independently constructed runtimes over two sinks that share no
  state are tested side by side, so a `select_tier` in one changes no device,
  selection, or sink call of the other;
- explicit route selection as configuration, never platform detection inside
  the family: the XR display subsystem route, the URP renderer-asset route,
  and an optional Adaptive Performance feedback route are separate seams
  behind the adapter's public surface, and which one (or which ordered set) a
  runtime uses is the consumer's explicit wiring at construction time, never
  a runtime check of the active platform or build target inside the adapter.

No test claims that a device held a measured frame rate, that a render scale
or foveation level looked correct, or that a build passed store certification.

## Claim ceiling

An EditMode run proves identity validation, tier and capability declaration
validation, registry and capability-descriptor-set construction, intent
application, one-selection-per-device state, state immutability, deterministic
replay, frame-budget-policy declaration validation, display-subsystem and
render-scale/foveation application to injected fakes, renderer-asset-map
resolution, and the Settings hook's pure conversion, for the tuple recorded in
the compatibility profile. It proves the plumbing only: that an accepted
select_tier, set_override, reset, or apply_preset intent reached the injected
device probe and renderer sink with the exact device, tier, override, and
computed refresh rate, render scale, foveation level, MSAA, shadow, and
post-processing values the Core resolved. A stable frame rate, comfort, and
store certification are receipts from a device build, never package claims;
no test in this family's gates claims a device held a measured frame rate or
that a build passed certification. Whether a tier's refresh rate, render
scale, or foveation level is comfortable, legible, or fast enough on a named
device, and any named-device or named-runtime capability behavior, are human
judgements or real store-submission outcomes recorded in a Device Lab receipt
that binds the observation to a full commit SHA, the exact dependency tuple,
the named device profile and runtime, the tier-registry and
capability-descriptor fingerprint, the posture, the measured duration, the
named build target and graphics API, and the tester identity; `not_tested` is
never evidence.
