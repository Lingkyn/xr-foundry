# Unity reference system composition

This directory contains the first XFCM composition root:

- `foundry.project.json` declares fixed components, variant choices, required
  capabilities, and source-bound cross-family adapters.
- `foundry.lock.json` pins the deterministic structural resolution, including
  component manifest hashes, adapter source hashes, and dependency order.
- `consumer/` contains the typed binding implementations and their bounded Unity
  integration harness.

The reference selects the Inventory UGUI renderer and XR UGUI surface. UI Toolkit
and XR UI Toolkit remain first-class alternatives in `component-catalog.json`; they
are not cumulative dependencies.

Check the lock with:

```bash
python scripts/compose_system.py --check --json
```

The v0.2 lock proves 13-component structural resolution and binds all three
consumer-owned adapter sources. It keeps `runtime_ready` false because the local
Unity experiment covers only the eight packages needed at the binding endpoints.
That experiment proves Editor import/script compilation and the named semantic
integration tests for one exact revision, including a production
`LocalFileSaveStore` recovery from a zero-byte primary file to a valid backup in a
temporary directory. It does not prove a player build, all 13 components, general
filesystem durability, XR input, renderer behavior, headset behavior, or
named-device evidence. See the
[double-loop result receipt](../../../docs/validation/experiments/2026-09-04-xag-xfcm-01-double-loop-result.md).
