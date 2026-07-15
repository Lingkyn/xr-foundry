# XAG-INV-01 Codex-to-Cursor Inventory handoff

Status: ready for one fresh-context Cursor execution attempt

Public task: <https://github.com/Lingkyn/xr-foundry/issues/45>

## Immutable boundary

- Base commit: `a25bbc7c9855ae4f094a58e43d8ab5ffdf37a7bf`
- Branch: `codex/cursor-inventory-handoff`
- Checkpoint: `XAG-INV-01`
- Device evidence: not required
- Accountable GitHub identity: `@Lingkyn`
- Execution assistant for this attempt: Cursor Agent CLI
- Merge and acceptance authority: not delegated to Cursor

The public Issue is the task authority. This handoff is a compact execution aid,
not a replacement for the Issue and not a requirement to preserve the originating
Agent's private reasoning.

## Observation already established

Inventory Core has broad domain, persistence, typed-state, and invariant coverage.
The experiment therefore asks for a **test-only material delta**, not new runtime
behavior. A candidate test is acceptable only when the current public Inventory
verification contract already states the invariant and the runtime already honors
it.

## Cursor duty

1. Start from a fresh session and read `AGENTS.md`, Issue #45, and
   `docs/standards/inventory/verification-contract.md`.
2. Inspect the current Inventory Core tests. Identify one material existing
   invariant that lacks a focused negative test.
3. Record the observation and why it is or is not safe to add the test. Earlier
   text in this handoff is reference material; correct it explicitly if evidence
   contradicts it.
4. If safe, add exactly one focused NUnit test to the allowed test file. If no
   safe delta exists, leave the test file unchanged and record the blocker.
5. Write the result receipt at the only allowed report path and run the repository
   validator.
6. Stop. Do not commit, push, edit GitHub, or expand scope.

## Allowed write paths

- `packages/unity/systems/inventory/com.lingkyn.inventory.core/Tests/Editor/InventoryAggregateTests.cs`
- `docs/validation/experiments/2026-07-15-xag-inv-01-cursor-result.md`

Everything else is read-only.

## Required result receipt

The result file must contain these concise sections:

- Public inputs read
- Existing invariant selected
- Evidence and counterexamples considered
- Material delta or blocker
- Files changed
- Commands and observed results
- Non-claims and limitations
- Exact next action for independent Codex review

Do not publish private chain-of-thought or a session transcript. Publish only the
inspectable reasoning summary needed for another contributor to review the result.

## Acceptance handoff

Cursor cannot mark this checkpoint complete. Codex will inspect the diff, verify
the allowed paths, assess whether the chosen invariant was already public, and run
the relevant Unity and repository checks. The Issue will record success, partial
success, or failure with evidence.
