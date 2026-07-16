# XAG-INV-01 Cursor result receipt

Status: accepted after bounded Cursor execution and independent Codex review

## Public inputs read

- `AGENTS.md`
- GitHub Issue #45 (`Lingkyn/xr-foundry`), including maintainer lease on base
  `a25bbc7c9855ae4f094a58e43d8ab5ffdf37a7bf` / handoff
  `e531eb27b7bf382b6b14272b72e9df08f2916d5a`
- `docs/validation/experiments/2026-07-15-xag-inv-01-handoff.md`
- `docs/standards/inventory/verification-contract.md` §3 Core behavior gate
- `packages/unity/systems/inventory/com.lingkyn.inventory.core/Tests/Editor/InventoryAggregateTests.cs`
- `packages/unity/systems/inventory/com.lingkyn.inventory.core/Tests/Editor/InventoryInvariantTests.cs`
- Runtime confirmation (read-only):
  `InventoryMutationPlanner.ApplyAdd` returns `MutationFailure.UnknownDefinition`
  when the catalog lacks the definition; `InventoryAggregate.Execute` returns
  `Failure(...)` without committing working state or incrementing revision

## Existing invariant selected

Verification contract requires deterministic coverage of **duplicate IDs and
unresolved definitions**, and of **failed-operation immutability**.

Duplicate unique instances already have a focused negative test
(`UniqueInstancesRequireIdentityAndRejectDuplicates`). No Editor test asserted
`MutationFailure.UnknownDefinition` for an Add of an unresolved definition ID,
nor that such a rejection leaves snapshot and revision unchanged.

## Evidence and counterexamples considered

- Grep of Core `Tests/` found no uses of `UnknownDefinition`, `UnknownContainer`,
  `SourceEmpty`, or related focused rejection codes.
- Handoff claim that Core already has broad coverage is accepted as direction;
  the unresolved-definition negative path was a remaining material gap under the
  public contract, not a missing runtime behavior.
- Alternate gaps (empty remove → `SourceEmpty`; unknown container;
  invalid transfer/slot) are also public and unimplemented in tests, but only one
  delta is allowed; unresolved definitions pairs directly with the already-tested
  duplicate-ID clause in the contract.
- No runtime change was required: planner and aggregate already honor rejection
  without mutation.

## Material delta or blocker

Added one focused NUnit test:

`UnresolvedDefinitionAddIsRejectedWithoutMutation`

It seeds one admitted stack, attempts Add of `"unknown-item"`, and asserts
`UnknownDefinition`, unchanged reported and aggregate revisions, and unchanged
snapshot state.

## Files changed

- `packages/unity/systems/inventory/com.lingkyn.inventory.core/Tests/Editor/InventoryAggregateTests.cs`
- `docs/validation/experiments/2026-07-15-xag-inv-01-cursor-result.md`

## Commands and observed results

`python` was unavailable on this host (`exit 127`); used `python3`.

```text
python3 scripts/validate_repository.py --json
```

Observed (`exit 0`, ~36s):

```json
{
  "schema": "xr-foundry.repository_validation.v1",
  "root": "<isolated-worktree>",
  "status": "pass",
  "errors": []
}
```

Unity EditMode execution of the new NUnit test was not run here (no Unity test
host in this Cursor duty); Codex owns that rerun.

## Non-claims and limitations

- Does not claim device, XR, presentation, or package-promotion readiness.
- Does not claim the Unity EditMode suite was executed in this worktree.
- Does not expand coverage to other untested rejection codes listed above.
- Cursor did not commit, push, edit GitHub, approve, or merge.

## Exact next action for independent Codex review

1. Diff only the two allowed paths against handoff commit
   `e531eb27b7bf382b6b14272b72e9df08f2916d5a`.
2. Confirm the invariant is already public in
   `docs/standards/inventory/verification-contract.md` and already implemented in
   Core runtime.
3. Run the Core Editor tests (or record the exact unavailable Unity gate) and
   re-run `python scripts/validate_repository.py --json`.
4. Record success, partial success, or failure on Issue #45; Cursor cannot close
   the checkpoint.

## Independent Codex acceptance

Reviewed at `2026-07-15T17:21:04Z` against handoff commit
`e531eb27b7bf382b6b14272b72e9df08f2916d5a`.

- Write-boundary audit: pass; exactly the two allowed paths changed.
- Public-contract audit: pass; the selected unresolved-definition and
  failed-operation immutability invariants already exist in section 3 of the
  Inventory verification contract, and the runtime already implements them.
- Review hardening: Codex added assertions that the seed mutation succeeds and
  that the result's before/after revisions and the aggregate's actual revision
  all remain unchanged.
- Focused Unity `6000.3.19f1` EditMode rerun: pass, 1/1 tests, 0 failed;
  result XML SHA-256
  `23410942C20BEBCE5E127F337D372AACBF43CEBFED134A3F5A9F278D9ED537D5`.
- Full Inventory Core EditMode rerun: pass, 22/22 tests, 0 failed;
  result XML SHA-256
  `83B8434359EE78352A50EE9BCCCE5CE15AF9ED85DDCE17A1F433C58F3911261F`.
- Independent repository validator rerun: pass, `errors: []`.
- Privacy audit: pass after replacing the validator's local worktree root with
  the placeholder shown above. Raw Unity logs and XML remain local because Unity
  embeds local filesystem paths in them.

Acceptance: **success** for this bounded cross-Agent experiment. This proves a
fresh Cursor session could recover a public Inventory task, make a constrained
material delta, stop at its delegated authority boundary, and hand the result to
Codex for independent acceptance. It does not prove unrestricted autonomous
merging, device behavior, or every future Agent pairing.
