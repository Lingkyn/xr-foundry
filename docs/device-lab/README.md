# Public Device Lab V1

Device Lab lets contributors test an immutable public revision on hardware and
submit evidence without repository write access. It separates three authorities so
that hardware facts, capability expectations, and one execution are never blended:

1. a **Device Profile** identifies hardware family, OS family, runtime ID, admitted
   input routes, and whether claims may currently be requested;
2. a **Capability Test Plan** identifies allowed package compositions, target
   profiles, required checks, optional claims, and expected outcomes; and
3. an **Execution Receipt** binds one immutable revision and artifact to one exact
   package tuple, profile, plan, environment, check result, tester, and timestamp.

None of these grants repository permission or package promotion. A passing receipt
still requires independent review and a maintainer claim decision.

## Evidence path

```text
task:ready + needs-device:<profile>
-> profile claim_allowed=true
-> maintainer-confirmed claim lease
-> immutable revision + artifact SHA-256/ref/application ID
-> exact package tuple + capability test plan
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

- a public repository, non-zero full 40-character commit SHA, artifact SHA-256,
  public artifact reference, and application ID;
- exact domain, presentation, renderer-adapter, and XR-adapter package IDs and
  versions selected from one plan composition;
- one admitted Device Profile and Capability Test Plan;
- engine/version, runtime ID/version, device family/model, OS family/version, and
  exact input routes;
- every required check ID with a non-`not_tested` status, observation, and evidence
  reference whose kind, immutable SHA-256, and repository path or public HTTPS URL
  are recorded;
- every optional claim, defaulting to unsupported unless its mapped optional check
  passed and its required input route is admitted by both profile and receipt; and
- accountable GitHub tester identity and UTC timestamps.

`claims_supported` is derived from the selected composition, overall result,
optional-check results, and executed routes. `claims_not_supported` must enumerate
every remaining plan claim ID. The two lists must be disjoint and exhaustive; free
text, a sibling-renderer claim, and an undeclared headset claim are invalid.

The generic validator rejects profile/plan/composition/runtime/device/input
mismatches, missing required checks, untested required checks, duplicate checks,
unsupported optional claims, placeholders, and invented passing claims.

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
must be reviewed before use.

## Legacy compatibility

Historical package-specific Inventory XR receipts and their
`validate_inventory_xr_device_receipt` validator remain an immutable compatibility
surface. Do not rewrite their schema, check IDs, verdict, or CLI meaning. They may
be cited under `legacy_receipts`, but cannot establish new generic Device Lab
authority. A new generic receipt must independently satisfy the current profile,
plan, package tuple, artifact, environment, check, and claim gates.

During branch integration, retain both routes:

- legacy `--device-receipt` for the historical Inventory XR schema; and
- generic `--device-lab-receipt` for `xr-foundry.device_execution_receipt.v1`.

## Adding profiles and plans

Start with a Task Hall Issue and public source/compatibility comparison. A profile
change owns hardware/runtime/input facts and claim admission. A test-plan change
owns capability checks, package compositions, and optional claims. Do not add
capability steps to profiles or hardware assumptions to plans.
