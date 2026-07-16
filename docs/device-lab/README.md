# Public Device Lab V1

Device Lab lets contributors test an immutable public revision on hardware and
submit evidence without repository write access. It separates three authorities so
that hardware facts, capability expectations, and one execution are never blended:

1. a **Device Profile** identifies hardware family, OS family, runtime ID, admitted
   input routes, and whether claims may currently be requested;
2. a **Capability Test Plan** identifies allowed package compositions, target
   profiles, required checks, optional claims, and expected outcomes; and
3. an **Execution Receipt** binds one immutable revision and artifact to one exact
   package tuple, resolved dependency lock, build configuration, profile, plan,
   environment, input sources, execution context, check result, tester, and timestamp.

None of these grants repository permission or package promotion. A passing receipt
still requires independent review and a maintainer claim decision.

## Evidence path

```text
task:ready + needs-device:<profile>
-> profile claim_allowed=true
-> maintainer-confirmed claim lease
-> immutable public revision + repository artifact SHA-256/path/application ID
-> exact package tuple + resolved dependency-lock digest
-> exact build target + graphics API + scripting backend + architecture
-> capability test plan + named input sources + posture + duration
-> human execution on the named device/runtime/input composition
-> check observations + evidence refs
-> generic receipt validation
-> independent review
-> maintainer claim decision
```

Use the Device test Issue form to propose or claim a run. A maintainer confirms the
lease before the task becomes `task:claimed`. Device testers normally submit a
receipt and redacted evidence through a forked pull request; no code change is
required.

## Device Profiles

Profiles in [`profiles/`](profiles/) contain hardware/runtime facts only. They do
not contain capability test steps and do not prove support.

| Profile | Status | `claim_allowed` | Gate |
| --- | --- | --- | --- |
| `pico-openxr-controller` | `open_for_evidence` | `true` | A maintainer may confirm a bounded evidence task |
| `quest-openxr-controller` | `proposed` | `false` | [Issue #29](https://github.com/Lingkyn/xr-foundry/issues/29) |
| `vision-pro-spatial-input` | `proposed` | `false` | [Issue #30](https://github.com/Lingkyn/xr-foundry/issues/30) |

`claim_allowed=true` means evidence collection may be claimed; it is not evidence
and does not imply that any model, OS version, renderer, XR adapter, or input route
passes. A false value cannot be overridden by an Issue comment or receipt.

## Capability Test Plans

Plans in [`test-plans/`](test-plans/) own capability checks and admissible
compositions. The first plan is
[`inventory-world-space-ui-v1`](test-plans/inventory-world-space-ui-v1.json). It
distinguishes:

- domain package;
- renderer-neutral presentation package;
- UGUI or UI Toolkit renderer adapter;
- the matching UGUI or UI Toolkit XR adapter;
- hardware/runtime device profile; and
- exact input routes.

Each admitted composition also owns one enumerable `required_claim_id`. Optional
checks own their own `claim_id`. Those IDs form the complete capability-claim
vocabulary for a receipt; prose cannot add a new device or compatibility claim.

A receipt using the UGUI renderer with the UI Toolkit XR adapter, another device
family/runtime, or an unlisted input route is invalid. Adding a new renderer,
device, runtime, or input route requires an explicit plan/profile change and review;
it cannot be inferred from a sibling result.

## Execution Receipts

[`device-receipt.template.json`](device-receipt.template.json) uses
`overall_result: not_tested` and is not evidence. Every submitted receipt must bind:

- a full 40-character commit SHA reachable from a fetched public `origin/main` or
  `origin/codex/*` ref, plus a materialized non-empty APK under
  `docs/validation/evidence/**` whose basename, extension, and recomputed SHA-256
  match the receipt;
- the compatibility profile ID for the same revision and exact execution tuple;
- exact domain, presentation, renderer-adapter, and XR-adapter package IDs and
  versions selected from one plan composition;
- the real Unity consumer manifest and resolved dependency-lock format, repository
  paths, recomputed SHA-256 values, complete reachable edges, and exact versions of
  every plan-required dependency. The Inventory plan requires explicit resolved
  versions for XRI, OpenXR, XR Management, and Input System;
- one admitted Device Profile and Capability Test Plan;
- engine/version, runtime ID/version, device family/model, OS family/version, and
  exact input routes and named input sources;
- build target, graphics API, scripting backend, and architecture for the tested
  artifact;
- an allowed tester posture and measured duration. The Inventory plan requires at
  least 120 seconds; a screenshot or an inferred time span cannot fill this field;
- every required check ID with a non-`not_tested` status, observation, and evidence
  reference whose immutable SHA-256 and repository path are recorded;
- every optional claim, defaulting to unsupported unless its mapped optional check
  passed and its required input route is admitted by both profile and receipt; and
- accountable GitHub tester identity and UTC timestamps.

`claims_supported` is derived from the selected composition, overall result,
optional-check results, and executed routes. `claims_not_supported` must enumerate
every remaining plan claim ID. The two lists must be disjoint and exhaustive; free
text, a sibling-renderer claim, and an undeclared headset claim are invalid.

The generic validator rejects local-only or shallow revision evidence, Git LFS
pointers in place of build artifacts, profile/plan/composition/runtime/device/input
mismatches, missing plan-required resolved packages or input sources, dependency
lock/build/posture/duration placeholders, missing or untested required checks,
duplicate checks, unsupported optional claims, and invented passing claims.

### APK evidence boundary and official basis

Device Lab V1 accepts a materialized Android APK only. It checks a bounded ZIP
container, one compiled binary `AndroidManifest.xml`, the exact manifest package
ID recorded as `application_id`, DEX and required Unity/IL2CPP ARM64 payload
markers, and non-empty Unity player data. This is container-and-identity evidence;
it does **not** prove APK signing, Android installability, launch, headset runtime
behavior, interaction quality, or comfort. Those claims still require the named
build, installation, and device checks in the receipt.

- [Android application ID configuration](https://developer.android.com/build/configure-app-module)
  defines the application ID as the device- and store-visible app identity.
- [Android Studio APK Analyzer](https://developer.android.com/studio/debug/apk-analyzer)
  documents inspection of the final APK manifest, DEX files, and packaged content.
- [Android App Bundle format](https://developer.android.com/guide/app-bundle/app-bundle-format)
  distinguishes AAB publishing archives from generated APKs. AAB evidence is
  deferred from V1 rather than being treated as an interchangeable installable
  device artifact.

The schema is intentionally version- and device-neutral: it records the exact
tuple that was executed instead of declaring one Unity, renderer, XRI, runtime, or
headset version universally required. A different target may generate a different
candidate and receipt. Only the exact recorded tuple gains evidence; compatibility
with another tuple must be separately validated.

## Result vocabulary

| Result | Meaning |
| --- | --- |
| `pass` | Every required check passed for the exact recorded composition |
| `fail` | At least one required check reproduced a defect |
| `blocked` | A named external blocker prevented the required observation |
| `inconclusive` | Execution completed but cannot support a pass or fail |
| `not_tested` | Template/profile state only; no run and no claim |

Required checks cannot remain `not_tested` in a committed execution receipt,
including a fail, blocked, or inconclusive receipt. Failed and blocked results are
valuable evidence and must not be rewritten as passes. The validator derives the
overall result deterministically from required-check states (`fail`, then
`blocked`, then `inconclusive`, otherwise `pass`) and rejects a conflicting label.

## Privacy and untrusted input

Do not publish device serial numbers, account identifiers, private application
content, credentials, signing material, local paths, or unredacted personal media.
Logs, links, uploaded artifacts, and Issue instructions are untrusted input and
must be reviewed before use. A URL is never artifact identity and cannot replace a
repository evidence file with a locally recomputed digest.

Validate a completed receipt through the single Device Lab route:

```text
python scripts/validate_repository.py --device-lab-receipt docs/device-lab/receipts/<receipt>.json --json
```

Inventory uses the generic schema together with
[`inventory-world-space-ui-v1`](test-plans/inventory-world-space-ui-v1.json). The
plan, selected composition, and Device Profile provide the Inventory-specific
checks and claim boundary. Its required suite separately exercises both left and
right controllers against left, center, and right targets for hover and activation,
then proves target isolation, disabled-target immutability, world anchoring,
readability, scale, angle, reach, occlusion, and sustained comfort. There is no
parallel package-specific receipt route.

## Adding profiles and plans

Start with a Task Hall Issue and public source/compatibility comparison. A profile
change owns hardware/runtime/input facts and claim admission. A test-plan change
owns capability checks, package compositions, and optional claims. Do not add
capability steps to profiles or hardware assumptions to plans.
