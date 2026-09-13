# Consumer lessons register

Status: **incubating repository standard**

The register turns problems observed through real consumer use and validation of
one package family into rules that every other live family must answer. It is
the mechanism by which the library learns once and applies the lesson everywhere,
instead of rediscovering the same gap family by family.

The machine authority is [`lessons-register.json`](lessons-register.json),
validated against [`lessons-register.schema.json`](lessons-register.schema.json)
by `scripts/validate_repository.py`. The repository contract fails when a live
family has not responded to a lesson.

## How a lesson is recorded

1. **Observed.** What actually happened, with evidence: a GitHub Issue or pull
   request, a validation record, or a source path. A consumer project may be the
   trigger, but it is never quoted as derivation material; the register records the
   gap the consumer exposed, not the consumer's implementation.
2. **Rule.** The generalized requirement, written so it can be checked against any
   family without knowing the originating one.
3. **Dispositions.** One entry per live package family (derived from each package's
   `foundry.component.json` `family`). The status is one of:

| Status | Meaning | Requires |
| --- | --- | --- |
| `adopted` | The current revision already satisfies the rule | A rationale naming the code, contract, or check that satisfies it |
| `gap` | The rule is violated or unaddressed today | A concrete `follow_up` naming the next action or Issue |
| `deferred` | The rule cannot apply yet (for example, no release to migrate from) | A `follow_up` stating the trigger that makes it apply |
| `not_applicable` | The family has no surface the rule can touch | A rationale explaining why |

A disposition binds the current revision only. It is not inherited by a later
version, another renderer, or another engine tuple, and it grants no maturity,
promotion, or release status.

## When to update the register

- A consumer, reviewer, device tester, or Agent reports a problem that is not
  specific to one product. Add a lesson and a disposition for every family.
- A new package family is admitted. Add its disposition to every existing lesson
  before its blueprint is scaffolded; repository validation enforces this once the
  family's first component manifest exists.
- A `gap` or `deferred` disposition is resolved. Change its status to `adopted`
  and point the rationale at the evidence.

Validate the result with:

```text
python scripts/validate_repository.py --json --run-contract-tests
```

## Current lessons

| ID | Lesson | Origin |
| --- | --- | --- |
| `LESSON-001` | Keep a closed, versioned classification separate from open tags | Inventory, [#85](https://github.com/Lingkyn/xr-foundry/issues/85) |
| `LESSON-002` | Consumers strand on old revisions when a breaking change ships without a migration path | Inventory, [#82](https://github.com/Lingkyn/xr-foundry/issues/82) |
| `LESSON-003` | An automated gate that only one workstation can run is a manual gate | Inventory, [#81](https://github.com/Lingkyn/xr-foundry/pull/81) |
| `LESSON-004` | Code that resolves upstream members by name must fail closed | Foundations, [#21](https://github.com/Lingkyn/xr-foundry/issues/21) |
| `LESSON-005` | Verification contracts drift from tests unless clause coverage is enumerated | Inventory, [XAG-INV-01](../../validation/experiments/2026-07-15-xag-inv-01-cursor-result.md) |
| `LESSON-006` | Multi-package Git consumers pin every sibling to one full SHA and declare the test harness | Inventory, [canonical consumer validation](../../validation/2026-07-15-canonical-nested-git-consumer.md) |
| `LESSON-007` | Renderer adapters expose a skin seam from their first version | Inventory, [#84](https://github.com/Lingkyn/xr-foundry/issues/84) |

The JSON file is authoritative for the per-family dispositions; this table is a
reading aid.
